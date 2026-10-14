using UnityEngine;
using System.Collections.Generic;

public partial class GBWrestler {
    string actionFeedback="",remoteFeedback="";float feedbackUntil,remoteStrain,feedbackPriorityUntil;bool remoteLegs;
    float shotUntil,shotAt,shotStarted,lastLiftCharge,lastLiftAt=-10;int shotLeadFoot;bool shotImpactDone,shotLaunchPending;Vector3 shotDirection=Vector3.forward;
    public bool Shooting {get{return Time.time<shotUntil&&!Down;}}
    public Vector3 ShotDirection {get{return shotDirection;}}
    bool ShotDriving {get{return Shooting&&Time.time-shotStarted>=.10f;}}
    void AdvanceShotEntry(){
        if(!shotLaunchPending)return;
        if(!Shooting){shotLaunchPending=false;return;}
        if(!ShotDriving)return;
        shotLaunchPending=false;
        // Change level before penetration. Deliver the existing paid drive
        // exactly once after the hips start lowering, never add another boost.
        foreach(var part in bodies)part.AddForce(shotDirection*2.8f+Vector3.down*.15f,ForceMode.VelocityChange);
    }
    readonly LineRenderer[] targetRings=new LineRenderer[2];
    bool LegTargeting {get{return GBApp.I.CurrentMode!=1&&command.Has(GBInput.Pin);}}
    public string TargetMode {get{return GBApp.I.CurrentMode==1?"Cursor / body":(GBApp.I.Authority?LegTargeting:remoteLegs)?"Legs":"Upper body";}}
    public string ActionFeedback {get{return Time.time<feedbackUntil?(GBApp.I.Authority?actionFeedback:remoteFeedback):"";}}
    public float GripStrain {get{if(!GBApp.I.Authority)return remoteStrain;float strain=0;foreach(var hand in hands)if(hand!=null&&hand.joint)strain=Mathf.Max(strain,hand.joint.Strain);return strain;}}
    public bool HeldByOpponent {get{foreach(var wrestler in GBApp.I.Wrestlers)if(wrestler!=this)foreach(var hand in wrestler.hands)if(hand!=null&&hand.joint&&hand.target&&hand.target.wrestler==this)return true;return false;}}
    public void ApplyCombatSnapshot(string feedback,float strain,bool legs){remoteFeedback=feedback??"";remoteStrain=Mathf.Clamp01(strain);remoteLegs=legs;feedbackUntil=Time.time+.5f;}
    void Feedback(string text,float duration=.8f){if(duration<=.4f&&Time.time<feedbackPriorityUntil)return;actionFeedback=text;feedbackUntil=Time.time+duration;if(duration>=.5f)feedbackPriorityUntil=Time.time+duration;}
    float scrambleUntil,scrambleRepressAt,scrambleRegenAt;bool scrambleExhausted,scrambleInputHeld,scrambleUsed;
    public const float ScrambleRepressSeconds=.4f;
    public const float ScrambleRegenDelay=.8f;
    public const float GroundStaminaRegenDelay=2.5f;
    float groundStaminaSeconds;
    bool restingOnGround;
    public float GroundStaminaSeconds {get{return groundStaminaSeconds;}}
    public bool GroundStaminaRecoveryReady {get{return restingOnGround&&groundStaminaSeconds>=GroundStaminaRegenDelay;}}
    void UpdateGroundStaminaRecovery(float dt){
        restingOnGround=false;
        if(Down||PhysicallyDownForPin||PinControlled){
            foreach(int index in new[]{0,1}){
                var body=bodies[index];RaycastHit floor;
                if(!Physics.Raycast(body.position+Vector3.up*.4f,Vector3.down,out floor,1.4f,1,QueryTriggerInteraction.Ignore)||floor.normal.y<.75f)continue;
                var shape=body.GetComponent<Collider>();
                if(shape&&Vector3.Distance(shape.ClosestPoint(floor.point),floor.point)<.09f){restingOnGround=true;break;}
            }
        }
        groundStaminaSeconds=restingOnGround?Mathf.Min(GroundStaminaRegenDelay,groundStaminaSeconds+dt):0;
    }
    void GateScrambleInput(bool requested){
        bool held=command.Has(GBInput.Brace);
        // Read raw defensive input before cooldown masking. A blocked press
        // is still an escape attempt, never a free regeneration interval.
        if(requested)scrambleRegenAt=Time.time+ScrambleRegenDelay;
        if(!held&&scrambleInputHeld&&scrambleUsed)scrambleRepressAt=Mathf.Max(scrambleRepressAt,Time.time+ScrambleRepressSeconds);
        scrambleInputHeld=held;
        if(!held)scrambleUsed=false;else scrambleUsed|=requested;
    }
    public float ScrambleCooldown {get{return Mathf.Max(0,Mathf.Max(scrambleUntil,scrambleRepressAt)-Time.time);}}
    public bool ScrambleReady {get{return Time.time>=scrambleUntil&&Time.time>=scrambleRepressAt&&stamina>=4;}}
    public bool EscapeRecoveryBlocked {get{return Time.time<scrambleRegenAt||(restingOnGround&&!GroundStaminaRecoveryReady)||((PinControlled||IsBeingPinned)&&!GroundStaminaRecoveryReady);}}
    void UpdateScrambleRecovery(bool requested){
        if(Time.time>=scrambleUntil&&stamina>=4)scrambleExhausted=false;
        if(requested&&stamina<4&&!scrambleExhausted){scrambleUntil=Time.time+1.2f;scrambleExhausted=true;}
    }
    int GripsOn(GBWrestler target){int count=0;foreach(var h in hands)if(h.joint&&h.target&&h.target.wrestler==target)count++;return count;}
    float AttackLeverage(GBWrestler target){
        int grips=GripsOn(target);float power=grips>=2?.9f:.8f;
        if(PinControlled||IsBeingPinned)return power*(grips>=2?.45f:.25f);
        if(PhysicallyDownForPin||Down)power*=.55f;
        return power;
    }
    void CombatReset(){groundStaminaSeconds=0;restingOnGround=false;scrambleUntil=scrambleRepressAt=scrambleRegenAt=0;scrambleExhausted=scrambleInputHeld=scrambleUsed=false;shotUntil=shotAt=lastLiftCharge=0;lastLiftAt=-10;shotImpactDone=shotLaunchPending=false;shotDirection=Vector3.forward;actionFeedback=remoteFeedback="";feedbackUntil=remoteStrain=feedbackPriorityUntil=0;remoteLegs=false;MomentumReset();for(int h=0;h<2;h++)if(targetRings[h])targetRings[h].enabled=false;}

