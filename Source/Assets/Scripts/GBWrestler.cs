using UnityEngine;
using System;
using System.Collections.Generic;

public partial class GBWrestler:MonoBehaviour {
    public int player;
    public string playerName;
    public Color color;
    public Rigidbody[] bodies=new Rigidbody[15];
    public ConfigurableJoint[] joints=new ConfigurableJoint[15];
    public Quaternion[] poses=new Quaternion[15];
    Transform[] seams=new Transform[15];float[] seamWidths=new float[15];Vector3[] seamLocal=new Vector3[15],seamParent=new Vector3[15];
    public Vector3[] initial=new Vector3[15];
    public int[] parents={-1,0,1,1,3,4,1,6,7,0,9,10,0,12,13};
    public GBInput command;
    public const float MaxStamina=155.25f;
    public const float ScrambleStaminaMultiplier=1.43f;
    public float StaminaFraction {get{return Mathf.Clamp01(stamina/MaxStamina);}}
    public float stamina=MaxStamina,balance=100,downUntil,recoveryUntil,pinProgress;
    public bool dummy,grounded,hanging,eliminated;
    public Vector3 facing=Vector3.forward;
    public Hand[] hands=new Hand[2];
    public float lastImpact,moveDistance,pressureUntil,stumbleUntil,EscapeUntil;
    public bool Stumbling {get{return !Down&&Time.time<stumbleUntil;}}
    public bool RecoveringFromCarry {get{return carriedFlight||Time.time<carriedRecoveryUntil;}}
    readonly Vector3[] plantedFeet=new Vector3[2],stepStarts=new Vector3[2],stepGoals=new Vector3[2];
    readonly float[] stepBegin={-10,-10};bool feetInitialized;int nextStep;float totalMass;
    int oldFlags;float strikeAt,jumpAt,jumpRequestUntil,lastFootSupport=-10,climbUntil,liftCharge,carriedRecoveryUntil;bool carriedFlight;Vector3 spawn,lastRoot,climbGoal;Quaternion facingRotation;
    PhysicsMaterial standingBoots,groundCloth;
    public SphereCollider[] shoulderPads=new SphereCollider[4];
    public class Hand {
        public GBGrip joint;public GBTarget target,candidate;public Vector3 localAnchor,aimBias;public LineRenderer line;
        public bool releaseRequired,torsoWrap;
        public string Label {get{return target?target.region:candidate?"reach "+candidate.region:"free";}}
    }
    public Rigidbody Root {get{return bodies[0];}}
    public bool Down {get{return Time.time<downUntil;}}
    public int GripCount {get{return (hands[0].joint?1:0)+(hands[1].joint?1:0);}}
    public void Build(int index,Vector3 at,Color outfit,bool isDummy=false) {
        SprawlReset();
        player=index;playerName=index==0?"EMBER":"COBALT";color=outfit;dummy=isDummy;spawn=at;
        initial=new[]{new Vector3(0,1.04f,0),new Vector3(0,1.375f,0),new Vector3(0,1.88f,0),new Vector3(-.29f,1.425f,0),new Vector3(-.31f,1.09f,0),new Vector3(-.31f,.87f,.015f),new Vector3(.29f,1.425f,0),new Vector3(.31f,1.09f,0),new Vector3(.31f,.87f,.015f),new Vector3(-.13f,.78f,0),new Vector3(-.13f,.35f,0),new Vector3(-.13f,.085f,.09f),new Vector3(.13f,.78f,0),new Vector3(.13f,.35f,0),new Vector3(.13f,.085f,.09f)};
        float[] mass={11,14,5,3,2,1,3,2,1,6,4,2,6,4,2};
        float[] radius={.23f,.26f,.19f,.105f,.09f,.095f,.105f,.09f,.095f,.13f,.11f,.115f,.13f,.11f,.115f};
        float[] height={.32f,.52f,.38f,.31f,.31f,.19f,.31f,.31f,.19f,.36f,.36f,.17f,.36f,.36f,.17f};
        string[] names={"pelvis","torso","head","left upper arm","left forearm","left hand","right upper arm","right forearm","right hand","left thigh","left shin","left foot","right thigh","right shin","right foot"};
        var skin=GBArt.Mat("Skin"+index,isDummy?new Color(.55f,.49f,.43f):index==0?new Color(.74f,.45f,.3f):new Color(.39f,.24f,.2f),0,.4f);
        var shirt=GBArt.Mat("Outfit"+index,outfit,0,.3f);var dark=GBArt.Mat("Graphite",new Color(.055f,.065f,.085f),.05f,.25f);var white=GBArt.Mat("Cream",new Color(.92f,.88f,.72f),0,.25f);
        var phys=new PhysicsMaterial("Wrestler cloth"){dynamicFriction=.38f,staticFriction=.45f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average};
        var boots=new PhysicsMaterial("Planted wrestling boots"){dynamicFriction=.65f,staticFriction=.95f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average};
        standingBoots=boots;groundCloth=phys;
        for(int n=0;n<15;n++) {
            var go=new GameObject(names[n]);go.transform.SetParent(transform,false);go.transform.position=at+initial[n];go.layer=8;
            var rb=go.AddComponent<Rigidbody>();rb.mass=mass[n];rb.linearDamping=.08f;rb.angularDamping=.3f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.maxAngularVelocity=16;rb.maxDepenetrationVelocity=6;rb.solverIterations=64;rb.solverVelocityIterations=24;bodies[n]=rb;
            if(n==11||n==14){var c=go.AddComponent<BoxCollider>();c.size=new Vector3(.18f,.17f,.30f);c.material=boots;GBArt.Box(go.transform,"Wrestling boot",Vector3.zero,c.size,dark,false);GBArt.Box(go.transform,"Boot sole",new Vector3(0,-.07f,.01f),new Vector3(.225f,.04f,.4f),white,false);}
            else {var c=go.AddComponent<CapsuleCollider>();c.radius=radius[n];c.height=n==1?.80f:n==0?.54f:height[n];c.material=phys;
                var mat=n==2||n==5||n==8?skin:n==0||n>=9?dark:shirt;
                if(n==0||n==1)GBBodyVisual.Body(go.transform,n==1,mat);else GBArt.Shape(go.transform,"Body mesh",n==2||n==5||n==8?PrimitiveType.Sphere:PrimitiveType.Capsule,Vector3.zero,new Vector3(radius[n]*2,height[n]*.5f,radius[n]*2),mat);
                if(n==2) go.transform.GetChild(0).localScale=Vector3.one*radius[n]*2;
                if(n==5||n==8)go.transform.GetChild(0).localScale=new Vector3(.19f,.19f,.16f);
            }
            var target=go.AddComponent<GBTarget>();target.wrestler=this;target.region=names[n];target.sound="cloth";
            var contact=go.AddComponent<GBContact>();contact.wrestler=this;contact.bodyIndex=n;
            poses[n]=Quaternion.identity;
        }
        // Four real shoulder supports compound into the existing torso body.
        // A single rounded capsule rocks on its center when chest/back down.
        for(int face=0;face<2;face++)for(int side=0;side<2;side++){
            var pad=bodies[1].gameObject.AddComponent<SphereCollider>();pad.radius=.055f;
            pad.center=new Vector3(side==0?-.12f:.12f,.18f,face==0?.22f:-.22f);
            pad.sharedMaterial=phys;shoulderPads[face*2+side]=pad;
        }
        // Preserve each anatomical region's original mass center.
        bodies[1].centerOfMass=Vector3.zero;
        for(int n=1;n<15;n++) {
            var joint=bodies[n].gameObject.AddComponent<ConfigurableJoint>();joints[n]=joint;joint.connectedBody=bodies[parents[n]];
            joint.autoConfigureConnectedAnchor=false;
            Vector3 anchor;
            switch(n){case 1:anchor=at+new Vector3(0,1.14f,0);break;case 2:anchor=at+new Vector3(0,1.67f,0);break;case 3:anchor=at+new Vector3(-.25f,1.57f,0);break;case 4:anchor=at+new Vector3(-.31f,1.255f,0);break;case 5:anchor=at+new Vector3(-.31f,.96f,0);break;case 6:anchor=at+new Vector3(.25f,1.57f,0);break;case 7:anchor=at+new Vector3(.31f,1.255f,0);break;case 8:anchor=at+new Vector3(.31f,.96f,0);break;case 9:anchor=at+new Vector3(-.13f,1.0f,0);break;case 10:anchor=at+new Vector3(-.13f,.56f,0);break;case 11:anchor=at+new Vector3(-.13f,.15f,0);break;case 12:anchor=at+new Vector3(.13f,1.0f,0);break;case 13:anchor=at+new Vector3(.13f,.56f,0);break;default:anchor=at+new Vector3(.13f,.15f,0);break;}
            joint.anchor=bodies[n].transform.InverseTransformPoint(anchor);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(anchor);
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit=new SoftJointLimit{limit=n==2?-40:-85};joint.highAngularXLimit=new SoftJointLimit{limit=n==2?40:85};joint.angularYLimit=new SoftJointLimit{limit=n==1?30:65};joint.angularZLimit=new SoftJointLimit{limit=n==1?30:70};
            // Joint-frame twist is opposite the child's physical bend here.
            // Allow positive knee flexion, with only ten degrees of extension.
            if(n==10||n==13){joint.lowAngularXLimit=new SoftJointLimit{limit=-145};joint.highAngularXLimit=new SoftJointLimit{limit=5};joint.angularYLimit=joint.angularZLimit=new SoftJointLimit{limit=20};}
            if(n==11||n==14){joint.lowAngularXLimit=new SoftJointLimit{limit=-55};joint.highAngularXLimit=new SoftJointLimit{limit=25};joint.angularYLimit=joint.angularZLimit=new SoftJointLimit{limit=20};}
            if(n==3||n==6){joint.lowAngularXLimit=new SoftJointLimit{limit=-65};joint.highAngularXLimit=new SoftJointLimit{limit=165};}
            // Reaching a nearby held contact needs a folded elbow, rather than
            // stopping at the old 85 degree limit and propping up the chest.
            if(n==4||n==7){joint.lowAngularXLimit=new SoftJointLimit{limit=-5};joint.highAngularXLimit=new SoftJointLimit{limit=150};joint.angularYLimit=joint.angularZLimit=new SoftJointLimit{limit=20};}
            joint.rotationDriveMode=RotationDriveMode.Slerp;joint.slerpDrive=new JointDrive{positionSpring=n==1?1800:650,positionDamper=n==1?100:42,maximumForce=n==1?180:70};joint.enablePreprocessing=false;joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.005f;joint.projectionAngle=5;
            joint.breakForce=float.PositiveInfinity;joint.breakTorque=float.PositiveInfinity;
            joint.massScale=joint.connectedMassScale=1;
            float seamRadius=n==1?.23f:n==2?.105f:n==3||n==6?.15f:n==9||n==12?.145f:n==10||n==13?.12f:n==5||n==8?.085f:.095f;
            var seamMaterial=n==2||n==5||n==8?skin:n>=9||n==1?dark:shirt;
            GBArt.Ball(bodies[n].transform,"Continuous "+names[n]+" joint",joint.anchor,Vector3.one*seamRadius*2,seamMaterial);
            GBArt.Ball(bodies[parents[n]].transform,"Connected "+names[n]+" joint",joint.connectedAnchor,Vector3.one*seamRadius*2,seamMaterial);
            seams[n]=GBArt.Shape(transform,"Flexible "+names[n]+" connection",PrimitiveType.Cylinder,Vector3.zero,Vector3.one,seamMaterial).transform;seamWidths[n]=seamRadius;seamLocal[n]=joint.anchor;seamParent[n]=joint.connectedAnchor;
        }
        // Neighbouring capsules overlap at their anatomical joint. Only those
        // fourteen connected pairs are excluded: hands, forearms, opposite
        // limbs and the torso otherwise stop each other through real contact.
        for(int n=1;n<15;n++)foreach(var child in bodies[n].GetComponents<Collider>())foreach(var parent in bodies[parents[n]].GetComponents<Collider>())Physics.IgnoreCollision(child,parent);
        GBArt.Text(bodies[1].transform,(index+1).ToString("D2"),new Vector3(0,.01f,.208f),.10f,Color.white,Quaternion.Euler(0,180,0));
        GBBodyVisual.Waistband(bodies[0].transform,shirt);
        for(int s=-1;s<=1;s+=2){GBArt.Ball(bodies[2].transform,"Eye",new Vector3(s*.072f,.025f,.165f),new Vector3(.043f,.036f,.022f),white);GBArt.Ball(bodies[2].transform,"Pupil",new Vector3(s*.072f,.023f,.18f),new Vector3(.019f,.025f,.014f),dark);GBArt.Box(bodies[2].transform,"Brow",new Vector3(s*.072f,.072f,.167f),new Vector3(.071f,.025f,.022f),dark,false);}
        GBArt.Box(bodies[2].transform,"Mouth",new Vector3(0,-.065f,.174f),new Vector3(.063f,.017f,.018f),dark,false);
        GBArt.Ball(bodies[2].transform,"Hair",new Vector3(0,.12f,-.016f),new Vector3(.345f,.17f,.32f),dark);
        for(int h=0;h<2;h++) {
            hands[h]=new Hand();var line=new GameObject("Hand reach").AddComponent<LineRenderer>();line.transform.SetParent(transform,false);line.positionCount=2;line.startWidth=.026f;line.endWidth=.012f;line.material=GBArt.Mat("Grip glow"+index,color,0,.1f,true);line.enabled=false;hands[h].line=line;
            for(int f=0;f<3;f++)GBArt.Box(bodies[h==0?5:8].transform,"Finger",new Vector3((f-1)*.038f,-.025f,.066f),new Vector3(.032f,.08f,.045f),skin,false);
        }
        lastRoot=Root.position;facingRotation=Quaternion.identity;
        if(dummy){playerName="DUMMY";foreach(var b in bodies)b.mass*=.7f;}
        foreach(var b in bodies)totalMass+=b.mass;
        GBRealisticWrestler.Attach(this);
        GBApp.I.Register(this);
    }
    public void ResetTo(Vector3 at) {
        restartPoseUntil=0;
        Release(0);Release(1);for(int n=0;n<15;n++){bodies[n].position=at+initial[n];bodies[n].rotation=Quaternion.identity;bodies[n].linearVelocity=Vector3.zero;bodies[n].angularVelocity=Vector3.zero;}
        stamina=MaxStamina;balance=100;downUntil=recoveryUntil=pressureUntil=climbUntil=liftCharge=stumbleUntil=EscapeUntil=jumpAt=strikeAt=jumpRequestUntil=carriedRecoveryUntil=0;carriedFlight=false;lastFootSupport=-10;lastImpact=-1;eliminated=false;oldFlags=0;facing=Vector3.forward;facingRotation=Quaternion.identity;command=default;pinProgress=0;lastRoot=at+initial[0];feetInitialized=false;nextStep=0;stepBegin[0]=stepBegin[1]=-10;ResetWalkMetrics();foreach(var hand in hands){hand.releaseRequired=false;hand.candidate=null;}CombatReset();CornerReset();PinReset();SprawlReset();
    }
    public void Simulate(GBInput input) {
        if(eliminated)return;command=input.Sanitize();
        bool scrambleRequested=command.Has(GBInput.Brace)&&(HeldByOpponent||Down||PhysicallyDownForPin||PinControlled);
        GateScrambleInput(scrambleRequested);UpdateScrambleRecovery(scrambleRequested);
        if(scrambleRequested&&!ScrambleReady){command.flags&=~GBInput.Brace;if(scrambleRequested)Feedback(Time.time<scrambleUntil||stamina<4?"Scramble exhausted - release Ctrl":"Scramble cooldown - stop tapping Ctrl",.2f);}
        SprawlRefresh();AdvanceShotEntry();var tuning=GBApp.I.Tuning;float dt=Time.fixedDeltaTime;
        grounded=Physics.Raycast(bodies[11].position+Vector3.up*.12f,Vector3.down,.37f,1,QueryTriggerInteraction.Ignore)||Physics.Raycast(bodies[14].position+Vector3.up*.12f,Vector3.down,.37f,1,QueryTriggerInteraction.Ignore);
        if(grounded)lastFootSupport=Time.time;
        if(!grounded&&HeldByOpponent)carriedFlight=true;
        if(carriedFlight){
            // A carried/released body keeps its earned orientation in flight.
            // Any core landing can begin recovery; it need not land on boots.
            carriedRecoveryUntil=Time.time+.35f;
            if(grounded&&Vector3.Dot(bodies[1].transform.up,Vector3.up)>.65f||Physics.Raycast(Root.position,Vector3.down,.4f,1,QueryTriggerInteraction.Ignore)||Physics.Raycast(bodies[1].position,Vector3.down,.4f,1,QueryTriggerInteraction.Ignore))carriedFlight=false;
        }
        hanging=false;for(int h=0;h<2;h++)if(hands[h].joint&&hands[h].target&&!hands[h].target.wrestler&&hands[h].target.GetComponent<Collider>().bounds.max.y>Root.position.y+.6f&&bodies[h==0?5:8].position.y>Root.position.y+.2f)hanging=true;
        int edges=command.flags&~oldFlags;oldFlags=command.flags;
        facing=Vector3.Slerp(facing,Shooting?ShotDirection:command.Aim,Mathf.Clamp01(dt*12));facingRotation=Quaternion.LookRotation(facing,Vector3.up);
        bool brace=command.Has(GBInput.Brace)&&!Down&&(!scrambleRequested||stamina>1);
        bool paidBrace=brace&&scrambleRequested;bool pinning=command.Has(GBInput.Pin)&&GBApp.I.CurrentMode==1;
        bool groundEscape=ScrambleReady&&command.Has(GBInput.Brace)&&command.Move.sqrMagnitude>.05f&&(Down||PinControlled);
        bool effort=command.Has(GBInput.Lift)&&GripCount>0||command.Has(GBInput.Sprint)&&command.Move.magnitude>.1f||GripCount>0||paidBrace||groundEscape||Sprawling;
        bool scrambling=groundEscape||(HeldByOpponent&&command.Has(GBInput.Brace)&&command.Move.sqrMagnitude>.08f&&stamina>8);
        float cost=GripCount*2.0f+(command.Has(GBInput.Lift)&&GripCount>0?tuning.liftCost:0)+(command.Has(GBInput.Sprint)&&command.Move.magnitude>.1f?12:0)+(paidBrace?7*(scrambling?ScrambleStaminaMultiplier:1):0)+(Sprawling?10:0);
        // Opponent resistance changes physical leverage, not our stamina bill.
        stamina=Mathf.Clamp(stamina+(effort?-cost:EscapeRecoveryBlocked?0:tuning.staminaRegen)*dt,0,MaxStamina);
        if(stamina<=.1f){Release(0);Release(1);}
        PinRefresh();
        bool sprawling=Sprawling;
        // A grounded cover/escape needs sliding knees and relaxed feet, rather
        // than the planted boot traction used to resist standing exchanges.
        foreach(int foot in new[]{11,14})bodies[foot].GetComponent<Collider>().sharedMaterial=Down||pinTarget||PinControlled||sprawling?groundCloth:standingBoots;
        // A neutral rival cannot fully recover footing while an opponent is
        // actively holding and working them. Bracing remains the strong answer.
        if(!Down)balance=Mathf.Min(100,balance+dt*(brace?30:HeldByOpponent?8:16));
        float upright=Vector3.Dot(bodies[1].transform.up,Vector3.up);
        bool nearFloor=Physics.Raycast(Root.position+Vector3.up*.1f,Vector3.down,1.5f,1,QueryTriggerInteraction.Ignore);
        bool pressured=Time.time<pressureUntil;
        bool canStand=!Down&&!pressured&&!PinControlled&&!carriedFlight&&Time.time>=carriedRecoveryUntil&&(grounded||nearFloor)&&(!JumpFlightWindow||grounded)&&!hanging&&Time.time>jumpAt-.2f;
        bool lowAim=false;StandingStance=false;
        if(canStand) {
            Vector3 locomotion=ShotDriving?ShotDirection:command.Move;
            float desired=ShotDriving?ShotDriveSpeed*ShotDrivePhase:command.Has(GBInput.Sprint)&&stamina>10?tuning.sprintSpeed:tuning.walkSpeed;
            if(sprawling)desired=.35f;else if(brace)desired*=.58f;if(Stumbling)desired*=.72f;
            // Distribute acceleration across the mass, so a resisted push does not
            // apply its entire overturning moment through the pelvis joint.
            Vector3 velocity=Vector3.zero;foreach(var part in bodies)velocity+=part.linearVelocity*part.mass;velocity/=Mathf.Max(1,totalMass);velocity.y=0;
            Vector3 force=Vector3.ClampMagnitude((locomotion*desired-velocity)*totalMass*9,tuning.moveForce*Mathf.Lerp(.7f,1,StaminaFraction));
            if(locomotion.sqrMagnitude<.01f)force=Vector3.ClampMagnitude(force,brace?1000:650);
            float driveMass=totalMass-bodies[11].mass-bodies[14].mass;
            for(int n=0;n<15;n++)if(n!=11&&n!=14)bodies[n].AddForce(force*(bodies[n].mass/driveMass),ForceMode.Force);
            float floor=Root.position.y-1.04f;RaycastHit ground;
            if(Physics.Raycast(Root.position+Vector3.up*.2f,Vector3.down,out ground,1.9f,1,QueryTriggerInteraction.Ignore))floor=ground.point.y;
            // Use the cursor's world height: as the hips lower, a fixed leg
            // target must not alternate between crouching and standing.
            lowAim=command.HasHandAim&&!brace&&!Shooting&&!pinning&&!sprawling&&Root.position.y+command.HandOffset.y<floor+.90f;
            float lateralMove=Mathf.Abs(Vector3.Dot(command.Move,facingRotation*Vector3.right));
            bool lateralWalk=lateralMove>Mathf.Abs(Vector3.Dot(command.Move,facing))*1.15f;
            StandingStance=brace&&!Shooting&&!pinning&&!lowAim&&!sprawling&&!PhysicallyDownForPin;
            float stanceHeight=sprawling?.48f:Shooting?ShotStanceHeight:StandingStance?.80f:brace||lowAim?.60f:pinning?.60f:command.Has(GBInput.Pin)?.9f:command.Move.sqrMagnitude>.01f?(lateralWalk?.84f:.88f):1.04f;
            float lift=(floor+stanceHeight-Root.position.y)*2800-Root.linearVelocity.y*330+(sprawling?0:totalMass*9.81f);
            Root.AddForce(Vector3.up*Mathf.Clamp(lift,pinning?-1000:-220,1950),ForceMode.Force);
            Quaternion desiredRotation=facingRotation;
            if(sprawling)feetInitialized=false;
            else {bool locomoting=command.Move.sqrMagnitude>.01f&&!brace&&!pinning&&!lowAim;ApplyTorque(Root,desiredRotation,Shooting||locomoting?760:360,Shooting||locomoting?90:60,620);StepFeet(floor,brace||Shooting||pinning||lowAim,dt);}
        } else {feetInitialized=false;if(!Down&&!hanging)Root.AddForce(command.Move*(pressured?120:95),ForceMode.Force);}
        for(int n=0;n<15;n++)poses[n]=facingRotation;
        bool walkingPose=canStand&&command.Move.sqrMagnitude>.01f&&!brace&&!Shooting&&!pinning&&!lowAim&&!sprawling;
        if(walkingPose)WalkPoses();
        if(Shooting)ShotPoses();
        if(StandingStance)StandingStancePoses();
        else if((brace||lowAim)&&!Shooting){
            // Match the lowered pelvis to a real folded leg chain. The head
            // follows the chest instead of fighting to remain upright, with
            // 30 degrees of neck extension inside the existing 40-degree limit.
            poses[9]=poses[12]=facingRotation*Quaternion.Euler(-62,0,0);
            poses[10]=poses[13]=facingRotation*Quaternion.Euler(62,0,0);
            poses[1]=facingRotation*Quaternion.Euler(65,0,0);
            poses[2]=facingRotation*Quaternion.Euler(35,0,0);
        }
        if(pinning){poses[9]=poses[12]=facingRotation*Quaternion.Euler(-62,0,0);poses[10]=poses[13]=facingRotation*Quaternion.Euler(62,0,0);poses[1]=facingRotation*Quaternion.Euler(65,0,0);poses[2]=facingRotation*Quaternion.Euler(35,0,0);}
        if(canStand&&!sprawling){ApplyTorque(bodies[1],poses[1],340,50,500);ApplyTorque(bodies[2],poses[2],85,18,125);}
        for(int h=0;h<2;h++)UpdateHand(h,command.Has(h==0?GBInput.Left:GBInput.Right));
        CombatTick(dt);
        CornerTick(dt);
        SprawlTick();
        PinTick(dt);
        ApplyBottomRestartPose();
        float muscle=Down?.045f:PinControlled?(command.Has(GBInput.Brace)?.12f:.025f):pressured?.18f:Mathf.Lerp(.65f,1,StaminaFraction)*(Stumbling?.82f:1);
        bool lifting=command.Has(GBInput.Lift)&&!command.Has(GBInput.Brace)&&GripCount>0&&!Down&&!pinTarget;liftCharge=lifting?Mathf.Min(1,liftCharge+dt*1.25f):0;
        for(int n=1;n<15;n++) {
            if(!joints[n])continue;Quaternion relative=Quaternion.Inverse(poses[parents[n]])*poses[n];joints[n].targetRotation=Quaternion.Inverse(relative);
            bool coverArm=pinTarget&&n>=3&&n<=8;
            // The real grip supports the cover. Stiff mouse IK joint motors
            // must let elbows fold instead of propping the torso above contact.
            float armPower=coverArm?.075f:lifting&&n>=3&&n<=8?2.5f:1;var d=joints[n].slerpDrive;d.maximumForce=(n==1?260:n>=9?161:97.75f)*muscle*armPower;d.positionSpring=(n==1?2100:n>=9?1207.5f:805)*muscle*armPower;d.positionDamper=(n==1?130:n>=9?74.75f:55.2f)*(coverArm?armPower:1);
            if((Shooting||walkingPose||StandingStance)&&n>=9){
                // The shared relative drives opposed the fast asymmetric entry.
                // Track the real planned world pose with a bounded physical
                // actuator, returning its full reaction to the supporting hips.
                // Existing positional/angular constraints and self contacts stay.
                if(n!=11&&n!=14){
                    Quaternion q=poses[n]*Quaternion.Inverse(bodies[n].rotation);if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    float angle;Vector3 axis;q.ToAngleAxis(out angle,out axis);if(angle>180)angle-=360;
                    Vector3 torque=axis.sqrMagnitude>.0001f?Vector3.ClampMagnitude(axis.normalized*angle*Mathf.Deg2Rad*276-bodies[n].angularVelocity*23,97.75f*muscle):Vector3.zero;
                    bodies[n].AddTorque(torque,ForceMode.Force);Root.AddTorque(-torque,ForceMode.Force);
                }
                // Boots already have the existing bounded world-foot actuator;
                // do not add a conflicting relative ankle drive on top of it.
                d.maximumForce=0;
            }
            joints[n].slerpDrive=d;
        }
        if((edges&GBInput.Jump)!=0)jumpRequestUntil=Time.time+.14f;
        // A stride briefly raises both boots beyond the strict contact probe.
        // Buffer the press and allow a small planted-foot grace, but never
        // allow the paid-flight window to become an airborne second jump.
        bool jumpSupport=grounded||(!JumpFlightWindow&&Root.linearVelocity.y<.75f&&(Time.time-lastFootSupport<.12f||Physics.Raycast(bodies[11].position+Vector3.up*.12f,Vector3.down,.49f,1,QueryTriggerInteraction.Ignore)||Physics.Raycast(bodies[14].position+Vector3.up*.12f,Vector3.down,.49f,1,QueryTriggerInteraction.Ignore)));
        if(jumpRequestUntil>=Time.time&&jumpRequestUntil>0&&!Down&&!PinControlled&&stamina>=(hanging?16:8)&&Time.time>jumpAt&&(hanging||jumpSupport)){jumpRequestUntil=0;jumpAt=Time.time+.45f;if(hanging){stamina-=16;climbUntil=Time.time+2.2f;foreach(var hand in hands)if(hand.target){climbGoal=hand.target.transform.TransformPoint(hand.localAnchor)+facing*.85f;climbGoal.y=hand.target.GetComponent<Collider>().bounds.max.y+1.08f;break;}}else{stamina-=8;lastFootSupport=-10;foreach(var b in bodies)b.AddForce(Vector3.up*3.8f,ForceMode.VelocityChange);BeginJumpMomentum();}}
        if(Time.time<climbUntil&&!Down&&stamina>1){if(Time.time>climbUntil-1.9f&&GripCount>0){Release(0);Release(1);foreach(var hand in hands)hand.releaseRequired=true;}Vector3 goal=climbGoal;if(Root.position.y<climbGoal.y-.3f){goal.x=Root.position.x;goal.z=Root.position.z;}Vector3 acceleration=Vector3.ClampMagnitude((goal-Root.position)*18-Root.linearVelocity*7+Vector3.up*9.81f,25);foreach(var part in bodies)part.AddForce(acceleration*part.mass,ForceMode.Force);stamina=Mathf.Max(0,stamina-dt*24);}
        if(command.Has(GBInput.Lift)&&GripCount>0&&!Down&&stamina>3&&!pinTarget){if(command.Has(GBInput.Brace))Slam();else Lift();}
        if((edges&GBInput.Strike)!=0&&!Down&&Time.time>strikeAt){strikeAt=Time.time+.45f;Shot();}
        UpdateScrambleRecovery(scrambleRequested);
        if(brace&&ScrambleReady&&pressured){bodies[1].AddForce(Vector3.up*280,ForceMode.Force);bodies[1].AddTorque(facing*90,ForceMode.Force);}
        moveDistance+=Vector3.Distance(Root.position,lastRoot);lastRoot=Root.position;
        foreach(var b in bodies){if(b.linearVelocity.magnitude>18)b.linearVelocity=Vector3.ClampMagnitude(b.linearVelocity,18);if(!Finite(b.position)){GBApp.I.Issue("Invalid body state");ResetTo(spawn);break;}}
    }
    void StepFeet(float floor,bool lowStance,float dt) {
        if(!feetInitialized){walkPlannerActive=false;for(int h=0;h<2;h++){plantedFeet[h]=bodies[h==0?11:14].position;plantedFeet[h].y=floor+.085f;stepBegin[h]=-10;}feetInitialized=true;}
        float speed=Vector3.ProjectOnPlane(Root.linearVelocity,Vector3.up).magnitude;
        bool walking=!lowStance&&!Shooting&&command.Move.sqrMagnitude>.01f;
        Vector3 lateralAxis=facingRotation*Vector3.right;
        float sideways=Vector3.Dot(command.Move,lateralAxis);
        bool sideStepping=walking&&Mathf.Abs(sideways)>Mathf.Abs(Vector3.Dot(command.Move,facing))*1.15f;
        // Lead outward on the first lateral step. The contact-gated opposite
        // step then follows, keeping each boot on its own side of the stance.
        if(walking&&!walkPlannerActive){
            walkStartedStep=false;
            // Neutral replants are not walking swings. Start from actual boots.
            for(int h=0;h<2;h++){plantedFeet[h]=bodies[h==0?11:14].position;plantedFeet[h].y=floor+.085f;stepBegin[h]=-10;}
            nextStep=sideStepping?(sideways>0?1:0):0;
        }
        walkPlannerActive=walking;
        // Keep neutral/pivot support unchanged. A longer walking stride must
        // not change the resistance or replant timing of a stationary grapple.
        float stepDuration=Shooting?.20f:lowStance?.22f:sideStepping?.18f:walking?Mathf.Lerp(.28f,.21f,Mathf.InverseLerp(1,5,speed)):.18f;bool otherStepping=Time.time-stepBegin[1-nextStep]<stepDuration;
        if(walking&&!walkStartedStep&&!otherStepping&&Time.time-stepBegin[nextStep]>=stepDuration){
            bool oppositeSupported=bodies[nextStep==0?14:11].GetComponent<Collider>().bounds.min.y<=floor+.025f;
            bool selectedSupported=bodies[nextStep==0?11:14].GetComponent<Collider>().bounds.min.y<=floor+.025f;
            // Replant the currently unsupported boot first. Otherwise waiting
            // to lift the sole supporting boot can deadlock a slow first step.
            if(!oppositeSupported&&selectedSupported)nextStep=1-nextStep;
        }
        // Walking motors can produce a short yaw oscillation. It is not an
        // intentional stationary pivot and must not erase an unfinished swing.
        bool pivoting=!walking&&Mathf.Abs(Root.angularVelocity.y)>2.5f;
        Vector3 travel=Shooting?ShotDirection:command.Move;
        // Lead the hips with a useful foot placement. Cadence follows actual
        // travel speed, so pushing a blocked rival does not march in place.
        Vector3 stride=travel*(lowStance?.08f:sideStepping?.22f:walking?Mathf.Lerp(.23f,.31f,Mathf.InverseLerp(1,5,speed)):.16f);
        if(walking&&!sideStepping)stride+=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(Root.linearVelocity,Vector3.up)*stepDuration*.5f,.40f);
        if(walking&&WalkPhaseDiagnostics)WalkPhaseEvidence="next="+nextStep+" otherSwing="+otherStepping+" pivot="+pivoting+" yaw="+Root.angularVelocity.y.ToString("0.00")+" gapL/R="+(bodies[11].GetComponent<Collider>().bounds.min.y-floor).ToString("0.000")+"/"+(bodies[14].GetComponent<Collider>().bounds.min.y-floor).ToString("0.000")+" ageL/R="+(Time.time-stepBegin[0]).ToString("0.00")+"/"+(Time.time-stepBegin[1]).ToString("0.00")+" feetY="+bodies[11].position.y.ToString("0.000")+"/"+bodies[14].position.y.ToString("0.000");
        for(int h=0;h<2;h++) {
            float halfStance=walking||Shooting?.13f:.145f;
            var foot=bodies[h==0?11:14];Vector3 home=Root.position+facingRotation*new Vector3(h==0?-halfStance:halfStance,0,.09f)+stride;
            if(Shooting)home+=ShotDirection*ShotFootOffset(h);
            home.y=floor+.085f;
            // A rapid turn pivots on the mat. Replant at the physical boot so
            // opposite feet do not chase crossing world targets through each other.
            if(pivoting){plantedFeet[h]=foot.position;plantedFeet[h].y=floor+.085f;stepBegin[h]=-10;}
            Vector3 drift=home-plantedFeet[h];drift.y=0;
            if(walking&&WalkPhaseDiagnostics)WalkPhaseEvidence+=" drift"+h+"="+drift.magnitude.ToString("0.000");
            // Only one boot advances at a time. Targets are world positions and
            // all tracking uses bounded forces; no body is moved or teleported.
            bool oppositeSupported=bodies[h==0?14:11].GetComponent<Collider>().bounds.min.y<=floor+.025f;
            if(!pivoting&&h==nextStep&&!otherStepping&&(!walking||oppositeSupported)&&Time.time-stepBegin[h]>=stepDuration&&drift.magnitude>(lowStance?.24f:sideStepping?.30f:walking?.36f:.30f)){
                stepStarts[h]=foot.position;stepGoals[h]=home;stepBegin[h]=Time.time;plantedFeet[h]=home;nextStep=1-h;otherStepping=true;
                if(walking)walkStartedStep=true;
                RecordFootPlacement(h,Vector3.ProjectOnPlane(home-foot.position,Vector3.up).magnitude);
            }
            float progress=Mathf.Clamp01((Time.time-stepBegin[h])/stepDuration);
            float clearance=Shooting?.09f:lowStance?.035f:walking?.15f:.12f;
            // Complete lowering before the swing timer expires so the boot
            // has time to establish actual contact before its partner lifts.
            float arcProgress=walking?Mathf.Clamp01(progress/.80f):progress;
            float raised=progress<1?Mathf.Sin(arcProgress*Mathf.PI)*clearance:0;
            Vector3 goal=progress<1?Vector3.Lerp(stepStarts[h],stepGoals[h],Mathf.SmoothStep(0,1,progress)):plantedFeet[h];goal.y=floor+.085f+raised;
            if(Shooting){goal=home;goal.y=floor+ShotBootHeight(h);}
            bool supportedPose=walking||StandingStance;
            Quaternion bootPose=Shooting?ShotBootRotation(h):supportedPose?WalkingBootRotation(h):facingRotation;
            if(supportedPose)goal.y=floor+BootSupportHeight(bootPose)+raised;
            RaycastHit support;if(!Physics.Raycast(goal+Vector3.up*.25f,Vector3.down,out support,.75f,1,QueryTriggerInteraction.Ignore)){RecordWalkStep(h,foot.position,1,lowStance);continue;}
            goal.y=support.point.y+(Shooting?ShotBootHeight(h) :(supportedPose?BootSupportHeight(bootPose):.085f)+raised);
            RecordWalkStep(h,goal,progress,lowStance);
            // Shot targets travel with the real hips. Damping against world
            // velocity held both boots behind, even when their targets advanced.
            Vector3 targetVelocity=Shooting?Vector3.ProjectOnPlane(Root.linearVelocity,Vector3.up):walking&&progress<1?Vector3.ClampMagnitude(Vector3.ProjectOnPlane(stepGoals[h]-stepStarts[h],Vector3.up)*(6*progress*(1-progress)/stepDuration),4.5f):Vector3.zero;
            Vector3 error=goal-foot.position;Vector3 force=error*(Shooting?600:320)-(foot.linearVelocity-targetVelocity)*(Shooting?36:26);
            if(pivoting){force.x=0;force.z=0;}
            force.y=error.y*500-foot.linearVelocity.y*40;foot.AddForce(Vector3.ClampMagnitude(force,150),ForceMode.Force);
            ApplyTorque(foot,bootPose,65,12,85);
        }
    }
    static bool Finite(Vector3 v){return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
    public float JointSeparation() {
        float worst=0;for(int n=1;n<joints.Length;n++){var j=joints[n];if(!j||!j.connectedBody)return float.PositiveInfinity;float distance=Vector3.Distance(bodies[n].position+bodies[n].rotation*j.anchor,j.connectedBody.position+j.connectedBody.rotation*j.connectedAnchor);worst=Mathf.Max(worst,distance);}return worst;
    }
    static void ApplyTorque(Rigidbody rb,Quaternion target,float spring,float damping,float cap) {
        Quaternion q=target*Quaternion.Inverse(rb.rotation);if(q.w<0){q.x=-q.x;q.y=-q.y;q.z=-q.z;q.w=-q.w;}float angle;Vector3 axis;q.ToAngleAxis(out angle,out axis);if(angle>180)angle-=360;
        if(axis.sqrMagnitude>.01f)rb.AddTorque(Vector3.ClampMagnitude(axis*angle*Mathf.Deg2Rad*spring-rb.angularVelocity*damping,cap),ForceMode.Force);
    }
}
