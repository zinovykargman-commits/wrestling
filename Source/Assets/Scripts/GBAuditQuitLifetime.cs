using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

public partial class GBAudit {
    // Keep this OS handle rooted until the game process ends. In particular,
    // the forced-exit fixture must not simulate cleanup by disposing it first.
    GBWindowsChildJob quitOwnedJob;
    Process quitOwnedHelper;
    [Serializable] class QuitReady {
        public string mode,startedUtc,error;
        public int gamePid,helperPid,listenerPort;
        public bool jobAttached,listenerListening;
    }
    IEnumerator QuitLifetimeRun(){
        var ready=new QuitReady{mode=Arg("--quit-mode","quit"),startedUtc=DateTime.UtcNow.ToString("o"),gamePid=Process.GetCurrentProcess().Id};
        try {
            if(Application.platform!=RuntimePlatform.WindowsPlayer)throw new InvalidOperationException("Windows-only process lifetime fixture");
            if(ready.mode!="quit"&&ready.mode!="x"&&ready.mode!="forced")throw new InvalidOperationException("Unknown quit mode");
            app.Network.Host(0,"quit-audit");
            var listener=(TcpListener)typeof(GBNetwork).GetField("listener",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Network);
            ready.listenerPort=((IPEndPoint)listener.LocalEndpoint).Port;
            ready.listenerListening=ready.listenerPort>0;
            quitOwnedJob=GBWindowsChildJob.Create();
            if(quitOwnedJob==null)throw new InvalidOperationException("Windows child ownership unavailable");
            // Block cmd on stdin until it belongs to the game-owned job. Its
            // harmless ping descendant therefore inherits that same job too.
            quitOwnedHelper=new Process{StartInfo=new ProcessStartInfo{
                FileName=Path.Combine(Environment.SystemDirectory,"cmd.exe"),
                Arguments="/d /q /c \"set /p gripQuitGate= & ping.exe -n 120 127.0.0.1 >nul\"",
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true
            }};
            if(!quitOwnedHelper.Start())throw new InvalidOperationException("Could not start owned test helper");
            ready.helperPid=quitOwnedHelper.Id;
            quitOwnedJob.Assign(quitOwnedHelper);ready.jobAttached=quitOwnedJob.Attached;
            quitOwnedHelper.StandardInput.WriteLine("ready");quitOwnedHelper.StandardInput.Flush();quitOwnedHelper.StandardInput.Close();
        }catch(Exception error){
            ready.error=error.ToString();
            // Assignment failures cannot leave the gated fixture helper behind.
            if(quitOwnedHelper!=null)try{if(!quitOwnedHelper.HasExited)quitOwnedHelper.Kill();}catch{}
        }
        File.WriteAllText(Path.Combine(directory,"quit-ready.json"),JsonUtility.ToJson(ready,true));
        UnityEngine.Debug.Log("QUIT_LIFETIME_READY "+JsonUtility.ToJson(ready));
        if(!string.IsNullOrEmpty(ready.error)){Application.Quit(1);yield break;}
        if(ready.mode=="quit"){
            yield return Wait(.5f);
            // Identical application quit action used by the in-game Quit button.
            Application.Quit();
        }
        float deadline=Time.realtimeSinceStartup+15;
        while(Time.realtimeSinceStartup<deadline){GC.KeepAlive(quitOwnedJob);yield return null;}
        File.WriteAllText(Path.Combine(directory,"quit-timeout.json"),"{\"error\":\"External close or application quit did not end this fixture within15 seconds\"}");
        Application.Quit(2);
    }
}
