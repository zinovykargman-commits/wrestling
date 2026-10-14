using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using Process=System.Diagnostics.Process;

// Native test copies are marked and confined below this process's temp root.
// No installed user app, preferences, signing identity or Git checkout is edited.
public static class GBAuditMacUpdater {
    static string NewOwnedRoot(){
        string root=Path.Combine(Path.GetTempPath(),"GRIP_BREAK-MacUpdaterAudit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"disposable-owned.txt"),Path.GetFileName(root));return root;
    }
    static bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(IOException){return true;}}
    static void WriteZip(string path,string extra=null,int extraMode=0){
        using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)){
            foreach(string name in new[]{GBMacUpdater.BundleName+"/Contents/Info.plist",GBMacUpdater.BundleName+"/Contents/MacOS/"+GBMacUpdater.ExecutableName}){
                var entry=zip.CreateEntry(name,System.IO.Compression.CompressionLevel.NoCompression);entry.ExternalAttributes=(0x8000|(name.EndsWith("Info.plist")?420:493))<<16;
                using(var output=entry.Open()){byte[] bytes=Encoding.UTF8.GetBytes("CRC and archive-structure fixture; not a signed app");output.Write(bytes,0,bytes.Length);}
            }
            if(extra!=null){var entry=zip.CreateEntry(extra);entry.ExternalAttributes=extraMode;using(var output=entry.Open()){byte[] bytes=Encoding.UTF8.GetBytes("fixture");output.Write(bytes,0,bytes.Length);}}
        }
    }
    static IEnumerator AwaitProcess(Process process,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!process.HasExited&&Time.realtimeSinceStartup<end)yield return null;if(!process.HasExited){try{process.Kill();}catch{}}}
    static string Inode(string path){return GBMacUpdater.RunTool("/usr/bin/stat",new[]{"-f","%i",path},10000);}
    static string CloneApp(string root){string app=Path.Combine(root,GBMacUpdater.BundleName);GBMacUpdater.RunTool("/usr/bin/ditto",new[]{GBMacUpdater.BundleRoot(Application.dataPath),app},30000);return app;}
    static GBMacUpdater.InstallJob FixtureJob(string app,string version,bool fail,out Process sleeper){
        string parent=Directory.GetParent(app).FullName,root=Path.Combine(parent,".GRIP_BREAK-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);GBMacUpdater.RunTool("/bin/chmod",new[]{"700",root},10000);
        string stage=Path.Combine(root,"stage",GBMacUpdater.BundleName);GBMacUpdater.RunTool("/usr/bin/ditto",new[]{GBMacUpdater.BundleRoot(Application.dataPath),stage},30000);GBMacUpdater.ValidateBundle(stage,version);
        sleeper=Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/sleep","2"){UseShellExecute=false,CreateNoWindow=true});
        var job=GBMacUpdater.MakeJob(app,stage,root,version,sleeper.Id,true);job.failRestart=fail;job.evidence=Path.Combine(parent,"restart-evidence");GBMacUpdater.WriteJob(job);return job;
    }
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        check("mac_updater_native_platform",Application.platform==RuntimePlatform.OSXPlayer,"native system ditto/codesign/open required");if(Application.platform!=RuntimePlatform.OSXPlayer)yield break;
        string[] args=Environment.GetCommandLineArgs();int zipArgument=Array.IndexOf(args,"--mac-updater-archive");string signedZip=zipArgument>=0&&zipArgument+1<args.Length?args[zipArgument+1]:"";
        if(!File.Exists(signedZip)){check("mac_actual_signed_archive_compatibility",false,"native runner must supply actual signed incoming archive");yield break;}
        string signedHash=GBMacUpdater.ParseChecksum(File.ReadAllText(Path.Combine(Directory.GetParent(signedZip).FullName,GBMacUpdater.ChecksumName)));GBMacUpdater.ValidateArchive(signedZip);check("mac_actual_signed_archive_compatibility",GBMacUpdater.Sha256(signedZip)==signedHash,"actual incoming signed universal ZIP: all paths, Unix types, extra fields, member modes/budgets and streamed CRC validated before publication; SHA="+signedHash);
        string owned=NewOwnedRoot(),valid=Path.Combine(owned,"valid.zip");WriteZip(valid);GBMacUpdater.ValidateArchive(valid);
        check("mac_zip_structure_crc_and_executable_modes",true,"all fixture members read and CRC checked; native signed-bundle verification follows separately");
        check("mac_version_semver_and_order",GBMacUpdater.IsNewer("v0.4.11","0.4.10")&&!GBMacUpdater.IsNewer("0.4.10","0.4.10")&&!GBMacUpdater.ValidVersion("0.4.12-beta")&&!GBMacUpdater.ValidVersion("vv0.4.11")&&!GBMacUpdater.ValidVersion(" 0.4.11"),"stable three-part versions only; no downgrade/current release in normal updater");
        string url="https://github.com/"+GBMacUpdater.Repository+"/releases/download/v0.4.11/"+GBMacUpdater.ArchiveName;
        check("mac_release_urls_are_exact",GBMacUpdater.ReleaseUrl(url,"v0.4.11",GBMacUpdater.ArchiveName)&&!GBMacUpdater.ReleaseUrl(url+"?other=1","v0.4.11",GBMacUpdater.ArchiveName)&&!GBMacUpdater.ReleaseUrl(url.Replace("github.com","example.com"),"v0.4.11",GBMacUpdater.ArchiveName)&&!GBMacUpdater.ReleaseUrl(url.Replace("macoswrestling","wrestling"),"v0.4.11",GBMacUpdater.ArchiveName),"only exact authenticated GitHub Mac-repository release paths");
        string sha=GBMacUpdater.Sha256(valid),line=sha+"  "+GBMacUpdater.ArchiveName+"\n";
        check("mac_checksum_rejects_missing_duplicate_invalid",GBMacUpdater.ParseChecksum(line)==sha&&Reject(()=>GBMacUpdater.ParseChecksum(line+line))&&Reject(()=>GBMacUpdater.ParseChecksum("garbage")),"one exact64-hex game checksum");
        var manifest=new GBMacUpdater.Manifest{tag="v0.4.11",version="0.4.11",url=url,size=new FileInfo(valid).Length,sha256=sha};GBMacUpdater.VerifyDownload(valid,manifest);
        manifest.sha256=new string('0',64);check("mac_archive_sha_rejects_wrong_digest",Reject(()=>GBMacUpdater.VerifyDownload(valid,manifest)),"download cannot proceed to extraction with a wrong SHA256");manifest.sha256=sha;manifest.size++;check("mac_archive_size_rejects_mismatch",Reject(()=>GBMacUpdater.VerifyDownload(valid,manifest)),"actual byte count must equal API asset size");
        foreach(string bad in new[]{GBMacUpdater.BundleName+"/../outside.txt","/tmp/outside.txt",GBMacUpdater.BundleName+"/Contents/../escape",GBMacUpdater.BundleName+"/contents/info.plist"}){
            string file=Path.Combine(owned,"bad-"+Guid.NewGuid().ToString("N")+".zip");WriteZip(file,bad);check("mac_zip_rejects_"+Path.GetFileName(file),Reject(()=>GBMacUpdater.ValidateArchive(file)),"unsafe/colliding path="+bad);
        }
        string link=Path.Combine(owned,"symlink.zip");WriteZip(link,GBMacUpdater.BundleName+"/Contents/link",unchecked((int)(0xa1ffU<<16)));check("mac_zip_rejects_symbolic_links",Reject(()=>GBMacUpdater.ValidateArchive(link)),"Unix link mode cannot escape staging");
        byte[] crcBytes=File.ReadAllBytes(valid);for(int n=0;n+20<crcBytes.Length;n++)if(BitConverter.ToUInt32(crcBytes,n)==0x02014b50){crcBytes[n+16]^=1;break;}string damaged=Path.Combine(owned,"bad-crc.zip");File.WriteAllBytes(damaged,crcBytes);check("mac_zip_rejects_bad_crc",Reject(()=>GBMacUpdater.ValidateArchive(damaged)),"central checksum mismatch rejected even before native extraction");
        check("mac_bundle_root_uses_contents_owner",GBMacUpdater.BundleRoot(Path.Combine(owned,GBMacUpdater.BundleName,"Contents"))==Path.Combine(owned,GBMacUpdater.BundleName)&&Reject(()=>GBMacUpdater.BundleRoot(owned)),"never replace parentfolder or preferences directory");
        string quoted="space's __STAGE__ $(printf UNSAFE) ; literal";string echo=GBMacUpdater.RunTool("/bin/sh",new[]{"-c","printf '%s' "+GBMacUpdater.ShellQuote(quoted)},10000);check("mac_shell_quoting_is_literal",echo==quoted,"spaces/apostrophes/substitutions/semicolon remain data");
        string original=GBMacUpdater.BundleRoot(Application.dataPath),originalHash=GBMacUpdater.Sha256(Path.Combine(original,"Contents","MacOS",GBMacUpdater.ExecutableName));GBMacUpdater.ValidateBundle(original,Application.version);
        check("mac_current_bundle_identity_and_signature",true,"native plistID/executable/version, executablebit and codesign --verify --deep --strict passed");
        string clone=CloneApp(owned),outside=Path.Combine(owned,"outside-user-file.txt");File.WriteAllText(outside,"preserve");
        string outsideStage=Path.Combine(owned,"outside-stage"),boundaryJob=Path.Combine(owned,".GRIP_BREAK-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(outsideStage);Directory.CreateDirectory(boundaryJob);
        check("mac_job_rejects_stage_outside_owned_boundary",Reject(()=>GBMacUpdater.MakeJob(clone,outsideStage,boundaryJob,Application.version,System.Diagnostics.Process.GetCurrentProcess().Id,true)),"existing canonicalized stage must be inside generated sibling job; rejected by actual boundary guard");
        Process rollbackOld;var rollback=FixtureJob(clone,Application.version,true,out rollbackOld);string oldInode=Inode(clone),newInode=Inode(rollback.stage);string installer=Path.Combine(rollback.root,"install.sh");GBMacUpdater.RunTool("/bin/sh",new[]{"-n",installer},10000);check("mac_installer_uses_posix_line_endings_and_parses",!File.ReadAllText(installer).Contains("\r"),"actual emitted shell parsed by native /bin/sh -n; no Windows CRLF in executable script");using(var process=GBMacUpdater.LaunchJob(rollback)){
            yield return new WaitForSecondsRealtime(.15f);
            check("mac_installer_waits_for_old_process",!rollbackOld.HasExited&&Inode(clone)==oldInode&&!Directory.Exists(rollback.backup),"real old process alive: installed app inode unchanged before old process exit");
            yield return AwaitProcess(rollbackOld,5);rollbackOld.WaitForExit();rollbackOld.Dispose();
            yield return AwaitProcess(process,50);
            check("mac_native_failed_restart_rolls_back_owned_app",process.HasExited&&process.ExitCode==1&&File.ReadAllText(Path.Combine(rollback.root,"result")).Trim()=="rolled-back"&&Inode(clone)==oldInode&&Inode(rollback.failed)==newInode&&!Directory.Exists(rollback.backup),"actual helperwait/atomicmoves/failed nativeopen: original inode restored, staged inode preserved as failedcopy");
        }
        Process successOld;var success=FixtureJob(clone,Application.version,false,out successOld);string expected=GBMacUpdater.Sha256(Path.Combine(success.stage,"Contents","MacOS",GBMacUpdater.ExecutableName));using(var process=GBMacUpdater.LaunchJob(success)){
            yield return AwaitProcess(successOld,5);successOld.WaitForExit();successOld.Dispose();
            yield return AwaitProcess(process,80);
            bool receipt=File.Exists(Path.Combine(success.root,"result"))&&File.ReadAllText(Path.Combine(success.root,"result")).Trim()=="installed-and-restarted";
            check("mac_native_install_replace_and_open_restart",process.HasExited&&process.ExitCode==0&&receipt&&File.Exists(Path.Combine(success.root,"restart-ok"))&&!Directory.Exists(success.backup)&&GBMacUpdater.Sha256(Path.Combine(clone,"Contents","MacOS",GBMacUpdater.ExecutableName))==expected,"native ditto/code signature/atomicinstall/open/firstframeack in a disposable clone; no publicdownload claim");
        }
        Process noAckOld;var noAck=FixtureJob(clone,Application.version,false,out noAckOld);noAck.failAck=true;GBMacUpdater.WriteJob(noAck);string noAckInode=Inode(clone);
        using(var process=GBMacUpdater.LaunchJob(noAck)){
            yield return AwaitProcess(noAckOld,5);noAckOld.WaitForExit();noAckOld.Dispose();yield return AwaitProcess(process,80);
            string started=Path.Combine(noAck.root,"restart-started");int childPid=0;if(File.Exists(started))int.TryParse(File.ReadAllLines(started)[0],out childPid);
            bool childGone=childPid>1;try{using(var child=Process.GetProcessById(childPid))childGone=child.HasExited;}catch(ArgumentException){}
            check("mac_unacknowledged_restart_stops_owned_child_before_rollback",process.HasExited&&process.ExitCode==1&&File.Exists(Path.Combine(noAck.root,"result"))&&File.ReadAllText(Path.Combine(noAck.root,"result")).Trim()=="rolled-back"&&Inode(clone)==noAckInode&&childGone&&!File.Exists(Path.Combine(noAck.root,"restart-ok")),"real started receipt PID="+childPid+"; exact signed disposable child stopped before original inode restored; no duplicate process");
        }
        check("mac_fixture_preserves_user_app_and_outside_files",File.ReadAllText(outside)=="preserve"&&GBMacUpdater.Sha256(Path.Combine(original,"Contents","MacOS",GBMacUpdater.ExecutableName))==originalHash,"only marked tempclone.app changed; preferences and caller's actual app untouched");
        check("mac_updater_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
        File.WriteAllText(Path.Combine(directory,"mac-updater-fixture-root.txt"),owned);
    }
    public static IEnumerator AckOnly(GBApp app,string directory,Action<string,bool,string> check){
        string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--mac-update-restarted");float end=Time.realtimeSinceStartup+15;
        string root=index>=0&&index+1<args.Length?args[index+1]:"";
        if(File.Exists(Path.Combine(root,"update-job.json"))){var job=JsonUtility.FromJson<GBMacUpdater.InstallJob>(File.ReadAllText(Path.Combine(root,"update-job.json")));if(job.audit&&job.failAck&&GBMacUpdater.AuditOwnedApp(GBMacUpdater.BundleRoot(Application.dataPath))){yield return new WaitForSecondsRealtime(65);check("mac_missing_ack_fixture_was_not_terminated",false,"native helper should stop this exact owned child before rollback");yield break;}}
        while(!File.Exists(Path.Combine(root,"restart-ok"))&&Time.realtimeSinceStartup<end)yield return null;
        check("mac_updated_clone_started_and_acknowledged",GBMacUpdater.AuditOwnedApp(GBMacUpdater.BundleRoot(Application.dataPath))&&File.Exists(Path.Combine(root,"restart-ok"))&&app.World!=null&&app.Issues.Count==0,"first live frame in signedupdated temporary app; version="+Application.version);
    }
    public static IEnumerator PublicInstall(GBApp app,string directory,Action<string,bool,string> check){
        var updater=app.GetComponent<GBMacUpdater>();if(!updater)updater=app.gameObject.AddComponent<GBMacUpdater>();
        updater.AllowCurrentReleaseInDisposableAudit();updater.CheckOrInstall();float end=Time.realtimeSinceStartup+60;while(updater.Busy&&Time.realtimeSinceStartup<end)yield return null;
        check("mac_public_release_matches_current_clone",updater.HasUpdate&&updater.LatestVersion==Application.version,"actuallatestnonprerelease Macarchive+checksum; "+updater.Status);if(!updater.HasUpdate)yield break;
        updater.InstallerLaunched+=job=>{
            File.WriteAllText(Path.Combine(directory,"public-install-job.json"),JsonUtility.ToJson(job,true));
            File.WriteAllText(Path.Combine(directory,"public-download-manifest.json"),JsonUtility.ToJson(updater.AvailableManifest,true));
            File.WriteAllText(Path.Combine(directory,"verified-download-sha.txt"),GBMacUpdater.Sha256(Path.Combine(job.root,"update.zip")));
        };
        updater.CheckOrInstall();end=Time.realtimeSinceStartup+280;while(updater.Busy&&Time.realtimeSinceStartup<end)yield return null;
        check("mac_public_install_handoff",false,"If reached, updater did not close the disposable old app for its native installer: "+updater.Status);
    }
    public static IEnumerator PublicClone(GBApp app,string directory,Action<string,bool,string> check){
        if(Application.platform!=RuntimePlatform.OSXPlayer){check("mac_public_clone_requires_native_mac",false,"native only");yield break;}
        string root=NewOwnedRoot(),clone=CloneApp(root),evidence=Path.Combine(root,"public-evidence"),outside=Path.Combine(root,"outside-user-file.txt");Directory.CreateDirectory(evidence);File.WriteAllText(outside,"preserve");
        string original=GBMacUpdater.BundleRoot(Application.dataPath),before=GBMacUpdater.Sha256(Path.Combine(original,"Contents","MacOS",GBMacUpdater.ExecutableName));
        GBMacUpdater.RunTool("/usr/bin/open",new[]{"-n","-g",clone,"--args","--mac-updater-public-install-test","--graphics-quality","performance","--evidence",evidence},10000);
        string jobFile=Path.Combine(evidence,"public-install-job.json");float deadline=Time.realtimeSinceStartup+300;while(!File.Exists(jobFile)&&Time.realtimeSinceStartup<deadline)yield return null;
        check("mac_public_download_and_native_handoff",File.Exists(jobFile),"actualGitHub download/checksum/CRC/signature stage in ownedclone");if(!File.Exists(jobFile))yield break;
        var job=JsonUtility.FromJson<GBMacUpdater.InstallJob>(File.ReadAllText(jobFile));string result=Path.Combine(job.root,"result");deadline=Time.realtimeSinceStartup+90;while(!File.Exists(result)&&Time.realtimeSinceStartup<deadline)yield return null;
        bool success=File.Exists(result)&&File.ReadAllText(result).Trim()=="installed-and-restarted";
        var manifest=JsonUtility.FromJson<GBMacUpdater.Manifest>(File.ReadAllText(Path.Combine(evidence,"public-download-manifest.json")));
        check("mac_public_checksum_and_real_replace_restart",success&&manifest.version==Application.version&&manifest.sha256==File.ReadAllText(Path.Combine(evidence,"verified-download-sha.txt")).Trim()&&File.Exists(Path.Combine(job.root,"restart-ok")),"publicactualZIP installed and firstframe acknowledged; result="+(File.Exists(result)?File.ReadAllText(result):"missing"));
        check("mac_public_update_preserves_original_and_outside_files",GBMacUpdater.Sha256(Path.Combine(original,"Contents","MacOS",GBMacUpdater.ExecutableName))==before&&File.ReadAllText(outside)=="preserve","callerapp and outside file remain unchanged; no prefs writes by automated clones");
        File.WriteAllText(Path.Combine(directory,"mac-public-clone-root.txt"),root);
    }
}
