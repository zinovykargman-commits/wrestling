using UnityEngine;
using System.Collections.Generic;

// Rounded clothing follows the torso and pelvis rigid bodies without adding
// separate physical pieces or changing the anatomical collision skeleton.
public static class GBBodyVisual {
    public static GameObject Body(Transform parent,bool torso,Material material){
        float[] y=torso?new[]{-.27f,-.235f,-.16f,-.04f,.10f,.19f,.245f,.27f}:new[]{-.20f,-.165f,-.08f,.04f,.135f,.175f,.19f};
        float[] x=torso?new[]{.09f,.165f,.185f,.22f,.25f,.255f,.21f,.105f}:new[]{.105f,.185f,.225f,.23f,.205f,.145f,.08f};
        float[] z=torso?new[]{.08f,.18f,.23f,.26f,.275f,.275f,.20f,.105f}:new[]{.095f,.15f,.175f,.18f,.165f,.12f,.07f};
        return Loft(parent,torso?"Rounded chest and waist":"Rounded wrestling shorts",y,x,z,material);
    }
    public static GameObject Waistband(Transform parent,Material material){return Loft(parent,"Soft waistband",new[]{.012f,.02f,.064f,.071f},new[]{.227f,.233f,.229f,.222f},new[]{.177f,.184f,.181f,.174f},material);}
    static GameObject Loft(Transform parent,string name,float[] y,float[] x,float[] z,Material material){
        const int sides=32;var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int ring=0;ring<y.Length;ring++)for(int side=0;side<sides;side++){float angle=side*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(angle)*x[ring],y[ring],Mathf.Sin(angle)*z[ring]));}
        for(int ring=0;ring<y.Length-1;ring++)for(int side=0;side<sides;side++){int a=ring*sides+side,b=ring*sides+(side+1)%sides,c=a+sides,d=b+sides;triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
        int bottom=vertices.Count;vertices.Add(new Vector3(0,y[0],0));int top=vertices.Count;vertices.Add(new Vector3(0,y[y.Length-1],0));
        for(int side=0;side<sides;side++){triangles.Add(bottom);triangles.Add(side);triangles.Add((side+1)%sides);int start=(y.Length-1)*sides;triangles.Add(top);triangles.Add(start+(side+1)%sides);triangles.Add(start+side);}
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<GBBodyMeshCleanup>();return go;
    }
}

public class GBBodyMeshCleanup:MonoBehaviour {void OnDestroy(){var filter=GetComponent<MeshFilter>();if(filter&&filter.sharedMesh)Destroy(filter.sharedMesh);}}
