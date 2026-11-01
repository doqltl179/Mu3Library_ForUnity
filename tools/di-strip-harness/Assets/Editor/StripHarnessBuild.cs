using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// The build entry of the DI stripping harness (tools/README.md#di-stripping-harness).
// mu3-cli unity di-strip-harness runs it with -executeMethod StripHarnessBuild.BuildPlayer and
// reads the HARNESS lines it logs.
public static class StripHarnessBuild
{
    private const string ScenePath = "Assets/Harness/Harness.unity";

    // Arguments: -mu3-out <player .exe path> (required), -mu3-backend mono|il2cpp (default mono),
    // and -mu3-stripping <ManagedStrippingLevel name> (default High). Exits 0 when the build
    // succeeded, 1 when it failed, and 2 when an argument is missing or wrong.
    public static void BuildPlayer()
    {
        try
        {
            string output = ArgumentAfter("-mu3-out");
            string backend = ArgumentAfter("-mu3-backend") ?? "mono";
            string stripping = ArgumentAfter("-mu3-stripping") ?? "High";
            if (string.IsNullOrEmpty(output) ||
                (backend != "mono" && backend != "il2cpp") ||
                !Enum.TryParse(stripping, false, out ManagedStrippingLevel level))
            {
                Debug.LogError($"HARNESS bad arguments: out={output} backend={backend} stripping={stripping}");
                EditorApplication.Exit(2);
                return;
            }

            SaveEmptyScene();

            // These settings live in the mirror's ProjectSettings, so every build sets them again.
            ScriptingImplementation implementation = backend == "il2cpp" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, implementation);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, level);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, "");
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 640;
            PlayerSettings.defaultScreenHeight = 480;
            PlayerSettings.runInBackground = true;

            Debug.Log($"HARNESS settings backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)} " +
                $"stripping={PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone)} " +
                $"stripEngineCode={PlayerSettings.stripEngineCode}");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception)
                    {
                        Debug.Log($"HARNESS STEP-{message.type} [{step.name}] {message.content}");
                    }
                }
            }

            Debug.Log($"HARNESS BUILD {report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} " +
                $"size={report.summary.totalSize} time={report.summary.totalTime} output={report.summary.outputPath}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    // A player needs a scene, and the harness needs an empty one: HarnessBoot creates itself when
    // it loads, so no asset references a script whose GUID only the mirror's .meta file knows.
    private static void SaveEmptyScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    private static string ArgumentAfter(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
