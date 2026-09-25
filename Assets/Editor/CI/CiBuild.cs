#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Entry point for the WebGL job in .github/workflows/build.yml (game-ci buildMethod).
// Exists because WebGL "Code Optimization" lives in EditorUserBuildSettings (Library/), not
// ProjectSettings, so a CI checkout never sees the value picked in the Editor's Build Profiles window.
public static class CiBuild
{
    // Not RuntimeSpeedLTO: LTO made the release wasm link run 40+ minutes on a 4-core GitHub runner.
    private const string WEBGL_CODE_OPTIMIZATION = "RuntimeSpeed";

    public static void BuildWebGL()
    {
        string[] args = Environment.GetCommandLineArgs();
        string buildPath = GetArg(args, "-customBuildPath") ?? "build/WebGL/BlueSteam";
        string version = GetArg(args, "-buildVersion");
        bool development = args.Contains("-devBuild");

        if (!string.IsNullOrEmpty(version) && version != "none")
        {
            PlayerSettings.bundleVersion = version;
        }

        SetWebGLCodeOptimization(WEBGL_CODE_OPTIMIZATION);

        var options = BuildOptions.None;
        if (development)
        {
            options |= BuildOptions.Development;
            PlayerSettings.WebGL.showDiagnostics = true;
        }

        var buildOptions = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = options,
        };

        Debug.Log($"[CiBuild] WebGL build -> {buildPath} (development={development})");
        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;
        Debug.Log($"[CiBuild] Result={summary.result} size={summary.totalSize} errors={summary.totalErrors} time={summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    // Reflection because UnityEditor.WebGL.UserBuildSettings only exists when the WebGL module is
    // installed; a direct reference would break compilation on machines without it.
    private static void SetWebGLCodeOptimization(string valueName)
    {
        Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("UnityEditor.WebGL.UserBuildSettings", false))
            .FirstOrDefault(type => type != null);
        PropertyInfo property = settingsType?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);

        if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
        {
            Debug.LogWarning("[CiBuild] UnityEditor.WebGL.UserBuildSettings.codeOptimization not found — Code Optimization left at its default.");
            return;
        }

        if (!Enum.GetNames(property.PropertyType).Contains(valueName))
        {
            Debug.LogWarning($"[CiBuild] Code Optimization '{valueName}' not available. Options: {string.Join(", ", Enum.GetNames(property.PropertyType))}");
            return;
        }

        property.SetValue(null, Enum.Parse(property.PropertyType, valueName));
        Debug.Log($"[CiBuild] WebGL Code Optimization = {property.GetValue(null)}");
    }

    private static string GetArg(string[] args, string name)
    {
        for (int argIndex = 0; argIndex < args.Length - 1; argIndex++)
        {
            if (args[argIndex] == name)
            {
                return args[argIndex + 1];
            }
        }
        return null;
    }
}
#endif
