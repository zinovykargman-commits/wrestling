using UnityEngine;

// Difficulty changes decisions and openings, never mass, forces, reach or stamina.
// All wrestling still goes through the same GBInput path used by the player.
public class GBBot {
    float nextThink,gripSince=-1,heldSince=-1,releaseUntil,strikeAt;
    float openingUntil,nextOpening,startedAt=-1,shotSeenAt=-1,sprawlUntil;
    float pinSetupSince=-1,coverAttemptSince=-1,coverRetryUntil;
    int lastDifficulty=-1,lastPartner=-1;
    GBInput decision;
    public int Decisions,Attacks,Throws;
    public string Intent {get;private set;}="READY";

    public GBInput Tick(GBApp app,GBWrestler me,GBWrestler rival){
        int difficulty=Mathf.Clamp(app.AIDifficulty,0,2);
        if(lastDifficulty!=difficulty){lastDifficulty=difficulty;nextThink=0;nextOpening=Time.time+4;openingUntil=0;shotSeenAt=-1;sprawlUntil=0;pinSetupSince=coverAttemptSince=-1;coverRetryUntil=0;}
        if(startedAt<0)startedAt=Time.time;
        if(Time.time<nextThink)return decision;
        nextThink=Time.time+ThinkInterval(difficulty);Decisions++;
        Vector3 delta=Vector3.ProjectOnPlane(rival.Root.position-me.Root.position,Vector3.up);
        float distance=delta.magnitude;
        Vector3 aim=distance>.05f?delta/distance:me.facing;
        Vector3 side=Vector3.Cross(Vector3.up,aim)*((me.player&1)==0?1:-1);
        Vector3 move=distance>1.02f?aim*(difficulty==0?.55f:.72f):distance<.65f?-aim*.22f:Vector3.zero;
        int bits=0;
        bool held=HeldBy(rival,me);
        if(held){if(heldSince<0)heldSince=Time.time;}else heldSince=-1;
        bool canAct=!me.Down&&!me.eliminated&&!me.hanging;
        bool winded=me.stamina<(difficulty==0?29:23);
        bool defending=held&&Time.time-heldSince>ReactionDelay(difficulty);
        bool pinOpportunity=rival.PhysicallyDownForPin;
        // A hand must reach the lower surface of a downed rival. Standing a
        // whole metre away makes that physically unreachable from a shoulder.
        if(pinOpportunity){
            // A lying pelvis sits away from the chest being reached. Face and
            // approach the same chest as the cursor so the manual flank grips
            // and the subsequent cover share a physical approach direction.
            delta=Vector3.ProjectOnPlane(rival.bodies[1].position-me.Root.position,Vector3.up);
            distance=delta.magnitude;
            aim=distance>.05f?delta/distance:me.facing;
            side=Vector3.Cross(Vector3.up,aim)*((me.player&1)==0?1:-1);
            move=distance>.48f?aim*.45f:Vector3.zero;
        }
        bool legAttack=difficulty==2&&!pinOpportunity&&rival.command.Has(GBInput.Brace)&&((int)(Time.time-startedAt)/3)%2==0;
        var region=rival.bodies[legAttack?9:1].GetComponent<Collider>();
        Vector3 target=region.ClosestPoint(me.bodies[1].position);
        // Select the exposed middle of a grounded chest. A shoulder-edge aim
        // can select an overlapping arm/head; an interior aim also drives the
        // free hand into the eventual chest-to-chest contact.
        if(pinOpportunity)target=CoverAim(me,rival);
        else {pinSetupSince=coverAttemptSince=-1;coverRetryUntil=0;}
        bits|=GBInput.HandAim;

        // Easy and Normal deliberately leave recognizable openings after exchanges.
        // Hard opens up when exhausted; it does not gain an invulnerable defense.
        if(difficulty<2&&Time.time>=nextOpening&&!pinOpportunity){
            openingUntil=Time.time+(difficulty==0?1.05f:.55f);
            nextOpening=Time.time+(difficulty==0?5.2f:7.8f);
        }
        bool opening=Time.time<openingUntil;
        // React only to an already visible forward shot, sampled at this
        // difficulty's normal decision cadence. Never read future key edges.
        bool visibleShot=difficulty>0&&rival.Shooting&&distance<2.6f&&Vector3.Dot(rival.ShotDirection,-aim)>.45f;
        if(visibleShot){if(shotSeenAt<0)shotSeenAt=Time.time;}else if(shotSeenAt>=0&&Time.time-shotSeenAt>.65f)shotSeenAt=-1;
        bool answerShot=difficulty>0&&shotSeenAt>=0&&Time.time-shotSeenAt>(difficulty==2?.06f:.14f)&&Time.time-shotSeenAt<.65f&&distance<2.7f;
        if(answerShot&&canAct&&!pinOpportunity&&me.stamina>32){sprawlUntil=Time.time+.45f;shotSeenAt=-1;}
        if(me.IsBeingPinned||(me.Down&&held)){
            bool respond=heldSince<0||Time.time-heldSince>ReactionDelay(difficulty);
            if(respond){bits|=GBInput.Brace;move=side*(difficulty==0?.65f:1f)-aim*.22f;Intent="BRIDGING / ROLLING";}
            else {move=Vector3.zero;Intent="PIN DEFENSE OPENING";}
        }
        else if(me.Down){bits|=GBInput.Brace;move=side*.6f;Intent="ROLLING TO RECOVER";}
        else if(Time.time<sprawlUntil&&canAct&&me.stamina>12){bits=GBInput.HandAim|GBInput.Sprawl;move=Vector3.zero;target=me.Root.position+aim*.65f+Vector3.up*.15f;Intent="SPRAWLING AGAINST THE SHOT";}
        else if(winded){
            // Release hands and stop paying for brace once free to regain stamina.
            move=-aim*.45f+side*.14f;
            if(defending)bits|=GBInput.Brace;
            Intent="CATCHING BREATH";
        }else if(opening){move=-aim*.12f;Intent="OPENING";}
        else if(canAct){
            Intent=distance>1.7f?"APPROACHING":"LOOKING FOR A HOLD";
            if(defending){
                bits|=GBInput.Brace;
                // A direction plus brace is a physical escape, with a visible side step.
                move=-aim*(difficulty==0?.25f:.48f)+side*(difficulty==2?.48f:.2f);
                Intent="BRACING / ESCAPING";
            }
            if(distance<1.75f&&Time.time>=releaseUntil&&me.stamina>30){
                bits|=GBInput.Left|GBInput.Right;
                // The cursor, rather than C or a privileged region selector,
                // chooses the torso or leg surface the actual hands reach.
            }
            if(me.GripCount>0){
                if(gripSince<0)gripSince=Time.time;
                float gripAge=Time.time-gripSince;
                if(pinOpportunity){
                    int torsoHands=0;
                    for(int h=0;h<2;h++){
                        var hand=me.hands[h];int button=h==0?GBInput.Left:GBInput.Right;
                        if(hand.joint&&hand.target&&hand.target.wrestler==rival){
                            torsoHands++;
                            // Any live nearby anatomical hold can control a grounded
                            // rival; C and actual shoulder contacts decide the pin.

                        }
                    }
                    if(torsoHands==0){pinSetupSince=coverAttemptSince=-1;move=distance>.55f?aim*.25f:Vector3.zero;Intent="REACHING EXPOSED TORSO";}
                    else {
                        if(pinSetupSince<0)pinSetupSince=Time.time;
                        // Use the earned torso hold immediately for grounded control.
                        // The independent free hand keeps manually reaching;
                        // the pin rule does not require a chest cover or two grips.
                        bool ready=torsoHands>0;
                        move=Vector3.zero;
                        if(Time.time<coverRetryUntil){Intent="RESETTING COVER / REACHING SECOND HAND";}
                        else if(ready){
                            if(coverAttemptSince<0)coverAttemptSince=Time.time;
                            if(me.IsCovering)coverAttemptSince=Time.time;
                            if(Time.time-coverAttemptSince>2.2f){
                                // Releasing C restores ordinary manual arm
                                // reach and lets the next attempt choose its
                                // physical approach from the current anchors.
                                coverRetryUntil=Time.time+.55f;coverAttemptSince=-1;
                                Intent="RESETTING STALLED COVER";
                            }else {bits|=GBInput.Pin;Intent="ROLLING / COVERING";}
                        }else Intent="WORKING SECOND TORSO GRIP";
                    }
                }
                else{
                    bool rivalBracing=rival.command.Has(GBInput.Brace)&&rival.stamina>16;
                    // Pull, physically rotate, then drive the held limbs down.
                    // No canned throw command or direct physics shortcut.
                    float windup=difficulty==0?.85f:difficulty==1?.5f:.35f;
                    float phase=(gripAge-windup)%2.6f;
                    if(gripAge<windup){move=-aim*.45f;target=me.Root.position+aim*.5f+Vector3.up*.35f;Intent="PULLING OFF BALANCE";}
                    else if(phase<1.25f){move=side*.65f-aim*.25f;aim=Quaternion.AngleAxis((me.player==0?1:-1)*(difficulty==0?25:60),Vector3.up)*aim;target=me.Root.position+aim*.7f+Vector3.up*(legAttack?.2f:.7f);if(me.stamina>35)bits|=GBInput.Lift;Intent="PIVOTING / LIFTING";}
                    else {move=aim*.55f+side*.25f;target=me.Root.position+aim*.9f-Vector3.up*.35f;Intent="TURNING / DRIVING DOWN";}
                    // Shove is X while gripping. Keep a full decision with X released
                    // between attempts so the input edge can fire again.
                    if(rivalBracing&&gripAge>windup&&Time.time>=strikeAt&&difficulty>0){
                        bits|=GBInput.Shove;strikeAt=Time.time+(difficulty==2?.95f:1.35f);
                        Attacks++;Intent="SHOVING";
                    }
                }
            }else{
                gripSince=-1;
                if(distance<1.3f&&Time.time>=strikeAt&&me.stamina>35&&!pinOpportunity&&!defending){
                    bits=(bits&~(GBInput.Left|GBInput.Right))|GBInput.Strike;
                    strikeAt=Time.time+(difficulty==0?1.65f:difficulty==1?1.05f:.8f);
                    Attacks++;Intent="SHOOTING";
                }
                if(difficulty==2&&distance>3&&me.stamina>70)bits|=GBInput.Sprint;
            }
        }
        if(me.GripCount==0){gripSince=-1;pinSetupSince=coverAttemptSince=-1;}
        // A slipped grip requires a real button release before another attempt.
        // Give the same release edge a human would use, rather than holding a
        // permanently blocked mouse button through every later decision.
        if(me.hands[0].releaseRequired||me.hands[1].releaseRequired){
            if(pinOpportunity){
                // Keep the other real anchor while the slipped hand gets its
                // own release edge. Releasing both would undo the setup.
                if(me.hands[0].releaseRequired)bits&=~GBInput.Left;
                if(me.hands[1].releaseRequired)bits&=~GBInput.Right;
            }else {bits&=~(GBInput.Left|GBInput.Right);releaseUntil=Time.time+.35f;}
            Intent="RELEASING / REPOSITIONING";
        }
        ApplyHazards(app,me,ref move,ref bits);
        Vector3 center=-Vector3.ProjectOnPlane(me.Root.position,Vector3.up);
        if(Mathf.Abs(me.Root.position.x)>3.2f||Mathf.Abs(me.Root.position.z)>3.2f)move=center.normalized*.6f;
        return Save(move,aim,bits,target-me.Root.position);
    }

