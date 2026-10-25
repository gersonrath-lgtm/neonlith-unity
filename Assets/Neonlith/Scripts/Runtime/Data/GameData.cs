using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>
    /// Zugriff auf die Spieldaten, die 1:1 aus dem Godot-Projekt übernommen sind und dort von
    /// <c>tools/art/levels/generate_missions.py</c> erzeugt werden (nie von Hand ändern, auch hier nicht:
    /// nach einer Änderung in Godot <c>Tools/unity_copy_assets.py</c> neu laufen lassen).
    /// Die Dateien liegen als TextAsset unter <c>Assets/Neonlith/Resources/Data</c>:
    /// <list type="bullet">
    /// <item><c>campaign.json</c>: Kapitel, 20 Missionen, Wellen, Funk, Wetter, Schwierigkeit (Missions in Godot)</item>
    /// <item><c>levels/missions/mission_NN.json</c>: Karte je Mission (Raster, Wege, Brücke; MapData in Godot)</item>
    /// <item><c>levels/strips/*.json</c>: Brückenbausätze (KlarBridge/FogBridge, docs/BRUECKEN_BAUKASTEN.md)</item>
    /// <item><c>hangar.json</c>: Halle (Hangar)</item>
    /// <item><c>config/*.json</c>: die ehemaligen .tres-Ressourcen (WeaponConfig, EssenceConfig, ...)</item>
    /// </list>
    /// Die Daten haben gemischte Listen (z. B. <c>"boss": ["vey", "Anomalie: Schwebeträgerin", 0.12]</c>),
    /// deshalb Newtonsoft.Json statt JsonUtility.
    /// </summary>
    public static class GameData
    {
        const string Root = "Data/";
        static readonly Dictionary<string, JObject> Cache = new Dictionary<string, JObject>();

        public static JObject Campaign => Load("campaign");
        public static JObject Hangar => Load("hangar");

        /// <summary>Karte einer Mission 1..20.</summary>
        public static JObject Mission(int n) => Load($"levels/missions/mission_{n:00}");

        /// <summary>Brückenbausatz, z. B. "moon_bridge" oder "moon_road".</summary>
        public static JObject Strip(string id) => Load("levels/strips/" + id);

        /// <summary>Konfiguration aus data/*.tres, z. B. Config("weapon")["mag_size"].</summary>
        public static JObject Config(string id) => Load("config/" + id);

        /// <summary>Anzahl der Missionen in campaign.json.</summary>
        public static int MissionCount
        {
            get
            {
                var m = Campaign?["missions"] as JArray;
                return m?.Count ?? 0;
            }
        }

        /// <summary>Godot-Pfade in den Daten ("res://data/levels/strips/moon_bridge.json") auf Resources abbilden.</summary>
        public static string ResourcePath(string godotPath)
        {
            const string prefix = "res://data/";
            if (string.IsNullOrEmpty(godotPath) || !godotPath.StartsWith(prefix)) return godotPath;
            var p = godotPath.Substring(prefix.Length);
            if (p.EndsWith(".json")) p = p.Substring(0, p.Length - 5);
            return p;
        }

        public static JObject Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var asset = Resources.Load<TextAsset>(Root + name);
            if (asset == null)
            {
                Debug.LogError($"Neonlith: Datendatei fehlt: Resources/{Root}{name}.json");
                return null;
            }
            var obj = JObject.Parse(asset.text);
            Cache[name] = obj;
            return obj;
        }

        public static void ClearCache() => Cache.Clear();
    }
}
