using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>Compile check only - fails the CLI run if anything does not build.</summary>
        public static void CI_Compile()
        {
            GenerateScene();
            Debug.Log("[FizzyMoo] Compile + scene generation OK");
        }
    }
}
