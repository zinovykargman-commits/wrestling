using UnityEngine;

// Difficulty changes decisions and openings, never mass, forces, reach or stamina.
// All wrestling still goes through the same GBInput path used by the player.
public class GBBot {
    float nextThink,gripSince=-1,heldSince=-1,releaseUntil,strikeAt,tackleAt,throwAt;
    float openingUntil,nextOpening,startedAt=-1;
    int lastDifficulty=-1,lastPartner=-1;
    GBInput decision;
    public int Decisions,Attacks,Throws;
    public string Intent {get;private set;}="READY";

    public GBInput Tick(GBApp app,GBWrestler me,GBWrestler rival){
        int difficulty=Mathf.Clamp(app.AIDifficulty,0,2);
        if(lastDifficulty!=difficulty){lastDifficulty=difficulty;nextThink=0;nextOpening=Time.time+4;openingUntil=0;}
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
        bool pinOpportunity=app.CurrentMode==1&&(rival.Down||Vector3.Dot(rival.bodies[1].transform.up,Vector3.up)<.55f);

        // Easy and Normal deliberately leave recognizable openings after exchanges.
        // Hard opens up when exhausted; it does not gain an invulnerable defense.
        if(difficulty<2&&Time.time>=nextOpening&&!pinOpportunity){
            openingUntil=Time.time+(difficulty==0?1.05f:.55f);
            nextOpening=Time.time+(difficulty==0?5.2f:7.8f);
        }
        bool opening=Time.time<openingUntil;
        if(me.Down){bits|=GBInput.Brace;move=Vector3.zero;Intent="RECOVERING";}
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
                // Holding C targets legs outside pin matches. Hard changes levels
                // against an upright brace instead of repeating the same upper grip.
                if(difficulty==2&&app.CurrentMode!=1&&rival.command.Has(GBInput.Brace)&&!rival.Down)
                    bits|=GBInput.Pin;
            }
            if(me.GripCount>0){
                if(gripSince<0)gripSince=Time.time;
                float gripAge=Time.time-gripSince;
                if(pinOpportunity){bits|=GBInput.Pin;move=aim*.13f;Intent="PINNING";}
                else{
                    bool rivalBracing=rival.command.Has(GBInput.Brace)&&rival.stamina>16;
                    // Pull first to disrupt balance, then turn and drive the throw.
                    // Easy has a long wind-up; Hard pivots around a braced opponent.
                    float windup=difficulty==0?.85f:difficulty==1?.5f:.35f;
                    move=gripAge<windup?-aim*.38f:
                        rivalBracing&&difficulty>0?side*.48f-aim*.15f:aim*.44f+side*.14f;
                    Intent=gripAge<windup?"PULLING":"SETTING UP A THROW";
                    if(gripAge>windup&&me.stamina>35&&!rivalBracing)bits|=GBInput.Lift;
                    // Shove is X while gripping. Keep a full decision with X released
                    // between attempts so the input edge can fire again.
                    if(rivalBracing&&gripAge>windup&&Time.time>=strikeAt&&difficulty>0){
                        bits|=GBInput.Shove;strikeAt=Time.time+(difficulty==2?.95f:1.35f);
                        Attacks++;Intent="SHOVING";
                    }
                    float throwDelay=difficulty==0?2.35f:difficulty==1?1.6f:1.15f;
                    bool throwWindow=!rivalBracing||rival.balance<42||gripAge>throwDelay+1.5f;
                    if(gripAge>throwDelay&&throwWindow&&Time.time>=throwAt&&me.stamina>24){
                        bits|=GBInput.Throw;Throws++;Intent="THROWING";
                        throwAt=Time.time+(difficulty==0?3.5f:difficulty==1?2.7f:2.1f);
                        releaseUntil=Time.time+(difficulty==0?1.1f:.7f);
                        gripSince=-1;
                    }
                }
            }else{
                gripSince=-1;
                if(distance<1.3f&&Time.time>=strikeAt&&me.stamina>35&&!pinOpportunity&&!defending){
                    bits=(bits&~(GBInput.Left|GBInput.Right))|GBInput.Strike;
                    strikeAt=Time.time+(difficulty==0?1.65f:difficulty==1?1.05f:.8f);
                    Attacks++;Intent="SHOOTING";
                }
                bool tackleWindow=Time.time-startedAt>(difficulty==0?3f:1f);
                if(distance>1.5f&&distance<2.2f&&tackleWindow&&Time.time>=tackleAt&&me.stamina>62&&me.grounded&&!defending&&!rival.Down){
                    bits|=GBInput.Tackle;tackleAt=Time.time+(difficulty==0?7f:difficulty==1?5.2f:3.8f);
                    Attacks++;Intent="TACKLING";
                }
                if(difficulty==2&&distance>3&&me.stamina>70)bits|=GBInput.Sprint;
            }
        }
        if(me.GripCount==0)gripSince=-1;
        ApplyHazards(app,me,ref move,ref bits);
        return Save(move,aim,bits);
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
        // Resisting partners never initiate grabs, strikes, tackles or throws.
        // Their only motion is a gentle physical escape while being held.
        move=SafeMove(app,me.Root.position,move);
        return Save(move,aim,bits);
    }

    GBInput Save(Vector3 move,Vector3 aim,int bits){
        decision=new GBInput{x=move.x,z=move.z,ax=aim.x,az=aim.z,flags=bits};return decision;
    }
    public static float ThinkInterval(int difficulty){return difficulty<=0?.38f:difficulty==1?.18f:.1f;}
    static float ReactionDelay(int difficulty){return difficulty<=0?.6f:difficulty==1?.28f:.14f;}
    static bool HeldBy(GBWrestler rival,GBWrestler me){
        foreach(var h in rival.hands)if(h.target&&h.target.wrestler==me)return true;
        return false;
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
