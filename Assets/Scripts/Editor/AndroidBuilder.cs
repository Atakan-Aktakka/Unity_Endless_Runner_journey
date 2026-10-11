using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds a debug-signed Android APK to Builds/EndlessRunner.apk.
// Editor:  menu Build > Android APK
// CLI:     Unity.exe -batchmode -quit -projectPath <path> -executeMethod AndroidBuilder.BuildApk -logFile build.log
public static class AndroidBuilder
{
    private const string OutputPath = "Builds/EndlessRunner.apk";
    private const string ReportPath = "Builds/build_result.txt";

    [MenuItem("Build/Android APK")]
    public static void BuildApk()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0 || !scenes[0].EndsWith("ScoreMenuScene.unity"))
        {
            Debug.LogError("AndroidBuilder: ScoreMenuScene must be scene 0 in the Build Settings.");
            WriteResult("Failed: wrong scene order");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        EditorUserBuildSettings.buildAppBundle = false; // .apk, not .aab

        // Use the project's keystore when its passwords are available in this session,
        // otherwise sign this build with the Android debug keystore and restore the setting afterwards
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        bool keystoreReady = customKeystore
            && !string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)
            && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass);
        if (customKeystore && !keystoreReady)
        {
            PlayerSettings.Android.useCustomKeystore = false;
        }
        string signing = keystoreReady ? "release keystore" : "debug keystore";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            // The build saves ProjectSettings with the temporary value; write the original back to disk
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            AssetDatabase.SaveAssets();
        }

        BuildSummary summary = report.summary;
        string message = string.Format("{0}: {1} ({2:0.0} MB, {3} errors, {4} warnings, {5:0}s, signed with {6})",
            summary.result, OutputPath, summary.totalSize / (1024f * 1024f), summary.totalErrors, summary.totalWarnings, summary.totalTime.TotalSeconds, signing);
        Debug.Log("AndroidBuilder: " + message);
        WriteResult(message);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }

    private static void WriteResult(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, System.DateTime.Now.ToString("s") + " " + message);
    }
}
