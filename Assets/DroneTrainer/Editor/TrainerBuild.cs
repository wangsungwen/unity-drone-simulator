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
            PlayerSettings.bundleVersion = "0.2.2";
            PlayerSettings.Android.bundleVersionCode = 4;
            PlayerSettings.iOS.buildNumber = "4";
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
            // Unity 2022.3 supports Android 5.1+, including 32-bit ARMv7/NEON phones.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2
            });
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
        public static void Android() { AndroidVariant(false); }
        [MenuItem("Drone Trainer/Build/Android Remote Controller APK")]
        public static void AndroidRemote() { AndroidVariant(true); }
        static void AndroidVariant(bool remote)
        {
#if UNITY_ANDROID
            string sdk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_SDK");
            string ndk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_NDK");
            string jdk = System.Environment.GetEnvironmentVariable("DRONE_ANDROID_JDK");
            if (!string.IsNullOrEmpty(sdk)) UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdk;
            if (!string.IsNullOrEmpty(ndk)) UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = ndk;
            if (!string.IsNullOrEmpty(jdk)) UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = jdk;
#endif
            EditorUserBuildSettings.buildAppBundle = false; Build(BuildTarget.Android, remote ? "Builds/AndroidRemote/DroneRemoteController.apk" : "Builds/Android/DroneTrainingLab.apk", remote);
        }
        [MenuItem("Drone Trainer/Build/iOS Xcode project")]
        public static void IOS() { Build(BuildTarget.iOS, "Builds/iOS"); }
        [MenuItem("Drone Trainer/Build/iOS Remote Controller Xcode project")]
        public static void IOSRemote() { Build(BuildTarget.iOS, "Builds/iOSRemote", true); }
        static void Build(BuildTarget target, string output, bool remote = false)
        {
            Configure(); Directory.CreateDirectory(Path.GetDirectoryName(output));
            const string launchPath = "Assets/DroneTrainer/Resources/TrainerLaunch.txt";
            byte[] prior = File.Exists(launchPath) ? File.ReadAllBytes(launchPath) : null;
            try
            {
                File.WriteAllText(launchPath, remote ? "remote" : "local");
                AssetDatabase.ImportAsset(launchPath, ImportAssetOptions.ForceSynchronousImport);
                if (target == BuildTarget.Android || target == BuildTarget.iOS)
                {
                    var group = target == BuildTarget.Android ? BuildTargetGroup.Android : BuildTargetGroup.iOS;
                    PlayerSettings.productName = remote ? "無人機電腦遙控器" : "無人機飛行模擬器";
                    PlayerSettings.SetApplicationIdentifier(group, "tw.traininglab.dronesimulator" + (remote ? ".remote" : ""));
                }
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = output, target = target, options = BuildOptions.None
                });
                if (result.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Build failed: " + result.summary.result);
                Debug.Log("Built " + output);
            }
            finally
            {
                if (prior == null) AssetDatabase.DeleteAsset(launchPath);
                else { File.WriteAllBytes(launchPath, prior); AssetDatabase.ImportAsset(launchPath); }
                Configure();
            }
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
