using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Reflection;

// Two real socket peers exercise production room/menu/result lifetime.
// RoundWin stages a result event; it does NOT claim a physically earned pin.
// Pin qualification is independently exercised by the existing pin audits.
public partial class GBAudit {
    int sessionExpectedId,sessionMaximumTargets;
    bool sessionTransportStable=true,sessionFinite=true;
    static int SessionField(GBNetwork network,string name){return (int)typeof(GBNetwork).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(network);}
    int SessionTargets(){int count=0;foreach(var w in app.Wrestlers)foreach(var h in w.hands)if(h.target)count++;return count;}
    bool SessionGripsClear(){foreach(var w in app.Wrestlers)foreach(var h in w.hands)if(h.target||h.joint)return false;return true;}
    void SessionFrame(bool aim=false){
        int mine=Has("--session-host-test")?0:1;
        if(app.Wrestlers.Count==2){
            var a=app.Wrestlers[mine];var b=app.Wrestlers[1-mine];
            Vector3 toward=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up).normalized;
            var input=new GBInput{ax=toward.x,az=toward.z};
            if(aim&&app.Phase==GBApp.GamePhase.Fighting){
                Vector3 target=b.bodies[1].GetComponent<Collider>().ClosestPoint(a.bodies[1].position),offset=target-a.Root.position;
                float move=Vector3.Distance(a.Root.position,b.Root.position)>.74f?.4f:0;
                int buttons=(a.hands[0].releaseRequired?0:GBInput.Left)|(a.hands[1].releaseRequired?0:GBInput.Right);
                input=new GBInput{x=toward.x*move,z=toward.z*move,ax=toward.x,az=toward.z,hx=offset.x,hy=offset.y,hz=offset.z,flags=GBInput.HandAim|buttons};
            }
            app.ScriptedInputs[mine]=input;
        }
        sessionMaximumTargets=Mathf.Max(sessionMaximumTargets,SessionTargets());
        sessionTransportStable&=app.Network.connected&&SessionField(app.Network,"sessionId")==sessionExpectedId;
        sessionFinite&=Finite();
    }
    IEnumerator SessionWait(float seconds,bool aim=false){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until&&app.Network.connected){SessionFrame(aim);yield return null;}}
    IEnumerator SessionUntil(Func<bool> condition,float seconds,bool aim=false){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until&&app.Network.connected){SessionFrame(aim);yield return null;}}
    void SessionCheck(string name,bool pass,string extra=""){Check(name,pass,"role="+app.Network.role+" connected="+app.Network.connected+" phase="+app.Phase+" epoch="+app.Epoch+" score="+app.Scores[0]+":"+app.Scores[1]+" targets="+SessionTargets()+" inputs="+app.Network.inputsReceived+" snapshots="+app.Network.snapshotsSent+"/"+app.Network.snapshotsReceived+" status="+app.Network.status+" "+extra);}
    IEnumerator SessionLifetimeRun(){
        bool host=Has("--session-host-test");int port=int.Parse(Arg("--port","7871"));app.LocalDevice=0;app.PlayerName=host?"LIFETIME HOST":"LIFETIME CLIENT";Neutral();
        bool publicInvite=Has("--session-tunnel");
        if(host){
            if(publicInvite){
                app.Network.HostInvite();float readyUntil=Time.realtimeSinceStartup+100;
                while(!app.Network.InviteReady&&app.Network.role==GBNetwork.Role.Host&&Time.realtimeSinceStartup<readyUntil)yield return null;
                SessionCheck("session_public_invite_prepared",app.Network.InviteReady);
                if(!app.Network.InviteReady)yield break;
                File.WriteAllText(Arg("--session-invite-file",Path.Combine(directory,"session-invite.txt")),app.Network.InviteLink);
            }else app.Network.Host(port,"session-lifetime-audit");
            File.WriteAllText(Path.Combine(directory,"session-listening.txt"),port.ToString());
        }else if(publicInvite){app.Network.JoinInvite(File.ReadAllText(Arg("--session-invite-file","")));}
        else app.Network.Join(Arg("--address","127.0.0.1"),port,"session-lifetime-audit");
        float until=Time.realtimeSinceStartup+(publicInvite?100:25);while(!app.Network.connected&&Time.realtimeSinceStartup<until)yield return null;
        SessionCheck("session_authenticated_two_process_peer",app.Network.connected);if(!app.Network.connected)yield break;
        sessionExpectedId=SessionField(app.Network,"sessionId");app.Network.SetReady(true);app.Network.Lobby();
        if(host)yield return SessionLifetimeHost();else yield return SessionLifetimeClient();
        Check("session_kept_same_transport_until_explicit_leave",sessionTransportStable,"same sessionId throughout round results, resets, rematch and both coordinated lobbies; no hidden reconnect");
        Check("session_manual_grip_cleanup_scope",true,"actual normal aim/click input produced maximum held targets="+sessionMaximumTargets+"; no Attach, forced count or assertion of physically earned pins; result event was deliberately staged");
        Check("session_lifetime_finite_and_runtime_clean",sessionFinite&&app.Issues.Count==0,"finite real snapshot/physics state; issues="+string.Join("; ",app.Issues));
    }
    IEnumerator SessionLifetimeHost(){
        yield return SessionUntil(()=>app.Network.peerReady,10);SessionCheck("session_both_ready_initial",app.Network.ready&&app.Network.peerReady);if(!app.Network.peerReady)yield break;
        app.Network.StartMatch();yield return SessionUntil(()=>app.Phase==GBApp.GamePhase.Fighting,7);
        SessionCheck("session_initial_countdown_enters_match",app.Phase==GBApp.GamePhase.Fighting);
        yield return SessionWait(3,true);
        for(int win=1;win<=3;win++){
            int previousEpoch=app.Epoch;app.RoundWin(0);
            SessionCheck("staged_result_"+win+"_releases_actual_grips",SessionGripsClear()&&app.Scores[0]==win&&app.Phase==(win==3?GBApp.GamePhase.MatchOver:GBApp.GamePhase.RoundOver),"production RoundWin event; not an earned-pin fixture");
            if(win<3){
                bool countdown=false;float deadline=Time.realtimeSinceStartup+9;
                while(Time.realtimeSinceStartup<deadline&&app.Network.connected&&(app.Epoch==previousEpoch||app.Phase!=GBApp.GamePhase.Fighting)){
                    SessionFrame();countdown|=app.Epoch>previousEpoch&&app.Phase==GBApp.GamePhase.Countdown;yield return null;
                }
                SessionCheck("staged_result_"+win+"_automatic_round_reset_survives",countdown&&app.Epoch>previousEpoch&&app.Phase==GBApp.GamePhase.Fighting&&SessionGripsClear()&&app.Registry.Count==92,"observed real automatic RoundOver→new epoch→Countdown→Fighting");
                // Leave a real Fighting snapshot visible to the peer before
                // staging the next result, rather than skipping it in one frame.
                yield return SessionWait(.4f);
            }
        }
        long inputs=app.Network.inputsReceived,snapshots=app.Network.snapshotsSent;yield return SessionWait(25);
        SessionCheck("match_results_idle_exceeds_peer_timeout",app.Network.connected&&app.Phase==GBApp.GamePhase.MatchOver&&app.Network.inputsReceived>inputs&&app.Network.snapshotsSent>snapshots&&SessionGripsClear(),"25s result idle; host's20s input timeout remains enabled");
        int rematchEpoch=app.Epoch;app.BeginMatch(app.CurrentArena,app.CurrentMode);yield return SessionUntil(()=>app.Phase==GBApp.GamePhase.Fighting,7);
        SessionCheck("host_rematch_uses_same_room_resets_scores",app.Network.connected&&app.Epoch>rematchEpoch&&app.Scores[0]==0&&app.Scores[1]==0&&app.Phase==GBApp.GamePhase.Fighting);
        yield return SessionUntil(()=>!app.InMatch&&app.Phase==GBApp.GamePhase.Menu,8);
        SessionCheck("client_lobby_request_returns_both_without_disconnect",app.Network.connected&&!app.InMatch&&app.Phase==GBApp.GamePhase.Menu&&!app.Network.ready&&!app.Network.peerReady&&SessionGripsClear());
        inputs=app.Network.inputsReceived;snapshots=app.Network.snapshotsSent;yield return SessionWait(25);
        SessionCheck("online_lobby_idle_exceeds_peer_timeout",app.Network.connected&&app.Phase==GBApp.GamePhase.Menu&&app.Network.inputsReceived>inputs&&app.Network.snapshotsSent>snapshots,"25s lobby idle; actual neutral client heartbeats continue");
        app.Network.SetReady(true);app.Network.Lobby();yield return SessionUntil(()=>app.Network.peerReady,10);
        SessionCheck("same_room_can_ready_up_again",app.Network.ready&&app.Network.peerReady&&app.Network.connected);
        app.Network.StartMatch();yield return SessionUntil(()=>app.Phase==GBApp.GamePhase.Fighting,7);
        SessionCheck("same_room_restarts_after_client_lobby",app.Phase==GBApp.GamePhase.Fighting&&app.Scores[0]==0&&app.Scores[1]==0&&app.Network.connected);
        yield return SessionWait(1);app.ReturnToOnlineLobby();yield return SessionWait(3);
        SessionCheck("host_lobby_returns_without_disconnect",app.Network.connected&&app.Phase==GBApp.GamePhase.Menu&&!app.InMatch&&!app.Network.ready&&!app.Network.peerReady&&SessionGripsClear());
        app.Network.Stop();SessionCheck("explicit_leave_alone_closes_host_session",!app.Network.connected&&app.Network.role==GBNetwork.Role.Offline);
    }
    IEnumerator SessionLifetimeClient(){
        bool firstResult=false,secondResult=false,matchResults=false,firstReset=false,secondReset=false,firstCountdown=false,secondCountdown=false;
        int initialEpoch=app.Epoch,sequence=SessionField(app.Network,"inputSequence");float deadline=Time.realtimeSinceStartup+35;
        while(Time.realtimeSinceStartup<deadline&&app.Network.connected&&!matchResults){
            SessionFrame(!firstResult);int score=app.Scores[0];
            firstResult|=score==1&&app.Phase==GBApp.GamePhase.RoundOver;secondResult|=score==2&&app.Phase==GBApp.GamePhase.RoundOver;
            firstCountdown|=firstResult&&score==1&&app.Phase==GBApp.GamePhase.Countdown;secondCountdown|=secondResult&&score==2&&app.Phase==GBApp.GamePhase.Countdown;
            firstReset|=firstResult&&score==1&&app.Phase==GBApp.GamePhase.Fighting&&SessionGripsClear();secondReset|=secondResult&&score==2&&app.Phase==GBApp.GamePhase.Fighting&&SessionGripsClear();
            matchResults=score==3&&app.Phase==GBApp.GamePhase.MatchOver;yield return null;
        }
        SessionCheck("client_first_result_and_automatic_reset_stream",firstResult&&firstCountdown&&firstReset&&app.Epoch>initialEpoch);
        SessionCheck("client_second_result_and_automatic_reset_stream",secondResult&&secondCountdown&&secondReset);
        SessionCheck("client_three_wins_reach_match_results",matchResults&&SessionGripsClear());if(!matchResults)yield break;
        long snapshots=app.Network.snapshotsReceived;yield return SessionUntil(()=>app.Phase==GBApp.GamePhase.Fighting&&app.Scores[0]==0&&app.Scores[1]==0,35);
        SessionCheck("client_result_idle_and_host_rematch_survive",app.Network.connected&&app.Phase==GBApp.GamePhase.Fighting&&app.Network.snapshotsReceived>snapshots);
        yield return SessionWait(1);app.ReturnToOnlineLobby();yield return SessionUntil(()=>!app.InMatch&&app.Phase==GBApp.GamePhase.Menu,8);
        SessionCheck("client_menu_action_keeps_room_and_clears_ready",app.Network.connected&&app.Phase==GBApp.GamePhase.Menu&&!app.InMatch&&!app.Network.ready&&!app.Network.peerReady&&SessionGripsClear());
        SessionCheck("client_input_sequence_survives_lobby",SessionField(app.Network,"inputSequence")>sequence,"initial="+sequence+" current="+SessionField(app.Network,"inputSequence"));sequence=SessionField(app.Network,"inputSequence");
        snapshots=app.Network.snapshotsReceived;yield return SessionWait(25);
        SessionCheck("client_lobby_idle_keeps_snapshots_and_monotonic_inputs",app.Network.connected&&app.Phase==GBApp.GamePhase.Menu&&app.Network.snapshotsReceived>snapshots&&SessionField(app.Network,"inputSequence")>sequence);
        app.Network.SetReady(true);yield return SessionUntil(()=>app.Phase==GBApp.GamePhase.Fighting&&app.InMatch,12);
        SessionCheck("client_same_room_ready_and_restart_stream",app.Network.connected&&app.Phase==GBApp.GamePhase.Fighting&&app.Scores[0]==0&&app.Scores[1]==0);
        yield return SessionUntil(()=>!app.InMatch&&app.Phase==GBApp.GamePhase.Menu,8);
        SessionCheck("client_host_lobby_transition_survives",app.Network.connected&&app.Phase==GBApp.GamePhase.Menu&&!app.Network.ready&&!app.Network.peerReady&&SessionGripsClear());
        // Do not mark the intended final transport close as a survival failure.
        float end=Time.realtimeSinceStartup+8;while(app.Network.connected&&Time.realtimeSinceStartup<end)yield return null;
        SessionCheck("explicit_host_leave_returns_client_to_menu",!app.Network.connected&&!app.InMatch&&app.Phase==GBApp.GamePhase.Menu&&app.Network.role==GBNetwork.Role.Offline);
    }
}
