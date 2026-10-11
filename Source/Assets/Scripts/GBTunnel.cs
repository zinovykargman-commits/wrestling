using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// The helper is bundled and pinned by the release. Only the host runs it; the
// friend connects to its public HTTPS/WebSocket endpoint inside the game.
public sealed class GBTunnel:IDisposable {
    readonly object gate=new object();
    readonly CancellationTokenSource readinessCancel=new CancellationTokenSource();
    Process process;GBWebSocketBridge bridge;
    string invite="",status="Starting private room...",error="",publicUrl,secret,configurationRoot;
    bool registered,disposed,started,resolving;
    public string InviteUrl {get{lock(gate)return invite;}}
    public string Status {get{lock(gate)return status;}}
    public string Error {get{lock(gate)return error;}}
    public bool Ready {get{lock(gate)return !disposed&&invite.Length>0&&registered;}}
    public int BridgePort {get{return bridge==null?0:bridge.Port;}}
    public static string GenerateSecret(){
        byte[] bytes=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
    public static bool TryParseInvite(string text,out Uri endpoint,out string roomSecret){
        endpoint=null;roomSecret=null;Uri input;
        if(!Uri.TryCreate((text??"").Trim(),UriKind.Absolute,out input))return false;
        if((input.Scheme!="https"&&input.Scheme!="wss")||input.Port!=443||input.UserInfo.Length>0||input.Query.Length>0||input.AbsolutePath!="/room")return false;
        if(!Regex.IsMatch(input.Host,@"^[a-z0-9][a-z0-9-]*\.trycloudflare\.com$",RegexOptions.IgnoreCase))return false;
        string fragment=input.Fragment;
        if(!fragment.StartsWith("#grip=",StringComparison.Ordinal))return false;
        string value=fragment.Substring(6);
        if(!Regex.IsMatch(value,@"^[A-Za-z0-9_-]{43}$"))return false;
        try{if(Convert.FromBase64String(value.Replace('-','+').Replace('_','/')+"=").Length!=32)return false;}catch{return false;}
        endpoint=new Uri("wss://"+input.Host+"/room");roomSecret=value;return true;
    }
    public void Start(int tcpPort,string roomSecret,string cloudflaredPath){
        lock(gate){if(started||disposed)throw new InvalidOperationException("Tunnel already started or disposed");started=true;secret=roomSecret;}
        var startupClock=Stopwatch.StartNew();Task.Run(()=>StartupWatchdog(startupClock,readinessCancel.Token));
        try{
            if(!Regex.IsMatch(roomSecret??"",@"^[A-Za-z0-9_-]{43}$"))throw new IOException("The room secret is invalid");
            string executable=Path.GetFullPath(cloudflaredPath);
            if(!File.Exists(executable))throw new IOException("The private-room helper is missing; reinstall the Windows package");
            bridge=new GBWebSocketBridge(tcpPort);
            configurationRoot=Path.Combine(Path.GetTempPath(),"GRIP_BREAK-Tunnel-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configurationRoot);string config=Path.Combine(configurationRoot,"config.yml");
            File.WriteAllText(config,"# GRIP BREAK isolated quick-tunnel configuration\n");
            var info=new ProcessStartInfo(executable,"--config \""+config+"\" tunnel --url http://127.0.0.1:"+bridge.Port+" --no-autoupdate"){
                UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(executable)
            };
            var child=new Process{StartInfo=info,EnableRaisingEvents=true};process=child;
            child.OutputDataReceived+=(sender,args)=>ReadLine(args.Data);child.ErrorDataReceived+=(sender,args)=>ReadLine(args.Data);
            child.Exited+=(sender,args)=>{lock(gate){if(!disposed){error="The private room closed. Host again to create a new invite link.";status=error;registered=false;}}readinessCancel.Cancel();};
            if(!child.Start())throw new IOException("The private-room helper could not start");
            child.BeginOutputReadLine();child.BeginErrorReadLine();
        }catch(Exception e){lock(gate){error=e.Message;status="Could not open private room: "+e.Message;}DisposeResources();}
    }
    async Task StartupWatchdog(Stopwatch clock,CancellationToken token){
        try{
            while(clock.Elapsed.TotalSeconds<150&&!token.IsCancellationRequested){
                lock(gate){if(disposed||error.Length>0||invite.Length>0)return;}
                await Task.Delay(1000,token);
            }
            lock(gate){
                if(!disposed&&!token.IsCancellationRequested&&error.Length==0&&invite.Length==0){
                    error=publicUrl==null||!registered
                        ?"Could not reach the private room service. Check your connection and host again."
                        :"The invite address took too long to become available. Host again to create a new link.";
                    status=error;
                }
            }
        }catch(OperationCanceledException){}
    }
    void ReadLine(string line){
        if(string.IsNullOrEmpty(line))return;
        lock(gate){
            if(disposed)return;
            Match match=Regex.Match(line,@"https://[a-z0-9][a-z0-9-]*\.trycloudflare\.com",RegexOptions.IgnoreCase);
            // Keep the first assigned hostname. Later process output cannot
            // redirect an in-progress readiness check to another room.
            if(match.Success&&publicUrl==null){
                string host=new Uri(match.Value).Host;
                if(!host.Equals("api.trycloudflare.com",StringComparison.OrdinalIgnoreCase)&&!host.Equals("www.trycloudflare.com",StringComparison.OrdinalIgnoreCase)){
                    publicUrl=match.Value;status="Connecting private room...";
                }
            }
            if(line.IndexOf("Registered tunnel connection",StringComparison.OrdinalIgnoreCase)>=0)registered=true;
            if(publicUrl!=null&&registered&&!resolving&&error.Length==0){
                resolving=true;status="Preparing invite address...";
                string selectedUrl=publicUrl;Task.Run(()=>WaitForAddress(selectedUrl,readinessCancel.Token));
            }
            // Do not turn recoverable transport retries into a permanent error.
            if(line.IndexOf("failed to request quick Tunnel",StringComparison.OrdinalIgnoreCase)>=0){error="Could not create the invite link. Check your connection and host again.";status=error;}
        }
    }
    async Task WaitForAddress(string selectedUrl,CancellationToken token){
        var clock=Stopwatch.StartNew();Task<IPAddress[]> lookup=null;
        try{
            string hostname=new Uri(selectedUrl).Host;
            while(clock.Elapsed.TotalSeconds<90&&!token.IsCancellationRequested){
                lock(gate){if(disposed||error.Length>0||publicUrl!=selectedUrl)return;}
                // Use the normal OS resolver. Reuse a pending lookup rather than
                // spawning overlapping DNS requests if that resolver is slow.
                if(lookup==null)lookup=Dns.GetHostAddressesAsync(hostname);
                int remaining=(int)Math.Max(1,(90-clock.Elapsed.TotalSeconds)*1000);
                Task completed=await Task.WhenAny(lookup,Task.Delay(Math.Min(3000,remaining),token));
                if(token.IsCancellationRequested)return;
                if(completed==lookup){
                    IPAddress[] addresses=null;
                    try{addresses=await lookup;}catch(System.Net.Sockets.SocketException){}catch(ArgumentException){}
                    lookup=null;
                    if(addresses!=null&&addresses.Length>0){
                        lock(gate){
                            if(!disposed&&registered&&error.Length==0&&publicUrl==selectedUrl&&!token.IsCancellationRequested){
                                invite=selectedUrl+"/room#grip="+secret;status="Invite link ready • copy it for your friend";
                            }
                        }
                        return;
                    }
                }
                remaining=(int)Math.Max(1,(90-clock.Elapsed.TotalSeconds)*1000);
                await Task.Delay(Math.Min(2000,remaining),token);
            }
            lock(gate){if(!disposed&&!token.IsCancellationRequested&&error.Length==0&&publicUrl==selectedUrl){
                error="The invite address did not become available. Host again to create a new link.";status=error;
            }}
        }catch(OperationCanceledException){}
        catch(Exception e){lock(gate){if(!disposed&&!token.IsCancellationRequested&&error.Length==0){error="Could not prepare the invite address: "+e.Message;status=error;}}}
    }
    void DisposeResources(){
        var child=Interlocked.Exchange(ref process,null);
        if(child!=null){try{if(!child.HasExited)child.Kill();}catch{}try{child.Dispose();}catch{}}
        bridge?.Dispose();bridge=null;
        if(configurationRoot!=null){
            // This newly-created folder owns one known file; never recursively
            // remove a user cloudflared configuration or another helper's data.
            try{File.Delete(Path.Combine(configurationRoot,"config.yml"));Directory.Delete(configurationRoot,false);}catch{}
            configurationRoot=null;
        }
    }
    public void Dispose(){
        lock(gate){if(disposed)return;disposed=true;registered=false;invite="";}
        readinessCancel.Cancel();
        DisposeResources();
    }
}
