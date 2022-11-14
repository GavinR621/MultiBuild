using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Unity.EditorCoroutines.Editor;

public class MultiBuild : EditorWindow
{
    
    [MenuItem("Tools/Multi Build")]
    public static void OnShowTools()
    {
        EditorWindow.GetWindow<MultiBuild>();
    }

    private BuildTargetGroup GetTargetGroupForTarget(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneWindows => BuildTargetGroup.Standalone,
        BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
        BuildTarget.StandaloneLinux64 => BuildTargetGroup.Standalone,
        BuildTarget.EmbeddedLinux => BuildTargetGroup.EmbeddedLinux,
        BuildTarget.Android => BuildTargetGroup.Android,
        BuildTarget.iOS => BuildTargetGroup.iOS,
        BuildTarget.tvOS => BuildTargetGroup.tvOS,
        BuildTarget.WebGL => BuildTargetGroup.WebGL,
        BuildTarget.XboxOne => BuildTargetGroup.XboxOne,
        BuildTarget.GameCoreXboxSeries => BuildTargetGroup.GameCoreXboxSeries,
        BuildTarget.PS4 => BuildTargetGroup.PS4,
        BuildTarget.PS5 => BuildTargetGroup.PS5,
        BuildTarget.Switch => BuildTargetGroup.Switch,
        BuildTarget.Lumin => BuildTargetGroup.Lumin,
        BuildTarget.WSAPlayer => BuildTargetGroup.WSA,
        _ => BuildTargetGroup.Unknown
    };

    Dictionary<BuildTarget, bool> TargetsToBuild = new Dictionary<BuildTarget, bool>();
    List<BuildTarget> AvailableTargets = new List<BuildTarget>();

    void OnEnable()
    {
        var buildTargets = System.Enum.GetValues(typeof(BuildTarget));

        foreach (var buildTargetValue in buildTargets)
        {
            BuildTarget target = (BuildTarget)buildTargetValue;

            if (!BuildPipeline.IsBuildTargetSupported(GetTargetGroupForTarget(target), target))
            {
                continue;
            }

            AvailableTargets.Add(target);

            if (!TargetsToBuild.ContainsKey(target))
            {
                TargetsToBuild[target] = false;
            }
        }

        if (TargetsToBuild.Count > AvailableTargets.Count)
        {
            List<BuildTarget> targetsToRemove = new List<BuildTarget>();
            foreach (var target in TargetsToBuild.Keys)
            {
                if (!AvailableTargets.Contains(target))
                {
                    targetsToRemove.Add(target);
                }
            }

            foreach (var target in targetsToRemove)
            {
                TargetsToBuild.Remove(target);
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Platforms to Build", EditorStyles.boldLabel);

        int numEnabled = 0;

        foreach (var target in AvailableTargets)
        {
            TargetsToBuild[target] = EditorGUILayout.Toggle(target.ToString(), TargetsToBuild[target]);

            if (TargetsToBuild[target])
            {
                numEnabled++;
            }
        }

        if (numEnabled > 0)
        {
            string prompt = numEnabled == 1 ? "Build 1 Platform" : $"Build {numEnabled} Platforms";
            if (GUILayout.Button(prompt))
            {
                List<BuildTarget> selectedPlatforms = new List<BuildTarget>();

                foreach (var target in selectedPlatforms)
                {
                    if (TargetsToBuild[target])
                    {
                        selectedPlatforms.Add(target);
                    }
                }

                EditorCoroutineUtility.StartCoroutine(PerformBuild(selectedPlatforms), this);
            }
        }
    }

    IEnumerator PerformBuild(List<BuildTarget> platformsToBuild)
    {
        int buildAllProgressID = Progress.Start("Build All", "Building all selected platforms", Progress.Options.Sticky);
        Progress.ShowDetails();

        yield return new EditorWaitForSeconds(1f);

        BuildTarget originalTarget = EditorUserBuildSettings.activeBuildTarget;

        for (int targetIndex = 0; targetIndex < TargetsToBuild.Count; ++targetIndex)
        {
            var buildTarget = platformsToBuild[targetIndex];

            int buildTaskProgressID = Progress.Start($"Build {buildTarget.ToString()}", null, Progress.Options.Sticky, buildAllProgressID);

            yield return new EditorWaitForSeconds(1f);

            if (!BuildIndiviudalTarget(buildTarget))
            {
                Progress.Finish(buildTaskProgressID, Progress.Status.Failed);
                Progress.Finish(buildAllProgressID, Progress.Status.Failed);

                if (EditorUserBuildSettings.activeBuildTarget != originalTarget)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(GetTargetGroupForTarget(originalTarget), originalTarget);
                }

                yield break;
            }

            Progress.Finish(buildTaskProgressID, Progress.Status.Succeeded);
            yield return new EditorWaitForSeconds(1f);
        }

        Progress.Finish(buildAllProgressID, Progress.Status.Succeeded);

        yield return null;
    }

    bool BuildIndiviudalTarget(BuildTarget target)
    {
        BuildPlayerOptions options = new BuildPlayerOptions();

        List<string> scenes = new List<string>();

        foreach (var scene in EditorBuildSettings.scenes)
        {
            scenes.Add(scene.path);
        }

        options.scenes = scenes.ToArray();
        options.target = target;
        options.targetGroup = GetTargetGroupForTarget(target);
        
        if (target == BuildTarget.Android)
        {
            string apkName = PlayerSettings.productName + ".apk";
            options.locationPathName = System.IO.Path.Combine("Builds", target.ToString(), apkName);
        }
        else
        {
            options.locationPathName = System.IO.Path.Combine("Builds", target.ToString(), PlayerSettings.productName);
        }

        if (BuildPipeline.BuildCanBeAppended(target, options.locationPathName) == CanAppendBuild.Yes)
        {
            options.options = BuildOptions.AcceptExternalModificationsToPlayer;
        }
        else
        {
            options.options = BuildOptions.None;
        }

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build for {target.ToString()} completed in {report.summary.totalTime.TotalSeconds} seconds");
            return true;
        }
        else
        {
            Debug.LogError($"Build for {target.ToString()} failed");
            return false;
        }
    }
}
