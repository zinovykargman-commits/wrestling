using UnityEngine;
using System;
using System.IO;
using System.Collections;

public static class GBAuditLocomotionQuality {
    sealed class Sample {
        public float gap,knee,clearance,asymmetry,supportGap,minimumChest=1;
        public bool finite=true;
        public void Read(GBWrestler w){
            gap=Mathf.Max(gap,w.JointSeparation());
            minimumChest=Mathf.Min(minimumChest,Vector3.Dot(w.bodies[1].rotation*Vector3.up,Vector3.up));
            clearance=Mathf.Max(clearance,Mathf.Abs(w.bodies[11].position.y-w.bodies[14].position.y));
            supportGap=Mathf.Max(supportGap,Mathf.Min(w.bodies[11].GetComponent<Collider>().bounds.min.y,w.bodies[14].GetComponent<Collider>().bounds.min.y)-.20f);
            float left=0,right=0;
            for(int f=0;f<2;f++){
                int thigh=f==0?9:12;
                float bend=Mathf.Abs(Mathf.DeltaAngle(0,(Quaternion.Inverse(w.bodies[thigh].rotation)*w.bodies[thigh+1].rotation).eulerAngles.x));
                knee=Mathf.Max(knee,bend);if(f==0)left=bend;else right=bend;
            }
            asymmetry=Mathf.Max(asymmetry,Mathf.Abs(left-right));
            foreach(var rb in w.bodies)finite&=!float.IsNaN(rb.position.x)&&!float.IsInfinity(rb.position.x)&&rb.linearVelocity.magnitude<=18.01f;
        }
    }
    static GBInput Forward(float amount=0){return new GBInput{az=1,z=amount};}
    static void CaptureMotion(GBApp app,string directory,string phase,bool enabled,ref float next,ref int frame){
        if(!enabled||Application.isBatchMode||Time.time<next)return;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,phase+"-motion-"+(frame++).ToString("000")+".png"));next=Time.time+.10f;
    }
    static float nextWalkLog;
    static void LogWalk(GBWrestler w,string phase){
        if(Time.time<nextWalkLog)return;nextWalkLog=Time.time+.10f;
        Debug.Log("WALK_PHASE phase="+phase+" time="+Time.time.ToString("0.00")+" root="+w.Root.position+" counts="+w.LeftFootPlacements+"/"+w.RightFootPlacements+" "+w.WalkPhaseEvidence);
    }
    static IEnumerator Settle(GBApp app,GBWrestler a,GBWrestler b){
        a.ResetTo(new Vector3(-1.6f,.28f,-2));b.ResetTo(new Vector3(2.2f,.28f,1.5f));
        app.ScriptedInputs[0]=Forward();app.ScriptedInputs[1]=Forward();
        yield return new WaitForSeconds(.8f);
    }
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];var sample=new Sample();a.WalkPhaseDiagnostics=true;
        bool capture=Array.IndexOf(Environment.GetCommandLineArgs(),"--capture-locomotion")>=0;
        float nextCapture=0;int captureFrame=0;
        yield return Settle(app,a,b);
        Vector3 start=a.Root.position;app.ScriptedInputs[0]=Forward(.35f);
        float until=Time.time+2.5f;
        while(Time.time<until){yield return new WaitForFixedUpdate();sample.Read(a);LogWalk(a,"slow");CaptureMotion(app,directory,"slow-stride",capture,ref nextCapture,ref captureFrame);}
        check("deliberate_slow_stride",a.LargestFootPlacement>.36f&&a.LeftFootPlacements>1&&a.RightFootPlacements>1&&Vector3.Distance(a.Root.position,start)>.5f,"largest placement="+a.LargestFootPlacement.ToString("0.000")+" m, L/R="+a.LeftFootPlacements+"/"+a.RightFootPlacements+", travel="+Vector3.Distance(a.Root.position,start).ToString("0.00"));
        if(!Application.isBatchMode){ScreenCapture.CaptureScreenshot(Path.Combine(directory,"locomotion-slow.png"));yield return null;}
        yield return Settle(app,a,b);
        start=a.Root.position;float initialStamina=a.stamina;app.ScriptedInputs[0]=Forward(1);
        until=Time.time+1.15f;
        nextCapture=0;captureFrame=0;
        while(Time.time<until){yield return new WaitForFixedUpdate();sample.Read(a);LogWalk(a,"normal");CaptureMotion(app,directory,"normal-stride",capture,ref nextCapture,ref captureFrame);}
        float normalTravel=Vector3.Distance(a.Root.position,start);
        check("useful_normal_stride",a.LargestFootPlacement>.42f&&a.LeftFootPlacements>1&&a.RightFootPlacements>1&&Mathf.Abs(a.LeftFootPlacements-a.RightFootPlacements)<=1&&normalTravel>1&&normalTravel<4,"largest placement="+a.LargestFootPlacement.ToString("0.000")+" m, alternating L/R="+a.LeftFootPlacements+"/"+a.RightFootPlacements+", travel="+normalTravel.ToString("0.00"));
        check("physical_boot_clearance",sample.clearance>.04f,"maximum real boot height difference="+sample.clearance.ToString("0.000")+" m");
        check("coordinated_physical_knees",sample.knee>15,"maximum actual knee flexion="+sample.knee.ToString("0.0")+" degrees");
        check("walking_keeps_balance",sample.minimumChest>.65f&&!a.Down,"minimum chest upright dot="+sample.minimumChest.ToString("0.000")+", down="+a.Down);
        check("walking_keeps_mat_support",sample.supportGap<.025f,"maximum gap under nearest real supporting foot="+sample.supportGap.ToString("0.000")+" m, no paid jump in walking fixture");
        check("walking_stamina_cost_unchanged",Mathf.Abs(a.stamina-initialStamina)<.001f&&GBWrestler.MaxStamina==155.25f&&GBWrestler.ScrambleStaminaMultiplier==1.43f,"non-sprinting stamina="+initialStamina+" -> "+a.stamina+", maximum155.25, scramble1.43 unchanged");
        if(!Application.isBatchMode){ScreenCapture.CaptureScreenshot(Path.Combine(directory,"locomotion-normal.png"));yield return null;}
        for(int side=-1;side<=1;side+=2){
            yield return Settle(app,a,b);var lateral=new Sample();start=a.Root.position;
            app.ScriptedInputs[0]=new GBInput{az=1,x=side};until=Time.time+1.0f;nextCapture=0;captureFrame=0;
            while(Time.time<until){yield return new WaitForFixedUpdate();lateral.Read(a);LogWalk(a,side<0?"left":"right");CaptureMotion(app,directory,side<0?"side-left":"side-right",capture,ref nextCapture,ref captureFrame);}
            check(side<0?"side_left_physical_steps":"side_right_physical_steps",Mathf.Abs(a.Root.position.x-start.x)>1&&a.LeftFootPlacements>1&&a.RightFootPlacements>1&&Mathf.Abs(a.LeftFootPlacements-a.RightFootPlacements)<=1&&lateral.minimumChest>.65f,"actual lateral travel="+Mathf.Abs(a.Root.position.x-start.x).ToString("0.000")+" m; alternating L/R="+a.LeftFootPlacements+"/"+a.RightFootPlacements);
            check(side<0?"side_left_keeps_mat_support":"side_right_keeps_mat_support",lateral.supportGap<.025f,"nearest real supporting boot maximum gap="+lateral.supportGap.ToString("0.000")+" m; no jump");
        }
        yield return Settle(app,a,b);
        var shotView=app.gameObject.AddComponent<GBAuditShotCamera>();shotView.Wrestler=a;shotView.View=app.Views.Primary;shotView.Capturing=capture&&!Application.isBatchMode;
        float beforeHeight=a.Root.position.y,beforeShot=a.stamina;app.ScriptedInputs[0]=Forward();a.Shot();
        int lead=Vector3.Dot(a.bodies[11].position-a.bodies[14].position,a.ShotDirection)>=0?0:1,leadThigh=lead==0?9:12,trailBoot=lead==0?14:11;
        float kneeClearance=float.PositiveInfinity,headUp=1,earlyTrail=float.PositiveInfinity,lateTrail=float.NegativeInfinity,shotStart=Time.time;
        check("shot_cost_unchanged",Mathf.Abs(beforeShot-a.stamina-12)<.001f&&a.Shooting,"one Q costs12, observed="+(beforeShot-a.stamina).ToString("0.000"));
        float minimumHeight=beforeHeight;var shot=new Sample();bool hadGrip=false,entryCaptured=false,trailCaptured=false;int ticks=0;nextCapture=0;captureFrame=0;
        while(a.Shooting){
            yield return new WaitForFixedUpdate();shot.Read(a);sample.Read(a);minimumHeight=Mathf.Min(minimumHeight,a.Root.position.y);hadGrip|=a.GripCount!=0;
            Vector3 actualKnee=a.bodies[leadThigh].position+a.bodies[leadThigh].rotation*a.joints[leadThigh+1].connectedAnchor;
            kneeClearance=Mathf.Min(kneeClearance,actualKnee.y-.2f);headUp=Mathf.Min(headUp,Vector3.Dot(a.bodies[2].rotation*Vector3.up,Vector3.up));
            float trail=Vector3.Dot(a.bodies[trailBoot].position-a.Root.position,a.ShotDirection),elapsed=Time.time-shotStart;
            if(elapsed>.10f&&elapsed<.34f)earlyTrail=Mathf.Min(earlyTrail,trail);if(elapsed>.34f)lateTrail=Mathf.Max(lateTrail,trail);
            CaptureMotion(app,directory,"shot",capture,ref nextCapture,ref captureFrame);
            if(ticks%24==0){
                int trailThigh=lead==0?12:9;
                Debug.Log("SHOT_PHASE elapsed="+elapsed.ToString("0.00")+" root="+a.Root.position+" rootRotation="+a.Root.rotation.eulerAngles+" actual/target thigh="+a.bodies[trailThigh].rotation.eulerAngles+"/"+a.poses[trailThigh].eulerAngles+" actual/target shin="+a.bodies[trailThigh+1].rotation.eulerAngles+"/"+a.poses[trailThigh+1].eulerAngles+" boot="+a.bodies[trailBoot].position);
            }
            ticks++;
            if(!Application.isBatchMode&&!entryCaptured&&elapsed>=.18f){entryCaptured=true;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"shot-level-entry.png"));}
            if(!Application.isBatchMode&&!trailCaptured&&elapsed>=.60f){trailCaptured=true;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"shot-trail-drive.png"));}
        }
        shotView.Capturing=false;UnityEngine.Object.Destroy(shotView);
        check("physical_shot_level_change_and_lead_leg",beforeHeight-minimumHeight>.16f&&shot.asymmetry>8&&shot.knee>25,"real hip lowering="+(beforeHeight-minimumHeight).ToString("0.000")+" m, left/right knee difference="+shot.asymmetry.ToString("0.0")+" degrees, knee flexion="+shot.knee.ToString("0.0"));
        check("shot_head_up_lead_knee_near_mat",headUp>.80f&&kneeClearance<.25f,"minimum actual head upright="+headUp.ToString("0.000")+", lead knee above mat="+kneeClearance.ToString("0.000")+" m");
        check("shot_trail_foot_steps_up",lateTrail>earlyTrail+.10f,"early/late actual trailing boot relative to hips="+earlyTrail.ToString("0.000")+"/"+lateTrail.ToString("0.000")+" m");
        check("shot_grabs_stay_manual",!hadGrip&&a.GripCount==0,"shot never attached a hand or auto grabbed");
        app.ScriptedInputs[0]=Forward();until=Time.time+1;
        while(Time.time<until){yield return new WaitForFixedUpdate();sample.Read(a);}
        check("shot_returns_to_ready_stance",!a.Shooting&&!a.Down&&Vector3.Dot(a.bodies[1].rotation*Vector3.up,Vector3.up)>.65f,"shooting="+a.Shooting+", down="+a.Down+", chest upright="+Vector3.Dot(a.bodies[1].rotation*Vector3.up,Vector3.up).ToString("0.000"));
        check("locomotion_connected_finite",sample.finite&&sample.gap<.06f&&app.Registry.Count==92,"maximum anchor separation="+sample.gap.ToString("0.00000")+" m, finite="+sample.finite+", unchanged registry="+app.Registry.Count);
        a.WalkPhaseDiagnostics=false;app.ScriptedInputs[0]=Forward();app.ScriptedInputs[1]=Forward();
    }
}
