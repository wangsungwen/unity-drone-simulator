using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace DroneTrainer.Editor
{
    [InitializeOnLoad]
    public static class TrainerBuild
    {
        public const string ScenePath = "Assets/DroneTrainer/Scenes/Trainer.unity";
        static TrainerBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("ProjectSettings/TrainerConfigured.txt")) Configure();
            };
        }
        [MenuItem("Drone Trainer/1. Configure project")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Independent Drone Lab";
            PlayerSettings.productName = "Drone Training Lab";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "tw.traininglab.dronesimulator");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "tw.traininglab.dronesimulator");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = player.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; player.ApplyModifiedPropertiesWithoutUndo(); }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            // Resources assets ensure shaders used by the procedural scene survive stripping.
            EnsureMaterial("Standard", "TrainerStandard"); EnsureMaterial("Unlit/Color", "TrainerUnlit");
            AssetDatabase.SaveAssets();
            File.WriteAllText("ProjectSettings/TrainerConfigured.txt", "Drone Trainer project settings v1\n");
            Debug.Log("Drone Trainer configured. Open Trainer.unity and press Play.");
        }
        static void EnsureMaterial(string shaderName, string name)
        {
            string path = "Assets/DroneTrainer/Resources/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find(shaderName)), path);
        }
        [MenuItem("Drone Trainer/2. Open training scene")]
        public static void OpenScene()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Drone Trainer/Build/Windows")]
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "Builds/Windows/DroneTrainingLab.exe"); }
        [MenuItem("Drone Trainer/Build/Android APK")]
        public static void Android()
        {
#if UNITY_ANDROID
            string sdk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_SDK");
            string ndk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_NDK");
            string jdk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_JDK");
            if (!string.IsNullOrEmpty(sdk)) UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdk;
            if (!string.IsNullOrEmpty(ndk)) UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = ndk;
            if (!string.IsNullOrEmpty(jdk)) UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = jdk;
#endif
            EditorUserBuildSettings.buildAppBundle = false; Build(BuildTarget.Android, "Builds/Android/DroneTrainingLab.apk");
        }
        [MenuItem("Drone Trainer/Build/iOS Xcode project")]
        public static void IOS() { Build(BuildTarget.iOS, "Builds/iOS"); }
        static void Build(BuildTarget target, string output)
        {
            Configure(); Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output, target = target, options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Build failed: " + result.summary.result);
            Debug.Log("Built " + output);
        }
#if UNITY_IOS
        [PostProcessBuild(100)]
        public static void AddLocalNetworkDescription(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var file = Path.Combine(path, "Info.plist"); var plist = new PlistDocument(); plist.ReadFromFile(file);
            plist.root.SetString("NSLocalNetworkUsageDescription", "透過區域網路將手機雙搖桿連線至您電腦上的無人機模擬器。");
            plist.WriteToFile(file);
        }
#endif
    }
}