    void ReachLedge(int h,Vector3 point){
        int upper=h==0?3:6,fore=h==0?4:7,index=h==0?5:8;
        Vector3 shoulder=bodies[1].transform.TransformPoint(joints[upper].connectedAnchor),direction=(point-shoulder).normalized;
        if(direction.sqrMagnitude<.1f)return;
        // Overhead reaches straighten the elbow. The usual bent grapple pose
        // shortens the arm and can leave a reachable rim above the fingertips.
        Quaternion rotation=facingRotation*Quaternion.FromToRotation(Vector3.down,Quaternion.Inverse(facingRotation)*direction);
        poses[upper]=poses[fore]=poses[index]=rotation;
        for(int n=0;n<2;n++){
            var body=bodies[n==0?upper:fore];Vector3 goal=shoulder+direction*(n==0?.22f:.58f);
            Vector3 force=Vector3.ClampMagnitude((goal-body.worldCenterOfMass)*600-(body.linearVelocity-bodies[1].GetPointVelocity(shoulder))*28,n==0?140:180);
            body.AddForce(force,ForceMode.Force);
            // The reach is an internal shoulder effort, with its real reaction
            // carried by the planted trunk; it cannot levitate a free body.
            bodies[1].AddForceAtPosition(-force,shoulder,ForceMode.Force);
        }
    }

