using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neonlith.Editor
{
    /// <summary>
    /// Projekteinstellungen für die Meta Quest 3 in einem Schritt setzen (Menü „Neonlith → Projekt einrichten“).
    /// Gegenstück zu <c>export_presets.cfg</c> und den [rendering]/[xr]-Abschnitten in <c>project.godot</c>.
    /// Nicht per Skript erreichbar und daher von Hand zu setzen (siehe README.md):
    /// XR Plug-in Management → Android → OpenXR; OpenXR-Features „Meta Quest Support“, Interaction Profile
    /// „Oculus Touch Controller“ und „Hand Interaction“; URP-Asset (kein Post-Processing, MSAA 4x, keine Schatten);
    /// OpenXR → Foveated Rendering (Meta) auf Stufe 2, fest (Godot: foveation_level=2, dynamic=false).
    /// </summary>
    public static class NeonlithProjectSetup
    {
        [MenuItem("Neonlith/Projekt einrichten (Quest 3)")]
        public static void Apply()
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

            // Vulkan allein (Godot 4.60+: Mobile-Renderer mit Vulkan, Foveation über VRS)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            PlayerSettings.Android.ARCoreEnabled = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // Godot: boot_splash/bg_color schwarz, kein Bild
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;

            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;   // etc2_astc wie Godot
            AssetDatabase.SaveAssets();
            Debug.Log("Neonlith: Projekteinstellungen gesetzt. Jetzt XR Plug-in Management, OpenXR-Features und URP von Hand prüfen (README.md).");
        }
    }
}