    // The UI can switch partners during a drill. Clear cached commands immediately
    // so a newly passive/resisting partner cannot retain an attacking command.
    public GBInput TrainingTick(GBApp app,GBWrestler me,GBWrestler rival){
        int partner=Mathf.Clamp(app.TrainingPartner,0,2);
        if(lastPartner!=partner){
            lastPartner=partner;nextThink=0;decision=default;gripSince=heldSince=-1;
            releaseUntil=0;openingUntil=0;nextOpening=Time.time+4;
        }
        Vector3 delta=Vector3.ProjectOnPlane(rival.Root.position-me.Root.position,Vector3.up);
        Vector3 aim=delta.sqrMagnitude>.0025f?delta.normalized:me.facing;
        if(partner==0){Intent="PASSIVE";return Save(Vector3.zero,aim,0);}
        if(partner==2)return Tick(app,me,rival);
        bool held=HeldBy(rival,me);
        if(held){if(heldSince<0)heldSince=Time.time;}else heldSince=-1;
        int bits=0;Vector3 move=Vector3.zero;
        if(me.Down){bits=GBInput.Brace;Intent="RECOVERING";}
        else if(held){
            float age=Time.time-heldSince;
            // Resist for a short, readable burst then relax. Players can practice
            // timing a throw against brace rather than fighting endless defense.
            bool resisting=age>.3f&&(age-.3f)%2.5f<1.65f&&me.stamina>15;
            if(resisting){
                bits=GBInput.Brace;move=-aim*.27f+Vector3.Cross(Vector3.up,aim)*.22f;
                Intent="RESISTING";
            }else Intent="RESISTANCE OPENING";
        }else Intent="READY FOR A HOLD";
        // Resisting partners never initiate grabs, shots or throws.
        // Their only motion is a gentle physical escape while being held.
        move=SafeMove(app,me.Root.position,move);
        return Save(move,aim,bits);
    }

