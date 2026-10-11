using UnityEngine;

// Body wobble and camera obstruction have separate filters; one view tracks its wrestler.
public class GBPlayerCameraRig:MonoBehaviour {
    public Camera Primary;
    public int PrimaryPlayer {get;private set;}
    public bool Split {get;private set;}
    GBApp app;
    readonly Vector3[] focus=new Vector3[2],focusVelocity=new Vector3[2];
    readonly bool[] initialized=new bool[2];
    public float ObstructionDistance {get;private set;}
    int lastEpoch=-1,lastPlayer=-1;
    float distanceVelocity,clearSince=-1,sideAngle,sideVelocity,chosenSide,nextChoice;
    readonly Vector3 offset=new Vector3(3.1f,3.8f,-5.8f);

    public void Setup(GBApp owner,Camera primary){
        app=owner;Primary=primary;
        Primary.name="Controlled wrestler follow camera";
    }
    public Camera ForPlayer(int player){return Primary;}
    public Vector3 RawFollowPoint(int player){return app.Wrestlers[player].Root.transform.position+Vector3.up*.55f;}
    // The mouse aim plane and look-at point share this stable focus.
    public Vector3 FollowPoint(int player){return initialized[player]?focus[player]:RawFollowPoint(player);}
    public void Menu(){Primary.rect=new Rect(0,0,1,1);Primary.fieldOfView=47;Split=false;lastEpoch=-1;initialized[0]=initialized[1]=false;}
    public void Tick(float shake){
        Split=false;
        PrimaryPlayer=app.Network.role==GBNetwork.Role.Client?1:0;
        Primary.rect=new Rect(0,0,1,1);
        bool snap=lastEpoch!=app.Epoch||lastPlayer!=PrimaryPlayer;
        Follow(Primary,PrimaryPlayer,snap,shake);
        lastEpoch=app.Epoch;lastPlayer=PrimaryPlayer;
    }
    static bool Finite(Vector3 value){return !float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);}
    float SafeDistance(Vector3 target,Vector3 direction,float length){
        // SphereCast misses starting overlaps; check the destination volume too.
        RaycastHit hit;float safe=length;
        if(Physics.SphereCast(target,.24f,direction,out hit,length,1,QueryTriggerInteraction.Ignore))safe=Mathf.Max(.12f,hit.distance-.28f);
        while(safe>.25f&&Physics.CheckSphere(target+direction*safe,.24f,1,QueryTriggerInteraction.Ignore))safe-=.15f;
        return Mathf.Max(.12f,safe);
    }
    void Follow(Camera view,int player,bool snap,float shake){
        float dt=Mathf.Clamp(Time.unscaledDeltaTime,.0001f,.05f);
        Vector3 raw=RawFollowPoint(player);if(!Finite(raw))return;
        snap|=!initialized[player]||Vector3.Distance(focus[player],raw)>12;
        if(snap){
            focus[player]=raw;focusVelocity[player]=Vector3.zero;initialized[player]=true;
            sideAngle=chosenSide=sideVelocity=distanceVelocity=0;clearSince=-1;nextChoice=0;
        }else{
            // Quick lateral tracking, slower vertical tracking for foot/body bob.
            Vector3 target=raw;
            if(Mathf.Abs(target.y-focus[player].y)<.055f)target.y=focus[player].y;
            Vector3 v=focusVelocity[player],p=focus[player];
            p.x=Mathf.SmoothDamp(p.x,target.x,ref v.x,.065f,120,dt);
            p.z=Mathf.SmoothDamp(p.z,target.z,ref v.z,.065f,120,dt);
            p.y=Mathf.SmoothDamp(p.y,target.y,ref v.y,.16f,120,dt);
            Vector3 horizontal=Vector3.ProjectOnPlane(p-raw,Vector3.up);
            if(horizontal.magnitude>.35f)p=raw+horizontal.normalized*.35f+Vector3.up*(p.y-raw.y);
            p.y=Mathf.Clamp(p.y,raw.y-.65f,raw.y+.65f);
            focus[player]=p;focusVelocity[player]=v;
        }
        Vector3 center=focus[player];float length=offset.magnitude;Vector3 preferred=offset/length;
        float defaultSafe=SafeDistance(center,preferred,length);
        // Hold a side choice at corners, only switching if the view is very close.
        if(Time.unscaledTime>=nextChoice){
            nextChoice=Time.unscaledTime+.35f;
            float currentSafe=SafeDistance(center,Quaternion.Euler(0,chosenSide,0)*preferred,length);
            if(defaultSafe<2.8f&&currentSafe<3.8f){
                float best=currentSafe,bestSide=chosenSide;
                foreach(float side in new float[]{-55,55,-100,100}){
                    float candidate=SafeDistance(center,Quaternion.Euler(0,side,0)*preferred,length);
                    if(candidate>best+1.1f){best=candidate;bestSide=side;}
                }
                chosenSide=bestSide;
            }else if(defaultSafe>length-.5f)chosenSide=0;
        }
        sideAngle=snap?chosenSide:Mathf.SmoothDampAngle(sideAngle,chosenSide,ref sideVelocity,.18f,360,dt);
        Vector3 direction=Quaternion.Euler(0,sideAngle,0)*preferred;
        float available=SafeDistance(center,direction,length);
        if(snap){ObstructionDistance=available;distanceVelocity=0;}
        else if(available<ObstructionDistance){ObstructionDistance=available;distanceVelocity=0;clearSince=-1;}
        else{
            if(available>ObstructionDistance+.18f){if(clearSince<0)clearSince=Time.unscaledTime;}else clearSince=-1;
            if(clearSince>=0&&Time.unscaledTime-clearSince>.22f)ObstructionDistance=Mathf.SmoothDamp(ObstructionDistance,available,ref distanceVelocity,.22f,6,dt);
        }
        // No second position filter: the wrestler remains at a predictable place.
        view.transform.position=center+direction*Mathf.Min(available,ObstructionDistance);
        view.transform.LookAt(center,Vector3.up);
        view.fieldOfView=Mathf.Lerp(65,47,Mathf.InverseLerp(1.5f,4.5f,ObstructionDistance));
        if(shake>0)view.transform.Rotate(0,0,Mathf.Sin(Time.unscaledTime*34)*Mathf.Min(.35f,shake*2),Space.Self);
    }
}
