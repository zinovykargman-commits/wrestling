using UnityEngine;
using System.Collections.Generic;

public class GBArena:MonoBehaviour {
    public int kind;
    public string arenaName;
    public List<GBBreakable> breakables=new List<GBBreakable>();
    public List<GBBreakable> towerSections=new List<GBBreakable>();
    public List<Transform> scenery=new List<Transform>();
    public List<Rigidbody> sweepers=new List<Rigidbody>();
    public List<Transform> ropes=new List<Transform>();
    public Vector3[] spawns=new Vector3[2];
    public float elapsed,suddenTime;
    public bool sudden;
    int collapsed,warned=-1;
    Transform[] boundary=new Transform[4];
    public int WarningSection {get{return warned;}}
    Material steel,black,wood,cream,orange,blue,glass,concrete;
    public void Build(int arena) {
        kind=arena;arenaName=new[]{"FREIGHT / 04","AFTER HOURS","LAST FLOOR","THE GYM"}[kind];
        steel=GBArt.Mat("Brushed steel",new Color(.24f,.3f,.36f),.72f,.4f);black=GBArt.Mat("Dark iron",new Color(.06f,.08f,.105f),.45f,.25f);wood=GBArt.Mat("Warm plywood",new Color(.5f,.29f,.13f),0,.22f);cream=GBArt.Mat("Ivory",new Color(.79f,.74f,.61f),0,.3f);orange=GBArt.Mat("Hazard orange",new Color(1,.33f,.075f),.15f,.4f);blue=GBArt.Mat("Blue enamel",new Color(.085f,.27f,.4f),.25f,.4f);glass=GBArt.Mat("Frosted glass",new Color(.35f,.65f,.7f),.65f,.86f);concrete=GBArt.Mat("Concrete",new Color(.35f,.36f,.37f),0,.12f);
        Random.InitState(1978+kind);
        if(kind==0)Train();else if(kind==1)Store();else if(kind==2)Tower();else Gym();
        if(kind==1||kind==2)for(int i=0;i<4;i++){boundary[i]=Box("Closing arena warning",Vector3.zero,Vector3.one,GBArt.Mat("Closing warning",new Color(1,.12f,.015f),0,.2f,true),false).transform;boundary[i].gameObject.SetActive(false);}
        var root=new GameObject("Arena accent lights");root.transform.SetParent(transform,false);
        GBArt.Light(root.transform,new Vector3(-6,4,-4),new Color(.12f,.65f,1),kind==3?.55f:5,17);GBArt.Light(root.transform,new Vector3(6,4,5),new Color(1,.31f,.1f),kind==3?.45f:5,17);
    }
    GameObject Box(string name,Vector3 at,Vector3 size,Material mat,bool collider=true) {return GBArt.Box(transform,name,at,size,mat,collider);}
    public Rigidbody Surface(string name,Vector3 at,Vector3 size,Material mat,bool grab=true,string sound="metal") {
        var g=Box(name,at,size,mat);var r=g.AddComponent<Rigidbody>();r.isKinematic=true;
        if(grab){var t=g.AddComponent<GBTarget>();t.region=name;t.sound=sound;}
        var c=g.AddComponent<GBContact>();c.sound=sound;GBApp.I.Register(r);return r;
    }
    Rigidbody Piece(Transform parent,string name,Vector3 at,Vector3 size,Material mat,float mass,bool kinematic,GBBreakable prop=null,string sound="wood") {
        var g=GBArt.Box(parent,name,at,size,mat,true);var mesh=g.GetComponent<MeshFilter>().mesh;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],size);mesh.vertices=vertices;mesh.RecalculateBounds();g.transform.localScale=Vector3.one;g.GetComponent<BoxCollider>().size=size;
        var r=g.AddComponent<Rigidbody>();r.mass=mass;r.isKinematic=kinematic;r.linearDamping=.12f;r.angularDamping=.15f;r.interpolation=RigidbodyInterpolation.Interpolate;r.collisionDetectionMode=kinematic?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.ContinuousDynamic;r.maxDepenetrationVelocity=3;
        var target=g.AddComponent<GBTarget>();target.region=name;target.prop=prop;target.sound=sound;var c=g.AddComponent<GBContact>();c.prop=prop;c.sound=sound;
        if(prop)prop.parts.Add(r);GBApp.I.Register(r);return r;
    }
    GBBreakable Group(string name,Vector3 at,float health,string type="wood") {var g=new GameObject(name);g.transform.SetParent(transform,false);g.transform.localPosition=at;var p=g.AddComponent<GBBreakable>();p.health=health;p.type=type;breakables.Add(p);return p;}
    void Rail(Vector3 a,Vector3 b,bool breakable=true) {
        Vector3 center=(a+b)*.5f;float length=Vector3.Distance(a,b);bool alongX=Mathf.Abs(a.x-b.x)>Mathf.Abs(a.z-b.z);
        if(breakable){var p=Group("Breakaway railing",center,90,"metal");Piece(p.transform,"railing",Vector3.up*.9f,alongX?new Vector3(length,.12f,.11f):new Vector3(.11f,.12f,length),orange,9,true,p,"metal");Piece(p.transform,"rail post",a-center+Vector3.up*.45f,new Vector3(.1f,.9f,.1f),steel,4,true,p,"metal");Piece(p.transform,"rail post",b-center+Vector3.up*.45f,new Vector3(.1f,.9f,.1f),steel,4,true,p,"metal");}
        else {Surface("railing",center+Vector3.up*.9f,alongX?new Vector3(length,.12f,.11f):new Vector3(.11f,.12f,length),orange,true);Box("Fixed post",a+Vector3.up*.45f,new Vector3(.1f,.9f,.1f),steel);Box("Fixed post",b+Vector3.up*.45f,new Vector3(.1f,.9f,.1f),steel);}
    }
    void HazardStripe(Vector3 at,Vector3 size,int axis=0) {
        int count=Mathf.Max(1,Mathf.RoundToInt((axis==0?size.x:size.z)/.35f));for(int i=0;i<count;i++){var pos=at;var s=size;if(axis==0){s.x=size.x/count;pos.x+=(-size.x*.5f+(i+.5f)*s.x);}else{s.z=size.z/count;pos.z+=(-size.z*.5f+(i+.5f)*s.z);}Box("Caution stripe",pos,s,i%2==0?orange:black,false);}
    }
    void Table(Vector3 at,Vector3 size,bool breakable=true) {
        var p=Group("Breakable table",at,breakable?110:100000);
        for(int i=0;i<3;i++)Piece(p.transform,"table top",new Vector3((i-1)*size.x/3,size.y,0),new Vector3(size.x/3,.14f,size.z),wood,7,true,p);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Piece(p.transform,"table leg",new Vector3(x*size.x*.4f,size.y*.5f,z*size.z*.35f),new Vector3(.11f,size.y,.11f),black,3,true,p,"metal");
    }
    void Chair(Vector3 at) {
        var rb=Piece(transform,"chair",at,new Vector3(.6f,.12f,.6f),blue,7,false,null,"wood");
        GBArt.Box(rb.transform,"Chair back",new Vector3(0,.4f,-.27f),new Vector3(.6f,.72f,.1f),blue,false);
        var c=rb.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,.4f,-.27f);c.size=new Vector3(.6f,.72f,.1f);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){GBArt.Box(rb.transform,"Chair leg",new Vector3(x*.23f,-.27f,z*.23f),new Vector3(.055f,.55f,.055f),black,false);var foot=rb.gameObject.AddComponent<BoxCollider>();foot.center=new Vector3(x*.23f,-.27f,z*.23f);foot.size=new Vector3(.055f,.55f,.055f);}
    }
    void Train() {
        spawns[0]=new Vector3(-.85f,.13f,-3.5f);spawns[1]=new Vector3(.85f,.13f,-3.5f);
        for(int car=-1;car<=1;car+=2) {
            float z=car*4.45f;Surface("freight deck",new Vector3(0,-.18f,z),new Vector3(5.6f,.36f,8.4f),steel,true);
            Box("Chassis",new Vector3(0,-.8f,z),new Vector3(4.6f,.8f,7.8f),black,false);
            HazardStripe(new Vector3(0,.014f,z-4.15f),new Vector3(5.5f,.023f,.22f));HazardStripe(new Vector3(0,.014f,z+4.15f),new Vector3(5.5f,.023f,.22f));
            for(int side=-1;side<=1;side+=2){Rail(new Vector3(side*2.75f,0,z-3.9f),new Vector3(side*2.75f,0,z-.8f));Rail(new Vector3(side*2.75f,0,z+.8f),new Vector3(side*2.75f,0,z+3.9f));}
            for(int x=-1;x<=1;x+=2)for(int p=-1;p<=1;p+=2){var w=GBArt.Cylinder(transform,"Train wheel",new Vector3(x*2.15f,-1.05f,z+p*2.8f),new Vector3(x*2.55f,-1.05f,z+p*2.8f),.5f,black);}
            for(int k=0;k<3;k++)for(int s=-1;s<=1;s+=2)Box("Deck seams",new Vector3(s*1.2f,.005f,z+(k-1)*2.6f),new Vector3(.02f,.018f,2.4f),black,false);
        }
        Surface("coupling bridge",new Vector3(0,-.02f,0),new Vector3(1.3f,.2f,.7f),black,true);
        Piece(transform,"cargo crate",new Vector3(-1.7f,.4f,4),new Vector3(.8f,.8f,.8f),wood,16,false);
        Piece(transform,"cargo crate",new Vector3(1.6f,.45f,5.1f),new Vector3(.9f,.9f,.9f),wood,20,false);
        Surface("cargo container",new Vector3(0,.95f,7),new Vector3(2.2f,1.9f,2.1f),blue,true);GBArt.Text(transform,"04 / FREIGHT",new Vector3(0,1.1f,5.91f),.2f,Color.white,Quaternion.identity);
        for(int i=0;i<2;i++){var beam=Surface("low overhead beam",new Vector3(0,1.58f,30+i*20),new Vector3(6.8f,.27f,.4f),orange,false);sweepers.Add(beam);}
        Box("Track bed",new Vector3(0,-2.1f,0),new Vector3(7,.2f,150),concrete,false);
        for(int s=-1;s<=1;s+=2)Box("Track",new Vector3(s*1.9f,-1.95f,0),new Vector3(.13f,.1f,150),steel,false);
        for(int i=0;i<40;i++){var t=Box("Passing sleeper",new Vector3(0,-2,i*3-60),new Vector3(5,.11f,.28f),wood,false).transform;scenery.Add(t);}
        for(int i=0;i<18;i++){var t=Box("Passing warehouses",new Vector3((i%2==0?-1:1)*(14+i%4*5),-1,i*10-70),new Vector3(7,8+i%3*4,6),i%2==0?black:concrete,false).transform;scenery.Add(t);}
        GBArt.Text(transform,"KEEP LOW",new Vector3(0,.025f,-6.2f),.3f,Color.white,Quaternion.Euler(90,0,0));
    }
    void Store() {
        spawns[0]=new Vector3(-1.1f,.12f,-3.2f);spawns[1]=new Vector3(1.1f,.12f,-3.2f);
        Surface("showroom floor",new Vector3(-1.4f,-.18f,0),new Vector3(15.2f,.36f,12),GBArt.Mat("Showroom terrazzo",new Color(.29f,.32f,.35f),.08f,.65f),true,"cloth");
        Surface("loading walkway",new Vector3(7.6f,-.18f,-1.75f),new Vector3(2.8f,.36f,8.5f),concrete,true);
        Box("Back wall",new Vector3(0,2.3f,6.1f),new Vector3(18,.25f+4.4f,.22f),black);Box("Side wall",new Vector3(-9.1f,2,0),new Vector3(.2f,4.2f,12),black);
        GBArt.Text(transform,"AFTER HOURS",new Vector3(-2.5f,3.1f,5.94f),.42f,new Color(1,.55f,.19f),Quaternion.identity);
        GBArt.Text(transform,"HOME / FORM",new Vector3(-2.5f,2.4f,5.92f),.19f,Color.white,Quaternion.identity);
        for(int x=-8;x<=8;x+=2)for(int z=-5;z<=5;z+=2)Box("Floor grout",new Vector3(x,.012f,z),new Vector3(1.95f,.012f,.025f),black,false);
        var couch=Surface("sofa",new Vector3(-5.1f,.4f,1.9f),new Vector3(3.4f,.8f,1.3f),blue,true,"cloth");
        Surface("sofa back",new Vector3(-5.1f,.97f,2.4f),new Vector3(3.4f,.6f,.3f),blue,true,"cloth");
        for(int s=-1;s<=1;s+=2)Surface("sofa arm",new Vector3(-5.1f+s*1.55f,.7f,1.9f),new Vector3(.3f,.5f,1.3f),blue,true,"cloth");
        for(int i=0;i<3;i++)Box("Cushion",new Vector3(-5.1f+(i-1)*1.04f,.84f,1.7f),new Vector3(.98f,.14f,.92f),cream,false);
        Table(new Vector3(-4.7f,0,-.2f),new Vector3(2.5f,.72f,1.2f));Table(new Vector3(2.7f,0,.2f),new Vector3(2.2f,.85f,1.5f));
        Chair(new Vector3(2.6f,.62f,-1.2f));Chair(new Vector3(4.2f,.62f,.4f));Chair(new Vector3(-6.7f,.62f,-2.6f));
        Surface("checkout counter",new Vector3(3,.63f,4.4f),new Vector3(3.9f,1.26f,1),cream,true,"wood");Box("Counter top",new Vector3(3,1.32f,4.4f),new Vector3(4.1f,.12f,1.2f),black,false);Box("Register",new Vector3(4,1.5f,4.4f),new Vector3(.6f,.34f,.4f),steel,false);
        for(int s=0;s<2;s++){var shelf=Piece(transform,"display shelf",new Vector3(-7+s*6,1.15f,4.9f),new Vector3(2.3f,2.3f,.4f),wood,32,false);for(int k=0;k<3;k++)GBArt.Box(shelf.transform,"Shelf lip",new Vector3(0,(k-1)*.72f,-.28f),new Vector3(2.35f,.09f,.65f),cream,false);}
        for(int i=0;i<6;i++)Piece(transform,"display box",new Vector3(-6+i*.6f,.25f,-4.6f),new Vector3(.42f,.5f,.42f),i%2==0?orange:cream,3,false);
        for(int x=-8;x<=8;x+=4){var panel=Group("Breakable storefront",new Vector3(x,0,-6),75,"metal");Piece(panel.transform,"glass barrier",new Vector3(0,.6f,0),new Vector3(3.5f,1.2f,.09f),glass,8,true,panel,"metal");Box("Window mullion",new Vector3(x+1.9f,1.4f,-6.1f),new Vector3(.08f,2.8f,.08f),steel,false);}
        HazardStripe(new Vector3(6.2f,.025f,4.2f),new Vector3(.3f,.03f,3.5f),1);HazardStripe(new Vector3(7.6f,.025f,2.52f),new Vector3(2.8f,.03f,.3f));
        Box("Loading pit interior",new Vector3(7.7f,-2,4.3f),new Vector3(3,.2f,3.5f),black,false);GBArt.Text(transform,"LOADING PIT",new Vector3(7.4f,.04f,1.7f),.18f,new Color(1,.55f,.15f),Quaternion.Euler(90,0,0));
        for(int i=0;i<5;i++){Box("Ceiling light",new Vector3(-6+i*3,4.4f,1),new Vector3(2,.07f,.3f),GBArt.Mat("Strip light",new Color(.75f,.88f,1),0,.2f,true),false);}
        Box("Outside sidewalk",new Vector3(0,-1,-11),new Vector3(30,.2f,8),concrete,false);
    }
    void Tower() {
        spawns[0]=new Vector3(-.85f,.13f,0);spawns[1]=new Vector3(.85f,.13f,0);
        Surface("central rooftop",new Vector3(0,-.22f,0),new Vector3(4,.44f,4),concrete,true);
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(x!=0||z!=0){var p=Group("Weak platform",new Vector3(x*4,0,z*4),200,"metal");towerSections.Add(p);for(int k=0;k<2;k++)Piece(p.transform,"platform slab",new Vector3((k-.5f)*2,-.2f,0),new Vector3(1.97f,.4f,3.97f),concrete,70,true,p,"metal");for(int k=0;k<2;k++)Box("Stress seam",new Vector3(x*4+(k-.5f)*1.5f,.012f,z*4),new Vector3(.035f,.012f,3.8f),black,false);}
        for(int s=-1;s<=1;s+=2){Rail(new Vector3(s*5.9f,0,-5.9f),new Vector3(s*5.9f,0,0));Rail(new Vector3(s*5.9f,0,0),new Vector3(s*5.9f,0,5.9f));Rail(new Vector3(-5.9f,0,s*5.9f),new Vector3(0,0,s*5.9f));Rail(new Vector3(0,0,s*5.9f),new Vector3(5.9f,0,s*5.9f));}
        Surface("scaffold walkway",new Vector3(8,-.12f,0),new Vector3(4.1f,.25f,1.8f),wood,true,"wood");Surface("side scaffold",new Vector3(10.5f,-.12f,1.7f),new Vector3(1,.25f,5.2f),wood,true,"wood");
        for(int x=7;x<=11;x+=2)for(int z=-1;z<=1;z+=2)Box("Scaffolding upright",new Vector3(x,-2,z),new Vector3(.1f,5,.1f),steel,false);
        var panel=Piece(transform,"hanging sign",new Vector3(-4.3f,1.8f,2.7f),new Vector3(1.9f,.7f,.15f),blue,12,false,null,"metal");var tether=panel.gameObject.AddComponent<ConfigurableJoint>();tether.autoConfigureConnectedAnchor=false;tether.anchor=new Vector3(0,.35f,0);tether.connectedAnchor=panel.position+Vector3.up*.35f;tether.xMotion=tether.yMotion=tether.zMotion=ConfigurableJointMotion.Locked;tether.angularXMotion=tether.angularYMotion=tether.angularZMotion=ConfigurableJointMotion.Free;
        GBArt.Text(panel.transform,"LAST FLOOR",new Vector3(0,0,-.087f),.12f,Color.white,Quaternion.identity);
        Box("Tower body",new Vector3(0,-17,0),new Vector3(11.8f,31,11.8f),black,false);
        for(int f=0;f<8;f++)for(int x=-4;x<=4;x+=2)Box("Tower windows",new Vector3(x,-3-f*3.4f,-5.96f),new Vector3(1,1.9f,.02f),blue,false);
        for(int i=0;i<24;i++){int side=i%4;float x=side==0?-22:side==1?22:Random.Range(-35f,35f);float z=side==2?-24:side==3?24:Random.Range(-35f,35f);Box("City building",new Vector3(x,-22,z),new Vector3(Random.Range(6,12),Random.Range(15,35),Random.Range(6,12)),i%3==0?blue:black,false);}
        HazardStripe(new Vector3(0,.025f,-1.8f),new Vector3(3.4f,.02f,.18f));
    }
    void Gym() {
        spawns[0]=new Vector3(-1.1f,.28f,0);spawns[1]=new Vector3(1.1f,.28f,0);
        Surface("ring mat",new Vector3(0,0,0),new Vector3(8.8f,.4f,8.8f),GBArt.Mat("Ring canvas",new Color(.15f,.2f,.24f),0,.15f),true,"cloth");
        Box("Ring base",new Vector3(0,-.6f,0),new Vector3(9.1f,1,9.1f),black,false);Box("Gym floor",new Vector3(0,-1.15f,0),new Vector3(27,.2f,20),concrete);
        GBArt.Text(transform,"GRIP BREAK",new Vector3(0,.212f,1.5f),.48f,Color.white,Quaternion.Euler(90,0,0));
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){Surface("ring post",new Vector3(x*4.5f,1,z*4.5f),new Vector3(.17f,1.9f,.17f),orange,true);for(int h=0;h<3;h++)GBArt.Ball(transform,"Corner pad",new Vector3(x*4.34f,.62f+h*.38f,z*4.34f),new Vector3(.3f,.22f,.3f),black);}
        for(int level=0;level<3;level++)for(int side=0;side<4;side++){
            float y=.62f+level*.38f;Vector3 a=side<2?new Vector3(-4.4f,y,side==0?-4.4f:4.4f):new Vector3(side==2?-4.4f:4.4f,y,-4.4f);Vector3 b=side<2?new Vector3(4.4f,y,a.z):new Vector3(a.x,y,4.4f);
            for(int segment=0;segment<4;segment++){Vector3 p=Vector3.Lerp(a,b,(segment+.5f)/4);bool alongX=side<2;var rope=Piece(transform,"ring rope",p,alongX?new Vector3(2.22f,.065f,.065f):new Vector3(.065f,.065f,2.22f),level==1?orange:cream,2,false,null,"cloth");var j=rope.gameObject.AddComponent<ConfigurableJoint>();j.autoConfigureConnectedAnchor=false;j.connectedAnchor=p;j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Limited;j.linearLimit=new SoftJointLimit{limit=.35f};j.xDrive=j.yDrive=j.zDrive=new JointDrive{positionSpring=1300,positionDamper=100,maximumForce=2500};j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;ropes.Add(rope.transform);}
        }
        Surface("practice ledge",new Vector3(7.1f,.65f,0),new Vector3(3.5f,1.4f,3.5f),blue,true);GBArt.Text(transform,"LEDGE DRILL",new Vector3(7,.02f,-2.7f),.24f,Color.white,Quaternion.Euler(90,0,0));
        Table(new Vector3(-7.4f,-1,1),new Vector3(2.5f,.8f,1.4f));Piece(transform,"training block",new Vector3(-6.6f,-.55f,-2.2f),new Vector3(.8f,.8f,.8f),orange,12,false);
        Box("Gym wall",new Vector3(0,2,10),new Vector3(27,6,.2f),black);GBArt.Text(transform,"THE GYM",new Vector3(0,3.5f,9.85f),.65f,new Color(.35f,.75f,1),Quaternion.identity);
        for(int i=-2;i<=2;i++){Box("Wall acoustic panel",new Vector3(i*4,1.2f,9.8f),new Vector3(2.6f,2,.1f),blue,false);GBArt.Light(transform,new Vector3(i*4,4.1f,6),new Color(.8f,.9f,1),2,10);}
        GBGymVisual.Apply(this);
    }
    public bool Eliminated(Vector3 p) {
        if(kind==3)return p.y<-4;
        if(p.y<-2.8f)return true;
        if(kind==0)return Mathf.Abs(p.x)>4.0f||Mathf.Abs(p.z)>12;
        if(kind==1)return Mathf.Abs(p.x)>9.5f||Mathf.Abs(p.z)>7;
        float limit=sudden?Mathf.Lerp(6.8f,1.2f,Mathf.Clamp01(suddenTime/30)):12;return Mathf.Abs(p.x)>limit||Mathf.Abs(p.z)>limit;
    }
    public bool TouchesRopes(GBWrestler w) {if(kind!=3)return false;foreach(var b in w.bodies)if(Mathf.Abs(b.position.x)>4.0f||Mathf.Abs(b.position.z)>4.0f)return true;return false;}
    public void Tick(float dt) {
        elapsed+=dt;if(sudden)suddenTime+=dt;
        if(kind==0){for(int i=0;i<sweepers.Count;i++){float phase=(elapsed*4.5f+i*22)%44;var p=new Vector3(0,1.55f,22-phase);sweepers[i].MovePosition(p);if(sudden)sweepers[i].MovePosition(new Vector3(0,Mathf.Lerp(1.55f,.4f,suddenTime/30),p.z));}}
        if(kind==2&&!GBApp.I.Practice&&collapsed<towerSections.Count&&elapsed>=70+collapsed*6){var p=towerSections[collapsed];WarnSection(collapsed);if(elapsed>=76+collapsed*6){p.Break();collapsed++;}}
        if(sudden&&kind==1){float limit=Mathf.Lerp(9.5f,1.5f,Mathf.Clamp01(suddenTime/30));foreach(var w in GBApp.I.Wrestlers)if(!w.dummy&&Mathf.Abs(w.Root.position.x)>limit)w.eliminated=true;}
    }
    public void VisualTick(float dt) {
        if(kind==0){foreach(var t in scenery){var p=t.position;p.z-=dt*18;if(p.z<-80)p.z+=160;t.position=p;}}
        if(kind==1||kind==2){float x=Mathf.Lerp(kind==1?9.5f:6.8f,kind==1?1.5f:1.2f,Mathf.Clamp01(suddenTime/30)),z=kind==1?6.5f:x;for(int i=0;i<4;i++){boundary[i].gameObject.SetActive(sudden&&(kind==2||i<2));bool side=i<2;boundary[i].position=side?new Vector3(i==0?-x:x,.13f,0):new Vector3(0,.13f,i==2?-z:z);boundary[i].localScale=side?new Vector3(.12f,.25f,z*2):new Vector3(x*2,.25f,.12f);}}
    }
    public void WarnSection(int index){if(index<0||index>=towerSections.Count||index==warned)return;warned=index;GBApp.I.Toast("PLATFORM FAILURE • MOVE TO THE CENTER",3);foreach(var r in towerSections[index].parts){r.GetComponent<Renderer>().sharedMaterial=orange;for(int k=0;k<3;k++){var crack=GBArt.Box(r.transform,"Warning crack",new Vector3(0,.205f,(k-1)*.75f),new Vector3(1.7f,.012f,.03f),black,false);crack.transform.localRotation=Quaternion.Euler(0,k*22-20,0);}}}
}
