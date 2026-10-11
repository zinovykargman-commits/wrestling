using UnityEngine;

public partial class GBWrestler {
    struct MomentumState {
        public float mass,inertia,yawMomentum;
        public Vector3 center,velocity;
    }
    float spinBudget,spinSampleAt=-10,grabMomentumAt,shotPressureUntil,shotForwardPeak,jumpImpactUntil;bool jumpImpactConsumed;
    public float ShotDriveSpeed {get;private set;}=4.2f;
    public float RecentSpinMomentum {get{return Time.time-spinSampleAt<.9f?spinBudget:0;}}
    public float SpinCarry {get{return Mathf.Clamp01(Mathf.Abs(RecentSpinMomentum)/48);}}
    public float MomentumSpeed {get{return Vector3.ProjectOnPlane(MeasureMomentum(this).velocity,Vector3.up).magnitude;}}
    public float LastGrabMomentumImpulse {get;private set;}
    public float LastGrabApproachSpeed {get;private set;}
    public float LastShotApproachSpeed {get;private set;}
    public float LastThrowSpinCarry {get;private set;}
    public float LastShotPhysicalImpulse {get;private set;}
    public int ShotImpactCount {get;private set;}
    public float LastBodyImpactApproachSpeed {get;private set;}
    public float LastBodyPhysicalImpulse {get;private set;}
    public int BodyImpactCount {get;private set;}
    public bool JumpImpactActive {get{return Time.time<jumpImpactUntil&&!jumpImpactConsumed;}}
    public bool MomentumActionWindow {get{return Time.time<shotUntil||Time.time<jumpImpactUntil;}}
    internal bool JumpFlightWindow {get{return Time.time<jumpImpactUntil;}}

    static float YawInertia(Rigidbody body){
        Quaternion principal=body.rotation*body.inertiaTensorRotation;
        Vector3 axis=Quaternion.Inverse(principal)*Vector3.up,tensor=body.inertiaTensor;
        return Mathf.Max(.001f,tensor.x*axis.x*axis.x+tensor.y*axis.y*axis.y+tensor.z*axis.z*axis.z);
    }
    static MomentumState MeasureMomentum(GBWrestler wrestler){
        MomentumState state=default;
        foreach(var body in wrestler.bodies){state.mass+=body.mass;state.center+=body.worldCenterOfMass*body.mass;state.velocity+=body.linearVelocity*body.mass;}
        state.mass=Mathf.Max(1,state.mass);state.center/=state.mass;state.velocity/=state.mass;
        foreach(var body in wrestler.bodies){
            Vector3 offset=body.worldCenterOfMass-state.center;
            state.inertia+=YawInertia(body)+body.mass*(offset.x*offset.x+offset.z*offset.z);
            Quaternion principal=body.rotation*body.inertiaTensorRotation;
            Vector3 intrinsic=principal*Vector3.Scale(body.inertiaTensor,Quaternion.Inverse(principal)*body.angularVelocity);
            state.yawMomentum+=Vector3.Cross(offset,(body.linearVelocity-state.velocity)*body.mass).y+intrinsic.y;
        }
        state.inertia=Mathf.Max(.1f,state.inertia);return state;
    }
    static MomentumState MeasureTarget(GBTarget target){
        if(target.wrestler)return MeasureMomentum(target.wrestler);
        return new MomentumState{mass=Mathf.Max(1,target.Body.mass),center=target.Body.worldCenterOfMass,velocity=target.Body.linearVelocity,inertia=YawInertia(target.Body)};
    }
    void MomentumReset(){
        spinBudget=shotForwardPeak=0;spinSampleAt=-10;grabMomentumAt=shotPressureUntil=jumpImpactUntil=0;jumpImpactConsumed=false;ShotDriveSpeed=4.2f;
        LastGrabMomentumImpulse=LastGrabApproachSpeed=LastShotApproachSpeed=LastThrowSpinCarry=LastShotPhysicalImpulse=LastBodyImpactApproachSpeed=LastBodyPhysicalImpulse=0;ShotImpactCount=BodyImpactCount=0;
        // Listen on the two weight-bearing bodies, not hands, head or feet.
        // Unity resolves the actual surface collision before we change stance.
        for(int n=0;n<2;n++){var relay=bodies[n].GetComponent<GBMomentumContact>();if(!relay)relay=bodies[n].gameObject.AddComponent<GBMomentumContact>();relay.owner=this;}
    }
    internal void BeginJumpMomentum(){jumpImpactUntil=Time.time+.9f;jumpImpactConsumed=false;}

