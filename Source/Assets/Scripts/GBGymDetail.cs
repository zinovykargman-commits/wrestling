using UnityEngine;
using System.Collections.Generic;

public static partial class GBGymVisual {
    static void FinishProps(GBArena arena){
        foreach(var rb in arena.GetComponentsInChildren<Rigidbody>()){
            if(rb.name!="table top"&&rb.name!="table leg"&&rb.name!="training block")continue;
            var renderer=rb.GetComponent<Renderer>();var collider=rb.GetComponent<BoxCollider>();if(!renderer||!collider)continue;
            renderer.enabled=false;bool block=rb.name=="training block";
            Round(rb.transform,block?"Timber plyometric box":"Rounded table component",collider.center,collider.size,rb.name=="table leg"?.02f:.025f,rb.name=="table leg"?metal:oak);
            if(block){
                for(int face=-1;face<=1;face+=2){Round(rb.transform,"Recessed plyobox handhold",new Vector3(0,.17f,face*.405f),new Vector3(.27f,.074f,.014f),.029f,leather);for(int fastener=-1;fastener<=1;fastener+=2)GBArt.Ball(rb.transform,"Plyobox screw",new Vector3(fastener*.31f,.30f,face*.408f),new Vector3(.014f,.014f,.006f),metal);}
            }
        }
    }
    static GameObject Lathe(Transform parent,string name,Vector3 at,Vector2[] profile,Material material){
        const int slices=64;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
        for(int row=0;row<profile.Length;row++)for(int s=0;s<=slices;s++){
            float angle=s*2*Mathf.PI/slices;v.Add(new Vector3(Mathf.Sin(angle)*profile[row].x,profile[row].y,Mathf.Cos(angle)*profile[row].x));uv.Add(new Vector2((float)s/slices,profile[row].y));
        }
        for(int row=0;row<profile.Length-1;row++)for(int s=0;s<slices;s++){int a=row*(slices+1)+s,b=a+slices+1;tri.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        var mesh=new Mesh{name=name+" curved shell"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static GameObject Loop(Transform p,string name,Vector3 at,float radius,float tube,Material m,Quaternion rotation){
        const int segments=40,rings=8;var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int a=0;a<=segments;a++)for(int b=0;b<=rings;b++){
            float t=a*2*Mathf.PI/segments,s=b*2*Mathf.PI/rings;Vector3 radial=new Vector3(Mathf.Cos(t),Mathf.Sin(t),0),n=radial*Mathf.Cos(s)+Vector3.forward*Mathf.Sin(s);
            verts.Add(radial*radius+n*tube);normals.Add(n);uv.Add(new Vector2((float)a/segments,(float)b/rings));
        }
        for(int a=0;a<segments;a++)for(int b=0;b<rings;b++){int i=a*(rings+1)+b,j=i+rings+1;tris.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
        var mesh=new Mesh{name=name+" ring"};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
        var go=new GameObject(name);go.transform.SetParent(p,false);go.transform.localPosition=at;go.transform.localRotation=rotation;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;return go;
    }
    static void FinishRoom(Transform p){
        Round(p,"Solid concrete ceiling",new Vector3(0,6.96f,0),new Vector3(27.2f,.20f,20.2f),.045f,concrete);
        GBArt.Box(p,"Gym entrance wall",new Vector3(0,3,-10.04f),new Vector3(27,8,.15f),paint,false);
        for(int side=-1;side<=1;side+=2){
            Round(p,"Painted wall skirting",new Vector3(side*13.29f,-.79f,0),new Vector3(.065f,.38f,20),.015f,leather);
            for(int bay=-1;bay<=1;bay++){
                Round(p,"Deep window sill",new Vector3(side*13.2f,2.45f,bay*5),new Vector3(.42f,.075f,3.32f),.025f,oak);
                for(int pane=-1;pane<=1;pane+=2)Round(p,"Window catch",new Vector3(side*13.24f,3.35f,bay*5+pane*.04f),new Vector3(.035f,.14f,.025f),.008f,metal);
            }
        }
        for(int z=-8;z<=8;z+=4){
            GBArt.Box(p,"I beam web",new Vector3(0,6.47f,z),new Vector3(26.9f,.43f,.075f),metal,false);
            for(int flange=-1;flange<=1;flange+=2)Round(p,"I beam flange",new Vector3(0,6.47f+flange*.22f,z),new Vector3(26.9f,.045f,.3f),.015f,metal);
            for(int x=-12;x<=12;x+=4)for(int flange=-1;flange<=1;flange+=2)GBArt.Ball(p,"Beam mounting bolt",new Vector3(x,6.2f,z+flange*.1f),new Vector3(.045f,.018f,.045f),metal);
        }
        for(int x=-10;x<=10;x+=5){
            GBArt.Cylinder(p,"Overhead ventilation duct",new Vector3(x,6.1f,-9),new Vector3(x,6.1f,9),.17f,metal);
            for(int z=-8;z<=8;z+=4)Loop(p,"Duct clamp",new Vector3(x,6.1f,z),.177f,.014f,metal,Quaternion.identity);
        }
        for(int i=0;i<4;i++){
            float x=-11.7f+i*.70f;
            Round(p,"Locker recessed door",new Vector3(x,.18f,7.455f),new Vector3(.59f,2.43f,.014f),.004f,metal);
            for(int hinge=-1;hinge<=1;hinge+=2)Round(p,"Locker hinge",new Vector3(x-.28f,.18f+hinge*.75f,7.43f),new Vector3(.028f,.08f,.025f),.009f,metal);
            GBArt.Text(p,(i+1).ToString("D2"),new Vector3(x,1.28f,7.42f),.11f,new Color(.75f,.75f,.70f),Quaternion.identity);
        }
        for(int seat=0;seat<6;seat++){
            float x=-10+seat*4,z=-8.6f;
            GBArt.Cylinder(p,"Bench cross brace",new Vector3(x-1.12f,-.94f,z-.15f),new Vector3(x+1.12f,-.53f,z-.15f),.025f,metal);
            for(int leg=-1;leg<=1;leg+=2){Round(p,"Bench rubber foot",new Vector3(x+leg*1.12f,-1.01f,z),new Vector3(.15f,.065f,.58f),.02f,leather);for(int edge=-1;edge<=1;edge+=2)GBArt.Ball(p,"Bench seat fastener",new Vector3(x+leg*1.12f,-.355f,z+edge*.21f),new Vector3(.024f,.007f,.024f),metal);}
            for(int slat=-1;slat<=1;slat++)GBArt.Box(p,"Bench timber seam",new Vector3(x,-.354f,z+slat*.17f),new Vector3(2.65f,.003f,.006f),leather,false);
        }
        for(int bag=0;bag<3;bag++){
            float x=7.8f+bag*1.8f;
            for(int side=-1;side<=1;side+=2){
                for(int link=0;link<32;link++)Loop(p,"Heavy bag chain link",new Vector3(x+side*.16f,1.48f+link*.057f,7.1f),.038f,.008f,metal,Quaternion.Euler(0,link%2*90,0));
                for(int stitch=0;stitch<24;stitch++)GBArt.Cylinder(p,"Heavy bag seam stitch",new Vector3(x+side*.11f,-.10f+stitch*.055f,6.815f),new Vector3(x+side*.11f,-.085f+stitch*.055f,6.815f),.0028f,ropeWhite);
            }
            Round(p,"Heavy bag label",new Vector3(x,.40f,6.792f),new Vector3(.21f,.30f,.016f),.025f,cloth);
            GBArt.Text(p,"GB",new Vector3(x,.40f,6.781f),.12f,new Color(.8f,.81f,.77f),Quaternion.identity);
        }
        for(int shelf=0;shelf<2;shelf++)for(int i=0;i<6;i++){
            float x=-1.6f+i*.63f,y=-.27f+shelf*.53f;
            for(int side=-1;side<=1;side+=2){var plate=Lathe(p,"Dumbbell machined end",new Vector3(x+side*.275f,y,7.65f),new[]{new Vector2(0,0),new Vector2(.12f,0),new Vector2(.137f,.014f),new Vector2(.137f,.035f),new Vector2(.12f,.048f),new Vector2(0,.048f)},leather);plate.transform.localRotation=Quaternion.Euler(0,0,-side*90);}
        }
        // Detailed power rack lives beyond the original playable ring.
        float rackX=-9,rackZ=5.5f;
        for(int side=-1;side<=1;side+=2){
            Round(p,"Power rack upright",new Vector3(rackX+side*.70f,.20f,rackZ),new Vector3(.095f,2.50f,.095f),.015f,metal);
            Round(p,"Power rack foot",new Vector3(rackX+side*.70f,-.98f,rackZ),new Vector3(.16f,.10f,1.6f),.02f,metal);
            for(int hole=0;hole<18;hole++)GBArt.Ball(p,"Rack adjustment bore",new Vector3(rackX+side*.70f,-.7f+hole*.105f,rackZ-.052f),new Vector3(.034f,.034f,.008f),leather);
            GBArt.Cylinder(p,"Barbell support",new Vector3(rackX+side*.70f,.42f,rackZ),new Vector3(rackX+side*.70f,.42f,rackZ-.26f),.025f,metal);
        }
        GBArt.Cylinder(p,"Barbell knurled shaft",new Vector3(rackX-1.15f,.52f,rackZ-.22f),new Vector3(rackX+1.15f,.52f,rackZ-.22f),.023f,metal);
        GBArt.Cylinder(p,"Rack pullup bar",new Vector3(rackX-.70f,1.40f,rackZ),new Vector3(rackX+.70f,1.40f,rackZ),.026f,metal);
        for(int side=-1;side<=1;side+=2)for(int plate=0;plate<3;plate++){
            float at=rackX+side*(.83f+plate*.065f);Loop(p,"Barbell rubber plate",new Vector3(at,.52f,rackZ-.22f),.185f,.055f,leather,Quaternion.Euler(0,90,0));Loop(p,"Plate steel hub",new Vector3(at,.52f,rackZ-.22f),.029f,.014f,metal,Quaternion.Euler(0,90,0));
        }
        for(int side=-1;side<=1;side+=2)for(int step=0;step<3;step++)GBArt.Cylinder(p,"Ring access step rail",new Vector3(-3.3f+side*.48f,-.83f+step*.20f,-5.75f+step*.25f),new Vector3(-3.3f+side*.48f,-.43f+step*.20f,-5.45f+step*.25f),.022f,metal);
    }
}
