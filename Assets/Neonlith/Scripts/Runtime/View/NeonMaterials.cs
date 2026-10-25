using System.Collections.Generic;
using UnityEngine;

namespace Neonlith.View
{
    /// <summary>
    /// Werkstoffe des ersten Stands. Grundlage sind zwei Materialien in <c>Resources/Materials</c>
    /// (vom Editor-Menü „Neonlith → Projekt einrichten“ angelegt: Surface = URP/Lit, Neon = URP/Unlit), damit
    /// die Shader sicher im Build liegen. Farben kommen aus <see cref="WorldTheme"/>; gleiche Funktion = gleiche Farbe.
    /// Später ersetzt der eigene Surface-Shader (docs: Shader-Tabelle) diese Materialien.
    /// </summary>
    public static class NeonMaterials
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public static void Clear() => Cache.Clear();

        static Material Base(string name, string shaderFallback)
        {
            var m = Resources.Load<Material>("Materials/" + name);
            if (m != null) return m;
            Debug.LogWarning($"Neonlith: Resources/Materials/{name}.mat fehlt (Menü Neonlith → Projekt einrichten); Ersatz über Shader.Find");
            var sh = Shader.Find(shaderFallback);
            return sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
        }

        /// <summary>Beleuchtete Fläche (Metall der Brücke, Figuren).</summary>
        public static Material Lit(string key, Color srgb, float metallic = 0.55f, float smooth = 0.45f)
        {
            if (Cache.TryGetValue(key, out var c)) return c;
            var m = new Material(Base("Surface", "Universal Render Pipeline/Lit")) { name = "Lit_" + key };
            m.SetColor(BaseColor, srgb);
            m.SetFloat(Metallic, metallic);
            m.SetFloat(Smoothness, smooth);
            Cache[key] = m;
            return m;
        }

        /// <summary>Beleuchtet mit Eigenleuchten (Gegner, Kern).</summary>
        public static Material Glow(string key, Color srgb, Color emission, float strength = 1.2f)
        {
            if (Cache.TryGetValue(key, out var c)) return c;
            var m = new Material(Base("Surface", "Universal Render Pipeline/Lit")) { name = "Glow_" + key };
            m.SetColor(BaseColor, srgb);
            m.SetFloat(Metallic, 0.2f);
            m.SetFloat(Smoothness, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor(EmissionColor, emission.linear * strength);
            Cache[key] = m;
            return m;
        }

        /// <summary>Unbeleuchtet (Leuchtlinien, Leuchtspur, Tafeln).</summary>
        public static Material Neon(string key, Color srgb)
        {
            if (Cache.TryGetValue(key, out var c)) return c;
            var m = new Material(Base("Neon", "Universal Render Pipeline/Unlit")) { name = "Neon_" + key };
            m.SetColor(BaseColor, srgb);
            Cache[key] = m;
            return m;
        }
    }
}
