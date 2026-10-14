using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

// The same fixture is copied into the verified old source and current source.
// Inputs use time since this fixture began, never the app's startup clock.
public partial class GBAudit {
    [Serializable] class PerformanceMetrics {
        public string build,engine,processor,gpu,graphicsPreset,inputSignature;
        public int width,height,targetFrameRate,vSyncCount,antiAliasing,bodies,wrestlerBodies,solverIterations,solverVelocityIterations,frames,framesWithGrip,framesWithBothWrestlersGripping,maximumHeldHands,framesFocused,startEpoch,endEpoch;
        public float fixedStep,shadowDistance,warmupSeconds,measurementSeconds,averageFps,minFps,p50FrameMs,p95FrameMs;
        public float travelledPlayer0,travelledPlayer1,maximumDisplacementPlayer0,maximumDisplacementPlayer1;
        public float[] frameMilliseconds;
        public bool capturedDuringMeasurement,allBodiesPhysical,settingsUnchanged;
    }
    void PerformanceInputs(float clock){
        for(int p=0;p<2;p++){
            var a=app.Wrestlers[p];var b=app.Wrestlers[1-p];
            Vector3 toward=Vector3.ProjectOnPlane(b.Root.position-a.Root.position,Vector3.up);
            float distance=toward.magnitude;Vector3 aim=distance>.001f?toward/distance:(p==0?Vector3.right:Vector3.left);
            Vector3 side=Vector3.Cross(Vector3.up,aim);
            float approach=distance>.90f?.35f:distance<.52f?-.12f:0;
            Vector3 movement=aim*approach+side*(Mathf.Sin(clock*.65f+(p==0?0:Mathf.PI))*.12f);
            Vector3 contact=b.bodies[1].GetComponent<Collider>().ClosestPoint(a.bodies[1].position);
            Vector3 hand=Vector3.ClampMagnitude(contact-a.Root.position,1.35f);
            int flags=GBInput.HandAim;
            if(clock%6f<5.55f){if(!a.hands[0].releaseRequired)flags|=GBInput.Left;if(!a.hands[1].releaseRequired)flags|=GBInput.Right;}
            app.ScriptedInputs[p]=new GBInput{x=movement.x,z=movement.z,ax=aim.x,az=aim.z,hx=hand.x,hy=hand.y,hz=hand.z,flags=flags};
        }
    }
    IEnumerator PerformanceRun(){
        app.LocalDevice=0;app.TrainingPartner=0;app.ScriptedInputs=new GBInput[2];app.SetGraphicsQuality(0);app.BeginMatch(3,1);
        Neutral();yield return Wait(3.3f);
        app.Phase=GBApp.GamePhase.Fighting;app.SetPhaseSeconds(300);
        var a=app.Wrestlers[0];var b=app.Wrestlers[1];
        a.ResetTo(new Vector3(-.60f,.25f,0));b.ResetTo(new Vector3(.60f,.25f,0));
        const float warmup=6,seconds=30;
        double fixtureStart=Time.realtimeSinceStartupAsDouble;
        while(Time.realtimeSinceStartupAsDouble-fixtureStart<warmup){PerformanceInputs((float)(Time.realtimeSinceStartupAsDouble-fixtureStart));yield return null;}
        var metrics=new PerformanceMetrics{build=Application.version,engine=Application.unityVersion,processor=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,graphicsPreset=app.GraphicsQualityName,width=Screen.width,height=Screen.height,targetFrameRate=Application.targetFrameRate,vSyncCount=QualitySettings.vSyncCount,antiAliasing=QualitySettings.antiAliasing,shadowDistance=QualitySettings.shadowDistance,bodies=app.Registry.Count,wrestlerBodies=a.bodies.Length+b.bodies.Length,solverIterations=a.Root.solverIterations,solverVelocityIterations=a.Root.solverVelocityIterations,fixedStep=Time.fixedDeltaTime,warmupSeconds=warmup,startEpoch=app.Epoch,inputSignature="body-surface manual aim; available L/R grips5.55s of6s; approach0.35 above0.90m; retreat0.12 below0.52m; tangential0.12*sin(relativeClock*0.65+player*pi); no shots/lift/pin/scramble;6s warmup+30s measured",capturedDuringMeasurement=false,allBodiesPhysical=true};
        Vector3 firstA=a.Root.position,firstB=b.Root.position,lastA=firstA,lastB=firstB;
        var samples=new List<float>(4096);double windowStart=Time.realtimeSinceStartupAsDouble,last=windowStart,end=windowStart+seconds;
        while(last<end){
            PerformanceInputs((float)(last-fixtureStart));
            yield return null;
            double now=Time.realtimeSinceStartupAsDouble;
            samples.Add((float)((now-last)*1000));last=now;
            metrics.travelledPlayer0+=Vector3.Distance(lastA,a.Root.position);metrics.travelledPlayer1+=Vector3.Distance(lastB,b.Root.position);
            lastA=a.Root.position;lastB=b.Root.position;
            metrics.maximumDisplacementPlayer0=Mathf.Max(metrics.maximumDisplacementPlayer0,Vector3.Distance(firstA,lastA));metrics.maximumDisplacementPlayer1=Mathf.Max(metrics.maximumDisplacementPlayer1,Vector3.Distance(firstB,lastB));
            int grips=a.GripCount+b.GripCount;
            if(grips>0)metrics.framesWithGrip++;
            if(a.GripCount>0&&b.GripCount>0)metrics.framesWithBothWrestlersGripping++;
            metrics.maximumHeldHands=Mathf.Max(metrics.maximumHeldHands,grips);
            if(Application.isFocused)metrics.framesFocused++;
            foreach(var w in app.Wrestlers)foreach(var rb in w.bodies)metrics.allBodiesPhysical&=!rb.isKinematic;
        }
        Neutral();metrics.frames=samples.Count;metrics.measurementSeconds=(float)(last-windowStart);metrics.averageFps=metrics.frames/metrics.measurementSeconds;metrics.frameMilliseconds=samples.ToArray();metrics.endEpoch=app.Epoch;
        samples.Sort();metrics.p50FrameMs=samples[Mathf.Clamp((int)Math.Ceiling(samples.Count*.50)-1,0,samples.Count-1)];metrics.p95FrameMs=samples[Mathf.Clamp((int)Math.Ceiling(samples.Count*.95)-1,0,samples.Count-1)];metrics.minFps=1000/samples[samples.Count-1];
        metrics.settingsUnchanged=app.GraphicsQualityName==metrics.graphicsPreset&&Screen.width==metrics.width&&Screen.height==metrics.height&&Time.fixedDeltaTime==metrics.fixedStep&&Application.targetFrameRate==metrics.targetFrameRate&&QualitySettings.vSyncCount==metrics.vSyncCount&&QualitySettings.antiAliasing==metrics.antiAliasing&&QualitySettings.shadowDistance==metrics.shadowDistance&&app.Registry.Count==metrics.bodies&&a.Root.solverIterations==metrics.solverIterations&&a.Root.solverVelocityIterations==metrics.solverVelocityIterations;
        File.WriteAllText(Path.Combine(directory,"performance-metrics.json"),JsonUtility.ToJson(metrics,true));
        Check("performance_fixed_high_1080p_settings",metrics.width==1920&&metrics.height==1080&&metrics.graphicsPreset=="HIGH"&&metrics.targetFrameRate==120&&metrics.vSyncCount==0&&metrics.antiAliasing==4&&Mathf.Abs(metrics.fixedStep-1f/240)<.000001f&&metrics.solverIterations==64&&metrics.solverVelocityIterations==24&&metrics.bodies==92&&metrics.wrestlerBodies==30&&metrics.allBodiesPhysical&&metrics.settingsUnchanged,"same native High1080p;120fps cap;240Hz;64/24 wrestler solver;92 registered bodies;30 active wrestler bodies");
        Check("performance_real_movement_and_manual_reach",metrics.maximumDisplacementPlayer0>.20f&&metrics.maximumDisplacementPlayer1>.20f,"actual maximum displacement="+metrics.maximumDisplacementPlayer0+"/"+metrics.maximumDisplacementPlayer1+"m; held frames="+metrics.framesWithGrip+"/"+metrics.frames+"; maximum held hands="+metrics.maximumHeldHands+"; this measures moving/reaching and records any earned grips; no grapple benchmark claim");
        Check("performance_full_window_without_capture",metrics.measurementSeconds>=seconds&&metrics.frames>30&&!metrics.capturedDuringMeasurement&&metrics.startEpoch==metrics.endEpoch,"seconds="+metrics.measurementSeconds+"; frames="+metrics.frames+"; avgFPS="+metrics.averageFps+";p50/p95ms="+metrics.p50FrameMs+"/"+metrics.p95FrameMs+"; no screenshots or round resets during window");
        Check("performance_runtime_clean",app.Issues.Count==0,string.Join("; ",app.Issues));
    }
}
