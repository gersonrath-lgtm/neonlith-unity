using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>
    /// Dauerhafter Fortschritt (SaveGame + Meta in Godot, erster Stand): Missionsrekorde mit Sieg, Sternen
    /// (Missions.stars: nach verlorenen Leben) und Kernsplittern, Freischaltung der nächsten Mission, gewählte
    /// Waffe, Linkshänder. Datei <c>progress.json</c> in <see cref="Application.persistentDataPath"/> (Godot: user://).
    /// Beschädigte Dateien werden beiseitegelegt (.corrupt) und durch Standardwerte ersetzt.
    /// </summary>
    public static class Progress
    {
        static JObject _data;
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "progress.json");

        public static JObject Data
        {
            get
            {
                if (_data != null) return _data;
                try
                {
                    if (File.Exists(Path)) _data = JObject.Parse(File.ReadAllText(Path));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Neonlith: Spielstand beschädigt, beiseitegelegt: " + ex.Message);
                    try { File.Move(Path, Path + ".corrupt"); } catch { }
                }
                if (_data == null) _data = Defaults();
                return _data;
            }
        }

        static JObject Defaults() => new JObject
        {
            ["version"] = 1,
            ["missions"] = new JObject(),
            ["shards"] = 0,
            ["shards_total"] = 0,
            ["trees"] = new JObject(),
            ["weapon"] = "pistol",
            ["left_handed"] = false,
        };
        // Meta: Kernsplitter je Mission (meta.gd)
        const int MissionFirst = 30, MissionFirstStep = 4, MissionStar = 8;
        const float RepeatMult = 0.6f;
        public const string Currency = "Kernsplitter";

        public static void Save()
        {
            try { File.WriteAllText(Path, Data.ToString()); }
            catch (Exception ex) { Debug.LogError("Neonlith: Speichern fehlgeschlagen: " + ex.Message); }
        }

        /// <summary>Sterne nach verlorenen Leben (Missions.stars): 3 bei höchstens 10 %, 2 bis 40 %, sonst 1.</summary>
        public static int Stars(int livesLost, int startLives)
        {
            var k = startLives > 0 ? livesLost / (float)startLives : 1f;
            return k <= 0.10f ? 3 : (k <= 0.40f ? 2 : 1);
        }

        /// <summary>
        /// Mission verbuchen (Meta.record_mission): Splitter je geschaffter Welle (1 + Kapitel; Wiederholung 60 %),
        /// erster Sieg 30 + 4·n, je neuem Stern 8, dazu Splitterernte (Kern) und Segenskarte. Liefert die Summe.
        /// </summary>
        public static int RecordMission(int n, int chapter, int wavesCleared, bool won, int livesLost, int startLives, float shardFactor = 1f)
        {
            var m = Data["missions"] as JObject ?? (JObject)(Data["missions"] = new JObject());
            var rec = m[n.ToString()] as JObject ?? new JObject { ["won"] = false, ["stars"] = 0, ["tries"] = 0, ["wins"] = 0 };
            var wins = rec["wins"]?.Value<int>() ?? 0;
            var first = won && wins == 0;
            var stars = won ? Stars(livesLost, startLives) : 0;
            var newStars = Math.Max(0, stars - (rec["stars"]?.Value<int>() ?? 0));
            var waves = wavesCleared * (1 + chapter);
            if (!first && wins > 0) waves = Mathf.RoundToInt(waves * RepeatMult);
            var firstBonus = first ? MissionFirst + MissionFirstStep * n : 0;
            var baseShards = waves + firstBonus + newStars * MissionStar;
            var bonus = Mathf.RoundToInt(baseShards * Game.SkillTrees.Mods("core").Get("shard_pct"));
            bonus += Mathf.RoundToInt((baseShards + bonus) * (shardFactor - 1f));
            var total = baseShards + bonus;
            rec["tries"] = (rec["tries"]?.Value<int>() ?? 0) + 1;
            if (won)
            {
                rec["won"] = true;
                rec["wins"] = wins + 1;
                rec["stars"] = Math.Max(rec["stars"]?.Value<int>() ?? 0, stars);
            }
            m[n.ToString()] = rec;
            Data["shards"] = Shards + total;
            Data["shards_total"] = (Data["shards_total"]?.Value<int>() ?? 0) + total;
            Save();
            return total;
        }

        // --- Fertigkeitsbäume (Meta: Ränge, Pfad, Kauf, Umskillen) --------------------------------------
        static JObject TreeState(string tree)
        {
            var trees = Data["trees"] as JObject ?? (JObject)(Data["trees"] = new JObject());
            var t = trees[tree] as JObject;
            if (t == null) { t = new JObject { ["path"] = "", ["ranks"] = new JObject() }; trees[tree] = t; }
            if (!(t["ranks"] is JObject)) t["ranks"] = new JObject();
            return t;
        }

        public static int Rank(string tree, string node) => (Data["trees"] as JObject)?[tree]?["ranks"]?[node]?.Value<int>() ?? 0;
        public static string ChosenPath(string tree) => (string)(Data["trees"] as JObject)?[tree]?["path"] ?? "";

        public static int TrunkPoints(string tree)
        {
            var n = 0;
            if (Game.SkillTrees.Trees.TryGetValue(tree, out var t)) foreach (var nd in t.nodes) if (nd.slot == "trunk") n += Rank(tree, nd.id);
            return n;
        }

        public static int SpentInTree(string tree, bool onlyPath)
        {
            var s = 0;
            if (!Game.SkillTrees.Trees.TryGetValue(tree, out var t)) return 0;
            foreach (var nd in t.nodes)
            {
                if (onlyPath && nd.slot == "trunk") continue;
                var r = Rank(tree, nd.id);
                for (var i = 0; i < r && i < nd.cost.Length; i++) s += nd.cost[i];
            }
            return s;
        }

        public static int NextCost(string tree, string node)
        {
            var nd = Game.SkillTrees.Node(tree, node);
            var r = Rank(tree, node);
            return nd == null || r >= nd.Ranks ? -1 : nd.cost[r];
        }

        /// <summary>Grund, warum ein Knoten nicht kaufbar ist ("" = kaufbar), Meta.buy_block.</summary>
        public static string BuyBlock(string tree, string node)
        {
            var nd = Game.SkillTrees.Node(tree, node);
            if (nd == null) return "Unbekannte Fertigkeit";
            var r = Rank(tree, node);
            if (r >= nd.Ranks) return "Höchster Rang erreicht";
            var t = Game.SkillTrees.Trees[tree];
            if (nd.slot != "trunk")
            {
                if (TrunkPoints(tree) < Game.SkillTrees.TrunkPoints) return $"Zuerst {Game.SkillTrees.TrunkPoints} Punkte im Stamm verteilen";
                var cp = ChosenPath(tree);
                if (cp.Length > 0 && cp != nd.slot) return "Anderer Pfad gewählt – Umskillen möglich";
                if (nd.index > 0)
                {
                    foreach (var p in t.nodes) if (p.slot == nd.slot && p.index == nd.index - 1 && Rank(tree, p.id) < p.Ranks) return $"Zuerst „{p.name}“ voll ausbauen";
                }
            }
            else if (nd.index == 1 && Rank(tree, t.nodes[0].id) < 1) return $"Zuerst „{t.nodes[0].name}“ lernen";
            if (Shards < nd.cost[r]) return "Nicht genug " + Currency;
            return "";
        }

        public static bool Buy(string tree, string node)
        {
            if (BuyBlock(tree, node).Length > 0) return false;
            var nd = Game.SkillTrees.Node(tree, node);
            var t = TreeState(tree);
            var r = Rank(tree, node);
            Data["shards"] = Shards - nd.cost[r];
            ((JObject)t["ranks"])[node] = r + 1;
            if (nd.slot != "trunk") t["path"] = nd.slot;
            Save();
            return true;
        }

        public static int RespecFee(string tree)
        {
            var spent = SpentInTree(tree, true);
            return Math.Min(spent, Math.Max(Game.SkillTrees.RespecMin, Mathf.RoundToInt(spent * Game.SkillTrees.RespecFee)));
        }

        /// <summary>Pfad zurücksetzen (Stamm bleibt), erstattet die Pfadpunkte abzüglich Gebühr.</summary>
        public static int Respec(string tree)
        {
            if (ChosenPath(tree).Length == 0) return 0;
            var refund = Math.Max(0, SpentInTree(tree, true) - RespecFee(tree));
            var t = TreeState(tree);
            var ranks = (JObject)t["ranks"];
            foreach (var nd in Game.SkillTrees.Trees[tree].nodes) if (nd.slot != "trunk") ranks.Remove(nd.id);
            t["path"] = "";
            Data["shards"] = Shards + refund;
            Save();
            return refund;
        }

        public static bool Won(int n) => (Data["missions"] as JObject)?[n.ToString()]?["won"]?.Value<bool>() ?? false;
        public static int StarsOf(int n) => (Data["missions"] as JObject)?[n.ToString()]?["stars"]?.Value<int>() ?? 0;
        /// <summary>Mission 1 ist immer offen, jede weitere nach dem Sieg der vorigen (Missions.unlocked).</summary>
        public static bool Unlocked(int n) => n <= 1 || Won(n - 1);
        public static int NextOpen() { for (var n = 1; n <= 20; n++) if (!Won(n)) return n; return 20; }
        /// <summary>Welten, deren letzte Mission gewonnen ist (für die Waffenfreischaltung: Mission 5, 10, 15).</summary>
        public static int WorldsDone() { var w = 0; for (var n = 5; n <= 20; n += 5) if (Won(n)) w++; return w; }

        public static string Weapon { get => (string)Data["weapon"] ?? "pistol"; set { Data["weapon"] = value; Save(); } }
        public static bool LeftHanded { get => Data["left_handed"]?.Value<bool>() ?? false; set { Data["left_handed"] = value; Save(); } }
        public static int Shards => Data["shards"]?.Value<int>() ?? 0;
    }
}
