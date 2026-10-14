using UnityEngine;

// A continuous render skin over the unchanged48 physical rope segments.
// Filter only the drawing: collisions, grips, joints and network state stay live.
public class GBRopeVisual:MonoBehaviour {
    GBArena arena;readonly Mesh[] skins=new Mesh[12];
    readonly Vector3[] filtered=new Vector3[48];readonly Vector3[] knots=new Vector3[6];
    readonly Vector3[] path=new Vector3[26],vertices=new Vector3[26*13],normals=new Vector3[26*13];
    public void Initialize(GBArena source,Material ivory,Material orange){
        arena=source;
        for(int i=0;i<48;i++)filtered[i]=arena.ropes[i].position;
        for(int group=0;group<12;group++){
            var go=new GameObject("Continuous braided rope");go.transform.SetParent(transform,false);
            var mesh=new Mesh{name="Rounded continuous rope"};mesh.MarkDynamic();skins[group]=mesh;
            var uv=new Vector2[26*13];var triangles=new int[25*12*6];int index=0;
            for(int ring=0;ring<26;ring++)for(int side=0;side<=12;side++)uv[ring*13+side]=new Vector2(ring*8.8f/25,side/12f);
            for(int ring=0;ring<25;ring++)for(int side=0;side<12;side++){
                int a=ring*13+side,b=a+1,c=a+13,d=c+1;
                triangles[index++]=a;triangles[index++]=b;triangles[index++]=c;
                triangles[index++]=b;triangles[index++]=d;triangles[index++]=c;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group/4==1?orange:ivory;
        }
    }
    void LateUpdate(){
        if(!arena||arena.ropes.Count!=48)return;
        float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
        for(int i=0;i<48;i++){
            Vector3 live=arena.ropes[i].position;
            filtered[i]=Vector3.Lerp(filtered[i],live,blend);
            filtered[i]=live+Vector3.ClampMagnitude(filtered[i]-live,.05f);
        }
        // This visual root stays unchanged throughout this LateUpdate. Sample
        // its affine point transform and scale-free direction rotation once,
        // instead of crossing into Transform for every rope vertex and normal.
        var worldToLocal=transform.worldToLocalMatrix;
        var inverseRotation=Quaternion.Inverse(transform.rotation);
        for(int group=0;group<12;group++){
            int side=group%4;float y=.62f+(group/4)*.38f;
            knots[0]=side<2?new Vector3(-4.4f,y,side==0?-4.4f:4.4f):new Vector3(side==2?-4.4f:4.4f,y,-4.4f);
            knots[5]=side<2?new Vector3(4.4f,y,knots[0].z):new Vector3(knots[0].x,y,4.4f);
            for(int i=0;i<4;i++)knots[i+1]=filtered[group*4+i];
            for(int point=0;point<26;point++){
                float t=point/5f;int segment=Mathf.Min(4,Mathf.FloorToInt(t));float u=t-segment;
                Vector3 a=knots[Mathf.Max(0,segment-1)],b=knots[segment],c=knots[segment+1],d=knots[Mathf.Min(5,segment+2)];
                path[point]=.5f*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
            }
            for(int ring=0;ring<26;ring++){
                Vector3 tangent=(path[Mathf.Min(25,ring+1)]-path[Mathf.Max(0,ring-1)]).normalized;
                Vector3 across=Vector3.Cross(tangent,Vector3.up).normalized,vertical=Vector3.Cross(tangent,across).normalized;
                for(int sideIndex=0;sideIndex<=12;sideIndex++){
                    float angle=sideIndex*Mathf.PI/6;Vector3 normal=across*Mathf.Cos(angle)+vertical*Mathf.Sin(angle);
                    int v=ring*13+sideIndex;vertices[v]=worldToLocal.MultiplyPoint3x4(path[ring]+normal*.035f);normals[v]=inverseRotation*normal;
                }
            }
            skins[group].vertices=vertices;skins[group].normals=normals;skins[group].RecalculateBounds();
        }
    }
    void OnDestroy(){foreach(var mesh in skins)if(mesh)Destroy(mesh);}
}
