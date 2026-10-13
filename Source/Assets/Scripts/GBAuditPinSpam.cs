using UnityEngine;
using System.Collections;
using System.IO;

public partial class GBAudit {
    void PinSpamSetup(bool chestDown){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.Paused=true;app.enabled=false;Neutral();
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(-.55f,-.08f,0));b.ResetTo(Vector3.zero);
        foreach(var wrestler in new[]{a,b})foreach(var rb in wrestler.bodies){rb.interpolation=RigidbodyInterpolation.None;rb.transform.SetPositionAndRotation(rb.position,rb.rotation);}
        Quaternion flat=Quaternion.Euler(chestDown?90:-90,0,0);Vector3 root=new Vector3(.35f,.46f,0);
        for(int n=0;n<15;n++){var rb=b.bodies[n];Vector3 at=root+flat*(b.initial[n]-b.initial[0]);rb.transform.SetPositionAndRotation(at,flat);rb.position=at;rb.rotation=flat;rb.linearVelocity=rb.angularVelocity=Vector3.zero;}
        Physics.SyncTransforms();a.command=new GBInput{az=1,flags=GBInput.Pin|GBInput.Left};a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;a.SendMessage("PinRefresh");
    }
    void PinSpamLift(GBWrestler b,float height){foreach(var rb in b.bodies){rb.position+=Vector3.up*height;rb.transform.SetPositionAndRotation(rb.position,rb.rotation);}Physics.SyncTransforms();}
    void PinSpamTicks(int ticks){for(int n=0;n<ticks;n++)app.SendMessage("CheckPin");}
    IEnumerator PinSpamRun(){
        var oldMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        foreach(bool front in new[]{false,true}){
            PinSpamSetup(front);yield return null;var a=app.Wrestlers[0];var b=app.Wrestlers[1];string face=front?"chest":"back";
            bool l,r;Check(face+"_earned_control",a.IsCovering&&b.ShoulderContact(out l,out r),"prepared real grip + both actual compound shoulder contacts");
            PinSpamTicks(144);float earned=app.PinCount;PinSpamLift(b,.12f);PinSpamTicks(20);
            Check(face+"_twitch_pauses_without_reset",Mathf.Abs(app.PinCount-earned)<.00001f&&app.PinAttacker==0&&app.Scores[0]==0,"earned="+earned+" after0.083s shoulder lift="+app.PinCount);
            PinSpamLift(b,-.12f);PinSpamTicks(20);Check(face+"_contact_resumes_count",app.PinCount>earned+.07f,"count="+app.PinCount);
            bool retained=true;float previous=app.PinCount;for(int cycle=0;cycle<20;cycle++){PinSpamLift(b,.12f);PinSpamTicks(12);retained&=Mathf.Abs(app.PinCount-previous)<.00001f;PinSpamLift(b,-.12f);PinSpamTicks(12);retained&=app.PinCount>previous;previous=app.PinCount;}
            Check(face+"_repeated_twitches_cannot_erase_progress",retained&&app.PinCount>1.6f,"20 short lifts pause; every real contact interval adds time; count="+app.PinCount);
            PinSpamLift(b,.12f);PinSpamTicks(Mathf.CeilToInt(GBApp.PinEscapeSeconds/Time.fixedDeltaTime));Check(face+"_real_shoulder_escape_resets",app.PinCount==0&&app.PinAttacker==-1&&a.GripCount==1,"sustained0.18s lift resets; never breaks actual grip");
            PinSpamLift(b,-.12f);PinSpamTicks(80);PinSpamLift(b,.12f);PinSpamTicks(10);a.command.flags&=~GBInput.Pin;app.SendMessage("CheckPin");Check(face+"_release_C_cancels_immediately",app.PinCount==0&&app.PinAttacker==-1,"C release bypasses twitch grace");
            PinSpamLift(b,-.12f);a.command.flags|=GBInput.Pin;a.SendMessage("PinRefresh");PinSpamTicks(80);a.Release(0);app.SendMessage("CheckPin");Check(face+"_broken_hold_cancels_immediately",app.PinCount==0&&app.PinAttacker==-1,"live grip loss bypasses twitch grace");
            a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;a.SendMessage("PinRefresh");PinSpamTicks(1);app.PinCount=GBApp.PinSeconds-.001f;PinSpamLift(b,.12f);PinSpamTicks(20);
            Check(face+"_cannot_win_on_raised_shoulders",app.Scores[0]==0&&app.PinCount<GBApp.PinSeconds,"count never advances or wins during a twitch");
            PinSpamLift(b,-.12f);app.SendMessage("CheckPin");Check(face+"_finish_requires_real_contact",app.Scores[0]==1,"2.5s earned contact, actual shoulders restored at finish");
        }
        foreach(bool moving in new[]{false,true}){
            PinSpamSetup(false);yield return null;var a=app.Wrestlers[0];var b=app.Wrestlers[1];Check((moving?"moving":"stationary")+"_live_close_pin_control",b.PinControlled&&b.IsBeingPinned,"control="+b.PinControlled+" cover="+b.IsBeingPinned+" chestRange="+Vector3.Distance(a.bodies[1].position,b.bodies[1].position)+" "+a.CoverGeometry);b.downUntil=0;b.stamina=80;float initial=b.stamina,maxGain=0;int activations=0;bool wasActive=false;float begin=Time.time;
            while(Time.time-begin<1.6f){
                bool pressed=(Mathf.FloorToInt((Time.time-begin)/.025f)&1)==0;
                var input=new GBInput{az=1,x=moving?1:0,flags=pressed?GBInput.Brace:0};float before=b.stamina;b.Simulate(input);
                bool active=b.command.Has(GBInput.Brace);if(active&&!wasActive)activations++;wasActive=active;maxGain=Mathf.Max(maxGain,b.stamina-before);yield return new WaitForFixedUpdate();
            }
            string mode=moving?"moving":"stationary";
            Check(mode+"_Ctrl_taps_do_not_recharge",maxGain<.00001f&&b.stamina<initial&&activations==1,"32 rapid tap cycles; stamina="+initial+" -> "+b.stamina+" max gain="+maxGain+" accepted bursts="+activations);
            Check(mode+"_grip_remains_physical",a.GripCount==1&&b.EscapeUntil==0,"no scripted grip release or timed immunity");
            float rest=b.stamina;float until=Time.time+.7f;while(Time.time<until){b.Simulate(new GBInput{az=1});yield return new WaitForFixedUpdate();}
            Check(mode+"_rest_under_pin_waits_for_ground_delay",Mathf.Abs(b.stamina-rest)<.00001f&&b.PinControlled&&!b.GroundStaminaRecoveryReady,"live C+grip control, ground seconds="+b.GroundStaminaSeconds+" stamina="+b.stamina);
            until=Time.time+.4f;while(Time.time<until){b.Simulate(new GBInput{az=1});yield return new WaitForFixedUpdate();}
            Check(mode+"_rest_under_pin_recovers_after_2_5_seconds",b.stamina>rest&&b.PinControlled&&b.GroundStaminaRecoveryReady,"resting defender recovers without a grip release; ground seconds="+b.GroundStaminaSeconds);
            a.command.flags&=~GBInput.Pin;a.SendMessage("PinRefresh");float beforeRecovery=b.stamina;b.Simulate(new GBInput{az=1});
            Check(mode+"_recovery_after_control_breaks",b.stamina>beforeRecovery&&Mathf.Abs((b.stamina-beforeRecovery)/Time.fixedDeltaTime-19)<.04f,"normal19/s returns after actual control ends and attempt delay expires");
            b.Simulate(new GBInput{az=1,x=1,flags=GBInput.Brace});b.Simulate(new GBInput{az=1});rest=b.stamina;
            until=Time.time+.5f;while(Time.time<until){b.Simulate(new GBInput{az=1});yield return new WaitForFixedUpdate();}
            Check(mode+"_release_has_recovery_delay",Mathf.Abs(b.stamina-rest)<.00001f,"cooldown/release is not a free regen window, delay0.8s");
            yield return new WaitForSeconds(.35f);beforeRecovery=b.stamina;b.Simulate(new GBInput{az=1});Check(mode+"_delayed_recovery_is_not_permanent",b.stamina>beforeRecovery,"rest after0.8s resumes regeneration outside a pin");
        }
        foreach(var wrestler in app.Wrestlers)foreach(var rb in wrestler.bodies)rb.interpolation=RigidbodyInterpolation.Interpolate;
        app.enabled=true;app.Paused=false;app.ResetRound(false);Physics.simulationMode=oldMode;app.ReturnToMenu();
        Check("pin_spam_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
