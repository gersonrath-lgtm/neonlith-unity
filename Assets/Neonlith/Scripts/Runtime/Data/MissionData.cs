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

    /// <summary>Bauplatz (Damm) oder Sperrplatz (Fahrbahnmitte) in Metern. pathDist: Wegstrecke je Weg (Sperren).</summary>
    public sealed class BuildPad
    {
        public int id;
        public Vector3 position;
        public float yawDeg;           // Laufrichtung (Sperren liegen quer dazu)
        public bool trap;
        public readonly Dictionary<int, float> pathDist = new Dictionary<int, float>();
        public Game.Tower tower;
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
        public int gold = Balance.StartGold;
        public float bossHp = 0.9f, summonMult = 1f, padR;
        public readonly List<string> towers = new List<string>();
        public readonly List<string> traps = new List<string>();
        public readonly List<BuildPad> pads = new List<BuildPad>();
        public const int Module = 4;           // BridgeLevel.MODULE
        public const int EdgeBlock = 2;        // BridgeLevel.EDGE_BLOCK
        public const float PadOut = 0.09f;     // BridgeLevel.PAD_OUT

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
            m.gold = (int?)md["gold"] ?? Balance.StartGold;
            m.bossHp = (float?)md["boss_hp"] ?? 0.9f;
            m.summonMult = (float?)md["summon"] ?? 1f;
            if (md["towers"] is JArray tw) foreach (var t in tw) m.towers.Add((string)t);
            if (md["traps"] is JArray tr) foreach (var t in tr) m.traps.Add((string)t);
            if (m.towers.Count == 0) m.towers.AddRange(Balance.DefenseOrder);

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
            m.padR = (float?)bridge["pad_r"] ?? 0f;
            m.BuildPads(bastionCell);

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

        /// <summary>
        /// Bauplätze aus den Läufen (BridgeLevel: Laufmodul von MODULE Feldern ab dem Anker: Ansatzfeld, Bauplatzpaar,
        /// Pfeil, Bauplatzpaar, freies Feld; Zuläufe vom Rand zählen vom Knoten am Ende rückwärts). Keine Plätze am
        /// Kartenrand, neben Wänden, auf Knotenplatten und weiter als pad_r vom Kern. Sperrplätze: freie Felder der
        /// Fahrbahnmitte ("plain"-Reihen) mit Wegstrecke je Weg.
        /// </summary>
        void BuildPads(Vector2 bastionCell)
        {
            var seen = new HashSet<string>();
            var walls = new HashSet<string>();
            var nodes = new HashSet<string>();
            foreach (var cells in pathCells)
            {
                for (var i = 0; i + 1 < cells.Length; i++)
                    if (Dir(cells[i], cells[i + 1]) == Vector3.zero) walls.Add(Key(cells[i]));
                for (var i = 1; i + 1 < cells.Length; i++)
                    if (!walls.Contains(Key(cells[i]))) nodes.Add(Key(cells[i]));
            }
            var id = 0;
            for (var pi = 0; pi < pathCells.Count; pi++)
            {
                var cells = pathCells[pi];
                var along = 0f;                                     // Wegstrecke in Metern (mit Zulauf)
                along += EntryLead * Balance.FieldM;
                for (var i = 0; i + 1 < cells.Length; i++)
                {
                    var a = cells[i]; var b = cells[i + 1];
                    var dv = Dir(a, b);
                    var segStart = along;
                    along += Vector3.Distance(ToM(a), ToM(b));
                    if (dv == Vector3.zero) continue;
                    var key = Key(a) + ">" + Key(b);
                    var rkey = Key(b) + ">" + Key(a);
                    var firstUse = !seen.Contains(key) && !seen.Contains(rkey);
                    seen.Add(key);
                    var isNodeA = nodes.Contains(Key(a)); var isNodeB = nodes.Contains(Key(b));
                    var isBastionB = Mathf.Approximately(b.x, bastionCell.x) && Mathf.Approximately(b.z, bastionCell.y);
                    var s = isNodeA ? a + dv * (BastionHalf + 1f) : a;
                    var e = isNodeB ? b - dv * (BastionHalf + 1f) : (isBastionB ? b - dv * (BastionHalf + 1f) : b);
                    var count = Mathf.RoundToInt(Mathf.Max(Mathf.Abs(e.x - s.x), Mathf.Abs(e.z - s.z))) + 1;
                    if (count < 2) continue;
                    var anchorEnd = (!isNodeA && !walls.Contains(Key(a))) && (isNodeB || walls.Contains(Key(b)) || isBastionB);
                    var perp = new Vector3(dv.z, 0f, -dv.x);
                    for (var k = 0; k < count; k++)
                    {
                        var c = s + dv * k;
                        var h = Mathf.Lerp(a.y, b.y, count > 1 ? k / (float)(count - 1) : 0f);
                        var mk = ModuleKind(anchorEnd ? count - 1 - k : k);
                        var atEdge = c.x < EdgeBlock || c.z < EdgeBlock || c.x >= gridSize - EdgeBlock || c.z >= gridSize - EdgeBlock;
                        var tooFar = padR > 0f && Vector2.Distance(new Vector2(c.x, c.z), bastionCell) > padR;
                        var nearWall = false;
                        foreach (var w in walls) { var wc = Unkey(w); if (Mathf.Max(Mathf.Abs(c.x - wc.x), Mathf.Abs(c.z - wc.y)) <= 1f) { nearWall = true; break; } }
                        // Sperrplatz in der Fahrbahnmitte (Wegstrecke bis zur Feldmitte)
                        var distM = segStart + Vector3.Distance(ToM(a), ToM(new Vector3(c.x, h, c.z)));
                        if (mk == "plain" && k > 0 && firstUse && !atEdge && !nearWall)
                        {
                            var tp = new BuildPad { id = id++, position = ToM(new Vector3(c.x, h, c.z)), yawDeg = YawOf(dv), trap = true };
                            tp.pathDist[pi] = distM;
                            pads.Add(tp);
                        }
                        else if (mk == "plain" && !firstUse)
                        {
                            // geteilter Lauf: Sperrplatz des ersten Wegs um diesen Weg ergänzen
                            foreach (var p in pads) if (p.trap && (p.position - ToM(new Vector3(c.x, h, c.z))).sqrMagnitude < 0.01f) p.pathDist[pi] = distM;
                        }
                        if (mk != "pad" || !firstUse || atEdge || tooFar || nearWall) continue;
                        foreach (var side in new[] { -1f, 1f })
                        {
                            var off = perp * side * ((DeckWidth * 0.5f - 0.5f + PadOut) * Balance.FieldM);
                            var pos = ToM(new Vector3(c.x, h + BermTop, c.z)) + off;
                            pads.Add(new BuildPad { id = id++, position = pos, yawDeg = YawOf(dv), trap = false });
                        }
                    }
                }
            }
        }

        static string ModuleKind(int idx)
        {
            if (idx <= 0) return "plain";
            switch ((idx - 1) % Module) { case 0: case 2: return "pad"; case 1: return "arrow"; }
            return "plain";
        }

        static string Key(Vector3 c) => $"{Mathf.RoundToInt(c.x)},{Mathf.RoundToInt(c.z)}";
        static Vector2 Unkey(string k) { var p = k.Split(','); return new Vector2(int.Parse(p[0]), int.Parse(p[1])); }
        static float YawOf(Vector3 dv) => Mathf.Atan2(dv.x, dv.z) * Mathf.Rad2Deg;

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
