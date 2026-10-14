using UnityEngine;
using System.Collections;
using System.IO;

public partial class GBAudit {
    IEnumerator GymRun(){
        app.BeginMatch(3,1);Neutral();yield return Wait(3.3f);
        var details=app.Arena.transform.Find("Gym visual details");
        Check("gym_decorations_present",details&&details.childCount>120,"detail objects="+(details?details.childCount:0));
        Check("gym_scenery_has_no_colliders_or_targets",details&&details.GetComponentsInChildren<Collider>().Length==0&&details.GetComponentsInChildren<GBTarget>().Length==0,"ringside decor cannot obstruct, grip or alter movement");
        Check("gym_registry_compatible",app.Registry.Count==92&&app.Arena.ropes.Count==48,"unchanged registered bodies="+app.Registry.Count+" rope segments="+app.Arena.ropes.Count);
        bool roundRopes=details&&details.GetComponent<GBRopeVisual>();int ropeSkins=0;
        foreach(var skin in details.GetComponentsInChildren<MeshFilter>())if(skin.name=="Continuous braided rope"){ropeSkins++;roundRopes&=skin.sharedMesh&&skin.sharedMesh.vertexCount==338&&skin.GetComponents<Collider>().Length==0;}
        foreach(var rope in app.Arena.ropes)roundRopes&=!rope.GetComponent<Renderer>().enabled&&rope.GetComponent<BoxCollider>().enabled&&rope.GetComponent<ConfigurableJoint>();
        Check("gym_rope_skins_follow_physics",roundRopes&&ropeSkins==12,"12 rounded continuous skins smoothly follow48 original physical segments; original colliders/constraints stay enabled, no new collision bodies");
        var canvas=details.Find("Printed woven canvas").GetComponent<Renderer>();
        Check("gym_matte_canvas_shader",canvas.sharedMaterial.shader&&canvas.sharedMaterial.shader.name=="GripBreak/MatteCanvas"&&canvas.sharedMaterial.shader.isSupported,"native shader="+canvas.sharedMaterial.shader.name+" keywords="+string.Join(",",canvas.sharedMaterial.shaderKeywords)+"; actual mat views captured below");
        Check("gym_textured_canvas",canvas.sharedMaterial.mainTexture&&canvas.sharedMaterial.mainTexture.width==2048&&canvas.sharedMaterial.GetTexture("_BumpMap")&&canvas.sharedMaterial.GetTexture("_MetallicGlossMap"),"2k photographed weave, normal and roughness-derived smoothness");
        Check("gym_roof_and_real_materials",details.Find("Solid concrete ceiling")&&Resources.Load<Texture2D>("Gym/rubber_tiles_normal")&&Resources.Load<Texture2D>("Gym/painted_plaster_wall_color"),"closed ceiling and licensed PBR surface maps");
        int pads=0,benches=0,banners=0,bags=0,weights=0;foreach(Transform t in details.GetComponentsInChildren<Transform>()){if(t.name=="Upholstered corner cushion")pads++;if(t.name=="Timber ringside bench")benches++;if(t.name=="Hanging club banner")banners++;if(t.name=="Hanging heavy bag")bags++;if(t.name=="Dumbbell handle")weights++;}
        Check("gym_corner_cushions",pads==12,"three rounded cushions per corner="+pads);
        Check("gym_benches_banners_equipment",benches==6&&banners==2&&bags==3&&weights==12,"benches="+benches+" banners="+banners+" bags="+bags+" dumbbells="+weights);
        string layout;Check("gym_station_layout_clear",GBGymVisual.LayoutClear(app.Arena,out layout),layout);
        CameraCheck("gym_owned_camera_centered");Check("gym_one_camera",!app.Views.Split&&Camera.allCamerasCount==1,"full-screen owned-player view retained");
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-gameplay.png"));yield return Wait(.35f);
        // Ordinary frames rendered by Unity; no extra manual render targets.
        app.enabled=false;var camera=app.Views.Primary;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldFov=camera.fieldOfView;
        foreach(var rb in app.Wrestlers[0].bodies)rb.isKinematic=true;foreach(var rb in app.Wrestlers[1].bodies)rb.isKinematic=true;
        // Compare the same lit mat from opposing downward gameplay views.
        camera.fieldOfView=58;camera.transform.position=new Vector3(-2.5f,4,-3.5f);camera.transform.LookAt(new Vector3(0,.2f,0));yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-mat-downward.png"));yield return Wait(.3f);
        camera.transform.position=new Vector3(2.5f,4,3.5f);camera.transform.LookAt(new Vector3(0,.2f,0));yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-mat-opposite.png"));yield return Wait(.3f);
        camera.fieldOfView=68;camera.transform.position=new Vector3(11.8f,5.6f,-9.2f);camera.transform.LookAt(new Vector3(0,1.5f,2));yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-wide.png"));yield return Wait(.3f);
        camera.fieldOfView=49;camera.transform.position=new Vector3(-2.5f,2,-6.0f);camera.transform.LookAt(new Vector3(-4.1f,.8f,-4.25f));yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-corner-detail.png"));yield return Wait(.3f);
        var roof=new System.Collections.Generic.List<Renderer>();foreach(var renderer in details.GetComponentsInChildren<Renderer>())if(renderer.enabled&&renderer.bounds.min.y>4.7f){renderer.enabled=false;roof.Add(renderer);}
        camera.orthographic=true;camera.orthographicSize=10.5f;camera.transform.position=new Vector3(0,22,0);camera.transform.rotation=Quaternion.Euler(90,0,0);yield return Wait(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gym-layout-cutaway.png"));yield return Wait(.3f);camera.orthographic=false;foreach(var renderer in roof)renderer.enabled=true;
        camera.transform.position=oldPosition;camera.transform.rotation=oldRotation;camera.fieldOfView=oldFov;app.enabled=true;
        foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)rb.isKinematic=false;
        var a=app.Wrestlers[0];Vector3 before=a.Root.position;app.ScriptedInputs[0]=new GBInput{x=1,ax=1};yield return Wait(.7f);Neutral();
        Check("gym_movement_remains_playable",Vector3.Distance(before,a.Root.position)>.5f,"distance="+Vector3.Distance(before,a.Root.position));
        Check("gym_performance",app.MeasuredFPS>=55,"native mean FPS="+app.MeasuredFPS+" minimum="+app.MinFPS+" GPU="+SystemInfo.graphicsDeviceName);
        Check("gym_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
