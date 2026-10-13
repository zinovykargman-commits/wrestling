using UnityEngine;

public partial class GBWrestler {
    public string WalkPhaseEvidence {get;private set;}
    public bool WalkPhaseDiagnostics;
    bool walkPlannerActive,walkStartedStep;
    readonly Vector3[] walkFootGoals=new Vector3[2];
    public float LargestFootPlacement {get;private set;}
    public int LeftFootPlacements {get;private set;}
    public int RightFootPlacements {get;private set;}
    void ResetWalkMetrics(){LargestFootPlacement=0;LeftFootPlacements=RightFootPlacements=0;WalkPhaseEvidence="reset";walkPlannerActive=walkStartedStep=false;}
    void RecordFootPlacement(int foot,float length){LargestFootPlacement=Mathf.Max(LargestFootPlacement,length);if(foot==0)LeftFootPlacements++;else RightFootPlacements++;}

    void RecordWalkStep(int foot,Vector3 goal,float progress,bool lowStance){
        walkFootGoals[foot]=goal;
    }

    void WalkPoses(){
        // Follow the real foot planner's raised step, rather than a free-running
        // sine wave. Only motor targets change; grounded feet keep their forces.
        LegFootPoses(95);
    }

    Quaternion WalkingBootRotation(int foot){
        Vector3 up=Quaternion.Inverse(facingRotation)*(bodies[foot==0?10:13].rotation*Vector3.up);
        float shinPitch=Mathf.Atan2(up.z,up.y)*Mathf.Rad2Deg;
        // Roll onto the forefoot when the calf advances over it. Keeping the
        // entire shoe flat forced an excessive backwards bend at the ankle.
        return facingRotation*Quaternion.Euler(Mathf.Clamp(shinPitch-25,0,35),0,0);
    }
    static float BootSupportHeight(Quaternion rotation){
        // Exact vertical extent of the unchanged physical boot. A raised heel
        // still plants the toe instead of sinking it through the canvas.
        return Mathf.Abs((rotation*Vector3.right).y)*.09f+Mathf.Abs((rotation*Vector3.up).y)*.085f+Mathf.Abs((rotation*Vector3.forward).y)*.15f;
    }

    void LegFootPoses(float maximumKnee){
        const float thighLength=.44f,shinLength=.41f;
        for(int foot=0;foot<2;foot++){
            int thigh=foot==0?9:12,shin=thigh+1,boot=thigh+2;
            Vector3 hip=Root.position+Root.rotation*joints[thigh].connectedAnchor;
            Quaternion bootRotation=Shooting?ShotBootRotation(foot):WalkingBootRotation(foot);
            Vector3 ankle=walkFootGoals[foot]+bootRotation*joints[boot].anchor;
            Vector3 local=Quaternion.Inverse(facingRotation)*(ankle-hip);
            float drop=Mathf.Max(Shooting?.05f:.25f,-local.y);
            float reach=Mathf.Clamp(Mathf.Sqrt(drop*drop+local.z*local.z+local.x*local.x),.20f,thighLength+shinLength);
            float cosine=Mathf.Clamp((thighLength*thighLength+shinLength*shinLength-reach*reach)/(2*thighLength*shinLength),-1,1);
            float knee=Mathf.Clamp(180-Mathf.Acos(cosine)*Mathf.Rad2Deg,0,maximumKnee);
            float hipFold=Mathf.Acos(Mathf.Clamp((thighLength*thighLength+reach*reach-shinLength*shinLength)/(2*thighLength*reach),-1,1))*Mathf.Rad2Deg;
            float pitch=Mathf.Clamp(-Mathf.Atan2(local.z,drop)*Mathf.Rad2Deg-hipFold,-80,40);
            float lateral=Mathf.Clamp(Mathf.Atan2(local.x,Mathf.Sqrt(drop*drop+local.z*local.z))*Mathf.Rad2Deg,-35,35);
            // Keep the planted boot's ankle target inside its existing limit,
            // including a short backwards step with a flexed knee.
            float bootPitch=Mathf.DeltaAngle(0,(Quaternion.Inverse(facingRotation)*bootRotation).eulerAngles.x);
            knee=Mathf.Min(knee,Mathf.Max(0,25+bootPitch-pitch));
            poses[thigh]=facingRotation*Quaternion.Euler(pitch,0,lateral);
            poses[shin]=poses[thigh]*Quaternion.Euler(knee,0,0);
            poses[boot]=bootRotation;
        }
    }

