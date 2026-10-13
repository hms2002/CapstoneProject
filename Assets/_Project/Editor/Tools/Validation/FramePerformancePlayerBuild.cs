#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FramePerformancePlayerBuild : IProcessSceneWithReport
{
    public int callbackOrder => 10000;
    static bool measuringBuild;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!measuringBuild || scene.name!="TestScene") return;
        foreach(var root in scene.GetRootGameObjects())
        {
            var panel=root.GetComponentInChildren<TestSceneControlPanel>(true);
            if(panel!=null){panel.gameObject.AddComponent<FramePerformancePlayerProbe>();return;}
        }
        throw new InvalidOperationException("Benchmark build scene has no test controller.");
    }
    public static void Build()
    {
        var args=Environment.GetCommandLineArgs(); string output=null; bool scriptsOnly=false;
        for(int i=0;i+1<args.Length;i++)
        {
            if(args[i]=="-framePerfBuildOutput")output=args[i+1];
            if(args[i]=="-framePerfScriptsOnly")scriptsOnly=args[i+1]=="true";
        }
        if(string.IsNullOrEmpty(output))throw new ArgumentException("-framePerfBuildOutput is required.");
        output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output));
        if(scriptsOnly && !File.Exists(output))throw new InvalidOperationException("Scripts-only measurement rebuild requires an existing complete player.");
        try
        {
            measuringBuild=true;
            BuildReport result=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{"Assets/_Project/Scenes/Tests/TestScene.unity"},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development | (scriptsOnly ? BuildOptions.BuildScriptsOnly : BuildOptions.None)
            });
            if(result==null || result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Measurement player build failed; see the build log.");
        }
        finally{measuringBuild=false;}
    }
}
#endif
