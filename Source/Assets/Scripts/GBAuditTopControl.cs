using UnityEngine;
using System;
using System.Collections;

// Prepared connected poses isolate the new position leverage and its actual
// force/reaction budget. Live manual pins/bridges remain in the pin suite.
public static class GBAuditTopControl {
    sealed class Sample {public float load,bias,leverage;public bool supported,cover;public Vector3 force,reaction;}
    static float Mat(GBApp app){foreach(var c in app.Arena.GetComponentsInChildren<Collider>())if(c.enabled&&c.name=="ring mat")return c.bounds.max.y;throw new InvalidOperationException("Authored mat missing");}
    static void Translate(GBWrestler w,Vector3 offset){foreach(var rb in w.bodies){rb.position+=offset;rb.transform.SetPositionAndRotation(rb.position,rb.rotation);}Physics.SyncTransforms();}
    static void Setup(GBApp app,Vector3 offset,int grips=2){
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.PrepareTopRestart();Translate(a,offset);b.ResetTo(Vector3.zero);
        Quaternion flat=Quaternion.Euler(-90,0,0);Vector3 root=new Vector3(0,Mat(app)+.26f,0);
        for(int n=0;n<15;n++){var rb=b.bodies[n];rb.position=root+flat*(b.initial[n]-b.initial[0]);rb.rotation=flat;rb.transform.SetPositionAndRotation(rb.position,rb.rotation);rb.linearVelocity=rb.angularVelocity=Vector3.zero;}
        b.downUntil=Time.time+20;b.stamina=GBWrestler.MaxStamina;Physics.SyncTransforms();
        a.command=new GBInput{az=1,flags=GBInput.Pin|GBInput.Left|(grips==2?GBInput.Right:0)};
        for(int h=0;h<grips;h++){a.Attach(h,b.bodies[1].GetComponent<GBTarget>(),b.bodies[1].position);a.hands[h].joint.enabled=false;}
    }
    static Sample Measure(GBApp app,Vector3 move=default){
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];a.command.x=move.x;a.command.z=move.z;
        Vector3 before=b.bodies[1].GetAccumulatedForce(),reaction=a.bodies[1].GetAccumulatedForce();
        a.SendMessage("PinTick",Time.fixedDeltaTime);
        return new Sample{load=a.LeftPinLoad+a.RightPinLoad,bias=a.PinPressureBias,leverage=a.TopControlLeverage,supported=a.TopControlSupported,cover=a.IsCovering,force=b.bodies[1].GetAccumulatedForce()-before,reaction=a.bodies[1].GetAccumulatedForce()-reaction};
    }
    public static IEnumerator Run(GBApp app,Action<string,bool,string> check){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);yield return null;
        app.Paused=true;app.enabled=false;var old=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        Setup(app,Vector3.zero);var good=Measure(app);
        Setup(app,new Vector3(.65f,.35f,0));var poor=Measure(app);
        check("supported_hips_improve_real_top_pressure",good.supported&&!poor.supported&&good.cover&&poor.cover&&good.leverage>poor.leverage+.12f&&good.load>poor.load*1.10f,"same victim/grips: supported low leverage/load="+good.leverage+"/"+good.load+" poor raised position="+poor.leverage+"/"+poor.load+"; eligibility remains viable");
        Setup(app,new Vector3(-.15f,0,0));var left=Measure(app);
        Setup(app,new Vector3(.15f,0,0));var right=Measure(app);
        check("real_hip_position_changes_shoulder_pressure",left.bias<-.08f&&right.bias>.08f,"no move command; left/right actual hip bias="+left.bias+"/"+right.bias);
        Setup(app,Vector3.zero);var neutral=Measure(app);var toward=Measure(app,Vector3.right);var opposite=Measure(app,Vector3.left);
        check("movement_still_steers_existing_weight",neutral.leverage>=1&&toward.bias>.8f&&opposite.bias<-.8f,"supported neutral leverage="+neutral.leverage+" movement bias="+toward.bias+"/"+opposite.bias+"; baseline preload retained");
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        b.bodies[1].linearVelocity=Vector3.up*20;var high=Measure(app);
        check("top_load_remains_bounded_equal_reaction",high.load<=650.001f&&good.load<=650.001f&&high.load>0&&(high.force+high.reaction).magnitude<.01f&&-high.force.y<=960.01f,"shoulder cap="+high.load+" total chest pressure="+(-high.force.y)+" force+reaction="+(high.force+high.reaction).magnitude+"; old650+310N budgets");
        Setup(app,Vector3.zero,1);var one=Measure(app);Setup(app,Vector3.zero,2);var two=Measure(app);
        check("one_arm_top_control_stays_weaker",one.cover&&two.cover&&one.load<two.load*.95f,"same supported hip position one/two actual load="+one.load+"/"+two.load);
        b=app.Wrestlers[1];a=app.Wrestlers[0];b.command=new GBInput{x=1,z=.7f,az=-1,flags=GBInput.Brace};
        var resisted=Measure(app);Vector3 before=b.Root.GetAccumulatedForce()+b.bodies[1].GetAccumulatedForce();float energy=b.stamina;
        b.SendMessage("BridgeAndRoll",Time.fixedDeltaTime);Vector3 bridge=b.Root.GetAccumulatedForce()+b.bodies[1].GetAccumulatedForce()-before;
        check("funded_bridge_can_overcome_top_weight",resisted.cover&&resisted.load<two.load*.35f&&bridge.y>-resisted.force.y+300&&b.stamina<energy,"actual paid bridge upward="+bridge.y+" resisted cover downward="+(-resisted.force.y)+" load="+resisted.load+" energy="+energy+" -> "+b.stamina+"; live shoulder escape separately tested by pin suite");
        a.command.flags&=~GBInput.Pin;var released=Measure(app);
        check("release_ends_top_pressure_immediately",!released.cover&&released.load==0&&released.force.sqrMagnitude<.0001f,"releaseC actual incremental load="+released.load+" force="+released.force);
        Physics.simulationMode=old;app.enabled=true;app.Paused=false;app.ReturnToMenu();
        check("top_control_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
