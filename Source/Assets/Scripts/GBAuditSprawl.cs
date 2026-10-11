using UnityEngine;
using System;
using System.IO;
using System.Collections;

// A normal-input posture/counter fixture. After initial ResetTo placement this
// runner never attaches grips, changes body poses, injects velocity, or freezes.
public static class GBAuditSprawl {
    sealed class Integrity {
        public bool finite=true;public float gap;public int missing;
        public void Measure(GBApp app){foreach(var w in app.Wrestlers){
            float separation=w.JointSeparation();if(float.IsInfinity(separation))missing++;else gap=Mathf.Max(gap,separation);
            foreach(var body in w.bodies){if(!body){finite=false;continue;}finite&=Valid(body.position)&&Valid(body.linearVelocity)&&Valid(body.angularVelocity);}
        }}
        static bool Valid(Vector3 v){return !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));}
    }
    sealed class Posture {
        public float hips,chest,frontDown,legsBack;public int samples,hands;public bool shooting;
        public void Measure(GBWrestler w,float floor){
            hips+=w.Root.position.y-floor;chest+=w.bodies[1].position.y-floor;
            frontDown+=Vector3.Dot(w.bodies[1].rotation*Vector3.forward,Vector3.down);
            Vector3 feet=(w.bodies[11].position+w.bodies[14].position)*.5f;
            legsBack+=Vector3.Dot(Vector3.ProjectOnPlane(feet-w.Root.position,Vector3.up),w.facing);samples++;
            hands=Mathf.Max(hands,w.GripCount);shooting|=w.Shooting;
        }
        public void Average(){float n=Mathf.Max(1,samples);hips/=n;chest/=n;frontDown/=n;legsBack/=n;}
        public override string ToString(){return "hips/chest="+hips.ToString("0.000")+"/"+chest.ToString("0.000")+" m above mat, frontDown="+frontDown.ToString("0.000")+" feet forward="+legsBack.ToString("0.000")+" m";}
    }
    sealed class Shot {public float approach,peak,travel,balanceLoss,contact;public bool shooting,sprawling;public int hands;}
    static GBInput Command(Vector3 aim,int flags=0,Vector3 hand=default,Vector3 move=default){
        if(hand==default)hand=aim*.68f+Vector3.up*.3f;
        return new GBInput{ax=aim.x,az=aim.z,x=move.x,z=move.z,hx=hand.x,hy=hand.y,hz=hand.z,flags=flags|GBInput.HandAim}.Sanitize();
    }
    static void Start(GBApp app){app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);app.ScriptedInputs[0]=Command(Vector3.forward);app.ScriptedInputs[1]=Command(Vector3.back);}
    static float Floor(GBApp app){foreach(var collider in app.Arena.GetComponentsInChildren<Collider>())if(collider.enabled&&collider.name=="ring mat")return collider.bounds.max.y;throw new InvalidOperationException("Sprawl fixture needs ring mat collider");}
    static Vector3 Velocity(GBWrestler w){Vector3 value=Vector3.zero;float mass=0;foreach(var body in w.bodies){value+=body.linearVelocity*body.mass;mass+=body.mass;}return value/Mathf.Max(1,mass);}
    static Vector3 Hands(GBWrestler w){return (w.bodies[5].position+w.bodies[8].position)*.5f-w.Root.position;}
    static IEnumerator Wait(GBApp app,Integrity integrity,float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end){integrity.Measure(app);yield return null;}}
    static IEnumerator Sample(GBApp app,GBWrestler w,Integrity integrity,float seconds,Posture result){
        float end=Time.realtimeSinceStartup+seconds,floor=Floor(app);while(Time.realtimeSinceStartup<end){result.Measure(w,floor);integrity.Measure(app);yield return null;}result.Average();
    }

    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        var integrity=new Integrity();Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(-1.6f,.28f,0));b.ResetTo(new Vector3(1.6f,.28f,0));yield return Wait(app,integrity,1);
        var standing=new Posture();yield return Sample(app,a,integrity,.4f,standing);app.Capture(Path.Combine(directory,"sprawl-standing.png"));
        float stamina=a.stamina;app.ScriptedInputs[0]=Command(Vector3.forward,GBInput.Sprawl);yield return Wait(app,integrity,1.6f);
        var sprawled=new Posture();yield return Sample(app,a,integrity,.5f,sprawled);app.Capture(Path.Combine(directory,"sprawl-held.png"));
        check("sprawl_actual_body_lowers",a.Sprawling&&standing.hips-sprawled.hips>.2f&&standing.chest-sprawled.chest>.25f,"standing "+standing+"; held E "+sprawled);
        check("sprawl_legs_extend_back_and_chest_turns_down",sprawled.frontDown>.5f&&sprawled.legsBack<-.15f&&sprawled.legsBack<standing.legsBack-.15f,"standing/held feet forward="+standing.legsBack.ToString("0.000")+"/"+sprawled.legsBack.ToString("0.000")+" actual torso frontDown="+sprawled.frontDown.ToString("0.000"));
        check("sprawl_costs_stamina_without_shot_or_auto_grip",stamina-a.stamina>8&&sprawled.hands==0&&!sprawled.shooting,"held2.1s stamina="+stamina.ToString("0.0")+"→"+a.stamina.ToString("0.0")+" max grips="+sprawled.hands+" shooting="+sprawled.shooting);
        app.ScriptedInputs[0]=Command(Vector3.forward,GBInput.Sprawl,new Vector3(0,-.2f,.65f));yield return Wait(app,integrity,.7f);Vector3 low=Hands(a);
        app.ScriptedInputs[0]=Command(Vector3.forward,GBInput.Sprawl,new Vector3(0,.8f,.65f));yield return Wait(app,integrity,.7f);Vector3 high=Hands(a);
        check("sprawl_preserves_manual_arm_motion",a.Sprawling&&high.y-low.y>.2f&&a.GripCount==0,"actual root-relative mean hand y low/high="+low.y.ToString("0.000")+"/"+high.y.ToString("0.000")+" grips="+a.GripCount);app.Capture(Path.Combine(directory,"sprawl-arms-high.png"));
        app.ScriptedInputs[0]=Command(Vector3.forward);yield return Wait(app,integrity,2.2f);var recovered=new Posture();yield return Sample(app,a,integrity,.4f,recovered);
        check("release_sprawl_returns_to_standing",!a.Sprawling&&!a.Down&&recovered.hips>standing.hips-.2f&&recovered.chest>standing.chest-.2f&&recovered.frontDown<.4f,"released "+recovered+"; original "+standing);app.Capture(Path.Combine(directory,"sprawl-recovered.png"));

        app.ScriptedInputs[0]=Command(Vector3.forward,GBInput.Sprawl);float fatigueEnd=Time.realtimeSinceStartup+a.stamina/10+2,exhaustedAt=-1;bool activated=false,reentered=false;
        while(Time.realtimeSinceStartup<fatigueEnd){
            activated|=a.Sprawling;if(activated&&!a.Sprawling&&a.stamina<7&&exhaustedAt<0)exhaustedAt=Time.realtimeSinceStartup;
            if(exhaustedAt>=0){reentered|=a.Sprawling;if(Time.realtimeSinceStartup-exhaustedAt>1)break;}
            integrity.Measure(app);yield return null;
        }
        check("held_exhausted_sprawl_does_not_rapidly_reenter",activated&&exhaustedAt>=0&&!reentered&&!a.Sprawling,"naturally exhausted through held E; exhaustion observed="+(exhaustedAt>=0)+" reentered without release="+reentered+" stamina after recovery="+a.stamina.ToString("0.0"));
        app.ScriptedInputs[0]=Command(Vector3.forward);yield return Wait(app,integrity,.3f);app.ScriptedInputs[0]=Command(Vector3.forward,GBInput.Sprawl);yield return Wait(app,integrity,.3f);
        check("exhausted_sprawl_can_restart_after_release",a.Sprawling&&a.stamina>4,"release/repress E after natural recovery; active="+a.Sprawling+" stamina="+a.stamina.ToString("0.0"));

        var neutral=new Shot();yield return RunningShot(app,integrity,false,neutral);
        var counter=new Shot();yield return RunningShot(app,integrity,true,counter);
        check("sprawl_faces_equally_fast_or_faster_running_shot",neutral.shooting&&counter.shooting&&neutral.approach>2&&counter.approach>2&&counter.approach>=neutral.approach-.75f&&neutral.contact>0&&counter.sprawling,"same legal sprint/shot input, relative approach neutral/sprawl="+neutral.approach.ToString("0.00")+"/"+counter.approach.ToString("0.00")+" m/s neutral contact speed="+neutral.contact.ToString("0.00")+" defender active sprawl="+counter.sprawling);
        check("sprawl_reduces_running_shot_effect",counter.peak<neutral.peak*.85f&&counter.travel<neutral.travel-.08f,"neutral/sprawl target peak="+neutral.peak.ToString("0.00")+"/"+counter.peak.ToString("0.00")+" m/s travel="+neutral.travel.ToString("0.00")+"/"+counter.travel.ToString("0.00")+" m balanceLoss="+neutral.balanceLoss.ToString("0.0")+"/"+counter.balanceLoss.ToString("0.0")+" contact="+neutral.contact.ToString("0.00")+"/"+counter.contact.ToString("0.00"));
        check("sprawl_counter_never_automatically_grips",neutral.hands==0&&counter.hands==0,"Q/E only maximum hands neutral/sprawl="+neutral.hands+"/"+counter.hands);

        for(int difficulty=1;difficulty<=2;difficulty++){
            Start(app);a=app.Wrestlers[0];b=app.Wrestlers[1];a.ResetTo(new Vector3(-1.1f,.28f,-1));b.ResetTo(new Vector3(1.1f,.28f,-1));app.ScriptedInputs[0]=Command(Vector3.right);app.ScriptedInputs[1]=Command(Vector3.left);yield return Wait(app,integrity,.7f);
            app.AIDifficulty=difficulty;var bot=new GBBot();bool anticipates=bot.Tick(app,b,a).Has(GBInput.Sprawl),sawShot=false,response=false;float observed=-1,reacted=-1,start=Time.realtimeSinceStartup,end=start+.8f;
            while(Time.realtimeSinceStartup<end){
                float now=Time.realtimeSinceStartup;app.ScriptedInputs[0]=Command(Vector3.right,now-start<.1f?GBInput.Strike:0);
                if(a.Shooting){sawShot=true;if(observed<0)observed=now;}
                GBInput input=bot.Tick(app,b,a);app.ScriptedInputs[1]=input;
                if(input.Has(GBInput.Sprawl)){response=true;if(reacted<0)reacted=now;}
                integrity.Measure(app);yield return null;
            }
            check("AI_"+difficulty+"_reacts_to_observed_shot_with_sprawl",!anticipates&&sawShot&&response&&observed>=0&&reacted>observed&&reacted-observed<.55f,"difficulty="+difficulty+" before-shot sprawl="+anticipates+" observed="+sawShot+" reaction="+(reacted-observed).ToString("0.000")+"s intent="+bot.Intent);
        }
        app.AIDifficulty=1;
        check("sprawl_bodies_connected_finite",integrity.finite&&integrity.missing==0&&integrity.gap<.06f,"all posture/arms/recovery/shots/AI max joint gap="+integrity.gap.ToString("0.0000")+"m missing="+integrity.missing);
        check("sprawl_no_runtime_errors",app.Issues.Count==0,string.Join("; ",app.Issues));
    }

    static IEnumerator RunningShot(GBApp app,Integrity integrity,bool sprawl,Shot result){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(-3,.28f,-1));b.ResetTo(new Vector3(.75f,.28f,-1));
        app.ScriptedInputs[0]=Command(Vector3.right);app.ScriptedInputs[1]=Command(Vector3.left,sprawl?GBInput.Sprawl:0);yield return Wait(app,integrity,1.3f);
        app.ScriptedInputs[0]=Command(Vector3.right,GBInput.Sprint,default,Vector3.right);float approachEnd=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<approachEnd&&b.Root.position.x-a.Root.position.x>1.5f){integrity.Measure(app);yield return null;}
        result.approach=Vector3.Dot(Velocity(a)-Velocity(b),Vector3.right);Vector3 position=b.Root.position;float balance=b.balance,start=Time.realtimeSinceStartup,end=start+.85f;
        while(Time.realtimeSinceStartup<end){
            float now=Time.realtimeSinceStartup;app.ScriptedInputs[0]=Command(Vector3.right,now-start<.1f?GBInput.Strike:0);
            result.shooting|=a.Shooting;result.sprawling|=b.Sprawling;result.peak=Mathf.Max(result.peak,Vector3.ProjectOnPlane(Velocity(b),Vector3.up).magnitude);result.travel=Mathf.Max(result.travel,b.Root.position.x-position.x);result.balanceLoss=Mathf.Max(result.balanceLoss,balance-b.balance);result.contact=Mathf.Max(result.contact,a.LastShotApproachSpeed);result.hands=Mathf.Max(result.hands,Mathf.Max(a.GripCount,b.GripCount));integrity.Measure(app);yield return null;
        }
    }
}
