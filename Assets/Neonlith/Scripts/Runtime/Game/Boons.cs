using System.Collections.Generic;
using Neonlith.Data;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>Segenskarte (Boons.CARDS in scripts/game/boons.gd).</summary>
    public sealed class BoonCard
    {
        public string id, name, text, icon, tower = "", rarity = "common";
        public bool repeat;
        public Dictionary<string, float> mods = new Dictionary<string, float>();
    }

    /// <summary>
    /// Segenskarten (scripts/game/boons.gd): nach jeder dritten geschafften Welle (nicht nach der letzten) drei
    /// zufällige Karten, eine wird gewählt; die Wirkung gilt für diese Mission. Turmwerte laufen über
    /// <see cref="ApplyMods"/> wie Balance.stats + SkillTrees.apply in Godot; Sofortwirkungen (Leben, Essenz)
    /// setzt die GameSession. Seltenheit: gewöhnlich, selten, episch (Build-Dreher).
    /// </summary>
    public static class Boons
    {
        public const int Every = 3, Choices = 3;
        public static readonly List<string> Active = new List<string>();
        public static readonly Dictionary<string, string> RarityNames = new Dictionary<string, string> { ["common"] = "Gewöhnlich", ["rare"] = "Selten", ["epic"] = "Episch" };
        public static readonly Dictionary<string, Color> RarityColor = new Dictionary<string, Color>
        {
            ["common"] = new Color(0.85f, 0.85f, 0.87f), ["rare"] = new Color(0.55f, 0.8f, 1f), ["epic"] = new Color(1f, 0.78f, 0.35f),
        };

        public static readonly Dictionary<string, BoonCard> Cards = Build();

        static BoonCard C(string id, string name, string text, string icon, string tower = "", string rarity = "common", bool repeat = false, params object[] mods)
        {
            var c = new BoonCard { id = id, name = name, text = text, icon = icon, tower = tower, rarity = rarity, repeat = repeat };
            for (var i = 0; i + 1 < mods.Length; i += 2) c.mods[(string)mods[i]] = System.Convert.ToSingle(mods[i + 1]);
            return c;
        }

        static Dictionary<string, BoonCard> Build()
        {
            var l = new List<BoonCard>
            {
                C("arrow_dmg", "Scharfe Bolzen", "Gatling: +25 % Schaden", "bow", "arrow", "common", false, "dmg", 0.25),
                C("ice_slow", "Kalte Linse", "Kryo-Blaster: +10 % Verlangsamung, Wirkung 0,5 s länger", "snowflake", "ice", "common", false, "slow", 0.1, "slow_time_add", 0.5),
                C("fire_burn", "Glutherz", "Plasmawerfer: +40 % Brandschaden", "flame", "fire", "common", false, "burn_pct", 0.4),
                C("fire_shred", "Zersetzer", "Plasmawerfer: brennende Gegner verlieren 4 weitere Rüstung", "shield", "fire", "common", false, "shred_add", 4.0),
                C("ice_wave", "Kältekranz", "Kryo-Blaster: +0,4 Felder Frostradius", "snowflake", "ice", "common", false, "splash_add", 0.4),
                C("chain_jump", "Überschlag", "Tesla-Spule: Kettenblitz springt 1× weiter", "bolt", "chain", "rare", false, "jumps_add", 1),
                C("sniper_focus", "Ruhige Hand", "Railgun: +20 % Reichweite, +15 % Schaden", "scope", "sniper", "rare", false, "range", 0.2, "dmg", 0.15),
                C("mortar_splash", "Schwere Granaten", "Raketenwerfer: +25 % Wirkfläche", "mortar", "mortar", "rare", false, "splash_pct", 0.25),
                C("overclock", "Übertakten", "Alle Türme: +10 % Feuerrate", "clock", "*", "rare", false, "rate", 0.1),
                C("long_sight", "Weitblick", "Alle Türme: +8 % Reichweite", "range", "*", "rare", false, "range", 0.08),
                C("bounty", "Kopfgeld", "+1 Essenz für jeden besiegten Gegner", "coin"),
                C("interest", "Zinsen", "Nach jeder Welle +10 % deiner Essenz (höchstens 40)", "coin", "", "rare"),
                C("lives", "Bollwerk", "+3 Leben", "heart", "", "common", true),
                C("repair", "Reparatur", "+5 Leben sofort", "heart", "", "common", true),
                C("treasure", "Schatzfund", "+150 Essenz sofort (+50 je Etappe)", "coin", "", "common", true),
                C("cheap", "Schnellbau", "Bauen und Ausbauen 10 % günstiger", "up", "", "rare"),
                C("shards", "Splitterernte", "+25 % Kernsplitter am Ende", "shard", "", "rare"),
                C("arrow_storm", "Kugelhagel", "Gatling: +60 % Feuerrate, −15 % Reichweite", "bow", "arrow", "epic", false, "rate", 0.6, "range", -0.15),
                C("ice_perma", "Permafrost", "Kryo-Blaster: Getroffene nehmen +25 % Schaden von allen Türmen, −20 % Schaden", "snowflake", "ice", "epic", false, "permafrost", 0.25, "dmg", -0.2),
                C("ice_zero", "Absoluter Nullpunkt", "Kryo-Blaster: +20 % Verlangsamung, Wirkung 1,5 s länger", "snowflake", "ice", "epic", false, "slow", 0.2, "slow_time_add", 1.5),
                C("fire_inferno", "Inferno", "Plasmawerfer: Brandschaden ×2,2, Brand 1 s länger, −15 % Aufprallschaden", "flame", "fire", "epic", false, "burn_pct", 1.2, "burn_time_add", 1.0, "dmg", -0.15),
                C("chain_storm", "Gewitterfront", "Tesla-Spule: Kettenblitz springt 3× weiter, +20 % Schaden", "bolt", "chain", "epic", false, "jumps_add", 3, "dmg", 0.2),
                C("sniper_orbital", "Orbitalschlag", "Railgun: Schaden ×2, −35 % Feuerrate", "scope", "sniper", "epic", false, "dmg", 1.0, "rate", -0.35),
                C("mortar_cluster", "Streubomben", "Raketenwerfer: +60 % Wirkfläche, +25 % Schaden", "mortar", "mortar", "epic", false, "splash_pct", 0.6, "dmg", 0.25),
                C("goldrush", "Goldrausch", "Wellenbelohnungen +30 %", "coin", "", "epic"),
            };
            var d = new Dictionary<string, BoonCard>();
            foreach (var c in l) d[c.id] = c;
            return d;
        }

        public static void Reset() => Active.Clear();
        public static bool Has(string id) => Active.Contains(id);
        public static void Take(string id) { if (Cards.ContainsKey(id)) Active.Add(id); }
        public static string Rarity(string id) => Cards.TryGetValue(id, out var c) ? c.rarity : "common";
        public static float CostFactor => Has("cheap") ? 0.9f : 1f;
        public static int BountyAdd => Has("bounty") ? 1 : 0;
        public static float RewardFactor => Has("goldrush") ? 1.3f : 1f;
        public static float ShardFactor => Has("shards") ? 1.25f : 1f;

        /// <summary>Kommt nach dieser geschafften Welle (0-basiert) eine Wahl? Nicht nach der letzten.</summary>
        public static bool OfferAfter(int waveIndex, int waveCount) => (waveIndex + 1) % Every == 0 && waveIndex + 1 < waveCount;

        /// <summary>Drei zufällige passende Karten (nur Türme, die es in der Mission gibt; keine doppelten).</summary>
        public static List<string> Roll(List<string> availableTowers)
        {
            var pool = new List<string>();
            foreach (var kv in Cards)
            {
                var c = kv.Value;
                if (Active.Contains(c.id) && !c.repeat) continue;
                if (c.tower.Length > 0 && c.tower != "*" && !availableTowers.Contains(c.tower)) continue;
                pool.Add(c.id);
            }
            var out_ = new List<string>();
            while (out_.Count < Choices && pool.Count > 0)
            {
                var i = Random.Range(0, pool.Count);
                out_.Add(pool[i]); pool.RemoveAt(i);
            }
            return out_;
        }

        /// <summary>Zusätzliche Turm-Modifikatoren (Boons.add_mods).</summary>
        public static Dictionary<string, float> Mods(string kind)
        {
            var m = new Dictionary<string, float>();
            foreach (var id in Active)
            {
                var c = Cards[id];
                if (c.tower.Length == 0 || c.mods.Count == 0) continue;
                if (c.tower != "*" && c.tower != kind) continue;
                if (c.tower == "*" && kind == "teahouse") continue;
                foreach (var kv in c.mods) m[kv.Key] = m.Get(kv.Key) + kv.Value;
            }
            return m;
        }

        /// <summary>SkillTrees.apply für die Modifikatoren der Segenskarten (Fertigkeiten der Basis folgen mit dem Hub).</summary>
        public static Dictionary<string, float> ApplyMods(string kind, Dictionary<string, float> baseStats)
        {
            var m = Mods(kind);
            if (m.Count == 0) return baseStats;
            var st = new Dictionary<string, float>(baseStats);
            if (st.ContainsKey("damage")) st["damage"] *= 1f + m.Get("dmg");
            if (st.ContainsKey("cooldown")) st["cooldown"] /= 1f + m.Get("rate");
            if (st.ContainsKey("range")) st["range"] *= 1f + m.Get("range");
            if (st.ContainsKey("splash") && (st["splash"] > 0f || m.ContainsKey("splash_add"))) st["splash"] = st["splash"] * (1f + m.Get("splash_pct")) + m.Get("splash_add");
            if (st.ContainsKey("slow")) st["slow"] += m.Get("slow");
            if (st.ContainsKey("slow_time")) st["slow_time"] = st["slow_time"] * (1f + m.Get("slow_time_pct")) + m.Get("slow_time_add");
            if (st.ContainsKey("burn_dps")) st["burn_dps"] *= 1f + m.Get("burn_pct");
            if (st.ContainsKey("burn_time")) st["burn_time"] += m.Get("burn_time_add");
            if (st.ContainsKey("shred")) st["shred"] += m.Get("shred_add");
            if (st.ContainsKey("dps")) st["dps"] *= 1f + m.Get("dmg");
            if (st.ContainsKey("jumps")) st["jumps"] += Mathf.RoundToInt(m.Get("jumps_add"));
            if (st.ContainsKey("falloff")) st["falloff"] = Mathf.Min(0.95f, st["falloff"] + m.Get("falloff_add"));
            if (m.ContainsKey("permafrost")) st["permafrost"] = m["permafrost"];
            return st;
        }
    }
}
