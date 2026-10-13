using UnityEngine;
using System.Collections;
using System.IO;

public partial class GBAudit {
    IEnumerator AppearanceRun(){
        app.BeginMatch(3,1);Neutral();yield return Wait(3.5f);
        bool finite=true,normalized=true,mapped=true,textured=true,shoeLocked=true;int vertices=0,parts=0,soleVertices=0;
        foreach(var wrestler in app.Wrestlers){
            var visual=wrestler.GetComponentInChildren<GBRealisticWrestler>();
            mapped&=visual&&visual.Skins.Length>=20;
            foreach(var skin in visual.Skins){
                parts++;vertices+=skin.sharedMesh.vertexCount;mapped&=skin.bones.Length==17&&skin.sharedMesh.bindposes.Length==17;
                for(int b=0;b<15;b++){
                    if(b==5||b==8){
                        int hand=b==5?0:1;var proxy=visual.HandContact.ShownHand(hand);
                        mapped&=skin.bones[b]==proxy&&!proxy.GetComponent<Rigidbody>()&&!proxy.GetComponent<Collider>()&&Vector3.Distance(proxy.position,wrestler.bodies[b].transform.position)<=.0281f&&Quaternion.Angle(proxy.rotation,wrestler.bodies[b].transform.rotation)<.01f;
                    }else mapped&=skin.bones[b]==wrestler.bodies[b].transform;
                }
                for(int b=15;b<17;b++)mapped&=skin.bones[b]&&skin.bones[b].name.Contains("render-only shoulder")&&!skin.bones[b].GetComponent<Rigidbody>()&&!skin.bones[b].GetComponent<Collider>();
                foreach(var weight in skin.sharedMesh.boneWeights){float sum=weight.weight0+weight.weight1+weight.weight2+weight.weight3;normalized&=Mathf.Abs(sum-1)<.0001f;}
                if(skin.name.Contains("shoe sole")||skin.name.Contains("fabric lace"))foreach(var weight in skin.sharedMesh.boneWeights){soleVertices++;shoeLocked&=(weight.boneIndex0==11||weight.boneIndex0==14)&&weight.weight0>.9999f;}
                var baked=new Mesh();skin.BakeMesh(baked);
                foreach(var v in baked.vertices)finite&=!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&v.magnitude<6;
                Destroy(baked);
                foreach(var mat in skin.sharedMaterials)if(mat.name.Contains("skin"))textured&=mat.mainTexture&&mat.GetTexture("_BumpMap");
            }
        }
        Check("appearance_real_physical_bones",mapped,"17 skin slots follow15 physical regions,2 shoulder pivots and2 replacement hand render proxies (bounded0.028m, exact physical rotation); parts="+parts);
        Check("appearance_normalized_finite_skin",normalized&&finite,"four normalized influences, current baked finite positions; vertices="+vertices);
        Check("appearance_licensed_skin_texture",textured,"diffuse and anatomical normal textures present on both athletes");
        Check("appearance_no_new_physics",app.Registry.Count==92,"same92 registered bodies and15 per athlete");
        int excluded=0,unexpected=0,enabledPairs=0;
        foreach(var w in app.Wrestlers)for(int i=0;i<15;i++)for(int j=i+1;j<15;j++){
            bool adjacent=w.parents[i]==j||w.parents[j]==i;bool ignored=Physics.GetIgnoreCollision(w.bodies[i].GetComponent<Collider>(),w.bodies[j].GetComponent<Collider>());
            if(ignored){excluded++;if(!adjacent)unexpected++;}else enabledPairs++;
        }
        Check("both_athletes_limb_and_torso_self_contact",excluded==28&&unexpected==0&&enabledPairs==182,"both athletes enabled limb/body pairs="+enabledPairs+"; only28 connected overlapping joint pairs excluded");
        Check("shoe_soles_laces_locked_to_physical_feet",shoeLocked&&soleVertices>0,"foot-only rigid attachment vertices="+soleVertices+"; flexible ankle collars follow calf/foot skin");
        bool ankleLimits=true;foreach(var w in app.Wrestlers)foreach(int foot in new[]{11,14})ankleLimits&=w.joints[foot].lowAngularXLimit.limit==-55&&w.joints[foot].highAngularXLimit.limit==25&&w.joints[foot].angularYLimit.limit==20&&w.joints[foot].angularZLimit.limit==20;
        Check("ankles_have_anatomical_bend_and_twist_stops",ankleLimits,"four ankles: physical25-degree dorsiflexion/55-degree plantarflexion,20-degree side/twist stops; dynamic render/contact checks required");
        var a=app.Wrestlers[0];var bperson=app.Wrestlers[1];
        float left=Mathf.Abs(a.bodies[10].position.x-a.bodies[9].position.x),right=Mathf.Abs(a.bodies[13].position.x-a.bodies[12].position.x);
        Check("appearance_straight_ready_leg_axes",left<.05f&&right<.05f,"live ready-state thigh/shin lateral center differences="+left.ToString("0.000")+"/"+right.ToString("0.000")+" m; render inspection still required");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-player-view.png"));yield return Wait(.2f);
        app.enabled=false;
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies){rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.None;}
        a.ResetTo(new Vector3(-.6f,.2f,0));bperson.ResetTo(new Vector3(.65f,.2f,0));
        bool aligned=true;float worstSoleGap=0;
        foreach(var w in app.Wrestlers)foreach(var skin in w.GetComponentInChildren<GBRealisticWrestler>().Skins)if(skin.name.Contains("shoe sole")){
            var baked=new Mesh();skin.BakeMesh(baked);float low=float.PositiveInfinity;
            foreach(var v in baked.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);
            worstSoleGap=Mathf.Max(worstSoleGap,Mathf.Abs(low-.2f));aligned&=Mathf.Abs(low-.2f)<.012f;Destroy(baked);
        }
        Check("upright_both_shoe_soles_align_with_mat",aligned,"front/back straight-pose maximum actual sole-bottom offset="+worstSoleGap.ToString("0.0000")+" m");
        var camera=app.Views.Primary;camera.fieldOfView=46;camera.transform.position=new Vector3(0,1.6f,3.8f);camera.transform.LookAt(new Vector3(0,1.15f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-front.png"));yield return Wait(.2f);
        camera.transform.position=new Vector3(0,1.6f,-3.8f);camera.transform.LookAt(new Vector3(0,1.15f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-back.png"));yield return Wait(.2f);
        camera.fieldOfView=32;camera.transform.position=new Vector3(0,1.95f,3.8f);camera.transform.LookAt(new Vector3(0,1.9f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-face.png"));yield return Wait(.2f);
        camera.fieldOfView=40;camera.transform.position=new Vector3(4,1.6f,3.2f);camera.transform.LookAt(new Vector3(0,1.15f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-side.png"));yield return Wait(.2f);
        var otherVisual=bperson.GetComponentInChildren<GBRealisticWrestler>().gameObject;otherVisual.SetActive(false);
        camera.fieldOfView=35;
        for(int side=-1;side<=1;side+=2){
            camera.transform.position=new Vector3(a.Root.position.x+side*2.8f,1.5f,0);camera.transform.LookAt(new Vector3(a.Root.position.x,1.35f,0));
            yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,side<0?"appearance-left-profile.png":"appearance-right-profile.png"));yield return Wait(.2f);
        }
        otherVisual.SetActive(true);
        // Frozen physical articulation diagnostic uses the same anchored bodies.
        // No posed snapshot is represented as a gameplay movement measurement.
        for(int n=1;n<15;n++){
            Quaternion local=Quaternion.identity;
            if(n==3||n==6)local=Quaternion.Euler(-75,0,n==3?-12:12);
            if(n==4||n==7)local=Quaternion.Euler(-95,0,0);
            if(n==9||n==12)local=Quaternion.Euler(-55,0,0);
            if(n==10||n==13)local=Quaternion.Euler(100,0,0);
            var parent=a.bodies[a.parents[n]];var child=a.bodies[n];child.rotation=parent.rotation*local;
            child.position=parent.position+parent.rotation*a.joints[n].connectedAnchor-child.rotation*a.joints[n].anchor;
        }
        camera.fieldOfView=43;camera.transform.position=new Vector3(3,1.8f,4.5f);camera.transform.LookAt(new Vector3(0,1.15f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-articulation.png"));yield return Wait(.2f);
        otherVisual.SetActive(false);camera.fieldOfView=36;camera.transform.position=new Vector3(a.Root.position.x+2.5f,1.65f,.8f);camera.transform.LookAt(new Vector3(a.Root.position.x,1.35f,0));
        yield return Wait(.2f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"appearance-bent-side.png"));yield return Wait(.2f);otherVisual.SetActive(true);
        Check("appearance_articulated_anchors_connected",a.JointSeparation()<.001f,"frozen bent arms/knees real joint anchors gap="+a.JointSeparation().ToString("0.00000"));
        finite=true;
        foreach(var skin in a.GetComponentInChildren<GBRealisticWrestler>().Skins){var baked=new Mesh();skin.BakeMesh(baked);foreach(var v in baked.vertices)finite&=!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&v.magnitude<6;Destroy(baked);}
        Check("appearance_articulated_mesh_finite",finite,"baked bent-limb surface finite; inspected native articulation screenshot required");
        // Close matched diagnostics for the user's two deferred arm defects.
        // These are frozen, joint-connected inspection poses, not a gameplay
        // animation or evidence that actual movement is already correct.
        a.ResetTo(new Vector3(-.45f,.2f,0));bperson.ResetTo(new Vector3(.45f,.2f,0));
        camera.fieldOfView=31;camera.transform.position=new Vector3(0,1.52f,3.1f);camera.transform.LookAt(new Vector3(0,1.48f,0));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"arms-neutral-front-close.png"));yield return Wait(.3f);
        camera.transform.position=new Vector3(0,1.55f,-3.1f);camera.transform.LookAt(new Vector3(0,1.48f,0));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"arms-neutral-back-close.png"));yield return Wait(.3f);
        foreach(var w in app.Wrestlers){
            w.Root.rotation=Quaternion.Euler(70,0,0);
            for(int n=1;n<15;n++){
                Quaternion local=Quaternion.identity;
                if(n==3||n==6)local=Quaternion.Euler(-150,0,n==3?-8:8);
                if(n==4||n==7)local=Quaternion.Euler(-65,0,0);
                var parent=w.bodies[w.parents[n]];var child=w.bodies[n];child.rotation=parent.rotation*local;
                child.position=parent.position+parent.rotation*w.joints[n].connectedAnchor-child.rotation*w.joints[n].anchor;
            }
        }
        camera.fieldOfView=37;camera.transform.position=new Vector3(0,3.25f,-1.4f);camera.transform.LookAt(new Vector3(0,1.15f,.32f));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"arms-overhead-back.png"));yield return Wait(.3f);
        camera.transform.position=new Vector3(0,2.95f,2.3f);camera.transform.LookAt(new Vector3(0,1.3f,.4f));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"arms-overhead-front.png"));yield return Wait(.3f);
        camera.transform.position=new Vector3(3.1f,1.7f,.6f);camera.transform.LookAt(new Vector3(0,1.15f,.3f));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"arms-overhead-side.png"));yield return Wait(.3f);
        Check("both_overhead_arm_chains_connected",a.JointSeparation()<.001f&&bperson.JointSeparation()<.001f,"both pitched-forward arm poses keep their real joint anchors connected; visual inspection required");
        a.ResetTo(new Vector3(-.5f,.28f,0));bperson.ResetTo(new Vector3(2.2f,.28f,1.6f));
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies){rb.isKinematic=false;rb.interpolation=RigidbodyInterpolation.Interpolate;}
        app.enabled=true;app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(30);
        app.ScriptedInputs[0]=new GBInput{az=1,hy=1.10f,hz=.75f,flags=GBInput.HandAim};
        app.ScriptedInputs[1]=new GBInput{az=1};yield return Wait(.9f);
        Check("live_raised_mouse_reach_connected",a.bodies[5].position.y>a.ArmShoulder(0).y+.10f&&a.bodies[8].position.y>a.ArmShoulder(1).y+.10f&&a.JointSeparation()<.03f&&a.GripCount==0,"actual independent hand motors reach overhead; no prepared pose/grip; L/R hands="+a.bodies[5].position+" / "+a.bodies[8].position+" gap="+a.JointSeparation());
        app.enabled=false;foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)rb.isKinematic=true;
        camera.fieldOfView=34;camera.transform.position=new Vector3(-.5f,2.15f,2.6f);camera.transform.LookAt(new Vector3(-.5f,1.8f,.25f));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"live-raised-reach-front.png"));yield return Wait(.3f);
        camera.transform.position=new Vector3(-.5f,2.15f,-2.6f);camera.transform.LookAt(new Vector3(-.5f,1.8f,.25f));
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"live-raised-reach-back.png"));yield return Wait(.3f);
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)rb.isKinematic=false;app.enabled=true;
        app.ScriptedInputs[0]=new GBInput{az=1,hy=1.10f,hz=.75f,flags=GBInput.HandAim|GBInput.Sprawl};yield return Wait(.75f);
        Check("live_sprawl_reach_connected",a.Sprawling&&a.JointSeparation()<.03f&&a.GripCount==0,"actual heldE with independent raisedmouse target; physics-only posture, gap="+a.JointSeparation());
        app.enabled=false;foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)rb.isKinematic=true;
        camera.fieldOfView=37;camera.transform.position=a.Root.position+new Vector3(0,2.1f,-1.1f);camera.transform.LookAt(a.bodies[1].position+Vector3.forward*.4f);
        yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"live-sprawl-reach-back.png"));yield return Wait(.3f);
        app.enabled=true;
    }
}
