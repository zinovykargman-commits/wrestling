using UnityEngine;
using System;
using System.IO;
using System.Collections;

// Measures physical outcomes through normal player commands. This audit never
// installs a grip, invokes an attack directly, or injects a momentum impulse.
public static class GBAuditMomentum {
    class Integrity {
        public bool finite=true;public float gap,maxSpeed,maxEnergy;public int missing;
        public void Measure(GBApp app){
            foreach(var w in app.Wrestlers){
                float separation=w.JointSeparation();if(float.IsInfinity(separation))missing++;else gap=Mathf.Max(gap,separation);
                float energy=0;foreach(var b in w.bodies){
                    if(!b){finite=false;continue;}Vector3 p=b.position,v=b.linearVelocity,angular=b.angularVelocity;Quaternion q=b.rotation;
                    finite&=Valid(p.x)&&Valid(p.y)&&Valid(p.z)&&Valid(v.x)&&Valid(v.y)&&Valid(v.z)&&Valid(angular.x)&&Valid(angular.y)&&Valid(angular.z)&&Valid(q.x)&&Valid(q.y)&&Valid(q.z)&&Valid(q.w);
                    maxSpeed=Mathf.Max(maxSpeed,v.magnitude);energy+=.5f*b.mass*v.sqrMagnitude;
                }maxEnergy=Mathf.Max(maxEnergy,energy);
            }
        }
        static bool Valid(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
    class ShotSample {public float approach,peakSpeed,displacement,balanceLoss,contactApproach,impactPressure,physicalImpulse;public int hands,impacts;public bool shooting;public string feedback;}
    class JumpSample {public float approach,peakSpeed,displacement,balanceLoss,physicalImpulse,impactApproach;public int impacts,hands;public bool airborne,funded;public string before="",after="";}
    class SpinSample {public float angularSpeed,gripGain,gripDelay,spinAtGrip,transferImpulse;public int hands;}
    static Vector3 Velocity(GBWrestler w){Vector3 momentum=Vector3.zero;float mass=0;foreach(var b in w.bodies){momentum+=b.linearVelocity*b.mass;mass+=b.mass;}return momentum/Mathf.Max(1,mass);}
    static float HorizontalSpeed(GBWrestler w){return Vector3.ProjectOnPlane(Velocity(w),Vector3.up).magnitude;}
    static float KineticEnergy(GBWrestler w){float energy=0;foreach(var body in w.bodies){Vector3 omega=Quaternion.Inverse(body.rotation*body.inertiaTensorRotation)*body.angularVelocity;energy+=.5f*(body.mass*body.linearVelocity.sqrMagnitude+Vector3.Dot(omega,Vector3.Scale(body.inertiaTensor,omega)));}return energy;}
    static int VictimGrips(GBWrestler holder,GBWrestler victim){int count=0;foreach(var h in holder.hands)if(h.joint&&h.target&&h.target.wrestler==victim)count++;return count;}
    static void Start(GBApp app){app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);Aim(app);}
    static void Aim(GBApp app,int flags=0,float move=0){app.ScriptedInputs[0]=new GBInput{ax=1,x=move,flags=flags};app.ScriptedInputs[1]=new GBInput{ax=-1};}
    static IEnumerator Observe(GBApp app,Integrity integrity,float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end){integrity.Measure(app);yield return null;}}

    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.LocalDevice=0;app.TrainingPartner=0;app.ScriptedInputs=new GBInput[2];var integrity=new Integrity();
        var slow=new ShotSample();yield return Shot(app,integrity,directory,"stationary",false,0,slow);
        var fast=new ShotSample();yield return Shot(app,integrity,directory,"running",true,0,fast);
        var brace=new ShotSample();yield return Shot(app,integrity,directory,"running-braced",true,GBInput.Brace,brace);
        var sprawl=new ShotSample();yield return Shot(app,integrity,directory,"running-sprawled",true,GBInput.Sprawl,sprawl);
        check("momentum_shot_actual_runup",slow.approach<.8f&&fast.approach>slow.approach+1.5f,"standing approach="+slow.approach.ToString("0.00")+" running="+fast.approach.ToString("0.00")+" m/s from physical massweighted velocities");
        check("momentum_shots_contact",slow.impacts==1&&fast.impacts==1&&slow.physicalImpulse>.3f&&fast.physicalImpulse>.3f&&slow.balanceLoss>1&&fast.balanceLoss>1&&slow.peakSpeed>.25f&&fast.peakSpeed>.25f,"standing/running balance loss="+slow.balanceLoss.ToString("0.0")+"/"+fast.balanceLoss.ToString("0.0")+" physical collision impulse="+slow.physicalImpulse.ToString("0.0")+"/"+fast.physicalImpulse.ToString("0.0")+" Ns; target peak m/s="+slow.peakSpeed.ToString("0.00")+"/"+fast.peakSpeed.ToString("0.00"));
        check("fast_shot_has_stronger_physical_result",fast.peakSpeed>slow.peakSpeed+.25f&&fast.displacement>slow.displacement+.1f,"standing/running target peak="+slow.peakSpeed.ToString("0.00")+"/"+fast.peakSpeed.ToString("0.00")+" m/s; forward travel="+slow.displacement.ToString("0.00")+"/"+fast.displacement.ToString("0.00")+" m; contact approach="+slow.contactApproach.ToString("0.00")+"/"+fast.contactApproach.ToString("0.00")+" m/s impact pressure="+slow.impactPressure.ToString("0.000")+"/"+fast.impactPressure.ToString("0.000")+" s; impact should carry into readable movement before recovery");
        check("momentum_q_never_auto_grabs",slow.shooting&&fast.shooting&&brace.shooting&&sprawl.shooting&&slow.hands==0&&fast.hands==0&&brace.hands==0&&sprawl.hands==0,"maximum hands standing/running/braced="+slow.hands+"/"+fast.hands+"/"+brace.hands+"; Q supplied without either hand button");
        check("brace_reduces_running_shot",brace.peakSpeed<fast.peakSpeed*.9f||brace.displacement<fast.displacement-.15f,"unbraced/braced target peak="+fast.peakSpeed.ToString("0.00")+"/"+brace.peakSpeed.ToString("0.00")+" forward="+fast.displacement.ToString("0.00")+"/"+brace.displacement.ToString("0.00")+" feedback="+brace.feedback);