    float ShotProgress {get{return Mathf.Clamp01((Time.time-shotStarted)/.78f);}}
    float ShotDrivePhase {get{return 1-.45f*Mathf.SmoothStep(0,1,(ShotProgress-.5f)/.3f);}}
    float ShotStanceHeight {get{return Mathf.Lerp(.68f,.56f,Mathf.SmoothStep(0,1,(ShotProgress-.17f)/.28f))+Mathf.SmoothStep(0,1,(ShotProgress-.55f)/.45f)*.40f;}}
    float ShotFootOffset(int foot){
        float kneeDown=Mathf.SmoothStep(0,1,(ShotProgress-.2f)/.28f),follow=Mathf.SmoothStep(0,1,(ShotProgress-.35f)/.4f),rise=Mathf.SmoothStep(0,1,(ShotProgress-.8f)/.2f);
        // Lead foot enters first, then the knee passes over its toe as the hips
        // advance. The trail foot steps up before rising through the entry.
        return foot==shotLeadFoot?Mathf.Lerp(Mathf.Lerp(.18f,-.34f,kneeDown),-.08f,rise):Mathf.Lerp(-.25f,.24f,follow);
    }
    float ShotToePhase {get{return Mathf.SmoothStep(0,1,(ShotProgress-.2f)/.25f)*(1-Mathf.SmoothStep(0,1,(ShotProgress-.8f)/.2f));}}
    Quaternion ShotBootRotation(int foot){
        Vector3 up=Quaternion.Inverse(facingRotation)*(bodies[foot==0?10:13].rotation*Vector3.up);
        float shinPitch=Mathf.Atan2(up.z,up.y)*Mathf.Rad2Deg;
        float pitch=Mathf.Max(foot==shotLeadFoot?ShotToePhase*35:0,Mathf.Clamp(shinPitch-25,0,75));
        return facingRotation*Quaternion.Euler(pitch,0,0);
    }
    float ShotBootHeight(int foot){return BootSupportHeight(ShotBootRotation(foot))+(foot==shotLeadFoot?0:Mathf.Sin(Mathf.Clamp01((ShotProgress-.35f)/.4f)*Mathf.PI)*.11f);}
    void ShotPoses(){
        // The low asymmetric entry is a target for the existing physical leg
        // motors, following the same supported boot targets as walking.
        LegFootPoses(145);
        float entry=Mathf.SmoothStep(0,1,ShotProgress/.3f),recover=Mathf.SmoothStep(0,1,(ShotProgress-.6f)/.4f);
        float chest=Mathf.Lerp(Mathf.Lerp(20,32,entry),15,recover);
        poses[1]=facingRotation*Quaternion.Euler(chest,0,0);
        poses[2]=facingRotation*Quaternion.Euler(Mathf.Max(0,chest-30),0,0);
        // An unloaded entry keeps hands forward; explicit aim/grips still win
        // in UpdateHand, and this posture never attaches either hand.
        if(!command.HasHandAim)for(int hand=0;hand<2;hand++){
            if(hands[hand].joint)continue;int upper=hand==0?3:6;
            poses[upper]=facingRotation*Quaternion.Euler(-50,0,hand==0?-8:8);
            poses[upper+1]=facingRotation*Quaternion.Euler(-85,0,0);poses[upper+2]=poses[upper+1];
        }
    }
}
