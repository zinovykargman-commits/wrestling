using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[Serializable] public class GBTuning {
    public float walkSpeed=3.3f, sprintSpeed=5.0f, moveForce=1450f, gripReach=1.45f;
    public float gripStrength=1050f, staminaRegen=19f, liftCost=26f, throwAssist=4.5f;
    public float impactThreshold=4.6f, recoverySeconds=1.05f;
}

[Serializable] public struct GBInput {
    public float x,z,ax,az,hx,hy,hz;
    public int flags,sequence;
    public double stamp;
    // Bit 256 (retired tackle) is rejected along with Throw.
    // Throw is reserved solely so historical audits compile; no live input may
    // activate it. Throws now follow the player's actual arm/body movement.
    public const int Left=1,Right=2,Sprint=4,Jump=8,Lift=16,Throw=32,Strike=64,Brace=128,Pin=512,Shove=1024,HandAim=2048,Sprawl=4096;
    public bool Has(int bit) { return (flags & bit)!=0; }
    public bool HasHandAim {get{return Has(HandAim);}}
    public Vector3 HandOffset {get{return Vector3.ClampMagnitude(new Vector3(hx,Mathf.Clamp(hy,-1.10f,1.25f),hz),1.35f);}}
    public Vector3 Move { get { return Vector3.ClampMagnitude(new Vector3(x,0,z),1); } }
    public Vector3 Aim { get { var v=new Vector3(ax,0,az); return v.sqrMagnitude>.05f ? v.normalized:Vector3.forward; } }
    public GBInput Sanitize() {
        if(!Finite(x)||!Finite(z)||!Finite(ax)||!Finite(az)||!Finite(hx)||!Finite(hy)||!Finite(hz)||double.IsNaN(stamp)||double.IsInfinity(stamp))return default;
        x=Mathf.Clamp(x,-1,1);z=Mathf.Clamp(z,-1,1);ax=Mathf.Clamp(ax,-1,1);az=Mathf.Clamp(az,-1,1);flags&=7903;
        Vector3 hand=HasHandAim?HandOffset:Vector3.zero;hx=hand.x;hy=hand.y;hz=hand.z;
        return this;
    }
    static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
}

public static class GBPad {
    [StructLayout(LayoutKind.Sequential)] public struct Pad { public ushort buttons; public byte leftTrigger,rightTrigger; public short lx,ly,rx,ry; }
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint packet; public Pad pad; }
    [StructLayout(LayoutKind.Sequential)] struct Vibration { public ushort left,right; }
    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index,out State state);
    [DllImport("xinput1_4.dll")] static extern uint XInputSetState(uint index,ref Vibration v);
    public static bool Read(int index,out Pad pad) {
        pad=default; if(Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsEditor)return false;
        try { State s; if(XInputGetState((uint)index,out s)==0){pad=s.pad;return true;} } catch(DllNotFoundException){} return false;
    }
    public static void Rumble(int index,float value) { if(index<0)return; try {var v=new Vibration {left=(ushort)(Mathf.Clamp01(value)*25000),right=(ushort)(Mathf.Clamp01(value)*42000)}; XInputSetState((uint)index,ref v);}catch{} }
    static float Axis(short x) { float f=x/32767f; return Mathf.Abs(f)<.16f?0:Mathf.Sign(f)*(Mathf.Abs(f)-.16f)/.84f; }
    public static GBInput Input(int index,Vector3 previousAim) {
        Pad p; if(!Read(index,out p))return default;
        var i=new GBInput{x=Axis(p.lx),z=Axis(p.ly),ax=Axis(p.rx),az=Axis(p.ry)};
        if(i.ax*i.ax+i.az*i.az<.05f){i.ax=previousAim.x;i.az=previousAim.z;}
        if((p.buttons&0x100)!=0)i.flags|=GBInput.Left;
        if((p.buttons&0x200)!=0)i.flags|=GBInput.Right;
        if((p.buttons&0x40)!=0)i.flags|=GBInput.Sprint;
        if((p.buttons&0x80)!=0)i.flags|=GBInput.Sprawl;
        if((p.buttons&0x1000)!=0)i.flags|=GBInput.Jump;
        if(p.rightTrigger>90)i.flags|=GBInput.Lift;
        if((p.buttons&0x4000)!=0)i.flags|=GBInput.Strike;
        if(p.leftTrigger>90)i.flags|=GBInput.Brace;
        if((p.buttons&0x2)!=0)i.flags|=GBInput.Pin;
        if((p.buttons&0x1)!=0)i.flags|=GBInput.Shove;
        return i;
    }
}

