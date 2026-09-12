using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;

namespace FizzyMoo.EditorTools
{
    /// <summary>
    /// Headless project setup + build. The whole pipeline is driven from the
    /// command line so the game can be rebuilt from a clean checkout with one
    /// command and no manual editor steps.
    /// </summary>
    public static class FizzyMooBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Fizzy Moo/1 - Generate Scene")]
        public static void GenerateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            ApplyPlayerSettings();
            EnsureAlwaysIncludedShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("[FizzyMoo] Scene generated at " + ScenePath);
        }


        /// <summary>
        /// Every material in this game is created at runtime via Shader.Find, so no
        /// scene asset references the shaders and the player build strips them - which
        /// shows up as "Value cannot be null. Parameter name: shader" on launch.
        /// Registering them as Always Included Shaders keeps them in the build.
        /// </summary>
        static void EnsureAlwaysIncludedShaders()
        {
            string[] names = { "Standard", "UI/Default", "Sprites/Default", "FizzyMoo/Outline" };
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) { Debug.LogWarning("[FizzyMoo] GraphicsSettings not found"); return; }

            var so = new SerializedObject(assets[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            var have = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var sh = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (sh != null) have.Add(sh.name);
            }
            foreach (var n in names)
            {
                if (have.Contains(n)) { Debug.Log("[FizzyMoo] shader already included: " + n); continue; }
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning("[FizzyMoo] shader not found: " + n); continue; }
                int idx = arr.arraySize;
                arr.InsertArrayElementAtIndex(idx);
                arr.GetArrayElementAtIndex(idx).objectReferenceValue = sh;
                Debug.Log("[FizzyMoo] always-include shader: " + n);
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Fizzy Moo";
            PlayerSettings.companyName = "ATLS 4616";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "edu.colorado.atls4616.fizzymoo");
            // Deterministic capture: no vsync stalls fighting Time.captureFramerate.
            QualitySettings.vSyncCount = 0;
        }

        [MenuItem("Fizzy Moo/2 - Build macOS Player")]
        public static void BuildMac()
        {
            GenerateScene();
            TrySetAppleSilicon();

            var dir = Path.GetFullPath("Build");
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, "FizzyMoo.app");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = target,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[FizzyMoo] Build {s.result}  size={s.totalSize / (1024 * 1024)}MB  errors={s.totalErrors}  -> {target}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Build failed: " + s.result);
        }

        /// <summary>Set via reflection so the script still compiles if the module moves.</summary>
        static void TrySetAppleSilicon()
        {
            try
            {
                var t = System.Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
                if (t == null) return;
                var prop = t.GetProperty("architecture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) return;
                var enumT = prop.PropertyType;
                var val = System.Enum.Parse(enumT, "ARM64");
                prop.SetValue(null, val);
                Debug.Log("[FizzyMoo] Target architecture set to ARM64");
            }
            catch (System.Exception e) { Debug.LogWarning("[FizzyMoo] arch set skipped: " + e.Message); }
        }

        // ---------------------------------------------------------------- iOS / AR

        const string ARKitLoader = "UnityEngine.XR.ARKit.ARKitLoader";

        /// <summary>
        /// iOS player + XR setup, done in code so the AR build is as reproducible as the desktop one.
        /// Two of these are silent killers if missed: with no XR loader registered ARSession reports
        /// Unsupported and you get a black screen with the HUD on top and zero errors; with an empty
        /// cameraUsageDescription iOS terminates the app the instant ARKit opens a capture session,
        /// which presents as a random crash rather than a permissions problem.
        /// </summary>
        public static void ApplyIOSSettings()
        {
            PlayerSettings.iOS.cameraUsageDescription = "Fizzy Moo puts Bessie's meadow on your table.";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 1);   // ARM64
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "edu.colorado.atls4616.fizzymoo");
            // The HUD has no orientation handling; lock it rather than let it rotate into a broken layout.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            EnsureARKitLoader();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Register the ARKit loader for iOS and fail loudly if it did not stick.</summary>
        static void EnsureARKitLoader()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.iOS);
            if (settings == null || settings.Manager == null)
                throw new System.Exception("[FizzyMoo] could not create XRGeneralSettings for iOS");

            if (!XRPackageMetadataStore.AssignLoader(settings.Manager, ARKitLoader, BuildTargetGroup.iOS))
                Debug.LogWarning("[FizzyMoo] AssignLoader returned false (may already be assigned)");

            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var names = settings.Manager.activeLoaders.Select(l => l == null ? "<null>" : l.GetType().FullName).ToList();
            Debug.Log("[FizzyMoo] iOS active XR loaders: " + (names.Count == 0 ? "NONE" : string.Join(", ", names)));
            if (!names.Any(n => n.Contains("ARKitLoader")))
                throw new System.Exception("[FizzyMoo] ARKit loader NOT registered for iOS - the build would " +
                                           "produce a black screen with no errors. Aborting.");
        }

        [MenuItem("Fizzy Moo/3 - Build iOS (Xcode project)")]
        public static void BuildIOS()
        {
            GenerateScene();
            ApplyIOSSettings();
            EnsureAlwaysIncludedShaders();

            var dir = Path.GetFullPath("Build/iOS");
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = dir,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[FizzyMoo] iOS build {s.result}  errors={s.totalErrors}  -> {dir}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("iOS build failed: " + s.result);
        }

        /// <summary>
        /// Desktop build carrying FIZZY_ARPREVIEW, so the AR stage can be exercised and
        /// frame-captured without a phone attached.
        /// </summary>
        [MenuItem("Fizzy Moo/4 - Build AR Preview (desktop)")]
        public static void BuildARPreview()
        {
            var nbt = UnityEditor.Build.NamedBuildTarget.Standalone;
            var defines = PlayerSettings.GetScriptingDefineSymbols(nbt);
            if (!defines.Contains("FIZZY_ARPREVIEW"))
                PlayerSettings.SetScriptingDefineSymbols(nbt,
                    string.IsNullOrEmpty(defines) ? "FIZZY_ARPREVIEW" : defines + ";FIZZY_ARPREVIEW");
            try { BuildMac(); }
            finally { PlayerSettings.SetScriptingDefineSymbols(nbt, defines); }
        }

        /// <summary>CI entry point: prove the iOS/XR configuration without running a full build.</summary>
        public static void CI_ConfigureIOS()
        {
            GenerateScene();
            ApplyIOSSettings();
            Debug.Log("[FizzyMoo] iOS configuration OK");
        }

        /// <summary>Compile check only - fails the CLI run if anything does not build.</summary>
        public static void CI_Compile()
        {
            GenerateScene();
            Debug.Log("[FizzyMoo] Compile + scene generation OK");
        }
    }
}
