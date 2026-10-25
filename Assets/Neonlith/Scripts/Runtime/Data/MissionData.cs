using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>Eine Gegnergruppe einer Welle (campaign.json: t, n, p, i, d).</summary>
    public sealed class WaveGroup
    {
        public string type;
        public int count;
        public int path;     // -1 = reihum (WaveManager: (i + gi) % Wege)
        public float interval, delay;
        public string elite;
    }

    public sealed class Wave
    {
        public string title;
        public int reward;
        public bool boss;
        public readonly List<WaveGroup> groups = new List<WaveGroup>();
    }

    /// <summary>
    /// Eine Mission 1..20 aus <c>campaign.json</c> und ihrer Karte <c>levels/missions/mission_NN.json</c>,
    /// umgerechnet in Meter (Unity: X nach rechts, Z nach vorn, Y nach oben; Godot-Felder [x, z, h] mal 2 m).
    /// Gegenstück zu Missions + BridgeLevel + MapData in Godot, reduziert auf das, was der erste Stand braucht:
    /// Wege, Kern, Standdeck, Wellen, Leben, Funk.
    /// </summary>
    public sealed class MissionData
    {
        public const float EntryLead = 6.68f;      // BridgeLevel.ENTRY_LEAD: Zulauf vor dem Eingang (Felder)
        public const float BastionHalf = 2f;       // BridgeLevel.BASTION_HALF: Kernplatte 5 × 5 Felder
        public const float BermTop = 0.525f;       // Dammkrone über der Fahrbahn (Felder)
        public const float GunRise = 1.6f;         // FogBridge.GUN_RISE: Standdeck über den Dämmen (Felder)
        public const float DeckWidth = 5f;         // Spalten "BRRRB": Damm, drei Fahrbahnfelder, Damm
        public const float RoadWidth = 3f;
        public const float PlatformR = 2.5f;       // GunTower.PLATFORM_R: Umkreis des Achtecks (Felder)
        public const float PlatformEdge = 0.15f;   // GunTower.EDGE: Abstand zur Kante

        public int number;
        public string name = "", text = "", commsStart = "", commsEnd = "";
        public int lives = Balance.StartLives;
        public int chapter;
        public int gridSize;
        public bool arena;
        /// <summary>Wege der Gegner in Metern, vom Zulauf (vor dem Eingang) bis zur Kante der Kernplatte.</summary>
        public readonly List<Vector3[]> paths = new List<Vector3[]>();
        /// <summary>Die Wegpunkte aus der Karte in Feldern (für die Brücke: Läufe, Knoten, Höhen).</summary>
        public readonly List<Vector3[]> pathCells = new List<Vector3[]>();
        public Vector3 core;        // Mitte der Kernplatte (m), Höhe = Fahrbahn
        public Vector3 deckTop;     // Mitte des Standdecks (m), Höhe = Standfläche
        public readonly List<Wave> waves = new List<Wave>();
        public float[] hpIndex = new float[0];
        public float[] hpWave = new float[0];

        public static MissionData Load(int n)
        {
            var md = GameData.Campaign?["missions"] is JArray arr && n >= 1 && n <= arr.Count ? (JObject)arr[n - 1] : null;
            var map = GameData.Mission(n);
            if (md == null || map == null)
            {
                Debug.LogError($"Neonlith: Mission {n} nicht ladbar");
                return null;
            }
            var m = new MissionData { number = n };
            m.name = (string)md["name"] ?? "";
            m.text = (string)md["text"] ?? "";
            m.lives = (int?)md["lives"] ?? Balance.StartLives;
            m.chapter = (int?)md["chapter"] ?? 0;
            m.commsStart = (string)md["comms"]?["start"] ?? "";
            m.commsEnd = (string)md["comms"]?["end"] ?? "";
            m.gridSize = (int?)map["grid_size"] ?? 41;

            var bridge = (JObject)map["bridge"];
            var bastionCell = Cell(bridge["bastion"]);
            m.arena = (bool?)bridge["arena"] ?? false;
            var bastionH = 0f;
            foreach (var p in (JArray)bridge["paths"])
            {
                var cells = new List<Vector3>();
                foreach (var pt in (JArray)p)
                {
                    var a = (JArray)pt;
                    cells.Add(new Vector3((float)a[0], a.Count > 2 ? (float)a[2] : 0f, (float)a[1]));
                }
                if (cells.Count < 2) continue;
                m.pathCells.Add(cells.ToArray());
                var last = cells[cells.Count - 1];
                if (Mathf.Approximately(last.x, bastionCell.x) && Mathf.Approximately(last.z, bastionCell.y))
                    bastionH = last.y;
                // Weg in Metern: Zulauf vor dem Eingang, Ende an der Kante der Kernplatte
                var pts = new List<Vector3>();
                var d0 = Dir(cells[0], cells[1]);
                pts.Add(ToM(cells[0] - d0 * EntryLead));
                for (var i = 0; i < cells.Count; i++)
                {
                    var c = cells[i];
                    if (i == cells.Count - 1 && Mathf.Approximately(c.x, bastionCell.x) && Mathf.Approximately(c.z, bastionCell.y))
                        c -= Dir(cells[i - 1], c) * (BastionHalf + 0.5f);
                    pts.Add(ToM(c));
                }
                m.paths.Add(pts.ToArray());
            }
            m.core = ToM(new Vector3(bastionCell.x, bastionH, bastionCell.y));
            m.deckTop = m.core + Vector3.up * ((BermTop + GunRise) * Balance.FieldM);

            foreach (var w in (JArray)md["waves"])
            {
                var wave = new Wave
                {
                    title = (string)w["title"] ?? "",
                    reward = (int?)w["reward"] ?? 0,
                    boss = (bool?)w["boss"] ?? false,
                };
                foreach (var g in (JArray)w["groups"])
                {
                    wave.groups.Add(new WaveGroup
                    {
                        type = (string)g["t"] ?? "mist_walker",
                        count = (int?)g["n"] ?? 0,
                        path = g["p"] != null ? (int)g["p"] : -1,
                        interval = (float?)g["i"] ?? 1f,
                        delay = (float?)g["d"] ?? 0f,
                        elite = (string)g["e"] ?? "",
                    });
                }
                m.waves.Add(wave);
            }
            var rank = (int?)md["rank"] ?? n;
            Balance.MissionWaveHp(n, m.chapter, rank, md["rank"] != null, (float?)md["diff"] ?? 1f, m.waves.Count, out m.hpIndex, out m.hpWave);
            return m;
        }

        public static Vector3 ToM(Vector3 cell) => new Vector3(cell.x * Balance.FieldM, cell.y * Balance.FieldM, cell.z * Balance.FieldM);

        static Vector2 Cell(JToken t)
        {
            var a = t as JArray;
            return a != null && a.Count >= 2 ? new Vector2((float)a[0], (float)a[1]) : new Vector2(-999, -999);
        }

        /// <summary>Richtung in der Ebene (Felder), senkrechte Wandabschnitte haben keine.</summary>
        public static Vector3 Dir(Vector3 a, Vector3 b)
        {
            var d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            return d.sqrMagnitude < 1e-6f ? Vector3.zero : d.normalized;
        }

        /// <summary>Laufbereich auf dem Standdeck: Scheibe um die Mitte (Meter), GunTower.stand_area.</summary>
        public float StandRadiusM => (PlatformR * Mathf.Cos(Mathf.PI / 8f) - PlatformEdge) * Balance.FieldM;
    }
}
