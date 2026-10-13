using UnityEngine;

public partial class GBWrestler {
    GBWrestler pinTarget;
    bool pinFront;
    Vector3 pinApproach;
    bool pinAlignedGrip;
    bool ClearingCover {get{return false;}}
    bool remoteCover,remoteLeftShoulder,remoteRightShoulder;
    int remotePinPlayer=-1;
    public float PinPressureBias {get;private set;}
    public float LeftPinLoad {get;private set;}
    public float RightPinLoad {get;private set;}
    public float TopControlLeverage {get;private set;}
    public float TopControlHipBias {get;private set;}
    public bool TopControlSupported {get;private set;}

    public GBWrestler PinTarget {
        get {
            if(GBApp.I&&!GBApp.I.Authority){foreach(var w in GBApp.I.Wrestlers)if(w.player==remotePinPlayer)return w;return null;}
            return pinTarget;
        }
    }
    public bool IsCovering {
        get {
            if(GBApp.I&&!GBApp.I.Authority)return remoteCover;
            if(!pinTarget||Down||eliminated||stamina<3||!command.Has(GBInput.Pin)||!HoldingWrestler(pinTarget))return false;
            // Pin eligibility belongs to the grounded rival's shoulder
            // contacts. The attacker needs a live nearby controlling grip,
            // not a chest-over-chest pose; a side control can count too.
            return Vector3.Distance(Root.position,pinTarget.Root.position)<=1.55f;
        }
    }
    float CoverSurfaceGap(){
        if(!pinTarget)return float.PositiveInfinity;
        var a=bodies[1].GetComponent<Collider>();var b=pinTarget.bodies[1].GetComponent<Collider>();
        Vector3 pa=a.ClosestPoint(pinTarget.bodies[1].position),pb=b.ClosestPoint(pa);pa=a.ClosestPoint(pb);
        return Vector3.Distance(pa,pb);
    }
    public string CoverGeometry {
        get {
            string own="root="+Root.position+" chest="+bodies[1].position+" frontDown="+Vector3.Dot(bodies[1].rotation*Vector3.forward,Vector3.down).ToString("0.000");
            if(!pinTarget)return "no target C="+command.Has(GBInput.Pin)+" hands="+GripCount+" down="+Down+" "+own;
            Vector3 delta=bodies[1].position-pinTarget.bodies[1].position;
            return "target="+pinTarget.player+" liveHold="+HoldingWrestler(pinTarget)+" C="+command.Has(GBInput.Pin)+" down="+Down+" stamina="+stamina.ToString("0.0")+" validCover="+IsCovering+" deltaY="+delta.y.ToString("0.000")+" horizontal="+Vector3.ProjectOnPlane(delta,Vector3.up).magnitude.ToString("0.000")+" surfaceGap="+CoverSurfaceGap().ToString("0.000")+" "+own+" victimChest="+pinTarget.bodies[1].position;
        }
    }
    public bool IsBeingPinned {
        get {if(!GBApp.I)return false;foreach(var w in GBApp.I.Wrestlers)if(w!=this&&w.PinTarget==this&&w.IsCovering)return true;return false;}
    }
    public bool PinControlled {
        get {
            if(!GBApp.I||GBApp.I.CurrentMode!=1)return false;
            // The target was earned while vulnerable. Balance regeneration
            // cannot reactivate a standing servo halfway through the physical
            // turn; a real grip/C/range/stamina loss still ends control.
            foreach(var w in GBApp.I.Wrestlers)if(w!=this&&w.pinTarget==this&&w.HoldingWrestler(this)&&Vector3.Distance(w.bodies[1].position,bodies[1].position)<1.35f)return true;
            return false;
        }
    }
    public void RemotePinState(bool covering,int targetPlayer,bool left,bool right){remoteCover=covering;remotePinPlayer=targetPlayer;remoteLeftShoulder=left;remoteRightShoulder=right;}
    void PinReset(){pinTarget=null;pinFront=false;pinApproach=Vector3.forward;pinAlignedGrip=false;remoteCover=remoteLeftShoulder=remoteRightShoulder=false;remotePinPlayer=-1;PinPressureBias=LeftPinLoad=RightPinLoad=TopControlHipBias=0;TopControlLeverage=1;TopControlSupported=false;}
    bool HoldingWrestler(GBWrestler target){foreach(var h in hands)if(h!=null&&h.joint&&h.target&&h.target.wrestler==target)return true;return false;}

