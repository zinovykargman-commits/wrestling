using UnityEngine;

public partial class GBWrestler {
    bool sprawlExhausted;
    public bool Sprawling {
        get {return command.Has(GBInput.Sprawl)&&!sprawlExhausted&&stamina>4&&!Down&&!PinTarget&&!PinControlled&&!hanging&&!eliminated;}
    }

    void SprawlReset(){sprawlExhausted=false;}
    void SprawlRefresh(){
        if(!command.Has(GBInput.Sprawl))sprawlExhausted=false;
        else if(stamina<=4)sprawlExhausted=true;
    }

    void SprawlTick(){
        SprawlRefresh();
        if(!Sprawling)return;
        shotUntil=0;shotImpactDone=true;
        // The held stance changes physical motor targets, with feet free to
        // slide behind the hips. Mouse arms and manual grips stay independent.
        Quaternion hip=facingRotation*Quaternion.Euler(75,0,0);
        Quaternion chest=facingRotation*Quaternion.Euler(85,0,0);
        poses[0]=hip;poses[1]=chest;poses[2]=facingRotation*Quaternion.Euler(50,0,0);
        poses[9]=poses[12]=facingRotation*Quaternion.Euler(75,0,0);
        poses[10]=poses[13]=facingRotation*Quaternion.Euler(80,0,0);
        poses[11]=poses[14]=facingRotation*Quaternion.Euler(55,0,0);
        float strength=Mathf.Lerp(.45f,1,StaminaFraction);
        Root.AddTorque(PinTorque(Root,hip,600,35,220*strength),ForceMode.Force);
        bodies[1].AddTorque(PinTorque(bodies[1],chest,260,40,180*strength),ForceMode.Force);
        bodies[2].AddTorque(PinTorque(bodies[2],poses[2],70,12,35*strength),ForceMode.Force);
        Feedback("Sprawling - keep your weight low",.2f);
    }
}
