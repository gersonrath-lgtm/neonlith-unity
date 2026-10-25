using System.Collections.Generic;
using Neonlith.Data;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>Knoten eines Fertigkeitsbaums (SkillTrees.nodes): Stamm oder Pfad A/B, Ränge mit Kosten in Kernsplittern.</summary>
    public sealed class SkillNode
    {
        public string id, name, text, slot;   // slot: "trunk", "A", "B"
        public int index;
        public int[] cost;
        public int Ranks => cost.Length;
        public Dictionary<string, float> mods = new Dictionary<string, float>();
    }

    public sealed class SkillTree
    {
        public string id, building, title, text;
        public string pathAName, pathAText, pathBName, pathBText;
        public List<SkillNode> nodes = new List<SkillNode>();
    }

    /// <summary>
    /// Fertigkeitsbäume der Basis (scripts/game/skill_trees.gd, 1:1): je Verteidigungsturm ein Baum, dazu Kern und
    /// Geschütz. Stamm mit zwei Knoten, ab 3 Stammpunkten ein Pfad A oder B mit drei Knoten. Wirkungen ("mods")
    /// addieren sich je Rang und werden in <see cref="Apply"/> auf die Turmwerte gerechnet (auch die Segenskarten
    /// laufen hier durch). Der Zustand (Ränge, Pfad, Splitter) liegt in <see cref="Progress"/>.
    /// </summary>
    public static class SkillTrees
    {
        public const int TrunkPoints = 3;
        public static readonly int[][] TrunkCost = { new[] { 4, 6, 8 }, new[] { 8, 12 } };
        public static readonly int[][] PathCost = { new[] { 12, 16 }, new[] { 20, 26 }, new[] { 40 } };
        public const float RespecFee = 0.2f; public const int RespecMin = 5;
        public static readonly string[] Flags = { "fragment", "freeze", "brittle_add", "wildfire", "thermo_add", "overload", "stun_chain", "execute", "aftershock", "permafrost" };
        public static readonly string[] Order = { "core", "ship", "arrow", "ice", "fire", "chain", "sniper", "mortar" };
        public static readonly Dictionary<string, SkillTree> Trees = Build();

        static SkillNode N(string id, string name, string text, params object[] mods)
        {
            var n = new SkillNode { id = id, name = name, text = text };
            for (var i = 0; i + 1 < mods.Length; i += 2) n.mods[(string)mods[i]] = System.Convert.ToSingle(mods[i + 1]);
            return n;
        }

        static SkillTree T(string id, string building, string title, string text, SkillNode[] trunk, string aName, string aText, SkillNode[] a, string bName, string bText, SkillNode[] b)
        {
            var t = new SkillTree { id = id, building = building, title = title, text = text, pathAName = aName, pathAText = aText, pathBName = bName, pathBText = bText };
            for (var i = 0; i < trunk.Length; i++) { trunk[i].slot = "trunk"; trunk[i].index = i; trunk[i].cost = TrunkCost[i]; t.nodes.Add(trunk[i]); }
            for (var i = 0; i < a.Length; i++) { a[i].slot = "A"; a[i].index = i; a[i].cost = PathCost[i]; a[i].id = "A_" + a[i].id; t.nodes.Add(a[i]); }
            for (var i = 0; i < b.Length; i++) { b[i].slot = "B"; b[i].index = i; b[i].cost = PathCost[i]; b[i].id = "B_" + b[i].id; t.nodes.Add(b[i]); }
            return t;
        }

        static Dictionary<string, SkillTree> Build()
        {
            var d = new Dictionary<string, SkillTree>();
            d["core"] = T("core", "Kernwarte", "Kern", "Allgemein, gilt in jeder Mission: Startgold, Leben, Einkommen und alle Türme.",
                new[] { N("reserve", "Kernreserve", "+15 Startgold je Rang", "start_gold", 15), N("shield", "Schildkern", "+2 Leben je Rang", "lives", 2) },
                "Ertrag", "Mehr Essenz und mehr Kernsplitter.",
                new[] { N("spring", "Goldader", "Generatoren: +15 % Einkommen je Rang", "income_pct", 0.15), N("harvest", "Splitterernte", "+10 % Kernsplitter je Rang", "shard_pct", 0.1), N("bounty", "Frühstart-Prämie", "Früher Wellenstart bringt doppeltes Bonusgold, Wellenbelohnungen +10 %", "early_mult", 1.0, "reward_pct", 0.1) },
                "Bollwerk", "Alle Türme stärker und ein zweites Herz.",
                new[] { N("chord", "Feuerleitung", "Alle Türme: +5 % Schaden je Rang", "tower_dmg", 0.05), N("field", "Weite Sicht", "Alle Türme: +6 % Reichweite je Rang", "tower_range", 0.06), N("heart", "Zweites Herz", "Einmal je Mission: fällt das Leben auf 0, bleibt der Kern mit 5 Leben bestehen", "second_heart", 5) });
            d["ship"] = T("ship", "Hangar", "Gewehr", "Deine Waffe in der Hand: Schaden und Feuerrate.",
                new[] { N("barrels", "Kalibrierte Läufe", "Gewehr: +8 % Schaden je Rang", "ship_dmg", 0.08), N("coolant", "Kühlmantel", "Gewehr: +15 % Magazin je Rang", "ship_heat", 0.15) },
                "Feuersturm", "Mehr Feuer, länger am Abzug.",
                new[] { N("rotor", "Schnellrotor", "Gewehr: +12 % Feuerrate je Rang", "ship_rate", 0.12), N("exchanger", "Wärmetauscher", "Gewehr: Nachladen 15 % schneller je Rang", "ship_drain", -0.15), N("twin", "Doppelläufe", "Gewehr: +50 % Schaden", "ship_twin", 1.0) },
                "Bollwerk", "Härter im Nahkampf.",
                new[] { N("plates", "Panzerplatten", "+1 Leben am Kern je Rang", "gun_armor", 4.0), N("workshop", "Feldwerkstatt", "Nach jeder Welle +1 Leben je Rang", "gun_regen", 1.0), N("retaliate", "Vergeltung", "Ladeschuss +50 % Schaden", "gun_retaliate", 1.0) });
            d["arrow"] = T("arrow", "Gatling-Werk", "Gatling", "",
                new[] { N("calib", "Kalibrierung", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Durchschlag", "Schwere Bolzen gegen Rüstung.",
                new[] { N("heavy", "Schwere Bolzen", "+15 % Schaden je Rang", "dmg", 0.15), N("pierce", "Panzerbrecher", "Ignoriert 20 % Rüstung je Rang", "pierce", 0.2), N("frag", "Splitterbolzen", "Treffer splittern: 50 % Schaden am nächsten Gegner dahinter", "fragment", 0.5) },
                "Schnellfeuer", "Mehr Bolzen, mehr Ziele.",
                new[] { N("rapid", "Schnellspanner", "+10 % Feuerrate je Rang", "rate", 0.1), N("reach", "Lange Schiene", "+8 % Reichweite je Rang", "range", 0.08), N("triple", "Dreifachschiene", "Trifft ein zusätzliches Ziel", "targets_add", 1) });
            d["ice"] = T("ice", "Kryo-Labor", "Kryo-Blaster", "",
                new[] { N("calib", "Kaltlicht", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Tiefkälte", "Stärkere und längere Verlangsamung.",
                new[] { N("deep", "Tiefkälte", "+6 % Verlangsamung je Rang", "slow", 0.06), N("linger", "Nachfrost", "+20 % Dauer der Verlangsamung je Rang", "slow_time_pct", 0.2), N("freeze", "Schockfrost", "12 % Chance, ein Ziel 0,7 s einzufrieren (nicht den Boss)", "freeze", 0.12) },
                "Kältewelle", "Frost trifft alle Gegner im Umkreis und macht sie verwundbar.",
                new[] { N("shard", "Scharfe Kanten", "+15 % Schaden je Rang", "dmg", 0.15), N("burst", "Kältekranz", "+0,35 Felder Flächenradius je Rang", "splash_add", 0.35), N("perma", "Permafrost", "Getroffene Gegner nehmen 15 % mehr Schaden von allen Türmen", "permafrost", 0.15) });
            d["fire"] = T("fire", "Plasmawerk", "Plasmawerfer", "",
                new[] { N("calib", "Heiße Kammer", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Zersetzung", "Heißerer Brand, der Rüstung zerfrisst.",
                new[] { N("ember", "Weißglut", "+15 % Brandschaden je Rang", "burn_pct", 0.15), N("long", "Rüstungsfresser", "Brennende Gegner verlieren 3 weitere Rüstung je Rang", "shred_add", 3.0), N("wild", "Flächenbrand", "Brennende Gegner explodieren beim Tod (30 % Schaden im Umkreis)", "wildfire", 0.3) },
                "Explosion", "Größere und härtere Einschläge.",
                new[] { N("blast", "Druckwelle", "+10 % Schaden je Rang", "dmg", 0.1), N("radius", "Weiter Kegel", "+10 % Explosionsradius je Rang", "splash_pct", 0.1), N("thermo", "Thermoschock+", "Thermoschock: +100 % statt +50 % Aufprallschaden", "thermo_add", 0.5) });
            d["chain"] = T("chain", "Tesla-Turm", "Tesla-Spule", "",
                new[] { N("calib", "Hochspannung", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Überladung", "Wuchtige Blitze.",
                new[] { N("surge", "Stromstoß", "+10 % Schaden je Rang", "dmg", 0.1), N("keep", "Leitfähigkeit", "Jeder Sprung verliert 5 % weniger Kraft je Rang", "falloff_add", 0.05), N("over", "Blitzschlag", "20 % Chance auf doppelten Schaden (ganzer Blitz)", "overload", 0.2) },
                "Netz", "Mehr Sprünge, größere Reichweite.",
                new[] { N("jump", "Verzweigung", "+1 Sprung je Rang", "jumps_add", 1), N("arc", "Lange Bögen", "+15 % Sprungweite je Rang", "jump_range_pct", 0.15), N("stun", "Lähmung", "Getroffene Gegner werden 0,8 s um 25 % verlangsamt", "stun_chain", 0.25) });
            d["sniper"] = T("sniper", "Railgun-Stand", "Railgun", "",
                new[] { N("calib", "Kimme und Korn", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Kopfschuss", "Kritische Treffer und Hinrichtung.",
                new[] { N("power", "Schwerer Kern", "+15 % Schaden je Rang", "dmg", 0.15), N("crit", "Schwachstelle", "10 % Chance auf doppelten Schaden je Rang", "crit", 0.1), N("exec", "Hinrichtung", "Gegner unter 15 % Leben werden sofort vernichtet (nicht der Boss)", "execute", 0.15) },
                "Späher", "Schneller nachladen, weiter sehen.",
                new[] { N("reload", "Schnellladung", "+10 % Feuerrate je Rang", "rate", 0.1), N("reach", "Fernoptik", "+8 % Reichweite je Rang", "range", 0.08), N("through", "Durchschuss", "Ignoriert 50 % Rüstung auf jeder Stufe", "pierce", 0.5) });
            d["mortar"] = T("mortar", "Raketensilo", "Raketenwerfer", "",
                new[] { N("calib", "Treibladung", "+8 % Schaden je Rang", "dmg", 0.08), N("series", "Serienbau", "−8 % Bau- und Ausbaukosten je Rang", "cost", -0.08) },
                "Schwere Ladung", "Härtere Einschläge bis zum Rand.",
                new[] { N("heavy", "Sprengkern", "+10 % Schaden je Rang", "dmg", 0.1), N("edge", "Gleichmäßige Wucht", "+10 % Randschaden je Rang", "edge_add", 0.1), N("quake", "Nachbeben", "Einschläge verlangsamen 1,5 s um 25 %", "aftershock", 0.25) },
                "Sperrfeuer", "Mehr Granaten.",
                new[] { N("reload", "Ladekette", "+10 % Feuerrate je Rang", "rate", 0.1), N("radius", "Streukegel", "+10 % Explosionsradius je Rang", "splash_pct", 0.1), N("twin", "Zwillingsrohr", "Feuert zwei Granaten auf verschiedene Ziele", "targets_add", 1) });
            return d;
        }

        public static SkillNode Node(string tree, string id) { if (!Trees.TryGetValue(tree, out var t)) return null; foreach (var n in t.nodes) if (n.id == id) return n; return null; }

        /// <summary>Summe der Wirkungen eines Baums aus den gelernten Rängen (Meta.mods).</summary>
        public static Dictionary<string, float> Mods(string tree)
        {
            var out_ = new Dictionary<string, float>();
            if (!Trees.TryGetValue(tree, out var t)) return out_;
            foreach (var n in t.nodes)
            {
                var r = Progress.Rank(tree, n.id);
                if (r <= 0) continue;
                foreach (var kv in n.mods) out_[kv.Key] = out_.Get(kv.Key) + kv.Value * r;
            }
            return out_;
        }

        /// <summary>Wirksame Turmwerte: Rohwerte der Stufe mit Fertigkeiten, Segenskarten und Kern (Balance.stats + SkillTrees.apply).</summary>
        public static Dictionary<string, float> Effective(string kind, int level)
        {
            var st = new Dictionary<string, float>(Balance.Stats(kind, level));
            var m = Mods(kind);
            foreach (var kv in Boons.Mods(kind)) m[kv.Key] = m.Get(kv.Key) + kv.Value;
            Apply(kind, st, m, Mods("core"));
            return st;
        }

        public static void Apply(string kind, Dictionary<string, float> st, Dictionary<string, float> m, Dictionary<string, float> core)
        {
            if (st.ContainsKey("damage")) st["damage"] *= 1f + m.Get("dmg");
            if (st.ContainsKey("cooldown")) st["cooldown"] /= 1f + m.Get("rate");
            if (st.ContainsKey("range")) st["range"] *= 1f + m.Get("range");
            if (st.ContainsKey("splash") && (st["splash"] > 0f || m.ContainsKey("splash_add"))) st["splash"] = st["splash"] * (1f + m.Get("splash_pct")) + m.Get("splash_add");
            if (st.ContainsKey("slow")) st["slow"] += m.Get("slow");
            if (st.ContainsKey("slow_time")) st["slow_time"] = st["slow_time"] * (1f + m.Get("slow_time_pct")) + m.Get("slow_time_add");
            if (st.ContainsKey("burn_dps")) st["burn_dps"] *= 1f + m.Get("burn_pct");
            if (st.ContainsKey("burn_time")) st["burn_time"] += m.Get("burn_time_add");
            if (st.ContainsKey("shred")) st["shred"] += m.Get("shred_add");
            if (st.ContainsKey("damage") && core.ContainsKey("tower_dmg")) st["damage"] *= 1f + core["tower_dmg"];
            if (st.ContainsKey("range") && core.ContainsKey("tower_range")) st["range"] *= 1f + core["tower_range"];
            if (st.ContainsKey("dps")) st["dps"] *= (1f + m.Get("dmg")) * (1f + core.Get("tower_dmg"));
            if (st.ContainsKey("jumps")) st["jumps"] += Mathf.RoundToInt(m.Get("jumps_add"));
            if (st.ContainsKey("falloff")) st["falloff"] = Mathf.Min(0.95f, st["falloff"] + m.Get("falloff_add"));
            if (m.ContainsKey("jump_range_pct")) st["jump_range"] = 1f + m["jump_range_pct"];
            if (st.ContainsKey("targets") || m.ContainsKey("targets_add")) st["targets"] = st.Get("targets", 1f) + Mathf.RoundToInt(m.Get("targets_add"));
            if (m.ContainsKey("pierce")) st["armor_pierce"] = Mathf.Min(1f, st.Get("armor_pierce") + m["pierce"]);
            if (m.ContainsKey("crit")) st["crit"] = m["crit"];
            if (m.ContainsKey("edge_add")) st["edge"] = Mathf.Min(1f, Balance.MortarEdgeFactor + m["edge_add"]);
            foreach (var f in Flags) if (m.ContainsKey(f)) st[f] = m[f];
            if (st.ContainsKey("income")) st["income"] = Mathf.RoundToInt(st["income"] * (1f + core.Get("income_pct")));
        }

        /// <summary>Kostenfaktor eines Bauwerks (Balance._cost_factor): Serienbau und Schnellbau, mindestens 0,5.</summary>
        public static float CostFactor(string kind) => Mathf.Max(0.5f, (1f + Mods(kind).Get("cost")) * Boons.CostFactor);
    }
}
