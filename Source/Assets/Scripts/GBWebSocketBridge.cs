using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// HTTP/1.1 upgrade on a loopback socket avoids HttpListener URL ACLs. After
// upgrade, the framework handles WebSocket masking, fragmentation and controls.
public sealed class GBWebSocketBridge:IDisposable {
    const int MaximumPacket=131072,MaximumHeaders=8192;
    readonly CancellationTokenSource cancel=new CancellationTokenSource();
    readonly ConcurrentDictionary<int,TcpClient> clients=new ConcurrentDictionary<int,TcpClient>();
    readonly TcpListener listener;
    readonly int gamePort;
    int serial,active,disposed;
    public int Port {get;private set;}
    public string LastPeerError {get;private set;}

    public GBWebSocketBridge(int tcpPort){
        if(tcpPort<1||tcpPort>65535)throw new ArgumentOutOfRangeException("tcpPort");
        gamePort=tcpPort;listener=new TcpListener(IPAddress.Loopback,0);listener.Start(4);
        Port=((IPEndPoint)listener.LocalEndpoint).Port;
        Task.Run((Func<Task>)Accept);
    }
    async Task Accept(){
        try{
            while(!cancel.IsCancellationRequested){
                var client=await listener.AcceptTcpClientAsync();
                if(cancel.IsCancellationRequested){client.Close();break;}
                int id=Interlocked.Increment(ref serial);clients[id]=client;
                _=Task.Run(()=>Handle(client,id));
            }
        }catch(Exception e){if(!cancel.IsCancellationRequested)LastPeerError=e.Message;}
    }
    static async Task Respond(NetworkStream stream,string code,string body,CancellationToken token){
        byte[] content=Encoding.UTF8.GetBytes(body);
        byte[] headers=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+"\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: "+content.Length+"\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers,0,headers.Length,token);await stream.WriteAsync(content,0,content.Length,token);
    }
    static async Task<string> Headers(NetworkStream stream,CancellationToken token){
        using(var bytes=new MemoryStream()){
            var single=new byte[1];int end=0;
            while(bytes.Length<MaximumHeaders){
                if(await stream.ReadAsync(single,0,1,token)==0)throw new IOException("Incomplete room handshake");
                byte value=single[0];bytes.WriteByte(value);
                end=((end<<8)|value);if(end==0x0D0A0D0A)return Encoding.ASCII.GetString(bytes.ToArray());
            }
            throw new IOException("Room handshake is too large");
        }
    }
    static string Header(string[] lines,string name){
        string value=null;
        foreach(var line in lines){int colon=line.IndexOf(':');if(colon<1)continue;
            if(string.Equals(line.Substring(0,colon),name,StringComparison.OrdinalIgnoreCase)){
                if(value!=null)throw new IOException("Duplicate room handshake header");value=line.Substring(colon+1).Trim();
            }
        }
        return value;
    }
    static bool TokenContains(string value,string token){
        if(value==null)return false;foreach(var part in value.Split(','))if(string.Equals(part.Trim(),token,StringComparison.OrdinalIgnoreCase))return true;return false;
    }
    async Task Handle(TcpClient front,int id){
        TcpClient backend=null;WebSocket socket=null;bool reserved=false;
        using(var session=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token)){
            try{
                front.NoDelay=true;var stream=front.GetStream();string text;
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(session.Token)){
                    timeout.CancelAfter(10000);
                    using(timeout.Token.Register(()=>{if(timeout.IsCancellationRequested)try{front.Close();}catch{}}))text=await Headers(stream,timeout.Token);
                }
                string[] lines=text.Split(new[]{"\r\n"},StringSplitOptions.None);
                string[] request=lines[0].Split(' ');
                if(request.Length!=3||request[0]!="GET"||request[2]!="HTTP/1.1"){
                    await Respond(stream,"400 Bad Request","Invalid room request",session.Token);return;
                }
                if(request[1]!="/room"){
                    await Respond(stream,"404 Not Found","GRIP BREAK room service",session.Token);return;
                }
                if(!TokenContains(Header(lines,"Connection"),"Upgrade")||!string.Equals(Header(lines,"Upgrade"),"websocket",StringComparison.OrdinalIgnoreCase)){
                    await Respond(stream,"200 OK","Paste this GRIP BREAK room link into the game's Join screen.",session.Token);return;
                }
                string key=Header(lines,"Sec-WebSocket-Key");byte[] decoded;
                try{decoded=Convert.FromBase64String(key??"");}catch{decoded=new byte[0];}
                if(decoded.Length!=16||Header(lines,"Sec-WebSocket-Version")!="13"){
                    await Respond(stream,"400 Bad Request","Unsupported room connection",session.Token);return;
                }
                if(Interlocked.CompareExchange(ref active,1,0)!=0){
                    await Respond(stream,"409 Conflict","This room already has a connected player",session.Token);return;
                }
                reserved=true;backend=new TcpClient();backend.NoDelay=true;
                using(session.Token.Register(()=>{try{backend.Close();}catch{}})){
                    await backend.ConnectAsync(IPAddress.Loopback,gamePort);
                    string accept;
                    using(var hash=SHA1.Create())accept=Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    byte[] reply=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");
                    await stream.WriteAsync(reply,0,reply.Length,session.Token);
                    socket=WebSocket.CreateFromStream(stream,true,null,TimeSpan.FromSeconds(15));
                    Task send=ToWebSocket(backend.GetStream(),socket,session.Token),receive=ToGame(socket,backend.GetStream(),session.Token);
                    await Task.WhenAny(send,receive);session.Cancel();backend.Close();socket.Abort();
                    try{await Task.WhenAll(send,receive);}catch(Exception e){if(!cancel.IsCancellationRequested)LastPeerError=e.Message;}
                }
            }catch(Exception e){if(!cancel.IsCancellationRequested)LastPeerError=e.Message;}
            finally{
                session.Cancel();socket?.Dispose();backend?.Close();front.Close();
                TcpClient ignored;clients.TryRemove(id,out ignored);if(reserved)Interlocked.Exchange(ref active,0);
            }
        }
    }
    static async Task<byte[]> Exact(NetworkStream stream,int count,CancellationToken token){
        byte[] bytes=new byte[count];int at=0;
        while(at<count){int read=await stream.ReadAsync(bytes,at,count-at,token);if(read==0)throw new IOException("Room game connection closed");at+=read;}
        return bytes;
    }
    static async Task ToWebSocket(NetworkStream stream,WebSocket socket,CancellationToken token){
        while(!token.IsCancellationRequested){
            byte[] header=await Exact(stream,4,token);int length=BitConverter.ToInt32(header,0);
            if(length<1||length>MaximumPacket)throw new IOException("Invalid room packet length");
            byte[] bytes=await Exact(stream,length,token);
            await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Binary,true,token);
        }
    }
    static async Task ToGame(WebSocket socket,NetworkStream stream,CancellationToken token){
        byte[] buffer=new byte[16384];
        while(!token.IsCancellationRequested){
            using(var message=new MemoryStream()){
                WebSocketReceiveResult result;
                do{
                    result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);
                    if(result.MessageType==WebSocketMessageType.Close)return;
                    if(result.MessageType!=WebSocketMessageType.Binary||message.Length+result.Count>MaximumPacket)throw new IOException("Invalid room WebSocket message");
                    message.Write(buffer,0,result.Count);
                }while(!result.EndOfMessage);
                if(message.Length<1)throw new IOException("Empty room packet");
                byte[] bytes=message.ToArray(),header=BitConverter.GetBytes(bytes.Length);
                await stream.WriteAsync(header,0,4,token);await stream.WriteAsync(bytes,0,bytes.Length,token);
            }
        }
    }
    public void Dispose(){
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        cancel.Cancel();listener.Stop();foreach(var client in clients.Values)try{client.Close();}catch{}
    }
}
