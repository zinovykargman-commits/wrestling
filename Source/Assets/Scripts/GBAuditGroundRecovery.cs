using UnityEngine;
using System;
using System.Collections;

// Production stamina ticks against prepared physical mat contacts.
public static class GBAuditGroundRecovery {
    static void Flat(GBWrestler w,bool front,float height=.46f){
        w.ResetTo(Vector3.zero);Quaternion rotation=Quaternion.Euler(front?90:-90,0,0);
        for(int n=0;n<15;n++){
            var body=w.bodies[n];Vector3 position=new Vector3(.35f,height,0)+rotation*(w.initial[n]-w.initial[0]);
            body.interpolation=RigidbodyInterpolation.None;body.position=position;body.rotation=rotation;
            body.transform.SetPositionAndRotation(position,rotation);body.linearVelocity=body.angularVelocity=Vector3.zero;
        }
        w.downUntil=Time.time+60;w.stamina=40;Physics.SyncTransforms();
    }
    public static IEnumerator Run(GBApp app,Action<string,bool,string> check){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.Paused=true;app.enabled=false;
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];var rest=new GBInput{az=1};float dt=Time.fixedDeltaTime;
        check("ground_recovery_delay_is_2_5_seconds",GBWrestler.GroundStaminaRegenDelay==2.5f,"production delay, original19 stamina/s rate retained");
        foreach(bool front in new[]{false,true}){
            Flat(b,front);a.ResetTo(new Vector3(-3,.28f,0));yield return null;string face=front?"chest":"back";
            int early=Mathf.FloorToInt(2.49f/dt);for(int n=0;n<early;n++)b.Simulate(rest);
            check(face+"_rest_waits_before_2_5_seconds",b.stamina==40&&!b.GroundStaminaRecoveryReady&&b.GroundStaminaSeconds>2.48f,"actual ground seconds="+b.GroundStaminaSeconds+" stamina="+b.stamina);
            for(int n=0;n<8;n++)b.Simulate(rest);
            float before=b.stamina;b.Simulate(rest);float rate=(b.stamina-before)/dt;
            check(face+"_rest_recovers_after_delay",b.GroundStaminaRecoveryReady&&b.stamina>40&&Mathf.Abs(rate-19)<.04f,"ready="+b.GroundStaminaRecoveryReady+" rate="+rate+" ground seconds="+b.GroundStaminaSeconds);
            float tired=b.stamina;b.Simulate(new GBInput{az=1,x=1,flags=GBInput.Brace});
            check(face+"_active_scramble_still_costs",b.stamina<tired&&b.command.Has(GBInput.Brace),"funded ground escape cannot regenerate while paying effort");
            tired=b.stamina;for(int n=0;n<20;n++){b.Simulate(rest);b.Simulate(new GBInput{az=1,x=1,flags=GBInput.Brace});}
            check(face+"_rapid_ctrl_taps_do_not_refill",b.stamina<=tired&&b.GroundStaminaRecoveryReady,"ready ground timer does not bypass raw Ctrl recovery block");
            float waiting=b.stamina;yield return new WaitForSeconds(.85f);b.Simulate(rest);
            check(face+"_rest_after_scramble_recovers",b.stamina>waiting,"original0.8s attempt recovery delay expires; ground timer need not restart");
            foreach(var body in b.bodies){body.position+=Vector3.up*2;body.transform.SetPositionAndRotation(body.position,body.rotation);}Physics.SyncTransforms();b.Simulate(rest);
            check(face+"_airborne_is_not_ground_rest",b.GroundStaminaSeconds==0&&!b.GroundStaminaRecoveryReady,"lifted core has no mat contact");
            Flat(b,front);a.ResetTo(new Vector3(-.55f,-.08f,0));a.command=new GBInput{az=1,flags=GBInput.Left|GBInput.Pin};
            a.Attach(0,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[0].joint.enabled=false;b.downUntil=0;a.SendMessage("PinRefresh");yield return null;
            check(face+"_real_pin_control_fixture",b.PinControlled&&b.IsBeingPinned,"existing live grip/C and physically grounded opponent");
            for(int n=0;n<early;n++)b.Simulate(rest);
            check(face+"_pinned_rest_waits_before_delay",b.stamina==40&&!b.GroundStaminaRecoveryReady,"pinning blocks early passive recovery");
            for(int n=0;n<8;n++)b.Simulate(rest);before=b.stamina;b.Simulate(rest);rate=(b.stamina-before)/dt;
            check(face+"_pinned_rest_recovers_after_delay",b.PinControlled&&b.IsBeingPinned&&b.GroundStaminaRecoveryReady&&Mathf.Abs(rate-19)<.04f,"live opponent pressure retained; rate="+rate);
            before=b.stamina;b.Attach(0,a.bodies[1].GetComponent<GBTarget>(),a.bodies[1].position);b.hands[0].joint.enabled=false;b.Simulate(new GBInput{az=1,flags=GBInput.Left});
            check(face+"_own_grip_still_costs",b.stamina<before,"own grip remains2/s");
            b.ResetTo(new Vector3(3,.28f,0));a.ResetTo(new Vector3(-3,.28f,0));b.stamina=40;b.Simulate(rest);
            check(face+"_reset_clears_ground_delay",b.GroundStaminaSeconds==0&&!b.GroundStaminaRecoveryReady&&b.stamina>40,"fresh standing round uses original immediate recovery");
        }
        foreach(var w in app.Wrestlers)foreach(var body in w.bodies)body.interpolation=RigidbodyInterpolation.Interpolate;
        app.enabled=true;app.Paused=false;app.ResetRound(false);Physics.simulationMode=mode;app.ReturnToMenu();
        check("ground_recovery_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
