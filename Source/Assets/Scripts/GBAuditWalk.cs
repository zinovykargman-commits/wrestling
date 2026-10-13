using UnityEngine;
using System;
using System.IO;
using System.Collections;

public static class GBAuditWalk {
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(100);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(-1.6f,.28f,-1.6f));b.ResetTo(new Vector3(1.6f,.28f,0));
        app.ScriptedInputs[0]=new GBInput{az=1};app.ScriptedInputs[1]=new GBInput{az=-1};yield return new WaitForSeconds(1);
        Vector3 start=a.Root.position;float gap=0,knee=0,lift=0;bool left=false,right=false,upright=true;int frames=0;
        float end=Time.time+3;app.ScriptedInputs[0]=new GBInput{az=1,z=.55f};
        while(Time.time<end){
            float difference=a.bodies[11].position.y-a.bodies[14].position.y;
            lift=Mathf.Max(lift,Mathf.Abs(difference));left|=difference>.045f;right|=difference<-.045f;
            foreach(int thigh in new[]{9,12}){
                Quaternion relative=Quaternion.Inverse(a.bodies[thigh].rotation)*a.bodies[thigh+1].rotation;
                knee=Mathf.Max(knee,Mathf.Abs(Mathf.DeltaAngle(0,relative.eulerAngles.x)));
            }
            gap=Mathf.Max(gap,a.JointSeparation());upright&=!a.Down&&Vector3.Dot(a.bodies[1].rotation*Vector3.up,Vector3.up)>.65f;
            if(frames++%12==0)app.Capture(Path.Combine(directory,"walk-"+(frames/12).ToString("000")+".png"));
            yield return null;
        }
        app.ScriptedInputs[0]=new GBInput{az=1};
        check("walking_moves_with_alternating_physical_steps",Vector3.Distance(a.Root.position,start)>.5f&&left&&right,"root travel="+Vector3.Distance(a.Root.position,start).ToString("0.00")+" m, left/right raised="+left+"/"+right+", foot height difference="+lift.ToString("0.000"));
        check("walking_knees_bend_without_losing_connected_upright_body",knee>12&&upright&&gap<.06f,"actual knee bend="+knee.ToString("0.0")+" degrees, upright="+upright+", joint gap="+gap.ToString("0.0000")+" m");
    }
}
