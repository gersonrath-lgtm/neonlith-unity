using UnityEngine;

namespace Neonlith
{
    /// <summary>
    /// Globale Shaderwerte, Gegenstück zu den <c>[shader_globals]</c> in <c>project.godot</c>
    /// (Godot: <c>global uniform</c> in 40 Shadern). In Unity werden sie mit <see cref="Shader.SetGlobalColor"/>
    /// und <see cref="Shader.SetGlobalFloat"/> gesetzt; jeder portierte Shader liest sie als Property ohne
    /// Material-Eintrag (in Shader Graph: Property mit Reference-Namen und „Exposed“ aus, oder in HLSL als
    /// <c>float4 _PalMain;</c> außerhalb eines CBUFFER).
    /// Namen: Godot <c>pal_main</c> → Unity <c>_PalMain</c> usw. (Tabelle in Docs/UNITY_PORT.md).
    /// </summary>
    public static class ShaderGlobals
    {
        // Palette (feste Grundwerte aus project.godot; WorldTheme.Apply überschreibt die Weltanteile)
        public static readonly int PalDeep = Shader.PropertyToID("_PalDeep");
        public static readonly int PalMain = Shader.PropertyToID("_PalMain");
        public static readonly int PalSide = Shader.PropertyToID("_PalSide");
        public static readonly int PalMech = Shader.PropertyToID("_PalMech");
        public static readonly int PalEdge = Shader.PropertyToID("_PalEdge");
        public static readonly int PalRed = Shader.PropertyToID("_PalRed");
        public static readonly int PalRedHot = Shader.PropertyToID("_PalRedHot");
        public static readonly int PalRedHi = Shader.PropertyToID("_PalRedHi");
        public static readonly int PalRedPeak = Shader.PropertyToID("_PalRedPeak");
        public static readonly int PalUi = Shader.PropertyToID("_PalUi");
        public static readonly int PalThreat = Shader.PropertyToID("_PalThreat");
        public static readonly int PalThreatHi = Shader.PropertyToID("_PalThreatHi");
        public static readonly int SceneDim = Shader.PropertyToID("_SceneDim");
        public static readonly int SpaceSky = Shader.PropertyToID("_SpaceSky");
        // Bildtest-Schalter (bt_*): 1 = an. bt_klar ist der Klarbild-Schalter (docs/KLARBILD.md)
        public static readonly int BtFine = Shader.PropertyToID("_BtFine");
        public static readonly int BtMetal = Shader.PropertyToID("_BtMetal");
        public static readonly int BtWear = Shader.PropertyToID("_BtWear");
        public static readonly int BtWeather = Shader.PropertyToID("_BtWeather");
        public static readonly int BtGloss = Shader.PropertyToID("_BtGloss");
        public static readonly int BtFar = Shader.PropertyToID("_BtFar");
        public static readonly int BtHolo = Shader.PropertyToID("_BtHolo");
        public static readonly int BtStars = Shader.PropertyToID("_BtStars");
        public static readonly int BtLite = Shader.PropertyToID("_BtLite");
        public static readonly int BtKlar = Shader.PropertyToID("_BtKlar");
        // Gemeinsames Aufleuchten bei großen Momenten (NeonPulse), 0..1
        public static readonly int NeonPulse = Shader.PropertyToID("_NeonPulse");
        // Wetter
        public static readonly int WxWet = Shader.PropertyToID("_WxWet");
        public static readonly int WxPuddles = Shader.PropertyToID("_WxPuddles");
        public static readonly int WxRain = Shader.PropertyToID("_WxRain");
        public static readonly int WxFlash = Shader.PropertyToID("_WxFlash");
        // Holo-System (docs/HOLO_SYSTEM.md)
        public static readonly int HoloStyle = Shader.PropertyToID("_HoloStyle");
        public static readonly int HoloQ = Shader.PropertyToID("_HoloQ");
        public static readonly int HoloPrimary = Shader.PropertyToID("_HoloPrimary");
        public static readonly int HoloSecondary = Shader.PropertyToID("_HoloSecondary");
        public static readonly int HoloAccent = Shader.PropertyToID("_HoloAccent");
        public static readonly int HoloWarn = Shader.PropertyToID("_HoloWarn");
        public static readonly int HoloShape = Shader.PropertyToID("_HoloShape");
        public static readonly int HoloMotion = Shader.PropertyToID("_HoloMotion");
        public static readonly int DioramaClip = Shader.PropertyToID("_DioramaClip");

