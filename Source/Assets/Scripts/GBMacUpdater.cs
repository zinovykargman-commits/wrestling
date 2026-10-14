using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Process=System.Diagnostics.Process;

// The shipped installer treats release metadata as data. Only this owned .app
// is replaced; preferences live outside it and are never copied or removed.
public sealed class GBMacUpdater:MonoBehaviour {
    public const string Repository="zinovykargman-commits/macoswrestling";
    public const string BundleIdentifier="com.gripbreak.macoswrestling";
    public const string ArchiveName="GRIP_BREAK-macOS.zip";
    public const string ChecksumName="GRIP_BREAK-macOS-SHA256.txt";
    public const string BundleName="GRIP BREAK.app";
    public const string ExecutableName="GRIP BREAK";
    public const long MaximumArchive=1024L*1024*1024,MaximumExpanded=1536L*1024*1024;
    public string Status {get;private set;}="CHECK FOR UPDATE";
    public bool Busy {get;private set;}
    public bool HasUpdate {get;private set;}
    public string LatestVersion {get;private set;}="";
    public string LastJobDirectory {get;private set;}="";
    public Manifest AvailableManifest {get{return available;}}
    public event Action<InstallJob> InstallerLaunched;
    [Serializable] public class Manifest {public string version,tag,url,sha256;public long size;}
    [Serializable] class Asset {public string name,browser_download_url;public long size;}
    [Serializable] class Release {public string tag_name;public bool draft,prerelease;public Asset[] assets;}
    [Serializable] public class InstallJob {public string app,stage,root,backup,failed,version,token,evidence;public int parentPid;public bool audit,failRestart,failAck;}
    Manifest available;bool allowCurrentAudit;
    void Awake(){StartCoroutine(AcknowledgeRestart());}
    public void CheckOrInstall(){
        if(Busy)return;
        if(Application.platform!=RuntimePlatform.OSXPlayer){Status="MAC UPDATES REQUIRE THE MAC GAME";return;}
        Busy=true;StartCoroutine(HasUpdate&&available!=null?Install():Check());
    }
    public void AllowCurrentReleaseInDisposableAudit(){
        if(!AuditOwnedApp(BundleRoot(Application.dataPath)))throw new InvalidOperationException("Current-release test requires an automated disposable app clone.");
        allowCurrentAudit=true;
    }
    IEnumerator Check(){
        HasUpdate=false;available=null;Status="CHECKING GITHUB...";
        Release release=null;string error=null;
        using(var request=Request("https://api.github.com/repos/"+Repository+"/releases/latest",25)){
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success){Fail("COULD NOT CHECK MAC UPDATE • TRY AGAIN");yield break;}
            try{if(request.downloadHandler.data.Length>262144)throw new InvalidDataException("Release metadata is too large.");release=JsonUtility.FromJson<Release>(request.downloadHandler.text);}catch(Exception e){error=e.Message;}
        }
        Asset zip=null,checksum=null;
        if(error==null){try{
            if(release==null||release.draft||release.prerelease||!ValidVersion(release.tag_name)||release.assets==null)throw new InvalidDataException("No stable game release.");
            foreach(var asset in release.assets){
                if(asset.name==ArchiveName){if(zip!=null)throw new InvalidDataException("Duplicate game archive.");zip=asset;}
                if(asset.name==ChecksumName){if(checksum!=null)throw new InvalidDataException("Duplicate checksum asset.");checksum=asset;}
            }
            if(zip==null||checksum==null||zip.size<1||zip.size>MaximumArchive||checksum.size<64||checksum.size>16384||!ReleaseUrl(zip.browser_download_url,release.tag_name,ArchiveName)||!ReleaseUrl(checksum.browser_download_url,release.tag_name,ChecksumName))throw new InvalidDataException("Release assets are not ready.");
        }catch(Exception e){error=e.Message;}}
        if(error!=null){Fail("MAC RELEASE IS NOT READY • "+error);yield break;}
        string digest=null;
        using(var request=Request(checksum.browser_download_url,25)){
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success){Fail("COULD NOT READ MAC CHECKSUM • TRY AGAIN");yield break;}
            try{if(request.downloadHandler.data.Length!=checksum.size)throw new InvalidDataException("Checksum size differs.");digest=ParseChecksum(request.downloadHandler.text);}catch(Exception e){error=e.Message;}
        }
        if(error!=null){Fail("MAC UPDATE VERIFICATION FAILED • "+error);yield break;}
        var manifest=new Manifest{tag=release.tag_name,version=Normalize(release.tag_name),url=zip.browser_download_url,size=zip.size,sha256=digest};
        if(!ValidManifest(manifest)){Fail("INVALID MAC UPDATE METADATA");yield break;}
        LatestVersion=manifest.version;
        bool auditCurrent=allowCurrentAudit&&AuditOwnedApp(BundleRoot(Application.dataPath))&&Normalize(Application.version)==manifest.version;
        if(!IsNewer(manifest.version,Application.version)&&!auditCurrent){Status="UP TO DATE • "+Application.version;Busy=false;yield break;}
        available=manifest;HasUpdate=true;Status="UPDATE "+manifest.version+" READY • RESTART REQUIRED";Busy=false;
    }
    IEnumerator Install(){
        string app=null,root=null,zip=null,error=null;
        try{
            app=CanonicalDirectory(BundleRoot(Application.dataPath));ValidateBundle(app,Application.version);
            string parent=Directory.GetParent(app).FullName;
            root=Path.Combine(parent,".GRIP_BREAK-update-"+Guid.NewGuid().ToString("N"));
            if(Directory.Exists(root)||File.Exists(root))throw new IOException("Update staging already exists.");
            Directory.CreateDirectory(root);RunTool("/bin/chmod",new[]{"700",root},10000);
            zip=Path.Combine(root,"update.zip");LastJobDirectory=root;
        }catch(Exception e){error=e.Message;}
        if(error!=null){Fail("MOVE THE APP TO A WRITABLE FOLDER BEFORE UPDATING • "+error);yield break;}
        Status="DOWNLOADING MAC UPDATE...";
        using(var request=Request(available.url,240)){
            request.downloadHandler=new DownloadHandlerFile(zip){removeFileOnAbort=true};var operation=request.SendWebRequest();
            while(!operation.isDone){Status="DOWNLOADING UPDATE • "+Mathf.RoundToInt(request.downloadProgress*100)+"%";yield return null;}
            if(request.result!=UnityWebRequest.Result.Success){Fail("DOWNLOAD FAILED • YOUR APP IS UNCHANGED");yield break;}
        }
        Status="VERIFYING MAC UPDATE...";yield return null;
        InstallJob job=null;
        try{
            VerifyDownload(zip,available);ValidateArchive(zip);
            string stage=ExtractVerified(zip,Path.Combine(root,"stage"),available.version);
            job=MakeJob(app,stage,root,available.version,Process.GetCurrentProcess().Id,allowCurrentAudit);
            if(job.audit)job.evidence=Path.Combine(Directory.GetParent(app).FullName,"restart-evidence");
            WriteJob(job);
        }catch(Exception e){error=e.Message;}
        if(error!=null){Fail("UPDATE REJECTED • YOUR APP IS UNCHANGED • "+error);yield break;}
        Status="RESTARTING TO INSTALL...";yield return null;
        try{var installer=LaunchJob(job);if(installer==null)throw new IOException("The installer did not start.");InstallerLaunched?.Invoke(job);Application.Quit();}catch(Exception e){Fail("UPDATE RESTART FAILED • "+e.Message);}
    }
    void Fail(string message){Status=message;Busy=false;Debug.LogWarning("GRIP MAC UPDATE: "+message);}
    static UnityWebRequest Request(string url,int timeout){var r=UnityWebRequest.Get(url);r.timeout=timeout;r.redirectLimit=5;r.SetRequestHeader("User-Agent","GRIP-BREAK-Mac/"+Application.version);r.SetRequestHeader("Accept",url.StartsWith("https://api.github.com/",StringComparison.Ordinal)?"application/vnd.github+json":"application/octet-stream");return r;}
    public static string Normalize(string value){return string.IsNullOrEmpty(value)?"":value[0]=='v'||value[0]=='V'?value.Substring(1):value;}
    public static bool ValidVersion(string value){Version version;string s=Normalize(value);return Regex.IsMatch(s,@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")&&Version.TryParse(s,out version);}
    public static bool IsNewer(string candidate,string current){Version next,old;return ValidVersion(candidate)&&ValidVersion(current)&&Version.TryParse(Normalize(candidate),out next)&&Version.TryParse(Normalize(current),out old)&&next>old;}
    public static bool ReleaseUrl(string url,string tag,string asset){
        Uri uri;return ValidVersion(tag)&&Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.Port==443&&uri.UserInfo.Length==0&&uri.Query.Length==0&&uri.Fragment.Length==0&&uri.AbsoluteUri=="https://github.com/"+Repository+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+asset;
    }
    public static bool ValidManifest(Manifest m){return m!=null&&ValidVersion(m.version)&&ValidVersion(m.tag)&&Normalize(m.tag)==Normalize(m.version)&&ReleaseUrl(m.url,m.tag,ArchiveName)&&m.size>0&&m.size<=MaximumArchive&&Regex.IsMatch(m.sha256??"",@"^[0-9a-fA-F]{64}$");}
    public static string ParseChecksum(string text){
        if(string.IsNullOrEmpty(text)||text.Length>16384)throw new InvalidDataException("Invalid checksum file.");string result=null;
        foreach(string raw in text.Replace("\r","").Split('\n')){
            string line=raw.Trim();if(line.Length==0)continue;
            var match=Regex.Match(line,@"^([0-9a-fA-F]{64})[ \t]+\*?(GRIP_BREAK-macOS(?:-Source)?\.zip)$");
            if(!match.Success)throw new InvalidDataException("Malformed checksum entry.");
            if(match.Groups[2].Value==ArchiveName){if(result!=null)throw new InvalidDataException("Duplicate archive checksum.");result=match.Groups[1].Value.ToLowerInvariant();}
        }
        if(result==null)throw new InvalidDataException("Game checksum missing.");return result;
    }
    public static string Sha256(string path){using(var h=SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public static void VerifyDownload(string path,Manifest manifest){if(!ValidManifest(manifest)||new FileInfo(path).Length!=manifest.size||!string.Equals(Sha256(path),manifest.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive size or SHA256 differs from the release.");}
    public static string BundleRoot(string dataPath){
        string contents=Path.GetFullPath(dataPath);var directory=new DirectoryInfo(contents);
        if(directory.Name!="Contents"||directory.Parent==null||!directory.Parent.Name.EndsWith(".app",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The running Mac app bundle could not be identified.");
        return directory.Parent.FullName;
    }
    public static string ShellQuote(string value){if(value==null||value.IndexOf('\0')>=0)throw new ArgumentException("Invalid shell argument.");return "'"+value.Replace("'","'\"'\"'")+"'";}
    static string ProcessQuote(string value){return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";}
    public static string RunTool(string executable,string[] arguments,int timeout){
        var info=new System.Diagnostics.ProcessStartInfo(executable,string.Join(" ",Array.ConvertAll(arguments,ProcessQuote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var p=Process.Start(info)){
            if(p==null)throw new IOException("Mac system tool did not start.");var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(timeout)){try{p.Kill();}catch{}throw new IOException(Path.GetFileName(executable)+" timed out.");}
            string stdout=output.GetAwaiter().GetResult(),stderr=error.GetAwaiter().GetResult();if(p.ExitCode!=0)throw new IOException(Path.GetFileName(executable)+" rejected the update: "+stderr.Trim());return stdout.Trim();
        }
    }
    static string CanonicalDirectory(string path){return RunTool("/bin/sh",new[]{"-c","cd \"$1\" && pwd -P","canonical-directory",Path.GetFullPath(path)},10000);}
    static void NoLinks(string root){
        var pending=new Stack<DirectoryInfo>();pending.Push(new DirectoryInfo(root));
        while(pending.Count>0){var dir=pending.Pop();if((dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bundle links are unsupported.");foreach(var child in dir.GetFileSystemInfos()){if((child.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Bundle links are unsupported.");if((child.Attributes&FileAttributes.Directory)!=0)pending.Push((DirectoryInfo)child);}}
    }
    static string Plist(string app,string key){return RunTool("/usr/libexec/PlistBuddy",new[]{"-c","Print :"+key,Path.Combine(app,"Contents","Info.plist")},10000);}
    public static void ValidateBundle(string app,string version){
        if(Application.platform!=RuntimePlatform.OSXPlayer)throw new PlatformNotSupportedException("Mac bundle checks need native macOS.");
        if(!Directory.Exists(app)||!Path.GetFileName(app).EndsWith(".app",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("App bundle missing.");NoLinks(app);
        if(Plist(app,"CFBundleIdentifier")!=BundleIdentifier||Plist(app,"CFBundleExecutable")!=ExecutableName||Plist(app,"CFBundlePackageType")!="APPL"||Normalize(Plist(app,"CFBundleShortVersionString"))!=Normalize(version))throw new InvalidDataException("App identity, executable or version differs.");
        string exe=Path.Combine(app,"Contents","MacOS",ExecutableName);if(!File.Exists(exe))throw new InvalidDataException("Game executable missing.");
        RunTool("/bin/sh",new[]{"-c","test -x \"$1\"","executable-check",exe},10000);RunTool("/usr/bin/codesign",new[]{"--verify","--deep","--strict",app},30000);
    }
    public static string ExtractVerified(string zip,string stage,string version){
        if(Directory.Exists(stage)||File.Exists(stage))throw new InvalidDataException("Staging must be new.");Directory.CreateDirectory(stage);
        RunTool("/usr/bin/ditto",new[]{"-x","-k",zip,stage},30000);NoLinks(stage);
        string app=Path.Combine(stage,BundleName);ValidateBundle(app,version);return app;
    }
    public static bool AuditOwnedApp(string app){
        if(!GBApp.I||!GBApp.I.Automated)return false;
        var parent=Directory.GetParent(Path.GetFullPath(app));if(parent==null||!Regex.IsMatch(parent.Name,@"^GRIP_BREAK-MacUpdaterAudit-[0-9a-f]{32}$"))return false;
        string temp=CanonicalDirectory(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,physical=CanonicalDirectory(parent.FullName);
        return physical.StartsWith(temp,StringComparison.Ordinal)&&File.Exists(Path.Combine(physical,"disposable-owned.txt"))&&File.ReadAllText(Path.Combine(physical,"disposable-owned.txt")).Trim()==parent.Name;
    }
    public static InstallJob MakeJob(string app,string stage,string root,string version,int parentPid,bool audit=false){
        app=CanonicalDirectory(app);root=CanonicalDirectory(root);stage=CanonicalDirectory(stage);var parent=Directory.GetParent(app);
        if(parent==null||Directory.GetParent(root).FullName!=parent.FullName||!Regex.IsMatch(Path.GetFileName(root),@"^\.GRIP_BREAK-update-[0-9a-f]{32}$")||!stage.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)||parentPid<1||!ValidVersion(version))throw new InvalidDataException("Unsafe installation boundary.");
        if(audit&&!AuditOwnedApp(app))throw new InvalidDataException("Native install test must own a disposable clone.");
        string token=Guid.NewGuid().ToString("N");return new InstallJob{app=app,stage=stage,root=root,version=Normalize(version),parentPid=parentPid,token=token,audit=audit,backup=Path.Combine(parent.FullName,".GRIP_BREAK-backup-"+token+".app"),failed=Path.Combine(parent.FullName,".GRIP_BREAK-failed-"+token+".app")};
    }
    public static void WriteJob(InstallJob job){
        ValidateJob(job);
        if((job.failRestart||job.failAck)&&(!job.audit||!AuditOwnedApp(job.app)))throw new InvalidDataException("Restart-failure fixture is not allowed outside an owned automated clone.");
        File.WriteAllText(Path.Combine(job.root,"job-token"),job.token);File.WriteAllText(Path.Combine(job.root,"update-job.json"),JsonUtility.ToJson(job,true));
        File.WriteAllText(Path.Combine(job.root,"install.sh"),InstallerScript(job),new UTF8Encoding(false));
    }
    public static Process LaunchJob(InstallJob job){
        ValidateJob(job);
        if((job.failRestart||job.failAck)&&(!job.audit||!AuditOwnedApp(job.app)))throw new InvalidDataException("Unowned failure fixture.");
        return Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/sh",ProcessQuote(Path.Combine(job.root,"install.sh"))){UseShellExecute=false,CreateNoWindow=true});
    }
    IEnumerator AcknowledgeRestart(){
        string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--mac-update-restarted");if(index<0||index+2>=args.Length)yield break;
        string root=Path.GetFullPath(args[index+1]),token=args[index+2];InstallJob job=null;bool valid=false;
        try{
            root=CanonicalDirectory(root);string app=CanonicalDirectory(BundleRoot(Application.dataPath));
            valid=Directory.GetParent(root).FullName==Directory.GetParent(app).FullName&&Regex.IsMatch(Path.GetFileName(root),@"^\.GRIP_BREAK-update-[0-9a-f]{32}$")&&Regex.IsMatch(token,@"^[0-9a-f]{32}$")&&File.ReadAllText(Path.Combine(root,"job-token"))==token;
            if(valid){job=JsonUtility.FromJson<InstallJob>(File.ReadAllText(Path.Combine(root,"update-job.json")));valid=job!=null&&job.token==token&&CanonicalDirectory(job.app)==app&&job.version==Normalize(Application.version);}
        }catch{valid=false;}
        if(!valid)yield break;
        WriteReceipt(root,"restart-started",Process.GetCurrentProcess().Id+"\n"+token);
        yield return null; // GBApp completes Awake and marks test clones Automated before this ownership check.
        if(job.audit&&job.failAck&&AuditOwnedApp(job.app))yield break;
        yield return new WaitForFixedUpdate();
        if(!GBApp.I||GBApp.I.World==null||GBApp.I.Issues.Count>0)yield break;
        WriteReceipt(root,"restart-ok",Application.version+"\n"+Process.GetCurrentProcess().Id+"\n"+token);
    }
    static void WriteReceipt(string root,string name,string value){string temp=Path.Combine(root,name+".tmp");File.WriteAllText(temp,value);File.Move(temp,Path.Combine(root,name));}
    static void ValidateJob(InstallJob job){
        if(job==null||job.parentPid<1||!ValidVersion(job.version)||!Regex.IsMatch(job.token??"",@"^[0-9a-f]{32}$"))throw new InvalidDataException("Invalid install job.");
        string app=Path.GetFullPath(job.app),root=Path.GetFullPath(job.root),stage=Path.GetFullPath(job.stage),parent=Directory.GetParent(app).FullName;
        if(!app.EndsWith(".app",StringComparison.OrdinalIgnoreCase)||Directory.GetParent(root).FullName!=parent||!Regex.IsMatch(Path.GetFileName(root),@"^\.GRIP_BREAK-update-[0-9a-f]{32}$")||!stage.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)||Path.GetFullPath(job.backup)!=Path.Combine(parent,".GRIP_BREAK-backup-"+job.token+".app")||Path.GetFullPath(job.failed)!=Path.Combine(parent,".GRIP_BREAK-failed-"+job.token+".app")||(job.audit&&!AuditOwnedApp(app)))throw new InvalidDataException("Install job escaped its app boundary.");
    }
    public static string InstallerScript(InstallJob j){
        string script=@"#!/bin/sh
set -u
app=__APP__
stage=__STAGE__
root=__ROOT__
backup=__BACKUP__
failed=__FAILED__
version=__VERSION__
token=__TOKEN__
oldpid=__PID__
audit=__AUDIT__
failrestart=__FAIL__
evidence=__EVIDENCE__
exec >>""$root/install.log"" 2>&1
result() { /usr/bin/printf '%s\n' ""$1"" > ""$root/result""; }
identity() { [ -d ""$1"" ] && [ ! -L ""$1"" ] && [ ""$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' ""$1/Contents/Info.plist"" 2>/dev/null)"" = 'com.gripbreak.macoswrestling' ]; }
count=0
while /bin/kill -0 ""$oldpid"" 2>/dev/null; do
  count=$((count+1)); [ ""$count"" -le 35 ] || { result 'old-process-did-not-exit'; exit 1; }
  /bin/sleep 1
done
identity ""$app"" && identity ""$stage"" || { result 'app-identity-rejected'; exit 1; }
[ ""$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' ""$stage/Contents/Info.plist"")"" = ""$version"" ] || { result 'staged-version-rejected'; exit 1; }
/usr/bin/codesign --verify --deep --strict ""$stage"" || { result 'staged-signature-rejected'; exit 1; }
[ ! -e ""$backup"" ] && [ ! -L ""$backup"" ] && [ ! -e ""$failed"" ] && [ ! -L ""$failed"" ] || { result 'backup-path-already-exists'; exit 1; }
/bin/mv ""$app"" ""$backup"" || { result 'old-app-backup-failed'; exit 1; }
if [ -e ""$app"" ] || [ -L ""$app"" ] || ! /bin/mv ""$stage"" ""$app""; then
  [ -e ""$app"" ] || /bin/mv ""$backup"" ""$app""
  result 'install-move-failed'; exit 1
fi
restartrequested=0
owned_restart() {
  [ -f ""$root/restart-started"" ] || return 1
  newpid=$(/usr/bin/sed -n '1p' ""$root/restart-started"")
  receipt=$(/usr/bin/sed -n '2p' ""$root/restart-started"")
  case ""$newpid"" in ''|*[!0-9]*) return 1 ;; esac
  [ ""$newpid"" -gt 1 ] && [ ""$newpid"" != ""$oldpid"" ] && [ ""$receipt"" = ""$token"" ] || return 1
  command=$(/bin/ps -ww -p ""$newpid"" -o command= 2>/dev/null) || return 2
  case ""$command"" in ""$app/Contents/MacOS/GRIP BREAK""|""$app/Contents/MacOS/GRIP BREAK ""*) return 0 ;; *) return 1 ;; esac
}
stop_restart() {
  [ ""$restartrequested"" = 1 ] || return 0
  owned_restart; ownership=$?
  [ ""$ownership"" = 2 ] && return 0
  [ ""$ownership"" = 0 ] || return 1
  /bin/kill -TERM ""$newpid"" 2>/dev/null || return 1
  stopcount=0
  while /bin/kill -0 ""$newpid"" 2>/dev/null; do
    owned_restart; ownership=$?
    [ ""$ownership"" = 2 ] && return 0
    [ ""$ownership"" = 0 ] || return 1
    stopcount=$((stopcount+1))
    if [ ""$stopcount"" = 8 ]; then /bin/kill -KILL ""$newpid"" 2>/dev/null || return 1; fi
    [ ""$stopcount"" -le 12 ] || return 1
    /bin/sleep 1
  done
  return 0
}
rollback() {
  stop_restart || { result 'restart-unconfirmed-needs-recovery'; exit 1; }
  identity ""$app"" && /bin/mv ""$app"" ""$failed"" && /bin/mv ""$backup"" ""$app"" || { result 'rollback-needs-recovery'; exit 1; }
  result 'rolled-back'
  [ ""$audit"" = 1 ] || /usr/bin/open -n ""$app""
  exit 1
}
if [ ""$failrestart"" = 1 ]; then
  /usr/bin/open -n -a ""$root/intentional-missing-audit-app.app"" || rollback
  rollback
fi
if [ ""$audit"" = 1 ]; then
  /usr/bin/open -n -g ""$app"" --args --mac-updater-ack-test --evidence ""$evidence"" --mac-update-restarted ""$root"" ""$token"" || rollback
else
  /usr/bin/open -n ""$app"" --args --mac-update-restarted ""$root"" ""$token"" || rollback
fi
restartrequested=1
count=0
while [ ! -f ""$root/restart-ok"" ]; do
  count=$((count+1)); [ ""$count"" -le 45 ] || rollback
  /bin/sleep 1
done
[ ""$(/usr/bin/sed -n '1p' ""$root/restart-ok"")"" = ""$version"" ] && [ ""$(/usr/bin/sed -n '3p' ""$root/restart-ok"")"" = ""$token"" ] && [ ""$(/usr/bin/sed -n '2p' ""$root/restart-ok"")"" = ""$(/usr/bin/sed -n '1p' ""$root/restart-started"")"" ] || rollback
identity ""$backup"" || { result 'backup-identity-changed'; exit 1; }
result 'installed-and-restarted'
/bin/rm -rf ""$backup""
# Keep this job's small receipt/log until the next owned cleanup. All large
# staged/downloaded files belong to this exact generated job directory only.
/bin/rm -f ""$root/update.zip""
/bin/rm -rf ""$root/stage""
/bin/rm -f ""$root/install.sh""
exit 0
";
        var values=new Dictionary<string,string>{{"__APP__",ShellQuote(j.app)},{"__STAGE__",ShellQuote(j.stage)},{"__ROOT__",ShellQuote(j.root)},{"__BACKUP__",ShellQuote(j.backup)},{"__FAILED__",ShellQuote(j.failed)},{"__VERSION__",ShellQuote(j.version)},{"__TOKEN__",ShellQuote(j.token)},{"__PID__",j.parentPid.ToString()},{"__AUDIT__",j.audit?"1":"0"},{"__FAIL__",j.failRestart?"1":"0"},{"__EVIDENCE__",ShellQuote(j.evidence??"")}};
        // Normalize only the authored template; quoted paths remain literal data.
        return Regex.Replace(script.Replace("\r\n","\n").Replace("\r","\n"),@"__(?:APP|STAGE|ROOT|BACKUP|FAILED|VERSION|TOKEN|PID|AUDIT|FAIL|EVIDENCE)__",match=>values[match.Value]);
    }

    sealed class Central {public string name;public uint crc,compressed,expanded,offset;public ushort flags,method;}
    static readonly uint[] crcTable=MakeCrcTable();
    static uint[] MakeCrcTable(){var table=new uint[256];for(uint n=0;n<256;n++){uint value=n;for(int k=0;k<8;k++)value=(value&1)!=0?0xedb88320U^(value>>1):value>>1;table[n]=value;}return table;}
    static uint Crc(Stream stream,long expected){var bytes=new byte[65536];long count=0;uint crc=0xffffffff;int read;while((read=stream.Read(bytes,0,bytes.Length))>0){count+=read;if(count>expected)throw new InvalidDataException("Expanded member length differs.");for(int n=0;n<read;n++)crc=crcTable[(crc^bytes[n])&255]^(crc>>8);}if(count!=expected)throw new InvalidDataException("Truncated archive member.");return ~crc;}
    static bool MemberPath(string name){
        if(string.IsNullOrEmpty(name)||name.Contains("\\")||name.Contains(":")||name[0]=='/'||name.IndexOf('\0')>=0)return false;
        foreach(char c in name)if(char.IsControl(c))return false;
        foreach(string part in name.TrimEnd('/').Split('/'))if(part.Length==0||part=="."||part=="..")return false;
        if(name==BundleName+"/"||name.StartsWith(BundleName+"/",StringComparison.Ordinal))return true;
        if(name=="__MACOSX/"||name.StartsWith("__MACOSX/",StringComparison.Ordinal))return true;
        return Array.IndexOf(new[]{"MAC-README.md","KNOWN_ISSUES.md","LICENSES.md","GYM_ASSET_LICENSES.md","VALIDATION.md","SOURCE_BUILD_SHA256.json"},name)>=0;
    }
    static void ValidateExtra(byte[] extra){
        int offset=0;while(offset<extra.Length){if(offset+4>extra.Length)throw new InvalidDataException("Truncated ZIP extra data.");int code=BitConverter.ToUInt16(extra,offset),size=BitConverter.ToUInt16(extra,offset+2);if(offset+4+size>extra.Length||code==1||code==0x7075||code==0x756e)throw new InvalidDataException("Unsupported ZIP64/alternate path or Unix type field.");offset+=4+size;}
    }
    static List<Central> ReadCentral(string path){
        using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream)){
            if(stream.Length<22||stream.Length>MaximumArchive)throw new InvalidDataException("Archive length is invalid.");
            long tailStart=Math.Max(0,stream.Length-65557);stream.Position=tailStart;byte[] tail=reader.ReadBytes((int)(stream.Length-tailStart));int end=-1;
            for(int n=tail.Length-22;n>=0;n--)if(BitConverter.ToUInt32(tail,n)==0x06054b50&&n+22+BitConverter.ToUInt16(tail,n+20)==tail.Length){end=n;break;}
            if(end<0||BitConverter.ToUInt16(tail,end+4)!=0||BitConverter.ToUInt16(tail,end+6)!=0)throw new InvalidDataException("ZIP end record missing or multi-disk ZIP.");
            int count=BitConverter.ToUInt16(tail,end+10);uint centralSize=BitConverter.ToUInt32(tail,end+12),offset=BitConverter.ToUInt32(tail,end+16);
            if(count<1||count>20000||BitConverter.ToUInt16(tail,end+8)!=count||centralSize==uint.MaxValue||offset==uint.MaxValue||(long)offset+centralSize!=tailStart+end)throw new InvalidDataException("Invalid ZIP directory or unsupported ZIP64.");
            var entries=new List<Central>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);stream.Position=offset;long expanded=0;
            for(int n=0;n<count;n++){
                if(reader.ReadUInt32()!=0x02014b50)throw new InvalidDataException("Bad ZIP directory.");reader.ReadUInt16();reader.ReadUInt16();ushort flags=reader.ReadUInt16(),method=reader.ReadUInt16();reader.ReadUInt32();
                uint crc=reader.ReadUInt32(),compressed=reader.ReadUInt32(),length=reader.ReadUInt32();int nameLength=reader.ReadUInt16(),extraLength=reader.ReadUInt16(),commentLength=reader.ReadUInt16();ushort disk=reader.ReadUInt16();reader.ReadUInt16();uint attributes=reader.ReadUInt32(),localOffset=reader.ReadUInt32();
                byte[] nameBytes=reader.ReadBytes(nameLength);if(nameBytes.Length!=nameLength)throw new InvalidDataException("Truncated ZIP filename.");
                if((flags&2048)==0)foreach(byte b in nameBytes)if(b>127)throw new InvalidDataException("Ambiguous ZIP filename encoding.");
                string name=new UTF8Encoding(false,true).GetString(nameBytes);int type=(int)((attributes>>16)&0xf000);
                if(!MemberPath(name)||!names.Add(name.TrimEnd('/').Normalize(NormalizationForm.FormC))||disk!=0||(flags&1)!=0||(method!=0&&method!=8)||(type!=0&&type!=0x8000&&type!=0x4000)||length==uint.MaxValue||compressed==uint.MaxValue||localOffset==uint.MaxValue||length>512L*1024*1024)throw new InvalidDataException("Unsafe ZIP path, type, encoding or member budget.");
                expanded+=length;if(expanded>MaximumExpanded)throw new InvalidDataException("Expanded ZIP budget exceeded.");
                byte[] extra=reader.ReadBytes(extraLength);if(extra.Length!=extraLength)throw new InvalidDataException("Truncated ZIP extra data.");ValidateExtra(extra);
                stream.Position+=commentLength;if(stream.Position>(long)offset+centralSize)throw new InvalidDataException("ZIP directory overruns.");
                long returnTo=stream.Position;stream.Position=localOffset;
                if(reader.ReadUInt32()!=0x04034b50)throw new InvalidDataException("Bad local ZIP header.");reader.ReadUInt16();ushort localFlags=reader.ReadUInt16(),localMethod=reader.ReadUInt16();reader.ReadUInt32();reader.ReadUInt32();reader.ReadUInt32();reader.ReadUInt32();int localName=reader.ReadUInt16(),localExtra=reader.ReadUInt16();byte[] localNameBytes=reader.ReadBytes(localName);
                if(localFlags!=flags||localMethod!=method||localName!=nameLength||!EqualBytes(localNameBytes,nameBytes)||(long)localOffset+30+localName+localExtra+compressed>offset)throw new InvalidDataException("ZIP local name/data differs from central directory.");
                byte[] localExtraBytes=reader.ReadBytes(localExtra);if(localExtraBytes.Length!=localExtra)throw new InvalidDataException("Truncated local ZIP extra data.");ValidateExtra(localExtraBytes);
                stream.Position=returnTo;entries.Add(new Central{name=name,crc=crc,compressed=compressed,expanded=length,offset=localOffset,flags=flags,method=method});
            }
            if(stream.Position!=(long)offset+centralSize)throw new InvalidDataException("ZIP directory length differs.");return entries;
        }
    }
    static bool EqualBytes(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int n=0;n<a.Length;n++)if(a[n]!=b[n])return false;return true;}
    public static void ValidateArchive(string path){
        var central=ReadCentral(path);bool plist=false,executable=false;var map=new Dictionary<string,Central>(StringComparer.Ordinal);foreach(var record in central)map.Add(record.name,record);
        using(var zip=ZipFile.OpenRead(path)){
            if(zip.Entries.Count!=central.Count)throw new InvalidDataException("ZIP readers disagree.");
            foreach(var entry in zip.Entries){Central record;if(!map.TryGetValue(entry.FullName,out record)||entry.Length!=record.expanded||entry.CompressedLength!=record.compressed)throw new InvalidDataException("ZIP member metadata differs.");using(var data=entry.Open())if(Crc(data,entry.Length)!=record.crc)throw new InvalidDataException("ZIP CRC rejected a damaged member.");
                plist|=entry.FullName==BundleName+"/Contents/Info.plist";executable|=entry.FullName==BundleName+"/Contents/MacOS/"+ExecutableName&&((entry.ExternalAttributes>>16)&73)!=0;
            }
        }
        if(!plist||!executable)throw new InvalidDataException("Signed Mac game bundle/executable missing.");
    }
}
