using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// Command-line builds, e.g.
    /// Unity -batchmode -quit -projectPath . -executeMethod TrashCats.Editor.BuildScript.BuildAndroid -logFile build.log
    /// Output goes to Builds/ (ignored by git).
    /// </summary>
    public static class BuildScript
    {
        private const string AndroidId = "com.trashcats.prototype";

        [MenuItem("TrashCats/Build/Android Development APK")]
        public static void BuildAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            Build(BuildTarget.Android, Path.Combine("Builds", "Android", "TrashCats-dev.apk"), BuildOptions.Development);
        }

        [MenuItem("TrashCats/Build/Linux Game")]
        public static void BuildLinuxGame()
        {
            Build(BuildTarget.StandaloneLinux64, Path.Combine("Builds", "Linux", "TrashCats.x86_64"), BuildOptions.Development);
        }

        [MenuItem("TrashCats/Build/Linux Animation Test")]
        public static void BuildLinuxAnimationTest()
        {
            AnimationTestSceneBuilder.Build();
            Build(BuildTarget.StandaloneLinux64, Path.Combine("Builds", "LinuxAnimationTest", "TrashCatsAnimTest.x86_64"),
                BuildOptions.None, new[] { AnimationTestSceneBuilder.ScenePath });
        }

        [MenuItem("TrashCats/Build/Linux All Animations")]
        public static void BuildLinuxAllAnimations()
        {
            AllAnimationsSceneBuilder.Build();
            Build(BuildTarget.StandaloneLinux64, Path.Combine("Builds", "LinuxAllAnimations", "TrashCatsAllAnimations.x86_64"),
                BuildOptions.None, new[] { AllAnimationsSceneBuilder.ScenePath });
        }

        private static void Build(BuildTarget target, string path, BuildOptions options, string[] scenes = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            scenes ??= EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = options,
            });

            var summary = report.summary;
            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            else if (summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {summary.result}");
        }
    }
}
