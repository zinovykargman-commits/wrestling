using UnityEngine;
using System;

public static class GBAuditArms {
    public static string ReachEvidence(GBWrestler wrestler,int h){
        var hand=wrestler.hands[h];Vector3 goal=wrestler.ArmAimGoal(h);
        return " hand="+wrestler.bodies[h==0?5:8].position+" reachableGoal="+goal+" physicalError="+Vector3.Distance(wrestler.bodies[h==0?5:8].position,goal).ToString("0.000")+" candidate="+(hand.candidate?hand.candidate.region:"none")+" releaseRequired="+hand.releaseRequired+" clamped="+wrestler.ArmAimClamped(h)+" root="+wrestler.Root.position+" shoulder="+wrestler.ArmShoulder(h);
    }
    public static void Free(GBApp app,Action<string,bool,string> check){
        var wrestler=app.Wrestlers[app.Views.PrimaryPlayer];Vector3 left=wrestler.ArmAimGoal(0),right=wrestler.ArmAimGoal(1);
        float leftReach=Vector3.Distance(left,wrestler.ArmShoulder(0)),rightReach=Vector3.Distance(right,wrestler.ArmShoulder(1));
        check("hand_markers_use_feasible_individual_goals",leftReach<=.8371f&&rightReach<=.8371f&&Vector3.Distance(left,right)>.04f,
            "actual shoulder reach L/R="+leftReach.ToString("0.000")+"/"+rightReach.ToString("0.000")+" m, goal separation="+Vector3.Distance(left,right).ToString("0.000")+" m; no raw unreachable shared cursor");
        check("hand_markers_only_owned_wrestler",app.ShowsArmAimFor(wrestler)&&!app.ShowsArmAimFor(app.Wrestlers[1-app.Views.PrimaryPlayer]),
            "one local camera player="+app.Views.PrimaryPlayer+"; opposing aim does not draw");
    }
    public static void Held(GBApp app,GBWrestler wrestler,int h,Action<string,bool,string> check){
        var hand=wrestler.hands[h];var marked=wrestler.ArmMarkedTarget(h);
        check("hand_highlight_names_actual_manual_grip",hand.joint&&hand.target&&marked==hand.target&&marked.wrestler!=wrestler,
            "marked="+(marked?marked.region:"none")+"; contact="+(hand.target?hand.target.transform.TransformPoint(hand.localAnchor).ToString():"none")+"; existing real manual hold, no material/collider mutation");
    }
}
