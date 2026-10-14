using UnityEngine;
using System;
using System.Collections;
using System.IO;

// Render actual follow-camera views offscreen. Never requests window focus.
public static class GBAuditCameraObstruction {
    static void FlushPose(GBApp app){
        // Script-held Rigidbody poses must be reflected in *both* skins before
        // an immediate manual Camera.Render, including render-only shoulders.
        foreach(var w in app.Wrestlers)foreach(var body in w.bodies)body.transform.SetPositionAndRotation(body.position,body.rotation);
        Physics.SyncTransforms();
        foreach(var w in app.Wrestlers){
            w.Visuals();
            foreach(var visual in w.GetComponentsInChildren<GBRealisticWrestler>())visual.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
        }
    }
    public static IEnumerator Run(GBApp app,string directory,Action<string,bool,string> check){
        app.BeginMatch(3,1);app.Phase=GBApp.GamePhase.Fighting;app.Paused=false;
        yield return null;
        bool enabled=app.enabled;app.enabled=false;
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        var rig=app.Views;int bodies=app.Registry.Count,colliders=app.Arena.GetComponentsInChildren<Collider>().Length;
        var interpolation=new RigidbodyInterpolation[app.Wrestlers.Count][];
        for(int p=0;p<app.Wrestlers.Count;p++){
            var w=app.Wrestlers[p];interpolation[p]=new RigidbodyInterpolation[w.bodies.Length];
            for(int n=0;n<w.bodies.Length;n++){interpolation[p][n]=w.bodies[n].interpolation;w.bodies[n].interpolation=RigidbodyInterpolation.None;}
        }
        Vector3[] positions={new Vector3(0,.28f,0),new Vector3(0,2.3f,0),new Vector3(-3.7f,.28f,-3.7f),new Vector3(3.7f,2.3f,3.7f),new Vector3(-3.7f,2.3f,3.7f)};
        string[] names={"standing","raised-center","near-corner","raised-corner","opposite-corner"};
        foreach(var role in new[]{GBNetwork.Role.Offline,GBNetwork.Role.Client}){
            app.Network.role=role;int player=role==GBNetwork.Role.Client?1:0;
            for(int n=0;n<positions.Length;n++){
                var w=app.Wrestlers[player];w.ResetTo(positions[n]);
                Vector3 other=new Vector3(-2.5f,.28f,2.5f);
                if(Vector3.ProjectOnPlane(other-positions[n],Vector3.up).magnitude<2.2f)other=new Vector3(2.5f,.28f,-2.5f);
                app.Wrestlers[1-player].ResetTo(other);
                FlushPose(app);app.Epoch++;rig.Tick(0);yield return null;
                FlushPose(app);rig.Tick(0);
                Vector3 point=rig.FollowPoint(player),camera=rig.Primary.transform.position,viewport=rig.Primary.WorldToViewportPoint(point);
                string id=(player==0?"orange-":"blue-")+names[n];
                check("camera_clear_"+id,rig.StructuralViewClear(point,camera)&&Vector3.Distance(point,camera)>2,"camera="+camera+" target="+point+" structural bounds="+rig.StructuralBoundsCount+"; actual capture below");
                check("camera_locked_"+id,rig.PrimaryPlayer==player&&!rig.Split&&viewport.z>0&&Mathf.Abs(viewport.x-.5f)<.002f&&Mathf.Abs(viewport.y-.5f)<.002f,"single owned-player view; viewport="+viewport);
                FlushPose(app);app.Capture(Path.Combine(directory,"camera-"+id+".png"),true);yield return null;
            }
        }
        int cacheBuilds=rig.StructuralCacheBuilds;
        for(int n=0;n<120;n++)rig.Tick(0);
        check("camera_structure_cached_once_per_world",rig.StructuralCacheBuilds==cacheBuilds&&rig.StructuralBoundsCount>20,"cache builds="+cacheBuilds+";120 follow ticks do not scan renderers");
        check("camera_unchanged_gameplay_geometry",app.Registry.Count==bodies&&app.Arena.GetComponentsInChildren<Collider>().Length==colliders&&app.Arena.ropes.Count==48,"registered bodies="+bodies+";existing colliders="+colliders+";48 original rope bodies;camera creates no colliders");
        app.Network.role=GBNetwork.Role.Offline;
        for(int p=0;p<app.Wrestlers.Count;p++)for(int n=0;n<app.Wrestlers[p].bodies.Length;n++)app.Wrestlers[p].bodies[n].interpolation=interpolation[p][n];
        Physics.simulationMode=mode;app.enabled=enabled;
        check("camera_obstruction_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
