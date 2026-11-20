using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// The build entry of the verification build (tools/README.md#verification-build).
// mu3-verify-build copies it into the project it generated from tools/unity-projects/full only,
// runs it with -executeMethod Mu3VerifyBuild.Build, and prints the MU3VERIFY lines it logs.
public static class Mu3VerifyBuild
{
    private const string Prefix = "MU3VERIFY ";
    private const string EmptyScenePath = "Assets/Mu3VerifyBuild/Empty.unity";

    // Arguments: -mu3-content build|none, -mu3-out <player .exe path>, and -mu3-scene <scene asset
    // path> once per scene (none builds one empty scene). Exits 0 when the player built, 1 when the
    // build failed, and 2 when an argument is wrong or the settings do not read back what
    // mu3-verify-build wrote, before anything is built.
    public static void Build()
    {
        int exitCode;
        try
        {
            exitCode = Run();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            exitCode = 1;
        }

        Debug.Log(Prefix + "exit " + exitCode);
        EditorApplication.Exit(exitCode);
    }

    private static int Run()
    {
        string content = ArgumentAfter("-mu3-content");
        string output = ArgumentAfter("-mu3-out");
        List<string> scenes = ArgumentsAfter("-mu3-scene");
        if ((content != "build" && content != "none") || string.IsNullOrEmpty(output))
        {
            Debug.LogError($"{Prefix}refused: bad arguments content={content} out={output}");
            return 2;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError(Prefix + "refused: the project has no Addressables settings");
            return 2;
        }

        List<string> problems = SettingsProblems(settings, content);
        foreach (string problem in problems)
        {
            Debug.LogError(Prefix + "refused: " + problem);
        }
        if (problems.Count > 0)
        {
            return 2;
        }

        if (scenes.Count == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(EmptyScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, EmptyScenePath))
            {
                Debug.LogError(Prefix + "refused: could not save " + EmptyScenePath);
                return 2;
            }
            scenes.Add(EmptyScenePath);
        }
        foreach (string scenePath in scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError(Prefix + "refused: no scene at " + scenePath);
                return 2;
            }
        }
        Debug.Log(Prefix + "scenes " + string.Join(", ", scenes));

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
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
                    Debug.Log($"{Prefix}step-{message.type} [{step.name}] {message.content}");
                }
            }
        }

        Debug.Log($"{Prefix}build {report.summary.result} errors={report.summary.totalErrors} " +
            $"warnings={report.summary.totalWarnings} time={report.summary.totalTime} output={report.summary.outputPath}");
        // BuildPlayer can return Unknown, not Failed, for a build that failed (#445).
        return report.summary.result == BuildResult.Succeeded ? 0 : 1;
    }

    // Why the settings Unity loaded differ from what mu3-verify-build wrote into the project's file,
    // or would let a content build write outside the project; empty when neither. The config builds
    // every group into the project's Library and no remote catalog (ADR 0001 D17), so a group or a
    // catalog that builds anywhere else stops the build before the content build could write there.
    private static List<string> SettingsProblems(AddressableAssetSettings settings, string content)
    {
        var problems = new List<string>();
        AddressableAssetProfileSettings profiles = settings.profileSettings;
        string active = settings.activeProfileId;
        string project = Path.GetFullPath(".");
        AddressableAssetSettings.PlayerBuildOption expected = content == "build"
            ? AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer
            : AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

        Debug.Log($"{Prefix}settings profile={profiles.GetProfileName(active)} " +
            $"BuildAddressablesWithPlayerBuild={settings.BuildAddressablesWithPlayerBuild} ({(int)settings.BuildAddressablesWithPlayerBuild}) " +
            $"BuildRemoteCatalog={settings.BuildRemoteCatalog}");

        if (settings.BuildAddressablesWithPlayerBuild != expected)
        {
            problems.Add($"BuildAddressablesWithPlayerBuild is {settings.BuildAddressablesWithPlayerBuild}, not {expected}");
        }
        if (settings.BuildRemoteCatalog)
        {
            string catalogBuild = settings.RemoteCatalogBuildPath.GetValue(settings);
            Debug.Log($"{Prefix}settings RemoteCatalogBuildPath={catalogBuild}");
            if (!IsInside(catalogBuild, project))
            {
                problems.Add($"the remote catalog builds to {catalogBuild}, outside the project {project}");
            }
        }
        foreach (AddressableAssetGroup group in settings.groups)
        {
            BundledAssetGroupSchema schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                continue;
            }
            string groupBuild = schema.BuildPath.GetValue(settings);
            Debug.Log($"{Prefix}settings group {group.Name} BuildPath={groupBuild} LoadPath={schema.LoadPath.GetValue(settings)}");
            if (!IsInside(groupBuild, project))
            {
                problems.Add($"the group {group.Name} builds to {groupBuild}, outside the project {project}");
            }
        }
        // A player built without content copies whatever an earlier content build left here.
        string earlierContent = Path.GetFullPath(Path.Combine("Library", "com.unity.addressables", "aa"));
        if (content == "none" && Directory.Exists(earlierContent))
        {
            problems.Add($"{earlierContent} holds an earlier content build, which a player without content would carry");
        }
        return problems;
    }

    private static bool IsInside(string path, string folder)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        string Normalize(string value) => Path.GetFullPath(value).TrimEnd('\\', '/').ToLowerInvariant();
        string full = Normalize(path);
        string root = Normalize(folder);
        return full == root || full.StartsWith(root + Path.DirectorySeparatorChar) || full.StartsWith(root + Path.AltDirectorySeparatorChar);
    }

    private static string ArgumentAfter(string name)
    {
        List<string> values = ArgumentsAfter(name);
        return values.Count > 0 ? values[0] : null;
    }

    private static List<string> ArgumentsAfter(string name)
    {
        var values = new List<string>();
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == name)
            {
                values.Add(args[i + 1]);
            }
        }
        return values;
    }
}
