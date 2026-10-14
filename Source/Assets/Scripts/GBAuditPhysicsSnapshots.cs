using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

// Differential checks execute the actual production helpers against the frozen
// 0.4.11 algorithms, using native Rigidbody/Collider state. No game actor is
// repositioned, frozen or given an additional force by this fixture.
public partial class GBAudit {
    [Serializable] class SnapshotCpuTiming {
        public string scope="Paired local helper CPU calls on identical native states; actor/state writes excluded; alternating AB/BA; not whole-game FPS";
        public int seed,cases,pairedCallsPerHelper,preexistingNonzeroAccumulatorCases,preexistingNonfiniteAccumulatorCases,changedAccumulatorCases;
        public double referenceSpringMs,candidateSpringMs,referenceElbowMs,candidateElbowMs,springOldOverNew,elbowOldOverNew;
        public string[] accumulatorExamples;
        public Vector3 resultChecksum;
    }
    static bool SnapshotFinite(Vector3 value){return !float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);}
    static long SnapshotTimedCall(Func<Vector3> operation,ref Vector3 checksum){
        long begin=Stopwatch.GetTimestamp();Vector3 result=operation();long ticks=Stopwatch.GetTimestamp()-begin;
        checksum+=result;return ticks;
    }
    static Vector3 ReferencePointMass(Rigidbody body,Vector3 point,Vector3 force){
        if(body.isKinematic)return Vector3.zero;
        Vector3 lever=point-body.worldCenterOfMass;
        Quaternion principal=body.rotation*body.inertiaTensorRotation;
        Vector3 localTorque=Quaternion.Inverse(principal)*Vector3.Cross(lever,force),tensor=body.inertiaTensor;
        Vector3 localAcceleration=new Vector3(tensor.x>0?localTorque.x/tensor.x:0,tensor.y>0?localTorque.y/tensor.y:0,tensor.z>0?localTorque.z/tensor.z:0);
        return force/Mathf.Max(.001f,body.mass)+Vector3.Cross(principal*localAcceleration,lever);
    }
    static Vector3 ReferenceSpring(Rigidbody hand,Rigidbody target,Vector3 a,Vector3 b,Vector3 error,Vector3 relative,float dt,float spring,float damping){
        float softness=damping*dt+spring*dt*dt;
        Vector3 x=Vector3.right+softness*(ReferencePointMass(hand,a,Vector3.right)+ReferencePointMass(target,b,Vector3.right));
        Vector3 y=Vector3.up+softness*(ReferencePointMass(hand,a,Vector3.up)+ReferencePointMass(target,b,Vector3.up));
        Vector3 z=Vector3.forward+softness*(ReferencePointMass(hand,a,Vector3.forward)+ReferencePointMass(target,b,Vector3.forward));
        Vector3 rhs=error*spring+relative*(damping+spring*dt);
        float determinant=Vector3.Dot(x,Vector3.Cross(y,z));
        if(determinant<=.000001f)return Vector3.zero;
        return new Vector3(Vector3.Dot(rhs,Vector3.Cross(y,z)),Vector3.Dot(rhs,Vector3.Cross(z,x)),Vector3.Dot(rhs,Vector3.Cross(x,y)))/determinant;
    }
    static Vector3 ReferenceElbow(Rigidbody chest,CapsuleCollider capsule,CapsuleCollider rival,bool covering,Vector3 direction,Vector3 bend,Vector3 circle,float bendRadius,Vector3 end,Vector3 previous,Vector3 initialElbow){
        float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
        Vector3 bestElbow=initialElbow;float bestScore=float.NegativeInfinity;
        for(int sample=0;sample<24;sample++){
            Vector3 pole=Quaternion.AngleAxis(sample*15,direction)*bend;
            Vector3 candidate=circle+pole*bendRadius;
            float clearance=float.PositiveInfinity;
            for(int endpoint=0;endpoint<3;endpoint++){
                Vector3 point=Vector3.Lerp(candidate,end,.24f+endpoint*.16f);
                Vector3 local=Quaternion.Inverse(chest.rotation)*(point-chest.position)-capsule.center;
                Vector3 core=Vector3.up*Mathf.Clamp(local.y,-half,half);
                clearance=Mathf.Min(clearance,(local-core).magnitude-capsule.radius-.11f);
                if(rival){
                    Vector3 rivalLocal=rival.transform.InverseTransformPoint(point)-rival.center;
                    Vector3 axis=rival.direction==0?Vector3.right:rival.direction==1?Vector3.up:Vector3.forward;
                    float rivalHalf=Mathf.Max(0,rival.height*.5f-rival.radius);
                    Vector3 rivalCore=axis*Mathf.Clamp(Vector3.Dot(rivalLocal,axis),-rivalHalf,rivalHalf);
                    clearance=Mathf.Min(clearance,(rivalLocal-rivalCore).magnitude-rival.radius-.11f);
                }
            }
            float score=(covering?clearance:Mathf.Min(clearance,.025f))+Vector3.Dot(pole,bend)*(covering?.035f:.075f)+Vector3.Dot(pole,previous)*.025f;
            if(score>bestScore){bestScore=score;bestElbow=candidate;}
        }
        return bestElbow;
    }
    static float SnapshotFloat(System.Random random,float minimum,float maximum){return minimum+(float)random.NextDouble()*(maximum-minimum);}
    static Vector3 SnapshotVector(System.Random random,float range){return new Vector3(SnapshotFloat(random,-range,range),SnapshotFloat(random,-range,range),SnapshotFloat(random,-range,range));}
    static void SnapshotBodyState(Rigidbody body,CapsuleCollider capsule,System.Random random,int index){
        capsule.radius=SnapshotFloat(random,.10f,.35f);capsule.height=capsule.radius*2+SnapshotFloat(random,.01f,.7f);capsule.center=SnapshotVector(random,.2f);
        body.position=SnapshotVector(random,3);body.rotation=Quaternion.Euler(SnapshotVector(random,180));body.mass=SnapshotFloat(random,.2f,35);
        body.centerOfMass=SnapshotVector(random,.15f);body.inertiaTensorRotation=Quaternion.Euler(SnapshotVector(random,180));
        body.inertiaTensor=new Vector3(index%17==0?0:SnapshotFloat(random,.005f,8),index%19==0?0:SnapshotFloat(random,.005f,8),index%23==0?0:SnapshotFloat(random,.005f,8));
        body.isKinematic=index%5==0;
    }
    IEnumerator PhysicsSnapshotsRun(){
        const int seed=187012,cases=400;
        int registryBefore=app.Registry.Count;
        var firstObject=new GameObject("Snapshot differential hand fixture");var secondObject=new GameObject("Snapshot differential target fixture");
        var firstCapsule=firstObject.AddComponent<CapsuleCollider>();var secondCapsule=secondObject.AddComponent<CapsuleCollider>();firstCapsule.isTrigger=secondCapsule.isTrigger=true;
        var first=firstObject.AddComponent<Rigidbody>();var second=secondObject.AddComponent<Rigidbody>();first.useGravity=second.useGravity=false;
        var random=new System.Random(seed);float maximumForceError=0,maximumElbowError=0;int forceDifferences=0,elbowDifferences=0;bool noAddedForce=true;
        var cpu=new SnapshotCpuTiming{seed=seed,cases=cases};var accumulatorExamples=new List<string>();
        long referenceSpringTicks=0,candidateSpringTicks=0,referenceElbowTicks=0,candidateElbowTicks=0;Vector3 timingChecksum=Vector3.zero;
        var timingBudget=Stopwatch.StartNew();
        for(int i=0;i<cases;i++){
            SnapshotBodyState(first,firstCapsule,random,i);SnapshotBodyState(second,secondCapsule,random,i+3);
            firstCapsule.direction=1;secondCapsule.direction=i%3;
            firstObject.transform.localScale=Vector3.one;secondObject.transform.localScale=new Vector3(SnapshotFloat(random,.7f,1.3f),SnapshotFloat(random,.7f,1.3f),SnapshotFloat(random,.7f,1.3f));
            Vector3 a=first.position+SnapshotVector(random,.6f),b=second.position+SnapshotVector(random,.6f),error=SnapshotVector(random,.8f),relative=SnapshotVector(random,6);
            float spring=SnapshotFloat(random,200,3500),damping=SnapshotFloat(random,15,140),dt=1f/240;
            Vector3 firstForceBefore=first.GetAccumulatedForce(),secondForceBefore=second.GetAccumulatedForce(),firstTorqueBefore=first.GetAccumulatedTorque(),secondTorqueBefore=second.GetAccumulatedTorque();
            bool nonzero=!firstForceBefore.Equals(Vector3.zero)||!secondForceBefore.Equals(Vector3.zero)||!firstTorqueBefore.Equals(Vector3.zero)||!secondTorqueBefore.Equals(Vector3.zero);
            bool nonfinite=!SnapshotFinite(firstForceBefore)||!SnapshotFinite(secondForceBefore)||!SnapshotFinite(firstTorqueBefore)||!SnapshotFinite(secondTorqueBefore);
            if(nonzero)cpu.preexistingNonzeroAccumulatorCases++;if(nonfinite)cpu.preexistingNonfiniteAccumulatorCases++;
            Vector3 reference=ReferenceSpring(first,second,a,b,error,relative,dt,spring,damping),actual=GBGrip.SolveSpringForce(first,second,a,b,error,relative,dt,spring,damping);
            maximumForceError=Mathf.Max(maximumForceError,Vector3.Distance(reference,actual));if(!reference.Equals(actual))forceDifferences++;
            Vector3 direction=SnapshotVector(random,1).normalized,bend=Vector3.ProjectOnPlane(SnapshotVector(random,1),direction).normalized;
            if(direction.sqrMagnitude<.01f)direction=Vector3.forward;if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(Vector3.up,direction).normalized;
            Vector3 circle=first.position+SnapshotVector(random,.6f),end=circle+direction*SnapshotFloat(random,.2f,.73f),previous=i%7==0?Vector3.zero:Vector3.ProjectOnPlane(SnapshotVector(random,1),direction).normalized;
            float bendRadius=SnapshotFloat(random,.01f,.323f);Vector3 initialElbow=circle+bend*bendRadius;
            bool covering=i%2==0;var rival=covering?secondCapsule:null;
            Vector3 expectedElbow=ReferenceElbow(first,firstCapsule,rival,covering,direction,bend,circle,bendRadius,end,previous,initialElbow);
            Vector3 actualElbow=GBWrestler.SelectArmElbow(first,firstCapsule,rival,covering,direction,bend,circle,bendRadius,end,previous,initialElbow);
            maximumElbowError=Mathf.Max(maximumElbowError,Vector3.Distance(expectedElbow,actualElbow));if(!expectedElbow.Equals(actualElbow))elbowDifferences++;
            // Warm each exact implementation before its paired comparison.
            // Delegate construction, random/state writes and checksum sums are
            // outside the measured intervals; invocation overhead is shared.
            Func<Vector3> oldSpring=()=>ReferenceSpring(first,second,a,b,error,relative,dt,spring,damping);
            Func<Vector3> newSpring=()=>GBGrip.SolveSpringForce(first,second,a,b,error,relative,dt,spring,damping);
            Func<Vector3> oldElbow=()=>ReferenceElbow(first,firstCapsule,rival,covering,direction,bend,circle,bendRadius,end,previous,initialElbow);
            Func<Vector3> newElbow=()=>GBWrestler.SelectArmElbow(first,firstCapsule,rival,covering,direction,bend,circle,bendRadius,end,previous,initialElbow);
            if(i==0)for(int warm=0;warm<4;warm++){timingChecksum+=oldSpring();timingChecksum+=newSpring();timingChecksum+=oldElbow();timingChecksum+=newElbow();}
            for(int repeat=0;repeat<3&&timingBudget.Elapsed.TotalMilliseconds<750;repeat++){
                if((i+repeat)%2==0){
                    referenceSpringTicks+=SnapshotTimedCall(oldSpring,ref timingChecksum);candidateSpringTicks+=SnapshotTimedCall(newSpring,ref timingChecksum);
                    referenceElbowTicks+=SnapshotTimedCall(oldElbow,ref timingChecksum);candidateElbowTicks+=SnapshotTimedCall(newElbow,ref timingChecksum);
                }else{
                    candidateElbowTicks+=SnapshotTimedCall(newElbow,ref timingChecksum);referenceElbowTicks+=SnapshotTimedCall(oldElbow,ref timingChecksum);
                    candidateSpringTicks+=SnapshotTimedCall(newSpring,ref timingChecksum);referenceSpringTicks+=SnapshotTimedCall(oldSpring,ref timingChecksum);
                }
                cpu.pairedCallsPerHelper++;
            }
            Vector3 firstForceAfter=first.GetAccumulatedForce(),secondForceAfter=second.GetAccumulatedForce(),firstTorqueAfter=first.GetAccumulatedTorque(),secondTorqueAfter=second.GetAccumulatedTorque();
            // Equals compares exact numerical components, including an already
            // undefined NaN component on a frozen/infinite inertia axis. A NaN
            // is recorded below, never silently substituted with a zero force.
            bool preserved=firstForceBefore.Equals(firstForceAfter)&&secondForceBefore.Equals(secondForceAfter)&&firstTorqueBefore.Equals(firstTorqueAfter)&&secondTorqueBefore.Equals(secondTorqueAfter);
            if(!preserved)cpu.changedAccumulatorCases++;noAddedForce&=preserved;
            if((nonzero||!preserved)&&accumulatorExamples.Count<3)accumulatorExamples.Add("case="+i+" forceA "+firstForceBefore.ToString("R")+" -> "+firstForceAfter.ToString("R")+" forceB "+secondForceBefore.ToString("R")+" -> "+secondForceAfter.ToString("R")+" torqueA "+firstTorqueBefore.ToString("R")+" -> "+firstTorqueAfter.ToString("R")+" torqueB "+secondTorqueBefore.ToString("R")+" -> "+secondTorqueAfter.ToString("R"));
        }
        double ticksToMs=1000.0/Stopwatch.Frequency;cpu.referenceSpringMs=referenceSpringTicks*ticksToMs;cpu.candidateSpringMs=candidateSpringTicks*ticksToMs;cpu.referenceElbowMs=referenceElbowTicks*ticksToMs;cpu.candidateElbowMs=candidateElbowTicks*ticksToMs;
        cpu.springOldOverNew=cpu.candidateSpringMs>0?cpu.referenceSpringMs/cpu.candidateSpringMs:0;cpu.elbowOldOverNew=cpu.candidateElbowMs>0?cpu.referenceElbowMs/cpu.candidateElbowMs:0;cpu.accumulatorExamples=accumulatorExamples.ToArray();cpu.resultChecksum=timingChecksum;
        File.WriteAllText(Path.Combine(directory,"physics-snapshot-cpu.json"),JsonUtility.ToJson(cpu,true));
        Check("snapshot_spring_matches_frozen_force_math",forceDifferences==0,"seed="+seed+"; native cases="+cases+"; exact component differences="+forceDifferences+"; maximum force error="+maximumForceError+"; masses, centers, rotations, anisotropic/zero-axis inertia and kinematic combinations");
        Check("snapshot_arm_matches_frozen_candidate_decisions",elbowDifferences==0,"seed="+seed+"; native cases="+cases+"; exact selected-elbow differences="+elbowDifferences+"; maximum error="+maximumElbowError+"m; all24 candidates and3 endpoints, ordinary/covering, rival axes0/1/2 and scaled transforms");
        Check("snapshot_helpers_preserve_forces_and_registry",noAddedForce&&app.Registry.Count==registryBefore,"before/after force+torque changes="+cpu.changedAccumulatorCases+"; initial nonzero cases="+cpu.preexistingNonzeroAccumulatorCases+"; initial nonfinite cases="+cpu.preexistingNonfiniteAccumulatorCases+"; registered game bodies unchanged="+registryBefore+"; actual examples in physics-snapshot-cpu.json");
        Check("snapshot_paired_cpu_timings_recorded",cpu.pairedCallsPerHelper>0&&cpu.referenceSpringMs>0&&cpu.candidateSpringMs>0&&cpu.referenceElbowMs>0&&cpu.candidateElbowMs>0,"paired calls per helper="+cpu.pairedCallsPerHelper+"; spring old/new ms="+cpu.referenceSpringMs+"/"+cpu.candidateSpringMs+"; elbow old/new ms="+cpu.referenceElbowMs+"/"+cpu.candidateElbowMs+"; ratios describe local helpers, not game FPS");
        int qualificationDifferences=0;
        for(int flags=0;flags<16;flags++){
            bool shooting=(flags&1)!=0,alreadyDelivered=(flags&2)!=0,jumpActive=(flags&4)!=0,down=(flags&8)!=0;
            // Execute the original branch qualification in its original order.
            bool oldQualifies;
            if(shooting&&!alreadyDelivered)oldQualifies=true;
            else if(!jumpActive||shooting||down)oldQualifies=false;
            else oldQualifies=true;
            if(oldQualifies!=GBWrestler.MomentumContactEligible(shooting,alreadyDelivered,jumpActive,down))qualificationDifferences++;
        }
        Check("inactive_momentum_gate_preserves_action_qualification",qualificationDifferences==0,"all16 shot-active/shot-consumed/jump-active/down combinations retain original branch qualification; active shot/jump transfer still requires native momentum gameplay checks");
        Destroy(firstObject);Destroy(secondObject);yield return null;
        Check("snapshot_fixture_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
