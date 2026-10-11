using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public static class BuildGripBreak {
    public static void Build() {
        PlayerSettings.companyName = "Grip Break";
        PlayerSettings.productName = "GRIP BREAK";
        PlayerSettings.bundleVersion = "0.2.0";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 55;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
        var seen=new System.Collections.Generic.HashSet<Shader>();
        for(int i=shaders.arraySize-1;i>=0;i--) {
            var shader=shaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if(!shader||shader.name=="GUI/Text Shader"||!seen.Add(shader)){shaders.GetArrayElementAtIndex(i).objectReferenceValue=null;shaders.DeleteArrayElementAtIndex(i);}
        }
        foreach(string name in new[]{"Standard", "Skybox/Procedural", "GripBreak/WorldText"}) {
            var shader=Shader.Find(name);if(shader&&seen.Add(shader)){int index=shaders.arraySize;shaders.InsertArrayElementAtIndex(index);shaders.GetArrayElementAtIndex(index).objectReferenceValue=shader;}
        }
        graphics.ApplyModifiedPropertiesWithoutUndo();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("GRIP BREAK").AddComponent<GBApp>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GripBreak.unity");
        string root = Directory.GetParent(Application.dataPath).Parent.FullName;
        string exe = Path.Combine(root, "Build", "GRIP_BREAK.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe));
        var report = BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/GripBreak.unity"}, exe, BuildTarget.StandaloneWindows64, BuildOptions.None);
        File.WriteAllText(Path.Combine(root,"build-status.txt"), report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
        Debug.Log("GRIP_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
