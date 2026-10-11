using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;

public partial class GBAudit {
    IEnumerator UpdaterRun(){
        var tunnel=new GBTunnel();var tunnelType=typeof(GBTunnel);var privateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var consume=tunnelType.GetMethod("ReadLine",privateFlags);string roomSecret=GBTunnel.GenerateSecret();tunnelType.GetField("secret",privateFlags).SetValue(tunnel,roomSecret);
        consume.Invoke(tunnel,new object[]{"API https://api.trycloudflare.com"});consume.Invoke(tunnel,new object[]{"https://grip-regression-unissued-host.trycloudflare.com"});
        Check("invite_waits_for_registered_route",!tunnel.Ready&&tunnel.InviteUrl.Length==0,"assigned hostname alone cannot advertise a disconnected route");
        var publishClock=System.Diagnostics.Stopwatch.StartNew();consume.Invoke(tunnel,new object[]{"Registered tunnel connection"});Uri endpoint;string parsedSecret;
        Check("invite_publishes_without_local_dns",tunnel.Ready&&publishClock.Elapsed.TotalSeconds<.25&&GBTunnel.TryParseInvite(tunnel.InviteUrl,out endpoint,out parsedSecret)&&parsedSecret==roomSecret,"registered route immediately publishes full authenticated link without resolving an unissued hostname");
        string firstInvite=tunnel.InviteUrl;consume.Invoke(tunnel,new object[]{"https://another-unissued-host.trycloudflare.com"});Check("invite_keeps_first_assigned_hostname",tunnel.InviteUrl==firstInvite,"later helper output cannot redirect the room");tunnel.Dispose();consume.Invoke(tunnel,new object[]{"Registered tunnel connection"});Check("disposed_invite_stays_closed",!tunnel.Ready&&tunnel.InviteUrl.Length==0,"late helper callbacks cannot reopen a disposed room");
        Check("update_version_comparison",GBUpdater.IsNewer("v0.2.0","0.1.9")&&!GBUpdater.IsNewer("0.2.0","0.2.0")&&!GBUpdater.IsNewer("nonsense","0.2.0"),"newer, equal and invalid versions");
        var manifest=new GBUpdater.Manifest{version="0.2.0",url="https://github.com/zinovykargman-commits/wrestling/releases/download/v0.2.0/GRIP_BREAK-Windows.zip",size=25,sha256=new string('a',64)};
        Check("update_manifest_accepts_own_release",GBUpdater.ValidManifest(manifest),"fixed repository HTTPS archive, numeric version, exact size/hash format");
        manifest.url="https://example.com/update.zip";Check("update_manifest_rejects_other_hosts",!GBUpdater.ValidManifest(manifest),"no arbitrary download host");
        string zip=Path.Combine(directory,"unsafe.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){var entry=archive.CreateEntry("GRIP_BREAK/../escaped.txt");using(var w=new StreamWriter(entry.Open()))w.Write("rejected");}
        bool rejected=false;try{GBUpdater.ExtractPackage(zip,Path.Combine(directory,"unsafe-stage"));}catch(InvalidDataException){rejected=true;}
        Check("update_rejects_archive_traversal",rejected&&!File.Exists(Path.Combine(directory,"escaped.txt")),"archive cannot write outside stage");
        zip=Path.Combine(directory,"valid.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){foreach(var name in new[]{"GRIP_BREAK/Build/GRIP_BREAK.exe","GRIP_BREAK/Build/GRIP_BREAK_Data/globalgamemanagers","GRIP_BREAK/PLAY.cmd"}){var entry=archive.CreateEntry(name);using(var w=new StreamWriter(entry.Open()))w.Write("test fixture");}}
        string package=GBUpdater.ExtractPackage(zip,Path.Combine(directory,"valid-stage"));Check("update_extracts_expected_layout",File.Exists(Path.Combine(package,"Build","GRIP_BREAK.exe")),"valid contained package preserves game layout");
        Check("update_sha256_reads_archive",GBUpdater.Sha256(zip).Length==64,"archive hash computed from bytes");
        app.Updater.CheckOrInstall();float deadline=Time.realtimeSinceStartup+45;while(app.Updater.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
        bool publicRequired=Has("--require-public-update");Check("update_check_returns_without_hanging",!app.Updater.Busy,"status="+app.Updater.Status);
        if(publicRequired)Check("public_update_manifest_verified",app.Updater.Status.StartsWith("UP TO DATE")&&app.Updater.LatestVersion==Application.version,"status="+app.Updater.Status+" latest="+app.Updater.LatestVersion);
        if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Path.Combine(directory,"update-menu.png"));yield return Wait(.3f);
        Check("updater_no_runtime_errors",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
