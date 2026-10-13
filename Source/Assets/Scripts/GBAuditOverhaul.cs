using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

public partial class GBAudit {
    bool measureArmContacts;float maximumArmPenetration;
    void MeasureArmContacts(GBWrestler wrestler){foreach(int arm in new[]{4,5,7,8}){var a=wrestler.bodies[arm].GetComponent<Collider>();for(int other=0;other<15;other++){if(other==arm)continue;var b=wrestler.bodies[other].GetComponent<Collider>();if(Physics.GetIgnoreCollision(a,b)||!a.bounds.Intersects(b.bounds))continue;Vector3 direction;float distance;if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out direction,out distance))maximumArmPenetration=Mathf.Max(maximumArmPenetration,distance);}}}
    IEnumerator OverhaulRun(){
        measureArmContacts=true;
        app.LocalDevice=0;app.TrainingPartner=0;app.BeginMatch(3,2);Neutral();yield return Wait(2);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];int incidentalDown=0;float maxLean=0;
        float end=Time.realtimeSinceStartup+5;
        while(Time.realtimeSinceStartup<end){if(a.Down)incidentalDown++;maxLean=Mathf.Max(maxLean,1-Vector3.Dot(a.bodies[1].transform.up,Vector3.up));yield return null;}
        Check("idle_no_accidental_knockdown",incidentalDown==0&&maxLean<.25f,"down frames="+incidentalDown+" maximum torso lean="+maxLean);
        int ignoredNeighbors=0,ignoredOther=0;for(int i=0;i<15;i++)for(int j=i+1;j<15;j++){bool ignored=Physics.GetIgnoreCollision(a.bodies[i].GetComponent<Collider>(),a.bodies[j].GetComponent<Collider>());bool neighbor=a.parents[i]==j||a.parents[j]==i;if(ignored&&neighbor)ignoredNeighbors++;if(ignored&&!neighbor)ignoredOther++;}
        Check("self_collisions_except_joint_neighbors",ignoredNeighbors==14&&ignoredOther==0,"ignored neighbors="+ignoredNeighbors+" other="+ignoredOther);
        a.ResetTo(new Vector3(-1,.25f,-1));b.ResetTo(new Vector3(-1,.25f,2));Neutral();yield return Wait(.7f);Vector3 shotStart=a.Root.position;app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Strike};yield return Wait(.3f);
        Check("forward_shot_without_automatic_grab",a.Root.position.z>shotStart.z+.45f&&a.GripCount==0&&a.Shooting,"forward="+(a.Root.position.z-shotStart.z)+" hands="+a.GripCount+" shooting="+a.Shooting);
        app.Capture(Path.Combine(directory,"forward-shot.png"));Neutral();yield return Wait(1);

        CloseSetup();yield return Wait(.8f);app.ScriptedInputs[0]=new GBInput{ax=1,flags=3};yield return Wait(1.6f);
        Check("upper_body_grab_natural",a.GripCount>0&&((a.hands[0].target&&a.hands[0].target.Body.position.y>b.Root.position.y)||(a.hands[1].target&&a.hands[1].target.Body.position.y>b.Root.position.y)),"natural reach: "+a.hands[0].Label+" / "+a.hands[1].Label);
        app.Capture(Path.Combine(directory,"upper-body-grab.png"));
        app.ScriptedInputs[1]=new GBInput{ax=-1,flags=GBInput.Brace};Vector3 before=a.Root.position;
        app.ScriptedInputs[0]=new GBInput{x=-1,ax=1,flags=3};yield return Wait(1.2f);
        Check("movement_against_resisting_hold",a.Root.position.x<before.x-.35f&&!a.Down,"backward displacement="+(before.x-a.Root.position.x));
        Neutral();CloseSetup();yield return Wait(.8f);app.ScriptedInputs[0]=new GBInput{ax=1,flags=3|GBInput.Pin};yield return Wait(1.8f);
        bool leg=false;foreach(var h in a.hands)if(h.target&&h.target.wrestler==b&&h.target.Body.position.y<b.Root.position.y)leg=true;
        Check("leg_selection_natural",leg,"C modifies selection: "+a.hands[0].Label+" / "+a.hands[1].Label);
        app.Capture(Path.Combine(directory,"leg-grab.png"));
        Neutral();CloseSetup();yield return Wait(.8f);app.ScriptedInputs[0]=new GBInput{ax=1,flags=3};yield return Wait(1.5f);
        Check("escape_fixture_acquired",a.GripCount>0,"natural grab before struggle");
        app.ScriptedInputs[1]=new GBInput{x=1,ax=-1,flags=GBInput.Brace};yield return Wait(.85f);
        Check("escape_releases_and_protects",a.GripCount==0&&b.EscapeUntil>Time.time,"attacker hands="+a.GripCount+" escape remaining="+(b.EscapeUntil-Time.time));
        yield return Wait(.2f);Check("escape_prevents_immediate_recapture",a.GripCount==0,"held grip buttons cannot instantly recapture escaped opponent");
        Neutral();a.ResetTo(new Vector3(-2,.25f,0));b.ResetTo(new Vector3(2,.25f,0));yield return Wait(1);
        float priorBalance=a.balance;a.Contact(2,20);yield return Wait(.2f);Check("small_bump_not_ragdoll",!a.Down&&a.balance>=priorBalance-10,"balance="+a.balance);
        a.Knock(100,.7f);yield return Wait(.1f);Check("committed_hit_knocks_down",a.Down,"high damage fixture");yield return Wait(3.5f);
        Check("recovery_returns_to_stance",!a.Down&&a.Root.position.y>.95f&&Vector3.Dot(a.bodies[1].transform.up,Vector3.up)>.65f,"pelvis="+a.Root.position.y+" upright="+Vector3.Dot(a.bodies[1].transform.up,Vector3.up));
        Vector3 v=app.Views.Primary.WorldToViewportPoint(a.Root.position+Vector3.up*.5f);Check("body_visible_after_recovery",v.z>0&&v.x>.15f&&v.x<.85f&&v.y>.15f&&v.y<.85f,"actual body viewport="+v);
        a.stamina=0;a.Lift();Check("exhaustion_feedback",a.ActionFeedback.ToLowerInvariant().Contains("stamina")||a.ActionFeedback.ToLowerInvariant().Contains("exhaust"),a.ActionFeedback);
        app.ScriptedInputs=new GBInput[1];app.TrainingPartner=0;app.ResetDrill();yield return Wait(1);a=app.Wrestlers[0];b=app.Wrestlers[1];
        Check("passive_training_partner",b.command.flags==0&&b.command.Move.sqrMagnitude==0,"passive partner neutral input");
        app.SetTrainingPartner(1);a.ResetTo(new Vector3(-.5f,.25f,0));b.ResetTo(new Vector3(.5f,.25f,0));app.ScriptedInputs[0]=new GBInput{ax=1,flags=3};bool sawResistance=false,initiatedAttack=false;float resistEnd=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<resistEnd){sawResistance|=b.command.Has(GBInput.Brace);initiatedAttack|=b.command.Has(GBInput.Strike)||b.command.Has(GBInput.Left);yield return null;}
        Check("resisting_training_partner",sawResistance&&!initiatedAttack,"observed brace="+sawResistance+" initiated attack="+initiatedAttack);
        app.SetTrainingPartner(2);NeutralOne();yield return Wait(4);Check("attacking_training_partner",b.moveDistance>.5f,"partner movement="+b.moveDistance);
        app.ResetDrill();yield return Wait(.5f);Check("quick_reset_preserves_partner_mode",app.TrainingPartner==2&&app.Phase==GBApp.GamePhase.Fighting&&app.Wrestlers[0].stamina>95,"clean physical round; partner choice retained");
        app.ScriptedInputs=null;app.ApplyFocus(true);var keys=new HashSet<KeyCode>();app.AuditKeyDown=k=>keys.Remove(k);
        keys.Add(KeyCode.F11);yield return Wait(.5f);Check("f11_enters_fullscreen",Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"mode="+Screen.fullScreenMode);
        keys.Add(KeyCode.F11);yield return Wait(.5f);Check("f11_restores_window",Screen.fullScreenMode==FullScreenMode.Windowed,"mode="+Screen.fullScreenMode);
        keys.Add(KeyCode.T);int epoch=app.Epoch;yield return Wait(.3f);Check("t_key_resets_training",app.Epoch>epoch,"epoch="+app.Epoch);app.AuditKeyDown=null;
        int[] decisions=new int[3];for(int difficulty=0;difficulty<3;difficulty++){app.AIDifficulty=difficulty;app.TrainingPartner=0;app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.ScriptedInputs=new GBInput[1];NeutralOne();var bot=new GBBot();float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<2){bot.Tick(app,app.Wrestlers[1],app.Wrestlers[0]);yield return null;}decisions[difficulty]=bot.Decisions;}
        Check("difficulty_changes_reaction_speed",decisions[0]<decisions[1]&&decisions[1]<decisions[2],"decision counts easy/normal/hard="+string.Join("/",decisions));
        Check("arms_physically_resist_self_intersection",maximumArmPenetration<.045f,"maximum measured nonadjacent forearm/hand penetration="+maximumArmPenetration.ToString("0.0000")+"m during stance, shots, holds, escape and recovery");
        Check("overhaul_finite_and_no_errors",Finite()&&app.Issues.Count==0,string.Join("; ",app.Issues));
    }
    void NeutralOne(){app.ScriptedInputs[0]=new GBInput{az=1};}
}
