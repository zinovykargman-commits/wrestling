using UnityEngine;

public partial class GBWrestler {
    Collider cornerCanvas;
    public bool TurningCorner {get;private set;}
    public float LastCornerForce {get;private set;}
    public float LastCornerBalanceRate {get;private set;}
    public float CornerImpulseTotal {get;private set;}
    void CornerReset(){cornerCanvas=null;TurningCorner=false;LastCornerForce=LastCornerBalanceRate=CornerImpulseTotal=0;}
    static bool CornerSupported(GBWrestler wrestler,Bounds canvas){
        foreach(int n in new[]{11,14}){var boot=wrestler.bodies[n].GetComponent<Collider>();var at=boot.bounds.center;float gap=boot.bounds.min.y-canvas.max.y;
            if(Mathf.Abs(gap)<=.025f&&at.x>=canvas.min.x&&at.x<=canvas.max.x&&at.z>=canvas.min.z&&at.z<=canvas.max.z)return true;
        }return false;
    }
    internal void CornerTick(float dt){
        TurningCorner=false;LastCornerForce=LastCornerBalanceRate=0;
        if(!GBApp.I.Authority||Down||PhysicallyDownForPin||PinControlled||IsBeingPinned||PinTarget||Sprawling||hanging||eliminated||RecoveringFromCarry||stamina<6||command.Has(GBInput.Lift|GBInput.Pin)||GripCount==0)return;
        if(!cornerCanvas)foreach(var c in GBApp.I.Arena.GetComponentsInChildren<Collider>())if(c.enabled&&c.gameObject.activeInHierarchy&&c.name=="ring mat"){cornerCanvas=c;break;}
        if(!cornerCanvas)return;float mat=cornerCanvas.bounds.max.y;
        bool supported=CornerSupported(this,cornerCanvas.bounds);
        if(!supported||Root.position.y<mat+.55f||Vector3.Dot(bodies[1].rotation*Vector3.up,Vector3.up)<.55f)return;
        // Process a rival once even when both hands catch the same leg.
        GBWrestler victim=null;int legHands=0;
        for(int h=0;h<2;h++){
            var hand=hands[h];if(!hand.joint||!hand.joint.enabled||!hand.target||!hand.target.wrestler||hand.target.wrestler==this)continue;
            string region=hand.target.region;if(!region.Contains("thigh")&&!region.Contains("shin")&&!region.Contains("foot"))continue;
            Vector3 point=hand.target.transform.TransformPoint(hand.localAnchor),ownerPoint=bodies[h==0?5:8].transform.TransformPoint(hand.joint.anchor);
            // A stretched, failing hold cannot award remote leverage.
            if(Vector3.Distance(point,ownerPoint)>.30f)continue;
            if(victim&&victim!=hand.target.wrestler)continue;victim=hand.target.wrestler;legHands++;
        }
        if(!victim||legHands==0||!CornerSupported(victim,cornerCanvas.bounds)||victim.Down||victim.PhysicallyDownForPin||victim.PinControlled||victim.IsBeingPinned||victim.eliminated||victim.Root.position.y<mat+.55f||Vector3.Dot(victim.bodies[1].rotation*Vector3.up,Vector3.up)<.55f)return;
        Vector3 toward=Vector3.ProjectOnPlane(victim.Root.position-Root.position,Vector3.up);float separation=toward.magnitude;
        if(separation<.25f||separation>1.6f)return;Vector3 tangent=Vector3.Cross(Vector3.up,toward/separation);
        float side=Vector3.Dot(command.Move,tangent);if(Mathf.Abs(side)<.25f)return;
        float earned=Vector3.Dot(Root.linearVelocity-victim.Root.linearVelocity,tangent)*Mathf.Sign(side);
        if(earned<=.12f)return;
        float motion=Mathf.InverseLerp(.12f,1.4f,earned)*Mathf.Clamp01(Mathf.Abs(side));
        bool braced=victim.command.Has(GBInput.Brace)&&victim.grounded&&victim.stamina>8;
        float resistance=victim.Sprawling?.15f:braced?.35f:1;
        float leverage=(GripsOn(victim)>=2?1:.45f)*Mathf.Lerp(.5f,1,StaminaFraction)*resistance*motion;
        Vector3 force=tangent*(Mathf.Sign(side)*110*leverage);
        // Pull the caught leg around its planted support. Both bodies receive
        // equal opposite forces at the same contact: no net lift, impulse or
        // unbalanced external torque is granted by pressing a movement key.
        for(int h=0;h<2;h++){
            var hand=hands[h];if(!hand.joint||!hand.joint.enabled||!hand.target||hand.target.wrestler!=victim)continue;
            string region=hand.target.region;if(!region.Contains("thigh")&&!region.Contains("shin")&&!region.Contains("foot"))continue;
            Vector3 point=hand.target.transform.TransformPoint(hand.localAnchor),ownerPoint=bodies[h==0?5:8].transform.TransformPoint(hand.joint.anchor);
            if(Vector3.Distance(point,ownerPoint)>.30f)continue;
            hand.target.Body.AddForceAtPosition(force/legHands,point,ForceMode.Force);
            Root.AddForceAtPosition(-force/legHands,point,ForceMode.Force);
        }
        // Earn only gradual extra imbalance while actually gaining an angle.
        // Existing physical grip/pull work pays its existing costs; this adds no
        // defender-brace bill, forced Knock, grip, pin or ground-flipping action.
        LastCornerForce=force.magnitude;LastCornerBalanceRate=18*leverage;
        victim.balance=Mathf.Max(0,victim.balance-LastCornerBalanceRate*dt);
        CornerImpulseTotal+=LastCornerForce*dt;TurningCorner=LastCornerForce>.1f;
        if(TurningCorner)Feedback(victim.Sprawling?"Sprawl resisting - keep the leg and angle":braced?"Braced leg - work around the outside":"Turning the corner - drive around the caught leg",.2f);
    }
}
