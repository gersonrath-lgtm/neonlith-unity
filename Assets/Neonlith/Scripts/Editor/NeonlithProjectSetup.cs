using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if NEONLITH_URP
using UnityEngine.Rendering.Universal;
#endif
#if NEONLITH_XRM
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
#endif
#if NEONLITH_OPENXR
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
#endif

namespace Neonlith.Editor
{
    /// <summary>
    /// Projekt für die Meta Quest 3 einrichten (Menü „Neonlith“). Gegenstück zu <c>export_presets.cfg</c> und den
    /// [rendering]/[xr]-Abschnitten in <c>project.godot</c>. „Alles einrichten“ führt die Schritte der Reihe nach aus;
    /// jeder Schritt steht auch einzeln im Menü. Was ein Schritt nicht automatisch schafft, meldet er im Log mit dem
    /// Ort in den Project Settings, an dem es von Hand zu setzen ist (README.md).
    /// </summary>
    public static class NeonlithProjectSetup
    {
        const string ScenePath = "Assets/Neonlith/Scenes/Bootstrap.unity";
        const string MatDir = "Assets/Neonlith/Resources/Materials";
        const string SettingsDir = "Assets/Neonlith/Settings";

        [MenuItem("Neonlith/Alles einrichten (Quest 3)", false, 0)]
        public static void All()
        {
            PlayerSettingsStep();
            Materials();
            RenderPipeline();
            XRSettings();
            Scene();
            Debug.Log("Neonlith: Einrichtung fertig. Prüfen: Project Settings → XR Plug-in Management (Android: OpenXR), OpenXR-Features, dann „Neonlith → APK bauen“.");
        }

