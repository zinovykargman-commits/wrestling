using UnityEngine;

public partial class GBApp {
    public const float RingRestartSeconds=1.5f;
    public int RingRestartBottom {get;private set;}=-1;
    public bool RingRestartHeld {get{return Phase==GamePhase.Countdown&&RingRestartBottom>=0&&Authority;}}
    public bool RingRestartCountdown {get{return Phase==GamePhase.Countdown&&RoundMessage.StartsWith("OUT OF BOUNDS • ",System.StringComparison.Ordinal);}}
    public int CountdownNumber {get{return Mathf.Clamp(Mathf.CeilToInt(PhaseSeconds*(RingRestartCountdown?2:1)),1,3);}}
    public static bool OutsideRing(GBWrestler w){return Mathf.Abs(w.Root.position.x)>5.3f||Mathf.Abs(w.Root.position.z)>5.3f||w.Root.position.y<-1.3f;}
    public void CheckRingOut(){
        if(!Authority||Phase!=GamePhase.Fighting)return;
        // The body which crosses the boundary is bottom, regardless of who
        // supplied its momentum. Arms reaching past a rope are still legal.
        foreach(var w in Wrestlers)if(OutsideRing(w)){RestartFromRingOut(w.player);break;}
    }
    public void RestartFromRingOut(int bottom){
        if(!Authority||bottom<0||bottom>=Wrestlers.Count)return;
        ResetRound(false);RingRestartBottom=bottom;
        var low=Wrestlers[bottom];var top=Wrestlers[1-bottom];
        low.PrepareBottomRestart();top.ResetTo(new Vector3(0,.28f,-1.1f));
        foreach(var w in Wrestlers)foreach(var rb in w.bodies){var position=rb.position;var rotation=rb.rotation;rb.isKinematic=true;rb.transform.SetPositionAndRotation(position,rotation);rb.position=position;rb.rotation=rotation;}
        Physics.SyncTransforms();
        RoundMessage="OUT OF BOUNDS • "+low.playerName+" STARTS ON BOTTOM";
        Phase=GamePhase.Countdown;SetPhaseSeconds(RingRestartSeconds);
        Toast(RoundMessage,RingRestartSeconds);
    }
    void FinishRingRestart(){
        foreach(var w in Wrestlers)foreach(var rb in w.bodies){rb.isKinematic=false;rb.WakeUp();}
        Wrestlers[RingRestartBottom].ReleaseBottomRestart();RingRestartBottom=-1;
        Phase=GamePhase.Fighting;Sfx.Play("whistle",.65f);
        // Inputs pressed during the countdown do not become buffered actions.
        System.Array.Clear(buffered,0,buffered.Length);
    }
}

public partial class GBWrestler {
    Quaternion[] restartPose;float restartPoseUntil;
    public void PrepareBottomRestart(){
        ResetTo(Vector3.zero);restartPose=new Quaternion[15];
        float[] pitch={90,65,35,-20,-65,0,-20,-65,0,0,90,0,0,90,0};
        for(int n=0;n<15;n++)restartPose[n]=Quaternion.Euler(pitch[n],0,0);
        Root.position=new Vector3(0,.72f,0);Root.rotation=restartPose[0];
        for(int n=1;n<15;n++){
            var j=joints[n];var parent=bodies[parents[n]];bodies[n].rotation=restartPose[n];
            // Forward kinematics preserves the real anatomical anchors.
            bodies[n].position=parent.position+parent.rotation*j.connectedAnchor-bodies[n].rotation*j.anchor;
            poses[n]=restartPose[n];j.targetRotation=Quaternion.Inverse(Quaternion.Inverse(restartPose[parents[n]])*restartPose[n]);
        }
        foreach(var rb in bodies)rb.transform.SetPositionAndRotation(rb.position,rb.rotation);
        facing=Vector3.forward;restartPoseUntil=0;Physics.SyncTransforms();
    }
    public void ReleaseBottomRestart(){pressureUntil=Time.time+.75f;restartPoseUntil=Time.time+.75f;feetInitialized=false;}
    void ApplyBottomRestartPose(){
        if(restartPose==null||Time.time>=restartPoseUntil||Down||PinControlled||command.Has(GBInput.Sprawl)||command.Has(GBInput.Brace))return;
        for(int n=0;n<15;n++)poses[n]=Quaternion.LookRotation(facing,Vector3.up)*restartPose[n];
        ApplyTorque(Root,poses[0],360,60,620);
        Root.AddForce(Vector3.up*Mathf.Clamp((.72f-Root.position.y)*1200-Root.linearVelocity.y*100,-500,150),ForceMode.Force);
    }
}
