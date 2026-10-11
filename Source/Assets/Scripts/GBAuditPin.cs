using UnityEngine;
using System;
using System.IO;
using System.Collections;

// Prepared poses isolate rule geometry. Covers, escapes and the standing
// exchange run the normal GBInput path: no Attach, forced count, or attack call.
public static class GBAuditPin {
    public static IEnumerator ScramblePatch(GBApp app,Action<string,bool,string> check){
        Start(app);yield return null;app.enabled=false;var oldMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));
        a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;
        var request=new GBInput{az=-1,x=1,flags=GBInput.Brace};
        for(int n=0;n<Mathf.CeilToInt(1.2f/Time.fixedDeltaTime);n++)b.Simulate(request);
        check("sustained_scramble_never_script_releases_hold",a.GripCount==1&&!a.hands[0].releaseRequired,"1.2s of production input steps; real grip retained without displacement/overload");
        check("scramble_has_no_timed_escape_immunity",b.EscapeUntil==0,"no automatic release, immunity or charged velocity kick");
        a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;
        int accepted=0;
        for(int n=0;n<10;n++){
            b.Simulate(request);if(b.command.Has(GBInput.Brace))accepted++;
            yield return new WaitForSeconds(.02f);b.Simulate(new GBInput{az=-1,x=1});yield return new WaitForSeconds(.02f);
        }
        check("jitter_clicks_cannot_repeat_scramble",accepted==1&&b.ScrambleCooldown>.3f,"accepted="+accepted+"/10 rapid presses; release recovery="+b.ScrambleCooldown);
        check("jitter_clicks_do_not_break_grip",a.GripCount==1&&!a.hands[0].releaseRequired,"prepared real grip survives repeated input edges");
        b.Simulate(request);check("holding_early_press_waits_for_recovery",!b.command.Has(GBInput.Brace),"early press is gated");
        yield return new WaitForSeconds(.45f);b.Simulate(request);check("held_scramble_resumes_after_recovery",b.command.Has(GBInput.Brace)&&b.ScrambleReady,"held key resumes after0.4s; no extra burst");
        b.stamina=0;b.Simulate(request);check("exhaustion_still_requires_1_2_seconds",b.ScrambleCooldown>1.19f&&!b.ScrambleReady,"remaining="+b.ScrambleCooldown);
        b.ResetTo(new Vector3(.5f,.28f,0));check("reset_clears_both_scramble_timers",b.ScrambleCooldown==0&&b.ScrambleReady,"new round restores control");
        a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));
        var target=b.bodies[1].GetComponent<GBTarget>();Vector3 contact=target.GetComponent<Collider>().ClosestPoint(a.bodies[5].position);a.bodies[5].position=contact;Physics.SyncTransforms();a.Attach(0,target,contact);var grip=a.hands[0].joint;grip.enabled=false;
        for(int n=0;n<48;n++)grip.SendMessage("FixedUpdate");check("unstrained_physical_hold_still_survives",a.GripCount==1,"production grip spring/strain evaluated with coincident contact endpoints");
        b.bodies[1].position+=Vector3.right*.8f;Physics.SyncTransforms();for(int n=0;n<48&&a.GripCount>0;n++)grip.SendMessage("FixedUpdate");
        check("real_physical_overload_can_still_break_hold",a.GripCount==0&&a.hands[0].releaseRequired,"real0.8m anchor separation breaks through strain, not a scramble key event");
        a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));Physics.simulationMode=oldMode;app.enabled=true;app.ReturnToMenu();check("scramble_patch_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
    public static IEnumerator StaminaPatch(GBApp app,Action<string,bool,string> check){
        Start(app);yield return null;app.Paused=true;app.enabled=false;
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        foreach(int hands in new[]{1,2})foreach(bool lift in new[]{false,true}){
            float neutral=0,resisting=0;
            foreach(bool brace in new[]{false,true}){
                a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));
                for(int h=0;h<hands;h++){a.Attach(h,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[h].joint.enabled=false;}
                b.command=new GBInput{az=-1,flags=brace?GBInput.Brace:0};
                int flags=GBInput.Left|(hands==2?GBInput.Right:0)|(lift?GBInput.Lift:0);float before=a.stamina;
                for(int n=0;n<20;n++)a.Simulate(new GBInput{az=1,flags=flags});
                float rate=(before-a.stamina)/(20*Time.fixedDeltaTime);if(brace)resisting=rate;else neutral=rate;
            }
            float expected=hands*2+(lift?app.Tuning.liftCost:0);
            check("opponent_brace_does_not_add_cost_"+hands+"_hands_"+(lift?"lift":"hold"),Mathf.Abs(neutral-resisting)<.02f&&Mathf.Abs(resisting-expected)<.03f,"normal="+neutral+" resisting="+resisting+" expected="+expected+" stamina/s; production Simulate and actual manual joints");
        }
        b.stamina=80;float ownBefore=b.stamina;b.Simulate(new GBInput{az=-1,flags=GBInput.Brace});
        check("brace_spends_only_bracer_stamina",Mathf.Abs((ownBefore-b.stamina)/Time.fixedDeltaTime-7)<.03f,"own brace cost="+(ownBefore-b.stamina)/Time.fixedDeltaTime);
        a.Release(0);a.Release(1);a.stamina=80;float recovery=a.stamina;a.Simulate(new GBInput{az=1});
        check("released_grips_allow_recovery",a.stamina>recovery&&Mathf.Abs((a.stamina-recovery)/Time.fixedDeltaTime-app.Tuning.staminaRegen)<.03f,"recovery="+(a.stamina-recovery)/Time.fixedDeltaTime+"/s");
        app.enabled=true;app.Paused=false;app.ReturnToMenu();check("stamina_patch_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
    // Prepared native fixtures isolate rules and applied forces. They do not
    // claim an autonomous player can complete every physical pin exchange.
    public static IEnumerator RulesPatch(GBApp app,string directory,Action<string,bool,string> check){
        LyingSetup(app,false,3.9f);yield return null;app.Paused=true;app.enabled=false;
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        Pose(b,new Vector3(3.9f,MatHeight(Vector3.zero)+.26f,0),Quaternion.Euler(-90,0,0));
        a.command=new GBInput{az=1,flags=GBInput.Left|GBInput.Pin};
        a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);
        a.hands[0].joint.enabled=false;a.SendMessage("PinRefresh");
        foreach(var w in app.Wrestlers)foreach(var body in w.bodies)body.isKinematic=true;
        bool l,r;check("rope_fixture_has_both_actual_shoulders",b.ShoulderContact(out l,out r)&&app.Arena.TouchesRopes(b)&&a.IsCovering,b.CoverGeometry+" shoulders="+l+"/"+r);
        for(int n=0;n<20;n++)app.SendMessage("CheckPin");
        check("rope_pin_count_advances",app.PinCount>.07f&&!app.PinStatus.Contains("ROPE"),"native rule count="+app.PinCount);
        a.command.flags&=~GBInput.Pin;app.SendMessage("CheckPin");check("releasing_C_still_resets_pin",app.PinCount==0,"count="+app.PinCount);
        a.command.flags|=GBInput.Pin;a.SendMessage("PinRefresh");app.SendMessage("CheckPin");
        b.bodies[1].position+=Vector3.up*.3f;Physics.SyncTransforms();app.SendMessage("CheckPin");check("lifting_shoulders_still_resets_pin",app.PinCount==0,"actual shoulder contacts lost");
        b.bodies[1].position-=Vector3.up*.3f;Physics.SyncTransforms();a.SendMessage("PinRefresh");app.SendMessage("CheckPin");a.Release(0);app.SendMessage("CheckPin");check("losing_grip_still_resets_pin",app.PinCount==0,"live hold removed");
        a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;a.SendMessage("PinRefresh");
        int ticks=Mathf.CeilToInt(GBApp.PinSeconds/Time.fixedDeltaTime);
        for(int n=0;n<ticks-1;n++)app.SendMessage("CheckPin");
        check("pin_does_not_win_early",app.Scores[0]==0&&app.PinCount>2.4f,"count="+app.PinCount);
        if(!Application.isBatchMode){Time.timeScale=0;app.Paused=false;app.enabled=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"rope-pin-2_5-seconds-hud.png"));yield return null;app.enabled=false;app.Paused=true;Time.timeScale=1;}
        app.SendMessage("CheckPin");if(app.Scores[0]==0)app.SendMessage("CheckPin");
        check("rope_pin_wins_at_2_5_seconds",app.Scores[0]==1,"score="+app.Scores[0]+" count="+app.PinCount);
        Start(app);yield return null;app.Paused=true;app.enabled=false;a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-.5f,.28f,0));b.ResetTo(new Vector3(.5f,.28f,0));
        typeof(GBWrestler).GetField("liftCharge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,1f);a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;
        float before=b.Root.GetAccumulatedForce().y;a.Lift();float one=b.Root.GetAccumulatedForce().y-before;
        a.Attach(1,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[1].joint.enabled=false;
        before=b.Root.GetAccumulatedForce().y;a.Lift();float two=b.Root.GetAccumulatedForce().y-before;
        check("one_hand_lift_weaker_than_two",one>0&&two>one*2,"actual accumulated native forces: one="+one+" two="+two);
        Pose(a,new Vector3(-.5f,MatHeight(Vector3.zero)+.26f,0),Quaternion.Euler(-90,0,0));a.downUntil=0;typeof(GBWrestler).GetField("liftCharge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,1f);
        a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;
        before=b.Root.GetAccumulatedForce().y;a.Lift();float floorOne=b.Root.GetAccumulatedForce().y-before;
        check("ground_lift_weaker",floorOne>0&&floorOne<one*.7f,"ground one="+floorOne+" standing one="+one);
        b.command=new GBInput{flags=GBInput.Pin|GBInput.Left};b.Attach(0,a.bodies[1].GetComponent<GBTarget>(),a.bodies[1].position);b.hands[0].joint.enabled=false;b.SendMessage("PinRefresh");
        before=b.Root.GetAccumulatedForce().y;a.Lift();float pinnedOne=b.Root.GetAccumulatedForce().y-before;
        check("pinned_one_hand_lift_is_weakest",a.IsBeingPinned&&pinnedOne>0&&pinnedOne<floorOne*.6f,"pinned="+a.IsBeingPinned+" force="+pinnedOne+" ground="+floorOne);
        app.enabled=true;Start(app);yield return null;a=app.Wrestlers[0];b=app.Wrestlers[1];b.ResetTo(new Vector3(3,.28f,0));
        Pose(a,new Vector3(-1,MatHeight(Vector3.zero)+.26f,0),Quaternion.Euler(-90,0,0));
        app.ScriptedInputs[0]=new GBInput{az=1,x=1,flags=GBInput.Brace};yield return new WaitForFixedUpdate();float t=Time.time,energy=a.stamina;
        yield return new WaitForSeconds(.3f);float rate=(energy-a.stamina)/(Time.time-t);
        check("scramble_cost_additional_ten_percent",Mathf.Abs(rate-18.59f)<.8f,"actual ground drain="+rate+"/s (previous16.9/s)");
        app.Paused=true;app.enabled=false;a.stamina=0;var request=new GBInput{az=1,x=1,flags=GBInput.Brace};a.Simulate(request);
        float cooldown=a.ScrambleCooldown;check("exhaustion_starts_1_2_second_timer",cooldown>1.19f&&cooldown<=1.201f&&!a.command.Has(GBInput.Brace),"remaining="+cooldown);
        a.stamina=20;yield return new WaitForSeconds(.6f);a.Simulate(request);
        check("funded_scramble_still_blocked_during_timer",!a.command.Has(GBInput.Brace)&&a.ScrambleCooldown>.5f,"remaining="+a.ScrambleCooldown+" stamina="+a.stamina);
        yield return new WaitForSeconds(.65f);a.Simulate(request);check("scramble_resumes_after_timer",a.command.Has(GBInput.Brace)&&a.ScrambleReady,"remaining="+a.ScrambleCooldown+" stamina="+a.stamina);
        a.ResetTo(new Vector3(-1,.28f,0));check("round_reset_clears_scramble_cooldown",a.ScrambleCooldown==0&&a.ScrambleReady,"reset restores full stamina and timer");
        app.enabled=true;app.Paused=false;app.ReturnToMenu();check("rules_patch_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
    sealed class Integrity {
        public bool finite=true;public float gap,penetration;public int missing;
        public void Measure(GBApp app){foreach(var w in app.Wrestlers){
            float g=w.JointSeparation();if(float.IsInfinity(g))missing++;else gap=Mathf.Max(gap,g);
            foreach(var b in w.bodies)finite&=b&&Valid(b.position)&&Valid(b.linearVelocity)&&Valid(b.angularVelocity);
            foreach(int n in new[]{4,5,7,8})for(int j=0;j<15;j++){
                if(n==j)continue;foreach(var a in w.bodies[n].GetComponents<Collider>())foreach(var b in w.bodies[j].GetComponents<Collider>()){
                if(!a.enabled||!b.enabled)continue;
                if(Physics.GetIgnoreCollision(a,b)||!a.bounds.Intersects(b.bounds))continue;
                Vector3 direction;float depth;if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out direction,out depth))penetration=Mathf.Max(penetration,depth);
                }
            }
        }}
        static bool Valid(Vector3 v){return !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));}
    }
    sealed class Outcome {public int grips;public float count,coverSeconds,defenderTravel,torsoRotation;public bool covered,shoulders,won,escapedUnderControl,ropeInterrupted,countWithoutChestCover,pressureLeft,pressureRight;public float maxLoad;public string geometry="";}
    sealed class ShotResult {public float approach,peak,travel,balanceLoss;public int hands;public bool shooting;}
    static void Start(GBApp app){app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);Neutral(app);}
    static void Neutral(GBApp app){app.ScriptedInputs[0]=new GBInput{az=1};app.ScriptedInputs[1]=new GBInput{az=-1};}
    static GBInput Input(GBWrestler w,Vector3 aim,Vector3 target,int flags=0,Vector3 move=default){
        Vector3 offset=target-w.Root.position;return new GBInput{x=move.x,z=move.z,ax=aim.x,az=aim.z,hx=offset.x,hy=offset.y,hz=offset.z,flags=flags|GBInput.HandAim}.Sanitize();
    }
    static int Grips(GBWrestler a,GBWrestler b){int count=0;foreach(var h in a.hands)if(h.joint&&h.target&&h.target.wrestler==b)count++;return count;}
    static Vector3 Velocity(GBWrestler w){Vector3 v=Vector3.zero;float mass=0;foreach(var b in w.bodies){v+=b.linearVelocity*b.mass;mass+=b.mass;}return v/Mathf.Max(1,mass);}
    static Vector3 HandMean(GBWrestler w){return (w.bodies[5].position+w.bodies[8].position)*.5f-w.Root.position;}
    static Vector3 Contact(GBWrestler a,GBWrestler b,int region){
        Vector3 probe=a.bodies[1].position;
        // A high torso probe chooses the top of a thigh at the pelvis overlap.
        // A cursor pointed at the middle of a leg must actually use that height.
        if(region>=9)probe.y=b.bodies[region].position.y;
        return b.bodies[region].GetComponent<Collider>().ClosestPoint(probe);
    }
    static Vector3 CoverContact(GBWrestler a,GBWrestler b){
        // Aim at the exposed upper torso from the attacker's shoulder height,
        // as the elevated player camera can. A low side hold under folded
        // elbows is a different position and need not produce a cover.
        return b.bodies[1].GetComponent<Collider>().ClosestPoint(a.bodies[1].position);
    }
    static IEnumerator Wait(GBApp app,Integrity integrity,float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end){integrity.Measure(app);yield return null;}}
    static void Pose(GBWrestler w,Vector3 root,Quaternion rotation){
        w.ResetTo(root-w.initial[0]);foreach(var b in w.bodies){int n=Array.IndexOf(w.bodies,b);Vector3 position=root+rotation*(w.initial[n]-w.initial[0]);b.transform.SetPositionAndRotation(position,rotation);b.position=position;b.rotation=rotation;b.linearVelocity=b.angularVelocity=Vector3.zero;}w.downUntil=Time.time+8;Physics.SyncTransforms();
    }
    static float MatHeight(Vector3 point){
        // World creation destroys decorative primitive colliders at end-frame.
        // Their temporary hits are not a valid definition of the authored mat.
        foreach(var collider in GBApp.I.Arena.GetComponentsInChildren<Collider>())if(collider.enabled&&collider.name=="ring mat")return collider.bounds.max.y;
        throw new InvalidOperationException("Pin fixture requires the authored ring mat collider");
    }
    static void LyingSetup(GBApp app,bool faceDown=false,float x=0){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(x-.62f,.28f,0));b.ResetTo(new Vector3(x+.4f,.28f,0));
        Pose(b,new Vector3(x,MatHeight(new Vector3(x,0,0))+.26f,0),Quaternion.Euler(faceDown?90:-90,0,0));
    }
    static IEnumerator Capture(GBApp app,string directory,string name,bool hud=false){
        app.Capture(Path.Combine(directory,name+".png"));
        if(hud){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+"-hud.png"));yield return null;}
    }
    public static IEnumerator GuardChecks(GBApp app,string directory,Action<string,bool,string> check){
        var integrity=new Integrity();Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        yield return Wait(app,integrity,.6f);float y=a.Root.position.y;bool blocked=true;
        for(int n=0;n<5;n++){a.stamina=0;app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Jump};yield return Wait(app,integrity,.05f);blocked&=a.Root.linearVelocity.y<1&&a.Root.position.y<y+.08f;app.ScriptedInputs[0]=new GBInput{az=1};yield return Wait(app,integrity,.05f);}
        check("exhausted_jump_spam_blocked",blocked,"five exhausted jump presses produced no jump impulse");a.stamina=GBWrestler.MaxStamina;app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Jump};yield return Wait(app,integrity,.05f);check("funded_jump_still_works",a.Root.linearVelocity.y>1&&a.stamina<GBWrestler.MaxStamina-5,"normal jump still spends 8 stamina and rises");
        Start(app);a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-.55f,.28f,0));b.ResetTo(new Vector3(.55f,.28f,0));yield return Wait(app,integrity,.6f);
        foreach(var body in b.bodies)body.isKinematic=true;b.balance=10;b.downUntil=Time.time+10;
        float end=Time.realtimeSinceStartup+2.5f;while(Time.realtimeSinceStartup<end&&Grips(a,b)==0){app.ScriptedInputs[0]=Input(a,Vector3.right,Contact(a,b,1),GBInput.Left|GBInput.Right);yield return null;}
        int grips=Grips(a,b);app.ScriptedInputs[0]=Input(a,Vector3.right,Contact(a,b,1),GBInput.Left|GBInput.Right|GBInput.Pin);yield return Wait(app,integrity,.15f);
        check("upright_rival_rejects_pin_attempt",grips>0&&!b.PhysicallyDownForPin&&a.PinTarget==null&&!b.PinControlled&&app.PinCount==0,"actual manual grip="+grips+"; standing target rejected despite low balance and stale Down timer");
        var result=new Outcome();yield return Cover(app,integrity,directory,true,false,false,result);check("grounded_pin_still_counts",result.won&&result.grips>0,"normal manual grounded pin count="+result.count+" won="+result.won);
    }
    static IEnumerator KneeLimits(GBApp app,Integrity integrity,Action<string,bool,string> check){
        var parentObject=new GameObject("native knee parent fixture");parentObject.transform.position=new Vector3(-50,5,0);
        var parent=parentObject.AddComponent<Rigidbody>();parent.isKinematic=true;
        var childObject=new GameObject("native knee child fixture");childObject.transform.position=parent.position;
        var child=childObject.AddComponent<Rigidbody>();child.useGravity=false;child.mass=2;child.solverIterations=32;
        var joint=childObject.AddComponent<ConfigurableJoint>();joint.connectedBody=parent;joint.autoConfigureConnectedAnchor=false;
        joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
        joint.angularXMotion=ConfigurableJointMotion.Limited;joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Locked;
        joint.rotationDriveMode=RotationDriveMode.XYAndZ;joint.angularXDrive=new JointDrive{positionSpring=1000,positionDamper=50,maximumForce=200};
        joint.lowAngularXLimit=new SoftJointLimit{limit=-10};joint.highAngularXLimit=new SoftJointLimit{limit=145};
        joint.targetRotation=Quaternion.Inverse(Quaternion.Euler(70,0,0));yield return Wait(app,integrity,.8f);
        float oldBend=Mathf.DeltaAngle(0,child.rotation.eulerAngles.x);
        joint.lowAngularXLimit=new SoftJointLimit{limit=-145};joint.highAngularXLimit=new SoftJointLimit{limit=10};
        child.rotation=Quaternion.identity;child.angularVelocity=Vector3.zero;child.WakeUp();yield return Wait(app,integrity,.8f);
        float flexion=Mathf.DeltaAngle(0,child.rotation.eulerAngles.x);
        joint.targetRotation=Quaternion.Inverse(Quaternion.Euler(-40,0,0));yield return Wait(app,integrity,.8f);
        float extension=Mathf.DeltaAngle(0,child.rotation.eulerAngles.x);
        check("native_knee_limit_direction",oldBend<18&&flexion>45&&flexion<90&&extension>=-16&&extension<5,"isolated real X-joint: old range positive bend="+oldBend.ToString("0.0")+" corrected flexion="+flexion.ToString("0.0")+" bounded extension="+extension.ToString("0.0"));
        foreach(var w in app.Wrestlers)foreach(int n in new[]{10,13})check("knee_range_"+w.player+"_"+n,w.joints[n].lowAngularXLimit.limit==-145&&w.joints[n].highAngularXLimit.limit==10,"production knee uses verified joint-frame range");
        UnityEngine.Object.Destroy(childObject);UnityEngine.Object.Destroy(parentObject);yield return null;
    }

    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.LocalDevice=0;app.Automated=true;var integrity=new Integrity();
        float loadL=200,loadR=200;GBWrestler.ShiftPinPressure(ref loadL,ref loadR,0);
        check("neutral_pin_pressure_preserved",loadL==200&&loadR==200,"neutral movement preserves existing shoulder loads");
        GBWrestler.ShiftPinPressure(ref loadL,ref loadR,1);
        check("right_pin_pressure_redistributes_existing_weight",Mathf.Abs(loadL-80)<.01f&&Mathf.Abs(loadR-320)<.01f,"400N total remains400N; right lean shifts120N, left preload retained");
        loadL=200;loadR=200;GBWrestler.ShiftPinPressure(ref loadL,ref loadR,-1);
        check("left_pin_pressure_symmetric",Mathf.Abs(loadL-320)<.01f&&Mathf.Abs(loadR-80)<.01f,"left lean mirrors right with conserved total pressure");
        Start(app);var scrambler=app.Wrestlers[0];app.Wrestlers[1].ResetTo(new Vector3(3,.28f,0));
        Pose(scrambler,new Vector3(-1,MatHeight(Vector3.zero)+.26f,0),Quaternion.Euler(-90,0,0));
        app.ScriptedInputs[0]=new GBInput{az=1,x=1,flags=GBInput.Brace};yield return new WaitForFixedUpdate();
        float scrambleStart=Time.time,scrambleStamina=scrambler.stamina;yield return Wait(app,integrity,.30f);
        float actualRate=(scrambleStamina-scrambler.stamina)/Mathf.Max(.001f,Time.time-scrambleStart);
        check("ground_scramble_cost_increased_additional_10_percent",Mathf.Abs(actualRate-18.59f)<.8f,"actual funded ground bridge drain="+actualRate.ToString("0.00")+"/s vs prior13/s; standing grip escape costs also scaled1.43");
        Start(app);check("only_pin_ring_mode",app.CurrentArena==3&&app.CurrentMode==1&&app.Wrestlers.Count==2,"ring arena="+app.CurrentArena+" pin mode="+app.CurrentMode+" bodies="+app.Wrestlers.Count+"; no training dummy");
        app.BeginMatch(0,0);check("legacy_mode_entry_still_pin_ring",app.CurrentArena==3&&app.CurrentMode==1,"legacy arena/mode entry resolves to current pin rules");app.Phase=GBApp.GamePhase.Fighting;
        yield return KneeLimits(app,integrity,check);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];check("stamina_capacity_increased_extra_15_percent",a.stamina==155.25f&&b.stamina==155.25f&&a.StaminaFraction==1,"both fresh wrestlers have155.25 stamina, unchanged costs, normalized full meter/strength");a.ResetTo(new Vector3(-1,.28f,0));b.ResetTo(new Vector3(2,.28f,0));Neutral(app);yield return Wait(app,integrity,.6f);
        int neighbors=0,others=0;for(int i=0;i<15;i++)for(int j=i+1;j<15;j++)if(Physics.GetIgnoreCollision(a.bodies[i].GetComponent<Collider>(),a.bodies[j].GetComponent<Collider>())){if(a.parents[i]==j||a.parents[j]==i)neighbors++;else others++;}
        check("anatomical_self_collisions",neighbors==14&&others==0,"ignored connected neighbors="+neighbors+" ignored nonadjacent pairs="+others);
        bool pads=a.shoulderPads.Length==4&&a.bodies[1].GetComponents<Collider>().Length==5;
        bool compoundPairs=true;int compoundIgnored=0;
        foreach(var pad in a.shoulderPads){pads&=pad&&pad.enabled&&!pad.isTrigger&&pad.attachedRigidbody==a.bodies[1]&&pad.gameObject==a.bodies[1].gameObject&&Mathf.Abs(pad.radius-.055f)<.0001f;
            for(int i=0;i<15;i++)if(i!=1){bool adjacent=a.parents[i]==1||a.parents[1]==i;foreach(var collider in a.bodies[i].GetComponents<Collider>()){bool ignored=Physics.GetIgnoreCollision(pad,collider);compoundPairs&=ignored==adjacent;if(ignored)compoundIgnored++;}}
        }
        check("physical_shoulders_share_connected_torso",pads,"four chest/back shoulder spheres share the existing 14 kg torso Rigidbody and target; no new bodies or joints");
        check("shoulder_compound_self_collision_pairs",compoundPairs&&compoundIgnored==16,"pad neighbor exclusions="+compoundIgnored+"; forearms/hands/opponent contacts remain enabled");
        app.ScriptedInputs[0]=Input(a,Vector3.forward,a.Root.position+new Vector3(0,-.55f,.7f));yield return Wait(app,integrity,.8f);Vector3 low=HandMean(a);
        app.ScriptedInputs[0]=Input(a,Vector3.forward,a.Root.position+new Vector3(0,.85f,.7f));yield return Wait(app,integrity,.8f);Vector3 high=HandMean(a);
        check("cursor_vertical_moves_real_hands",high.y-low.y>.2f,"mean hand root-relative y low/high="+low.y.ToString("0.000")+"/"+high.y.ToString("0.000")+" m, no click buttons");
        GBAuditArms.Free(app,check);yield return Capture(app,directory,"arm-high",true);
        app.ScriptedInputs[0]=Input(a,Vector3.forward,a.Root.position+new Vector3(-.75f,.25f,.5f));yield return Wait(app,integrity,.8f);Vector3 left=HandMean(a);
        app.ScriptedInputs[0]=Input(a,Vector3.forward,a.Root.position+new Vector3(.75f,.25f,.5f));yield return Wait(app,integrity,.8f);Vector3 right=HandMean(a);
        check("cursor_lateral_moves_real_hands",right.x-left.x>.2f,"mean hand x left/right="+left.x.ToString("0.000")+"/"+right.x.ToString("0.000")+" m");
        check("cursor_never_auto_grabs",a.GripCount==0,"aim-only hands="+a.GripCount);yield return Capture(app,directory,"arm-side");

        Start(app);a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-.5f,.28f,-1));b.ResetTo(new Vector3(.5f,.28f,-1));yield return Wait(app,integrity,.7f);
        float reachEnd=Time.realtimeSinceStartup+2.5f;while(Time.realtimeSinceStartup<reachEnd&&Grips(a,b)==0){app.ScriptedInputs[0]=Input(a,Vector3.right,Contact(a,b,1),GBInput.Left);integrity.Measure(app);yield return null;}
        check("left_button_independent_natural_grip",a.hands[0].joint&&a.hands[0].target&&a.hands[0].target.wrestler==b&&!a.hands[1].joint,"left="+a.hands[0].Label+" right="+a.hands[1].Label+"; no Attach calls");
        GBAuditArms.Held(app,a,0,check);
        Vector3 handStart=a.bodies[5].position,targetStart=b.Root.position;float baseBalance=b.balance;
        app.ScriptedInputs[0]=Input(a,Vector3.right,a.Root.position+new Vector3(-.2f,.4f,.8f),GBInput.Left,new Vector3(-.65f,0,.5f));yield return Wait(app,integrity,1);
        check("cursor_grip_physically_pulls",Vector3.Distance(a.bodies[5].position,handStart)>.15f&&Vector3.Distance(b.Root.position,targetStart)>.08f,"hand travel="+Vector3.Distance(a.bodies[5].position,handStart).ToString("0.000")+" victim travel="+Vector3.Distance(b.Root.position,targetStart).ToString("0.000")+" balance loss="+(baseBalance-b.balance).ToString("0.0"));
        yield return Capture(app,directory,"manual-pull",true);
        Start(app);a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-.35f,.28f,-1));b.ResetTo(new Vector3(.35f,.28f,-1));yield return Wait(app,integrity,.6f);
        bool leg=false;reachEnd=Time.realtimeSinceStartup+2.5f;while(Time.realtimeSinceStartup<reachEnd&&!leg){app.ScriptedInputs[0]=Input(a,Vector3.right,Contact(a,b,9),GBInput.Left|GBInput.Right);foreach(var h in a.hands)leg|=h.joint&&h.target&&h.target.wrestler==b&&(h.target.region.Contains("thigh")||h.target.region.Contains("shin")||h.target.region.Contains("foot"));integrity.Measure(app);yield return null;}
        Vector3 legCursor=a.Root.position+a.command.HandOffset,leftShoulder=a.bodies[1].transform.TransformPoint(a.joints[3].connectedAnchor),rightShoulder=a.bodies[1].transform.TransformPoint(a.joints[6].connectedAnchor);
        check("cursor_low_selects_leg_without_C",leg&&!a.command.Has(GBInput.Pin),"actual low-target hands="+a.hands[0].Label+" / "+a.hands[1].Label+" cursor="+legCursor+" thigh="+b.bodies[9].position+" hand L/R="+a.bodies[5].position+"/"+a.bodies[8].position+" shoulder-cursor L/R="+Vector3.Distance(leftShoulder,legCursor).ToString("0.000")+"/"+Vector3.Distance(rightShoulder,legCursor).ToString("0.000"));yield return Capture(app,directory,"arm-leg-grip");
        Neutral(app);yield return Wait(app,integrity,.2f);Vector3 staleStart=Velocity(b);app.ScriptedInputs[0]=new GBInput{ax=1,flags=32};yield return Wait(app,integrity,.15f);
        check("obsolete_throw_bit_ignored",(new GBInput{flags=32}.Sanitize().flags&32)==0&&!a.ActionFeedback.StartsWith("Thrown",StringComparison.OrdinalIgnoreCase),"reserved bit sanitizes away; no Throw action/binding; target velocity change diagnostic="+(Velocity(b)-staleStart).magnitude.ToString("0.000"));

        var stationary=new ShotResult();yield return Shot(app,integrity,false,false,stationary);
        var running=new ShotResult();yield return Shot(app,integrity,true,false,running);
        var braced=new ShotResult();yield return Shot(app,integrity,true,true,braced);
        check("Q_running_momentum_stronger",running.approach>stationary.approach+1&&running.peak>stationary.peak+.2f&&running.travel>stationary.travel+.08f,"approach stationary/run="+stationary.approach.ToString("0.00")+"/"+running.approach.ToString("0.00")+" target peak="+stationary.peak.ToString("0.00")+"/"+running.peak.ToString("0.00")+" travel="+stationary.travel.ToString("0.00")+"/"+running.travel.ToString("0.00"));
        check("Q_uses_no_automatic_hands",stationary.shooting&&running.shooting&&stationary.hands==0&&running.hands==0&&braced.hands==0,"shot maximum hands stationary/run/brace="+stationary.hands+"/"+running.hands+"/"+braced.hands);
        check("brace_resists_running_Q",braced.peak<running.peak*.9f||braced.travel<running.travel-.1f,"run/braced peak="+running.peak.ToString("0.00")+"/"+braced.peak.ToString("0.00")+" travel="+running.travel.ToString("0.00")+"/"+braced.travel.ToString("0.00"));

        Start(app);a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-.5f,.28f,-1));b.ResetTo(new Vector3(.5f,.28f,-1));yield return Wait(app,integrity,.6f);
        float spinStart=Time.realtimeSinceStartup,spinEnd=spinStart+.7f,yawPeak=0;
        while(Time.realtimeSinceStartup<spinEnd){float angle=(Time.realtimeSinceStartup-spinStart)/.7f*Mathf.PI*2;Vector3 turn=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));app.ScriptedInputs[0]=Input(a,turn,a.Root.position+turn*.7f+Vector3.up*.4f);yawPeak=Mathf.Max(yawPeak,Mathf.Abs(a.Root.angularVelocity.y));integrity.Measure(app);yield return null;}
        int spinHands=0;float transfer=0;reachEnd=Time.realtimeSinceStartup+2.5f;
        while(Time.realtimeSinceStartup<reachEnd&&spinHands==0){Vector3 toward=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up).normalized;app.ScriptedInputs[0]=Input(a,toward,Contact(a,b,1),GBInput.Left,Vector3.Distance(a.Root.position,b.Root.position)>.8f?toward*.35f:Vector3.zero);spinHands=Grips(a,b);transfer=Mathf.Max(transfer,a.LastGrabMomentumImpulse);integrity.Measure(app);yield return null;}
        check("spin_manual_aim_grab_physical",yawPeak>2&&spinHands==1&&transfer>.01f,"actual root yaw peak="+yawPeak.ToString("0.00")+" rad/s natural one-hand grip="+spinHands+" measured transfer="+transfer.ToString("0.00")+" Ns; no throw action "+GBAuditArms.ReachEvidence(a,0));yield return Capture(app,directory,"arm-spin-grab");

        LyingSetup(app,true);b=app.Wrestlers[1];bool sl,sr;bool both=b.ShoulderContact(out sl,out sr);
        check("face_down_actual_shoulders_valid",both&&sl&&sr,"face-down front shoulder contacts left/right="+sl+"/"+sr+" torso forward-up="+Vector3.Dot(b.bodies[1].transform.forward,Vector3.up).ToString("0.000"));
        Pose(b,new Vector3(0,MatHeight(Vector3.zero)+.29f,0),Quaternion.AngleAxis(35,Vector3.forward)*Quaternion.Euler(-90,0,0));both=b.ShoulderContact(out sl,out sr);
        check("one_shoulder_pose_invalid",!both&&(sl^sr),"actual back shoulder contacts left/right="+sl+"/"+sr);
        Pose(b,new Vector3(0,MatHeight(Vector3.zero)+.26f,0),Quaternion.Euler(-90,0,0));both=b.ShoulderContact(out sl,out sr);
        check("supine_fixture_actual_shoulders",both,"actual mat back contacts left/right="+sl+"/"+sr+" torso y="+b.bodies[1].position.y.ToString("0.000"));
        var noc=new Outcome();yield return Cover(app,integrity,directory,false,false,false,noc);
        check("no_C_no_count_with_manual_grip",noc.grips>0&&noc.count==0&&!noc.covered,"natural grips="+noc.grips+" peak count="+noc.count.ToString("0.00"));
        var nogrip=new Outcome();yield return Cover(app,integrity,directory,true,false,true,nogrip);
        check("no_manual_grip_no_count",nogrip.grips==0&&nogrip.count==0&&!nogrip.covered,"C alone grips="+nogrip.grips+" count="+nogrip.count.ToString("0.00"));
        LyingSetup(app,false,3.9f);b=app.Wrestlers[1];check("rope_body_contact_detected",app.Arena.TouchesRopes(b),"prepared connected supine body crosses actual ropes");
        var covered=new Outcome();yield return Cover(app,integrity,directory,true,false,false,covered);
        var steered=new Outcome();yield return Cover(app,integrity,directory,true,false,false,steered,steer:true);
        check("C_movement_steers_live_physical_pressure",steered.pressureLeft&&steered.pressureRight&&steered.maxLoad<=650.01f,"actual manual C grip, both direction signs observed="+steered.pressureLeft+"/"+steered.pressureRight+"; shared actual shoulder load peak="+steered.maxLoad.ToString("0.0")+" N");
        check("steered_pin_remains_achievable",steered.won,"same physical grip after directional pressure reaches normal three-count="+steered.count.ToString("0.00"));
        check("manual_C_physically_covers",covered.grips>0&&covered.covered&&covered.shoulders,"manual grips="+covered.grips+" actual cover="+covered.covered+" shoulders="+covered.shoulders+" cover duration="+covered.coverSeconds.ToString("0.00")+" "+covered.geometry);
        check("two_point_five_second_physical_pin_wins",covered.won&&covered.count>=GBApp.PinSeconds-.1f,"peak continuous count="+covered.count.ToString("0.00")+" round score="+app.Scores[0]+"; no count mutation");
        check("pin_counts_without_attackers_chest_cover",covered.countWithoutChestCover,"actual count advanced while attacker torso surfaces were separated; grounded shoulders and manual control define pin");
        var prone=new Outcome();yield return Cover(app,integrity,directory,true,false,false,prone,false,false,true);
        check("chest_down_physical_pin_wins",prone.grips>0&&prone.covered&&prone.shoulders&&prone.won&&prone.count>2.5f,"chest-down manual cover count="+prone.count.ToString("0.00")+" won="+prone.won+" "+prone.geometry);
        var escaped=new Outcome();yield return Cover(app,integrity,directory,true,true,false,escaped);
        check("bridge_roll_breaks_real_pin",escaped.covered&&escaped.count>.1f&&escaped.escapedUnderControl&&!escaped.won&&app.PinCount<.1f&&escaped.defenderTravel>.15f,"started cover="+escaped.covered+" peak count="+escaped.count.ToString("0.00")+" physical shoulder/cover break while standing recovery disabled="+escaped.escapedUnderControl+" remaining="+app.PinCount.ToString("0.00")+" escape travel="+escaped.defenderTravel.ToString("0.00")+" torso rotation="+escaped.torsoRotation.ToString("0.0")+" "+escaped.geometry);
        var rope=new Outcome();yield return Cover(app,integrity,directory,true,false,false,rope,true);
        check("rope_allows_actual_live_count",rope.count>.1f&&!rope.ropeInterrupted,"live count before rope="+rope.count.ToString("0.00")+" actual body rope contact resets="+rope.ropeInterrupted+" score="+app.Scores[0]);
        var aiEscape=new Outcome();yield return Cover(app,integrity,directory,true,true,false,aiEscape,false,true);
        check("AI_bridge_roll_escapes_live_pin",aiEscape.covered&&aiEscape.count>.1f&&aiEscape.escapedUnderControl&&!aiEscape.won,"AI defender physical break with standing recovery disabled="+aiEscape.escapedUnderControl+" live count peak="+aiEscape.count.ToString("0.00")+" travel="+aiEscape.defenderTravel.ToString("0.00")+" "+aiEscape.geometry);

        LyingSetup(app);a=app.Wrestlers[0];b=app.Wrestlers[1];var pinBot=new GBBot();app.AIDifficulty=1;float aiEnd=Time.realtimeSinceStartup+8;int aiGrips=0;bool aiCover=false;float aiCount=0;
        while(Time.realtimeSinceStartup<aiEnd&&app.Phase==GBApp.GamePhase.Fighting){app.ScriptedInputs[0]=pinBot.Tick(app,a,b);aiGrips=Mathf.Max(aiGrips,Grips(a,b));aiCover|=a.IsCovering;aiCount=Mathf.Max(aiCount,app.PinCount);integrity.Measure(app);yield return null;}
        check("AI_naturally_grips_and_covers_downed_rival",aiGrips>0&&aiCover&&aiCount>.1f,"AI manual reach hands="+aiGrips+" actual cover="+aiCover+" count="+aiCount.ToString("0.00")+" intent="+pinBot.Intent+" "+CoverGeometry(a,b));yield return Capture(app,directory,"pin-ai-cover",true);

        yield return NaturalPlay(app,integrity,directory,check);
        yield return GBAuditSprawl.Run(app,directory,check);
        yield return GBAuditWalk.Run(app,directory,check);
        int[] decisions=new int[3];bool legal=true;for(int difficulty=0;difficulty<3;difficulty++){
            Start(app);app.AIDifficulty=difficulty;var bot=new GBBot();a=app.Wrestlers[1];b=app.Wrestlers[0];float end=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<end){GBInput i=bot.Tick(app,a,b);legal&=!i.Has(32|256)&&i.HasHandAim;app.ScriptedInputs[1]=i;integrity.Measure(app);yield return null;}decisions[difficulty]=bot.Decisions;
        }
        check("AI_uses_real_arm_commands_no_throw",legal,"all sampled difficulties aim physical hands and emit no retired Throw or Tackle bit");
        check("AI_difficulty_reaction_cadence",decisions[0]<decisions[1]&&decisions[1]<decisions[2],"easy/normal/hard decisions in matched2s="+string.Join("/",decisions));
        app.AIDifficulty=1;
        app.ApplyFocus(true);app.ScriptedInputs=new GBInput[2];bool f11=false;app.AuditKeyDown=k=>{if(k==KeyCode.F11&&f11){f11=false;return true;}return false;};
        if(Screen.fullScreenMode!=FullScreenMode.Windowed){f11=true;yield return Wait(app,integrity,.4f);}
        f11=true;yield return Wait(app,integrity,.5f);check("F11_enters_fullscreen",Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"native screen mode="+Screen.fullScreenMode);
        f11=true;yield return Wait(app,integrity,.5f);check("F11_restores_window",Screen.fullScreenMode==FullScreenMode.Windowed,"native screen mode="+Screen.fullScreenMode);app.AuditKeyDown=null;
        check("pin_arms_bodies_connected_finite",integrity.finite&&integrity.missing==0&&integrity.gap<.06f,"max physical joint gap="+integrity.gap.ToString("0.0000")+" m missing="+integrity.missing);
        check("pin_arm_self_contact_bounded",integrity.penetration<.045f,"maximum measured nonadjacent forearm/hand penetration="+integrity.penetration.ToString("0.0000")+" m across cursor, grips, covers and escapes");
        check("pin_no_runtime_errors",app.Issues.Count==0,string.Join("; ",app.Issues));
    }

    static IEnumerator Cover(GBApp app,Integrity integrity,string directory,bool c,bool escape,bool noGrip,Outcome result,bool rope=false,bool aiEscape=false,bool faceDown=false,bool steer=false){
        LyingSetup(app,faceDown);var a=app.Wrestlers[0];var b=app.Wrestlers[1];Vector3 start=b.Root.position;Quaternion rotation=b.bodies[1].rotation;float end=Time.realtimeSinceStartup+(c&&!noGrip?12:2.4f),last=Time.realtimeSinceStartup;bool escaping=false;
        bool captured=false,movedToRope=false;float steerAt=-1;var defenseBot=new GBBot();if(aiEscape)app.AIDifficulty=1;
        while(Time.realtimeSinceStartup<end&&app.Phase==GBApp.GamePhase.Fighting){
            Vector3 toward=Vector3.ProjectOnPlane(b.bodies[1].position-a.Root.position,Vector3.up).normalized;
            int heldButtons=noGrip?0:(a.hands[0].releaseRequired?0:GBInput.Left)|(a.hands[1].releaseRequired?0:GBInput.Right);
            int heldGrips=Grips(a,b);
            bool coverRequest=c&&(noGrip||heldGrips>0);
            // Follow the actual player sequence: aim/reach/click first, then
            // hold C with a live manual grip and move over the shoulders.
            // The free arm can still reach for its separate manual grip.
            Vector3 pinMove=c&&!noGrip&&heldGrips==0?toward*.45f:Vector3.zero;
            if(steer&&a.IsCovering){if(steerAt<0)steerAt=Time.time;float t=Time.time-steerAt;if(t<.55f)pinMove=Vector3.ProjectOnPlane(b.bodies[1].rotation*Vector3.right,Vector3.up).normalized*(t<.275f?.25f:-.25f);}
            app.ScriptedInputs[0]=Input(a,toward,CoverContact(a,b),heldButtons|(coverRequest?GBInput.Pin:0),pinMove);
            if(steer&&a.IsCovering){result.pressureLeft|=a.PinPressureBias<-.1f;result.pressureRight|=a.PinPressureBias>.1f;result.maxLoad=Mathf.Max(result.maxLoad,a.LeftPinLoad+a.RightPinLoad);}
            int grips=Grips(a,b);result.grips=Mathf.Max(result.grips,grips);result.covered|=a.IsCovering;bool l,r;result.shoulders|=b.ShoulderContact(out l,out r);result.count=Mathf.Max(result.count,app.PinCount);result.geometry=CoverGeometry(a,b);
            if(app.PinCount>.02f){var own=a.bodies[1].GetComponent<Collider>();var rival=b.bodies[1].GetComponent<Collider>();Vector3 pa=own.ClosestPoint(b.bodies[1].position),pb=rival.ClosestPoint(pa);if(Vector3.Distance(own.ClosestPoint(pb),pb)>.08f)result.countWithoutChestCover=true;}
            float now=Time.realtimeSinceStartup;if(a.IsCovering)result.coverSeconds+=now-last;last=now;
            if(escape&&app.PinCount>.3f)escaping=true;
            if(escaping)app.ScriptedInputs[1]=aiEscape?defenseBot.Tick(app,b,a):new GBInput{ax=-toward.x,az=-toward.z,x=1,z=.7f,flags=GBInput.Brace};
            // PinControlled disables standing recovery even after the normal
            // down timer expires; the bridge must break real contact here.
            if(escaping&&(b.Down||b.PinControlled)&&(!a.IsCovering||!b.ShoulderContact(out l,out r))&&app.PinCount<.01f&&result.count>.1f)result.escapedUnderControl=true;
            if(rope&&!movedToRope&&app.PinCount>.3f){foreach(var w in new[]{a,b})foreach(var body in w.bodies)body.position+=Vector3.right*3.9f;Physics.SyncTransforms();movedToRope=true;}
            if(movedToRope&&app.Arena.TouchesRopes(b)&&b.ShoulderContact(out l,out r)&&a.IsCovering&&app.PinCount<.01f){result.ropeInterrupted=true;break;}
            if(c&&!noGrip&&!escape&&!rope&&!captured&&app.PinCount>.6f){captured=true;yield return Capture(app,directory,faceDown?"pin-chest-down-live-count":"pin-back-live-count",true);}
            result.defenderTravel=Mathf.Max(result.defenderTravel,Vector3.Distance(start,b.Root.position));result.torsoRotation=Mathf.Max(result.torsoRotation,Quaternion.Angle(rotation,b.bodies[1].rotation));integrity.Measure(app);yield return null;
        }
        result.won=app.Scores[0]>0;yield return Capture(app,directory,rope?"pin-rope":faceDown?"pin-chest-down":c?(escape?(aiEscape?"pin-ai-bridge-escape":"pin-bridge-escape"):noGrip?"pin-no-grip":"pin-cover") :"pin-no-C",c&&!escape&&!noGrip);
    }
    static string CoverGeometry(GBWrestler a,GBWrestler b){
        bool sl,sr;b.ShoulderContact(out sl,out sr);var text=new System.Text.StringBuilder(a.CoverGeometry+" shoulders="+sl+"/"+sr+" victimEuler="+b.bodies[1].rotation.eulerAngles+" pinStatus="+GBApp.I.PinStatus);
        text.Append(" hands=").Append(a.hands[0].Label).Append("/").Append(a.hands[1].Label);
        text.Append(" armEffort=").Append(a.LastArmEffort.ToString("0.0")).Append("N actualLegChain[");
        // Observe the actual rigid bodies and parent-relative rotations rather
        // than the requested pose: a propped shin/foot can prevent chest contact.
        foreach(int n in new[]{0,1,3,4,5,6,7,8,9,10,11,12,13,14}){
            var body=a.bodies[n];Vector3 relative=n==0?body.rotation.eulerAngles:(Quaternion.Inverse(a.bodies[a.parents[n]].rotation)*body.rotation).eulerAngles;
            text.Append(n).Append(":p").Append(body.position.ToString("F2"));
            text.Append(" up").Append(Vector3.Dot(body.rotation*Vector3.up,Vector3.up).ToString("0.00"));
            text.Append(" euler").Append(body.rotation.eulerAngles.ToString("F0"));
            text.Append(" joint").Append(relative.ToString("F0")).Append(" contacts=").Append(body.GetComponent<GBContact>().AuditContacts).Append(';');
        }
        return text.Append(']').ToString();
    }
    static IEnumerator Shot(GBApp app,Integrity integrity,bool running,bool brace,ShotResult result){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(running?-3:-.75f,.28f,-1));b.ResetTo(new Vector3(.75f,.28f,-1));
        if(brace)app.ScriptedInputs[1]=new GBInput{ax=-1,flags=GBInput.Brace};yield return Wait(app,integrity,.65f);
        if(running){app.ScriptedInputs[0]=new GBInput{ax=1,x=1,flags=GBInput.Sprint};float end=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<end&&b.Root.position.x-a.Root.position.x>1.5f){integrity.Measure(app);yield return null;}}
        result.approach=Vector3.Dot(Velocity(a)-Velocity(b),Vector3.right);Vector3 start=b.Root.position;float balance=b.balance,t=Time.realtimeSinceStartup,endShot=t+.75f;
        while(Time.realtimeSinceStartup<endShot){app.ScriptedInputs[0]=new GBInput{ax=1,flags=Time.realtimeSinceStartup-t<.1f?GBInput.Strike:0};result.shooting|=a.Shooting;result.peak=Mathf.Max(result.peak,Vector3.ProjectOnPlane(Velocity(b),Vector3.up).magnitude);result.travel=Mathf.Max(result.travel,b.Root.position.x-start.x);result.balanceLoss=Mathf.Max(result.balanceLoss,balance-b.balance);result.hands=Mathf.Max(result.hands,a.GripCount);integrity.Measure(app);yield return null;}
    }
    static IEnumerator NaturalPlay(GBApp app,Integrity integrity,string directory,Action<string,bool,string> check){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(-.6f,.28f,-1));b.ResetTo(new Vector3(.6f,.28f,-1));yield return Wait(app,integrity,.6f);
        float start=Time.realtimeSinceStartup,end=start+18,last=start,lostHoldAt=-1,nextTrace=0;int grips=0;bool down=false,timedDown=false,cover=false,pinCommit=false;float count=0,minBalance=100,minChestHeight=10,minUpright=1,gripSeconds=0;
        while(Time.realtimeSinceStartup<end&&app.Phase==GBApp.GamePhase.Fighting){
            float t=Time.realtimeSinceStartup-start;Vector3 delta=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up),aim=delta.sqrMagnitude>.01f?delta.normalized:Vector3.right,side=Vector3.Cross(Vector3.up,aim);
            int heldNow=Grips(a,b);bool eligible=b.PhysicallyDownForPin;
            if(eligible&&heldNow>0)pinCommit=true;
            if(heldNow==0){if(lostHoldAt<0)lostHoldAt=Time.realtimeSinceStartup;if(Time.realtimeSinceStartup-lostHoldAt>.6f)pinCommit=false;}else lostHoldAt=-1;
            if(delta.magnitude>1.8f)pinCommit=false;
            // A player commits to holding C through the physical roll. Balance
            // recovery during that roll does not mean they release the button.
            bool pin=pinCommit||eligible;
            int flags=GBInput.Left|GBInput.Right;Vector3 move=delta.magnitude>1.15f?aim*.55f:Vector3.zero,target=CoverContact(a,b);
            if(t<.12f){flags=GBInput.Strike;move=aim*.6f;}
            else if(pin){flags|=GBInput.Pin;move=Vector3.zero;}
            else if(Grips(a,b)>0){float phase=t%3;if(phase<1.6f){move=-aim*.65f+side*.3f;target=a.Root.position+aim*.55f+Vector3.up*.3f;}else {move=side*.8f-aim*.25f;aim=Quaternion.AngleAxis(55,Vector3.up)*aim;target=a.Root.position+aim*.75f-Vector3.up*.25f;}}
            Vector3 ringPosition=Vector3.ProjectOnPlane(a.Root.position,Vector3.up);if(!pin&&ringPosition.magnitude>2.1f)move=-ringPosition.normalized*.65f+side*.2f;
            if(a.hands[0].releaseRequired||a.hands[1].releaseRequired)flags&=~(GBInput.Left|GBInput.Right);
            app.ScriptedInputs[0]=Input(a,aim,target,flags,move);app.ScriptedInputs[1]=new GBInput{ax=-aim.x,az=-aim.z};int liveGrips=Grips(a,b);grips=Mathf.Max(grips,liveGrips);
            float chestHeight=b.bodies[1].position.y-MatHeight(b.bodies[1].position),upright=Vector3.Dot(b.bodies[1].rotation*Vector3.up,Vector3.up),now=Time.realtimeSinceStartup;
            if(liveGrips>0)gripSeconds+=now-last;last=now;minBalance=Mathf.Min(minBalance,b.balance);minChestHeight=Mathf.Min(minChestHeight,chestHeight);minUpright=Mathf.Min(minUpright,upright);
            down|=chestHeight<.65f&&upright<.55f;timedDown|=b.Down;cover|=a.IsCovering;count=Mathf.Max(count,app.PinCount);integrity.Measure(app);yield return null;
            if(t>=nextTrace){nextTrace=t+.5f;bool sl,sr;b.ShoulderContact(out sl,out sr);Debug.Log("PIN_PLAY t="+t.ToString("0.0")+" balance="+b.balance.ToString("0.0")+" shoulders="+sl+"/"+sr+" ropes="+app.Arena.TouchesRopes(b)+" count="+app.PinCount.ToString("0.00")+" victim="+b.bodies[1].rotation.eulerAngles+" "+a.CoverGeometry);}
        }
        check("standing_manual_exchange_reaches_pin",grips>0&&down&&cover&&count>.15f,"natural shot/reach/pull/pivot/C: grips="+grips+" actual low horizontal body="+down+" timedDown diagnostic="+timedDown+" actual cover="+cover+" peak count="+count.ToString("0.00")+" score="+app.Scores[0]+" minimum balance="+minBalance.ToString("0.0")+" low chestHeight/upright="+minChestHeight.ToString("0.000")+"/"+minUpright.ToString("0.000")+" grip seconds="+gripSeconds.ToString("0.00")+" "+a.CoverGeometry+"; no prepared lying pose or direct attack/grip call");
        yield return Capture(app,directory,"pin-natural-play",true);
    }
}
