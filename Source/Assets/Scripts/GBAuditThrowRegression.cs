using UnityEngine;
using System;
using System.Collections;
using System.IO;

public partial class GBAudit {
    IEnumerator ThrowRegressionRun(){
        app.LocalDevice=0;app.TrainingPartner=0;app.BeginMatch(3,2);Neutral();yield return Wait(1);
        Vector3[] aims={Vector3.right,Vector3.left,Vector3.forward,Vector3.back,new Vector3(.8f,0,.6f),new Vector3(-.6f,0,.8f)};
        float[] liftTimes={.75f,1f,1.25f,.85f,1.1f,1.2f};bool allIntact=true;
        Func<GBWrestler,Vector3> velocity=w=>{Vector3 momentum=Vector3.zero;float mass=0;foreach(var part in w.bodies){momentum+=part.linearVelocity*part.mass;mass+=part.mass;}return momentum/Mathf.Max(1,mass);};
        Func<GBWrestler,GBWrestler,int> victimHands=(holder,victim)=>{int count=0;foreach(var hand in holder.hands)if(hand.joint&&hand.target&&hand.target.wrestler==victim)count++;return count;};
        for(int attempt=0;attempt<aims.Length;attempt++){
            var a=app.Wrestlers[0];var b=app.Wrestlers[1];Vector3 aim=aims[attempt],center=new Vector3(0,.25f,-1);string prefix="throw_repeat_"+(attempt+1);
            // Reset the whole anatomical fixture; acquisition itself uses only
            // player aim and held hand buttons, never Attach or injected joints.
            Neutral();a.ResetTo(center-aim*.5f);b.ResetTo(center+aim*.5f);
            app.ScriptedInputs[0]=new GBInput{ax=aim.x,az=aim.z};app.ScriptedInputs[1]=new GBInput{ax=-aim.x,az=-aim.z};yield return Wait(.8f);
            app.ScriptedInputs[0]=new GBInput{ax=aim.x,az=aim.z,flags=GBInput.Left|GBInput.Right};
            float acquireEnd=Time.realtimeSinceStartup+3;int peakHands=0;
            while(Time.realtimeSinceStartup<acquireEnd){peakHands=Mathf.Max(peakHands,victimHands(a,b));allIntact&=Finite()&&a.JointSeparation()<.06f&&b.JointSeparation()<.06f;if(victimHands(a,b)==2)break;yield return null;}
            yield return Wait(.12f+attempt*.035f);
            Check(prefix+"_natural_two_hand_acquisition",peakHands==2,"natural peak="+peakHands+" retained="+victimHands(a,b)+" aim="+aim+" left="+a.hands[0].Label+" right="+a.hands[1].Label);
            float beforeLift=b.Root.position.y,maximumHeight=beforeLift,liftEnd=Time.realtimeSinceStartup+liftTimes[attempt];
            app.ScriptedInputs[0]=new GBInput{ax=aim.x,az=aim.z,flags=GBInput.Left|GBInput.Right|GBInput.Lift};
            while(Time.realtimeSinceStartup<liftEnd){maximumHeight=Mathf.Max(maximumHeight,b.Root.position.y);allIntact&=Finite()&&a.JointSeparation()<.06f&&b.JointSeparation()<.06f;yield return null;}
            float rise=maximumHeight-beforeLift;Check(prefix+"_physical_lift",rise>.15f,"lift seconds="+liftTimes[attempt]+" start y="+beforeLift.ToString("0.000")+" max y="+maximumHeight.ToString("0.000")+" rise="+rise.ToString("0.000")+" feedback="+a.ActionFeedback);
            int handsBeforeThrow=victimHands(a,b);Vector3 start=b.Root.position,velocityBefore=velocity(b);float staminaBefore=a.stamina;
            Check(prefix+"_committed_throw_setup",handsBeforeThrow==2,"manual victim grips="+handsBeforeThrow+" pelvis="+start+" average victim velocity="+velocityBefore+" stamina="+staminaBefore.ToString("0.0"));
            app.Capture(Path.Combine(directory,prefix+"-before.png"));
            float throwEnd=Time.realtimeSinceStartup+1;
            int throwFlags=GBInput.Left|GBInput.Right|GBInput.Throw|(attempt>=3?GBInput.Lift:0);
            app.ScriptedInputs[0]=new GBInput{ax=aim.x,az=aim.z,flags=throwFlags};yield return Wait(.12f);
            int handsAfterThrow=a.GripCount;Vector3 launchVelocity=velocity(b);string throwFeedback=a.ActionFeedback;
            app.ScriptedInputs[0]=new GBInput{ax=aim.x,az=aim.z};
            while(Time.realtimeSinceStartup<throwEnd){allIntact&=Finite()&&a.JointSeparation()<.06f&&b.JointSeparation()<.06f;yield return null;}
            float displacement=Vector3.Dot(b.Root.position-start,aim);
            Check(prefix+"_release",handsBeforeThrow>=1&&handsAfterThrow==0,"lift "+(attempt>=3?"held":"released")+" before="+handsBeforeThrow+" after="+handsAfterThrow+" feedback="+throwFeedback);
            Check(prefix+"_forward_displacement",handsBeforeThrow==2&&displacement>.4f,"projected forward="+displacement.ToString("0.000")+" aim="+aim+" root start="+start+" root end="+b.Root.position+" velocity before="+velocityBefore+" launch="+launchVelocity+" final="+velocity(b)+" feedback="+throwFeedback);
            app.Capture(Path.Combine(directory,prefix+(handsBeforeThrow==2&&displacement>.4f?"-success.png":"-failure.png")));
        }
        Neutral();Check("throw_regression_connected_finite",allIntact&&Finite()&&maximumJointGap<.06f&&missingJointFrames==0,"six natural-grip lifts/throws; maximum anatomical gap="+maximumJointGap.ToString("0.0000")+" missing joint frames="+missingJointFrames);
        Check("throw_regression_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
