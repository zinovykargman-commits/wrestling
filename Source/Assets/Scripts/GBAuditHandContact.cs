using UnityEngine;
using System;
using System.IO;
using System.Collections;

public static class GBAuditHandContact {
    static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.LocalDevice=0;app.TrainingPartner=0;app.ScriptedInputs=new GBInput[2];app.BeginMatch(3,1);
        // Isolate a live fighting fixture as the existing natural-grip tests do.
        app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(60);
        app.ScriptedInputs[0]=new GBInput{ax=1};app.ScriptedInputs[1]=new GBInput{ax=-1};yield return Wait(.1f);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(-.5f,.25f,0));b.ResetTo(new Vector3(.5f,.25f,0));yield return Wait(1.0f);
        var fit=a.GetComponentInChildren<GBRealisticWrestler>().HandContact;
        check("hand_contact_samples_authored_palms",fit.PalmSampleCount(0)>=16&&fit.PalmSampleCount(1)>=16,"actual authored palm/finger vertices sampled L/R="+fit.PalmSampleCount(0)+"/"+fit.PalmSampleCount(1));
        fit.RenderFrame(.02f);
        check("free_hands_have_no_visual_contact_offset",fit.Correction(0)==Vector3.zero&&fit.Correction(1)==Vector3.zero&&a.GripCount==0,"neutral/free aim stays at actual interpolated physical hands");
        float end=Time.realtimeSinceStartup+5.5f,maxOffset=0,maxRotation=0,minGap=float.PositiveInfinity,bestImprovement=0,nextLog=0;
        int heldFrames=0,contactFrames=0;bool bounded=true;
        while(Time.realtimeSinceStartup<end){
            // A real player points at an exposed surface, not the center inside
            // the rival. Match the existing successful manual-grip fixtures.
            Vector3 contact=b.bodies[1].GetComponent<Collider>().ClosestPoint(a.bodies[1].position);
            Vector3 to=contact-a.Root.position,heading=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up).normalized;
            float move=Vector3.Distance(a.Root.position,b.Root.position)>.65f?.40f:0;
            int buttons=(a.hands[0].releaseRequired?0:GBInput.Left)|(a.hands[1].releaseRequired?0:GBInput.Right);
            app.ScriptedInputs[0]=new GBInput{x=heading.x*move,z=heading.z*move,ax=heading.x,az=heading.z,hx=to.x,hy=to.y,hz=to.z,flags=GBInput.HandAim|buttons};
            app.ScriptedInputs[1]=new GBInput{ax=-heading.x,az=-heading.z};
            yield return null;
            fit.RenderFrame(Time.deltaTime);
            if(Time.realtimeSinceStartup>=nextLog){
                nextLog=Time.realtimeSinceStartup+.5f;
                Debug.Log("HAND_CONTACT_LIVE phase="+app.Phase+" command="+a.command.flags+" roots="+a.Root.position+" / "+b.Root.position+" cursor="+(a.Root.position+a.command.HandOffset)+" gapL/R="+fit.UncorrectedContactGap(0)+"/"+fit.UncorrectedContactGap(1)+" fitL/R="+fit.ContactEligible(0)+"/"+fit.ContactEligible(1)+GBAuditArms.ReachEvidence(a,0)+GBAuditArms.ReachEvidence(a,1));
            }
            for(int hand=0;hand<2;hand++){
                int index=hand==0?5:8;var actual=a.bodies[index].transform;
                maxOffset=Mathf.Max(maxOffset,fit.Correction(hand).magnitude);
                maxRotation=Mathf.Max(maxRotation,Quaternion.Angle(fit.ShownHand(hand).rotation,actual.rotation));
                bounded&=fit.Correction(hand).magnitude<=GBHandContactVisual.MaximumCorrection+.0001f;
                if(a.hands[hand].joint&&a.hands[hand].target&&a.hands[hand].target.wrestler==b)heldFrames++;
                if(fit.ContactEligible(hand)){
                    contactFrames++;minGap=Mathf.Min(minGap,fit.CorrectedContactGap(hand));
                    bestImprovement=Mathf.Max(bestImprovement,fit.UncorrectedContactGap(hand)-fit.CorrectedContactGap(hand));
                }
            }
            if(contactFrames>16&&heldFrames>24)break;
        }
        check("contact_fit_uses_actual_manual_body_hold",heldFrames>0&&contactFrames>0,"normal mouse-button inputs acquired held frames="+heldFrames+"; nearby rendered-palm frames="+contactFrames+"; no Attach or posed arms in this fixture");
        check("hand_visual_adjustment_is_small_and_rotation_exact",bounded&&maxRotation<.01f,"max shown offset="+maxOffset+" m, limit0.028m; physical wrist rotation difference="+maxRotation);
        check("nearby_palm_closes_existing_contact_gap",contactFrames>0&&(bestImprovement>.0005f||minGap<=.006f),"best sampled gap improvement="+bestImprovement+" m; best shown sample gap="+minGap+" m; actual close-up inspection still required");
        var positions=new Vector3[15];var rotations=new Quaternion[15];var forces=new Vector3[15];
        for(int n=0;n<15;n++){positions[n]=a.bodies[n].position;rotations[n]=a.bodies[n].rotation;forces[n]=a.bodies[n].GetAccumulatedForce();}
        fit.RenderFrame(.02f);bool physicalSame=true;
        for(int n=0;n<15;n++)physicalSame&=positions[n]==a.bodies[n].position&&rotations[n]==a.bodies[n].rotation&&forces[n]==a.bodies[n].GetAccumulatedForce();
        check("render_contact_adds_no_physics_or_force",physicalSame&&app.Registry.Count==92,"render pass preserves all15 actual rigid transforms and accumulated forces; registry92");
        if(heldFrames>0){
            Vector3 midpoint=(a.bodies[5].position+a.bodies[8].position+b.bodies[1].position)/3;
            app.CaptureAt(Path.Combine(directory,"held-body-contact-front.png"),midpoint+new Vector3(.15f,.50f,1.45f),midpoint);
            app.CaptureAt(Path.Combine(directory,"held-body-contact-side.png"),midpoint+new Vector3(1.45f,.30f,.15f),midpoint);
        }
        app.ScriptedInputs[0]=new GBInput{ax=1};yield return Wait(.15f);fit.RenderFrame(.02f);
        check("released_grips_clear_visual_offset_immediately",a.GripCount==0&&fit.Correction(0)==Vector3.zero&&fit.Correction(1)==Vector3.zero,"normal button release leaves free arm aim untouched");
        a.ResetTo(new Vector3(-.55f,.2f,0));fit.RenderFrame(.02f);
        check("round_reset_clears_hand_contact_visuals",fit.Correction(0)==Vector3.zero&&fit.Correction(1)==Vector3.zero&&!fit.ContactEligible(0)&&!fit.ContactEligible(1),"reset returns each render proxy to its real physical hand");
        check("hand_contact_no_runtime_issues",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
