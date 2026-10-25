using System.Collections.Generic;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>Gegnerart, Zeile aus Balance.ENEMIES (scripts/autoload/balance.gd). Werte in Feldern und Sekunden.</summary>
    public sealed class EnemyType
    {
        public string id, name, swarm = "", weakShort = "";
        public float hp, speed, armor, size, halfWidth;
        public int bounty, damage;
        public bool boss, flying, miniBoss, noVariation, fillRoad;
        public string[] tags = new string[0];
        // Fähigkeiten (0 oder leer = keine)
        public float dashSpeed, dashTime, dashCd;                 // Hetzer
        public float guardRange, guardMult;                       // Schilder
        public float healRatio, healInterval, healRadius; public bool healSelf;   // Nebelweberin
        public float markEvery, markRange, markTime;              // Spieher
        public string spawnOnDeath = ""; public int spawnCount;   // Packträger
        public float regenPct;                                    // Eisfürst
        public float shield, shieldRegen, shieldDelay;            // Schild (Grundtyp)
        public float slowResist, maxSlow = 0.70f, slowDurMult = 1f, physMult = 1f, elemMult = 1f;
        public Dictionary<string, float> srcMult = new Dictionary<string, float>();
        public string summonType = "", summonAtType = ""; public int summonCount, summonAtCount; public float summonEvery; public float[] summonAt = new float[0];
        public float enrageAt, enrageSpeed = 1f;
        public float eliteHp, eliteSize;

        public bool Has(string tag) { foreach (var t in tags) if (t == tag) return true; return false; }
    }

    /// <summary>Elite-Eigenschaft (Balance.ELITES).</summary>
    public sealed class EliteType
    {
        public string id, name, text;
        public float shield, shieldRegen, shieldDelay = 3f, physMult = 1f, maxSlow = -1f, slowDurMult = 1f, armorAdd, hpMult = 1f;
        public bool cloak;
    }

    /// <summary>Bauwerk (Balance.STRUCTURES): Turm, Gebäude oder Sperre mit drei Stufen.</summary>
    public sealed class StructureType
    {
        public string id, name, category, damageType = "", shortText = "", role = "", icon = "";
        public bool air = true;
        public int defaultTarget;
        public List<Dictionary<string, float>> levels = new List<Dictionary<string, float>>();
        public List<string> upgradeTexts = new List<string>();
        public bool IsDefense => category == "defense";
        public bool IsTrap => category == "trap";
        public int Cost(int level) => level < levels.Count ? Mathf.RoundToInt(levels[level]["cost"]) : -1;
    }

    /// <summary>Waffe des Arsenals (scripts/game/arsenal.gd).</summary>
    public sealed class ArmType
    {
        public string id, name;
        public int mag;
        public float damage, cooldown, reload;
        public bool semi, twoHand, charge;
        public int unlockWorld;      // 0 = ab Start, 1 = nach Welt I, 2 = nach Welt II
    }

    /// <summary>
    /// Balancewerte 1:1 aus <c>balance.gd</c>, <c>arsenal.gd</c> und <c>run.gd</c> (4.99.65). Einheiten wie in Godot:
    /// Reichweite in Feldern, Tempo in Feldern je Sekunde, Zeiten in Spielsekunden. Umrechnung Feld → Meter:
    /// <see cref="FieldM"/> (Standmodus, Lebensgröße).
    /// </summary>
    public static class Balance
    {
        public const float FieldM = 2f;

        public const int StartGold = 230;
        public const int StartLives = 20;
        public const float SellRatio = 0.70f;
        public const float SellRatioSamePhase = 1.0f;
        public const float BuildTime = 22f;
        public const float EarlyCallGoldPerSec = 1f;
        public const float ArmorMinFactor = 0.25f;
        public const float ThermoShockBonus = 0.5f;
        public const float BrittleBonus = 0.25f;
        public const float SlowCap = 0.70f;
        public const float ChainJumpRange = 1.7f;
        public const float MortarEdgeFactor = 0.35f;
        public const float CritMult = 2f;
        public const bool BurnBlocksHealing = true;
        public const float TrapHit = 9f, TrapAttackers = 8f, TrapHold = 0.75f, FenceHalf = 0.7f;
        public const float EnemySpeedK = 1.3f;
        public const float HpGrowth = 0.18f, HpGrowthQuad = 0.005f;
        public const float EliteHp = 4f, EliteHpTank = 2.5f, EliteSize = 1.3f; public const int EliteBounty = 3;
        public const float DotTick = 0.25f;
        public const float TilePath = 17.888544f;      // Wegpixel je Feld (Geschosstempo in Godot ist in Pixeln je Sekunde)
        public static readonly string[] DefenseOrder = { "arrow", "ice", "fire", "chain", "sniper", "mortar" };
        public static readonly string[] BuildingOrder = { "teahouse" };
        public static readonly string[] TrapOrder = { "barricade", "laserfence" };
        public static readonly string[] PhysicalSources = { "arrow", "sniper", "mortar" };
        public static readonly string[] TargetModes = { "Erster", "Stärkster", "Nächster", "Letzter" };

        // Missionsstärke (run.gd)
        static readonly float[] MissionEntry = { 0.3f, 0.8f, 1.2f, 1.6f, 2.0f };
        const float MissionEnd0 = 4.0f, MissionEndStep = 0.45f, MissionHpStep = 0.008f;

        public static readonly Dictionary<string, StructureType> Structures = BuildStructures();
        public static readonly Dictionary<string, EnemyType> Enemies = BuildEnemies();
        public static readonly Dictionary<string, EliteType> Elites = new Dictionary<string, EliteType>
        {
            ["schild"] = new EliteType { id = "schild", name = "Schildträger", text = "Schild aus 40 % seiner Leben, lädt sich nach 3 s ohne Treffer wieder auf", shield = 0.4f, shieldRegen = 0.12f, shieldDelay = 3f },
            ["phantom"] = new EliteType { id = "phantom", name = "Phantom", text = "Nimmt nur halben physischen Schaden (Gatling, Railgun, Raketen)", physMult = 0.5f },
            ["unbeugsam"] = new EliteType { id = "unbeugsam", name = "Unbeugsam", text = "Kaum zu verlangsamen, +6 Rüstung", maxSlow = 0.15f, slowDurMult = 0.5f, armorAdd = 6f },
            ["schatten"] = new EliteType { id = "schatten", name = "Signalschatten", text = "Für Türme unsichtbar, bis die Spielerwaffe ihn trifft", cloak = true, hpMult = 0.5f },
        };
        public static readonly Dictionary<string, ArmType> Arms = new Dictionary<string, ArmType>
        {
            ["pistol"] = new ArmType { id = "pistol", name = "Pistole", mag = 12, damage = 34f, cooldown = 0.2f, reload = 0.9f, semi = true, unlockWorld = 0 },
            ["rifle"] = new ArmType { id = "rifle", name = "Sturmgewehr", mag = 30, damage = 16f, cooldown = 0.09f, reload = 0.7f, semi = false, twoHand = true, charge = true, unlockWorld = 1 },
            ["sniper"] = new ArmType { id = "sniper", name = "Sniper", mag = 5, damage = 170f, cooldown = 1.0f, reload = 1.5f, semi = true, twoHand = true, unlockWorld = 2 },
        };
        // Ladeschuss des Sturmgewehrs (WeaponConfig: charge_time, charge_damage, charge_splash in Feldern)
        public const float ChargeTime = 1.2f, ChargeDamage = 140f, ChargeSplash = 1.2f; public const int ChargeAmmo = 6;

        public static readonly Dictionary<string, Color> TowerColor = new Dictionary<string, Color>
        {
            ["arrow"] = WorldTheme.Hex("5ff4ff"), ["ice"] = WorldTheme.Hex("4f86ff"), ["fire"] = WorldTheme.Hex("ff8a3a"),
            ["chain"] = WorldTheme.Hex("b48cff"), ["sniper"] = WorldTheme.Hex("ff5ad2"), ["mortar"] = WorldTheme.Hex("ffd84d"),
            ["teahouse"] = WorldTheme.Hex("f2c14e"), ["barricade"] = WorldTheme.Hex("ffb43a"), ["laserfence"] = WorldTheme.Hex("ff3a30"),
        };

        public static EnemyType Enemy(string id) => Enemies.TryGetValue(id, out var e) ? e : Enemies["mist_walker"];
        /// <summary>Rolle je Gegnerart (Balance.ENEMIES "role") für die Gegner-Info.</summary>
        public static readonly Dictionary<string, string> EnemyRole = new Dictionary<string, string>
        {
            ["mist_walker"] = "Standardgegner", ["skitter"] = "Schnell, sprintet los, wenn er getroffen wird", ["flicker_swarm"] = "Klein, schnell, in riesigen Gruppen",
            ["mist_colossus"] = "Tank, schirmt die Gegner hinter sich ab", ["cube"] = "Standardgegner des Schwarms", ["dart"] = "Schnell und zerbrechlich",
            ["block"] = "Langsam, massiv, gepanzert", ["spotter"] = "Markiert Türme, die dann kurz ausfallen", ["carrier"] = "Platzt und lässt Schwärmer frei",
            ["mist_weaver"] = "Heilt Verbündete in der Nähe", ["glass_angel"] = "Fliegt schnell – Raketenwerfer treffen ihn nicht",
            ["boss_orun"] = "Boss · Panzerkoloss", ["boss_orun_minor"] = "Zwischenboss · Riesen-Schilder", ["boss_brood"] = "Boss · ruft Schwärmer und Packträger",
            ["boss_vey"] = "Boss · Schwarmmutter", ["boss_kael"] = "Boss · Eispanzer",
        };
        public static StructureType Structure(string id) => Structures.TryGetValue(id, out var s) ? s : null;
        public static bool IsTrap(string id) => Structures.TryGetValue(id, out var s) && s.IsTrap;
        public static bool IsDefense(string id) => Structures.TryGetValue(id, out var s) && s.IsDefense;
        public static int BuildCost(string id) => Structure(id)?.Cost(0) ?? 0;
        /// <summary>Kosten für level → level + 1, oder -1 am Maximum.</summary>
        public static int UpgradeCost(string id, int level) { var s = Structure(id); return s == null || level + 1 >= s.levels.Count ? -1 : s.Cost(level + 1); }
        public static Dictionary<string, float> Stats(string id, int level) { var s = Structure(id); return s.levels[Mathf.Clamp(level, 0, s.levels.Count - 1)]; }
        public static float Get(this Dictionary<string, float> st, string key, float fallback = 0f) => st != null && st.TryGetValue(key, out var v) ? v : fallback;
        public static bool IsPhysical(string source) { foreach (var p in PhysicalSources) if (p == source) return true; return false; }

        /// <summary>Lebensfaktor und Vorlagenindex je Welle einer Mission (Run._build_mission, ohne _fit_counts).</summary>
        public static void MissionWaveHp(int mission, int chapter, int rank, bool hasRank, float diff, int waveCount, out float[] hpIndex, out float[] hpWave)
        {
            var ch = chapter;
            if (hasRank) ch = rank <= 4 ? 0 : (rank <= 8 ? 1 : 2);
            var x0 = MissionEntry[Mathf.Clamp(ch, 0, MissionEntry.Length - 1)];
            var x1 = Mathf.Min(14f, MissionEnd0 + MissionEndStep * (rank - 1));
            var hpBoost = 1f + MissionHpStep * (rank - 1);
            hpIndex = new float[waveCount];
            hpWave = new float[waveCount];
            for (var k = 0; k < waveCount; k++)
            {
                var t = k / (float)Mathf.Max(waveCount - 1, 1);
                var x = Mathf.Lerp(x0, x1, t);
                hpIndex[k] = x;
                hpWave[k] = (0.8f + 1.2f * Mathf.Clamp(x, 0f, 14f) / 14f) * hpBoost;
            }
        }

        /// <summary>Balance.enemy_hp: Grundwert mal Wachstum über die Vorlagenwelle x mal Faktor der Welle (Boss: boss_hp der Mission).</summary>
        public static float EnemyHp(EnemyType type, float x, float waveFactor, float bossHpMult)
        {
            if (type.boss) return type.hp * bossHpMult;
            return type.hp * (1f + HpGrowth * x + HpGrowthQuad * x * x) * waveFactor;
        }

        // ------------------------------------------------------------------------------------------------
        static Dictionary<string, float> L(params object[] kv)
        {
            var d = new Dictionary<string, float>();
            for (var i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = System.Convert.ToSingle(kv[i + 1]);
            return d;
        }

        static StructureType S(string id, string name, string category, string damageType, bool air, string role, string shortText, int defaultTarget, string[] texts, params Dictionary<string, float>[] levels)
        {
            var s = new StructureType { id = id, name = name, category = category, damageType = damageType, air = air, role = role, shortText = shortText, defaultTarget = defaultTarget };
            s.levels.AddRange(levels);
            s.upgradeTexts.AddRange(texts);
            return s;
        }

        static Dictionary<string, StructureType> BuildStructures()
        {
            var d = new Dictionary<string, StructureType>();
            d["arrow"] = S("arrow", "Gatling", "defense", "physical", true, "Physischer Schaden · Einzelziel",
                "Schnelles Dauerfeuer auf Einzelziele. Rüstung mindert den Schaden.", 0,
                new[] { "", "Doppelschiene: mehr Schaden, schneller, größere Reichweite", "Leuchtrahmen: trifft zwei Ziele gleichzeitig" },
                L("cost", 60, "damage", 14, "cooldown", 0.80, "range", 3.2, "targets", 1, "proj_speed", 300),
                L("cost", 75, "damage", 23, "cooldown", 0.67, "range", 3.5, "targets", 1, "proj_speed", 330),
                L("cost", 130, "damage", 30, "cooldown", 0.61, "range", 3.8, "targets", 2, "proj_speed", 360));
            d["ice"] = S("ice", "Kryo-Blaster", "defense", "frost", true, "Frostschaden · Verlangsamung",
                "Verlangsamt Gegner, ab Stufe 3 im Umkreis.", 0,
                new[] { "", "Kristallkäfig: stärkere und längere Verlangsamung", "Splitterkranz: Frost trifft alle Gegner im Umkreis" },
                L("cost", 75, "damage", 10, "cooldown", 1.00, "range", 2.9, "slow", 0.35, "slow_time", 2.0, "splash", 0, "proj_speed", 260),
                L("cost", 85, "damage", 16, "cooldown", 0.95, "range", 3.2, "slow", 0.45, "slow_time", 2.5, "splash", 0, "proj_speed", 280),
                L("cost", 140, "damage", 24, "cooldown", 0.90, "range", 3.5, "slow", 0.55, "slow_time", 3.0, "splash", 1.1, "proj_speed", 300));
            d["fire"] = S("fire", "Plasmawerfer", "defense", "fire", true, "Flächenschaden · Brand · Rüstungsbruch",
                "Glutgeschosse mit Flächenschaden. Setzt Gegner in Brand; brennende Gegner verlieren Rüstung.", 0,
                new[] { "", "Doppelschlitz: stärkere Explosion, heißerer Brand, mehr Rüstungsbruch", "Glutschale: großer Explosionsradius, langer Brand, starker Rüstungsbruch" },
                L("cost", 100, "damage", 18, "cooldown", 1.50, "range", 3.0, "splash", 1.0, "burn_dps", 7, "burn_time", 3.0, "shred", 3, "proj_speed", 170),
                L("cost", 110, "damage", 30, "cooldown", 1.40, "range", 3.2, "splash", 1.15, "burn_dps", 12, "burn_time", 3.0, "shred", 5, "proj_speed", 180),
                L("cost", 165, "damage", 42, "cooldown", 1.30, "range", 3.5, "splash", 1.3, "burn_dps", 18, "burn_time", 3.5, "shred", 7, "proj_speed", 190));
            d["chain"] = S("chain", "Tesla-Spule", "defense", "lightning", true, "Kettenschaden · Gruppen",
                "Kettenblitz: springt auf mehrere Gegner, jeder Sprung schwächer. Stark gegen Schwärme.", 0,
                new[] { "", "Zweite Wicklung: stärkerer Blitz, springt auf 4 Gegner", "Resonanzkrone: springt auf 5 Gegner und verliert weniger Kraft" },
                L("cost", 95, "damage", 18, "cooldown", 1.05, "range", 3.0, "jumps", 3, "falloff", 0.72),
                L("cost", 90, "damage", 27, "cooldown", 0.95, "range", 3.2, "jumps", 4, "falloff", 0.76),
                L("cost", 150, "damage", 37, "cooldown", 0.85, "range", 3.4, "jumps", 5, "falloff", 0.8));
            d["sniper"] = S("sniper", "Railgun", "defense", "physical", true, "Einzelschaden · Reichweite",
                "Sehr große Reichweite, hoher Einzelschaden, langsam. Zielt zuerst auf den Stärksten.", 1,
                new[] { "", "Langes Rohr: mehr Schaden und Reichweite", "Kernschuss: ignoriert die Hälfte der Rüstung" },
                L("cost", 110, "damage", 70, "cooldown", 2.60, "range", 6.5),
                L("cost", 100, "damage", 112, "cooldown", 2.40, "range", 7.0),
                L("cost", 165, "damage", 172, "cooldown", 2.20, "range", 7.5, "armor_pierce", 0.5));
            d["mortar"] = S("mortar", "Raketenwerfer", "defense", "physical", false, "Flächenschaden · Reichweite",
                "Hoher Bogenschuss, großer Explosionsradius. Schaden nimmt zum Rand hin ab. Trifft keine Flieger.", 0,
                new[] { "", "Schwerer Kern: mehr Schaden und größerer Radius", "Doppelkammer: größter Radius, schnelleres Nachladen" },
                L("cost", 100, "damage", 29, "cooldown", 2.00, "range", 4.4, "splash", 1.3, "proj_speed", 130),
                L("cost", 100, "damage", 46, "cooldown", 1.90, "range", 4.7, "splash", 1.5, "proj_speed", 140),
                L("cost", 160, "damage", 69, "cooldown", 1.75, "range", 5.0, "splash", 1.7, "proj_speed", 150));
            d["teahouse"] = S("teahouse", "Generator", "building", "", true, "Einkommen",
                "Bringt nach jeder geschafften Welle zusätzliche Essenz.", 0,
                new[] { "", "Stelenbecken: mehr Einnahmen", "Goldobelisk: noch mehr Einnahmen" },
                L("cost", 90, "income", 12), L("cost", 90, "income", 22), L("cost", 130, "income", 34));
            d["barricade"] = S("barricade", "Barrikade", "trap", "", false, "Sperre · Haltepunkt",
                "Sperrt die Fahrbahn. Bodengegner bleiben stehen und schlagen auf sie ein, bis sie bricht. Nach jeder Welle wird sie repariert.", 0,
                new[] { "", "Stahlkern: hält doppelt so lange", "Dornenpanzer: noch zäher, wer zuschlägt, nimmt Schaden" },
                L("cost", 40, "hp", 360), L("cost", 45, "hp", 720), L("cost", 80, "hp", 1200, "thorns", 10));
            d["laserfence"] = S("laserfence", "Laserzaun", "trap", "laser", false, "Flächenschaden · Durchgang",
                "Rote Laserlinien quer über die Fahrbahn. Jeder Bodengegner im Gitter nimmt Schaden und wird gebremst.", 0,
                new[] { "", "Zweites Gitter: mehr Schaden, stärker gebremst", "Hochleistungsemitter: sehr hoher Schaden" },
                L("cost", 70, "dps", 30, "slow", 0.2), L("cost", 70, "dps", 52, "slow", 0.25), L("cost", 120, "dps", 84, "slow", 0.3));
            return d;
        }

        static EnemyType E(string id, string name, float hp, float speed, float armor, int bounty, int damage, float size, string swarm, string weak, params string[] tags)
        {
            return new EnemyType { id = id, name = name, hp = hp, speed = speed, armor = armor, bounty = bounty, damage = damage, size = size, swarm = swarm, weakShort = weak, tags = tags };
        }

        static Dictionary<string, EnemyType> BuildEnemies()
        {
            var d = new Dictionary<string, EnemyType>();
            d["mist_walker"] = E("mist_walker", "Lauerer", 100, 1.0f, 0, 4, 1, 1.0f, "cube", "keine Schwäche – reine Feuerkraft", "Ground", "Standard");
            var sk = E("skitter", "Hetzer", 55, 1.6f, 0, 4, 1, 1.0f, "long", "sprintet bei Treffern – Kryo verlangsamt", "Ground", "Fast", "Runner", "LowHP");
            sk.dashSpeed = 1.7f; sk.dashTime = 0.8f; sk.dashCd = 4f; d["skitter"] = sk;
            var fs = E("flicker_swarm", "Schwärmer", 14, 1.3f, 0, 1, 1, 0.5f, "small", "Schwarm – Flächen- und Kettenschaden", "Ground", "Swarm", "Small", "LowHP");
            fs.eliteHp = 15f; fs.eliteSize = 2.6f; d["flicker_swarm"] = fs;
            var mc = E("mist_colossus", "Schilder", 720, 0.45f, 10, 18, 2, 1.4f, "heavy", "Rüstung 10, schirmt ab – zuerst ihn knacken", "Ground", "Tank", "Armored", "HighHP", "Large", "Guard");
            mc.halfWidth = 0.5f; mc.guardRange = 2.5f; mc.guardMult = 0.6f; d["mist_colossus"] = mc;
            var cu = E("cube", "Würfel", 90, 1.0f, 0, 3, 1, 1.0f, "cube", "keine Schwäche – Masse", "Ground", "Standard"); cu.noVariation = true; d["cube"] = cu;
            var da = E("dart", "Splitter", 40, 1.8f, 0, 2, 1, 0.8f, "long", "wenig Leben – jeder Treffer zählt", "Ground", "Fast", "Runner", "LowHP"); da.noVariation = true; d["dart"] = da;
            var bl = E("block", "Block", 520, 0.5f, 6, 14, 2, 1.4f, "heavy", "Rüstung 6 – Ladeschuss und Panzerbrecher", "Ground", "Tank", "Armored", "HighHP", "Large"); bl.halfWidth = 0.5f; bl.noVariation = true; d["block"] = bl;
            var sp = E("spotter", "Spieher", 120, 0.8f, 0, 8, 1, 1.0f, "", "legt Türme lahm – zuerst ausschalten", "Ground", "Support", "PriorityTarget");
            sp.markEvery = 6f; sp.markRange = 3.2f; sp.markTime = 3f; d["spotter"] = sp;
            var ca = E("carrier", "Packträger", 380, 0.6f, 3, 10, 2, 1.2f, "", "platzt – Flächenschaden bereithalten", "Ground", "HighHP", "Spawner");
            ca.halfWidth = 0.45f; ca.spawnOnDeath = "flicker_swarm"; ca.spawnCount = 8; d["carrier"] = ca;
            var we = E("mist_weaver", "Nebelweberin", 180, 0.75f, 2, 9, 1, 1.0f, "", "heilt – Feuer stoppt die Heilung", "Ground", "Support", "Healer", "PriorityTarget");
            we.healRatio = 0.06f; we.healInterval = 2.5f; we.healRadius = 2.3f; d["mist_weaver"] = we;
            var ga = E("glass_angel", "Stecher", 145, 1.1f, 2, 7, 1, 1.0f, "", "fliegt – Raketen treffen nicht", "Flying", "Air"); ga.flying = true; d["glass_angel"] = ga;
            var orun = E("boss_orun", "Schwadenwächter Orun", 5200, 0.5f, 26, 300, 20, 2.6f, "", "Rüstung 26 – Panzerbrecher nötig", "Ground", "Boss", "Armored");
            orun.boss = true; orun.slowResist = 0.5f; orun.fillRoad = true; orun.summonType = "boss_orun_minor"; orun.summonCount = 1; orun.summonAt = new[] { 0.66f, 0.33f }; orun.enrageAt = 0.25f; orun.enrageSpeed = 1.3f; d["boss_orun"] = orun;
            var om = E("boss_orun_minor", "Schildwächter", 1100, 0.55f, 10, 40, 5, 0.6f, "", "Rüstung 10, schirmt ab – Panzerbrecher", "Ground", "Armored", "HighHP", "Large", "Guard");
            om.miniBoss = true; om.noVariation = true; om.halfWidth = 0.5f; om.guardRange = 3.5f; om.guardMult = 0.5f; d["boss_orun_minor"] = om;
            var br = E("boss_brood", "Brutmutter", 7000, 0.42f, 10, 300, 20, 2.6f, "", "Rüstung 10, ruft Brut – Flächenschaden und Panzerbrecher", "Ground", "Boss", "Summoner");
            br.boss = true; br.slowResist = 0.5f; br.fillRoad = true; br.summonType = "flicker_swarm"; br.summonCount = 14; br.summonEvery = 9f; br.summonAt = new[] { 0.66f, 0.33f }; br.summonAtType = "carrier"; br.summonAtCount = 2; br.enrageAt = 0.25f; br.enrageSpeed = 1.3f; d["boss_brood"] = br;
            var vey = E("boss_vey", "Dunstkönigin Vey", 6600, 0.62f, 4, 300, 20, 2.4f, "", "fliegt, Tesla +60 %, ruft Brut", "Flying", "Boss", "Summoner");
            vey.boss = true; vey.flying = true; vey.slowResist = 0.4f; vey.physMult = 0.6f; vey.srcMult["chain"] = 1.6f; vey.summonType = "flicker_swarm"; vey.summonCount = 18; vey.summonEvery = 7f; vey.summonAt = new[] { 0.75f, 0.5f, 0.25f }; vey.enrageAt = 0.3f; vey.enrageSpeed = 1.35f; d["boss_vey"] = vey;
            var kael = E("boss_kael", "Eisfürst Kael", 8200, 0.55f, 10, 300, 20, 2.7f, "", "immun gegen Kälte, Plasma ×2", "Ground", "Boss", "Frost");
            kael.boss = true; kael.slowResist = 1f; kael.maxSlow = 0f; kael.srcMult["ice"] = 0f; kael.srcMult["fire"] = 2f; kael.srcMult["dot"] = 2f; kael.regenPct = 0.015f; kael.summonType = "skitter"; kael.summonCount = 8; kael.summonAt = new[] { 0.6f, 0.3f }; kael.enrageAt = 0.25f; kael.enrageSpeed = 1.3f; d["boss_kael"] = kael;
            return d;
        }
    }
}