    // Target selection uses actual collider surfaces. Reaching hands remain physical.
    void UpdateHand(int h,bool holding) {
        if(command.HasHandAim){UpdateMouseHand(h,holding);return;}
        Hand hand=hands[h];int upper=h==0?3:6,fore=h==0?4:7,index=h==0?5:8;var arm=bodies[index];
        if(hand.releaseRequired){if(!holding)hand.releaseRequired=false;hand.candidate=null;return;}
        if(hand.target&&(!hand.target.gameObject.activeInHierarchy||!hand.target.grabbable||!hand.target.GetComponent<Collider>().enabled))Release(h);
        if(Down||stamina<4){Release(h);hand.candidate=null;if(holding&&stamina<4)Feedback("Exhausted - release grips to recover stamina",.25f);return;}
        if(!holding)Release(h);if(hand.target&&!hand.joint)hand.target=null;
        if(hand.joint){
            hand.joint.breakForce=GBApp.I.Tuning.gripStrength*Mathf.Lerp(.45f,1,StaminaFraction);
            Vector3 shoulderPoint=bodies[1].position+facingRotation*new Vector3(h==0?-.27f:.27f,.15f,0);Vector3 toGrip=hand.target.transform.TransformPoint(hand.localAnchor)-shoulderPoint;
            bool ledge=!hand.target.wrestler&&hand.target.Body.isKinematic&&toGrip.y>.2f;
            float angle=command.Has(GBInput.Lift)&&!command.Has(GBInput.Brace)?-145:Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(toGrip,facing),-toGrip.y)*Mathf.Rad2Deg,ledge?-165:-145,-10);
            poses[upper]=facingRotation*Quaternion.Euler(angle+10,0,h==0?-10:10);poses[fore]=facingRotation*Quaternion.Euler(angle-10,0,0);poses[index]=poses[fore];
            if(ledge)ReachLedge(h,hand.target.transform.TransformPoint(hand.localAnchor));
            if(hand.target.prop)hand.target.prop.Stress(command.Has(GBInput.Lift)?32:5);return;
        }
        Vector3 shoulder=bodies[1].position+facingRotation*new Vector3(h==0?-.27f:.27f,.15f,0),preferred=shoulder+facing*.75f+(LegTargeting?Vector3.down*.55f:Vector3.zero);
        GBTarget best=null;Vector3 point=preferred;float score=float.MaxValue;bool protectedTarget=false;
        foreach(var c in Physics.OverlapSphere(shoulder,GBApp.I.Tuning.gripReach,~0,QueryTriggerInteraction.Ignore)){
            var target=c.GetComponent<GBTarget>();if(!target||!target.grabbable||target.wrestler==this||!target.Body)continue;
            if(target.wrestler){bool leg=target.region.Contains("thigh")||target.region.Contains("shin")||target.region.Contains("foot");if(GBApp.I.CurrentMode!=1&&leg!=LegTargeting)continue;if(Time.time<target.wrestler.EscapeUntil){protectedTarget=true;continue;}}
            Vector3 p=c.ClosestPoint(arm.position);
            if(!target.wrestler&&target.Body.isKinematic&&c.bounds.max.y>shoulder.y+.3f&&c.bounds.max.y<shoulder.y+1.1f){var edge=arm.position;edge.y=c.bounds.max.y-.04f;p=c.ClosestPoint(edge);}
            Vector3 dir=p-shoulder;if(dir.magnitude>GBApp.I.Tuning.gripReach||Vector3.Dot(dir.normalized,command.Aim)<-.25f)continue;
            RaycastHit hit;if(Physics.Linecast(shoulder,p,out hit,1,QueryTriggerInteraction.Ignore)&&hit.collider!=c)continue;
            float priority=target.wrestler?(target.region=="torso"?-.20f:target.region.Contains("thigh")?-.12f:target.region=="head"?.4f:target.region.Contains("hand")?.18f:0):.08f;
            float s=Vector3.Distance(arm.position,p)+Vector3.Distance(preferred,p)*.25f+priority;if(s<score){best=target;point=p;score=s;}
        }
        hand.candidate=best;if(!holding)return;
        if(!best)Feedback(protectedTarget?"Opponent escaped - regain position":"Out of reach - move closer and aim",.2f);
        bool highLedge=best&&!best.wrestler&&best.Body.isKinematic&&point.y>shoulder.y+.2f;
        float reachAngle=best?Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(point-shoulder,facing),-(point-shoulder).y)*Mathf.Rad2Deg,highLedge?-165:-130,-15):-72;
        poses[upper]=facingRotation*Quaternion.Euler(reachAngle+8,0,h==0?-12:12);poses[fore]=facingRotation*Quaternion.Euler(reachAngle-10,0,0);poses[index]=poses[fore];
        if(highLedge)ReachLedge(h,point);
        arm.AddForce(Vector3.ClampMagnitude(((best?point:preferred)-arm.position)*330-arm.linearVelocity*24,230),ForceMode.Force);
        if(best&&Vector3.Distance(arm.position,point)<.34f)Attach(h,best,point);
    }
    public bool Attach(int h,GBTarget target,Vector3 point) {
        if(h<0||h>1||!target||target.wrestler==this||!target.Body||!target.grabbable||!target.GetComponent<Collider>().enabled)return false;
        if(hands[h].joint||target.wrestler&&Time.time<target.wrestler.EscapeUntil)return false;
        int index=h==0?5:8;var j=bodies[index].gameObject.AddComponent<GBGrip>();j.owner=this;j.handIndex=h;j.handBody=bodies[index];j.connectedBody=target.Body;
        // The forgiving reach does not create a lever outside the hand. Both
        // spring endpoints belong to their real collider surfaces.
        Vector3 handPoint=bodies[index].GetComponent<Collider>().ClosestPoint(point);
        j.anchor=bodies[index].transform.InverseTransformPoint(handPoint);j.connectedAnchor=target.transform.InverseTransformPoint(point);j.breakForce=GBApp.I.Tuning.gripStrength;
        hands[h].torsoWrap=false;hands[h].aimBias=Vector3.zero;hands[h].joint=j;hands[h].target=target;hands[h].localAnchor=j.connectedAnchor;if(!GrabMomentum(target,point,bodies[index]))Feedback("Grip: "+target.region,.65f);GBApp.I.Sfx.Play("grip",.5f);return true;
    }
    public void Release(int h){if(hands[h]==null)return;if(hands[h].joint){hands[h].joint.enabled=false;Destroy(hands[h].joint);}hands[h].joint=null;hands[h].target=null;}
    public void GripBroke(int h){
        var target=hands[h].target;
        bool ledge=target&&!target.wrestler&&target.Body&&target.Body.isKinematic&&target.GetComponent<Collider>().bounds.max.y>Root.position.y+.6f;
        Release(h);hands[h].releaseRequired=!ledge;
        Feedback(ledge?"Ledge slipped - keep reaching to catch":"Grip slipped - release and grab again",.9f);
    }

    void CombatTick(float dt) {
        MomentumTick(dt);
        bool escaping=ScrambleReady&&HeldByOpponent&&command.Has(GBInput.Brace)&&command.Move.sqrMagnitude>.08f&&stamina>8&&!Down;
        // Scrambling works against the real hold. It never releases the
        // opponent's hands or awards a timed escape/velocity impulse.
        if(escaping){stamina=Mathf.Max(0,stamina-dt*5*ScrambleStaminaMultiplier);Feedback("Work out of the hold - brace and move",.2f);}
        if(Down||GripCount==0||command.Has(GBInput.Lift)||stamina<6)return;
        float backward=Mathf.Max(0,-Vector3.Dot(command.Move,facing));Vector3 side=Vector3.Cross(Vector3.up,facing);float lateral=Vector3.Dot(command.Move,side);
        if(backward<.15f&&Mathf.Abs(lateral)<.2f)return;var affected=new HashSet<GBWrestler>();
        foreach(var hand in hands){
            if(!hand.joint||!hand.target||!hand.target.wrestler||!affected.Add(hand.target.wrestler))continue;
            var victim=hand.target.wrestler;bool braced=victim.command.Has(GBInput.Brace)&&victim.grounded&&victim.stamina>8;float resistance=(braced?.35f:1)*AttackLeverage(victim);
            Vector3 force=(-facing*backward*230+side*lateral*165)*resistance;victim.Root.AddForce(force,ForceMode.Force);Root.AddForce(-force,ForceMode.Force);
            if(Mathf.Abs(lateral)>.2f){
                Vector3 pivot=side*lateral*65*resistance,contact=hand.target.transform.TransformPoint(hand.localAnchor);
                hand.target.Body.AddForceAtPosition(pivot,contact,ForceMode.Force);
                Root.AddForceAtPosition(-pivot,contact,ForceMode.Force);
            }
            float legBonus=hand.target.region.Contains("thigh")||hand.target.region.Contains("shin")?1.35f:1;
            victim.balance=Mathf.Max(0,victim.balance-dt*(backward*32+Mathf.Abs(lateral)*24)*resistance*legBonus);if(victim.balance<20)victim.Knock(22,.55f);
            stamina=Mathf.Max(0,stamina-dt*2);Feedback(braced?"Opponent bracing - pivot or wait for stamina":backward>.35f?"Pulling opponent off balance":"Pivoting around the hold",.2f);
        }
    }
    public void Lift() {
        if(stamina<8){Feedback("Exhausted - lift needs stamina");return;}
        RaycastHit support;bool supported=Physics.Raycast(Root.position+Vector3.up*.1f,Vector3.down,out support,2.4f,1,QueryTriggerInteraction.Ignore);
        if(!grounded&&!supported){Feedback("Plant your feet before lifting",.2f);return;}
        lastLiftCharge=liftCharge;lastLiftAt=Time.time;
        if(supported){float stanceError=support.point.y+1.04f-Root.position.y;Root.AddForce(Vector3.up*Mathf.Clamp(stanceError*3000-Root.linearVelocity.y*360,-1800,0),ForceMode.Force);}
        var affected=new HashSet<GBWrestler>();
        for(int h=0;h<2;h++){var hand=hands[h];if(!hand.joint||!hand.target)continue;var target=hand.target.wrestler;float leverage=target?AttackLeverage(target):1;int targetGrips=target?GripsOn(target):0;
            bodies[h==0?5:8].AddForce((Vector3.up*240+facing*45)*leverage,ForceMode.Force);
            if(target&&affected.Add(target)){
                bool braced=target.command.Has(GBInput.Brace)&&target.grounded&&target.stamina>8;float resistance=(braced?.32f:target.Down?1.1f:.9f)*leverage;
                // A committed lift can unload neutral planted feet. This is a
                // short force-driven pressure state, never a frozen opponent:
                // movement, manually held grips and directional escapes remain.
                if(!braced&&targetGrips==2&&!PinControlled&&!PhysicallyDownForPin&&liftCharge>.18f)target.pressureUntil=Mathf.Max(target.pressureUntil,Time.time+.08f);
                float desired=1700+(Root.position.y+.7f-target.Root.position.y)*1800-target.Root.linearVelocity.y*270;
                target.Root.AddForce(Vector3.up*Mathf.Clamp(desired,0,targetGrips==2?2200:750)*resistance*Mathf.Lerp(.4f,1,StaminaFraction)*Mathf.Lerp(.3f,1,liftCharge),ForceMode.Force);
                target.balance=Mathf.Max(0,target.balance-Time.fixedDeltaTime*(braced?7:26)*leverage);Feedback(braced?"Opponent bracing - pull or pivot first":targetGrips==1?"One hand lift - use both grips":"Lifting - position the opponent for a pin",.2f);
            }else if(!target&&!hand.target.Body.isKinematic)hand.target.Body.AddForce(Vector3.up*Mathf.Min(350,hand.target.Body.mass*14),ForceMode.Force);
        }
    }
    public void Slam() {
        var affected=new HashSet<Rigidbody>();for(int h=0;h<2;h++){var t=hands[h].target;if(!hands[h].joint||!t)continue;var r=t.wrestler?t.wrestler.Root:t.Body;if(!affected.Add(r)||r.isKinematic)continue;
            float leverage=t.wrestler?AttackLeverage(t.wrestler):1;r.AddForce(Vector3.down*(t.wrestler?1000:Mathf.Min(400,r.mass*18))*leverage,ForceMode.Force);if(t.wrestler)t.wrestler.balance=Mathf.Max(0,t.wrestler.balance-Time.fixedDeltaTime*32*leverage);Feedback("Slamming - keep the grip",.2f);
        }
    }
    // A shot changes stance and momentum only. The player operates both grips.
    public void Shot(){
        if(Time.time<shotAt){Feedback("Shot recovering - reposition",.3f);return;}
        if(stamina<12){Feedback("Exhausted - shot needs stamina");return;}
        if(Down||hanging||Sprawling||Time.time<pressureUntil||!Physics.Raycast(Root.position+Vector3.up*.1f,Vector3.down,1.8f,1,QueryTriggerInteraction.Ignore)){Feedback(Sprawling?"Release sprawl before shooting":"Plant your feet before shooting",.5f);return;}
        shotDirection=facing.normalized;shotStarted=Time.time;shotUntil=Time.time+.78f;shotAt=Time.time+.9f;shotImpactDone=false;LastShotApproachSpeed=0;stamina=Mathf.Max(0,stamina-12);
        // Enter on the foot that is already ahead. The rear leg follows with
        // real joint motors; the shot never swaps or teleports the limbs.
        shotLeadFoot=Vector3.Dot(bodies[11].position-bodies[14].position,shotDirection)>=0?0:1;
        nextStep=shotLeadFoot;
        BeginShotMomentum();
        shotLaunchPending=true;
        Feedback("Forward shot - grab manually to follow through",.7f);
    }
    public void Contact(float speed,float impulse) {if(Time.time-lastImpact<.3f||speed<GBApp.I.Tuning.impactThreshold)return;lastImpact=Time.time;Knock(Mathf.Clamp((speed-GBApp.I.Tuning.impactThreshold)*6+impulse*.01f,5,58),speed>8?.7f:.35f);}
    public void Knock(float damage,float duration) {
        if(Time.time<recoveryUntil)return;bool braced=command.Has(GBInput.Brace)&&grounded&&stamina>8;if(braced&&damage<60)damage*=.55f;balance=Mathf.Max(0,balance-damage);
        if(damage>=60||balance<20){downUntil=Mathf.Max(downUntil,Time.time+Mathf.Clamp(duration,.55f,GBApp.I.Tuning.recoverySeconds));recoveryUntil=downUntil+.8f;Release(0);Release(1);balance=35;Feedback("Down - recover your footing",.7f);}
        else{stumbleUntil=Mathf.Max(stumbleUntil,Time.time+Mathf.Clamp(duration,.15f,.4f));Feedback(braced?"Braced the impact":"Stumbling - keep your footing",.4f);}
    }
    public void Visuals() {
        for(int n=1;n<15;n++)if(seams[n]){Vector3 a=bodies[n].transform.TransformPoint(seamLocal[n]),b=bodies[parents[n]].transform.TransformPoint(seamParent[n]);seams[n].position=(a+b)*.5f;seams[n].rotation=Quaternion.FromToRotation(Vector3.up,b-a);seams[n].localScale=new Vector3(seamWidths[n]*2,Mathf.Max(.001f,Vector3.Distance(a,b))*.5f,seamWidths[n]*2);}
        for(int h=0;h<2;h++){var hand=hands[h];if(hand==null)continue;var target=hand.target?hand.target:hand.candidate;hand.line.enabled=target!=null;
            if(!target){if(targetRings[h])targetRings[h].enabled=false;continue;}Vector3 point=hand.target?target.transform.TransformPoint(hand.localAnchor):target.GetComponent<Collider>().ClosestPoint(bodies[h==0?5:8].position);
            Color tint=hand.target?Color.Lerp(new Color(.3f,1,.55f),new Color(1,.3f,.18f),hand.joint?hand.joint.Strain:GripStrain):TargetMode=="Legs"?new Color(1,.74f,.18f):new Color(.25f,.85f,1);
            hand.line.startColor=hand.line.endColor=tint;hand.line.SetPosition(0,bodies[h==0?5:8].position);hand.line.SetPosition(1,point);
            if(!targetRings[h]){var ring=new GameObject("Grip target highlight").AddComponent<LineRenderer>();ring.transform.SetParent(transform,false);ring.useWorldSpace=true;ring.loop=true;ring.positionCount=24;ring.startWidth=ring.endWidth=.022f;ring.material=hand.line.material;targetRings[h]=ring;}
            var marker=targetRings[h];marker.enabled=true;marker.startColor=marker.endColor=tint;Vector3 normal=(point-bodies[h==0?5:8].position).normalized;if(normal.sqrMagnitude<.1f)normal=facing;
            Vector3 side=Vector3.Cross(normal,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;Vector3 up=Vector3.Cross(side,normal).normalized;
            for(int n=0;n<24;n++){float angle=n*Mathf.PI*2/24;marker.SetPosition(n,point-normal*.025f+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*.13f);}
        }
    }
}

