using UnityEngine;

public partial class GBWrestler {
    public float LastArmEffort {get;private set;}
    readonly Vector3[] armBendPole=new Vector3[2];
    readonly Quaternion[] wristPose=new[]{Quaternion.identity,Quaternion.identity};
    readonly float[] wristPoseAt=new[]{-10f,-10f};
    public Vector3 ArmShoulder(int h){var joint=joints[h==0?3:6];return bodies[1].transform.TransformPoint(joint?joint.connectedAnchor:new Vector3(h==0?-.28f:.28f,.21f,0));}
    public GBTarget ArmMarkedTarget(int h){return hands[h].target?hands[h].target:hands[h].candidate;}
    Vector3 HandCenterGoal(int h){
        var hand=hands[h];var body=bodies[h==0?5:8];Vector3 goal=ArmTarget(h);
        if(hand.target){
            Vector3 anchor=hand.target.transform.TransformPoint(hand.localAnchor);
            Vector3 normal=ContactNormal(hand.target.GetComponent<Collider>(),anchor,body.position-anchor);
            if(command.Has(GBInput.Pin)&&PinTarget)return anchor+normal*(hand.joint?hand.joint.anchor.magnitude:.095f)+(goal-anchor)*.20f;
            return hand.joint?goal-body.rotation*hand.joint.anchor:anchor+normal*.095f;
        }
        if(hand.candidate){
            var shape=hand.candidate.GetComponent<Collider>();Vector3 cursor=Root.position+command.HandOffset,shoulder=ArmShoulder(h);
            Vector3 aimed=AimedSurface(shape,cursor,shoulder),point=hand.candidate.region=="torso"?TorsoWrapPoint(shape,aimed,h):aimed;
            return point+ContactNormal(shape,point,shoulder-point)*.100f;
        }
        return goal;
    }
    public Vector3 ArmAimGoal(int h){
        Vector3 shoulder=ArmShoulder(h),delta=HandCenterGoal(h)-shoulder;
        return shoulder+(delta.sqrMagnitude>.001f?delta.normalized:facing)*Mathf.Clamp(delta.magnitude,.20f,.718f);
    }
    public bool ArmAimClamped(int h){return Vector3.Distance(ArmAimGoal(h),HandCenterGoal(h))>.035f;}
    public Vector3 ArmTarget(int h){
        Vector3 side=Vector3.Cross(Vector3.up,facing).normalized;
        return Root.position+command.HandOffset+side*(h==0?-.10f:.10f)+(hands[h].joint&&hands[h].target?hands[h].target.transform.TransformVector(hands[h].aimBias):Vector3.zero);
    }
    Vector3 TorsoWrapPoint(Collider collider,Vector3 cursor,int h){
        var capsule=collider as CapsuleCollider;if(!capsule)capsule=collider.GetComponent<CapsuleCollider>();if(!capsule)return cursor;
        Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
        Vector3 local=capsule.transform.InverseTransformPoint(cursor)-capsule.center;
        // The rounded end narrows to a central point. A wrapping torso grip
        // uses the real flank, keeping hands outside the chest-cover interface.
        float half=Mathf.Max(0,capsule.height*.5f-capsule.radius),axial=Mathf.Clamp(Vector3.Dot(local,axis),-half,half);
        Vector3 spine=capsule.center+axis*axial;
        Vector3 side=Vector3.Cross(Vector3.up,facing)*(h==0?-1:1);
        Vector3 radial=Vector3.ProjectOnPlane(capsule.transform.InverseTransformDirection(side),axis);
        if(radial.sqrMagnitude<.09f)radial=h==0?Vector3.left:Vector3.right;
        radial=Vector3.ProjectOnPlane(radial,axis).normalized;
        float cap=Mathf.Max(0,Mathf.Abs(axial)-half),radius=Mathf.Sqrt(Mathf.Max(.0001f,capsule.radius*capsule.radius-cap*cap));
        return capsule.transform.TransformPoint(spine+radial*radius);
    }
    static Vector3 AimedSurface(Collider collider,Vector3 cursor,Vector3 shoulder){
        var capsule=collider as CapsuleCollider;
        if(!capsule&&collider.GetComponent<GBTarget>()&&collider.GetComponent<GBTarget>().region=="torso")capsule=collider.GetComponent<CapsuleCollider>();
        if(capsule){
            Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
            Vector3 local=capsule.transform.InverseTransformPoint(cursor),delta=local-capsule.center;
            float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            Vector3 spine=capsule.center+axis*Mathf.Clamp(Vector3.Dot(delta,axis),-half,half),normal=local-spine;
            // Direct stick targets can enter a body. Project an interior cursor
            // onto its real capsule rather than installing a grip inside it.
            if(normal.magnitude<capsule.radius-.002f){
                if(normal.sqrMagnitude<.00001f)normal=capsule.transform.InverseTransformPoint(shoulder)-spine;
                if(normal.sqrMagnitude<.00001f)normal=Vector3.forward;
                return capsule.transform.TransformPoint(spine+normal.normalized*capsule.radius);
            }
        }
        return collider.ClosestPoint(cursor);
    }
    static Vector3 ContactNormal(Collider shape,Vector3 contact,Vector3 fallback){
        Vector3 normal=contact-shape.bounds.center;
        var capsule=shape as CapsuleCollider;
        if(capsule){
            Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
            Vector3 local=capsule.transform.InverseTransformPoint(contact)-capsule.center;
            float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            Vector3 core=capsule.center+axis*Mathf.Clamp(Vector3.Dot(local,axis),-half,half);
            normal=contact-capsule.transform.TransformPoint(core);
        }
        if(normal.sqrMagnitude<.0001f)normal=fallback;
        return normal.normalized;
    }
    void ArmForce(Rigidbody body,Vector3 goal,float spring,float damping,float cap){
        Vector3 point=body.worldCenterOfMass;
        Vector3 force=GBGrip.SolveSpringForce(body,bodies[1],point,point,goal-point,Root.linearVelocity-body.linearVelocity,Time.fixedDeltaTime,spring,damping);
        force=Vector3.ClampMagnitude(force,cap);
        // A cover commits body weight instead of straightening an arm into a
        // push-up. Keep lateral/upward cursor reach, but let a low hand fall
        // with gravity without lifting the chest through shoulder reaction.
        if(command.Has(GBInput.Pin)&&PinTarget&&!ClearingCover)force.y=Mathf.Max(force.y,-120);
        body.AddForce(force,ForceMode.Force);
        // Equal forces at the same world point preserve total linear and
        // angular momentum. The anatomical shoulder carries the effort.
        bodies[1].AddForceAtPosition(-force,point,ForceMode.Force);
        LastArmEffort=Mathf.Max(LastArmEffort,force.magnitude);
    }
    void DriveMouseArm(int h,Vector3? reach=null){
        int upper=h==0?3:6,fore=h==0?4:7,index=h==0?5:8;
        Vector3 shoulder=ArmShoulder(h),desired=reach??ArmTarget(h),delta=desired-shoulder;
        Vector3 direction=delta.sqrMagnitude>.001f?delta.normalized:facing;
        const float upperLength=.323f,lowerLength=.41f;
        float distance=Mathf.Clamp(delta.magnitude,.20f,upperLength+lowerLength-.01f);
        Vector3 end=shoulder+direction*distance;
        bool covering=command.Has(GBInput.Pin)&&PinTarget;
        Vector3 side=covering?Vector3.ProjectOnPlane(bodies[1].rotation*Vector3.right,Vector3.up).normalized:Vector3.Cross(Vector3.up,facing).normalized;
        // Fold a covering elbow out of the chest interface. The cursor/held
        // anchor still sets the hand goal; this changes only elbow routing.
        Vector3 bend=Vector3.ProjectOnPlane(covering?side*(h==0?-1:1)+Vector3.up*.25f:-bodies[1].transform.up*.75f+side*(h==0?-.65f:.65f)+facing*.18f,direction).normalized;
        if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(facing,direction).normalized;
        float along=(upperLength*upperLength-lowerLength*lowerLength+distance*distance)/(2*distance);
        Vector3 elbow=shoulder+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
        Vector3 foreGoal=Vector3.Lerp(elbow,end,.40f);
        {
            // Search the physically reachable two-link elbow circle. Changing
            // only the bend pole preserves both link lengths and the actual
            // hand endpoint while avoiding the torso with the forearm capsule.
            var chest=bodies[1];var capsule=chest.GetComponent<CapsuleCollider>();
            float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            var rivalCapsule=covering?PinTarget.bodies[1].GetComponent<CapsuleCollider>():null;
            float bendRadius=Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
            Vector3 circle=shoulder+direction*along,bestElbow=elbow,previous=Vector3.ProjectOnPlane(armBendPole[h],direction).normalized;
            float bestScore=float.NegativeInfinity;
            for(int sample=0;sample<24;sample++){
                Vector3 pole=Quaternion.AngleAxis(sample*15,direction)*bend;
                Vector3 candidate=circle+pole*bendRadius;
                float clearance=float.PositiveInfinity;
                for(int endpoint=0;endpoint<3;endpoint++){
                    Vector3 point=Vector3.Lerp(candidate,end,.24f+endpoint*.16f);
                    Vector3 local=Quaternion.Inverse(chest.rotation)*(point-chest.position)-capsule.center;
                    Vector3 core=Vector3.up*Mathf.Clamp(local.y,-half,half);
                    clearance=Mathf.Min(clearance,(local-core).magnitude-capsule.radius-.11f);
                    if(rivalCapsule){
                        // A legal route must also stay outside the rival's
                        // real torso. A forearm under that chest becomes a
                        // physical spacer and stops its shoulders settling.
                        Vector3 rivalLocal=rivalCapsule.transform.InverseTransformPoint(point)-rivalCapsule.center;
                        Vector3 axis=rivalCapsule.direction==0?Vector3.right:rivalCapsule.direction==1?Vector3.up:Vector3.forward;
                        float rivalHalf=Mathf.Max(0,rivalCapsule.height*.5f-rivalCapsule.radius);
                        Vector3 rivalCore=axis*Mathf.Clamp(Vector3.Dot(rivalLocal,axis),-rivalHalf,rivalHalf);
                        clearance=Mathf.Min(clearance,(rivalLocal-rivalCore).magnitude-rivalCapsule.radius-.11f);
                    }
                }
                // Prefer an outward anatomical bend and the previous pole;
                // contact clearance still wins when the torso blocks a route.
                float score=(covering?clearance:Mathf.Min(clearance,.025f))+Vector3.Dot(pole,bend)*(covering?.035f:.075f)+Vector3.Dot(pole,previous)*.025f;
                if(score>bestScore){bestScore=score;bestElbow=candidate;}
            }
            elbow=bestElbow;foreGoal=Vector3.Lerp(elbow,end,.40f);
            armBendPole[h]=(elbow-circle).normalized;
        }
        Vector3 upperDirection=(elbow-shoulder).normalized,lowerDirection=(end-elbow).normalized;
        // One shared elbow hinge plane keeps humerus/forearm rotation coherent.
        // Independent shortest-arc fits could twist the elbow through the arm.
        Vector3 hinge=Vector3.Cross(lowerDirection,upperDirection).normalized;
        if(hinge.sqrMagnitude<.01f)hinge=side;
        if(Vector3.Dot(hinge,side)<0)hinge=-hinge;
        // The humerus is slightly angled in its real connected rest chain.
        // Align that anchor axis, rather than treating it as a vertical shaft.
        // Both arm frames keep the same anatomical bend-plane orientation.
        Vector3 restUpper=(joints[fore].connectedAnchor-joints[upper].anchor).normalized;
        Quaternion upperRest=Quaternion.LookRotation(Vector3.forward,-restUpper);
        poses[upper]=Quaternion.LookRotation(Vector3.Cross(hinge,-upperDirection).normalized,-upperDirection)*Quaternion.Inverse(upperRest);
        poses[fore]=poses[index]=Quaternion.LookRotation(Vector3.Cross(hinge,-lowerDirection).normalized,-lowerDirection);
        Vector3 palmForward=Vector3.ProjectOnPlane(facing,lowerDirection);
        if(palmForward.sqrMagnitude>.001f){
            Quaternion relaxed=Quaternion.LookRotation(palmForward.normalized,-lowerDirection);
            // Keep a modest wrist twist; the anatomical joint remains the
            // only means of turning the real hand.
            Quaternion targetWrist=Quaternion.RotateTowards(poses[fore],relaxed,35);
            if(Time.time-wristPoseAt[h]>.15f)wristPose[h]=poses[fore];
            wristPose[h]=Quaternion.RotateTowards(wristPose[h],targetWrist,680*Time.fixedDeltaTime);
            wristPoseAt[h]=Time.time;poses[index]=wristPose[h];
        }
        float power=Down||PinControlled?.18f:Mathf.Lerp(.65f,1,StaminaFraction),routePower=power;
        if(command.Has(GBInput.Pin)&&PinTarget&&hands[h].joint){
            // Cover the rival with the body while retaining a live mouse pull.
            // Full arm extension effort would react upward through the chest
            // and prevent the real torso surfaces from settling together.
            power*=.05f;
            poses[index]=Quaternion.Slerp(bodies[index].rotation,poses[index],.30f);
        }
        ArmForce(bodies[upper],Vector3.Lerp(shoulder,elbow,.53f),520,32,140*(covering?routePower*.5f:power));
        ArmForce(bodies[fore],foreGoal,covering?900:560,36,180*(covering?routePower:power));
        ArmForce(bodies[index],end,700,42,(hands[h].joint?320:260)*power);
    }
    void UpdateMouseHand(int h,bool holding){
        var hand=hands[h];var body=bodies[h==0?5:8];if(h==0)LastArmEffort=0;
        if(hand.target&&(!hand.target.gameObject.activeInHierarchy||!hand.target.grabbable||!hand.target.GetComponent<Collider>().enabled))Release(h);
        if(!holding)Release(h);
        if(hand.releaseRequired&&!holding)hand.releaseRequired=false;
        if(Down||stamina<4){Release(h);hand.candidate=null;DriveMouseArm(h);if(holding&&stamina<4)Feedback("Exhausted - release grips to recover stamina",.25f);return;}
        if(hand.target&&!hand.joint)hand.target=null;
        if(hand.joint){
            // Cursor effort names the existing grip endpoint. The motor needs
            // the hand's center, otherwise it drives the sphere into the body.
            Vector3 anchorGoal=ArmTarget(h);
            if(command.Has(GBInput.Pin)&&PinTarget){
                Vector3 actualAnchor=hand.target.transform.TransformPoint(hand.localAnchor);
                Vector3 normal=ContactNormal(hand.target.GetComponent<Collider>(),actualAnchor,body.position-actualAnchor);
                Vector3 centerGoal=actualAnchor+normal*hand.joint.anchor.magnitude+(anchorGoal-actualAnchor)*.20f;
                DriveMouseArm(h,centerGoal);
                // Keep the original grip endpoint, but turn the wrist so the
                // hand sits beside its contact instead of above it as a spacer.
                Quaternion basePose=poses[h==0?4:7];
                if(hand.joint.anchor.sqrMagnitude>.00001f){
                    Quaternion palm=Quaternion.FromToRotation(basePose*hand.joint.anchor.normalized,-normal)*basePose;
                    poses[h==0?5:8]=Quaternion.RotateTowards(basePose,palm,60);
                }
            }
            else DriveMouseArm(h,anchorGoal-body.rotation*hand.joint.anchor);
            hand.joint.breakForce=GBApp.I.Tuning.gripStrength*Mathf.Lerp(.45f,1,StaminaFraction);
            if(hand.target.prop)hand.target.prop.Stress(command.Has(GBInput.Lift)?32:5);
            return;
        }
        Vector3 cursor=Root.position+command.HandOffset,shoulder=bodies[1].transform.TransformPoint(joints[h==0?3:6].connectedAnchor);
        GBTarget best=null;Vector3 contact=cursor;float score=float.PositiveInfinity;bool protectedTarget=false;
        // The cursor chooses a contact, rather than a torso preference choosing
        // where the player's arms go. Every body region is eligible.
        foreach(var collider in Physics.OverlapSphere(cursor,.36f,~0,QueryTriggerInteraction.Ignore)){
            var target=collider.GetComponent<GBTarget>();if(!target||!target.grabbable||target.wrestler==this||!target.Body)continue;
            if(target.wrestler&&Time.time<target.wrestler.EscapeUntil){protectedTarget=true;continue;}
            Vector3 aimed=AimedSurface(collider,cursor,shoulder),point=target.region=="torso"?TorsoWrapPoint(collider,aimed,h):aimed;
            if(Vector3.Distance(aimed,cursor)>.36f||Vector3.Distance(point,shoulder)>GBApp.I.Tuning.gripReach)continue;
            RaycastHit obstruction;if(Physics.Linecast(shoulder,point,out obstruction,1,QueryTriggerInteraction.Ignore)&&obstruction.collider!=collider)continue;
            // An easier-to-reach pelvis must not replace the exact leg/wrist
            // under the cursor. Reach determines when a grip can attach, not
            // which body part the player selected. Centers only break ties
            // where anatomical colliders share the same surface point.
            float candidateScore=Vector3.Distance(aimed,cursor)+Vector3.Distance(cursor,target.Body.worldCenterOfMass)*.0001f;
            if(candidateScore<score){best=target;contact=point;score=candidateScore;}
        }
        hand.candidate=hand.releaseRequired?null:best;
        Vector3 reachGoal=ArmTarget(h);
        if(best){
            var shape=best.GetComponent<Collider>();
            Vector3 normal=ContactNormal(shape,contact,shoulder-contact);
            reachGoal=contact+normal*.100f;
        }
        DriveMouseArm(h,reachGoal);
        if(!holding||hand.releaseRequired)return;
        if(best&&Vector3.Distance(body.position,contact)<.34f){Vector3 nominal=ArmTarget(h);if(Attach(h,best,contact)&&best.region=="torso"){hand.aimBias=best.transform.InverseTransformVector(contact-nominal);hand.torsoWrap=true;}}
        else Feedback(protectedTarget?"Opponent escaped - aim and regain position":best?"Reach the highlighted contact":"Aim at a body part, then hold its hand button",.2f);
    }
}
