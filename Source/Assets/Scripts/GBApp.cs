using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class GBApp:MonoBehaviour {
    public static GBApp I;
    public enum GamePhase {Menu,Countdown,Fighting,RoundOver,MatchOver}
    public GBTuning Tuning=new GBTuning();
    public GBSound Sfx;public GBNetwork Network;public GBUpdater Updater;
    public GameObject World;public GBArena Arena;
    public List<GBWrestler> Wrestlers=new List<GBWrestler>();
    public List<Rigidbody> Registry=new List<Rigidbody>();
    public Vector3[] TargetPositions;public Quaternion[] TargetRotations;
    public int AIDifficulty=1,TrainingPartner=0;
    public int Epoch,CurrentArena,CurrentMode,SelectedArena=0,SelectedMode=0;
    public int[] Scores=new int[2];
    public bool InMatch,Paused;public GamePhase Phase;
    public float Remaining=120,PinCount,ClientSnapshotAt;public double LastHostTime;
    public string RoundMessage="",PlayerName="PLAYER ONE";
    public bool Practice {get{return CurrentMode==2;}}
    public bool Authority {get{return !Network||Network.Authority;}}
    public bool CameraShake=true,Rumble=true;
    public List<string> Issues=new List<string>();
    [NonSerialized] public GBInput[] ScriptedInputs;
    [NonSerialized] public Func<KeyCode,bool> AuditKeyState;
    [NonSerialized] public Func<KeyCode,bool> AuditKeyDown;
    [NonSerialized] public Vector3? AuditMousePosition;
    GBBot bot=new GBBot();
    bool focusPaused;
    public bool LocalAI {get{return Network.role==GBNetwork.Role.Offline&&!Practice;}}
    bool Scripted(int p){return Automated&&ScriptedInputs!=null&&p>=0&&p<ScriptedInputs.Length;}
    bool KeyHeld(KeyCode k){return Automated&&AuditKeyState!=null?AuditKeyState(k):Input.GetKey(k);}
    bool KeyDown(KeyCode k){return Automated&&AuditKeyDown!=null?AuditKeyDown(k):Input.GetKeyDown(k);}
    Vector3 MousePosition {get{return Automated&&AuditMousePosition.HasValue?AuditMousePosition.Value:Input.mousePosition;}}
    public bool Automated;
    int windowWidth=1920,windowHeight=1080;
    Camera cam;public GBPlayerCameraRig Views;public bool ControlsVisible;Vector3 cameraVelocity;float shake,phaseUntil,toastUntil;
    string toast="",screen="home",invite="",captureAction="";
    float fps=60,fpsMin=1000,fpsSeconds;int fpsFrames;bool focus=true;
    GBInput[] inputs=new GBInput[2];int[] lastPoll=new int[2],buffered=new int[2];
    public int LocalDevice=0; // 0 keyboard/mouse, 1 one controller
    Dictionary<string,KeyCode> keys=new Dictionary<string,KeyCode>();Dictionary<string,int> flags=new Dictionary<string,int>();
    Dictionary<string,int> padCodes=new Dictionary<string,int>();bool capturePad;float captureStart;
    GUIStyle title,heading,body,small,button,label;Texture2D pixel;Font font;
    int lessonStep;float lessonStart,lessonLift,lessonClimbTarget;bool lessonHadGrip;
    string[] lessons={"MOVE • walk three metres around the mat.","GRAB • hold either hand near your friend or the dummy.","LIFT • hold lift until your target rises off the mat.","THROW • aim away, then throw to release your grip.","ESCAPE • hold brace for two seconds; your friend can resist too.","LEDGE • catch the blue ledge with a hand, then jump to climb.","TRAINING COMPLETE • experiment with tackles, slams and lower-body trips."};
    string[] arenaNames={"FREIGHT TRAIN","FURNITURE STORE","CRUMBLING TOWER","PRACTICE GYM"};
    string[] arenaDetails={"Moving freight cars, breakaway rails, low beams.","Tables, chairs, display shelves and a loading pit.","A rooftop that gives way under your feet.","A proper ring, a dummy and room to experiment."};
    string[] actions={"Left hand","Right hand","Sprint","Jump / climb","Lift","Throw","Strike","Brace / escape","Tackle","Pin","Shove","Forward","Back","Left","Right"};
    public float MeasuredFPS {get{return fpsSeconds>0?fpsFrames/fpsSeconds:0;}}
    public float MinFPS {get{return fpsMin;}}
    public float PhaseSeconds {get{return Mathf.Max(0,phaseUntil-Time.time);}}
    public void SetPhaseSeconds(float seconds){phaseUntil=Time.time+seconds;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(!FindFirstObjectByType<GBApp>())new GameObject("GRIP BREAK").AddComponent<GBApp>();}
    void Awake() {
        I=this;Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=1f/240;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception||type==LogType.Error){if(!Issues.Contains(message))Issues.Add(message);}};
        Physics.defaultSolverIterations=16;Physics.defaultSolverVelocityIterations=8;Physics.defaultContactOffset=.012f;
        Sfx=gameObject.AddComponent<GBSound>();Network=gameObject.AddComponent<GBNetwork>();Updater=gameObject.AddComponent<GBUpdater>();
        Defaults();LoadPrefs();ScriptedInputs=null;
        GBArt.WorldLighting();cam=new GameObject("Shared match camera").AddComponent<Camera>();cam.gameObject.AddComponent<AudioListener>();cam.fieldOfView=47;cam.nearClipPlane=.15f;cam.farClipPlane=200;cam.backgroundColor=new Color(.06f,.09f,.14f);cam.allowHDR=true;cam.allowMSAA=true;
        Views=gameObject.AddComponent<GBPlayerCameraRig>();Views.Setup(this,cam);
        BuildWorld(0,0,false);cam.transform.position=new Vector3(12,12,-19);cam.transform.LookAt(new Vector3(0,.5f,0));
        pixel=Texture2D.whiteTexture;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        string[] args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--momentum-test")>=0||Array.IndexOf(args,"--throw-test")>=0||Array.IndexOf(args,"--input-test")>=0||Array.IndexOf(args,"--ai-test")>=0||Array.IndexOf(args,"--updater-test")>=0||Array.IndexOf(args,"--overhaul-test")>=0||Array.IndexOf(args,"--audit")>=0||Array.IndexOf(args,"--camera-test")>=0||Array.IndexOf(args,"--moves")>=0||Array.IndexOf(args,"--host-test")>=0||Array.IndexOf(args,"--client-test")>=0||Array.IndexOf(args,"--soak")>=0||Array.IndexOf(args,"--record")>=0||Array.IndexOf(args,"--menu-shot")>=0){Automated=true;gameObject.AddComponent<GBAudit>();}
    }
    void Defaults() {
        KeyCode[] defaults={KeyCode.Mouse0,KeyCode.Mouse1,KeyCode.LeftShift,KeyCode.Space,KeyCode.E,KeyCode.F,KeyCode.Q,KeyCode.LeftControl,KeyCode.R,KeyCode.C,KeyCode.X,KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D};
        int[] bits={1,2,4,8,16,32,64,128,256,512,1024,0,0,0,0};int[] pads={0x100,0x200,0x40,0x1000,0x20000,0x8000,0x4000,0x10000,0x2000,0x2,0x1,0,0,0,0};
        for(int i=0;i<actions.Length;i++){keys[actions[i]]=defaults[i];flags[actions[i]]=bits[i];padCodes[actions[i]]=pads[i];}
    }
    void LoadPrefs(){AIDifficulty=Mathf.Clamp(PlayerPrefs.GetInt("aiDifficulty",1),0,2);PlayerName=PlayerPrefs.GetString("name","PLAYER ONE");Sfx.volume=PlayerPrefs.GetFloat("volume",.7f);CameraShake=PlayerPrefs.GetInt("shake",1)==1;Rumble=PlayerPrefs.GetInt("rumble",1)==1;foreach(var a in actions){keys[a]=(KeyCode)PlayerPrefs.GetInt("key_"+a,(int)keys[a]);padCodes[a]=PlayerPrefs.GetInt("pad_"+a,padCodes[a]);}}
    void SavePrefs(){if(Automated)return;PlayerPrefs.SetInt("aiDifficulty",AIDifficulty);PlayerPrefs.SetString("name",PlayerName);PlayerPrefs.SetFloat("volume",Sfx.volume);PlayerPrefs.SetInt("shake",CameraShake?1:0);PlayerPrefs.SetInt("rumble",Rumble?1:0);foreach(var a in actions){PlayerPrefs.SetInt("key_"+a,(int)keys[a]);PlayerPrefs.SetInt("pad_"+a,padCodes[a]);}PlayerPrefs.Save();}
    public void Register(Rigidbody rb){Registry.Add(rb);}
    public void Register(GBWrestler w){Wrestlers.Add(w);foreach(var rb in w.bodies)Register(rb);}
    public void BuildWorld(int arena,int mode,bool client) {
        if(World){World.SetActive(false);Destroy(World);}Registry.Clear();Wrestlers.Clear();CurrentArena=arena;CurrentMode=mode;
        World=new GameObject("Match world");Arena=World.AddComponent<GBArena>();Arena.Build(arena);bot=new GBBot();
        for(int p=0;p<2;p++){var g=new GameObject(p==0?"Ember":"Cobalt");g.transform.SetParent(World.transform);var w=g.AddComponent<GBWrestler>();w.Build(p,Arena.spawns[p],p==0?new Color(.95f,.29f,.075f):new Color(.04f,.52f,.78f));w.playerName=Network.role==GBNetwork.Role.Client?(p==1?PlayerName:Network.peerName):(p==0?PlayerName:Network.connected?Network.peerName:"PLAYER TWO");}
        if(Network.role==GBNetwork.Role.Offline)Wrestlers[1].playerName=mode==2?"TRAINING PARTNER":"COBALT • AI";
        if(arena==3&&mode==2){var g=new GameObject("Practice dummy");g.transform.SetParent(World.transform);g.AddComponent<GBWrestler>().Build(2,new Vector3(0,.28f,2),new Color(.53f,.48f,.4f),true);}
        TargetPositions=new Vector3[Registry.Count];TargetRotations=new Quaternion[Registry.Count];for(int n=0;n<Registry.Count;n++){TargetPositions[n]=Registry[n].position;TargetRotations[n]=Registry[n].rotation;if(client)Registry[n].isKinematic=true;}
        if(client){foreach(var j in World.GetComponentsInChildren<Joint>())Destroy(j);}
        Sfx.SetArena(arena);PinCount=0;lessonStep=0;lessonStart=Time.time;lessonHadGrip=false;lessonLift=lessonClimbTarget=0;
    }
    public void BeginMatch(int arena,int mode) {
        if(!Authority)return;if(mode==1||mode==2)arena=3;else arena=Mathf.Clamp(arena,0,2);
        GBPad.Pad pad;if(LocalDevice==1&&!GBPad.Read(0,out pad)){LocalDevice=0;Toast("No controller detected • using keyboard and mouse",4);}
        SelectedArena=arena;SelectedMode=mode;Scores[0]=Scores[1]=0;InMatch=true;Paused=false;focusPaused=false;ControlsVisible=false;Time.timeScale=1;RoundMessage="";Epoch++;BuildWorld(arena,mode,false);Phase=mode==2?GamePhase.Fighting:GamePhase.Countdown;Remaining=120;phaseUntil=Time.time+3;screen="home";
    }
    public void ReturnToMenu(){Paused=false;focusPaused=false;ControlsVisible=false;Time.timeScale=1;InMatch=false;Phase=GamePhase.Menu;screen="home";RoundMessage="";if(Authority&&Wrestlers.Count>0&&!Wrestlers[0].joints[1]){Epoch++;BuildWorld(SelectedArena<3?SelectedArena:0,0,false);}}
    public void ShowMenu(string name){screen=name;}
    void Update() {
        if(KeyDown(KeyCode.F11))ToggleFullscreen();
        Network.Pump();float dt=Time.unscaledDeltaTime;if(dt>0){fps=Mathf.Lerp(fps,1/dt,.06f);if(Time.realtimeSinceStartup>5){fpsSeconds+=dt;fpsFrames++;fpsMin=Mathf.Min(fpsMin,1/dt);}}
        if(InMatch&&Practice&&Network.role==GBNetwork.Role.Offline&&!Paused&&!ControlsVisible&&KeyDown(KeyCode.T))ResetDrill();
        if(InMatch&&KeyDown(KeyCode.F1))ControlsVisible=!ControlsVisible;
        if(KeyDown(KeyCode.Escape)){if(captureAction!=""){captureAction="";}else if(InMatch&&ControlsVisible)ControlsVisible=false;else if(InMatch){focusPaused=false;Paused=!Paused;if(Authority&&Network.role==GBNetwork.Role.Offline)Time.timeScale=Paused?0:1;}else screen="home";}
        if(captureAction!=""&&Time.unscaledTime-captureStart>.25f)CaptureBinding();
        if(InMatch&&!Paused&&!ControlsVisible&&(focus||Automated)){int mine=Network.role==GBNetwork.Role.Client?1:0;for(int p=0;p<2;p++){if(p!=mine&&(Network.role!=GBNetwork.Role.Offline||!Scripted(p)))continue;var i=Scripted(p)?ScriptedInputs[p]:PollInput(p);int pulse=(i.flags&~lastPoll[p])&(GBInput.Jump|GBInput.Throw|GBInput.Strike|GBInput.Tackle|GBInput.Shove);buffered[p]|=pulse;lastPoll[p]=i.flags;inputs[p]=i;if(Network.role==GBNetwork.Role.Client)Wrestlers[p].facing=i.Aim;}if(Network.role==GBNetwork.Role.Client){var i=inputs[1];i.flags|=buffered[1];Network.SendInput(i);buffered[1]=0;}}
        else {inputs[0]=inputs[1]=default;lastPoll[0]=lastPoll[1]=0;buffered[0]=buffered[1]=0;if(Network.role==GBNetwork.Role.Client&&Network.connected)Network.SendInput(default);}
        if(!Authority&&World){float lerp=1-Mathf.Exp(-Time.unscaledDeltaTime*22);for(int i=0;i<Registry.Count;i++){Registry[i].position=Vector3.Lerp(Registry[i].position,TargetPositions[i],lerp);Registry[i].rotation=Quaternion.Slerp(Registry[i].rotation,TargetRotations[i],lerp);}}
        Arena?.VisualTick(dt);foreach(var w in Wrestlers)w.Visuals();
        if(InMatch&&Practice&&Authority&&Wrestlers.Count>0)Tutorial();
        if(Rumble&&Time.frameCount%14==0){for(int i=0;i<2;i++)GBPad.Rumble(i,0);}
    }
    void Tutorial(){var w=Wrestlers[0];switch(lessonStep){case 0:if(w.moveDistance>3)lessonStep++;break;case 1:if(w.GripCount>0){lessonStep++;foreach(var h in w.hands)if(h.target){lessonLift=h.target.wrestler?h.target.wrestler.Root.position.y:h.target.Body.position.y;break;}}break;case 2:if(w.command.Has(GBInput.Lift)&&w.GripCount>0)foreach(var h in w.hands)if(h.target&&(h.target.wrestler?h.target.wrestler.Root.position.y:h.target.Body.position.y)>lessonLift+.15f){lessonStep++;lessonHadGrip=true;break;}break;case 3:if(lessonHadGrip&&w.command.Has(GBInput.Throw)&&w.GripCount==0){lessonStep++;lessonStart=Time.time;}break;case 4:if(!w.command.Has(GBInput.Brace))lessonStart=Time.time;else if(Time.time-lessonStart>2)lessonStep++;break;case 5:if(w.hanging&&w.command.Has(GBInput.Jump))foreach(var h in w.hands)if(h.target)lessonClimbTarget=h.target.GetComponent<Collider>().bounds.max.y+.7f;if(lessonClimbTarget>0&&w.Root.position.y>lessonClimbTarget&&Vector3.Dot(w.bodies[1].transform.up,Vector3.up)>.5f)lessonStep++;break;}}
    void FixedUpdate() {
        if(!Authority||!World)return;
        for(int p=0;p<Wrestlers.Count;p++) {
            GBInput i=default;
            if(InMatch&&Phase==GamePhase.Fighting){if(Wrestlers[p].dummy)i=new GBInput{ax=0,az=-1};else if(Network.role==GBNetwork.Role.Host&&p==1)i=Network.remoteInput;else if(!Paused){if(p==1&&Network.role==GBNetwork.Role.Offline&&!Scripted(p))i=Practice?bot.TrainingTick(this,Wrestlers[1],Wrestlers[0]):bot.Tick(this,Wrestlers[1],Wrestlers[0]);else if(p<inputs.Length){i=inputs[p];i.flags|=buffered[p];buffered[p]=0;}}}
            else i=new GBInput{ax=0,az=1};
            Wrestlers[p].Simulate(i);
        }
        if(InMatch)Arena.Tick(Time.fixedDeltaTime);
        if(!InMatch)return;
        if(Phase==GamePhase.Countdown&&Time.time>=phaseUntil){Phase=GamePhase.Fighting;Sfx.Play("win",.6f);}
        if(Phase==GamePhase.Fighting) {
            if(!Practice)Remaining-=Time.fixedDeltaTime;
            if(CurrentMode==0){foreach(var w in Wrestlers)if(!w.dummy&&(Arena.Eliminated(w.Root.position)||w.eliminated))w.eliminated=true;
                if(Wrestlers[0].eliminated||Wrestlers[1].eliminated){if(phaseUntil<=0)phaseUntil=Time.time+.25f;if(Time.time>=phaseUntil)RoundWin(Wrestlers[0].eliminated&&Wrestlers[1].eliminated?-1:Wrestlers[0].eliminated?1:0);}
                else phaseUntil=0;
                if(Remaining<=0&&!Arena.sudden){Arena.sudden=true;Toast("SUDDEN DEATH • THE ARENA IS CLOSING",4);}
                if(Remaining<-30)RoundWin(-1);
            } else if(CurrentMode==1)CheckPin();
            else if(Practice){foreach(var w in Wrestlers)if(Arena.Eliminated(w.Root.position))w.ResetTo(w.dummy?new Vector3(0,.28f,2):Arena.spawns[w.player]);}
            if(CurrentMode==1){foreach(var w in Wrestlers)if(Mathf.Abs(w.Root.position.x)>5.3f||Mathf.Abs(w.Root.position.z)>5.3f||w.Root.position.y<-1.3f){ResetRound(false);Toast("OUT OF THE RING • RESET",2);break;}}
        }
        if(Phase==GamePhase.RoundOver&&Time.time>=phaseUntil)ResetRound(true);
    }
    void CheckPin() {
        int attacker=-1;for(int p=0;p<2;p++){var a=Wrestlers[p];var d=Wrestlers[1-p];if(!a.command.Has(GBInput.Pin)||a.Down||a.GripCount<1||a.stamina<3||Vector3.Distance(a.Root.position,d.Root.position)>1.65f||Arena.RopeBreak(d))continue;
            Vector3 l=d.bodies[1].transform.TransformPoint(new Vector3(-.23f,.18f,0));Vector3 r=d.bodies[1].transform.TransformPoint(new Vector3(.23f,.18f,0));
            bool shoulders=l.y<.62f&&r.y<.62f&&Mathf.Abs(Vector3.Dot(d.bodies[1].transform.up,Vector3.up))<.55f;
            if(shoulders){attacker=p;d.pressureUntil=Time.time+.08f;d.bodies[1].AddForce(Vector3.down*220,ForceMode.Force);a.stamina=Mathf.Max(0,a.stamina-Time.fixedDeltaTime*14);break;}
        }
        if(attacker>=0){PinCount+=Time.fixedDeltaTime;if(PinCount>=3)RoundWin(attacker);}else PinCount=0;
    }
    public void RoundWin(int winner) {
        if(Phase!=GamePhase.Fighting)return;
        foreach(var w in Wrestlers){w.Release(0);w.Release(1);}
        if(winner>=0){Scores[winner]++;RoundMessage=Wrestlers[winner].playerName+" WINS THE ROUND";}else RoundMessage="DRAW • RUN IT BACK";
        Sfx.Play("win",.8f);if(CurrentArena==3)Sfx.Play("crowd",.45f);if(winner>=0&&Scores[winner]>=3){Phase=GamePhase.MatchOver;RoundMessage=Wrestlers[winner].playerName+" TAKES THE MATCH";}else{Phase=GamePhase.RoundOver;phaseUntil=Time.time+3;}
    }
    public void ResetRound(bool countdown) {if(!Authority)return;Epoch++;BuildWorld(CurrentArena,CurrentMode,false);Remaining=120;PinCount=0;RoundMessage="";Phase=countdown&&!Practice?GamePhase.Countdown:GamePhase.Fighting;phaseUntil=Time.time+3;}
    public void ToggleFullscreen(){if(Screen.fullScreenMode==FullScreenMode.Windowed){windowWidth=Screen.width;windowHeight=Screen.height;var resolution=Screen.currentResolution;Screen.SetResolution(resolution.width,resolution.height,FullScreenMode.FullScreenWindow);}else Screen.SetResolution(windowWidth,windowHeight,FullScreenMode.Windowed);}
    public void ResetDrill(){if(!Practice||Network.role!=GBNetwork.Role.Offline)return;ResetRound(false);inputs[0]=inputs[1]=default;buffered[0]=buffered[1]=lastPoll[0]=lastPoll[1]=0;Toast("DRILL RESET",1.5f);}
    public void SetTrainingPartner(int behavior){TrainingPartner=Mathf.Clamp(behavior,0,2);bot=new GBBot();Toast("PARTNER: "+new[]{"PASSIVE","RESISTING","ATTACKING"}[TrainingPartner],2);}
    public int PadFor(int p){return Rumble&&LocalDevice==1&&p==(Network.role==GBNetwork.Role.Client?1:0)?0:-1;}
    GBInput PollInput(int p) {
        if(LocalDevice==1){var i=GBPad.Input(0,Vector3.zero);bool aiming=i.ax*i.ax+i.az*i.az>.02f;GBPad.Pad pad;if(GBPad.Read(0,out pad)){i.flags=0;foreach(var a in actions){int code=padCodes[a];if(code==0)continue;bool pressed=code==0x10000?pad.leftTrigger>90:code==0x20000?pad.rightTrigger>90:(pad.buttons&code)!=0;if(pressed)i.flags|=flags[a];}}else {LocalDevice=0;Toast("Controller disconnected • keyboard and mouse active",3);return PollInput(p);}i=CameraRelative(i,p);if(!aiming){i.ax=Wrestlers[p].facing.x;i.az=Wrestlers[p].facing.z;}return i;}
        var k=new GBInput();k.x=(KeyHeld(keys["Right"])?1:0)-(KeyHeld(keys["Left"])?1:0);k.z=(KeyHeld(keys["Forward"])?1:0)-(KeyHeld(keys["Back"])?1:0);k=CameraRelative(k,p);
        var view=Views.ForPlayer(p);if(view.pixelRect.Contains(MousePosition)){var ray=view.ScreenPointToRay(MousePosition);var plane=new Plane(Vector3.up,Views.FollowPoint(p));float distance;if(plane.Raycast(ray,out distance)){var aim=ray.GetPoint(distance)-Wrestlers[p].Root.position;k.ax=aim.x;k.az=aim.z;if(aim.x*aim.x+aim.z*aim.z>.01f){var normalized=new Vector2(k.ax,k.az).normalized;k.ax=normalized.x;k.az=normalized.y;}else{k.ax=Wrestlers[p].facing.x;k.az=Wrestlers[p].facing.z;}}}else{k.ax=Wrestlers[p].facing.x;k.az=Wrestlers[p].facing.z;}
        foreach(var a in actions)if(flags[a]!=0&&KeyHeld(keys[a]))k.flags|=flags[a];return k;
    }
    GBInput CameraRelative(GBInput i,int player=0){var view=Views.ForPlayer(player);Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized,right=Vector3.ProjectOnPlane(view.transform.right,Vector3.up).normalized;Vector3 move=forward*i.z+right*i.x,aim=forward*i.az+right*i.ax;i.x=move.x;i.z=move.z;if(aim.sqrMagnitude>.02f){i.ax=aim.x;i.az=aim.z;}return i;}
    void CaptureBinding(){if(capturePad){GBPad.Pad p;if(GBPad.Read(0,out p)){int code=p.leftTrigger>110?0x10000:p.rightTrigger>110?0x20000:p.buttons;if(code!=0){if(code<0x10000)code&=-code;padCodes[captureAction]=code;captureAction="";SavePrefs();}}}else foreach(KeyCode k in Enum.GetValues(typeof(KeyCode)))if(KeyDown(k)&&k!=KeyCode.Escape&&k!=KeyCode.F1&&k!=KeyCode.F11&&k!=KeyCode.T){keys[captureAction]=k;captureAction="";SavePrefs();break;}}
    void LateUpdate() {
        GBArt.UpdateText();
        foreach(var wrestler in Wrestlers)wrestler.Visuals();
        if(!cam||Wrestlers.Count<2)return;
        if(InMatch){Views.Tick(CameraShake?shake:0);shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*.4f);return;}
        Views.Menu();
        var a=Wrestlers[0].Root.position;var b=Wrestlers[1].Root.position;if(a.y<-2)a.y=0;if(b.y<-2)b.y=0;
        var center=(a+b)*.5f;center.y=Mathf.Max(.6f,center.y*.6f);center.x=Mathf.Clamp(center.x,-8,8);center.z=Mathf.Clamp(center.z,-9,9);
        float separation=Vector3.Distance(a,b);float distance=Mathf.Clamp(15+separation*.65f,17,29);
        if(!InMatch){center=CurrentArena==0?new Vector3(0,.5f,-2):Vector3.up;distance=24;center.x=-2;}
        Vector3 offset=new Vector3(.62f,.86f,-1).normalized*distance;Vector3 desired=center+offset;RaycastHit obstruction;
        if(Physics.SphereCast(center,.22f,offset.normalized,out obstruction,distance,1,QueryTriggerInteraction.Ignore)&&obstruction.distance>4)desired=center+offset.normalized*Mathf.Max(6,obstruction.distance-.4f);
        cam.transform.position=Vector3.SmoothDamp(cam.transform.position,desired,ref cameraVelocity,.24f,100,Time.unscaledDeltaTime);
        Vector3 look=center;if(CameraShake&&shake>0){look+=UnityEngine.Random.insideUnitSphere*shake;shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*.4f);}cam.transform.LookAt(look);
    }
    public void Shake(float amount){if(CameraShake)shake=Mathf.Max(shake,amount);}
    public void Toast(string message,float seconds=2){toast=message;toastUntil=Time.unscaledTime+seconds;}
    public void Issue(string message){if(!Issues.Contains(message))Issues.Add(message);Debug.LogWarning("GRIP_ISSUE "+message);}
    public void Burst(Vector3 at,Color color){var go=new GameObject("Impact dust");go.transform.position=at;var ps=go.AddComponent<ParticleSystem>();var main=ps.main;main.duration=.22f;main.loop=false;main.startLifetime=.35f;main.startSpeed=1.5f;main.startSize=.045f;main.startColor=color;main.maxParticles=16;var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,12)});var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.12f;var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.material=GBArt.Mat("Dust",new Color(.75f,.7f,.65f));Destroy(go,1);}
    void OnApplicationFocus(bool hasFocus){ApplyFocus(hasFocus);}
    public void ApplyFocus(bool hasFocus){
        focus=hasFocus;
        if(!hasFocus){inputs[0]=inputs[1]=default;lastPoll[0]=lastPoll[1]=buffered[0]=buffered[1]=0;
            if(InMatch&&Network.role==GBNetwork.Role.Offline&&!Paused){focusPaused=true;Paused=true;Time.timeScale=0;}}
        else if(focusPaused){focusPaused=false;Paused=false;Time.timeScale=1;}
    }
    void OnApplicationQuit(){SavePrefs();Network.Stop();GBPad.Rumble(0,0);GBPad.Rumble(1,0);}
    void Styles() {
        if(title!=null)return;
        title=new GUIStyle(GUI.skin.label){font=font,fontSize=78,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
        heading=new GUIStyle(title){fontSize=28};body=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true,normal={textColor=new Color(.79f,.84f,.88f)}};small=new GUIStyle(body){fontSize=14};label=new GUIStyle(body){fontSize=16};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(22,10,5,5)};button.normal.background=pixel;button.hover.background=pixel;button.active.background=pixel;button.normal.textColor=Color.white;button.hover.textColor=Color.white;button.active.textColor=Color.white;
        GUI.skin.textField.font=font;GUI.skin.textField.fontSize=18;GUI.skin.toggle.font=font;GUI.skin.toggle.fontSize=18;
    }
    void Panel(Rect r,Color color){var c=GUI.color;GUI.color=color;GUI.DrawTexture(r,pixel);GUI.color=c;}
    bool Button(Rect r,string text,bool primary=false){Color old=GUI.backgroundColor;GUI.backgroundColor=primary?new Color(.95f,.3f,.08f):new Color(.15f,.2f,.27f);var style=r.width<140?new GUIStyle(button){fontSize=14,padding=new RectOffset(8,5,5,5)}:button;bool clicked=GUI.Button(r,text,style);GUI.backgroundColor=old;return clicked;}
    void Label(Rect r,string text,GUIStyle style=null){GUI.Label(r,text,style??body);}
    void Bar(Rect r,float amount,Color color){Panel(r,new Color(.02f,.04f,.065f,.9f));Panel(new Rect(r.x,r.y,r.width*Mathf.Clamp01(amount),r.height),color);}
    void OnGUI() {
        Styles();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
        if(InMatch){HUD();if(ControlsVisible)ControlsPanel();else if(Paused)PauseMenu();else if(Phase==GamePhase.MatchOver)Results();}
        else {Panel(new Rect(0,0,670,900),new Color(.025f,.04f,.065f,.96f));Panel(new Rect(670,0,4,900),new Color(.98f,.3f,.06f,.75f));Label(new Rect(56,38,580,105),"GRIP BREAK",title);Label(new Rect(61,143,560,32),"GET A HOLD. START SOMETHING.",body);
            if(screen=="home")Home();else if(screen=="online")Online();else if(screen=="how")How();else if(screen=="options")Options();else if(screen=="keybinds")Keybinds();
            Label(new Rect(61,843,520,40),"AI LOCAL   /   PRIVATE ONLINE   /   TRAINING",small);
            Label(new Rect(1030,788,530,42),arenaNames[CurrentArena],heading);Label(new Rect(1030,830,530,40),arenaDetails[CurrentArena],small);
        }
        if(Time.unscaledTime<toastUntil){Panel(new Rect(420,800,760,45),new Color(.04f,.07f,.1f,.94f));var t=new GUIStyle(body){alignment=TextAnchor.MiddleCenter};Label(new Rect(430,810,740,30),toast,t);}
    }
    void Home() {
        Label(new Rect(61,211,530,35),"CHOOSE YOUR FLOOR",small);
        for(int i=0;i<3;i++){if(Button(new Rect(61,248+i*56,535,48),(SelectedArena==i?"●  ":"   ")+arenaNames[i])){SelectedArena=i;BuildWorld(i,0,false);}}
        if(Button(new Rect(61,430,535,43),"YOUR INPUT: "+(LocalDevice==1?"CONTROLLER":"KEYBOARD + MOUSE")+"   ›"))LocalDevice=LocalDevice==1?0:1;
        if(Button(new Rect(61,483,260,40),"RULES: "+(SelectedMode==1?"PIN":"RING-OUT")+"   ›"))SelectedMode=SelectedMode==1?0:1;
        if(Button(new Rect(337,483,260,40),"AI: "+new[]{"EASY","NORMAL","HARD"}[AIDifficulty]+"   ›")){AIDifficulty=(AIDifficulty+1)%3;SavePrefs();}
        if(Button(new Rect(61,539,535,57),"AI LOCAL   →",true)){Network.Stop();BeginMatch(SelectedArena,SelectedMode==1?1:0);}
        if(Button(new Rect(61,610,260,54),"PRIVATE ONLINE"))screen="online";
        if(Button(new Rect(336,610,260,54),"TRAINING")){Network.Stop();BeginMatch(3,2);}
        if(Button(new Rect(61,686,170,48),"HOW TO PLAY"))screen="how";
        if(Button(new Rect(426,686,170,48),"OPTIONS"))screen="options";
        if(Button(new Rect(244,686,170,48),"KEYBINDS"))screen="keybinds";
        bool oldEnabled=GUI.enabled;GUI.enabled=!Updater.Busy;if(Button(new Rect(61,752,350,43),Updater.HasUpdate?"UPDATE & RESTART":"CHECK FOR UPDATE",Updater.HasUpdate))Updater.CheckOrInstall();GUI.enabled=oldEnabled;
        if(Button(new Rect(426,752,170,43),"QUIT"))Application.Quit();Label(new Rect(61,799,535,36),Updater.Status+" • v"+Application.version,small);
    }
    void Online() {
        Label(new Rect(61,206,540,35),"PRIVATE MATCH",heading);Label(new Rect(61,250,540,50),"Host, copy your invite, and send it to your friend. They paste it here to join.",body);
        Label(new Rect(61,319,130,30),"Your name",small);PlayerName=GUI.TextField(new Rect(210,319,387,35),PlayerName,24);
        Label(new Rect(61,373,535,30),Network.role==GBNetwork.Role.Host?"YOUR INVITE LINK":"PASTE YOUR FRIEND'S INVITE",small);
        if(Network.role==GBNetwork.Role.Host){GUI.TextField(new Rect(61,411,535,45),Network.InviteLink,512);bool enabled=GUI.enabled;GUI.enabled=Network.InviteReady;if(Button(new Rect(61,467,535,40),"COPY INVITE"))CopyInvite();GUI.enabled=enabled;}
        else {invite=GUI.TextField(new Rect(61,411,535,45),invite,512);if(Button(new Rect(61,467,535,40),"PASTE INVITE"))invite=GUIUtility.systemCopyBuffer.Trim();}
        if(Network.role==GBNetwork.Role.Offline){if(Button(new Rect(61,520,260,55),"HOST & MAKE LINK",true)){Network.HostInvite();SavePrefs();}
            if(Button(new Rect(337,520,260,55),"JOIN FRIEND",true)){if(Network.JoinInvite(invite.Trim()))SavePrefs();else Toast("Paste the full invite copied from your friend's game",3);}}
        else {
            if(Button(new Rect(61,520,260,55),Network.ready?"✓ READY":"READY UP",true)){Network.SetReady(!Network.ready);Network.Lobby();}
            if(Network.role==GBNetwork.Role.Host&&Button(new Rect(337,520,260,55),"START MATCH")){if(Network.connected&&Network.ready&&Network.peerReady)Network.StartMatch();else Toast("Both players need to be ready",3);}
            if(Button(new Rect(61,588,536,34),"MODE: "+new[]{"RING-OUT","PIN WRESTLING","FREE PRACTICE"}[SelectedMode]+"   ›")&&Network.role==GBNetwork.Role.Host){SelectedMode=(SelectedMode+1)%3;Network.Lobby();}
            if(Button(new Rect(61,626,536,30),"ARENA: "+arenaNames[SelectedMode==0?SelectedArena%3:3]+"   ›")&&Network.role==GBNetwork.Role.Host){SelectedArena=(SelectedArena+1)%3;Network.Lobby();}
        }
        Label(new Rect(61,666,535,65),Network.status+(Network.connected?"\n"+Network.peerName+": "+(Network.peerReady?"READY":"NOT READY"):""),body);
        if(Button(new Rect(61,742,260,48),"BACK")){Network.Stop();ReturnToMenu();}
        if(Button(new Rect(337,742,260,48),"INPUT: "+(LocalDevice==1?"PAD":"KEYBOARD")))LocalDevice=LocalDevice==1?0:1;
        Label(new Rect(61,801,535,42),"Keep this room open. Closing it ends the temporary invite.",small);
    }
    public bool CopyInvite(){if(!Network.InviteReady)return false;GUIUtility.systemCopyBuffer=Network.InviteLink;Toast("INVITE COPIED • send it to your friend",2);return true;}
    void How() {
        Label(new Rect(61,206,540,35),"WRESTLE WITH THE WORLD",heading);
        Label(new Rect(61,263,535,160),"Hold each hand separately to grab. Walk backward to drag. Hold LIFT to raise your opponent, then THROW to release them in your aiming direction.\n\nBRACE to resist and twist out. Catch a ledge with a hand, then JUMP to pull up.",body);
        Label(new Rect(61,449,535,210),"DEFAULT KEYBOARD: WASD + mouse aim\nHands: mouse buttons • Lift: E • Throw: F\nJump / climb: Space • Sprint: Shift\nShot: Q • Shove: X • Brace / escape: Ctrl\nTackle: R • Legs / pin: C\n\nGAMEPAD: sticks move / aim, LB / RB hands\nRT lift • Y throw • A jump • X shot\nLT brace • B tackle • L3 sprint\nD-pad down legs / pin • D-pad up shove",body);
        Label(new Rect(61,670,535,56),"Choose Easy, Normal or Hard AI. Training partners can be passive, resisting or attacking. Your camera follows you.",small);
        if(Button(new Rect(61,742,260,48),"BACK"))screen="home";if(Button(new Rect(337,742,260,48),"TRY THE GYM",true)){Network.Stop();BeginMatch(3,2);}
    }
    void Options() {
        Label(new Rect(61,206,540,35),"MAKE IT YOURS",heading);Label(new Rect(61,254,210,30),"Master volume",body);Sfx.volume=GUI.HorizontalSlider(new Rect(270,264,290,20),Sfx.volume,0,1);
        CameraShake=GUI.Toggle(new Rect(61,301,220,30),CameraShake," Camera shake");Rumble=GUI.Toggle(new Rect(316,301,220,30),Rumble," Controller rumble");
        if(Button(new Rect(61,349,535,49),"KEYBINDS →"))screen="keybinds";
        Label(new Rect(61,422,535,68),"F11 toggles fullscreen. You can change every wrestling action on the Keybinds page.",body);
        if(Button(new Rect(61,742,535,48),"SAVE & BACK")){SavePrefs();screen="home";}
    }
    public void BeginRebind(string action,bool controller){if(!keys.ContainsKey(action))return;capturePad=controller;captureAction=action;captureStart=Time.unscaledTime;}
    public void ResetBindings(){Defaults();captureAction="";SavePrefs();}
    void Keybinds(){
        Label(new Rect(61,206,535,36),"KEYBINDS",heading);
        Label(new Rect(61,251,535,54),"Select an action, then press its new key or button. Escape cancels. Changes save immediately.",body);
        if(Button(new Rect(61,319,535,45),capturePad?"DEVICE: CONTROLLER   ›":"DEVICE: KEYBOARD + MOUSE   ›")){capturePad=!capturePad;captureAction="";}
        for(int n=0;n<actions.Length;n++){int col=n/8,row=n%8;string a=actions[n];if(capturePad&&n>=11)continue;Label(new Rect(61+col*276,391+row*39,160,32),a=="Strike"?"Wrestling shot":a=="Pin"?"Legs / pin":a,small);string text=captureAction==a?"PRESS…":capturePad?PadName(padCodes[a]):KeyName(keys[a]);if(Button(new Rect(221+col*276,386+row*39,110,35),text))BeginRebind(a,capturePad);}
        Label(new Rect(61,709,535,29),"Fixed: F1 controls • Esc pause • F11 fullscreen • T drill reset",small);
        if(Button(new Rect(61,752,260,48),"BACK")){captureAction="";SavePrefs();screen="home";}
        if(Button(new Rect(337,752,260,48),"RESET BINDINGS"))ResetBindings();
    }
    static string PadName(int code){switch(code){case 0x100:return "LB";case 0x200:return "RB";case 0x40:return "L3";case 0x1000:return "A";case 0x2000:return "B";case 0x4000:return "X";case 0x8000:return "Y";case 0x10000:return "LT";case 0x20000:return "RT";case 2:return "D↓";case 1:return "D↑";case 4:return "D←";case 8:return "D→";default:return "0x"+code.ToString("X");}}
    void HUD() {
        Panel(new Rect(0,0,1600,112),new Color(.025f,.04f,.06f,.88f));
        for(int p=0;p<2;p++){var w=Wrestlers[p];float x=p==0?42:1120;Label(new Rect(x,12,405,32),w.playerName,heading);Bar(new Rect(x,49,195,10),w.stamina/100,w.color);Bar(new Rect(x+205,49,195,10),w.balance/100,new Color(.35f,.8f,.55f));Label(new Rect(x,61,400,22),"STAMINA                 BALANCE "+Mathf.CeilToInt(w.balance),small);Label(new Rect(x,84,400,24),"L: "+w.hands[0].Label+"   /   R: "+w.hands[1].Label,small);}
        var centered=new GUIStyle(heading){alignment=TextAnchor.MiddleCenter};Label(new Rect(560,15,480,46),Practice?"FREE PRACTICE":Scores[0]+"   :   "+Scores[1],centered);
        Label(new Rect(560,67,480,32),Practice?"T • RESET DRILL":CurrentMode==1?"PIN / THREE COUNT":Arena.sudden?"SUDDEN DEATH":Mathf.CeilToInt(Mathf.Max(0,Remaining)).ToString(),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
        Label(new Rect(42,185,900,30),"YOUR WRESTLER: "+(Views.PrimaryPlayer==0?"ORANGE":"BLUE")+" • CAMERA LOCKED TO YOU",small);
        var controlled=Wrestlers[Views.PrimaryPlayer];
        Panel(new Rect(30,715,485,88),new Color(.025f,.04f,.065f,.84f));Label(new Rect(43,726,460,23),"TARGET: "+controlled.TargetMode+"   •   GRIP STRAIN "+Mathf.RoundToInt(controlled.GripStrain*100)+"%",small);Bar(new Rect(43,753,460,7),controlled.GripStrain,new Color(1,.55f,.18f));Label(new Rect(43,768,460,30),controlled.ActionFeedback,small);
        if(Practice&&Network.role==GBNetwork.Role.Offline){if(Button(new Rect(1050,837,325,47),"PARTNER: "+new[]{"PASSIVE","RESISTING","ATTACKING"}[TrainingPartner]+"   ›"))SetTrainingPartner((TrainingPartner+1)%3);if(Button(new Rect(1385,837,185,47),"RESET DRILL"))ResetDrill();}
        if(Button(new Rect(660,846,280,44),"F1 • CONTROLS"))ControlsVisible=!ControlsVisible;
        if(Phase==GamePhase.Countdown){Label(new Rect(550,355,500,125),Mathf.Max(1,Mathf.CeilToInt(phaseUntil-Time.time)).ToString(),new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=110});}
        if(Phase==GamePhase.RoundOver){Panel(new Rect(350,365,900,100),new Color(.03f,.05f,.08f,.92f));Label(new Rect(365,390,870,60),RoundMessage,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter});}
        if(PinCount>0){Label(new Rect(650,220,300,130),Mathf.Min(3,1+Mathf.FloorToInt(PinCount)).ToString(),new GUIStyle(title){alignment=TextAnchor.MiddleCenter});Bar(new Rect(590,348,420,10),PinCount/3,new Color(1,.4f,.1f));}
        if(Arena.kind==0){float z=Arena.sweepers.Count>0?Arena.sweepers[0].position.z:20;if(z>Wrestlers[0].Root.position.z+2&&z<Wrestlers[0].Root.position.z+9)Label(new Rect(570,134,470,40),"LOW BEAM • BRACE & DUCK",new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.52f,.15f)}});}
        if(Arena.sudden){float limit=Mathf.Lerp(CurrentArena==1?9.5f:6.8f,CurrentArena==1?1.5f:1.2f,Arena.suddenTime/30);Label(new Rect(565,150,480,35),CurrentArena==0?"BEAMS LOWERING • BRACE":"SAFE WIDTH: "+(limit*2).ToString("0.0")+" m",new GUIStyle(body){alignment=TextAnchor.MiddleCenter});}
        if(Practice){Panel(new Rect(310,130,980,43),new Color(.035f,.07f,.11f,.85f));Label(new Rect(325,141,950,26),(lessonStep<6?(lessonStep+1)+" / 6   ":"")+lessons[lessonStep],new GUIStyle(small){alignment=TextAnchor.MiddleCenter});}
    }
    public string ControlSummary(int player){
        bool pad=LocalDevice==1;
        if(pad)return "MOVE  Left stick\nAIM  Right stick\nLEFT / RIGHT HAND  "+PadName(padCodes["Left hand"])+" / "+PadName(padCodes["Right hand"])+" (hold)\nLIFT  "+PadName(padCodes["Lift"])+" (hold)\nTHROW  "+PadName(padCodes["Throw"])+"\nJUMP / CLIMB  "+PadName(padCodes["Jump / climb"])+"\nSPRINT  "+PadName(padCodes["Sprint"])+"\nWRESTLING SHOT  "+PadName(padCodes["Strike"])+"\nBRACE / ESCAPE  "+PadName(padCodes["Brace / escape"])+" (hold)\nTACKLE  "+PadName(padCodes["Tackle"])+"\nLEGS / PIN  "+PadName(padCodes["Pin"])+" (hold)\nSHOVE  "+PadName(padCodes["Shove"]);
        string text="MOVE  "+KeyName(keys["Forward"])+" "+KeyName(keys["Left"])+" "+KeyName(keys["Back"])+" "+KeyName(keys["Right"])+"\nAIM  Mouse in your view";
        foreach(var a in actions){if(flags[a]==0)continue;text+="\n"+(a=="Strike"?"WRESTLING SHOT":a=="Pin"?"LEGS / PIN":a.ToUpper())+"  "+KeyName(keys[a])+((a=="Left hand"||a=="Right hand"||a=="Lift"||a=="Brace / escape"||a=="Pin")?" (hold)":"");}return text;
    }
    static string KeyName(KeyCode k){switch(k){case KeyCode.Mouse0:return "Left mouse";case KeyCode.Mouse1:return "Right mouse";case KeyCode.LeftControl:return "Left Ctrl";case KeyCode.RightControl:return "Right Ctrl";case KeyCode.LeftShift:return "Left Shift";case KeyCode.RightShift:return "Right Shift";case KeyCode.Return:return "Enter";default:return k.ToString();}}
    public void ControlsPanel(){
        Panel(new Rect(155,222,1290,601),new Color(.025f,.045f,.07f,.98f));Label(new Rect(195,245,950,45),"YOUR CONTROLS",heading);
        int mine=Network.role==GBNetwork.Role.Client?1:0;
        var text=new GUIStyle(body){fontSize=20};
        Label(new Rect(195,310,560,35),mine==0?"YOU • ORANGE":"YOU • BLUE",heading);
        Label(new Rect(195,354,565,363),ControlSummary(mine),text);
        Label(new Rect(845,354,550,335),"Hold hands to reach for the upper body.\nHold LEGS / PIN to target legs.\nIn pin matches, that action pins.\nMove back to pull; sideways to pivot.\nSHOT: forward entry, no auto-grab.\nSHOVE: push away a held rival.\nLift, aim, throw • Lift + brace to slam.\nBrace + move to struggle out of holds.\nTraining: T or RESET DRILL starts over.",text);
        Label(new Rect(195,735,820,57),"F1 shows/hides controls • Esc opens the pause menu • F11 toggles fullscreen\nYour camera follows your wrestler in every mode.",small);
        if(Button(new Rect(1090,742,310,50),"CLOSE • F1",true))ControlsVisible=false;
    }
    void PauseMenu(){Panel(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.7f));Panel(new Rect(535,215,530,470),new Color(.04f,.065f,.095f,.98f));Label(new Rect(577,250,460,50),Network.role==GBNetwork.Role.Offline?"TAKE A BREATHER":"MENU • MATCH CONTINUES",heading);if(Button(new Rect(578,330,444,57),"KEEP WRESTLING",true)){Paused=false;Time.timeScale=1;}if(Authority&&Button(new Rect(578,404,444,53),"RESET ROUND")){ResetRound(false);Paused=false;Time.timeScale=1;}if(Button(new Rect(578,476,444,53),"RETURN TO LOBBY")){Paused=false;Time.timeScale=1;ReturnToMenu();screen=Network.role==GBNetwork.Role.Offline?"home":"online";if(Network.role==GBNetwork.Role.Client){Network.Stop();ReturnToMenu();}}if(Button(new Rect(578,548,444,53),"QUIT"))Application.Quit();if(Button(new Rect(578,620,444,45),"CONTROLS • F1"))ControlsVisible=true;}
    void Results(){Panel(new Rect(340,260,920,370),new Color(.035f,.055f,.085f,.96f));Label(new Rect(380,300,850,60),RoundMessage,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter});Label(new Rect(550,382,500,65),Scores[0]+" : "+Scores[1],new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=54});if(Authority&&Button(new Rect(400,501,385,58),"REMATCH",true))BeginMatch(CurrentArena,CurrentMode);if(Button(new Rect(815,501,385,58),"LOBBY")){ReturnToMenu();screen=Network.role==GBNetwork.Role.Offline?"home":"online";if(Network.role==GBNetwork.Role.Client){Network.Stop();ReturnToMenu();}}}
    public void Capture(string path,bool primaryOnly=false) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));foreach(var wrestler in Wrestlers)wrestler.Visuals();var rt=new RenderTexture(1600,900,24);var rect=cam.rect;if(primaryOnly)cam.rect=new Rect(0,0,1,1);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=null;cam.rect=rect;RenderTexture.active=null;Destroy(rt);Destroy(tex);
    }
    public void CaptureAt(string path,Vector3 position,Vector3 look){var p=cam.transform.position;var q=cam.transform.rotation;cam.transform.position=position;cam.transform.LookAt(look);Capture(path,true);cam.transform.position=p;cam.transform.rotation=q;}
}