public static class GBArt {
    static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static Material worldText;static Font worldFont;
    public static Material Mat(string key,Color color,float metal=0,float smooth=.35f,bool glow=false) {
        Material m;if(mats.TryGetValue(key,out m))return m;
        m=new Material(Shader.Find("Standard")); m.name=key; m.color=color; m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",smooth);
        if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.7f);}
        mats[key]=m;return m;
    }
    public static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,bool collider=false) {
        var go=GameObject.CreatePrimitive(type); go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider){var shapeCollider=go.GetComponent<Collider>();shapeCollider.enabled=false;UnityEngine.Object.Destroy(shapeCollider);}return go;
    }
    public static GameObject Box(Transform p,string n,Vector3 at,Vector3 size,Material m,bool solid=true) {return Shape(p,n,PrimitiveType.Cube,at,size,m,solid);}
    public static GameObject Ball(Transform p,string n,Vector3 at,Vector3 size,Material m) {return Shape(p,n,PrimitiveType.Sphere,at,size,m);}
    public static GameObject Cylinder(Transform p,string n,Vector3 a,Vector3 b,float radius,Material m,bool solid=false) {
        var g=Shape(p,n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),m,solid);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return g;
    }
    public static TextMesh Text(Transform p,string text,Vector3 at,float size,Color color,Quaternion rotation) {
        var go=new GameObject(text);go.transform.SetParent(p,false);go.transform.localPosition=at;go.transform.localRotation=rotation;
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.characterSize=size*.12f;tm.fontSize=72;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
        if(worldFont==null)worldFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");tm.font=worldFont;worldFont.RequestCharactersInTexture(text,72);
        if(worldText==null)worldText=new Material(Shader.Find("GripBreak/WorldText"));worldText.mainTexture=worldFont.material.mainTexture;go.GetComponent<MeshRenderer>().sharedMaterial=worldText;return tm;
    }
    public static void UpdateText(){if(worldText&&worldFont)worldText.mainTexture=worldFont.material.mainTexture;}
    public static void Light(Transform p,Vector3 at,Color color,float intensity,float range) {
        var go=new GameObject("Area light");go.transform.SetParent(p,false);go.transform.localPosition=at;var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;
    }
    public static void WorldLighting() {
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.37f,.46f,.58f);RenderSettings.ambientEquatorColor=new Color(.23f,.27f,.34f);RenderSettings.ambientGroundColor=new Color(.09f,.11f,.15f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.1f,.14f,.21f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0055f;
        var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetColor("_SkyTint",new Color(.25f,.4f,.6f));sky.SetColor("_GroundColor",new Color(.06f,.08f,.12f));sky.SetFloat("_Exposure",.65f);RenderSettings.skybox=sky;
        var g=new GameObject("Evening sun");g.transform.rotation=Quaternion.Euler(42,-35,0);var l=g.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.1f;l.color=new Color(1,.79f,.57f);l.shadows=LightShadows.Soft;
        var f=new GameObject("Cool fill");f.transform.rotation=Quaternion.Euler(48,150,0);var fl=f.AddComponent<Light>();fl.type=LightType.Directional;fl.color=new Color(.34f,.64f,1);fl.intensity=.6f;
    }
}

