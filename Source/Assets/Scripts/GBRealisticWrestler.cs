using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Visual skinning reads the same interpolated physical regions on host/client.
// It adds no colliders, bodies, animations, forces or network state.
public sealed class GBRealisticWrestler:MonoBehaviour {
    [Serializable] public class Surface {public string name,main,normal;public float[] color;public float roughness;public bool cutout;}
    [Serializable] public class Part {public string name,file;public int[] materials;public int vertices,triangles;}
    [Serializable] public class Asset {public string name;public Surface[] materials;public Part[] parts;public int boneCount,physicalBoneCount;}
    static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
    static readonly Dictionary<string,Material[]> surfaces=new Dictionary<string,Material[]>();
    public SkinnedMeshRenderer[] Skins {get;private set;}
    public int VertexCount {get;private set;}
    GBWrestler owner;Transform[] shoulderBlend;
    readonly Vector3[] shoulderAnchors=new Vector3[2];
    public GBHandContactVisual HandContact {get;private set;}
    void LateUpdate(){UpdateShoulders();if(HandContact)HandContact.RenderFrame(Time.deltaTime);}
    void UpdateShoulders(){
        if(owner==null||shoulderBlend==null)return;
        for(int hand=0;hand<2;hand++){
            int upper=hand==0?3:6;
            // Clients intentionally remove local physics joints. Keep the
            // authored chest anchor captured before that platform transition.
            shoulderBlend[hand].position=owner.bodies[1].transform.TransformPoint(shoulderAnchors[hand]);
            // Two render-only half-rotation pivots preserve shoulder volume.
            // They read the same interpolated physics on host and client.
            shoulderBlend[hand].rotation=Quaternion.Slerp(owner.bodies[1].transform.rotation,owner.bodies[upper].transform.rotation,.5f);
        }
    }
    public static GBRealisticWrestler Attach(GBWrestler wrestler){
        string key=wrestler.player==0?"ember":"cobalt";
        var text=Resources.Load<TextAsset>("Wrestlers/"+key);
        if(!text)throw new InvalidOperationException("Missing licensed wrestler skin: "+key);
        var asset=JsonUtility.FromJson<Asset>(text.text);
        if(asset.boneCount!=17||asset.physicalBoneCount!=15)throw new InvalidOperationException("Wrestler skin requires15 physical regions and2 render-only shoulder pivots");
        // Retain all physics/targets and independent reach/highlight lines.
        foreach(var old in wrestler.GetComponentsInChildren<MeshRenderer>())old.enabled=false;
        var root=new GameObject(asset.name+" anatomical skin");root.transform.SetParent(wrestler.transform,false);
        root.transform.position=wrestler.Root.position-wrestler.initial[0];
        var visual=root.AddComponent<GBRealisticWrestler>();visual.owner=wrestler;
        visual.shoulderBlend=new Transform[2];
        for(int hand=0;hand<2;hand++){var blend=new GameObject(hand==0?"Left render-only shoulder":"Right render-only shoulder");blend.transform.SetParent(root.transform,false);visual.shoulderBlend[hand]=blend.transform;visual.shoulderAnchors[hand]=wrestler.joints[hand==0?3:6].connectedAnchor;}
        visual.UpdateShoulders();
        Material[] materials;
        if(!surfaces.TryGetValue(key,out materials)){
            materials=new Material[asset.materials.Length];
            for(int i=0;i<materials.Length;i++)materials[i]=MakeMaterial(asset.materials[i]);
            surfaces[key]=materials;
        }
        var bones=new Transform[17];var bind=new Matrix4x4[17];
        for(int i=0;i<15;i++){bones[i]=wrestler.bodies[i].transform;bind[i]=bones[i].worldToLocalMatrix*root.transform.localToWorldMatrix;}
        for(int hand=0;hand<2;hand++){bones[15+hand]=visual.shoulderBlend[hand];bind[15+hand]=bones[15+hand].worldToLocalMatrix*root.transform.localToWorldMatrix;}
        visual.Skins=new SkinnedMeshRenderer[asset.parts.Length];
        for(int i=0;i<asset.parts.Length;i++){
            var part=asset.parts[i];Mesh mesh;
            if(!meshes.TryGetValue(part.file,out mesh)){mesh=ReadMesh(part.file);mesh.bindposes=bind;meshes[part.file]=mesh;}
            var child=new GameObject(part.name);child.transform.SetParent(root.transform,false);
            var skin=child.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];
            skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=true;
            skin.localBounds=new Bounds(new Vector3(0,1,0),Vector3.one*6);
            skin.shadowCastingMode=ShadowCastingMode.On;skin.receiveShadows=true;
            var mats=new Material[part.materials.Length];for(int m=0;m<mats.Length;m++)mats[m]=materials[part.materials[m]];
            skin.sharedMaterials=mats;visual.Skins[i]=skin;visual.VertexCount+=mesh.vertexCount;
        }
        visual.HandContact=root.AddComponent<GBHandContactVisual>();
        visual.HandContact.Initialize(wrestler,bones,visual.Skins);
        return visual;
    }
    static Material MakeMaterial(Surface surface){
        var shader=Resources.Load<Shader>("Wrestlers/"+(surface.cutout?"WrestlerCutout":"WrestlerOpaque"));
        if(!shader)throw new InvalidOperationException("Missing wrestler surface shader");
        var m=new Material(shader){name=surface.name};
        m.color=new Color(surface.color[0],surface.color[1],surface.color[2],1);
        m.SetFloat("_Glossiness",Mathf.Min(.18f,1-Mathf.Sqrt(surface.roughness)));
        if(surface.name.Contains("fabric")||surface.name.Contains("singlet")){
            m.SetFloat("_Glossiness",.04f);
        }
        if(!string.IsNullOrEmpty(surface.main))m.mainTexture=Resources.Load<Texture2D>("Wrestlers/"+surface.main);
        if(!string.IsNullOrEmpty(surface.normal)){m.SetTexture("_BumpMap",Resources.Load<Texture2D>("Wrestlers/"+surface.normal));m.SetFloat("_BumpScale",.18f);}
        if(surface.name.Contains("eyes"))m.SetFloat("_Glossiness",.08f);
        if(surface.cutout){m.SetFloat("_Cutoff",surface.name.Contains("hair")?.08f:.30f);m.renderQueue=2450;}
        return m;
    }
    static Mesh ReadMesh(string key){
        var data=Resources.Load<TextAsset>("Wrestlers/"+key);
        if(!data)throw new InvalidOperationException("Missing wrestler mesh: "+key);
        using(var reader=new BinaryReader(new MemoryStream(data.bytes))){
            int count=reader.ReadInt32(),sub=reader.ReadInt32();
            if(count<1||count>250000||sub<1||sub>8)throw new InvalidDataException("Invalid wrestler mesh header");
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];
            for(int i=0;i<count;i++){
                vertices[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                var w=new BoneWeight{boneIndex0=reader.ReadByte(),boneIndex1=reader.ReadByte(),boneIndex2=reader.ReadByte(),boneIndex3=reader.ReadByte(),weight0=reader.ReadSingle(),weight1=reader.ReadSingle(),weight2=reader.ReadSingle(),weight3=reader.ReadSingle()};weights[i]=w;
            }
            var mesh=new Mesh{name=key,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.boneWeights=weights;mesh.subMeshCount=sub;
            for(int s=0;s<sub;s++){int length=reader.ReadInt32();var triangles=new int[length];for(int t=0;t<length;t++)triangles[t]=reader.ReadInt32();mesh.SetTriangles(triangles,s);}
            if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing wrestler mesh bytes");
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
