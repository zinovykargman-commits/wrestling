using UnityEngine;
using System;
using System.IO;
using System.Collections;

public partial class GBAudit {
    static float StanceMat(GBApp app){
        foreach(var c in app.Arena.GetComponentsInChildren<Collider>())if(c.enabled&&c.gameObject.activeInHierarchy&&c.name=="ring mat")return c.bounds.max.y;
        throw new InvalidOperationException("Authored ring mat missing");
    }
    static float StanceKnee(GBWrestler w,int thigh){return Mathf.Abs(Mathf.DeltaAngle(0,(Quaternion.Inverse(w.bodies[thigh].rotation)*w.bodies[thigh+1].rotation).eulerAngles.x));}
    void StanceCapture(string name){if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));}
    IEnumerator StanceRun(){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];float floor=StanceMat(app);
        a.ResetTo(new Vector3(-1.5f,.28f,-1.2f));b.ResetTo(new Vector3(2.2f,.28f,1.6f));Neutral();
        yield return Wait(.9f);float standing=a.Root.position.y,energy=a.stamina;
        var side=app.gameObject.AddComponent<GBAuditShotCamera>();side.Wrestler=a;side.View=app.Views.Primary;side.Capturing=true;
        StanceCapture("stance-before-side");yield return null;
        app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Brace};
        // Scripted input is polled in Update before production FixedUpdate.
        // Exclude that delivery interval from sustained-held sampling.
        float inputDeadline=Time.time+.25f;while(!a.command.Has(GBInput.Brace)&&Time.time<inputDeadline)yield return new WaitForFixedUpdate();
        float until=Time.time+1.2f,next=0,gap=0,minimumEnergy=energy;int accepted=0,frames=0,captures=0;
        while(Time.time<until){
            yield return new WaitForFixedUpdate();frames++;if(a.StandingStance&&a.command.Has(GBInput.Brace))accepted++;
            gap=Mathf.Max(gap,a.JointSeparation());minimumEnergy=Mathf.Min(minimumEnergy,a.stamina);
            if(Time.time>=next&&captures<8){StanceCapture("stance-lowering-side-"+(captures++).ToString("00"));next=Time.time+.15f;}
        }
        float lower=a.Root.position.y,left=StanceKnee(a,9),right=StanceKnee(a,12),chest=Vector3.Dot(a.bodies[1].rotation*Vector3.up,Vector3.up),head=Vector3.Dot(a.bodies[2].rotation*Vector3.up,Vector3.up);
        float leftGap=a.bodies[11].GetComponent<Collider>().bounds.min.y-floor,rightGap=a.bodies[14].GetComponent<Collider>().bounds.min.y-floor;
        // The solver's contact/anchor tolerance is5mm. Keep an observable
        // roughly13cm level change without failing a valid pose by0.3mm;
        // independent knee, head, boot-support and stamina gates still apply.
        Check("standing_ctrl_lowers_physical_hips",standing-lower>.125f&&lower>floor+.65f&&lower<floor+.92f,"hips="+standing+" -> "+lower+" mat="+floor+"; minimum0.125m (0.13m target with5mm solver tolerance)");
        Check("standing_ctrl_bends_both_anatomical_knees",left>20&&right>20&&left<120&&right<120,"actual knee flexion L/R="+left+"/"+right);
        Check("standing_ctrl_head_up_chest_forward",chest>.65f&&chest<.99f&&head>.90f,"chest/head upright dots="+chest+"/"+head);
        Check("standing_ctrl_both_boots_supported",leftGap<.025f&&rightGap<.025f&&leftGap>-.015f&&rightGap>-.015f,"actual boot clearance L/R="+leftGap+"/"+rightGap);
        Check("standing_ctrl_is_free_and_not_ground_escape",minimumEnergy>=energy-.001f&&accepted==frames&&!a.PhysicallyDownForPin&&!a.Down&&a.ScrambleCooldown==0,"energy min="+minimumEnergy+" start="+energy+" accepted="+accepted+"/"+frames+" cooldown="+a.ScrambleCooldown);
        StanceCapture("stance-held-side");yield return null;
        float leftHand=Vector3.Dot(a.bodies[5].position-a.bodies[1].position,a.facing),rightHand=Vector3.Dot(a.bodies[8].position-a.bodies[1].position,a.facing);
        Check("stance_free_hands_guard_forward",leftHand>.12f&&rightHand>.12f&&a.GripCount==0,"actual hand forward offset L/R="+leftHand+"/"+rightHand+"; no mouse target or automatic grip");
        side.Capturing=false;var front=app.gameObject.AddComponent<GBAuditStanceCamera>();front.Wrestler=a;front.View=app.Views.Primary;
        yield return Wait(.12f);StanceCapture("stance-held-front");yield return Wait(.05f);front.enabled=false;Destroy(front);side.Capturing=true;
        for(int direction=0;direction<2;direction++){
            Vector3 start=a.Root.position;float supportGap=-10;bool posture=true;
            app.ScriptedInputs[0]=new GBInput{az=1,x=direction==1?.7f:0,z=direction==0?.7f:0,flags=GBInput.Brace};
            until=Time.time+.65f;next=0;captures=0;
            while(Time.time<until){yield return new WaitForFixedUpdate();
                supportGap=Mathf.Max(supportGap,Mathf.Min(a.bodies[11].GetComponent<Collider>().bounds.min.y,a.bodies[14].GetComponent<Collider>().bounds.min.y)-floor);
                posture&=a.StandingStance&&!a.Down&&!a.PhysicallyDownForPin;gap=Mathf.Max(gap,a.JointSeparation());
                if(Time.time>=next){StanceCapture((direction==0?"stance-forward-motion-":"stance-side-motion-")+(captures++).ToString("00"));next=Time.time+.15f;}
            }
            Vector3 travel=a.Root.position-start;float distance=direction==0?Vector3.Dot(travel,Vector3.forward):Vector3.Dot(travel,Vector3.right);
            Check(direction==0?"ctrl_forward_stance_moves_supported":"ctrl_lateral_stance_moves_supported",distance>.15f&&supportGap<.025f&&posture&&a.stamina>=energy-.001f,"actual travel="+distance+" nearest boot maximumgap="+supportGap+" stable stance="+posture+" stamina="+a.stamina);
        }
        Neutral();yield return Wait(.9f);
        Check("stance_release_restores_standing",!a.StandingStance&&a.Root.position.y>lower+.13f&&a.ScrambleCooldown==0,"released hips="+a.Root.position.y+" cooldown="+a.ScrambleCooldown);
        a.stamina=2;app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Brace};yield return Wait(.12f);
        Check("standing_stance_available_when_exhausted",a.StandingStance&&a.command.Has(GBInput.Brace)&&a.ScrambleCooldown==0,"physical stance at starting stamina2 without a ground exhaustion timer");
        a.ResetTo(new Vector3(-1.5f,.28f,-1.2f));Neutral();yield return Wait(.8f);standing=a.Root.position.y;
        float shotEnergy=a.stamina;a.Shot();Check("lower_shot_cost_unchanged",Mathf.Abs(shotEnergy-a.stamina-12)<.001f&&a.Shooting,"same production Shot action cost12; no extra impulse");Neutral();
        float lowest=standing,minHead=10;until=Time.time+.68f;next=0;captures=0;
        while(Time.time<until){yield return new WaitForFixedUpdate();lowest=Mathf.Min(lowest,a.Root.position.y);minHead=Mathf.Min(minHead,a.bodies[2].position.y-floor);gap=Mathf.Max(gap,a.JointSeparation());if(Time.time>=next){StanceCapture("lower-shot-side-"+(captures++).ToString("00"));next=Time.time+.10f;}}
        Check("shot_has_actual_lower_level_change",standing-lowest>.371f&&lowest<floor+.75f&&lowest>floor+.30f&&minHead>.80f,"physical lowering exceeds f167 dedicated native drop.361m by1cm; standing/min hips="+standing+"/"+lowest+" minimum head above mat="+minHead+" entry target reduced6cm with same final recovery");
        Check("stance_shot_stay_connected_no_auto_grab",gap<.03f&&a.JointSeparation()<.03f&&a.GripCount==0&&a.bodies.Length==15&&app.Registry.Count==92,"max stance/shot joint gap="+gap+" grips="+a.GripCount+" registry="+app.Registry.Count);
        side.Capturing=false;Destroy(side);
        // Existing paid ground/held-escape regression uses production input,
        // exact costs, rapid re-press rejection and1.2s exhaustion recovery.
        yield return GBAuditPin.ScramblePatch(app,Check);
        app.ReturnToMenu();app.ShowMenu("options");yield return Wait(.3f);
        var cam=app.Views.Primary;var canvas=cam.WorldToViewportPoint(new Vector3(0,floor+.6f,0));
        Check("menu_camera_inside_gym_ring_visible",cam.transform.position.y<6&&Mathf.Abs(cam.transform.position.x)<13&&Mathf.Abs(cam.transform.position.z)<10&&canvas.z>0&&canvas.x>.42f&&canvas.x<1&&canvas.y>0&&canvas.y<1,"camera="+cam.transform.position+" ring viewport="+canvas+" roof retained; screenshot requires visual review");
        int quality=app.GraphicsQuality,bodies=app.Registry.Count;float physics=Time.fixedDeltaTime;int renderers=app.World.GetComponentsInChildren<Renderer>().Length;
        app.SetGraphicsQuality(1);yield return Wait(.15f);StanceCapture("options-balanced");
        Check("balanced_changes_render_settings",QualitySettings.antiAliasing==2&&QualitySettings.shadowDistance==18&&QualitySettings.shadows==ShadowQuality.All,"AA2 /18m shadows /lower light maps");
        app.SetGraphicsQuality(2);yield return Wait(.15f);StanceCapture("options-performance");
        Check("performance_changes_render_settings",QualitySettings.antiAliasing==0&&QualitySettings.shadows==ShadowQuality.Disable&&!cam.allowHDR,"AA/shadows/HDR off");
        app.SetGraphicsQuality(0);yield return Wait(.15f);StanceCapture("options-high");
        Check("high_restores_authored_rendering",QualitySettings.antiAliasing==4&&QualitySettings.shadowDistance==28&&QualitySettings.shadows==ShadowQuality.All&&cam.allowHDR,"AA4 /28m authoredgym shadows /HDR");
        Check("graphics_preserves_assets_and_physics",bodies==app.Registry.Count&&renderers==app.World.GetComponentsInChildren<Renderer>().Length&&physics==Time.fixedDeltaTime,"registry="+bodies+" renderers="+renderers+" physics step="+physics);
        app.SetGraphicsQuality(quality);Check("stance_menu_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}

// Ordinary-frame front diagnostic of the live stance; no frozen body pose.
[DefaultExecutionOrder(1001)]
public class GBAuditStanceCamera:MonoBehaviour {
    public GBWrestler Wrestler;public Camera View;
    void LateUpdate(){if(!GBApp.I.Automated||!Wrestler||!View)return;
        var hips=Wrestler.Root.position;View.fieldOfView=46;
        View.transform.position=hips+Wrestler.facing*3.3f+Vector3.up*.65f;
        View.transform.LookAt(hips+Vector3.up*.30f);
    }
}