public class GBSound:MonoBehaviour {
    public float volume=.7f;
    public AudioSource source,ambience;
    public AudioClip wood,metal,cloth,hit,grip,win,wind,beat,cheer;
    void Awake() {
        source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0; source.priority=64;
        ambience=gameObject.AddComponent<AudioSource>();ambience.loop=true;ambience.spatialBlend=0;
        wood=Noise("Wood crack",.16f,180,.8f);metal=Noise("Metal clang",.35f,730,.35f);cloth=Noise("Mat thump",.12f,70,.12f);hit=Noise("Impact",.16f,105,.45f);grip=Noise("Grip latch",.07f,420,.07f);win=Noise("Round bell",.9f,650,.05f);wind=Noise("Train wind",3.0f,40,.45f,true);beat=Noise("Arena pulse",2f,65,.035f,true);cheer=Noise("Crowd swell",1.3f,210,1.6f);
    }
    AudioClip Noise(string name,float secs,float freq,float noise,bool loop=false) {
        int rate=22050;int n=(int)(secs*rate);float[] v=new float[n];var rng=new System.Random(name.GetHashCode());float smooth=0;
        for(int i=0;i<n;i++){float t=(float)i/rate;float env=loop?.45f:Mathf.Exp(-t/(secs*.22f));smooth=Mathf.Lerp(smooth,(float)(rng.NextDouble()*2-1),.18f);v[i]=env*(Mathf.Sin(t*freq*Mathf.PI*2)*.38f+Mathf.Sin(t*freq*1.52f*Mathf.PI*2)*.13f+smooth*noise);}
        var c=AudioClip.Create(name,n,1,rate,false);c.SetData(v,0);return c;
    }
    public void Play(string type,float strength=1) {if(volume<=0)return;source.pitch=UnityEngine.Random.Range(.88f,1.1f);source.PlayOneShot(type=="wood"?wood:type=="metal"?metal:type=="cloth"?cloth:type=="grip"?grip:type=="win"?win:type=="crowd"?cheer:hit,Mathf.Clamp(strength,.12f,1)*volume);}
    public void SetArena(int arena) {ambience.clip=arena==0?wind:beat;ambience.volume=volume*.16f;ambience.Play();}
    void Update(){ambience.volume=volume*.16f;}
}

public class GBTarget:MonoBehaviour {
    public GBWrestler wrestler;
    public GBBreakable prop;
    public string region="surface",sound="metal";
    public bool grabbable=true;
    public Rigidbody Body {get{return GetComponent<Rigidbody>();}}
}

public class GBContact:MonoBehaviour {
    public GBWrestler wrestler;public GBBreakable prop;public int bodyIndex=-1; public string sound="cloth"; float last;
    public string AuditContacts="";float auditContactAt=-1;
    void OnCollisionStay(Collision c){
        if(!GBApp.I||!GBApp.I.Automated)return;
        if(auditContactAt!=Time.fixedTime){auditContactAt=Time.fixedTime;AuditContacts="";}
        Vector3 normal=Vector3.zero;for(int n=0;n<c.contactCount;n++)normal+=c.GetContact(n).normal;
        AuditContacts+=c.collider.name+":"+normal.normalized.ToString("F2")+" ";
    }
    void OnCollisionEnter(Collision c) {
        if(!GBApp.I||!GBApp.I.Authority)return;
        var contacted=c.collider.GetComponent<GBTarget>();
        if(wrestler&&contacted&&contacted.wrestler==wrestler)return;
        float speed=c.relativeVelocity.magnitude;
        if(wrestler){
            // Boots, knees and arms repeatedly touch the mat while walking,
            // reaching or recovering. Those support contacts are not attacks.
            bool core=bodyIndex>=0&&bodyIndex<=2;bool otherWrestler=c.collider.GetComponent<GBTarget>() is GBTarget target&&target.wrestler&&target.wrestler!=wrestler;
            bool upwardSurface=false;for(int n=0;n<c.contactCount;n++)if(c.GetContact(n).normal.y>.55f){upwardSurface=true;break;}
            bool damagingLanding=core&&upwardSurface&&speed>7.0f;
            if(core&&(otherWrestler||!upwardSurface||damagingLanding))wrestler.Contact(speed,c.impulse.magnitude);
        }
        if(prop)prop.Hit(speed,c.impulse.magnitude);
        if(speed>1.3f&&Time.time-last>.18f){last=Time.time;GBApp.I.Sfx.Play(sound,Mathf.Clamp01(speed/7));if(speed>4)GBApp.I.Shake(.07f);}
    }
}

public class GBBreakable:MonoBehaviour {
    public List<Rigidbody> parts=new List<Rigidbody>();public float health=130;public bool broken;public string type="wood";
    public void Hit(float speed,float impulse) {if(broken||speed<2.8f)return;health-=Mathf.Min(100,impulse*.25f+speed*4);if(health<=0)Break();}
    public void Stress(float force){if(!broken){health-=force*Time.fixedDeltaTime;if(health<0)Break();}}
    public void Break(){if(broken)return;broken=true;foreach(var b in parts){if(b){b.isKinematic=false;b.WakeUp();}}GBApp.I.Sfx.Play(type,1);GBApp.I.Burst(transform.position,new Color(.72f,.6f,.4f));}
    public void ReplicateBreak(){broken=true;GBApp.I.Sfx.Play(type,.6f);GBApp.I.Burst(transform.position,new Color(.72f,.6f,.4f));}
}
