using System;
using System.IO;
using UnityEngine;

// Automated checks only: render the normal camera at full requested quality
// into an offscreen surface so testing never needs to take keyboard focus.
[DefaultExecutionOrder(10000)]
public sealed class GBBackgroundRender:MonoBehaviour {
    [Serializable] public class Proof {public int width,height,antiAliasing,cameraFrames;public bool surfaceCreated;public string scope="Normal full-scene camera rendered offscreen; no forced focus, no geometry/lighting/physics changes.";}
    Camera camera;RenderTexture surface;string directory;int cameraFrames;bool saved,wasEnabled;
    void OnEnable(){
        camera=GBApp.I.Views.Primary;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--evidence");directory=i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Application.dataPath,"..","Evidence");Directory.CreateDirectory(directory);
        surface=new RenderTexture(Screen.width,Screen.height,24,camera.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.ARGB32){name="Background native performance check",antiAliasing=Mathf.Max(1,QualitySettings.antiAliasing)};
        surface.Create();camera.targetTexture=surface;wasEnabled=camera.enabled;camera.enabled=false;Camera.onPostRender+=Rendered;
    }
    void LateUpdate(){if(camera&&surface){camera.targetTexture=surface;camera.Render();}}
    void Rendered(Camera rendered){if(rendered==camera&&rendered.targetTexture==surface)cameraFrames++;}
    void Save(){
        if(saved||!surface)return;saved=true;
        File.WriteAllText(Path.Combine(directory,"background-render-proof.json"),JsonUtility.ToJson(new Proof{width=surface.width,height=surface.height,antiAliasing=surface.antiAliasing,cameraFrames=cameraFrames,surfaceCreated=surface.IsCreated()},true));
        // Capture only after the measured window has ended.
        if(surface.IsCreated()){var previous=RenderTexture.active;RenderTexture.active=surface;var image=new Texture2D(surface.width,surface.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,surface.width,surface.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,"background-final-frame.png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=previous;}
        Camera.onPostRender-=Rendered;if(camera){camera.targetTexture=null;camera.enabled=wasEnabled;}surface.Release();Destroy(surface);
    }
    void OnApplicationQuit(){Save();}
    void OnDisable(){Save();}
}
