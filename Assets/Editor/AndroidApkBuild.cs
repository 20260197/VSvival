using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CookieBreaker.Editor
{
    public static class AndroidApkBuild
    {
        private const string MainScene = CookieBreakerProjectSetup.MainScenePath;
        private const string ApplicationId = "com.mystudio.cookiebreaker";

        [MenuItem("Cookie Breaker/Build Android Test APK")]
        public static void Build()
        {
            if (Application.unityVersion != "6000.5.1f1")
                throw new BuildFailedException("Open this project with Unity 6000.5.1f1.");

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Install Android Build Support, SDK, NDK and OpenJDK for this Unity version.");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android and Switch Platform in File > Build Profiles before building.");

            if (PlayerSettings.productName != "Cookie Breaker" ||
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != ApplicationId ||
                PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                throw new BuildFailedException("Check Product Name, Android Application ID and Portrait orientation in Player Settings.");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                throw new BuildFailedException("This test build requires IL2CPP and ARM64 in Android Player Settings.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
                throw new BuildFailedException("Create and inspect the main scene using Cookie Breaker > Create Test Main Scene before building.");

            string output = Path.Combine(CookieBreakerProjectSetup.ProjectRoot, "Builds/Android/CookieBreaker.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            bool previousBundleSetting = EditorUserBuildSettings.buildAppBundle;
            bool previousExportSetting = EditorUserBuildSettings.exportAsGoogleAndroidProject;

            try
            {
                // Build an installable development APK rather than exporting Gradle files.
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development | BuildOptions.StrictMode
                });

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(output))
                    throw new BuildFailedException($"Android APK build failed: {report.summary.result}, {report.summary.totalErrors} errors.");

                Debug.Log($"Cookie Breaker APK built: {output} ({report.summary.totalSize} bytes)");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundleSetting;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportSetting;
            }
        }
    }
}