        check("sprawl_reduces_running_shot",sprawl.balanceLoss<fast.balanceLoss*.75f&&sprawl.impactPressure<fast.impactPressure*.6f,"unbraced/sprawled balance loss="+fast.balanceLoss.ToString("0.0")+"/"+sprawl.balanceLoss.ToString("0.0")+" pressure="+fast.impactPressure.ToString("0.00")+"/"+sprawl.impactPressure.ToString("0.00")+" s; target peak="+fast.peakSpeed.ToString("0.00")+"/"+sprawl.peakSpeed.ToString("0.00"));
        check("shot_contact_budget_is_one",slow.impacts==1&&fast.impacts==1&&brace.impacts<=1&&sprawl.impacts<=1,"paid Q impact counts standing/running/braced/sprawled="+slow.impacts+"/"+fast.impacts+"/"+brace.impacts+"/"+sprawl.impacts+"; no duplicate per collider or stay callback");
        var touch=new JumpSample();yield return JumpImpact(app,integrity,directory,"stationary-touch",false,false,touch);
        var jump=new JumpSample();yield return JumpImpact(app,integrity,directory,"forward-jump",true,false,jump);
        var jumpBrace=new JumpSample();yield return JumpImpact(app,integrity,directory,"braced-forward-jump",true,true,jumpBrace);
        check("forward_jump_has_earned_collision",jump.airborne&&jump.impacts==1&&jump.physicalImpulse>1&&jump.impactApproach>=1.8f,"paid normal jump airborne="+jump.airborne+" actual core collision count="+jump.impacts+" impulse="+jump.physicalImpulse.ToString("0.0")+" Ns; approach="+jump.impactApproach.ToString("0.00")+" m/s; launch diagnostics "+jump.before+" ; "+jump.after);
        check("jump_momentum_beats_stationary_touch",jump.peakSpeed>touch.peakSpeed+.3f&&jump.displacement>touch.displacement+.1f&&jump.balanceLoss>touch.balanceLoss+4,"stationary/jump opponent peak="+touch.peakSpeed.ToString("0.00")+"/"+jump.peakSpeed.ToString("0.00")+" m/s travel="+touch.displacement.ToString("0.00")+"/"+jump.displacement.ToString("0.00")+" m balance loss="+touch.balanceLoss.ToString("0.0")+"/"+jump.balanceLoss.ToString("0.0"));
        check("brace_mitigates_jump_body_impact",jumpBrace.balanceLoss<jump.balanceLoss*.8f||jumpBrace.displacement<jump.displacement*.8f,"neutral/braced loss="+jump.balanceLoss.ToString("0.0")+"/"+jumpBrace.balanceLoss.ToString("0.0")+" travel="+jump.displacement.ToString("0.00")+"/"+jumpBrace.displacement.ToString("0.00"));
        check("jump_one_contact_budget_no_auto_grab",touch.impacts==0&&jump.impacts==1&&jumpBrace.impacts<=1&&touch.hands==0&&jump.hands==0&&jumpBrace.hands==0,"normal Jump held after initial edge: impacts="+touch.impacts+"/"+jump.impacts+"/"+jumpBrace.impacts+" hands="+touch.hands+"/"+jump.hands+"/"+jumpBrace.hands+"; no attack injection or impulse injection");