    GBInput Save(Vector3 move,Vector3 aim,int bits,Vector3 handOffset=default){
        decision=new GBInput{x=move.x,z=move.z,ax=aim.x,az=aim.z,hx=handOffset.x,hy=handOffset.y,hz=handOffset.z,flags=bits}.Sanitize();return decision;
    }
    public static float ThinkInterval(int difficulty){return difficulty<=0?.38f:difficulty==1?.18f:.1f;}
    static float ReactionDelay(int difficulty){return difficulty<=0?.6f:difficulty==1?.28f:.14f;}
    static bool HeldBy(GBWrestler rival,GBWrestler me){
        foreach(var h in rival.hands)if(h.target&&h.target.wrestler==me)return true;
        return false;
    }
    static Vector3 CoverAim(GBWrestler me,GBWrestler rival){
        var collider=rival.bodies[1].GetComponent<Collider>();
        Vector3 toward=Vector3.ProjectOnPlane(me.bodies[1].position-rival.bodies[1].position,Vector3.up).normalized;
        bool groundedChest=Mathf.Abs(Vector3.Dot(rival.bodies[1].rotation*Vector3.up,Vector3.up))<.6f;
        Vector3 exposed=groundedChest?Vector3.up+toward*.15f:toward;
        return collider.ClosestPoint(rival.bodies[1].worldCenterOfMass+exposed);
    }
    void ApplyHazards(GBApp app,GBWrestler me,ref Vector3 move,ref int bits){
        if(me.hanging){bits=GBInput.Left|GBInput.Right|GBInput.Jump;Intent="CLIMBING BACK";}
        if(app.CurrentArena==0){foreach(var beam in app.Arena.sweepers)if(Vector3.Distance(beam.position,me.Root.position)<3.5f)bits|=GBInput.Brace;}
        if(app.CurrentArena==2&&(app.Arena.WarningSection>=0||app.Arena.sudden)&&Vector3.ProjectOnPlane(me.Root.position,Vector3.up).magnitude>3.5f){
            move=-Vector3.ProjectOnPlane(me.Root.position,Vector3.up).normalized*.75f;Intent="LEAVING UNSAFE FLOOR";
        }
        move=SafeMove(app,me.Root.position,move);
    }
    public static Vector3 SafeMove(GBApp app,Vector3 at,Vector3 desired){
        if(desired.sqrMagnitude<.001f)return desired;
        Vector3 forward=desired.normalized;
        for(int n=0;n<5;n++){
            float angle=n==0?0:n==1?55:n==2?-55:n==3?110:-110;
            Vector3 candidate=Quaternion.Euler(0,angle,0)*forward;
            Vector3 future=at+candidate*.85f;
            RaycastHit hit;
            bool floor=Physics.Raycast(future+Vector3.up*.35f,Vector3.down,out hit,2.5f,1,QueryTriggerInteraction.Ignore)&&hit.point.y>at.y-1.7f;
            bool blocked=Physics.SphereCast(at+Vector3.up*.2f,.2f,candidate,out hit,.65f,1,QueryTriggerInteraction.Ignore);
            if(floor&&!blocked&&!app.Arena.Eliminated(future))return candidate*desired.magnitude;
        }
        return Vector3.zero;
    }
}