        /// <summary>Standardwerte wie in project.godot (lineare Werte, dort bereits linear hinterlegt).</summary>
        public static void ApplyDefaults()
        {
            Shader.SetGlobalColor(PalDeep, new Color(0.0024f, 0.003f, 0.004f, 1));
            Shader.SetGlobalColor(PalMain, new Color(0.0056f, 0.0075f, 0.011f, 1));
            Shader.SetGlobalColor(PalSide, new Color(0.0103f, 0.0152f, 0.0242f, 1));
            Shader.SetGlobalColor(PalMech, new Color(0.0242f, 0.0343f, 0.0513f, 1));
            Shader.SetGlobalColor(PalEdge, new Color(0.0723f, 0.0976f, 0.1384f, 1));
            Shader.SetGlobalColor(PalRed, new Color(0.7529f, 0.0194f, 0.0116f, 1));
            Shader.SetGlobalColor(PalRedHot, new Color(1f, 0.0437f, 0.0296f, 1));
            Shader.SetGlobalColor(PalRedHi, new Color(1f, 0.1441f, 0.0685f, 1));
            Shader.SetGlobalColor(PalRedPeak, new Color(1f, 0.3278f, 0.2346f, 1));
            Shader.SetGlobalColor(PalUi, new Color(0.6867f, 0.7305f, 0.807f, 1));
            Shader.SetGlobalColor(PalThreat, new Color(0.75f, 0.0617f, 0.0742f, 1));
            Shader.SetGlobalColor(PalThreatHi, new Color(0.75f, 0.2459f, 0.176f, 1));
            Shader.SetGlobalFloat(SceneDim, 0.38f);
            foreach (var id in new[] { BtFine, BtMetal, BtWear, BtWeather, BtGloss, BtFar, BtHolo, BtStars, BtLite, BtKlar, HoloQ })
                Shader.SetGlobalFloat(id, 1f);
            foreach (var id in new[] { NeonPulse, WxWet, WxPuddles, WxRain, WxFlash, HoloStyle })
                Shader.SetGlobalFloat(id, 0f);
            Shader.SetGlobalVector(DioramaClip, new Vector4(-10000, -10000, 10000, 10000));
            Shader.SetGlobalColor(HoloPrimary, new Color(0.016f, 0.687f, 1f, 1));
            Shader.SetGlobalColor(HoloSecondary, new Color(0.003f, 0.212f, 0.479f, 1));
            Shader.SetGlobalColor(HoloAccent, new Color(0.328f, 0.863f, 1f, 1));
            Shader.SetGlobalColor(HoloWarn, new Color(0.711f, 0.095f, 0.073f, 1));
            Shader.SetGlobalVector(HoloShape, new Vector4(1.4f, 0.55f, 0.45f, 0.3f));
            Shader.SetGlobalVector(HoloMotion, new Vector4(0.45f, 0.12f, 6f, 0f));
        }

        /// <summary>
        /// Weltfarben in die Palette schreiben, 1:1 nach <c>Palette.apply()</c> in Godot (scripts/view3d/palette.gd):
        /// Metalltöne aus Haupt- und Sekundärgrund, die „Rot“-Plätze tragen das Neon der Welt (Linien, Akzente,
        /// Kerne), pal_threat bleibt in jeder Welt das Rot der Gegner. Farben kommen als sRGB und werden linear gesetzt.
        /// </summary>
        public static void ApplyWorld(WorldTheme t)
        {
            var g = t.ground.linear;
            var g2 = t.ground2.linear;
            Shader.SetGlobalColor(PalDeep, Darkened(g, 0.5f));
            Shader.SetGlobalColor(PalMain, g);
            Shader.SetGlobalColor(PalSide, g2);
            Shader.SetGlobalColor(PalMech, Lightened(g2, 0.18f));
            Shader.SetGlobalColor(PalEdge, Lightened(g2, 0.42f));
            // 4.97: um ein Viertel gedämpft (zu hell in der Brille)
            Shader.SetGlobalColor(PalRed, Lin(t.neon, 0.56f));
            Shader.SetGlobalColor(PalRedHot, Lin(t.tower, 0.75f));
            Shader.SetGlobalColor(PalRedHi, Lin(t.hi, 0.75f));
            Shader.SetGlobalColor(PalRedPeak, Lin(t.hi, 0.9f));
            Shader.SetGlobalColor(PalUi, new Color(0.6867f, 0.7305f, 0.807f, 1));
            Shader.SetGlobalColor(PalThreat, Lin(WorldTheme.Threat, 0.75f));
            Shader.SetGlobalColor(PalThreatHi, Lin(WorldTheme.ThreatHi, 0.75f));
            // Tiefennebel im Weltmodus (WorldTheme.apply_env in Godot)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = t.sky;
            RenderSettings.fogEndDistance = t.fogEnd;
        }

        /// <summary>Godot WorldTheme.lin: sRGB-Farbe linear, mit Faktor k.</summary>
        static Color Lin(Color srgb, float k)
        {
            var c = srgb.linear;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>Godot Color.darkened.</summary>
        static Color Darkened(Color c, float k) => new Color(c.r * (1 - k), c.g * (1 - k), c.b * (1 - k), c.a);

        /// <summary>Godot Color.lightened.</summary>
        static Color Lightened(Color c, float k) => new Color(c.r + (1 - c.r) * k, c.g + (1 - c.g) * k, c.b + (1 - c.b) * k, c.a);
    }
}
