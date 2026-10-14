using UnityEngine;
using System.Collections.Generic;

// Scenery and skins only: the existing registered physical world is unchanged.
public static partial class GBGymVisual {
    static Material canvas,rubber,cloth,leather,ropeWhite,ropeOrange,paint,oak,metal,light,concrete;
    static readonly Dictionary<string,Mesh> rounded=new Dictionary<string,Mesh>();
    public static void Apply(GBArena arena){
        Materials();var root=new GameObject("Gym visual details");root.transform.SetParent(arena.transform,false);var p=root.transform;
        foreach(var rb in GBApp.I.Registry){
            if(rb.name=="ring mat")rb.GetComponent<Renderer>().sharedMaterial=cloth;
            if(rb.name=="ring post"){
                rb.GetComponent<Renderer>().enabled=false;
                var wrapper=new GameObject("Corner post visual");wrapper.transform.SetParent(rb.transform,false);Vector3 scale=rb.transform.localScale;wrapper.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                GBArt.Cylinder(wrapper.transform,"Steel corner upright",new Vector3(0,-.94f,0),new Vector3(0,.94f,0),.086f,metal);
                GBArt.Cylinder(wrapper.transform,"Post cap",new Vector3(0,.94f,0),new Vector3(0,.975f,0),.10f,paint);
            }
        }
        foreach(var rope in arena.ropes){
            rope.GetComponent<Renderer>().enabled=false;
        }
        root.AddComponent<GBRopeVisual>().Initialize(arena,ropeWhite,ropeOrange);
        foreach(var renderer in arena.GetComponentsInChildren<Renderer>()){
            if(renderer.name=="Corner pad"||renderer.name=="Wall acoustic panel"||renderer.name=="LEDGE DRILL"||renderer.name=="THE GYM")renderer.enabled=false;
            if(renderer.name=="Gym floor") {renderer.sharedMaterial=rubber;}
            if(renderer.name=="Ring base")renderer.sharedMaterial=leather;
            if(renderer.name=="Gym wall")renderer.sharedMaterial=paint;
            if(renderer.name=="practice ledge")renderer.enabled=false;
            if(renderer.name=="table top")renderer.sharedMaterial=oak;
            if(renderer.name=="table leg")renderer.sharedMaterial=metal;
        }
        GBArt.Shape(p,"Printed woven canvas",PrimitiveType.Plane,new Vector3(0,.203f,0),new Vector3(.88f,1,.88f),canvas);
        Round(p,"Training platform upholstery",new Vector3(7.1f,.65f,0),new Vector3(3.5f,1.4f,3.5f),.12f,leather);
        for(int panel=0;panel<4;panel++)Round(p,"Training platform cushion",new Vector3(7.1f,1.365f,(panel-1.5f)*.86f),new Vector3(3.45f,.065f,.85f),.025f,cloth);
        // Every detail is non-solid, with no GBTarget and no network registry entry.
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
            for(int h=0;h<3;h++){
                Vector3 at=new Vector3(x*4.32f,.62f+h*.38f,z*4.32f);
                var pad=Round(p,"Upholstered corner cushion",at,new Vector3(.45f,.255f,.24f),.055f,leather);pad.transform.localRotation=Quaternion.LookRotation(new Vector3(-x,0,-z));
                GBArt.Cylinder(p,"Turnbuckle tie",new Vector3(x*4.5f,at.y,z*4.5f),at,.035f,metal);
                var seam=Round(p,"Pad seam",at+new Vector3(-x,0,-z).normalized*.125f,new Vector3(.34f,.018f,.018f),.007f,ropeWhite);seam.transform.localRotation=pad.transform.localRotation;
            }
        }
        for(int side=0;side<4;side++){
            var apron=Round(p,"Stitched apron panel",new Vector3(side<2?0:side==2?-4.565f:4.565f,-.38f,side<2?side==0?-4.565f:4.565f:0),new Vector3(8.7f,.72f,.025f),.012f,leather);
            if(side>=2)apron.transform.localRotation=Quaternion.Euler(0,90,0);
            GBArt.Box(apron.transform,"Apron piping",new Vector3(0,.32f,-.015f),new Vector3(8.55f,.028f,.018f),ropeOrange,false);
        }
        GBArt.Text(p,"GRIP BREAK",new Vector3(0,-.39f,-4.585f),.28f,new Color(.88f,.88f,.81f),Quaternion.identity);
        Room(p);FinishRoom(p);FinishProps(arena);OrganizeStations(p);
        Lighting(arena,p);
    }
    static void Materials(){
        if(canvas)return;
        // A diffuse-only shader avoids every specular/reflection pass, rather
        // than relying on Standard's runtime keywords in the built player.
        canvas=new Material(Resources.Load<Shader>("Gym/MatteCanvas")){name="Gym printed woven canvas",color=Color.white};
        canvas.mainTexture=Resources.Load<Texture2D>("Gym/canvas_color");canvas.mainTextureScale=Vector2.one;
        canvas.SetTexture("_BumpMap",Resources.Load<Texture2D>("Gym/canvas_normal"));canvas.SetFloat("_BumpScale",.18f);
        canvas.SetTexture("_MetallicGlossMap",Resources.Load<Texture2D>("Gym/canvas_smooth"));
        cloth=PBR("Gym canvas sides","fabric_pattern_07",new Color(.43f,.49f,.52f),new Vector2(18,18),.20f);
        rubber=PBR("Gym speckled rubber tiles","rubber_tiles",Color.white,new Vector2(7,5),.7f);
        leather=PBR("Gym charcoal upholstery","leather_red_02",new Color(.19f,.20f,.21f),new Vector2(2,2),.65f);
        ropeWhite=GBArt.Mat("Gym ivory rope braid",new Color(.83f,.82f,.73f),0,.23f);ropeWhite.mainTexture=Grain("Rope weave",128,0);ropeWhite.mainTextureScale=new Vector2(4,12);
        ropeOrange=GBArt.Mat("Gym burnt orange rope braid",new Color(.8f,.25f,.08f),0,.24f);ropeOrange.mainTexture=ropeWhite.mainTexture;ropeOrange.mainTextureScale=ropeWhite.mainTextureScale;
        paint=PBR("Gym warm plaster wall","painted_plaster_wall",new Color(.92f,.91f,.86f),new Vector2(6,2),.4f);
        oak=PBR("Gym warm bench timber","wood_floor_worn",new Color(.90f,.78f,.62f),new Vector2(2,1),.5f);
        concrete=PBR("Gym concrete roof","concrete_floor_02",new Color(.7f,.72f,.73f),new Vector2(5,4),.35f);
        metal=GBArt.Mat("Gym brushed steel fittings",new Color(.22f,.25f,.27f),.55f,.46f);
        light=GBArt.Mat("Gym soft diffuser",new Color(.81f,.84f,.79f),0,.3f,true);
    }
    static Material PBR(string name,string asset,Color tint,Vector2 scale,float bump){
        var material=GBArt.Mat(name,tint,0,.3f);material.mainTexture=Resources.Load<Texture2D>("Gym/"+asset+"_color");material.mainTextureScale=scale;
        material.SetTexture("_BumpMap",Resources.Load<Texture2D>("Gym/"+asset+"_normal"));material.SetFloat("_BumpScale",bump);material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap",Resources.Load<Texture2D>("Gym/"+asset+"_smooth"));material.SetFloat("_GlossMapScale",.45f);material.EnableKeyword("_METALLICGLOSSMAP");return material;
    }
    static Texture2D Grain(string name,int size,int kind){
        var texture=new Texture2D(size,size,TextureFormat.RGB24,true);texture.name=name;texture.wrapMode=TextureWrapMode.Repeat;texture.anisoLevel=4;
        var pixels=new Color[size*size];var rng=new System.Random(431+kind);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float tone=.89f+(float)rng.NextDouble()*.12f;
            if(kind==0)tone+=((x%4<2)==(y%4<2)?.025f:-.025f);
            if(kind==1){if(x<2||y<2)tone*=.72f;if(rng.NextDouble()<.035)tone+=.18f;}
            if(kind==2)tone-=Mathf.Pow(Mathf.PerlinNoise(x*.15f,y*.15f),3)*.10f;
            if(kind==3)tone=.72f+Mathf.PerlinNoise(x*.18f,y*.014f)*.35f;
            pixels[y*size+x]=new Color(tone,tone,tone);
        }
        texture.SetPixels(pixels);texture.Apply(true,true);return texture;
    }
    static Texture2D Canvas(){
        const int size=1024;var texture=new Texture2D(size,size,TextureFormat.RGB24,true);texture.name="Printed wrestling canvas1024";texture.wrapMode=TextureWrapMode.Clamp;texture.anisoLevel=8;
        var pixels=new Color[size*size];var rng=new System.Random(1978);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float wx=((x+.5f)/size-.5f)*8.8f,wz=((y+.5f)/size-.5f)*8.8f,r=Mathf.Sqrt(wx*wx+wz*wz);
            float noise=((float)rng.NextDouble()-.5f)*.023f+Mathf.PerlinNoise(x*.027f,y*.027f)*.025f;
            Color c=new Color(.25f+noise,.34f+noise,.42f+noise);
            float edge=Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz));
            if(edge>4.04f)c=new Color(.13f+noise,.20f+noise,.26f+noise);
            if(Mathf.Abs(edge-4.02f)<.028f)c=new Color(.72f,.69f,.56f);
            if(Mathf.Abs(wx)+Mathf.Abs(wz)>7.7f)c=wx*wz>0?new Color(.57f,.24f,.13f):new Color(.15f,.38f,.48f);
            float ring=Mathf.Clamp01((.022f-Mathf.Abs(r-2.8f))*180);
            float center=Mathf.Clamp01((.014f-Mathf.Abs(r-.5f))*180);
            if(Mathf.Abs(wz)<.022f&&Mathf.Abs(wx)<.3f)center=1;
            c=Color.Lerp(c,new Color(.72f,.75f,.70f),Mathf.Max(ring,center)*.85f);
            c*=1+((x%4<2)==(y%4<2)?.014f:-.014f);pixels[y*size+x]=c;
        }
        texture.SetPixels(pixels);texture.Apply(true,true);return texture;
    }
    static GameObject Round(Transform p,string name,Vector3 at,Vector3 size,float radius,Material mat){
        string key=size.ToString("F4")+radius.ToString("F4");Mesh mesh;
        if(!rounded.TryGetValue(key,out mesh)){
            const int steps=12;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();Vector3 half=size*.5f,inner=half-Vector3.one*radius;
            foreach(Vector3 n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
                Vector3 u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.Cross(n,Vector3.up);Vector3 v=Vector3.Cross(n,u);int start=vertices.Count;
                for(int row=0;row<=steps;row++)for(int col=0;col<=steps;col++){
                    Vector3 q=Vector3.Scale(n+u*(col*2f/steps-1)+v*(row*2f/steps-1),half);
                    Vector3 a=new Vector3(Mathf.Clamp(q.x,-inner.x,inner.x),Mathf.Clamp(q.y,-inner.y,inner.y),Mathf.Clamp(q.z,-inner.z,inner.z)),normal=(q-a).normalized;
                    vertices.Add(a+normal*radius);normals.Add(normal);uv.Add(new Vector2((float)col/steps,(float)row/steps));
                }
                for(int row=0;row<steps;row++)for(int col=0;col<steps;col++){int a=start+row*(steps+1)+col,b=a+1,c=a+steps+1,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}
            }
            mesh=new Mesh{name="Rounded gym fitting"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();rounded[key]=mesh;
        }
        var go=new GameObject(name);go.transform.SetParent(p,false);go.transform.localPosition=at;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static void Room(Transform p){
        GBArt.Shape(p,"Extended gym floor",PrimitiveType.Plane,new Vector3(0,-1.049f,0),new Vector3(2.7f,1,2.6f),rubber);
        var window=GBArt.Mat("Gym daylight window glass",new Color(.70f,.78f,.83f),.05f,.82f,true);
        GBArt.Box(p,"Upper back wall",new Vector3(0,5.9f,10.01f),new Vector3(27,1.8f,.18f),paint,false);
        for(int side=-1;side<=1;side+=2){
            GBArt.Box(p,"Gym window sill wall",new Vector3(side*13.45f,.71f,0),new Vector3(.18f,3.52f,20),paint,false);
            GBArt.Box(p,"Gym upper window wall",new Vector3(side*13.45f,5.77f,0),new Vector3(.18f,2.52f,20),paint,false);
            foreach(float pier in new[]{-9.2f,-2.5f,2.5f,9.2f})GBArt.Box(p,"Window wall pier",new Vector3(side*13.45f,3.45f,pier),new Vector3(.18f,2.0f,2.8f),paint,false);
            for(int bay=-1;bay<=1;bay++){
                float z=bay*5;
                GBArt.Box(p,"Gym window pane",new Vector3(side*13.35f,3.4f,z),new Vector3(.035f,1.8f,3.2f),window,false);
                for(int frame=-1;frame<=1;frame+=2){GBArt.Box(p,"Window vertical frame",new Vector3(side*13.30f,3.4f,z+frame*1.61f),new Vector3(.065f,1.95f,.075f),metal,false);GBArt.Box(p,"Window horizontal frame",new Vector3(side*13.30f,3.4f+frame*.91f,z),new Vector3(.065f,.075f,3.3f),metal,false);}
                GBArt.Box(p,"Window center mullion",new Vector3(side*13.30f,3.4f,z),new Vector3(.07f,1.85f,.05f),metal,false);
            }
        }
        for(int z=-8;z<=8;z+=8){
            GBArt.Box(p,"Roof crossbeam",new Vector3(0,6.65f,z),new Vector3(27,.18f,.16f),metal,false);
            for(int side=-1;side<=1;side+=2)GBArt.Box(p,"Side wall column",new Vector3(side*13.25f,2.8f,z),new Vector3(.16f,7.65f,.18f),metal,false);
        }
        for(int seat=0;seat<6;seat++){
                float x=-10+seat*4,z=-8.6f;
                Round(p,"Timber ringside bench",new Vector3(x,-.43f,z),new Vector3(2.8f,.14f,.65f),.04f,oak);
                for(int leg=-1;leg<=1;leg+=2)GBArt.Box(p,"Bench steel legs",new Vector3(x+leg*1.12f,-.73f,z),new Vector3(.09f,.55f,.5f),metal,false);
        }
        for(int x=-12;x<=12;x+=4){
            GBArt.Box(p,"Wall pilaster",new Vector3(x,1.9f,9.82f),new Vector3(.12f,5.5f,.18f),metal,false);
            GBArt.Box(p,"Acoustic backing",new Vector3(x,1.05f,9.83f),new Vector3(2.9f,2,.08f),leather,false);
            for(int slat=0;slat<9;slat++)GBArt.Box(p,"Timber acoustic slat",new Vector3(x+(slat-4)*.31f,1.05f,9.74f),new Vector3(.08f,1.95f,.07f),oak,false);
        }
        for(int side=-1;side<=1;side+=2){
            var banner=Round(p,"Hanging club banner",new Vector3(side*7.8f,3.15f,9.60f),new Vector3(2.65f,1.35f,.045f),.018f,leather);
            GBArt.Text(banner.transform,"GRIP BREAK",new Vector3(0,.20f,-.032f),.25f,new Color(.87f,.83f,.70f),Quaternion.identity);
            GBArt.Text(banner.transform,"WRESTLING CLUB",new Vector3(0,-.23f,-.032f),.13f,new Color(.60f,.70f,.74f),Quaternion.identity);
            GBArt.Box(banner.transform,"Banner hem",new Vector3(0,-.60f,-.03f),new Vector3(2.3f,.035f,.01f),ropeOrange,false);
        }
        GBArt.Text(p,"GRIP BREAK / ATHLETICS",new Vector3(0,3.6f,9.65f),.40f,new Color(.81f,.85f,.80f),Quaternion.identity);
        for(int i=0;i<4;i++){
            float x=-11.7f+i*.70f;
            Round(p,"Steel locker",new Vector3(x,.18f,7.8f),new Vector3(.64f,2.5f,.65f),.025f,paint);
            for(int row=0;row<4;row++)GBArt.Box(p,"Locker vent",new Vector3(x,1.06f-row*.09f,7.466f),new Vector3(.35f,.027f,.012f),leather,false);
            GBArt.Box(p,"Locker handle",new Vector3(x+.20f,.17f,7.446f),new Vector3(.025f,.23f,.035f),metal,false);
        }
        for(int bag=0;bag<3;bag++){
            float x=7.8f+bag*1.8f;
            Lathe(p,"Hanging heavy bag",new Vector3(x,-.22f,7.1f),new[]{new Vector2(0,0),new Vector2(.26f,.025f),new Vector2(.30f,.09f),new Vector2(.305f,.4f),new Vector2(.30f,1.45f),new Vector2(.27f,1.62f),new Vector2(.12f,1.66f),new Vector2(0,1.67f)},leather);
            GBArt.Cylinder(p,"Heavy bag trim",new Vector3(x,.92f,7.1f),new Vector3(x,1.08f,7.1f),.304f,ropeOrange);
            for(int s=-1;s<=1;s+=2)GBArt.Cylinder(p,"Bag suspension",new Vector3(x+s*.21f,1.44f,7.1f),new Vector3(x,3.3f,7.1f),.014f,metal);
        }
        GBArt.Box(p,"Equipment rail",new Vector3(9.6f,3.34f,7.1f),new Vector3(5.1f,.13f,.13f),metal,false);
        for(int shelf=0;shelf<2;shelf++){
            float y=-.45f+shelf*.53f;Round(p,"Weight rack shelf",new Vector3(0,y,7.65f),new Vector3(4,.10f,.65f),.03f,metal);
            for(int i=0;i<6;i++){
                float x=-1.6f+i*.63f;GBArt.Cylinder(p,"Dumbbell handle",new Vector3(x-.19f,y+.18f,7.65f),new Vector3(x+.19f,y+.18f,7.65f),.025f,metal);
                for(int s=-1;s<=1;s+=2)GBArt.Cylinder(p,"Dumbbell plate",new Vector3(x+s*.18f,y+.18f,7.65f),new Vector3(x+s*.28f,y+.18f,7.65f),.13f,leather);
            }
        }
        for(int s=-1;s<=1;s+=2)GBArt.Box(p,"Weight rack upright",new Vector3(s*1.8f,-.37f,7.65f),new Vector3(.1f,1.4f,.1f),metal,false);
        for(int x=-3;x<=3;x+=6)for(int z=-3;z<=3;z+=6){
            Round(p,"Overhead light housing",new Vector3(x,5.3f,z),new Vector3(2.4f,.18f,.52f),.04f,metal);
            GBArt.Box(p,"Overhead light diffuser",new Vector3(x,5.2f,z),new Vector3(2.20f,.018f,.39f),light,false);
            for(int s=-1;s<=1;s+=2)GBArt.Cylinder(p,"Fixture suspension",new Vector3(x+s*.8f,5.35f,z),new Vector3(x+s*.8f,6.6f,z),.012f,metal);
        }
        // Visual stairs dress the existing base without adding new collision.
        for(int step=0;step<3;step++)Round(p,"Ringside access step",new Vector3(-3.3f,-.91f+step*.20f,-5.5f+step*.25f),new Vector3(1.0f,.18f,.72f),.035f,rubber);
    }
    static void Lighting(GBArena arena,Transform p){
        foreach(var l in arena.GetComponentsInChildren<Light>())l.intensity*=.3f;
        GBArt.Light(p,new Vector3(-2,5,0),new Color(1,.95f,.85f),1.6f,15);
        GBArt.Light(p,new Vector3(3,4,3),new Color(.86f,.92f,1),1.0f,15);
        RenderSettings.ambientSkyColor=new Color(.47f,.49f,.52f);RenderSettings.ambientEquatorColor=new Color(.30f,.31f,.32f);RenderSettings.ambientGroundColor=new Color(.18f,.17f,.16f);RenderSettings.fogDensity=.0015f;
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){
            if(l.name=="Evening sun"){l.color=new Color(1,.95f,.87f);l.intensity=.65f;l.transform.rotation=Quaternion.Euler(38,-68,0);l.shadowStrength=.78f;l.shadowBias=.018f;l.shadowNormalBias=.15f;l.shadowResolution=UnityEngine.Rendering.LightShadowResolution.VeryHigh;}
            else {l.color=new Color(.81f,.87f,.93f);l.intensity=.28f;}
        }
        var reflection=Resources.Load<Cubemap>("Gym/gym_01");if(reflection){RenderSettings.defaultReflectionMode=UnityEngine.Rendering.DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflection;RenderSettings.reflectionIntensity=.45f;}
        for(int side=-1;side<=1;side+=2)for(int bay=-1;bay<=1;bay++){
            var go=new GameObject("Daylight through window");go.transform.SetParent(p,false);go.transform.localPosition=new Vector3(side*12.9f,4.0f,bay*5);
            go.transform.LookAt(new Vector3(side*5,0,bay*4));var l=go.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=100;l.range=20;l.intensity=2.4f;l.color=new Color(.86f,.91f,1);l.shadows=LightShadows.Soft;l.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Medium;l.shadowBias=.025f;l.shadowNormalBias=.1f;
        }
        QualitySettings.shadowDistance=28;
    }
}
