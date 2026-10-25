using UnityEngine;

namespace Neonlith
{
    /// <summary>
    /// Die vier Welten der Kampagne (Farbverlauf hell nach dunkel), 1:1 aus
    /// <c>scripts/view3d/world_theme.gd</c> (Stand 4.99.65). Farben sind sRGB-Hexwerte wie in Godot.
    /// Gleiche Funktion = gleiche Farbe: neon (Kanten, Leuchtlinien), hi (Highlight), pad (Bauplätze),
    /// arrow (Laufpfeile), threat (Gegner, immer Rot), sky/sky2 (Himmel, Nebel), ground/ground2 (Metall).
    /// </summary>
    [System.Serializable]
    public sealed class WorldTheme
    {
        public string id;
        public string name;
        public string mood;
        public Color sky, sky2, ground, ground2, neon, hi, pad, tower, arrow;
        /// <summary>Welt IV: dunkleres Rot der Linien; sonst leer (Color.clear) und THREAT gilt.</summary>
        public Color red;
        public Vector3 skyTint;
        public float skyDesat;
        public float ambient;
        public float skyEnergy;
        public float fogEnd;
        public float fogDensity;

        public static readonly Color Threat = Hex("FF4D57");
        public static readonly Color ThreatHi = Hex("FF9B85");
        public const int LevelsPerWorld = 5;

        public static readonly WorldTheme[] Themes =
        {
            new WorldTheme
            {
                id = "w1", name = "Lichtfeld", mood = "hell, clean, offen, silbergrau und cyan",
                sky = Hex("8A9099"), sky2 = Hex("C8CCD2"), ground = Hex("2B3036"), ground2 = Hex("41474F"),
                neon = Hex("4FE8FF"), hi = Hex("E2FBFF"), pad = Hex("62EEFF"), tower = Hex("4FE8FF"), arrow = Hex("4FE8FF"),
                red = Color.clear, skyTint = new Vector3(1.0f, 1.01f, 1.04f), skyDesat = 0.85f,
                ambient = 0.9f, skyEnergy = 1.0f, fogEnd = 110f, fogDensity = 0.5f,
            },
            new WorldTheme
            {
                id = "w2", name = "Kernstation", mood = "klar, technisch, anthrazit und kühles Weiß",
                sky = Hex("121417"), sky2 = Hex("2A2E34"), ground = Hex("17191C"), ground2 = Hex("25282D"),
                neon = Hex("DCE6EE"), hi = Hex("FFFFFF"), pad = Hex("C4D0DA"), tower = Hex("DCE6EE"), arrow = Hex("DCE6EE"),
                red = Color.clear, skyTint = new Vector3(0.98f, 1.0f, 1.03f), skyDesat = 0.92f,
                ambient = 0.55f, skyEnergy = 1.0f, fogEnd = 52f, fogDensity = 1.0f,
            },
            new WorldTheme
            {
                id = "w3", name = "Tiefe", mood = "dunkel, Kontrast, Silhouette, Glut",
                sky = Hex("0A0706"), sky2 = Hex("2A1408"), ground = Hex("131110"), ground2 = Hex("201C1A"),
                neon = Hex("FF8A2E"), hi = Hex("FFD2A0"), pad = Hex("FFA04D"), tower = Hex("FF8A2E"), arrow = Hex("FF8A2E"),
                red = Color.clear, skyTint = new Vector3(1.45f, 0.95f, 0.6f), skyDesat = 0.92f,
                ambient = 0.4f, skyEnergy = 1.0f, fogEnd = 44f, fogDensity = 1.0f,
            },
            new WorldTheme
            {
                id = "w4", name = "Endkern", mood = "bedrohlich, Schwarz und Rot",
                sky = Hex("0A0304"), sky2 = Hex("3A080B"), ground = Hex("141012"), ground2 = Hex("241B1D"),
                neon = Hex("FF3A30"), hi = Hex("FFB09A"), pad = Hex("FF5048"), tower = Hex("FF3A30"), arrow = Hex("FF3A30"),
                red = Hex("FF4D57"), skyTint = new Vector3(2.0f, 0.5f, 0.45f), skyDesat = 0.95f,
                ambient = 0.45f, skyEnergy = 1.2f, fogEnd = 56f, fogDensity = 0.85f,
            },
        };

        /// <summary>Aktive Welt 0..3. Standard Welt 2 (Kernstil: Menü, Hangar, Testkarten), wie in Godot.</summary>
        public static int Index = 1;

        public static WorldTheme Current => Themes[Mathf.Clamp(Index, 0, Themes.Length - 1)];

        /// <summary>Welt einer Mission 1..20 als Index 0..3 (fünf Missionen je Welt; 0 oder negativ = Welt 2).</summary>
        public static int WorldOfMission(int mission)
        {
            if (mission <= 0) return 1;
            return Mathf.Clamp((mission - 1) / LevelsPerWorld, 0, Themes.Length - 1);
        }

        /// <summary>Himmelstextur der Welt: Art/Textures/space_sky_w1..w4.png.</summary>
        public string SkyTextureName => "space_sky_" + id;

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
        }
    }
}