        var still=new SpinSample();yield return SpinGrab(app,integrity,directory,false,still);
        var spin=new SpinSample();yield return SpinGrab(app,integrity,directory,true,spin);
        check("hard_spin_is_physical_rotation",spin.angularSpeed>2f&&spin.angularSpeed>still.angularSpeed+1f,"standing/spinning root yaw peak="+still.angularSpeed.ToString("0.00")+"/"+spin.angularSpeed.ToString("0.00")+" rad/s from Rigidbody.angularVelocity");
        check("spin_manual_grips_naturally_acquired",still.hands>0&&spin.hands>0,"standing/spinning natural hands="+still.hands+"/"+spin.hands+" reach delay="+still.gripDelay.ToString("0.00")+"/"+spin.gripDelay.ToString("0.00")+" seconds; no Attach calls");
        check("recent_spin_carries_grabbed_opponent",spin.gripGain>still.gripGain+.15f,"standing/spinning target horizontal speed gain during matched one-hand grip="+still.gripGain.ToString("0.00")+"/"+spin.gripGain.ToString("0.00")+" m/s; recent physical spin="+spin.spinAtGrip.ToString("0.00")+" Nms transfer="+spin.transferImpulse.ToString("0.00")+" Ns");
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(-.5f,.25f,-2));b.ResetTo(new Vector3(.5f,.25f,-2));yield return Observe(app,integrity,.8f);
        int repeatedAcquisitions=0,previousHands=0;float repeatEnd=Time.realtimeSinceStartup+18,start=Time.realtimeSinceStartup,repeatEnergy=0,repeatSpeed=0,peakAt=0,maxCom=0,maxHorizontalCom=0,comPeakAt=0,energyPeakAt=0,firstIntegral=0,lastIntegral=0,firstDuration=0,lastDuration=0,firstPeak=0,lastPeak=0,lastFrame=start;string peakPart="",comPeakWrestler="",energyPeakPositions="";Vector3 comPeakPosition=Vector3.zero,comPeakVelocity=Vector3.zero;bool withinRing=true;
        while(Time.realtimeSinceStartup<repeatEnd){
            float phase=(Time.realtimeSinceStartup-start)%1.5f,angle=phase<.65f?phase/.65f*Mathf.PI*2:0;
            Vector3 delta=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up),toward=delta.sqrMagnitude>.01f?delta.normalized:Vector3.right;
            Vector3 aim=Quaternion.AngleAxis(-angle*Mathf.Rad2Deg,Vector3.up)*toward,move=delta.magnitude>1.2f?toward*.15f:Vector3.zero;
            // Approach only when out of reach. Constant drift would move this
            // isolated grip test off the raised mat into ropes/ledge/fall forces.
            app.ScriptedInputs[0]=new GBInput{x=move.x,z=move.z,ax=aim.x,az=aim.z,flags=phase>=.65f&&phase<1.27f?GBInput.Left|GBInput.Right:0};app.ScriptedInputs[1]=new GBInput{ax=-toward.x,az=-toward.z};
            int hands=VictimGrips(a,b);if(hands>0&&previousHands==0)repeatedAcquisitions++;previousHands=hands;
            float energy=KineticEnergy(a)+KineticEnergy(b),now=Time.realtimeSinceStartup,elapsed=now-start,dt=Mathf.Min(.1f,now-lastFrame);lastFrame=now;
            foreach(var w in new[]{a,b}){
                Vector3 comVelocity=Velocity(w);if(comVelocity.magnitude>maxCom){maxCom=comVelocity.magnitude;comPeakAt=elapsed;comPeakPosition=w.Root.position;comPeakVelocity=comVelocity;comPeakWrestler=w.playerName;}
                maxHorizontalCom=Mathf.Max(maxHorizontalCom,Vector3.ProjectOnPlane(comVelocity,Vector3.up).magnitude);withinRing&=Mathf.Abs(w.Root.position.x)<3.8f&&Mathf.Abs(w.Root.position.z)<3.8f&&w.Root.position.y>.1f;
                foreach(var part in w.bodies){float speed=part.linearVelocity.magnitude;if(speed>repeatSpeed){repeatSpeed=speed;peakAt=elapsed;peakPart=w.playerName+" "+part.name;}}
            }
            if(energy>repeatEnergy){repeatEnergy=energy;energyPeakAt=elapsed;energyPeakPositions="attacker="+a.Root.position+" defender="+b.Root.position;}
            if(elapsed<3){firstIntegral+=energy*dt;firstDuration+=dt;firstPeak=Mathf.Max(firstPeak,energy);}else if(elapsed>=15){lastIntegral+=energy*dt;lastDuration+=dt;lastPeak=Mathf.Max(lastPeak,energy);}
            integrity.Measure(app);yield return null;
        }
        check("repeated_grip_fixture_reacquires",repeatedAcquisitions>=3,"natural spin/release/regrab transitions="+repeatedAcquisitions+" across twelve cycles / 18 seconds");
        check("repeated_grip_fixture_stays_on_mat",withinRing,"root positions remained within±3.8m and above0.1m; ordinary approach only while separation>1.2m; final attacker="+a.Root.position+" defender="+b.Root.position);
        float firstAverage=firstIntegral/Mathf.Max(.01f,firstDuration),lastAverage=lastIntegral/Mathf.Max(.01f,lastDuration);
        // A light hand's instantaneous orbital/contact-correction speed is not
        // whole-wrestler momentum. Compare complete two-cycle windows including
        // rotational energy; require bounded COM speed and no growing reservoir.
        bool bounded=firstDuration>2.5f&&lastDuration>2.5f&&maxCom<8&&repeatEnergy<6500&&lastAverage<firstAverage*2.5f+250&&lastPeak<firstPeak*2.5f+500;
        check("repeated_grips_keep_energy_bounded",repeatedAcquisitions>=3&&bounded,"max massweighted COM="+maxCom.ToString("0.00")+" horizontal="+maxHorizontalCom.ToString("0.00")+" m/s; peak COM wrestler="+comPeakWrestler+" root="+comPeakPosition+" velocity="+comPeakVelocity+" time="+comPeakAt.ToString("0.000")+" s; total kinetic energy peak="+repeatEnergy.ToString("0.0")+" J time="+energyPeakAt.ToString("0.000")+" s "+energyPeakPositions+"; first/last 3s average="+firstAverage.ToString("0.0")+"/"+lastAverage.ToString("0.0")+" peak="+firstPeak.ToString("0.0")+"/"+lastPeak.ToString("0.0")+" J; limb peak diagnostic="+repeatSpeed.ToString("0.00")+" m/s part="+peakPart+" time="+peakAt.ToString("0.000")+" s");
        app.Capture(Path.Combine(directory,"momentum-repeated-grips.png"));Aim(app);yield return Observe(app,integrity,3);
        float settledEnergy=KineticEnergy(a)+KineticEnergy(b),settledCom=Mathf.Max(Velocity(a).magnitude,Velocity(b).magnitude);
        check("repeated_grip_momentum_settles_when_released",a.GripCount==0&&b.GripCount==0&&settledCom<1&&settledEnergy<Mathf.Max(150,lastAverage*.75f),"after three seconds with all actions released: max COM="+settledCom.ToString("0.00")+" m/s energy="+settledEnergy.ToString("0.0")+" J vs active late-window average="+lastAverage.ToString("0.0")+" J");
        check("momentum_bodies_connected_finite",integrity.finite&&integrity.gap<.06f&&integrity.missing==0,"all shots, forward jumps, spins and grips; max joint gap="+integrity.gap.ToString("0.0000")+" m missing="+integrity.missing+" maximum body speed="+integrity.maxSpeed.ToString("0.00"));
        check("momentum_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }

    static IEnumerator Shot(GBApp app,Integrity integrity,string directory,string name,bool running,int defense,ShotSample sample){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(running?-3:-.75f,.25f,-1));b.ResetTo(new Vector3(.75f,.25f,-1));
        if(defense!=0)app.ScriptedInputs[1]=new GBInput{ax=-1,flags=defense};yield return Observe(app,integrity,.8f);
        if(running){
            app.ScriptedInputs[0]=new GBInput{ax=1,x=1,flags=GBInput.Sprint};float end=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<end&&b.Root.position.x-a.Root.position.x>1.5f){integrity.Measure(app);yield return null;}
        }
        sample.approach=Vector3.Dot(Velocity(a)-Velocity(b),Vector3.right);Vector3 initial=b.Root.position;float initialBalance=b.balance,endShot=Time.realtimeSinceStartup+1.05f;
        app.ScriptedInputs[0]=new GBInput{ax=1,flags=GBInput.Strike};float buttonEnd=Time.realtimeSinceStartup+.1f;
        while(Time.realtimeSinceStartup<endShot){
            if(Time.realtimeSinceStartup>=buttonEnd)app.ScriptedInputs[0]=new GBInput{ax=1};
            sample.peakSpeed=Mathf.Max(sample.peakSpeed,HorizontalSpeed(b));sample.displacement=Mathf.Max(sample.displacement,b.Root.position.x-initial.x);sample.balanceLoss=Mathf.Max(sample.balanceLoss,initialBalance-b.balance);sample.hands=Mathf.Max(sample.hands,a.GripCount);sample.shooting|=a.Shooting;
            sample.impacts=a.ShotImpactCount;sample.physicalImpulse=Mathf.Max(sample.physicalImpulse,a.LastShotPhysicalImpulse);sample.contactApproach=Mathf.Max(sample.contactApproach,a.LastShotApproachSpeed);sample.impactPressure=Mathf.Max(sample.impactPressure,b.pressureUntil-Time.time);
            if(!string.IsNullOrEmpty(a.ActionFeedback))sample.feedback=a.ActionFeedback;integrity.Measure(app);yield return null;
        }
        app.Capture(Path.Combine(directory,"momentum-shot-"+name+".png"));
    }

    static IEnumerator JumpImpact(GBApp app,Integrity integrity,string directory,string name,bool jumping,bool braced,JumpSample sample){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(jumping?-3:-.55f,.25f,-1));b.ResetTo(new Vector3(.55f,.25f,-1));
        if(braced)app.ScriptedInputs[1]=new GBInput{ax=-1,flags=GBInput.Brace};yield return Observe(app,integrity,.8f);
        if(jumping){app.ScriptedInputs[0]=new GBInput{ax=1,x=1,flags=GBInput.Sprint};float approachEnd=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<approachEnd&&b.Root.position.x-a.Root.position.x>1.4f){integrity.Measure(app);yield return null;}}
        sample.approach=Vector3.Dot(Velocity(a)-Velocity(b),Vector3.right);Vector3 start=b.Root.position;float balance=b.balance,end=Time.realtimeSinceStartup+1.8f,moveEnd=Time.realtimeSinceStartup+.20f;
        sample.before="before Space stamina="+a.stamina.ToString("0.00")+" grounded="+a.grounded+" Down="+a.Down+" pinControlled="+a.PinControlled+" jumpWindow="+a.JumpFlightWindow+" gap="+(b.Root.position.x-a.Root.position.x).ToString("0.00")+" approach="+sample.approach.ToString("0.00")+" feet="+a.bodies[11].position.y.ToString("0.00")+"/"+a.bodies[14].position.y.ToString("0.00");
        app.ScriptedInputs[0]=new GBInput{ax=1,x=jumping?1:0,flags=jumping?GBInput.Jump:0};
        while(Time.realtimeSinceStartup<end){
            if(Time.realtimeSinceStartup>=moveEnd)app.ScriptedInputs[0]=new GBInput{ax=1,flags=jumping?GBInput.Jump:0};
            sample.funded|=a.JumpFlightWindow;
            if(string.IsNullOrEmpty(sample.after)&&a.command.Has(GBInput.Jump))sample.after="first processed Space stamina="+a.stamina.ToString("0.00")+" grounded="+a.grounded+" Down="+a.Down+" window="+a.JumpFlightWindow+" velocity="+Velocity(a).ToString("F2")+" inputflags="+a.command.flags;
            sample.airborne|=!a.grounded&&Velocity(a).y>.4f;sample.impacts=a.BodyImpactCount;sample.physicalImpulse=Mathf.Max(sample.physicalImpulse,a.LastBodyPhysicalImpulse);sample.impactApproach=Mathf.Max(sample.impactApproach,a.LastBodyImpactApproachSpeed);
            sample.peakSpeed=Mathf.Max(sample.peakSpeed,HorizontalSpeed(b));sample.displacement=Mathf.Max(sample.displacement,b.Root.position.x-start.x);sample.balanceLoss=Mathf.Max(sample.balanceLoss,balance-b.balance);sample.hands=Mathf.Max(sample.hands,a.GripCount);integrity.Measure(app);yield return null;
        }app.Capture(Path.Combine(directory,"momentum-"+name+".png"));
    }

    static IEnumerator SpinGrab(GBApp app,Integrity integrity,string directory,bool spinning,SpinSample sample){
        Start(app);var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.ResetTo(new Vector3(-.5f,.25f,-1));b.ResetTo(new Vector3(.5f,.25f,-1));yield return Observe(app,integrity,.8f);
        float spinStart=Time.realtimeSinceStartup,spinEnd=spinStart+.7f;
        while(Time.realtimeSinceStartup<spinEnd){
            float angle=spinning?(Time.realtimeSinceStartup-spinStart)/.7f*Mathf.PI*2:0;app.ScriptedInputs[0]=new GBInput{ax=Mathf.Cos(angle),az=Mathf.Sin(angle)};
            sample.angularSpeed=Mathf.Max(sample.angularSpeed,Mathf.Abs(a.Root.angularVelocity.y));integrity.Measure(app);yield return null;
        }
        float beforeGripSpeed=HorizontalSpeed(b),reachStart=Time.realtimeSinceStartup,reachEnd=reachStart+2.6f;sample.spinAtGrip=a.RecentSpinMomentum;Aim(app,GBInput.Left);
        while(Time.realtimeSinceStartup<reachEnd&&VictimGrips(a,b)==0){integrity.Measure(app);yield return null;}
        sample.hands=VictimGrips(a,b);sample.gripDelay=Time.realtimeSinceStartup-reachStart;sample.transferImpulse=a.LastGrabMomentumImpulse;float holdEnd=Time.realtimeSinceStartup+.2f;
        while(Time.realtimeSinceStartup<holdEnd){sample.gripGain=Mathf.Max(sample.gripGain,HorizontalSpeed(b)-beforeGripSpeed);integrity.Measure(app);yield return null;}
        app.Capture(Path.Combine(directory,"momentum-"+(spinning?"spin":"stationary")+"-grab-carry.png"));
    }
}

