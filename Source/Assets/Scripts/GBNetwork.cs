using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

public class GBNetwork:MonoBehaviour {
    public enum Role {Offline,Host,Client}
    public Role role;
    public string status="Local play",peerName="FRIEND";
    public bool connected,ready,peerReady;
    public int rttMs,latencySimulation;public float dropSimulation;
    public long snapshotsSent,snapshotsReceived,inputsReceived;
    public GBInput remoteInput;
    public float lastInput;
    TcpListener listener;TcpClient socket;CancellationTokenSource cancel;
    ClientWebSocket websocket;GBTunnel tunnel;CancellationTokenSource connectionCancel;long handshakeStarted;
    readonly object transportGate=new object();int sessionId;
    float connectStarted;
    const int MaxPacket=131072;
    public string InviteLink {get{return tunnel!=null?tunnel.InviteUrl:"";}}
    public bool InviteReady {get{return role==Role.Host&&tunnel!=null&&tunnel.Ready;}}
    public string InviteStatus {get{return tunnel!=null?tunnel.Status:status;}}
    BlockingCollection<byte[]> outgoing;
    readonly ConcurrentQueue<byte[]> incoming=new ConcurrentQueue<byte[]>();
    readonly ConcurrentQueue<string> errors=new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> diagnostics=new ConcurrentQueue<string>();
    public string LastConnectionDiagnostic {get;private set;}="";
    readonly List<Delayed> delayed=new List<Delayed>();
    class Delayed {public float at;public byte[] bytes;}
    string password,nonce;double lastSnapshot;int inputSequence;float heartbeat;
    public const int Protocol=12;
    public bool Authority {get{return role!=Role.Client;}}
    void Awake(){role=Role.Offline;}
    public void Host(int port,string secret) {
        HostTransport(port,secret,IPAddress.Any);
    }
    int HostTransport(int port,string secret,IPAddress bindAddress){
        Stop();role=Role.Host;password=secret;status="Waiting for your friend…";cancel=new CancellationTokenSource();
        int id=sessionId;var token=cancel.Token;
        try {
            var server=new TcpListener(bindAddress,port);listener=server;server.Start(2);
            Task.Run(async()=>{
                while(!token.IsCancellationRequested){
                    try {
                        var client=await server.AcceptTcpClientAsync();
                        lock(transportGate){if(!Active(id,token)||socket!=null){client.Close();continue;}socket=client;}
                        var challenge=Guid.NewGuid().ToString("N");
                        lock(transportGate){if(!Active(id,token)){client.Close();break;}nonce=challenge;}
                        BeginSocket(client,token,id);
                        SendForSession(Packet(7,w=>{w.Write(Protocol);w.Write(challenge);}),id,token,client);
                    }catch(Exception e){ReportError(id,token,"Hosting ended: "+e.Message);break;}
                }
            });
            return ((IPEndPoint)server.LocalEndpoint).Port;
        }catch(Exception e){ReportError(id,token,"Could not host: "+e.Message);return 0;}
    }
    public void HostInvite(){
        string secret=GBTunnel.GenerateSecret();int port=HostTransport(0,secret,IPAddress.Loopback);
        if(port==0)return;
        try {
            string root=Directory.GetParent(Path.GetFullPath(Application.dataPath)).Parent.FullName;
            tunnel=new GBTunnel();
            tunnel.Start(port,secret,Path.Combine(root,"Network","cloudflared.exe"));
            status="Creating your private invite…";
        }catch(Exception e){status="Could not create invite: "+e.Message;Stop(false);}
    }
    public static bool TryParseInvite(string invite,out Uri websocketUri,out string secret){return GBTunnel.TryParseInvite(invite,out websocketUri,out secret);}
    public static bool ValidateInvite(string invite){Uri uri;string secret;return TryParseInvite(invite,out uri,out secret);}
    public bool JoinInvite(string invite){
        Uri uri;string secret;
        if(!TryParseInvite(invite,out uri,out secret)){status="Invalid invite • paste the complete link";return false;}
        Stop();role=Role.Client;GBApp.I.Epoch=-1;GBApp.I.LastHostTime=0;password=secret;status="Connecting to invite…";
        cancel=new CancellationTokenSource();connectStarted=Time.realtimeSinceStartup;var token=cancel.Token;int id=sessionId;
        Task.Run(async()=>{
            Exception failure=null;
            using(var connecting=CancellationTokenSource.CreateLinkedTokenSource(token)){
                // A newly issued quick-tunnel hostname can need time to reach
                // recursive DNS caches. Retry without bypassing DNS or TLS.
                connecting.CancelAfter(TimeSpan.FromSeconds(90));
                for(int attempt=0;attempt<30&&!connecting.IsCancellationRequested;attempt++){
                    var client=new ClientWebSocket();client.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
                    lock(transportGate){if(!Active(id,token)){client.Dispose();return;}websocket=client;}
                    try{
                        await client.ConnectAsync(uri,connecting.Token);
                        if(!Active(id,token)){client.Abort();client.Dispose();return;}
                        BeginWebSocket(client,token,id);return;
                    }catch(Exception e){failure=e;client.Dispose();ReportDiagnostic(id,token,"Invite connect attempt "+(attempt+1)+": "+SafeException(e,secret));}
                    if(attempt<29&&!connecting.IsCancellationRequested){try{await Task.Delay(Math.Min(3000,1000+attempt*300),connecting.Token);}catch(OperationCanceledException){break;}}
                }
            }
            ReportError(id,token,"Invite connection failed • check the link and try again"+(failure is PlatformNotSupportedException?" (WebSocket unavailable)":""));
        });
        return true;
    }
    public void Join(string address,int port,string secret) {
        Stop();role=Role.Client;GBApp.I.Epoch=-1;GBApp.I.LastHostTime=0;password=secret;status="Connecting…";cancel=new CancellationTokenSource();var token=cancel.Token;int id=sessionId;connectStarted=Time.realtimeSinceStartup;
        Task.Run(async()=>{TcpClient c=null;try{c=new TcpClient();lock(transportGate){if(!Active(id,token)){c.Close();return;}socket=c;}await c.ConnectAsync(address,port);if(!Active(id,token)){c.Close();return;}BeginSocket(c,token,id);}catch(Exception e){c?.Close();ReportError(id,token,"Connection failed: "+e.Message);}});
    }
    bool Active(int id,CancellationToken token){return id==Volatile.Read(ref sessionId)&&!token.IsCancellationRequested;}
    void ReportError(int id,CancellationToken token,string error){lock(transportGate){if(Active(id,token))errors.Enqueue(error);}}
    void ReportDiagnostic(int id,CancellationToken token,string diagnostic){lock(transportGate){if(Active(id,token))diagnostics.Enqueue(diagnostic);}}
    static string SafeException(Exception error,string secret){
        var message=new StringBuilder();int depth=0;
        while(error!=null&&depth++<5){
            if(message.Length>0)message.Append(" -> ");
            string text=error.Message??"";if(!string.IsNullOrEmpty(secret))text=text.Replace(secret,"[room token]");
            text=System.Text.RegularExpressions.Regex.Replace(text,@"#grip=[A-Za-z0-9_-]+","#grip=[redacted]");
            text=text.Replace('\r',' ').Replace('\n',' ');if(text.Length>400)text=text.Substring(0,400);
            message.Append(error.GetType().Name).Append(": ").Append(text);error=error.InnerException;
        }
        return message.ToString();
    }
    void ReceiveForSession(byte[] bytes,int id,CancellationToken token){lock(transportGate){if(!Active(id,token))return;if(incoming.Count>=300)throw new IOException("Peer sent too many messages");incoming.Enqueue(bytes);}}
    void SendForSession(byte[] bytes,int id,CancellationToken token,TcpClient expectedSocket){lock(transportGate){if(Active(id,token)&&ReferenceEquals(socket,expectedSocket))SendImmediate(bytes);}}
    BlockingCollection<byte[]> NewOutgoing(int id,CancellationToken token){
        lock(transportGate){if(!Active(id,token))return null;var queue=new BlockingCollection<byte[]>(256);outgoing=queue;return queue;}
    }
    void BeginSocket(TcpClient client,CancellationToken token,int id) {
        client.NoDelay=true;client.ReceiveTimeout=15000;client.SendTimeout=10000;
        lock(transportGate){if(!Active(id,token)){client.Close();return;}connectionCancel=CancellationTokenSource.CreateLinkedTokenSource(token);token=connectionCancel.Token;handshakeStarted=System.Diagnostics.Stopwatch.GetTimestamp();}
        var stream=client.GetStream();var queue=NewOutgoing(id,token);if(queue==null){client.Close();return;}
        Task.Run(()=>{try{foreach(var bytes in queue.GetConsumingEnumerable(token)){var length=BitConverter.GetBytes(bytes.Length);stream.Write(length,0,4);stream.Write(bytes,0,bytes.Length);}}catch(Exception e){ReportError(id,token,"Connection ended: "+e.Message);}});
        Task.Run(async()=>{try{while(!token.IsCancellationRequested){var header=await Exact(stream,4,token);int length=BitConverter.ToInt32(header,0);if(length<1||length>MaxPacket)throw new IOException("Invalid packet length");ReceiveForSession(await Exact(stream,length,token),id,token);}}catch(Exception e){ReportError(id,token,"Friend disconnected: "+e.Message);}});
    }
    void BeginWebSocket(ClientWebSocket client,CancellationToken token,int id){
        var queue=NewOutgoing(id,token);if(queue==null){client.Abort();client.Dispose();return;}
        // ClientWebSocket permits exactly one sender and one receiver in parallel.
        Task.Run(async()=>{try{foreach(var bytes in queue.GetConsumingEnumerable(token)){if(bytes.Length<1||bytes.Length>MaxPacket)throw new IOException("Invalid packet length");await client.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Binary,true,token);}}catch(Exception e){ReportDiagnostic(id,token,"Invite send ended: "+SafeException(e,password));ReportError(id,token,"Room connection ended • ask your friend for a new invite");}});
        Task.Run(async()=>{
            var buffer=new byte[MaxPacket];
            try {
                while(!token.IsCancellationRequested){
                    int length=0;WebSocketReceiveResult result;
                    do {
                        result=await client.ReceiveAsync(new ArraySegment<byte>(buffer,length,buffer.Length-length),token);
                        if(result.MessageType==WebSocketMessageType.Close)throw new IOException("The invite connection closed");
                        if(result.MessageType!=WebSocketMessageType.Binary)throw new IOException("Only binary game packets are accepted");
                        length+=result.Count;if(length>=MaxPacket&&!result.EndOfMessage)throw new IOException("Invite packet is too large");
                    }while(!result.EndOfMessage);
                    if(length<1)throw new IOException("Empty game packet");var bytes=new byte[length];Buffer.BlockCopy(buffer,0,bytes,0,length);ReceiveForSession(bytes,id,token);
                }
            }catch(Exception e){ReportDiagnostic(id,token,"Invite receive ended: "+SafeException(e,password));ReportError(id,token,"Host room closed • ask for a new invite");}
        });
    }
    static async Task<byte[]> Exact(NetworkStream stream,int n,CancellationToken token){var bytes=new byte[n];int at=0;while(at<n){int read=await stream.ReadAsync(bytes,at,n-at,token);if(read==0)throw new IOException("The connection was closed");at+=read;}return bytes;}
    static byte[] Packet(byte type,Action<BinaryWriter> write){using(var ms=new MemoryStream()){using(var w=new BinaryWriter(ms,Encoding.UTF8,true)){w.Write(type);write(w);}return ms.ToArray();}}
    string Proof(string challenge){using(var h=new HMACSHA256(Encoding.UTF8.GetBytes(password??"")))return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(challenge)));}
    void SendImmediate(byte[] b){var queue=outgoing;if(queue==null)return;try{if(!queue.IsAddingCompleted&&!queue.TryAdd(b))errors.Enqueue("Connection queue is full • reconnect");}catch(ObjectDisposedException){}catch(InvalidOperationException){}}
    void Send(byte[] b,bool simulate=false){if(simulate&&dropSimulation>0&&UnityEngine.Random.value<dropSimulation)return;if(simulate&&latencySimulation>0)delayed.Add(new Delayed{at=Time.realtimeSinceStartup+latencySimulation*.0005f,bytes=b});else SendImmediate(b);}
    public void SetReady(bool value){ready=value;if(role==Role.Client)Send(Packet(2,w=>w.Write(value)));}
    public void StartMatch(){if(role==Role.Host&&connected&&ready&&peerReady){GBApp.I.BeginMatch(GBApp.I.SelectedArena,GBApp.I.SelectedMode);}}
    public void Lobby(){if(role==Role.Host&&connected)Send(Packet(6,w=>{w.Write(GBApp.I.SelectedArena);w.Write(GBApp.I.SelectedMode);w.Write(ready);w.Write(GBApp.I.PlayerName);}),false);}
    public void SendInput(GBInput i){if(role!=Role.Client||!connected)return;i=i.Sanitize();i.sequence=++inputSequence;i.stamp=Time.realtimeSinceStartupAsDouble;Send(Packet(3,w=>{w.Write(i.sequence);w.Write(i.stamp);w.Write(i.x);w.Write(i.z);w.Write(i.ax);w.Write(i.az);w.Write(i.hx);w.Write(i.hy);w.Write(i.hz);w.Write(i.flags);}),true);}
    public void Pump() {
        string diagnostic;while(diagnostics.TryDequeue(out diagnostic)){LastConnectionDiagnostic=diagnostic;Debug.Log("GRIP_NETWORK "+diagnostic);}
        if(role==Role.Host&&tunnel!=null){if(!string.IsNullOrEmpty(tunnel.Error))errors.Enqueue(tunnel.Error);else if(!connected)status=tunnel.Ready?"Invite ready • share it with your friend":tunnel.Status;}
        if(role==Role.Host&&!connected&&socket!=null&&handshakeStarted>0&&(System.Diagnostics.Stopwatch.GetTimestamp()-handshakeStarted)/(double)System.Diagnostics.Stopwatch.Frequency>10)DropPendingPeer();
        for(int i=0;i<delayed.Count;)if(Time.realtimeSinceStartup>=delayed[i].at){SendImmediate(delayed[i].bytes);delayed.RemoveAt(i);}else i++;
        string error;if(errors.TryDequeue(out error)){
            if(role==Role.Host&&!connected&&socket!=null&&(error.StartsWith("Connection ended:",StringComparison.Ordinal)||error.StartsWith("Friend disconnected:",StringComparison.Ordinal))){DropPendingPeer();}
            else {status=error;GBApp.I.Toast(error,6);Stop(false);GBApp.I.ReturnToMenu();return;}
        }
        byte[] bytes;int budget=100;while(budget-->0&&incoming.TryDequeue(out bytes)) {
            try {using(var r=new BinaryReader(new MemoryStream(bytes))) {
                byte type=r.ReadByte();
                if(type==7&&role==Role.Client){int version=r.ReadInt32();string challenge=r.ReadString();if(version!=Protocol)throw new IOException("Different game versions");Send(Packet(1,w=>{w.Write(Proof(challenge));w.Write(GBApp.I.PlayerName);w.Write(Protocol);}));}
                else if(type==1&&role==Role.Host&&!connected){string proof=r.ReadString();string name=r.ReadString();int version=r.ReadInt32();if(proof!=Proof(nonce)||version!=Protocol)throw new IOException("Wrong room password or game version");peerName=name.Length>24?name.Substring(0,24):name;connected=true;lastInput=Time.realtimeSinceStartup;status="Friend connected";Send(Packet(5,w=>{w.Write(GBApp.I.PlayerName);w.Write(Protocol);}));Lobby();}
                else if(type==5&&role==Role.Client){peerName=r.ReadString();if(r.ReadInt32()!=Protocol)throw new IOException("Different game versions");connected=true;GBApp.I.ClientSnapshotAt=Time.realtimeSinceStartup;status="Connected to "+peerName;}
                else if(!connected)continue;
                else if(type==2&&role==Role.Host){peerReady=r.ReadBoolean();Lobby();}
                else if(type==3&&role==Role.Host){var cmd=new GBInput{sequence=r.ReadInt32(),stamp=r.ReadDouble(),x=r.ReadSingle(),z=r.ReadSingle(),ax=r.ReadSingle(),az=r.ReadSingle(),hx=r.ReadSingle(),hy=r.ReadSingle(),hz=r.ReadSingle(),flags=r.ReadInt32()}.Sanitize();if(cmd.sequence>remoteInput.sequence){remoteInput=cmd;lastInput=Time.realtimeSinceStartup;inputsReceived++;}}
                else if(type==4&&role==Role.Client){ReadSnapshot(r);snapshotsReceived++;}
                else if(type==6&&role==Role.Client){GBApp.I.SelectedArena=Mathf.Clamp(r.ReadInt32(),0,3);GBApp.I.SelectedMode=Mathf.Clamp(r.ReadInt32(),0,2);peerReady=r.ReadBoolean();peerName=r.ReadString();}
            }}catch(Exception e){if(role==Role.Host&&!connected&&socket!=null){DropPendingPeer();GBApp.I.Toast("Room authentication failed • waiting for your friend",4);}else errors.Enqueue("Session error: "+e.Message);}
        }
        if(role==Role.Host&&connected&&Time.realtimeSinceStartup-lastInput>.35f)remoteInput=default;
        if(role==Role.Client&&!connected&&Time.realtimeSinceStartup-connectStarted>100)errors.Enqueue("Connection timed out • check the invite and try again");
        if(role==Role.Client&&connected&&Time.realtimeSinceStartup-GBApp.I.ClientSnapshotAt>8)errors.Enqueue("Host timed out; return to the lobby to reconnect");
        if(role==Role.Host&&connected&&Time.realtimeSinceStartup-lastInput>20)errors.Enqueue("Friend timed out; host a new room to reconnect");
        if(role==Role.Host&&connected&&GBApp.I.World!=null&&Time.realtimeSinceStartupAsDouble-lastSnapshot>=1.0/30){lastSnapshot=Time.realtimeSinceStartupAsDouble;SendSnapshot();}
        if(role==Role.Host&&connected&&!GBApp.I.InMatch){heartbeat+=Time.unscaledDeltaTime;if(heartbeat>1){heartbeat=0;Lobby();}}
    }
    void SendSnapshot() {
        var a=GBApp.I;Send(Packet(4,w=>{
            w.Write(Protocol);w.Write(a.Epoch);w.Write(Time.realtimeSinceStartupAsDouble);w.Write(a.CurrentArena);w.Write(a.CurrentMode);w.Write((int)a.Phase);w.Write(a.Scores[0]);w.Write(a.Scores[1]);w.Write(a.Remaining);w.Write(a.PinCount);w.Write(Mathf.Clamp(a.PinAttacker,-1,1));string pinStatus=a.PinStatus??"";w.Write(pinStatus.Length>100?pinStatus.Substring(0,100):pinStatus);w.Write(a.Arena.sudden);w.Write(a.Arena.suddenTime);w.Write(a.RoundMessage??"");
            w.Write(a.Registry.Count);foreach(var rb in a.Registry){w.Write(rb!=null&&rb.gameObject.activeSelf);Write(w,rb?rb.position:Vector3.zero);var q=rb?rb.rotation:Quaternion.identity;w.Write(q.x);w.Write(q.y);w.Write(q.z);w.Write(q.w);}
            w.Write(a.Wrestlers.Count);foreach(var p in a.Wrestlers){w.Write(p.stamina);w.Write(p.balance);w.Write(p.Down);w.Write(p.eliminated);w.Write(p.ActionFeedback);w.Write(p.GripStrain);w.Write(p.TargetMode=="Legs");bool leftShoulder,rightShoulder;p.ShoulderContact(out leftShoulder,out rightShoulder);w.Write(p.IsCovering);w.Write(p.PinTarget?p.PinTarget.player:-1);w.Write(leftShoulder);w.Write(rightShoulder);for(int h=0;h<2;h++){var target=p.hands[h].target;w.Write(target?a.Registry.IndexOf(target.Body):-1);Write(w,p.hands[h].localAnchor);}}
            w.Write(a.Arena.elapsed);w.Write(a.Arena.breakables.Count);foreach(var prop in a.Arena.breakables){w.Write(prop.broken);w.Write(prop.health);}w.Write(a.Arena.WarningSection);w.Write(a.PhaseSeconds);
        }),true);snapshotsSent++;
    }
    public static void Write(BinaryWriter w,Vector3 p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
    static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    public static Vector3 Read(BinaryReader r){var value=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(!Finite(value.x)||!Finite(value.y)||!Finite(value.z)||Mathf.Abs(value.x)>100000||Mathf.Abs(value.y)>100000||Mathf.Abs(value.z)>100000)throw new IOException("Invalid snapshot position");return value;}
    void ReadSnapshot(BinaryReader r) {
        if(r.ReadInt32()!=Protocol)throw new IOException("Snapshot version mismatch");int epoch=r.ReadInt32();double hostTime=r.ReadDouble();int arena=r.ReadInt32();int mode=r.ReadInt32();int phase=r.ReadInt32();int s0=r.ReadInt32();int s1=r.ReadInt32();float remaining=r.ReadSingle();float pin=r.ReadSingle();int pinAttacker=r.ReadInt32();string pinStatus=r.ReadString();if(pinAttacker<-1||pinAttacker>1||pinStatus.Length>100||!Finite(pin)||!Finite(remaining)||double.IsNaN(hostTime)||double.IsInfinity(hostTime))throw new IOException("Invalid pin match state");bool sudden=r.ReadBoolean();float st=r.ReadSingle();string message=r.ReadString();
        var a=GBApp.I;if(a.Epoch!=epoch||a.World==null){a.BuildWorld(arena,mode,true);a.Epoch=epoch;}
        bool whistle=a.RingRestartCountdown&&phase==(int)GBApp.GamePhase.Fighting;
        a.Phase=(GBApp.GamePhase)phase;a.InMatch=a.Phase!=GBApp.GamePhase.Menu;a.Scores[0]=s0;a.Scores[1]=s1;a.Remaining=remaining;a.PinCount=pin;a.PinAttacker=pinAttacker;a.PinStatus=pinStatus;a.Arena.sudden=sudden;a.Arena.suddenTime=st;a.RoundMessage=message;
        if(whistle)a.Sfx.Play("whistle",.65f);
        int count=r.ReadInt32();if(count!=a.Registry.Count)throw new IOException("Arena objects differ; use the same build");
        for(int i=0;i<count;i++){bool active=r.ReadBoolean();var p=Read(r);var q=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;if(!Finite(q.x)||!Finite(q.y)||!Finite(q.z)||!Finite(q.w)||!Finite(norm)||norm<.0001f||norm>4)throw new IOException("Invalid snapshot rotation");a.Registry[i].gameObject.SetActive(active);a.TargetPositions[i]=p;a.TargetRotations[i]=q;}
        int players=r.ReadInt32();if(players!=a.Wrestlers.Count)throw new IOException("Player state mismatch");foreach(var p in a.Wrestlers){p.stamina=r.ReadSingle();p.balance=r.ReadSingle();if(!Finite(p.stamina)||p.stamina<0||p.stamina>GBWrestler.MaxStamina||!Finite(p.balance)||p.balance<0||p.balance>100)throw new IOException("Invalid wrestler state");bool down=r.ReadBoolean();p.downUntil=down?Time.time+.2f:0;p.eliminated=r.ReadBoolean();p.ApplyCombatSnapshot(r.ReadString(),r.ReadSingle(),r.ReadBoolean());bool covering=r.ReadBoolean();int targetPlayer=r.ReadInt32();bool leftShoulder=r.ReadBoolean(),rightShoulder=r.ReadBoolean();if(targetPlayer<-1||targetPlayer>=a.Wrestlers.Count||targetPlayer==p.player)throw new IOException("Invalid pin target");p.RemotePinState(covering,targetPlayer,leftShoulder,rightShoulder);for(int h=0;h<2;h++){int target=r.ReadInt32();p.hands[h].localAnchor=Read(r);p.hands[h].target=target>=0&&target<a.Registry.Count?a.Registry[target].GetComponent<GBTarget>():null;}}
        a.Arena.elapsed=r.ReadSingle();int propCount=r.ReadInt32();if(propCount!=a.Arena.breakables.Count)throw new IOException("Prop state mismatch");foreach(var prop in a.Arena.breakables){bool broken=r.ReadBoolean();prop.health=r.ReadSingle();if(broken&&!prop.broken)prop.ReplicateBreak();}a.Arena.WarnSection(r.ReadInt32());a.SetPhaseSeconds(r.ReadSingle());
        if(a.Wrestlers.Count>=2){a.Wrestlers[0].playerName=peerName;a.Wrestlers[1].playerName=a.PlayerName;}
        a.ClientSnapshotAt=Time.realtimeSinceStartup;a.LastHostTime=hostTime;
    }
    public void Stop(bool clearStatus=true) {
        lock(transportGate){
            Interlocked.Increment(ref sessionId);
            if(cancel!=null){cancel.Cancel();cancel.Dispose();cancel=null;}
            if(connectionCancel!=null){connectionCancel.Cancel();connectionCancel.Dispose();connectionCancel=null;}handshakeStarted=0;
            try{listener?.Stop();}catch{}listener=null;try{socket?.Close();}catch{}socket=null;
            try{websocket?.Abort();websocket?.Dispose();}catch{}websocket=null;
            try{outgoing?.CompleteAdding();outgoing?.Dispose();}catch{}outgoing=null;
            byte[] b;while(incoming.TryDequeue(out b)){}string e;while(errors.TryDequeue(out e)){}
            while(diagnostics.TryDequeue(out e)){}LastConnectionDiagnostic="";
        }
        try{tunnel?.Dispose();}catch{}tunnel=null;
        connected=ready=peerReady=false;remoteInput=default;role=Role.Offline;delayed.Clear();lastSnapshot=0;inputSequence=0;heartbeat=0;connectStarted=0;nonce=null;if(clearStatus)status="Local play";
    }
    void DropPendingPeer(){
        lock(transportGate){
            if(connectionCancel!=null){connectionCancel.Cancel();connectionCancel.Dispose();connectionCancel=null;}
            try{socket?.Close();}catch{}socket=null;try{outgoing?.CompleteAdding();outgoing?.Dispose();}catch{}outgoing=null;
            byte[] packet;while(incoming.TryDequeue(out packet)){}string error;while(errors.TryDequeue(out error)){}nonce=null;handshakeStarted=0;peerReady=false;remoteInput=default;
        }
        status=tunnel!=null&&tunnel.Ready?"Invite ready • waiting for your friend":"Waiting for your friend…";
    }
    void OnDestroy(){Stop();}
}
