using UnityEngine;
using System;
using System.IO;
using System.Collections;

// Prepared airborne holds isolate landing recovery. No throw action or
// artificial gameplay impulse is introduced; release uses ordinary input.
public static class GBAuditCarry {
    static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    static void Pose(GBWrestler w,Vector3 root,Quaternion rotation){
        w.ResetTo(root-w.initial[0]);for(int n=0;n<15;n++){var b=w.bodies[n];Vector3 p=root+rotation*(w.initial[n]-w.initial[0]);b.transform.SetPositionAndRotation(p,rotation);b.position=p;b.rotation=rotation;b.linearVelocity=b.angularVelocity=Vector3.zero;}Physics.SyncTransforms();
    }
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.BeginMatch(3,1);check("stamina_extra_15_percent",app.Wrestlers[0].stamina==135f*1.15f&&app.Wrestlers[1].stamina==135f*1.15f&&app.Wrestlers[0].StaminaFraction==1,"fresh/reset capacity="+app.Wrestlers[0].stamina+"; 135 x 1.15; full normalized meter/strength");
        for(int sample=0;sample<2;sample++){
            app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);app.ScriptedInputs[0]=new GBInput{ax=1};app.ScriptedInputs[1]=new GBInput{ax=-1};yield return Wait(.4f);
            var a=app.Wrestlers[0];var b=app.Wrestlers[1];bool tilted=sample==1;string name=tilted?"tilted":"upright";
            // Poll held input before installing the prepared hold. Fixed steps
            // can otherwise run before the next rendered input poll releases it.
            app.ScriptedInputs[0]=new GBInput{ax=1,flags=GBInput.Left};yield return null;
            Pose(b,new Vector3(.65f,2.25f,-1),Quaternion.Euler(0,0,tilted?100:0));
            var target=b.bodies[1].GetComponent<GBTarget>();bool attached=a.Attach(0,target,target.GetComponent<Collider>().ClosestPoint(a.bodies[5].position));
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            check(name+"_prepared_airborne_hold",attached&&b.RecoveringFromCarry,"prepared real grip="+attached+" airborne recovery latched="+b.RecoveringFromCarry+"; no Down timer");
            app.ScriptedInputs[0]=new GBInput{ax=1};yield return null;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            check(name+"_manual_release",a.GripCount==0,"ordinary release input; remaining hands="+a.GripCount);
            float end=Time.realtimeSinceStartup+2.5f,maxGap=0;bool touched=false,coreFirst=false,coreLanded=false,finite=true;float landingUp=1,flightUp=Vector3.Dot(b.bodies[1].transform.up,Vector3.up);
            RaycastHit floor;Physics.Raycast(new Vector3(0,10,0),Vector3.down,out floor,20,1,QueryTriggerInteraction.Ignore);
            while(Time.realtimeSinceStartup<end){
                maxGap=Mathf.Max(maxGap,b.JointSeparation());foreach(var body in b.bodies)finite&=!float.IsNaN(body.position.x)&&!float.IsInfinity(body.position.x);
                bool core=b.bodies[0].GetComponent<Collider>().bounds.min.y<=floor.point.y+.035f||b.bodies[1].GetComponent<Collider>().bounds.min.y<=floor.point.y+.035f;
                bool feet=b.bodies[11].GetComponent<Collider>().bounds.min.y<=floor.point.y+.035f||b.bodies[14].GetComponent<Collider>().bounds.min.y<=floor.point.y+.035f;
                coreLanded|=core;
                if(!touched&&(core||feet)){touched=true;coreFirst=core&&!feet;landingUp=Vector3.Dot(b.bodies[1].transform.up,Vector3.up);app.Capture(Path.Combine(directory,name+"-landing.png"));}
                yield return null;
            }
            check(name+"_physical_landing",touched&&(tilted?landingUp<.55f:!coreFirst&&landingUp>.7f),"first core contact="+coreFirst+" landed on core="+coreLanded+" release torso up="+flightUp.ToString("0.00")+" landing up="+landingUp.ToString("0.00")+"; contact while sideways is distinct from a standing feet-first landing; boot edges may touch first");
            check(name+"_recovery_not_stuck",!b.RecoveringFromCarry,"carry flight/settle lock cleared after floor contact="+!b.RecoveringFromCarry);
            check(name+"_body_integrity",finite&&maxGap<.06f,"max permanent joint gap="+maxGap.ToString("0.0000"));
        }
        check("carry_runtime_no_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
