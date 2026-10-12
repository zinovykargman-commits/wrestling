using UnityEngine;
using System.Collections;
using System.IO;

public partial class GBAudit {
    IEnumerator RingRestartRun(){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;Neutral();
        for(int offender=0;offender<2;offender++){
            app.ResetRound(false);var low=app.Wrestlers[offender];
            // Includes a thrown airborne body; reset follows the exited body,
            // never the player who held or propelled it.
            low.ResetTo(new Vector3(offender==0?5.4f:0,offender==0?0:2,offender==1?-5.4f:0));
            foreach(var rb in low.bodies)rb.linearVelocity=new Vector3(6,3,0);
            app.Scores[0]=1;app.Scores[1]=2;int epoch=app.Epoch;app.CheckRingOut();
            low=app.Wrestlers[offender];var top=app.Wrestlers[1-offender];
            Check("offender_"+offender+"_bottom",app.RingRestartBottom==offender&&app.Epoch==epoch+1,"actual boundary-crossing wrestler; airborne launch allowed");
            Check("offender_"+offender+"_connected_pose",low.JointSeparation()<.001f&&top.JointSeparation()<.001f,"gap="+low.JointSeparation());
            Check("offender_"+offender+"_kneeling",Mathf.Abs(Vector3.Dot(low.bodies[1].transform.up,Vector3.up))<.5f&&low.bodies[5].position.y<.35f&&low.bodies[8].position.y<.35f&&low.bodies[5].position.z>low.Root.position.z+.5f&&top.Root.position.z<low.Root.position.z-.9f,"hips="+low.Root.position+" hand="+low.bodies[5].position+" knee="+(low.bodies[10].position+low.bodies[10].rotation*low.joints[10].anchor));
            bool clear=true;foreach(var body in low.bodies)foreach(var c in body.GetComponents<Collider>())clear&=c.bounds.min.y>=.19f;
            Check("offender_"+offender+"_above_canvas",clear,"no body collider initially penetrates mat top .2");
            Check("offender_"+offender+"_scores_unchanged",app.Scores[0]==1&&app.Scores[1]==2&&app.PinCount==0&&low.GripCount==0&&top.GripCount==0,"no score, free grip or free pin on restart");
            Check("offender_"+offender+"_1_5_seconds",Mathf.Abs(app.PhaseSeconds-1.5f)<.03f&&app.CountdownNumber==3,"seconds="+app.PhaseSeconds+" number="+app.CountdownNumber);
            Vector3 start=low.Root.position;float begin=Time.time;
            app.ScriptedInputs[0]=app.ScriptedInputs[1]=new GBInput{x=1,ax=1,flags=GBInput.Left|GBInput.Right|GBInput.Jump|GBInput.Pin};
            if(offender==0){yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"restart-3.png"));}
            while(Time.time-begin<.58f)yield return null;
            Check("offender_"+offender+"_count_2",app.CountdownNumber==2&&app.Phase==GBApp.GamePhase.Countdown,"seconds="+app.PhaseSeconds);
            if(offender==0)ScreenCapture.CaptureScreenshot(Path.Combine(directory,"restart-2.png"));
            while(Time.time-begin<1.08f)yield return null;
            Check("offender_"+offender+"_count_1",app.CountdownNumber==1&&app.Phase==GBApp.GamePhase.Countdown,"seconds="+app.PhaseSeconds);
            if(offender==0)ScreenCapture.CaptureScreenshot(Path.Combine(directory,"restart-1.png"));
            Check("offender_"+offender+"_input_locked",Vector3.Distance(start,low.Root.position)<.001f&&low.GripCount==0&&low.stamina==GBWrestler.MaxStamina,"gameplay inputs cannot move bodies or spend stamina before whistle");
            Neutral();while(Time.time-begin<1.505f)yield return null;
            Check("offender_"+offender+"_whistle",app.Phase==GBApp.GamePhase.Fighting&&app.Sfx.LastPlayed=="whistle"&&app.Sfx.whistle.length>.25f&&!low.Root.isKinematic,"countdown ends, whistle clip issued and physics resumes");
            yield return Wait(.2f);
            if(offender==0){ScreenCapture.CaptureScreenshot(Path.Combine(directory,"restart-after-whistle.png"));yield return null;}
            Check("offender_"+offender+"_stays_low",low.Root.position.y<.9f&&low.JointSeparation()<.06f&&app.PinCount==0,"hips="+low.Root.position+" gap="+low.JointSeparation());
        }
        app.ResetRound(false);app.Wrestlers[0].ResetTo(new Vector3(4.15f,.28f,0));int before=app.Epoch;app.CheckRingOut();
        Check("rope_contact_does_not_reset",before==app.Epoch,"contact at ropes stays in ring; boundary is outside rope perimeter");
        app.BeginMatch(3,1);Check("ordinary_start_preserved",app.RingRestartBottom==-1&&Mathf.Abs(app.PhaseSeconds-3)<.02f,"normal start remains 3 seconds; ring-out alone is 1.5 seconds");
        Check("restart_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
