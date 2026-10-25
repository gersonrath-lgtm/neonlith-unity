using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Neonlith.Editor
{
    /// <summary>
    /// Build-Einstieg für GitHub Actions (GameCI, .github/workflows/quest-apk.yml): richtet das Projekt wie das
    /// Menü „Neonlith → Alles einrichten“ ein und baut die APK an den Pfad, den GameCI mit
    /// <c>-customBuildPath</c> übergibt (sonst build/Android/Neonlith.apk).
    /// </summary>
    public static class CiBuild
    {
        public static void Build()
        {
            var path = Arg("-customBuildPath") ?? "build/Android/Neonlith.apk";
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(path, "Neonlith.apk");
            Debug.Log("Neonlith CI: Ziel " + path);

            NeonlithProjectSetup.PlayerSettingsStep();
            NeonlithProjectSetup.Materials();
            NeonlithProjectSetup.RenderPipeline();
            NeonlithProjectSetup.XRSettings();
            NeonlithProjectSetup.Scene();
            EditorUserBuildSettings.buildAppBundle = false;      // APK, kein AAB
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Neonlith/Scenes/Bootstrap.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"Neonlith CI: {s.result}, {s.totalSize / 1e6:0.0} MB, {s.totalErrors} Fehler, {s.totalWarnings} Warnungen");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