    public bool PhysicallyDownForPin {
        get {RaycastHit floor;return PinFloor(bodies[1].position,out floor)&&bodies[1].position.y<floor.point.y+.8f&&Vector3.Dot(bodies[1].rotation*Vector3.up,Vector3.up)<.5f;}
    }
    void PinRefresh(){
        var previous=pinTarget;
        pinTarget=null;
        if(!GBApp.I||GBApp.I.CurrentMode!=1||!command.Has(GBInput.Pin)||Down||hanging||eliminated||stamina<3)return;
        // Balance can regenerate during a real turn. Retain that commitment
        // only while the player still holds C and an actual nearby grip.
        RaycastHit retainedFloor;
        if(previous&&!previous.eliminated&&previous.PhysicallyDownForPin&&HoldingWrestler(previous)&&Vector3.Distance(Root.position,previous.Root.position)<=1.55f&&PinFloor(previous.bodies[1].position,out retainedFloor)){pinTarget=previous;return;}
        foreach(var hand in hands){
            if(hand==null||!hand.joint||!hand.target||!hand.target.wrestler)continue;
            var victim=hand.target.wrestler;if(victim==this||victim.eliminated||Vector3.Distance(Root.position,victim.Root.position)>1.55f)continue;
            RaycastHit floor;if(!PinFloor(victim.bodies[1].position,out floor))continue;
            bool offBalance=victim.PhysicallyDownForPin;
            if(offBalance){
                pinTarget=victim;pinFront=Vector3.Dot(victim.bodies[1].rotation*Vector3.forward,Vector3.up)<-.35f;
                pinApproach=Vector3.ProjectOnPlane(victim.bodies[1].position-Root.position,Vector3.up).normalized;
                Vector3 gripRight=Vector3.zero;int wrapped=0;
                for(int h=0;h<2;h++){
                    var grip=hands[h];if(!grip.joint||!grip.target||grip.target.wrestler!=victim)continue;
                    var shape=victim.bodies[1].GetComponent<CapsuleCollider>();if(!shape)continue;
                    Vector3 axis=shape.direction==0?Vector3.right:shape.direction==1?Vector3.up:Vector3.forward;
                    Vector3 contact=grip.target.transform.TransformPoint(grip.localAnchor);
                    Vector3 local=shape.transform.InverseTransformPoint(contact)-shape.center;
                    Vector3 radial=Vector3.ProjectOnPlane(local,axis);
                    radial=Vector3.ProjectOnPlane(shape.transform.TransformVector(radial),Vector3.up);
                    if(radial.sqrMagnitude<.01f)continue;
                    Vector3 right=radial.normalized*(h==0?-1:1);
                    if(wrapped>0&&Vector3.Dot(gripRight.normalized,right)<.5f)continue;
                    gripRight+=right;wrapped++;
                }
                // Align the shoulders with the original flank grips. A side
                // approach otherwise folds the far wrist under the chest.
                pinAlignedGrip=wrapped>0;
                if(pinAlignedGrip)pinApproach=Vector3.Cross(gripRight.normalized,Vector3.up).normalized;
                if(pinApproach.sqrMagnitude<.1f)pinApproach=facing;break;
            }
        }
    }
    static bool PinFloor(Vector3 point,out RaycastHit hit){
        return Physics.Raycast(point+Vector3.up*.4f,Vector3.down,out hit,2.4f,1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.75f;
    }
    public bool ShoulderContact(out bool left,out bool right){
        if(GBApp.I&&!GBApp.I.Authority){left=remoteLeftShoulder;right=remoteRightShoulder;return left&&right;}
        left=right=false;
        float facingMat=Vector3.Dot(bodies[1].rotation*Vector3.forward,Vector3.up);
        if(Mathf.Abs(facingMat)<.6f)return false;
        bool front=facingMat<0;
        left=ShoulderOnMat(-1,front);right=ShoulderOnMat(1,front);return left&&right;
    }
    public Vector3 ShoulderPoint(int side,bool front){
        var pad=shoulderPads[(front?0:2)+(side<0?0:1)];
        return bodies[1].position+bodies[1].rotation*pad.center-Vector3.up*pad.radius;
    }
    bool ShoulderOnMat(int side,bool front){
        // Either chest or back may face the mat. Each shoulder independently
        // needs real floor contact; side-on bodies and lifted shoulders fail.
        // Measure the bottom of the actual compound shoulder sphere.
        Vector3 point=ShoulderPoint(side,front);
        RaycastHit hit;if(!Physics.Raycast(point+Vector3.up*.16f,Vector3.down,out hit,.30f,1,QueryTriggerInteraction.Ignore)||hit.normal.y<.75f)return false;
        float gap=point.y-hit.point.y;return gap>=-.035f&&gap<.09f;
    }
    // Implicit angular damping uses each principal inertia, avoiding a stiff
    // explicit damper on a light limb. Every correction remains capped torque.
    static Vector3 PinTorque(Rigidbody body,Quaternion target,float spring,float damping,float cap){
        Quaternion q=target*Quaternion.Inverse(body.rotation);if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
        float angle;Vector3 axis;q.ToAngleAxis(out angle,out axis);if(angle>180)angle-=360;
        if(axis.sqrMagnitude<.0001f)return Vector3.zero;
        Quaternion principal=body.rotation*body.inertiaTensorRotation;Quaternion inverse=Quaternion.Inverse(principal);
        Vector3 error=inverse*(axis.normalized*angle*Mathf.Deg2Rad),velocity=inverse*body.angularVelocity,tensor=body.inertiaTensor;
        float dt=Time.fixedDeltaTime,softness=damping*dt+spring*dt*dt;
        Vector3 raw=error*spring-velocity*(damping+spring*dt);
        raw.x=tensor.x>0?raw.x/(1+softness/tensor.x):0;raw.y=tensor.y>0?raw.y/(1+softness/tensor.y):0;raw.z=tensor.z>0?raw.z/(1+softness/tensor.z):0;
        return Vector3.ClampMagnitude(principal*raw,cap);
    }
    void PinTick(float dt){
        PinPressureBias=LeftPinLoad=RightPinLoad=TopControlHipBias=0;TopControlLeverage=1;TopControlSupported=false;
        if(!GBApp.I||GBApp.I.CurrentMode!=1)return;
        PinRefresh();BridgeAndRoll(dt);
        if(!pinTarget)return;
        var victim=pinTarget;RaycastHit floor;if(!PinFloor(victim.bodies[1].position,out floor)){pinTarget=null;return;}
        // C controls the grounded opponent from the player's real position.
        // Normal low-stance locomotion/arm motors remain active; there is no
        // automatic relocation or required chest-over-chest pose.
        Vector3 direction=facing;
        float strength=Mathf.Lerp(.45f,1,StaminaFraction);
        bool resisting=victim.ScrambleReady&&victim.command.Has(GBInput.Brace)&&victim.command.Move.sqrMagnitude>.05f&&victim.stamina>4;
        bool leftShoulder,rightShoulder;
        if(victim.ShoulderContact(out leftShoulder,out rightShoulder))pinFront=Vector3.Dot(victim.bodies[1].rotation*Vector3.forward,Vector3.up)<0;
        float control=strength*(GripCount>1?1:.9f)*(resisting?.30f:1);
        UpdateTopControl(victim,floor.point.y);
        Vector3 spine=Vector3.ProjectOnPlane(victim.bodies[1].rotation*Vector3.up,Vector3.up);if(spine.sqrMagnitude<.04f)spine=-direction;spine.Normalize();
        Quaternion faceOnMat=Quaternion.LookRotation(pinFront?Vector3.down:Vector3.up,spine);
        Vector3 torsoTorque=PinTorque(victim.bodies[1],faceOnMat,2800*TopControlLeverage,65,150*control),hipTorque=PinTorque(victim.Root,faceOnMat,1600*TopControlLeverage,50,95*control);
        victim.bodies[1].AddTorque(torsoTorque,ForceMode.Force);victim.Root.AddTorque(hipTorque,ForceMode.Force);bodies[1].AddTorque(-torsoTorque-hipTorque,ForceMode.Force);
        float pressure=Mathf.Clamp((victim.bodies[1].position.y-floor.point.y-.29f)*350+Mathf.Max(0,victim.bodies[1].linearVelocity.y)*35,0,240)*control;
        if(IsCovering)pressure+=70*control;
        pressure=Mathf.Min(310*control,pressure*TopControlLeverage);
        victim.bodies[1].AddForce(Vector3.down*pressure,ForceMode.Force);bodies[1].AddForce(Vector3.up*pressure,ForceMode.Force);
        if(IsCovering){
            // Apply cover pressure where the actual shoulders rise, rather
            // than relying on center-of-mass height to detect a lifted pad.
            // The attacker receives the complete reaction at each same point.
            Vector3 leftPoint=victim.ShoulderPoint(-1,pinFront),rightPoint=victim.ShoulderPoint(1,pinFront);
            float low=Mathf.Min(leftPoint.y,rightPoint.y);
            float preload=Mathf.Clamp((low-floor.point.y)*1600+35,35,220);
            float leftLoad=Mathf.Clamp((preload+(leftPoint.y-low)*4800+Mathf.Max(0,victim.bodies[1].GetPointVelocity(leftPoint).y)*45)*TopControlLeverage,0,600);
            float rightLoad=Mathf.Clamp((preload+(rightPoint.y-low)*4800+Mathf.Max(0,victim.bodies[1].GetPointVelocity(rightPoint).y)*45)*TopControlLeverage,0,600);
            // Movement steers existing pressure toward the chosen shoulder.
            // Keep the neutral preload and shared weight limit: leaning does
            // not weaken an ordinary pin or invent additional body weight.
            PinPressureBias=Mathf.Clamp(Vector3.Dot(Vector3.ClampMagnitude(command.Move,1),(rightPoint-leftPoint).normalized)+TopControlHipBias,-1,1);
            ShiftPinPressure(ref leftLoad,ref rightLoad,PinPressureBias);
            // A full body's weight is the shared upper bound. Prefer the raised
            // shoulder, but also load a pair lifted together by the wrist hold.
            float scale=Mathf.Min(1,650/Mathf.Max(1,leftLoad+rightLoad))*control;
            LeftPinLoad=leftLoad*scale;RightPinLoad=rightLoad*scale;
            for(int side=-1;side<=1;side+=2){
                Vector3 point=side<0?leftPoint:rightPoint;
                float load=(side<0?leftLoad:rightLoad)*scale;
                victim.bodies[1].AddForceAtPosition(Vector3.down*load,point,ForceMode.Force);
                bodies[1].AddForceAtPosition(Vector3.up*load,point,ForceMode.Force);
            }
        }
        stamina=Mathf.Max(0,stamina-dt*3);Feedback(IsCovering?"Controlling - keep both shoulders on the mat":"Maintain your grip and control",.2f);
    }
    void UpdateTopControl(GBWrestler victim,float floor){
        // Position improves an ordinary hold, rather than becoming a second
        // pin qualification. A standing side-control keeps its old leverage.
        // Actual low, supported hips connect body weight through the chest;
        // hanging far away on a wrist cannot create the same pressure.
        Vector3 weightPoint=Root.position*.6f+bodies[1].position*.4f;
        float distance=Vector3.ProjectOnPlane(weightPoint-victim.bodies[1].position,Vector3.up).magnitude;
        float close=1-Mathf.InverseLerp(.55f,1.2f,distance);
        float lowHips=1-Mathf.InverseLerp(.70f,1.05f,Root.position.y-floor);
        for(int part=9;part<=14;part++){
            var collider=bodies[part].GetComponent<Collider>();if(!collider)continue;
            Vector3 sole=new Vector3(collider.bounds.center.x,collider.bounds.min.y,collider.bounds.center.z);
            RaycastHit hit;if(Physics.Raycast(sole+Vector3.up*.04f,Vector3.down,out hit,.10f,1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.75f&&Mathf.Abs(sole.y-hit.point.y)<.06f){TopControlSupported=true;break;}
        }
        float far=Mathf.InverseLerp(.90f,1.55f,distance);
        TopControlLeverage=Mathf.Clamp(1+.18f*close*lowHips*(TopControlSupported?1:0)-.15f*far-(TopControlSupported?0:.10f),.75f,1.18f);
        Vector3 shoulderAxis=(victim.ShoulderPoint(1,pinFront)-victim.ShoulderPoint(-1,pinFront)).normalized;
        TopControlHipBias=Mathf.Clamp(Vector3.Dot(weightPoint-victim.bodies[1].position,shoulderAxis)/.40f,-1,1)*.45f*close*lowHips*(TopControlSupported?1:0);
    }
    void BridgeAndRoll(float dt){
        if(!ScrambleReady||!command.Has(GBInput.Brace)||command.Move.sqrMagnitude<.05f||stamina<4||eliminated||(!Down&&!PinControlled&&Vector3.Dot(bodies[1].rotation*Vector3.up,Vector3.up)>.5f))return;
        RaycastHit floor;if(!PinFloor(Root.position,out floor))return;
        float effort=Mathf.Lerp(.35f,1,StaminaFraction),height=Mathf.Clamp01((floor.point.y+.70f-Root.position.y)/.45f);
        // A fresh bridge must lift against the coverer's weight as well as
        // the defender's own articulated mass. Effort still drains stamina.
        Root.AddForce(Vector3.up*(1600*height*effort),ForceMode.Force);bodies[1].AddForce(Vector3.up*(450*height*effort),ForceMode.Force);
        Vector3 move=command.Move.normalized,axis=Vector3.Cross(Vector3.up,move);
        bodies[1].AddTorque(Vector3.ClampMagnitude(axis*210*effort-bodies[1].angularVelocity*8,220),ForceMode.Force);Root.AddTorque(axis*85*effort,ForceMode.Force);
        Vector3 velocity=Vector3.zero;foreach(var b in bodies)velocity+=b.linearVelocity*b.mass;velocity/=Mathf.Max(1,totalMass);velocity.y=0;
        Vector3 force=Vector3.ClampMagnitude((move*2.2f-velocity)*totalMass*4,650)*effort;foreach(var b in bodies)b.AddForce(force*(b.mass/Mathf.Max(1,totalMass)),ForceMode.Force);
        stamina=Mathf.Max(0,stamina-dt*13*ScrambleStaminaMultiplier);Feedback("Bridging and rolling - lift a shoulder to escape",.2f);
    }
    internal static void ShiftPinPressure(ref float left,ref float right,float bias){
        bias=Mathf.Clamp(bias,-1,1);
        float available=bias>=0?left:right;
        float transfer=Mathf.Min((left+right)*.30f*Mathf.Abs(bias),Mathf.Max(0,available-35));
        if(bias>=0){left-=transfer;right+=transfer;}else{right-=transfer;left+=transfer;}
    }
}
