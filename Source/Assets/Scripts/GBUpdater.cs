using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Process=System.Diagnostics.Process;

// Public release metadata supplies data only. The installer is shipped with the
// current game, copied outside the installation, then run after the game closes.
public class GBUpdater:MonoBehaviour {
    public const string Repository="zinovykargman-commits/wrestling";
    public const string LatestReleaseApi="https://api.github.com/repos/"+Repository+"/releases/latest";
    GBMacUpdater mac;string windowsStatus="CHECK FOR UPDATE",windowsLatestVersion="";bool windowsBusy,windowsHasUpdate;
    public string Status {get{return mac?mac.Status:windowsStatus;}private set{windowsStatus=value;}}
    public bool Busy {get{return mac?mac.Busy:windowsBusy;}private set{windowsBusy=value;}}
    public bool HasUpdate {get{return mac?mac.HasUpdate:windowsHasUpdate;}private set{windowsHasUpdate=value;}}
    public string LatestVersion {get{return mac?mac.LatestVersion:windowsLatestVersion;}private set{windowsLatestVersion=value;}}
    Manifest available;
    void Awake(){if(Application.platform==RuntimePlatform.OSXPlayer)mac=gameObject.AddComponent<GBMacUpdater>();}

    [Serializable] class Asset {public string name,browser_download_url;public long size;}
    [Serializable] class Release {public string tag_name;public Asset[] assets;}
    [Serializable] public class Manifest {public string version,url,sha256;public long size;}
    [Serializable] class Job {public string installRoot,packageRoot,zipPath,expectedSha,version,restartArguments;public int parentPid;}