// Contact spring preserves the acyclic structural tree even with reciprocal holds.
public class GBGrip:MonoBehaviour {
    public GBWrestler owner;public int handIndex;public Rigidbody handBody,connectedBody;public Vector3 anchor,connectedAnchor;public float breakForce=1050;
    float strained;public float Strain {get;private set;}
    struct PointMassSnapshot {
        public bool kinematic;public Vector3 lever,tensor;public Quaternion principal,inversePrincipal;public float mass;
        public PointMassSnapshot(Rigidbody body,Vector3 point){
            kinematic=body.isKinematic;lever=tensor=Vector3.zero;principal=inversePrincipal=Quaternion.identity;mass=1;
            if(kinematic)return;
            lever=point-body.worldCenterOfMass;principal=body.rotation*body.inertiaTensorRotation;
            inversePrincipal=Quaternion.Inverse(principal);tensor=body.inertiaTensor;mass=Mathf.Max(.001f,body.mass);
        }
    }
    static Vector3 PointInverseMass(in PointMassSnapshot body,Vector3 force){
        if(body.kinematic)return Vector3.zero;
        Vector3 localTorque=body.inversePrincipal*Vector3.Cross(body.lever,force),tensor=body.tensor;
        // Unity represents frozen principal axes with zero inertia; they have
        // zero inverse inertia rather than an artificially huge response.
        Vector3 localAcceleration=new Vector3(tensor.x>0?localTorque.x/tensor.x:0,tensor.y>0?localTorque.y/tensor.y:0,tensor.z>0?localTorque.z/tensor.z:0);
        return force/body.mass+Vector3.Cross(body.principal*localAcceleration,body.lever);
    }
    internal static Vector3 SolveSpringForce(Rigidbody hand,Rigidbody target,Vector3 a,Vector3 b,Vector3 error,Vector3 relative,float dt,float spring=2800,float damping=90){
        // Solve F = k*(error + dt*vNext) + c*vNext, where
        // vNext = relative - dt*K*F. K includes each contact's rotational
        // response: a light hand can have much less point mass than body mass.
        // Explicit point damping otherwise reverses that velocity many times
        // in one step, pumping the connected joints when the anchor is offset.
        float softness=damping*dt+spring*dt*dt;
        // Body properties cannot change between these three axis evaluations.
        // Keep the same solve/math order, with two local native-data snapshots.
        var handMass=new PointMassSnapshot(hand,a);var targetMass=new PointMassSnapshot(target,b);
        Vector3 x=Vector3.right+softness*(PointInverseMass(handMass,Vector3.right)+PointInverseMass(targetMass,Vector3.right));
        Vector3 y=Vector3.up+softness*(PointInverseMass(handMass,Vector3.up)+PointInverseMass(targetMass,Vector3.up));
        Vector3 z=Vector3.forward+softness*(PointInverseMass(handMass,Vector3.forward)+PointInverseMass(targetMass,Vector3.forward));
        Vector3 rhs=error*spring+relative*(damping+spring*dt);
        float determinant=Vector3.Dot(x,Vector3.Cross(y,z));
        if(determinant<=.000001f)return Vector3.zero;
        return new Vector3(Vector3.Dot(rhs,Vector3.Cross(y,z)),Vector3.Dot(rhs,Vector3.Cross(z,x)),Vector3.Dot(rhs,Vector3.Cross(x,y)))/determinant;
    }
    void FixedUpdate(){
        if(!owner||!handBody||!connectedBody||!GBApp.I.Authority)return;
        Vector3 a=handBody.position+handBody.rotation*anchor,b=connectedBody.position+connectedBody.rotation*connectedAnchor;
        Vector3 error=b-a,relative=connectedBody.GetPointVelocity(b)-handBody.GetPointVelocity(a);
        Vector3 force=SolveSpringForce(handBody,connectedBody,a,b,error,relative,Time.fixedDeltaTime);
        if(owner.command.Has(GBInput.Pin)&&owner.PinTarget&&connectedBody.GetComponent<GBTarget>()&&connectedBody.GetComponent<GBTarget>().wrestler==owner.PinTarget){
            // A pin commits downward pressure. Let the hand yield toward its
            // unchanged contact instead of using a strained wrist as an
            // involuntary lift of the grounded rival. Lateral grip remains.
            force.y=Mathf.Max(force.y,-65);
        }
        Strain=Mathf.Clamp01(Mathf.Max(error.magnitude/.5f,force.magnitude/Mathf.Max(1,breakForce*1.6f)));
        bool ledge=connectedBody.isKinematic&&b.y>owner.Root.position.y+.2f;
        // Slightly forgive ordinary hold strain and brief spin/contact spikes.
        // Preserve the force cap/reaction and ledge slip behavior.
        if(error.magnitude>(ledge?.65f:.56f)||force.magnitude>breakForce*(ledge?2.1f:2.3f))strained+=Time.fixedDeltaTime;else strained=Mathf.Max(0,strained-Time.fixedDeltaTime*2);
        if(strained>(ledge?.18f:.15f)){owner.GripBroke(handIndex);return;}force=Vector3.ClampMagnitude(force,breakForce);handBody.AddForceAtPosition(force,a,ForceMode.Force);
        if(!connectedBody.isKinematic)connectedBody.AddForceAtPosition(-force,b,ForceMode.Force);
    }
}


