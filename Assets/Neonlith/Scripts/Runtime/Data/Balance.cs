using System.Collections.Generic;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>Gegnerart, Zeile aus Balance.ENEMIES (scripts/autoload/balance.gd). Werte in Feldern und Sekunden.</summary>
    public sealed class EnemyType
    {
        public string id, name, swarm;
        public float hp, speed, armor, size;
        public int bounty, damage;
        public bool boss, flying;
    }

    /// <summary>Waffe des Arsenals (scripts/game/arsenal.gd).</summary>
    public sealed class ArmType
    {
        public string id, name;
        public int mag;
        public float damage, cooldown, reload;
        public bool semi;
    }

    /// <summary>
    /// Balancewerte, die der erste spielbare Stand braucht: 1:1 aus <c>balance.gd</c>, <c>arsenal.gd</c> und
    /// <c>run.gd</c> (4.99.65). Einheiten wie in Godot: Reichweite in Feldern, Tempo in Feldern je Sekunde,
    /// Zeiten in Spielsekunden. Umrechnung Feld → Meter: <see cref="FieldM"/> (Standmodus, Lebensgröße).
    /// </summary>
    public static class Balance
    {
        /// <summary>Meter je Feld im Standmodus (GunTower: Umkreis 2,5 Felder = 5 m).</summary>
        public const float FieldM = 2f;

        public const int StartLives = 20;
        public const float BuildTime = 22f;          // Countdown zwischen zwei Wellen
        public const float ArmorMinFactor = 0.25f;   // direkter Treffer verursacht mindestens 25 %
        public const float EnemySpeedK = 1.3f;       // 4.99.63 alle Gegner 30 % schneller
        public const float HpGrowth = 0.18f;
        public const float HpGrowthQuad = 0.005f;

        // Missionsstärke (run.gd)
        static readonly float[] MissionEntry = { 0.3f, 0.8f, 1.2f, 1.6f, 2.0f };
        const float MissionEnd0 = 4.0f;
        const float MissionEndStep = 0.45f;
        const float MissionHpStep = 0.008f;

        public static readonly Dictionary<string, EnemyType> Enemies = new Dictionary<string, EnemyType>
        {
            ["mist_walker"] = E("mist_walker", "Lauerer", 100, 1.0f, 0, 4, 1, 1.0f, "cube"),
            ["skitter"] = E("skitter", "Hetzer", 55, 1.6f, 0, 4, 1, 1.0f, "long"),
            ["flicker_swarm"] = E("flicker_swarm", "Schwärmer", 14, 1.3f, 0, 1, 1, 0.5f, "small"),
            ["mist_colossus"] = E("mist_colossus", "Schilder", 720, 0.45f, 10, 18, 2, 1.4f, "heavy"),
            ["cube"] = E("cube", "Würfel", 90, 1.0f, 0, 3, 1, 1.0f, "cube"),
            ["dart"] = E("dart", "Splitter", 40, 1.8f, 0, 2, 1, 0.8f, "long"),
            ["block"] = E("block", "Block", 520, 0.5f, 6, 14, 2, 1.4f, "heavy"),
            ["spotter"] = E("spotter", "Spieher", 120, 0.8f, 0, 8, 1, 1.0f, ""),
            ["carrier"] = E("carrier", "Packträger", 380, 0.6f, 3, 10, 2, 1.2f, ""),
            ["mist_weaver"] = E("mist_weaver", "Nebelweberin", 180, 0.75f, 2, 9, 1, 1.0f, ""),
            ["glass_angel"] = E("glass_angel", "Stecher", 145, 1.1f, 2, 7, 1, 1.0f, "", flying: true),
            ["boss_orun"] = E("boss_orun", "Schwadenwächter Orun", 5200, 0.5f, 26, 300, 20, 2.6f, "", boss: true),
            ["boss_orun_minor"] = E("boss_orun_minor", "Schildwächter", 1100, 0.55f, 10, 40, 5, 0.6f, ""),
            ["boss_brood"] = E("boss_brood", "Brutmutter", 7000, 0.42f, 10, 300, 20, 2.6f, "", boss: true),
            // Bosse der Welten II bis IV (balance.gd ab Zeile 234): bis zum Port der Bosslogik mit den Werten der Brutmutter
            ["boss_vey"] = E("boss_vey", "Anomalie: Schwebeträgerin", 6000, 0.45f, 12, 300, 20, 2.6f, "", boss: true),
            ["boss_kael"] = E("boss_kael", "Anomalie: Kältekern", 7500, 0.4f, 16, 300, 20, 2.6f, "", boss: true),
        };

        public static readonly Dictionary<string, ArmType> Arms = new Dictionary<string, ArmType>
        {
            ["pistol"] = new ArmType { id = "pistol", name = "Pistole", mag = 12, damage = 34f, cooldown = 0.2f, reload = 0.9f, semi = true },
            ["rifle"] = new ArmType { id = "rifle", name = "Sturmgewehr", mag = 30, damage = 16f, cooldown = 0.09f, reload = 0.7f, semi = false },
            ["sniper"] = new ArmType { id = "sniper", name = "Sniper", mag = 5, damage = 170f, cooldown = 1.0f, reload = 1.5f, semi = true },
        };

        static EnemyType E(string id, string name, float hp, float speed, float armor, int bounty, int damage, float size, string swarm, bool boss = false, bool flying = false)
        {
            return new EnemyType { id = id, name = name, hp = hp, speed = speed, armor = armor, bounty = bounty, damage = damage, size = size, swarm = swarm, boss = boss, flying = flying };
        }

        public static EnemyType Enemy(string id)
        {
            return Enemies.TryGetValue(id, out var e) ? e : Enemies["mist_walker"];
        }

        /// <summary>
        /// Lebensfaktor und Vorlagenindex je Welle einer Mission (Run._build_mission in run.gd, ohne die
        /// Nachjustierung der Gegnerzahlen _fit_counts, die den Faktor höchstens senkt).
        /// </summary>
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

        /// <summary>Balance.enemy_hp: Grundwert mal Wachstum über die Vorlagenwelle x mal Faktor der Welle.</summary>
        public static float EnemyHp(EnemyType type, float x, float waveFactor)
        {
            return type.hp * (1f + HpGrowth * x + HpGrowthQuad * x * x) * waveFactor;
        }

        /// <summary>Combat.direct_damage: Rüstung zieht ab, mindestens 25 % kommen durch.</summary>
        public static float DirectDamage(EnemyType type, float amount)
        {
            return Mathf.Max(amount - type.armor, amount * ArmorMinFactor);
        }
    }
}