    void BeginShotMomentum(){
        float forward=Mathf.Max(0,Vector3.Dot(MeasureMomentum(this).velocity,shotDirection));
        ShotDriveSpeed=Mathf.Clamp(forward+2.8f,4.7f,7.2f);shotForwardPeak=forward;
    }
    void MomentumTick(float dt){
        if(Shooting)shotForwardPeak=Mathf.Max(shotForwardPeak,Vector3.Dot(MeasureMomentum(this).velocity,shotDirection));
        if(Time.time<shotPressureUntil&&command.Has(GBInput.Brace)&&stamina>8){if(pressureUntil<=shotPressureUntil+.02f)pressureUntil=Mathf.Min(pressureUntil,Time.time+.03f);shotPressureUntil=0;}
        spinBudget*=Mathf.Exp(-dt*2.1f);
        if(Time.time-spinSampleAt>=.9f)spinBudget=0;
        if(Down||!grounded||Time.time<grabMomentumAt)return;
        var measured=MeasureMomentum(this);
        // Keep one recent physical peak, never sum rotations into free energy.
        float physical=Mathf.Clamp(measured.yawMomentum,-48,48);
        if(Mathf.Abs(physical)>Mathf.Abs(spinBudget)+.1f){spinBudget=physical;spinSampleAt=Time.time;}
    }
    // Apply a contact wrench across the articulated mass. Translation and yaw
    // reaction sum to the same impulse as a force at the contact, while avoiding
    // an enormous impulse applied only to a one-kilogram hand.
    static void BodyMomentumImpulse(GBWrestler wrestler,Vector3 impulse,Vector3 point){
        MomentumState state=MeasureMomentum(wrestler);
        float yawDelta=Vector3.Cross(point-state.center,impulse).y/state.inertia;
        foreach(var body in wrestler.bodies){
            Vector3 orbit=Vector3.Cross(Vector3.up*yawDelta,body.worldCenterOfMass-state.center);
            body.AddForce((impulse/state.mass+orbit)*body.mass,ForceMode.Impulse);
            Quaternion principal=body.rotation*body.inertiaTensorRotation;
            Vector3 angularImpulse=principal*Vector3.Scale(body.inertiaTensor,Quaternion.Inverse(principal)*(Vector3.up*yawDelta));
            body.AddTorque(angularImpulse,ForceMode.Impulse);
        }
    }
    static void TargetMomentumImpulse(GBTarget target,Vector3 impulse,Vector3 point){
        if(target.wrestler)BodyMomentumImpulse(target.wrestler,impulse,point);
        else if(!target.Body.isKinematic)target.Body.AddForceAtPosition(impulse,point,ForceMode.Impulse);
    }
    static float MomentumImpulseLimit(GBWrestler wrestler,Vector3 impulse,Vector3 point){
        MomentumState state=MeasureMomentum(wrestler);float yaw=Vector3.Cross(point-state.center,impulse).y/state.inertia,scale=1;
        foreach(var body in wrestler.bodies){
            Vector3 delta=impulse/state.mass+Vector3.Cross(Vector3.up*yaw,body.worldCenterOfMass-state.center),velocity=body.linearVelocity;
            const float speedLimit=12.5f;
            if((velocity+delta).sqrMagnitude<=speedLimit*speedLimit)continue;
            if(velocity.sqrMagnitude>=speedLimit*speedLimit){if((velocity+delta).sqrMagnitude>velocity.sqrMagnitude)scale=0;continue;}
            float a=delta.sqrMagnitude,b=2*Vector3.Dot(velocity,delta),c=velocity.sqrMagnitude-speedLimit*speedLimit;
            if(a>.0001f)scale=Mathf.Min(scale,Mathf.Clamp01((-b+Mathf.Sqrt(Mathf.Max(0,b*b-4*a*c)))/(2*a)));
        }return scale;
    }
    bool GrabMomentum(GBTarget target,Vector3 point,Rigidbody hand){
        if(target.Body.isKinematic||Time.time<grabMomentumAt||Mathf.Abs(RecentSpinMomentum)<1)return false;
        MomentumState source=MeasureMomentum(this),victim=MeasureTarget(target);
        Vector3 offset=point-source.center;
        Vector3 carry=Vector3.ClampMagnitude(Vector3.Cross(Vector3.up*(RecentSpinMomentum/source.inertia),offset),3.5f);
        if(carry.magnitude<.25f)return false;
        Vector3 direction=carry.normalized;
        LastGrabApproachSpeed=Vector3.Dot(hand.GetPointVelocity(point)-target.Body.GetPointVelocity(point),direction);
        // Subtract momentum the target already carries, so two grips do not
        // award the same spin twice. The spring handles residual relative speed.
        float closing=Mathf.Max(0,carry.magnitude-Vector3.Dot(target.Body.GetPointVelocity(point)-source.velocity,direction));
        float sourceLever=Vector3.Cross(offset,direction).y,victimLever=Vector3.Cross(point-victim.center,direction).y;
        float inverseMass=1/source.mass+1/victim.mass+sourceLever*sourceLever/source.inertia+victimLever*victimLever/victim.inertia;
        float budgetLimit=Mathf.Abs(RecentSpinMomentum)*.30f/Mathf.Max(.2f,Mathf.Abs(sourceLever));
        float energyLimit=Mathf.Sqrt(.30f*RecentSpinMomentum*RecentSpinMomentum/(source.inertia*Mathf.Max(.01f,inverseMass)));
        float impulseMagnitude=Mathf.Min(35,budgetLimit,energyLimit,closing*.7f/Mathf.Max(.01f,inverseMass));
        bool braced=target.wrestler&&target.wrestler.command.Has(GBInput.Brace)&&target.wrestler.grounded&&target.wrestler.stamina>8;
        if(braced)impulseMagnitude*=.35f;
        if(impulseMagnitude<.5f)return false;
        Vector3 impulse=direction*impulseMagnitude;
        float safe=MomentumImpulseLimit(this,-impulse,point);if(target.wrestler)safe=Mathf.Min(safe,MomentumImpulseLimit(target.wrestler,impulse,point));
        impulse*=safe;impulseMagnitude*=safe;if(impulseMagnitude<.5f)return false;
        TargetMomentumImpulse(target,impulse,point);BodyMomentumImpulse(this,-impulse,point);
        spinBudget=Mathf.Sign(spinBudget)*Mathf.Max(0,Mathf.Abs(spinBudget)-impulseMagnitude*Mathf.Abs(sourceLever));
        grabMomentumAt=Time.time+.9f;LastGrabMomentumImpulse=impulseMagnitude;
        Feedback(braced?"Spin caught - opponent braced the momentum":"Spin carried into the grip",.8f);return true;
    }
    Vector3 ThrowSpinMomentum(GBTarget target,Vector3 point,Vector3 victimVelocity){
        LastThrowSpinCarry=0;if(Mathf.Abs(RecentSpinMomentum)<1)return Vector3.zero;
        MomentumState source=MeasureMomentum(this),victim=MeasureTarget(target);
        Vector3 carry=Vector3.ClampMagnitude(Vector3.Cross(Vector3.up*(RecentSpinMomentum/source.inertia),victim.center-source.center),2.2f);
        if(carry.magnitude<.2f)return Vector3.zero;
        Vector3 direction=carry.normalized;
        float alreadyCarried=Mathf.Max(0,Vector3.Dot(victimVelocity-source.velocity,direction));
        float remaining=Mathf.Max(0,carry.magnitude-alreadyCarried);
        // A planted wrestler can steer the earned rotational carry into their
        // throw aim. Preserve its magnitude rather than adding a free boost.
        Vector3 steered=(command.Aim*.75f+direction*.66f).normalized;
        carry=steered*remaining;
        float lever=Mathf.Abs(Vector3.Cross(point-source.center,steered).y);
        float impulseLimit=Mathf.Abs(RecentSpinMomentum)*.8f/Mathf.Max(.3f,lever);
        carry=Vector3.ClampMagnitude(carry,Mathf.Min(1.6f,impulseLimit/victim.mass));
        float safe=MomentumImpulseLimit(this,-carry*victim.mass,point);
        if(target.wrestler)safe=Mathf.Min(safe,MomentumImpulseLimit(target.wrestler,carry*victim.mass,point));
        carry*=safe;
        LastThrowSpinCarry=carry.magnitude;spinBudget=0;grabMomentumAt=Time.time+.9f;return carry;
    }
    // Only a real torso/hip collision opens this path. Collision.impulse is
    // already delivered by PhysX: never add it again or mint a base impulse.
    internal void PhysicalMomentumContact(Collision collision){
        if(!GBApp.I||!GBApp.I.Authority||collision.contactCount==0||eliminated)return;
        var target=collision.rigidbody?collision.rigidbody.GetComponent<GBTarget>():null;
        if(!target||!target.wrestler||target.wrestler==this||target.wrestler.eliminated)return;
        var victim=target.wrestler;Vector3 point=collision.GetContact(0).point;
        var source=MeasureMomentum(this);var other=MeasureMomentum(victim);
        bool braced=(victim.command.Has(GBInput.Brace)||victim.Sprawling)&&victim.grounded&&victim.stamina>8;
        float actualImpulse=collision.impulse.magnitude;
        if(Shooting&&!shotImpactDone){
            // A low core drive can meet the opponent's hanging/guarding arms
            // before its trunk. These are still attached physical mass, and
            // must not shield an unbraced rival from the earned contact.
            if(target.region!="pelvis"&&target.region!="torso"&&!target.region.Contains("thigh")&&!target.region.Contains("shin")&&!target.region.Contains("arm")&&!target.region.Contains("hand"))return;
            if(Vector3.Dot(victim.Root.position-Root.position,shotDirection)<.05f)return;
            float closing=Mathf.Clamp(Mathf.Max(0,Mathf.Max(Vector3.Dot(source.velocity-other.velocity,shotDirection),shotForwardPeak-Vector3.Dot(other.velocity,shotDirection))),0,8);
            if(closing<.65f||actualImpulse<.3f)return;
            shotImpactDone=true;ShotImpactCount++;LastShotApproachSpeed=closing;LastShotPhysicalImpulse=actualImpulse;
            // The incoming speed was earned through ground drive. For a short
            // time neutral posture yields, so it cannot erase that collision.
            // Planted brace/sprawl keeps its footing motors and low support.
            float pressure=braced?0:Mathf.Clamp(.16f+closing*.055f,.20f,.55f);
            if(!braced){victim.shotPressureUntil=Time.time+pressure;victim.pressureUntil=Mathf.Max(victim.pressureUntil,victim.shotPressureUntil);}
            float loss=Mathf.Clamp(26+closing*5.2f,29,68)*(braced?.32f:1);
            victim.Knock(loss,Mathf.Clamp(.25f+closing*.045f,.30f,.6f));
            GBApp.I.Sfx.Play("hit",.6f);GBApp.I.Burst(point,color);Feedback(braced?"Shot resisted - change angle":"Shot connected - grab to finish",.7f);return;
        }
        if(!JumpImpactActive||Shooting||Down)return;
        // One genuine body impact per paid jump. Compare full mass velocity,
        // not a fast loose hand, and include descent onto the rival's trunk.
        Vector3 toward=(other.center-source.center).normalized;
        float closingJump=Mathf.Max(0,Vector3.Dot(source.velocity-other.velocity,toward));
        float collisionClosing=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,toward));
        closingJump=Mathf.Min(8,Mathf.Max(closingJump,collisionClosing));
        if(closingJump<1.8f||actualImpulse<1)return;
        jumpImpactConsumed=true;BodyImpactCount++;LastBodyImpactApproachSpeed=closingJump;LastBodyPhysicalImpulse=actualImpulse;
        if(!braced){victim.pressureUntil=Mathf.Max(victim.pressureUntil,Time.time+Mathf.Clamp((closingJump-1.2f)*.08f,.08f,.35f));}
        victim.Knock(Mathf.Clamp((closingJump-1.2f)*7+Mathf.Min(actualImpulse,80)*.05f,5,38)*(braced?.35f:1),.3f);
        Feedback(braced?"Body momentum met planted resistance":"Jump momentum carried into contact",.7f);
    }
}

// A real contact carries its existing momentum; this relay never applies force.
public sealed class GBMomentumContact:MonoBehaviour {
    public GBWrestler owner;
    void OnCollisionEnter(Collision collision){if(owner)owner.PhysicalMomentumContact(collision);}
    void OnCollisionStay(Collision collision){if(owner)owner.PhysicalMomentumContact(collision);}
}

