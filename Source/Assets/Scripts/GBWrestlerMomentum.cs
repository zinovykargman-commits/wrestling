using UnityEngine;

public partial class GBWrestler {
    struct MomentumState {
        public float mass,inertia,yawMomentum;
        public Vector3 center,velocity;
    }
    float spinBudget,spinSampleAt=-10,grabMomentumAt,shotPressureUntil,shotForwardPeak;
    public float ShotDriveSpeed {get;private set;}=4.2f;
    public float RecentSpinMomentum {get{return Time.time-spinSampleAt<.9f?spinBudget:0;}}
    public float SpinCarry {get{return Mathf.Clamp01(Mathf.Abs(RecentSpinMomentum)/48);}}
    public float MomentumSpeed {get{return Vector3.ProjectOnPlane(MeasureMomentum(this).velocity,Vector3.up).magnitude;}}
    public float LastGrabMomentumImpulse {get;private set;}
    public float LastGrabApproachSpeed {get;private set;}
    public float LastShotApproachSpeed {get;private set;}
    public float LastThrowSpinCarry {get;private set;}

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
    void MomentumReset(){spinBudget=shotForwardPeak=0;spinSampleAt=-10;grabMomentumAt=shotPressureUntil=0;ShotDriveSpeed=4.2f;LastGrabMomentumImpulse=LastGrabApproachSpeed=LastShotApproachSpeed=LastThrowSpinCarry=0;}
    void BeginShotMomentum(){
        float forward=Mathf.Max(0,Vector3.Dot(MeasureMomentum(this).velocity,shotDirection));
        ShotDriveSpeed=Mathf.Clamp(forward+2.8f,4.2f,7);shotForwardPeak=forward;
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
    void ShotMomentum(GBTarget target,Vector3 point,bool braced){
        Vector3 relative=Root.GetPointVelocity(point)-target.Body.GetPointVelocity(point);
        MomentumState source=MeasureMomentum(this),victim=MeasureTarget(target);
        // Contact can already have absorbed velocity before this callback.
        // Use the measured approach peak, never a commanded or wished speed.
        float closing=Mathf.Clamp(Mathf.Max(Vector3.Dot(relative,shotDirection),shotForwardPeak-Vector3.Dot(victim.velocity,shotDirection)),0,8);LastShotApproachSpeed=closing;
        float reducedMass=source.mass*victim.mass/(source.mass+victim.mass);
        float impulseMagnitude=Mathf.Min(180,70+closing*reducedMass*.5f)*(braced?.45f:1);
        Vector3 impulse=shotDirection*impulseMagnitude;
        float safe=MomentumImpulseLimit(this,-impulse,point);if(target.wrestler)safe=Mathf.Min(safe,MomentumImpulseLimit(target.wrestler,impulse,point));impulse*=safe;
        TargetMomentumImpulse(target,impulse,point);BodyMomentumImpulse(this,-impulse,point);
        if(!braced){
            // A faster real collision unloads the target's planted stance for
            // a short stumble, allowing earned momentum to carry through.
            float pressure=Mathf.Clamp((closing-2.5f)*.075f,0,.32f);
            target.wrestler.shotPressureUntil=Time.time+pressure;
            target.wrestler.pressureUntil=Mathf.Max(target.wrestler.pressureUntil,target.wrestler.shotPressureUntil);
        }
        target.wrestler.Knock(Mathf.Min(48,20+closing*3)*(braced?.45f:1),.35f);
    }
}
