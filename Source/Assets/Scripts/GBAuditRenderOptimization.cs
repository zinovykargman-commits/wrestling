using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

// Current-player geometry and draw-submission checks. This fixture neither
// reduces detail nor establishes whole-game FPS improvement.
public partial class GBAudit {
    [Serializable] class RenderOptimizationCounters {
        public string scope="Current native full-scene counters; no FPS improvement claim; capture renders excluded";
        public string graphicsApi,device;
        public bool instancingSupported,countersValid,instancedCounterAvailable;
        public int frames,registeredBodies,chainLinks,ductClamps,uniqueLoopMeshes,standardMaterials;
        public double meanInstancedBatches,meanBatches,meanDrawCalls,meanSetPassCalls;
        public long maximumInstancedBatches;
    }
    struct ExpectedLoop {
        public string name;public Vector3 position;public Quaternion rotation;
        public float radius,tube;
    }
    static List<ExpectedLoop> RenderExpectedLoops(){
        var result=new List<ExpectedLoop>();
        for(int x=-10;x<=10;x+=5)for(int z=-8;z<=8;z+=4)
            result.Add(new ExpectedLoop{name="Duct clamp",position=new Vector3(x,6.1f,z),radius=.177f,tube=.014f,rotation=Quaternion.identity});
        for(int bag=0;bag<3;bag++)for(int side=-1;side<=1;side+=2)for(int link=0;link<32;link++)
            result.Add(new ExpectedLoop{name="Heavy bag chain link",position=new Vector3(7.8f+bag*1.8f+side*.16f,1.48f+link*.057f,7.1f),radius=.038f,tube=.008f,rotation=Quaternion.Euler(0,link%2*90,0)});
        for(int side=-1;side<=1;side+=2)for(int plate=0;plate<3;plate++){
            var position=new Vector3(-9+side*(.83f+plate*.065f),.52f,5.5f-.22f);
            result.Add(new ExpectedLoop{name="Barbell rubber plate",position=position,radius=.185f,tube=.055f,rotation=Quaternion.Euler(0,90,0)});
            result.Add(new ExpectedLoop{name="Plate steel hub",position=position,radius=.029f,tube=.014f,rotation=Quaternion.Euler(0,90,0)});
        }
        return result;
    }
    static bool RenderLoopGeometry(Mesh mesh,float radius,float tube,out string evidence){
        const int segments=40,rings=8;
        if(!mesh||mesh.vertexCount!=369||mesh.subMeshCount!=1){evidence="missing mesh or incorrect vertex/submesh count";return false;}
        var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var indices=mesh.GetTriangles(0);
        int differences=0,index=0;
        if(normals.Length!=369||uv.Length!=369||indices.Length!=1920){evidence="attribute/triangle count differs";return false;}
        for(int a=0;a<=segments;a++)for(int b=0;b<=rings;b++){
            float t=a*2*Mathf.PI/segments,s=b*2*Mathf.PI/rings;
            Vector3 radial=new Vector3(Mathf.Cos(t),Mathf.Sin(t),0),normal=radial*Mathf.Cos(s)+Vector3.forward*Mathf.Sin(s);
            Vector3 vertex=radial*radius+normal*tube;Vector2 texcoord=new Vector2((float)a/segments,(float)b/rings);
            if(!vertices[index].Equals(vertex)||!normals[index].Equals(normal)||!uv[index].Equals(texcoord))differences++;
            index++;
        }
        index=0;
        for(int a=0;a<segments;a++)for(int b=0;b<rings;b++){
            int i=a*(rings+1)+b,j=i+rings+1;
            if(indices[index]!=i||indices[index+1]!=i+1||indices[index+2]!=j||indices[index+3]!=i+1||indices[index+4]!=j+1||indices[index+5]!=j)differences++;
            index+=6;
        }
        evidence="369 vertices;640 triangles;exact original vertex/normal/UV/index formula differences="+differences;
        return differences==0;
    }
    IEnumerator RenderOptimizationRun(){
        app.BeginMatch(3,1);Neutral();yield return Wait(3.4f);
        var details=app.Arena.transform.Find("Gym visual details");
        int bodies=app.Registry.Count,colliders=app.Arena.GetComponentsInChildren<Collider>().Length;
        var expected=RenderExpectedLoops();var found=new bool[expected.Count];
        var shared=new Dictionary<Vector2,Mesh>();var seenGeometry=new HashSet<Mesh>();
        bool identities=true,geometry=true,transforms=true,shadows=true,loopMaterials=true;int loops=0,chains=0,clamps=0;
        var geometryEvidence=new List<string>();
        foreach(var filter in details.GetComponentsInChildren<MeshFilter>()){
            if(filter.name!="Heavy bag chain link"&&filter.name!="Duct clamp"&&filter.name!="Barbell rubber plate"&&filter.name!="Plate steel hub")continue;
            loops++;if(filter.name=="Heavy bag chain link")chains++;if(filter.name=="Duct clamp")clamps++;
            Vector3 position=details.InverseTransformPoint(filter.transform.position);
            Quaternion rotation=Quaternion.Inverse(details.rotation)*filter.transform.rotation;
            int match=-1;
            for(int i=0;i<expected.Count;i++)if(!found[i]&&expected[i].name==filter.name&&(expected[i].position-position).sqrMagnitude<.0000000001f){match=i;break;}
            if(match<0){transforms=false;continue;}
            found[match]=true;var item=expected[match];
            transforms&=Mathf.Abs(Quaternion.Dot(rotation,item.rotation))>.999999f&&(filter.transform.lossyScale-Vector3.one).sqrMagnitude<.0000000001f;
            var renderer=filter.GetComponent<MeshRenderer>();
            shadows&=renderer&&renderer.enabled&&renderer.shadowCastingMode==ShadowCastingMode.On&&renderer.receiveShadows&&filter.GetComponents<Collider>().Length==0;
            var material=renderer?renderer.sharedMaterial:null;
            if(filter.name=="Barbell rubber plate")loopMaterials&=material&&material.shader.name=="Standard"&&material.name=="Gym charcoal upholstery"&&material.color.Equals(new Color(.19f,.20f,.21f))&&material.GetFloat("_Metallic")==0&&material.GetFloat("_Glossiness")==.3f&&material.GetFloat("_GlossMapScale")==.45f&&material.mainTexture==Resources.Load<Texture2D>("Gym/leather_red_02_color")&&material.GetTexture("_BumpMap")==Resources.Load<Texture2D>("Gym/leather_red_02_normal")&&material.GetTexture("_MetallicGlossMap")==Resources.Load<Texture2D>("Gym/leather_red_02_smooth");
            else loopMaterials&=material&&material.shader.name=="Standard"&&material.name=="Gym brushed steel fittings"&&material.color.Equals(new Color(.22f,.25f,.27f))&&material.GetFloat("_Metallic")==.55f&&material.GetFloat("_Glossiness")==.46f;
            var key=new Vector2(item.radius,item.tube);Mesh first;
            if(shared.TryGetValue(key,out first))identities&=first==filter.sharedMesh;else shared.Add(key,filter.sharedMesh);
            if(seenGeometry.Add(filter.sharedMesh)){
                string evidence;geometry&=RenderLoopGeometry(filter.sharedMesh,item.radius,item.tube,out evidence);geometryEvidence.Add(key+": "+evidence);
            }
        }
        foreach(bool present in found)transforms&=present;
        Check("render_loop_counts_preserved",loops==229&&chains==192&&clamps==25,"all229 original loops:192 chain links,25 clamps,6 rubber plates,6 steel hubs");
        Check("render_loop_mesh_identity_shared",identities&&shared.Count==4&&seenGeometry.Count==4,"same exact(radius,tube) geometry shares one mesh;unique meshes="+seenGeometry.Count+";no static batching");
        Check("render_loop_geometry_exact",geometry&&seenGeometry.Count==4,string.Join("; ",geometryEvidence));
        Check("render_loop_transforms_preserved",transforms&&loops==expected.Count,"all original positions/rotations/unit scales remain after station parenting;position tolerance10 micrometers");
        Check("render_loop_shadow_collision_preserved",shadows,"individual enabled MeshRenderers cast/receive shadows;no loop colliders added");
        Check("render_loop_material_properties_preserved",loopMaterials,"original brushed steel tint/metallic/smoothness and rubber plate upholstery tint/PBR texture maps retained");
        var materials=new HashSet<Material>();bool supported=true;int instancingEnabled=0;
        foreach(var renderer in app.World.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
            if(material&&material.shader&&material.shader.name=="Standard"){if(materials.Add(material)&&material.enableInstancing)instancingEnabled++;supported&=material.shader.isSupported;}
        Check("render_standard_material_shaders_supported",materials.Count>0&&supported,"unique shared Standard materials="+materials.Count+";instancing-enabled="+instancingEnabled+" (diagnostic only);custom wrestler/canvas/text shaders retained");
        bool physical=true;
        foreach(var w in app.Wrestlers)foreach(var body in w.bodies)physical&=body.solverIterations==64&&body.solverVelocityIterations==24;
        foreach(var rope in app.Arena.ropes)physical&=rope.GetComponent<BoxCollider>().enabled&&rope.GetComponent<ConfigurableJoint>();
        Check("render_physics_settings_preserved",physical&&bodies==92&&app.Arena.ropes.Count==48&&Time.fixedDeltaTime==1f/240,"92 registered bodies;48 original rope colliders/joints;240Hz;wrestlers64/24 iterations");
        var counters=new RenderOptimizationCounters{graphicsApi=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,instancingSupported=SystemInfo.supportsInstancing,registeredBodies=bodies,chainLinks=chains,ductClamps=clamps,uniqueLoopMeshes=seenGeometry.Count,standardMaterials=materials.Count};
        using(var instances=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Instanced Batches Count",1))
        using(var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
        using(var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
        using(var passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",1)){
            counters.countersValid=batches.Valid&&draws.Valid&&passes.Valid;counters.instancedCounterAvailable=instances.Valid;
            yield return null;
            for(int frame=0;frame<90;frame++){
                yield return null;
                counters.frames++;if(instances.Valid){counters.meanInstancedBatches+=instances.LastValue;counters.maximumInstancedBatches=Math.Max(counters.maximumInstancedBatches,instances.LastValue);}counters.meanBatches+=batches.LastValue;counters.meanDrawCalls+=draws.LastValue;counters.meanSetPassCalls+=passes.LastValue;
            }
        }
        counters.meanInstancedBatches/=counters.frames;counters.meanBatches/=counters.frames;counters.meanDrawCalls/=counters.frames;counters.meanSetPassCalls/=counters.frames;
        File.WriteAllText(Path.Combine(directory,"render-optimization-counters.json"),JsonUtility.ToJson(counters,true));
        Check("render_native_full_scene_counters_recorded",counters.countersValid&&counters.meanDrawCalls>0,"90 full-scene frames;instanced mean="+counters.meanInstancedBatches+" (diagnostic only);draws="+counters.meanDrawCalls+";batches="+counters.meanBatches+";SetPass="+counters.meanSetPassCalls+";neither positive instancing nor an FPS gain is assumed");
        bool enabled=app.enabled;app.enabled=false;
        var camera=app.Views.Primary;Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;float fov=camera.fieldOfView;
        camera.fieldOfView=68;camera.transform.position=new Vector3(0,3.8f,-8.7f);camera.transform.LookAt(new Vector3(0,1.8f,6));
        app.Capture(Path.Combine(directory,"render-gym-forward.png"),true);
        camera.transform.position=new Vector3(0,3.8f,8.7f);camera.transform.LookAt(new Vector3(0,1.4f,-6));
        app.Capture(Path.Combine(directory,"render-gym-back.png"),true);
        camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=fov;app.enabled=enabled;
        Check("render_registry_and_colliders_unchanged",app.Registry.Count==bodies&&app.Arena.GetComponentsInChildren<Collider>().Length==colliders,"read-only geometry/counter checks retain registered bodies/collider count;two actual full-detail views captured separately");
        Check("render_optimization_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
