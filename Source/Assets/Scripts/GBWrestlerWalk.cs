using UnityEngine;

public partial class GBWrestler {
    readonly Vector3[] walkFootGoals=new Vector3[2];
    readonly float[] walkSwings=new float[2];

    void RecordWalkStep(int foot,Vector3 goal,float progress,bool lowStance){
        walkFootGoals[foot]=goal;
        walkSwings[foot]=lowStance||progress>=1?0:Mathf.Sin(Mathf.Clamp01(progress)*Mathf.PI);
    }

    void WalkPoses(){
        // Follow the real foot planner's raised step, rather than a free-running
        // sine wave. Only motor targets change; grounded feet keep their forces.
        const float thighLength=.39f,shinLength=.36f;
        for(int foot=0;foot<2;foot++){
            int thigh=foot==0?9:12,shin=thigh+1,boot=thigh+2;
            Vector3 hip=Root.position+Root.rotation*joints[thigh].connectedAnchor;
            Vector3 ankle=walkFootGoals[foot]+facingRotation*joints[boot].anchor;
            Vector3 local=Quaternion.Inverse(facingRotation)*(ankle-hip);
            float drop=Mathf.Max(.25f,-local.y);
            float reach=Mathf.Clamp(Mathf.Sqrt(drop*drop+local.z*local.z),.20f,thighLength+shinLength);
            float cosine=Mathf.Clamp((thighLength*thighLength+shinLength*shinLength-reach*reach)/(2*thighLength*shinLength),-1,1);
            float knee=Mathf.Clamp(180-Mathf.Acos(cosine)*Mathf.Rad2Deg,0,85)*walkSwings[foot];
            float pitch=Mathf.Clamp(-Mathf.Atan2(local.z,drop)*Mathf.Rad2Deg-knee*shinLength/(thighLength+shinLength),-60,40);
            float lateral=Mathf.Clamp(Mathf.Atan2(local.x,drop)*Mathf.Rad2Deg,-14,14);
            // Keep the planted boot's ankle target inside its existing limit,
            // including a short backwards step with a flexed knee.
            knee=Mathf.Min(knee,Mathf.Max(0,80-pitch));
            poses[thigh]=facingRotation*Quaternion.Euler(pitch,0,lateral);
            poses[shin]=poses[thigh]*Quaternion.Euler(knee,0,0);
            poses[boot]=facingRotation;
        }
    }
}
