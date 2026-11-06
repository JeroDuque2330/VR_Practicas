using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Quest APK with EscenaPrincipal.
// Menu: VR > Build Quest APK, or from the command line:
//   Unity.exe -batchmode -quit -projectPath <proyecto> -buildTarget Android -executeMethod BuildQuest.Build
public static class BuildQuest
{
    const string ScenePath = "Assets/_Main/Scenes/EscenaPrincipal.unity";
    const string OutputPath = "Builds/VR_Practicas.apk";

    [MenuItem("VR/Build Quest APK")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildQuest] {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime})");

        if (Application.isBatchMode && s.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
