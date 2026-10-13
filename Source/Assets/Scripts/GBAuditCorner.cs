using UnityEngine;
using System;
using System.IO;
using System.Collections;

public static class GBAuditCorner {
    struct Trial {public string gates;public float force,loss,cost;public Vector3 netForce,netTorque;}
    static Vector3 Force(GBApp app){Vector3 total=Vector3.zero;foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)total+=rb.GetAccumulatedForce();return total;}
    static Vector3 Torque(GBApp app){Vector3 total=Vector3.zero;foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)total+=rb.GetAccumulatedTorque()+Vector3.Cross(rb.worldCenterOfMass,rb.GetAccumulatedForce());return total;}
    static void SyncPreparedBodies(GBApp app){
        // Rigidbody.position writes are buffered until a physics step. With
        // simulation intentionally stopped, synchronize the fixture's current
        // physical pose into collider/Transform space before computing anchors.
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)rb.transform.SetPositionAndRotation(rb.position,rb.rotation);
        Physics.SyncTransforms();
    }
    static Trial Isolate(GBApp app,int grips,int region,float lateral,float speed,int defense,bool airborne=false,bool down=false,bool far=false,bool bill=false,bool victimAirborne=false,bool outside=false,bool below=false){
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(0,.20f,-.65f));b.ResetTo(new Vector3(0,.20f,.30f));
        // Deliberately prepared manual-contact fixture isolates the production
        // contact wrench and gates. Natural acquisition is tested separately.
        SyncPreparedBodies(app);
        var target=b.bodies[region].GetComponent<GBTarget>();var point=target.GetComponent<Collider>().ClosestPoint(a.bodies[5].position);
        for(int h=0;h<grips;h++){a.bodies[h==0?5:8].position=point;SyncPreparedBodies(app);a.Attach(h,target,point);}
        if(far)a.bodies[5].position+=Vector3.right*.8f;
        if(airborne){a.bodies[11].position+=Vector3.up*.25f;a.bodies[14].position+=Vector3.up*.25f;}
        if(victimAirborne){b.bodies[11].position+=Vector3.up*.25f;b.bodies[14].position+=Vector3.up*.25f;}
        if(outside){a.bodies[11].position+=Vector3.right*8;a.bodies[14].position+=Vector3.right*8;}
        if(below){a.bodies[11].position-=Vector3.up*.25f;a.bodies[14].position-=Vector3.up*.25f;}
        if(down)a.downUntil=Time.time+1;
        foreach(var rb in a.bodies)rb.linearVelocity=Vector3.right*speed;
        a.command=new GBInput{az=1,x=lateral,flags=(grips>0?GBInput.Left:0)|(grips>1?GBInput.Right:0)};b.command=new GBInput{az=-1,flags=defense};b.grounded=true;
        SyncPreparedBodies(app);float balance=b.balance,energy=a.stamina;Vector3 force=Force(app),torque=Torque(app);
        if(bill)a.Simulate(a.command);else a.CornerTick(Time.fixedDeltaTime);
        string gates="handCount="+a.GripCount+" roots="+a.Root.position+"/"+b.Root.position+" footbottoms="+a.bodies[11].GetComponent<Collider>().bounds.min.y+"/"+a.bodies[14].GetComponent<Collider>().bounds.min.y+"/"+b.bodies[11].GetComponent<Collider>().bounds.min.y+"/"+b.bodies[14].GetComponent<Collider>().bounds.min.y;
        if(a.hands[0].joint)gates+=" handRb="+a.bodies[5].position+" handTransform="+a.bodies[5].transform.position+" targetRb="+target.Body.position+" targetTransform="+target.transform.position+" localHandAnchor="+a.hands[0].joint.anchor+" localLegAnchor="+a.hands[0].localAnchor+" anchorGap="+Vector3.Distance(a.bodies[5].transform.TransformPoint(a.hands[0].joint.anchor),target.transform.TransformPoint(a.hands[0].localAnchor));
        Debug.Log("CORNER_ISOLATE region="+region+" grips="+grips+" side="+lateral+" speed="+speed+" defense="+defense+" force="+a.LastCornerForce+" "+gates);
        var result=new Trial{gates=gates,force=a.LastCornerForce,loss=balance-b.balance,cost=(energy-a.stamina)/Time.fixedDeltaTime,netForce=Force(app)-force,netTorque=Torque(app)-torque};
        // ResetTo changes pose/velocity but does not clear accumulated wrenches.
        // Cancel this diagnostic's forces before the next independent fixture.
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies){rb.AddForce(-rb.GetAccumulatedForce(),ForceMode.Force);rb.AddTorque(-rb.GetAccumulatedTorque(),ForceMode.Force);}
        return result;
    }
    static GBInput LegAim(GBWrestler a,GBWrestler b,Vector3 move){
        Vector3 toward=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up).normalized;
        Vector3 point=b.bodies[9].position-toward*.10f-Vector3.up*.04f,offset=point-a.Root.position;
        return new GBInput{ax=toward.x,az=toward.z,x=move.x,z=move.z,hx=offset.x,hy=offset.y,hz=offset.z,flags=GBInput.Brace|GBInput.HandAim|GBInput.Left|GBInput.Right};
    }
    static int LegGrips(GBWrestler a,GBWrestler b){int count=0;foreach(var h in a.hands)if(h.joint&&h.target&&h.target.wrestler==b&&(h.target.region.Contains("thigh")||h.target.region.Contains("shin")||h.target.region.Contains("foot")))count++;return count;}
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);yield return null;
        app.enabled=false;var simulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        // Direct-contact fixtures need current collider/anchor transforms,
        // independent of the render interpolation from the previous gameframe.
        var interpolation=new RigidbodyInterpolation[app.Registry.Count];for(int n=0;n<app.Registry.Count;n++){interpolation[n]=app.Registry[n].interpolation;app.Registry[n].interpolation=RigidbodyInterpolation.None;}
        var two=Isolate(app,2,9,1,.8f,0);var one=Isolate(app,1,9,1,.8f,0);
        check("corner_adds_bounded_physical_leg_pressure",two.force>20&&two.force<=110&&two.loss>0,"prepared live manual leg contacts: actual extra force="+two.force+" N balance loss="+two.loss+" in one physicaltick; "+two.gates);
        check("corner_contact_wrench_has_equal_reaction",two.force>20&&two.netForce.magnitude<.01f&&two.netTorque.magnitude<.02f,"actual accumulated net force="+two.netForce+" and origin torque="+two.netTorque+" across both15-body athletes");
        check("corner_one_arm_is_weaker",one.force>0&&one.force<two.force*.60f&&one.loss<two.loss*.60f,"one/two actual force="+one.force+"/"+two.force+" and extra balance loss="+one.loss+"/"+two.loss);
        var brace=Isolate(app,2,9,1,.8f,GBInput.Brace);var sprawl=Isolate(app,2,9,1,.8f,GBInput.Sprawl);
        check("corner_brace_and_sprawl_resist",brace.force<two.force*.5f&&sprawl.force<brace.force*.6f,"normal/brace/sprawl actualforce="+two.force+"/"+brace.force+"/"+sprawl.force);
        check("corner_requires_earned_sideways_motion",Isolate(app,2,9,1,0,0).force==0&&Isolate(app,2,9,0,.8f,0).force==0,"stationary side key and non-lateral input supply no finishing pressure");
        check("corner_requires_actual_leg_contact",Isolate(app,0,9,1,.8f,0).force==0&&Isolate(app,2,1,1,.8f,0).force==0&&Isolate(app,1,9,1,.8f,0,false,false,true).force==0,"no grip, torso grip and remote stretched leggrip all inactive");
        check("corner_requires_mat_support_and_no_ground_flip",Isolate(app,2,9,1,.8f,0,true).force==0&&Isolate(app,2,9,1,.8f,0,false,true).force==0&&Isolate(app,2,9,1,.8f,0,victimAirborne:true).force==0&&Isolate(app,2,9,1,.8f,0,outside:true).force==0&&Isolate(app,2,9,1,.8f,0,below:true).force==0,"airborne attacker/victim, down attacker, boots outside mat or below mat receive no corner leverage");
        var normalBill=Isolate(app,2,9,1,.8f,0,false,false,false,true);var braceBill=Isolate(app,2,9,1,.8f,GBInput.Brace,false,false,false,true);
        check("corner_preserves_grip_work_cost_no_brace_bill",normalBill.force>0&&braceBill.force>0&&Mathf.Abs(normalBill.cost-6)<.04f&&Mathf.Abs(braceBill.cost-normalBill.cost)<.04f,"actual full production Simulate stamina/s normal/braced="+normalBill.cost+"/"+braceBill.cost+"; two grips4+existingpull2, no new cost");
        // Reset removes every prepared hand/body fixture before live gameplay.
        app.Wrestlers[0].ResetTo(new Vector3(0,.20f,-.25f));app.Wrestlers[1].ResetTo(new Vector3(0,.20f,.30f));Physics.SyncTransforms();for(int n=0;n<app.Registry.Count;n++)app.Registry[n].interpolation=interpolation[n];Physics.simulationMode=simulation;app.enabled=true;
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];app.ScriptedInputs[0]=new GBInput{az=1,flags=GBInput.Brace};app.ScriptedInputs[1]=new GBInput{az=-1};yield return new WaitForSeconds(.7f);
        float until=Time.time+1.5f;int caught=0;float gap=0;
        while(Time.time<until&&caught==0){app.ScriptedInputs[0]=LegAim(a,b,Vector3.zero);yield return new WaitForFixedUpdate();caught=LegGrips(a,b);gap=Mathf.Max(gap,a.JointSeparation());}
        check("corner_leg_is_manually_caught_in_live_play",caught>0,"natural handinput+mouse leg aim acquired="+caught+"; no Attach/velocity injection in live scenario; "+GBAuditArms.ReachEvidence(a,0));
        float earned=0,next=0;int frames=0;Vector3 before=b.Root.position;until=Time.time+1.4f;
        while(Time.time<until){Vector3 toward=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up).normalized;app.ScriptedInputs[0]=LegAim(a,b,Vector3.Cross(Vector3.up,toward)*.8f);yield return new WaitForFixedUpdate();earned=Mathf.Max(earned,a.CornerImpulseTotal);gap=Mathf.Max(gap,Mathf.Max(a.JointSeparation(),b.JointSeparation()));if(!Application.isBatchMode&&Time.time>=next){ScreenCapture.CaptureScreenshot(Path.Combine(directory,"corner-live-motion-"+(frames++).ToString("00")+".png"));next=Time.time+.15f;}}
        check("live_manual_leg_catch_can_turn_corner",caught>0&&earned>.5f&&Vector3.Distance(before,b.Root.position)>.05f,"actual corner force integral="+earned+" Ns; opponent displacement="+Vector3.Distance(before,b.Root.position)+" m; fixture creates no instant takedown");
        check("corner_live_bodies_connected",gap<.03f&&app.Registry.Count==92&&a.bodies.Length==15,"actual maxliveanchor separation="+gap+" and registry="+app.Registry.Count);
        app.ScriptedInputs[0]=app.ScriptedInputs[1]=new GBInput{az=1};yield return new WaitForSeconds(.1f);
        check("corner_release_stops_finishing_pressure",!a.TurningCorner&&a.LastCornerForce==0&&a.GripCount==0,"release returns normal physical play");
        check("corner_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));app.ReturnToMenu();
    }
}
