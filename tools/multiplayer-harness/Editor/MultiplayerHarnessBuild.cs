using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// The build entry of the multiplayer harness (tools/README.md#multiplayer-harness).
// mu3-multiplayer-harness copies it into the project it generated from the minimal config, runs
// it with -executeMethod MultiplayerHarnessBuild.BuildPlayer, and prints the MPHARNESS lines it logs.
public static class MultiplayerHarnessBuild
{
    private const string Prefix = "MPHARNESS ";
    private const string ScenePath = "Assets/MultiplayerHarness/Harness.unity";

    // Arguments: -mu3-out <player .exe path>. Exits 0 when the build succeeded, 1 when it failed,
    // and 2 when the argument is missing.
    public static void BuildPlayer()
    {
        try
        {
            string output = ArgumentAfter("-mu3-out");
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogError($"{Prefix}bad arguments: out={output}");
                EditorApplication.Exit(2);
                return;
            }

            SaveEmptyScene();

            // These settings live in the generated project's ProjectSettings, which the next
            // refresh writes back from the config, so every build sets them again. Two players run
            // on one computer, so neither may refuse a second instance.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 640;
            PlayerSettings.defaultScreenHeight = 480;

            Debug.Log($"{Prefix}SETTINGS backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)} " +
                $"stripping={PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone)} " +
                $"forceSingleInstance={PlayerSettings.forceSingleInstance} runInBackground={PlayerSettings.runInBackground}");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                    {
                        Debug.Log($"{Prefix}STEP-{message.type} [{step.name}] {message.content}");
                    }
                }
            }

            Debug.Log($"{Prefix}BUILD {report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} " +
                $"time={report.summary.totalTime} output={report.summary.outputPath}");
            // BuildPlayer can return Unknown, not Failed, for a build that failed (#445).
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    // A player needs a scene, and the harness needs an empty one: MultiplayerHarnessPlayer creates
    // itself when it loads, so no asset references a script whose GUID only the project's .meta
    // file knows.
    private static void SaveEmptyScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("could not save " + ScenePath);
        }

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