        // ------------------------------------------------------------------------------------------------
        [MenuItem("Neonlith/1 Player-Einstellungen", false, 20)]
        public static void PlayerSettingsStep()
        {
            PlayerSettings.productName = "Neonlith";
            PlayerSettings.companyName = "Neonlith";
            PlayerSettings.bundleVersion = "4.99.65";          // export_presets.cfg version/name
            PlayerSettings.Android.bundleVersionCode = 5055;    // export_presets.cfg version/code
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.example.neonlith");

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // nur arm64-v8a wie in Godot
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;            // Quest 3: Android 12L
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.ARCoreEnabled = false;

            // Vulkan allein (Godot 4.60+: Mobile-Renderer mit Vulkan, Foveation über VRS)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);

            // Godot: boot_splash/bg_color schwarz, kein Bild
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;

            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;   // etc2_astc wie Godot
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
            AssetDatabase.SaveAssets();
            Debug.Log("Neonlith: Player-Einstellungen gesetzt (Paket com.example.neonlith, 4.99.65/5055, IL2CPP, ARM64, API 32, Vulkan, Linear, ASTC).");
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Grundmaterialien in Resources (damit die URP-Shader im Build liegen) und Himmel je Welt.</summary>
        [MenuItem("Neonlith/2 Materialien und Himmel", false, 21)]
        public static void Materials()
        {
            Directory.CreateDirectory(MatDir);
            Material(MatDir + "/Surface.mat", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Metallic", 0.55f);
                m.SetFloat("_Smoothness", 0.45f);
            });
            // eigener Neon-Shader mit Fernberuhigung, sonst URP/Unlit
            var neonShader = Shader.Find("Neonlith/Neon");
            Material(MatDir + "/Neon.mat", neonShader != null && neonShader.isSupported ? "Neonlith/Neon" : "Universal Render Pipeline/Unlit", m =>
            {
                if (m.HasProperty("_FarStart")) { m.SetFloat("_FarStart", 40f); m.SetFloat("_FarEnd", 120f); }
            });
            foreach (var t in WorldTheme.Themes)
            {
                var texPath = $"Assets/Neonlith/Art/Textures/space_sky_{t.id}.png";
                var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (imp != null && (imp.maxTextureSize < 4096 || !imp.mipmapEnabled))
                {
                    imp.maxTextureSize = 4096;
                    imp.mipmapEnabled = true;
                    imp.sRGBTexture = true;
                    imp.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                var theme = t;
                Material($"{MatDir}/Sky_{t.id}.mat", "Skybox/Panoramic", m =>
                {
                    m.SetTexture("_MainTex", tex);
                    m.SetFloat("_Mapping", 1f);        // Latitude Longitude Layout
                    m.SetFloat("_ImageType", 0f);      // 360 Grad
                    m.SetFloat("_Exposure", theme.skyEnergy);
                    m.SetColor("_Tint", new Color(0.5f * theme.skyTint.x, 0.5f * theme.skyTint.y, 0.5f * theme.skyTint.z, 0.5f));
                });
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Neonlith: Materialien unter " + MatDir);
        }

        static void Material(string path, string shaderName, System.Action<Material> init)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogError($"Neonlith: Shader {shaderName} nicht gefunden (URP installiert?)"); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = sh;
            init?.Invoke(m);
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>URP für die Quest: Forward, MSAA 4x, ohne HDR, Schatten, Depth- und Opaque-Texture (KLARBILD.md, PERFORMANCE.md).</summary>
        [MenuItem("Neonlith/3 Render-Pipeline (URP)", false, 22)]
        public static void RenderPipeline()
        {
#if NEONLITH_URP
            Directory.CreateDirectory(SettingsDir);
            var rdPath = SettingsDir + "/NeonlithRenderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
            }
            var assetPath = SettingsDir + "/NeonlithURP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            var so = new SerializedObject(asset);
            Set(so, "m_MSAA", 4);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0);      // keine Zusatzlichter
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_RenderScale", 1.0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = asset;
            var levels = QualitySettings.names.Length;
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("Neonlith: URP-Asset " + assetPath + " ist die Render-Pipeline (Graphics und alle Quality-Stufen).");
#else
            Debug.LogError("Neonlith: Paket com.unity.render-pipelines.universal fehlt.");
#endif
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"Neonlith: URP-Feld {name} nicht gefunden (andere URP-Version?), bitte im Asset von Hand setzen"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>XR Plug-in Management: OpenXR für Android, Touch-Controller, Handtracking, Meta-Quest-Features.</summary>
        [MenuItem("Neonlith/4 XR und OpenXR", false, 23)]
        public static void XRSettings()
        {
#if NEONLITH_XRM
            try
            {
                XRGeneralSettingsPerBuildTarget all = null;
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out all);
                if (all == null)
                {
                    Directory.CreateDirectory("Assets/XR");
                    all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(all, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, all, true);
                }
                var settings = all.SettingsForBuildTarget(BuildTargetGroup.Android);
                if (settings == null)
                {
                    all.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                    settings = all.SettingsForBuildTarget(BuildTargetGroup.Android);
                }
                settings.InitManagerOnStart = true;
                var ok = XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
                Debug.Log(ok ? "Neonlith: OpenXR-Loader für Android zugewiesen." : "Neonlith: OpenXR-Loader konnte nicht zugewiesen werden: Project Settings → XR Plug-in Management → Android → OpenXR ankreuzen.");
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(all);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Neonlith: XR Plug-in Management nicht per Skript einstellbar (" + ex.Message + "). Von Hand: Project Settings → XR Plug-in Management → Android → OpenXR.");
            }
#else
            Debug.LogError("Neonlith: Paket com.unity.xr.management fehlt.");
#endif
#if NEONLITH_OPENXR
            try
            {
                FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
                var s = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (s != null)
                {
                    s.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;   // Multiview (Godot: view_configuration Stereo)
                    s.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None; // Godot: submit_depth_buffer = false
                    var names = new System.Text.StringBuilder();
                    foreach (var f in s.GetFeatures())
                    {
                        var n = f.GetType().Name;
                        var want = n.Contains("OculusTouchControllerProfile") || n.Contains("HandInteractionProfile")
                            || n.Contains("MetaQuest") || n.Contains("Foveat") || n.Contains("HandTracking");
                        if (want && !f.enabled) { f.enabled = true; names.Append(n).Append(' '); }
                    }
                    EditorUtility.SetDirty(s);
                    Debug.Log("Neonlith: OpenXR Android: Multiview, kein Depth-Submit; eingeschaltet: " + names + "(prüfen unter Project Settings → XR Plug-in Management → OpenXR).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Neonlith: OpenXR-Features nicht per Skript einstellbar (" + ex.Message + "). Von Hand: OpenXR → Android: Oculus Touch Controller Profile, Hand Interaction Profile, Meta Quest Support, Foveated Rendering.");
            }
#endif
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Szene mit dem Bootstrap-Objekt anlegen und als einzige Szene in die Build Settings eintragen.</summary>
        [MenuItem("Neonlith/5 Szene erzeugen", false, 24)]
        public static void Scene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            var b = go.AddComponent<Bootstrap>();
            b.mission = 0;            // Halle der Tore
            b.weapon = "auto";
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Neonlith: Szene " + ScenePath + " angelegt und in die Build Settings eingetragen.");
        }

        // ------------------------------------------------------------------------------------------------
        [MenuItem("Neonlith/APK bauen (Quest 3)", false, 40)]
        public static void BuildApk()
        {
            if (!File.Exists(ScenePath)) Scene();
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Neonlith.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"Neonlith: Build {report.summary.result}, {report.summary.totalSize / 1e6:0.0} MB → Builds/Neonlith.apk. Installieren: adb install -r Builds/Neonlith.apk");
        }
    }
}