    public void CheckOrInstall(){
        if(mac){mac.CheckOrInstall();return;}
        if(Busy)return;
        if(Application.isEditor){Status="UPDATES ARE AVAILABLE IN THE WINDOWS GAME";return;}
        if(Application.platform!=RuntimePlatform.WindowsPlayer){Status="WINDOWS UPDATES ONLY";return;}
        Busy=true;
        StartCoroutine(HasUpdate&&available!=null?Install():Check());
    }
    IEnumerator Check(){
        HasUpdate=false;available=null;Status="CHECKING GITHUB...";
        string releaseText;
        using(var request=Request(LatestReleaseApi,20)){
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success){Fail(request.responseCode==404?"NO RELEASE AVAILABLE YET":"COULD NOT CHECK UPDATE • TRY AGAIN");yield break;}
            releaseText=request.downloadHandler.text;
        }
        Release release=null;
        try{release=JsonUtility.FromJson<Release>(releaseText);}catch(Exception e){Debug.LogWarning("Update release metadata: "+e.Message);}
        Asset metadata=null,archive=null;
        if(release!=null&&release.assets!=null)foreach(var asset in release.assets){
            if(asset.name=="GRIP_BREAK-Update.json")metadata=asset;
            if(asset.name=="GRIP_BREAK-Windows.zip")archive=asset;
        }
        if(metadata==null||archive==null||!ReleaseUrl(metadata.browser_download_url)||!ReleaseUrl(archive.browser_download_url)){
            Fail("RELEASE IS NOT READY • TRY AGAIN LATER");yield break;
        }
        string manifestText;
        using(var request=Request(metadata.browser_download_url,20)){
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success){Fail("COULD NOT READ UPDATE • TRY AGAIN");yield break;}
            manifestText=request.downloadHandler.text;
        }
        Manifest manifest=null;
        try{manifest=JsonUtility.FromJson<Manifest>(manifestText);}catch(Exception e){Debug.LogWarning("Update manifest: "+e.Message);}
        if(!ValidManifest(manifest)||manifest.url!=archive.browser_download_url||manifest.size!=archive.size||NormalizeVersion(manifest.version)!=NormalizeVersion(release.tag_name)){
            Fail("UPDATE VERIFICATION FAILED • TRY AGAIN LATER");yield break;
        }
        LatestVersion=manifest.version;
        if(!IsNewer(manifest.version,Application.version)){Status="UP TO DATE • "+Application.version;Busy=false;yield break;}
        available=manifest;HasUpdate=true;Status="UPDATE "+manifest.version+" READY • RESTART REQUIRED";Busy=false;
    }
    IEnumerator Install(){
        string installRoot=null,jobRoot=null,zip=null,package=null,helper=null;
        string setupError=null;
        try{
            installRoot=Directory.GetParent(Path.GetFullPath(Application.dataPath)).Parent.FullName;
            if(!File.Exists(Path.Combine(installRoot,"Build","GRIP_BREAK.exe")))throw new IOException("Game installation was not found.");
            string bundled=Path.Combine(installRoot,"Update","install-update.ps1");
            if(!File.Exists(bundled))throw new IOException("The bundled update installer is missing.");
            jobRoot=Path.Combine(Application.persistentDataPath,"updates",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(jobRoot);zip=Path.Combine(jobRoot,"update.zip");
            helper=Path.Combine(jobRoot,"install-update.ps1");File.Copy(bundled,helper,false);
        }catch(Exception e){setupError=e.Message;}
        if(setupError!=null){Fail("UPDATE COULD NOT START • "+setupError);yield break;}
        Status="DOWNLOADING UPDATE...";
        using(var request=Request(available.url,180)){
            request.downloadHandler=new DownloadHandlerFile(zip){removeFileOnAbort=true};
            var operation=request.SendWebRequest();
            while(!operation.isDone){Status="DOWNLOADING UPDATE • "+Mathf.RoundToInt(request.downloadProgress*100)+"%";yield return null;}
            if(request.result!=UnityWebRequest.Result.Success){Fail("DOWNLOAD FAILED • YOUR GAME IS UNCHANGED");yield break;}
        }
        Status="VERIFYING UPDATE...";yield return null;
        string error=null;
        try{
            if(new FileInfo(zip).Length!=available.size||!string.Equals(Sha256(zip),available.sha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded file did not match its release checksum.");
            package=ExtractPackage(zip,Path.Combine(jobRoot,"stage"));
            var job=new Job{installRoot=installRoot,packageRoot=package,zipPath=zip,expectedSha=available.sha256,version=available.version,parentPid=Process.GetCurrentProcess().Id};
            File.WriteAllText(Path.Combine(jobRoot,"update-job.json"),JsonUtility.ToJson(job,true),Encoding.UTF8);
        }catch(Exception e){error=e.Message;}
        if(error!=null){Fail("UPDATE REJECTED • "+error);yield break;}
        Status="RESTARTING TO INSTALL...";yield return null;
        try{
            string command="& '"+helper.Replace("'","''")+"' -JobFile '"+Path.Combine(jobRoot,"update-job.json").Replace("'","''")+"'";
            string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var start=new System.Diagnostics.ProcessStartInfo(powershell,"-NoProfile -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(command))){
                UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden
            };
            var process=Process.Start(start);
            if(process==null)throw new IOException("The update installer did not start.");
            Application.Quit();
        }catch(Exception e){Fail("RESTART FAILED • "+e.Message);}
    }
    void Fail(string message){Status=message;Busy=false;Debug.LogWarning("GRIP UPDATE: "+message);}
    static UnityWebRequest Request(string url,int timeout){
        var request=UnityWebRequest.Get(url);request.timeout=timeout;request.redirectLimit=5;
        request.SetRequestHeader("User-Agent","GRIP-BREAK/"+Application.version);
        request.SetRequestHeader("Accept",url.StartsWith("https://api.github.com/",StringComparison.Ordinal)?"application/vnd.github+json":"application/octet-stream");
        return request;
    }
    static bool ReleaseUrl(string value){
        Uri uri;
        return Uri.TryCreate(value,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&
            uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)&&string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment);
    }
    public static bool ValidManifest(Manifest manifest){
        if(manifest==null||!ReleaseUrl(manifest.url)||manifest.size<1||manifest.size>1024L*1024*1024||string.IsNullOrEmpty(manifest.sha256)||manifest.sha256.Length!=64)return false;
        Version parsed;if(!Version.TryParse(NormalizeVersion(manifest.version),out parsed))return false;
        foreach(char c in manifest.sha256)if(!Uri.IsHexDigit(c))return false;
        return true;
    }
    static string NormalizeVersion(string value){return string.IsNullOrEmpty(value)?"":value.Trim().TrimStart('v','V');}
    public static bool IsNewer(string candidate,string current){
        Version next,old;return Version.TryParse(NormalizeVersion(candidate),out next)&&Version.TryParse(NormalizeVersion(current),out old)&&next>old;
    }
    public static string Sha256(string path){
        using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    public static string ExtractPackage(string archivePath,string stageRoot){
        string root=Path.GetFullPath(stageRoot);Directory.CreateDirectory(root);
        string prefix=root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long expanded=0;
        using(var archive=ZipFile.OpenRead(archivePath)){
            if(archive.Entries.Count>20000)throw new InvalidDataException("Too many archive entries.");
            foreach(var entry in archive.Entries){
                string name=entry.FullName.Replace('\\','/');
                if(!name.StartsWith("GRIP_BREAK/",StringComparison.Ordinal)||name.Contains(":")||name.IndexOf('\0')>=0||
                    name.StartsWith("GRIP_BREAK/Source/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unexpected archive path.");
                // Unix symlink mode and traversal must never escape the staging tree.
                if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Archive links are unsupported.");
                foreach(string part in name.Split('/'))if(part==".."||part==".")throw new InvalidDataException("Unsafe archive path.");
                string destination=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));
                if(!destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!seen.Add(destination))throw new InvalidDataException("Archive path collision.");
                expanded+=entry.Length;if(expanded>1536L*1024*1024)throw new InvalidDataException("Update is too large.");
                if(name.EndsWith("/",StringComparison.Ordinal)){Directory.CreateDirectory(destination);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(destination));entry.ExtractToFile(destination,false);
            }
        }
        string package=Path.Combine(root,"GRIP_BREAK");
        if(!File.Exists(Path.Combine(package,"Build","GRIP_BREAK.exe"))||!Directory.Exists(Path.Combine(package,"Build","GRIP_BREAK_Data"))||!File.Exists(Path.Combine(package,"PLAY.cmd")))
            throw new InvalidDataException("The Windows game is missing from the update.");
        return package;
    }
}
