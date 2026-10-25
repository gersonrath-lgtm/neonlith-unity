using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Ablauf einer Mission (Game + GameState + WaveManager in Godot): Funk, Countdown, Wellen aus campaign.json,
    /// Bauen, Verbessern und Verkaufen auf Bauplätzen, Zielwahl und Treffer der Türme, alle Schadensregeln
    /// (Combat), Gegnerfähigkeiten (Schilder, Spieher, Nebelweberin, Packträger, Bosse), Barrikaden, Essenz
    /// (Kopfgeld direkt gutgeschrieben wie Godot ohne essence_manual), Generator-Einkommen, früher Start mit
    /// Bonus, Leben am Kern, Sieg und Niederlage, Pause. Spielzeit läuft getrennt von der Bildzeit.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public const int EnemyLayer = 8, PadLayer = 9, TowerLayer = 10, MenuLayer = 11;
        public enum Phase { Intro, Build, Wave, Victory, Defeat }

        public MissionData mission;
        public Phase phase = Phase.Intro;
        public int lives, gold, wave = -1, kills, phaseId, essenceCollected;
        public float countdown, playTime;
        public string message = "";
        public float messageUntil;
        public bool paused, pauseMenuOpen;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Tower> towers = new List<Tower>();
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public Enemy boss;
        public float DeltaTime { get; private set; }
        /// <summary>Spielereignisse (Tutorial, Drohne, Funk): wave_started, wave_cleared, lives_lost, boss, boss_defeated,
        /// victory, defeat, first_seen:<art>, shot, ship_hit, essence, built:<art>, upgraded, placing:, selected, boon:<id>.</summary>
        public event System.Action<string> OnEvent;
        public event System.Action<List<string>> OnBoonOffer;
        public bool offerOpen;                  // Segenskarten offen: Bauzeit steht
        public Tutorial tutorial;
        readonly HashSet<string> _seenTypes = new HashSet<string>();
        float _idleT = 18f, _midT;
        bool _leaked, _critical;

        struct Spawn { public float time; public string type; public int path; public string elite; }
        readonly List<Spawn> _queue = new List<Spawn>();
        readonly List<Enemy> _guards = new List<Enemy>();
        float _waveTime;
        int _seq;
        bool _xLatch;
        Transform _enemyRoot, _towerRoot, _fxRoot;
        AudioSource _music, _sfx;
        readonly Dictionary<string, float> _sfxLast = new Dictionary<string, float>();

        public static GameSession Create(MissionData m)
        {
            var go = new GameObject("Session");
            var s = go.AddComponent<GameSession>();
            s.mission = m;
            s.lives = m.lives;
            s.gold = m.gold;
            s._enemyRoot = new GameObject("Gegner").transform;
            s._towerRoot = new GameObject("Tuerme").transform;
            s._fxRoot = new GameObject("Effekte").transform;
            s._music = go.AddComponent<AudioSource>();
            s._music.loop = true; s._music.volume = 0.5f;
            s._sfx = go.AddComponent<AudioSource>();
            s._sfx.spatialBlend = 0f;
            s.PlayMusic("theme_game");
            s.Say(m.commsStart.Length > 0 ? Comms.Speaker + ": " + m.commsStart : $"Mission {m.number}: {m.name}", 7f);
            s.countdown = 10f;
            Boons.Reset();
            if (m.number == 1 && !Tutorial.Done)
            {
                s.tutorial = new Tutorial(WorldTheme.Current.name);
                s.tutorial.Start();
                s.countdown = 60f;             // die Einführung führt zum Wellenstart (X)
            }
            return s;
        }

        void Update()
        {
            Buttons();
            DeltaTime = paused ? 0f : Time.deltaTime;
            if (paused) return;
            playTime += DeltaTime;
            tutorial?.Update(DeltaTime);
            switch (phase)
            {
                case Phase.Intro:
                case Phase.Build:
                    if (!offerOpen) countdown -= DeltaTime;
                    if (countdown <= 0f) StartWave(false);
                    _idleT -= DeltaTime;
                    if (_idleT <= 0f) { _idleT = Random.Range(22f, 32f); Radio(Comms.Line("idle"), 4f); }
                    break;
                case Phase.Wave:
                    StepWave();
                    break;
            }
            // Simulationsschritt: Gegner, Türme, Geschosse (Reihenfolge wie Game.sim_step)
            if (phase == Phase.Wave || phase == Phase.Build || phase == Phase.Intro)
            {
                var dt = DeltaTime;
                for (var i = enemies.Count - 1; i >= 0; i--) if (i < enemies.Count) enemies[i].Step(dt);
                foreach (var t in towers) t.Step(dt);
                for (var i = projectiles.Count - 1; i >= 0; i--) { var p = projectiles[i]; p.Step(dt); if (p.done) projectiles.RemoveAt(i); }
            }
        }

        void Buttons()
        {
            var x = XRInput.StartWave;
            if (x && !_xLatch && (phase == Phase.Build || phase == Phase.Intro) && !offerOpen) StartWave(true);
            _xLatch = x;

        }

        // ------------------------------------------------------------------------------------------------
        // Wellen
        void StartWave(bool early)
        {
            if (early && countdown > 0f && phase == Phase.Build)
            {
                var bonus = Mathf.FloorToInt(countdown * Balance.EarlyCallGoldPerSec);
                if (bonus > 0) { gold += bonus; Say($"Früher Start: +{bonus} Essenz", 2f); }
            }
            wave++;
            if (wave >= mission.waves.Count) { Win(); return; }
            var w = mission.waves[wave];
            _queue.Clear();
            _waveTime = 0f;
            var paths = Mathf.Max(mission.paths.Count, 1);
            for (var gi = 0; gi < w.groups.Count; gi++)
            {
                var g = w.groups[gi];
                var bossT = Balance.Enemy(g.type).boss;
                for (var i = 0; i < g.count; i++)
                {
                    var pid = 0;
                    if (paths > 1 && !bossT) pid = g.path >= 0 ? g.path % paths : (i + gi) % paths;
                    _queue.Add(new Spawn { time = g.delay + g.interval * i, type = g.type, path = pid, elite = g.elite ?? "" });
                }
            }
            _queue.Sort((p, q) => p.time.CompareTo(q.time));
            phase = Phase.Wave;
            countdown = -1f;
            _midT = 0f;
            var radio = Comms.StartText(mission, wave);
            Say($"Welle {wave + 1}/{mission.waves.Count}: {w.title}" + (radio.Length > 0 ? "\n" + Comms.Speaker + ": " + radio : ""), 4f);
            Sfx("wave_start", 0.8f);
            Fire("wave_started");
            if (w.boss) PlayMusic("theme_boss");
        }

        void StepWave()
        {
            _waveTime += DeltaTime;
            var total = _queue.Count + enemies.Count;
            while (_queue.Count > 0 && _queue[0].time <= _waveTime)
            {
                var s = _queue[0];
                _queue.RemoveAt(0);
                var e = SpawnEnemy(s.type, 0f, s.path);
                if (s.elite.Length > 0) e.MakeElite(s.elite);
            }
            // Zwischenstand einmal je Welle, wenn alle erschienen sind und noch viele laufen
            if (_queue.Count == 0 && enemies.Count >= 6 && _midT == 0f) { _midT = 1f; Radio(Comms.MidText(enemies.Count), 3f); }
            if (_queue.Count == 0 && enemies.Count == 0) WaveComplete();
        }

        public Enemy SpawnEnemy(string typeId, float atDist, int pid)
        {
            var t = Balance.Enemy(typeId);
            var x = wave >= 0 && wave < mission.hpIndex.Length ? mission.hpIndex[wave] : Mathf.Max(wave, 0);
            var f = wave >= 0 && wave < mission.hpWave.Length ? mission.hpWave[wave] : 1f;
            pid = Mathf.Clamp(pid, 0, mission.paths.Count - 1);
            var e = Enemy.Spawn(this, t, mission.paths[pid], pid, Balance.EnemyHp(t, x, f, mission.bossHp), atDist, ++_seq, _enemyRoot);
            enemies.Add(e);
            if (t.guardRange > 0f && !t.boss && !t.flying) _guards.Add(e);
            if (t.boss)
            {
                boss = e;
                Sfx("boss_roar", 0.9f);
                Say($"{t.name} greift an!\n{Comms.Speaker}: {Comms.Line("boss")}", 4f);
                Fire("boss");
            }
            if (_seenTypes.Add(t.id)) Fire("first_seen:" + t.id);
            return e;
        }

        void WaveComplete()
        {
            var w = mission.waves[wave];
            var income = 0;
            foreach (var t in towers) { t.Repair(); if (t.Kind == "teahouse") income += (int)t.Stats.Get("income"); }
            var reward = Mathf.RoundToInt(w.reward * Boons.RewardFactor);
            gold += reward + income;
            if (Boons.Has("interest")) gold += Mathf.Min(40, Mathf.FloorToInt(gold * 0.1f));
            phaseId++;
            Sfx("wave_clear", 0.7f);
            Fire("wave_cleared");
            if (wave + 1 >= mission.waves.Count) { Win(); return; }
            phase = Phase.Build;
            countdown = Balance.BuildTime;
            _idleT = 12f;
            Say($"Welle {wave + 1} geschafft: +{reward}{(income > 0 ? $" +{income} Generator" : "")} Essenz. Nächste in {Balance.BuildTime:0} s (X: sofort)\n{Comms.Speaker}: {Comms.ClearedText(mission, wave)}", 6f);
            if (w.boss) PlayMusic("theme_game");
            if (Boons.OfferAfter(wave, mission.waves.Count))
            {
                var cards = Boons.Roll(mission.towers);
                if (cards.Count > 0) OnBoonOffer?.Invoke(cards);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Schaden (Game.damage_enemy)
        public float DamageEnemy(Enemy e, float amount, string source, bool direct)
        {
            if (!e.alive) return 0f;
            amount *= Combat.TypeMult(e, source);
            if (e.vulnTime > 0f) amount *= 1f + e.vulnAmount;
            amount *= GuardMult(e);
            var dmg = direct ? Combat.DirectDamage(e, amount) : amount;
            e.sinceHit = 0f;
            e.OnHit();
            if (e.cloaked && (source == "kanzel" || source == "player_laser")) Reveal(e);
            var toHp = dmg;
            if (e.shield > 0f)
            {
                var ab = Mathf.Min(e.shield, dmg);
                e.shield -= ab; toHp -= ab;
                if (e.shield <= 0f) e.shield = 0f;
            }
            e.hp -= toHp;
            if (direct) e.HitFlash();
            if (e.hp <= 0f) Kill(e);
            return dmg;
        }

        float GuardMult(Enemy e)
        {
            if (e.flying || e.type.boss || e.type.guardRange > 0f || _guards.Count == 0) return 1f;
            foreach (var g in _guards)
            {
                if (g == e || !g.alive || g.pathId != e.pathId) continue;
                var gap = g.dist - e.dist;
                if (gap > 0f && gap <= g.type.guardRange * Balance.FieldM) { e.guarded = 1f; g.guarded = 1f; return g.type.guardMult; }
            }
            return 1f;
        }

        void Reveal(Enemy e)
        {
            if (!e.cloaked) return;
            e.cloaked = false;
            Sfx("ice_hit", 0.5f, e.Center);
        }

        void Kill(Enemy e)
        {
            e.alive = false; e.hp = 0f;
            kills++;
            Split(e);
            var gain = e.bounty + (e.bounty > 0 ? Boons.BountyAdd : 0);
            if (gain > 0) { gold += gain; essenceCollected += gain; Fire("essence"); }
            if (e.type.boss) Fire("boss_defeated");
            if (e.wildfireDmg > 0f && e.burnTime > 0f)
            {
                var wd = e.wildfireDmg; e.wildfireDmg = 0f;
                foreach (var o in EnemiesNear(e.Position, 1f * Balance.FieldM, true)) if (o != e) DamageEnemy(o, wd, "fire", true);
            }
            enemies.Remove(e);
            _guards.Remove(e);
            if (e == boss) boss = NextBoss();
            Sfx(e.type.boss ? "boss_down" : (e.type.size >= 1.3f ? "enemy_die_big" : "enemy_die"), 0.45f, e.Center);
            FxRing(e.Position, e.type.boss ? 2.6f : 0.9f, WorldTheme.Threat);
            e.Despawn();
        }

        Enemy NextBoss() { foreach (var o in enemies) if (o.alive && o.type.boss) return o; return null; }

        /// <summary>Packträger: beim Tod entstehen Schwärmer an derselben Wegstelle.</summary>
        void Split(Enemy e)
        {
            if (e.type.spawnOnDeath.Length == 0 || e.type.boss) return;
            for (var i = 0; i < e.type.spawnCount; i++)
            {
                var c = SpawnEnemy(e.type.spawnOnDeath, e.dist, e.pathId);
                c.lateral = e.lateral + (i - (e.type.spawnCount - 1) * 0.5f) * 0.35f;
            }
        }

        public void EnemyReached(Enemy e)
        {
            if (!e.alive) return;
            e.alive = false; e.leaked = true;
            enemies.Remove(e);
            _guards.Remove(e);
            if (e == boss) boss = NextBoss();
            lives = Mathf.Max(0, lives - e.damage);
            Sfx("life_lost", 0.8f);
            XRInput.Haptic(0.4f, 0.12f);
            Fire("lives_lost");
            if (!_leaked) { _leaked = true; Radio(Comms.Line("first_leak"), 3f); }
            else if (!_critical && lives > 0 && lives < mission.lives * 0.3f) { _critical = true; Radio(Comms.Line("critical"), 3f); }
            e.Despawn();
            if (lives <= 0) Lose();
        }

        // ------------------------------------------------------------------------------------------------
        // Gegnerfähigkeiten
        public bool SpotterMark(Enemy sp)
        {
            var r = sp.type.markRange * Balance.FieldM;
            Tower best = null; var bd = float.MaxValue;
            foreach (var t in towers)
            {
                if (!t.IsDefense || t.jamT > 0f) continue;
                var d = Planar(t.Position, sp.Position);
                if (d <= r && d < bd) { bd = d; best = t; }
            }
            if (best == null) return false;
            best.jamT = sp.type.markTime;
            FxRing(best.Position, 0.6f, new Color(0.7f, 0.35f, 1f));
            Sfx("ice_hit", 0.4f, best.Position);
            return true;
        }

        public void HealPulse(Enemy healer)
        {
            var r = healer.type.healRadius * Balance.FieldM;
            Enemy best = null; var bestMissing = 0f;
            foreach (var o in enemies)
            {
                if (!o.alive || (o == healer && !healer.type.healSelf) || !Combat.CanHeal(o)) continue;
                if (Planar(o.Position, healer.Position) > r) continue;
                var missing = o.maxHp - o.hp;
                if (missing > bestMissing) { bestMissing = missing; best = o; }
            }
            FxRing(healer.Position, healer.type.healRadius, new Color(0.7f, 1f, 0.5f));
            if (best == null) return;
            best.lastHeal = best.age;
            best.hp = Mathf.Min(best.maxHp, best.hp + best.maxHp * healer.type.healRatio);
            best.healGlow = 1f;
            Sfx("heal", 0.4f, best.Center);
        }

        public void BossSummon(Enemy b, string type, int count, bool quiet)
        {
            count = Mathf.Max(1, Mathf.RoundToInt(count * mission.summonMult));
            for (var i = 0; i < count; i++)
            {
                var d = Mathf.Max(0f, b.dist - (6f + i * 5f) / Balance.TilePath * Balance.FieldM);
                SpawnEnemy(type, d, b.pathId);
            }
            if (quiet) return;
            Sfx("boss_summon", 0.8f);
            Say($"{b.type.name} ruft Verstärkung!", 3f);
        }

        public void BossEnraged(Enemy b)
        {
            Sfx("boss_roar", 0.9f);
            Say($"{b.type.name} tobt und wird schneller!", 3f);
        }

        /// <summary>Barrikade auf dem Weg: Gegner hält davor und schlägt zu (Game.barrier_stop).</summary>
        public float BarrierStop(Enemy e, float d0, float d1)
        {
            var hold = Balance.TrapHold * Balance.FieldM;
            foreach (var t in towers)
            {
                if (t.Kind != "barricade" || t.trapHp <= 0f || !t.pad.pathDist.TryGetValue(e.pathId, out var td)) continue;
                var stop = td - hold;
                if (d0 <= stop + 0.1f && d1 > stop) { t.TrapAttack(e); return d0 <= stop ? Mathf.Max(d0, stop) : d0; }
            }
            return d1;
        }

        public void TrapBroken(Tower t)
        {
            Sfx("mortar_hit", 0.5f, t.Position);
            Say("Barrikade gebrochen – nach der Welle wird sie repariert", 3f);
        }

        // ------------------------------------------------------------------------------------------------
        // Türme: Zielwahl und Treffer
        public static float Planar(Vector3 a, Vector3 b) { var dx = a.x - b.x; var dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        public List<Enemy> EnemiesNear(Vector3 pos, float r, bool air)
        {
            var o = new List<Enemy>();
            foreach (var e in enemies) if (e.alive && e.IsTargetable && (air || !e.flying) && Planar(pos, e.Position) <= r) o.Add(e);
            return o;
        }

        public List<Enemy> FindTargets(Tower t, int n)
        {
            var r = t.RangeM;
            var air = t.CanTargetAir;
            var c = new List<Enemy>();
            foreach (var e in enemies) if (e.TowerTargetable && (air || !e.flying) && Planar(t.Position, e.Position) <= r) c.Add(e);
            if (c.Count > 1)
            {
                switch (t.targetMode)
                {
                    case 0: c.Sort((a, b) => b.dist.CompareTo(a.dist)); break;
                    case 3: c.Sort((a, b) => a.dist.CompareTo(b.dist)); break;
                    case 1: c.Sort((a, b) => b.hp != a.hp ? b.hp.CompareTo(a.hp) : b.dist.CompareTo(a.dist)); break;
                    case 2: var p = t.Position; c.Sort((a, b) => Planar(p, a.Position).CompareTo(Planar(p, b.Position))); break;
                }
                if (t.Kind == "ice")
                {
                    var fresh = new List<Enemy>(); var slowed = new List<Enemy>();
                    foreach (var e in c) (e.slowTime > 0.4f ? slowed : fresh).Add(e);
                    fresh.AddRange(slowed); c = fresh;
                }
            }
            if (c.Count > n) c.RemoveRange(n, c.Count - n);
            return c;
        }

        public void SpawnProjectile(Tower t, Enemy target) => projectiles.Add(Projectile.Spawn(this, t, target, _fxRoot));

        public void PlayShot(string kind)
        {
            switch (kind)
            {
                case "arrow": Sfx("arrow_shot", 0.35f); break;
                case "ice": Sfx("ice_shot", 0.35f); break;
                case "fire": Sfx("fire_shot", 0.45f); break;
                case "chain": Sfx("zap_shot", 0.35f); break;
                case "sniper": Sfx("sniper_shot", 0.45f); break;
                case "mortar": Sfx("mortar_shot", 0.45f); break;
            }
        }

        public void ChainLightning(Tower t, Enemy first)
        {
            var st = t.Stats;
            var dmg = st.Get("damage");
            var jr = Balance.ChainJumpRange * Balance.FieldM;
            var hit = new List<Enemy> { first };
            var cur = first;
            while (hit.Count < (int)st.Get("jumps", 1f))
            {
                Enemy best = null; var bd = jr;
                foreach (var e in enemies)
                {
                    if (!e.TowerTargetable || hit.Contains(e)) continue;
                    var d = Planar(cur.Position, e.Position);
                    if (d <= bd) { bd = d; best = e; }
                }
                if (best == null) break;
                hit.Add(best); cur = best;
            }
            var col = Balance.TowerColor["chain"];
            var prev = t.Muzzle;
            foreach (var e in hit) { FxLine(prev, e.Center, col, 0.08f); prev = e.Center; }
            for (var i = 0; i < hit.Count; i++) DamageEnemy(hit[i], dmg * Mathf.Pow(st.Get("falloff", 0.7f), i), "chain", true);
        }

        public void SniperShot(Tower t, Enemy target)
        {
            if (!target.alive) return;
            var st = t.Stats;
            var dmg = st.Get("damage");
            if (target.vulnTime > 0f) dmg *= 1f + target.vulnAmount;
            var real = Combat.DirectDamage(target, dmg, st.Get("armor_pierce"));
            FxLine(t.Muzzle, target.Center, Balance.TowerColor["sniper"], 0.1f);
            var vt = target.vulnTime; target.vulnTime = 0f;
            DamageEnemy(target, real, "sniper", false);
            target.vulnTime = vt;
            if (target.alive) target.HitFlash();
        }

        public void ProjectileHit(Projectile p)
        {
            var st = p.stats;
            var tgt = p.target != null && p.target.alive ? p.target : null;
            switch (p.kind)
            {
                case "arrow":
                    if (tgt != null)
                    {
                        var adm = st.Get("damage");
                        if (tgt.IsSlowed) adm *= 1f + Balance.BrittleBonus;
                        DamageEnemy(tgt, adm, "arrow", true);
                        Sfx("arrow_hit", 0.25f, p.targetPos);
                    }
                    break;
                case "ice":
                {
                    var splash = st.Get("splash") * Balance.FieldM;
                    var hits = splash > 0f ? EnemiesNear(p.targetPos, splash, true) : (tgt != null ? new List<Enemy> { tgt } : new List<Enemy>());
                    if (splash > 0f) FxRing(p.targetPos, st.Get("splash"), new Color(0.7f, 0.92f, 1f));
                    foreach (var e in hits)
                    {
                        DamageEnemy(e, st.Get("damage"), "ice", true);
                        if (e.alive)
                        {
                            Combat.ApplySlow(e, st.Get("slow"), st.Get("slow_time"));
                            var perma = st.Get("permafrost");
                            if (perma > 0f) { e.vulnAmount = Mathf.Max(e.vulnAmount, perma); e.vulnTime = Mathf.Max(e.vulnTime, st.Get("slow_time") + 0.2f); }
                        }
                    }
                    Sfx("ice_hit", 0.3f, p.targetPos);
                    break;
                }
                case "fire":
                {
                    var r = st.Get("splash") * Balance.FieldM;
                    foreach (var e in EnemiesNear(p.targetPos, r, true))
                    {
                        var dmg = st.Get("damage");
                        if (e.IsSlowed) { dmg *= 1f + Balance.ThermoShockBonus; e.slowTime = 0f; e.slowAmount = 0f; }
                        DamageEnemy(e, dmg, "fire", true);
                        if (e.alive) Combat.ApplyBurn(e, st.Get("burn_dps"), st.Get("burn_time"), st.Get("shred"));
                    }
                    FxRing(p.targetPos, st.Get("splash"), new Color(1f, 0.7f, 0.3f));
                    Sfx("fire_hit", 0.45f, p.targetPos);
                    break;
                }
                case "mortar":
                {
                    var r = st.Get("splash") * Balance.FieldM;
                    foreach (var e in EnemiesNear(p.targetPos, r, false))
                    {
                        var k = Mathf.Clamp01(Planar(p.targetPos, e.Position) / Mathf.Max(r, 0.001f));
                        DamageEnemy(e, st.Get("damage") * Mathf.Lerp(1f, Balance.MortarEdgeFactor, k), "mortar", true);
                    }
                    FxRing(p.targetPos, st.Get("splash"), Balance.TowerColor["mortar"]);
                    Sfx("mortar_hit", 0.5f, p.targetPos);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Bauen, Verbessern, Verkaufen
        public bool Running => phase != Phase.Victory && phase != Phase.Defeat;

        public string CanBuild(string kind, BuildPad pad)
        {
            if (!Running) return "Das Spiel ist beendet";
            var st = Balance.Structure(kind);
            if (st == null) return "Unbekanntes Bauwerk";
            if (st.IsTrap != pad.trap) return st.IsTrap ? "Sperren stehen nur mitten auf der Fahrbahn" : "Hier kann nicht gebaut werden";
            if (pad.tower != null) return "Dieses Feld ist belegt";
            if (st.IsTrap) foreach (var t in towers) if (t.IsTrap && Planar(t.Position, pad.position) < 2.5f * Balance.FieldM) return "Zu nah an einer anderen Sperre";
            if (gold < BuildCost(kind)) return "Nicht genug Essenz";
            return "";
        }

        public Tower Build(string kind, BuildPad pad)
        {
            var reason = CanBuild(kind, pad);
            if (reason.Length > 0) { Say(reason, 2f); Sfx("ui_error", 0.5f); return null; }
            gold -= BuildCost(kind);
            var t = Tower.Create(this, Balance.Structure(kind), pad, _towerRoot);
            t.invested = BuildCost(kind);
            t.builtThisPhase = phase == Phase.Build || phase == Phase.Intro;
            pad.tower = t;
            towers.Add(t);
            FxRing(pad.position, 0.8f, new Color(1f, 0.95f, 0.8f));
            Sfx("build", 0.6f, pad.position);
            Fire("built:" + kind);
            return t;
        }

        public bool Upgrade(Tower t)
        {
            var cost = UpgradeCost(t.Kind, t.level);
            if (cost < 0) { Say("Höchste Stufe erreicht", 2f); return false; }
            if (gold < cost) { Say("Nicht genug Essenz", 2f); Sfx("ui_error", 0.5f); return false; }
            gold -= cost;
            t.invested += cost;
            t.Upgrade();
            Sfx("upgrade", 0.6f, t.Position);
            Fire("upgraded");
            return true;
        }

        public int SellValue(Tower t)
        {
            var ratio = Balance.SellRatio;
            if ((phase == Phase.Build || phase == Phase.Intro) && t.builtThisPhase) ratio = Balance.SellRatioSamePhase;
            return Mathf.FloorToInt(t.invested * ratio);
        }

        public int Sell(Tower t)
        {
            var v = SellValue(t);
            gold += v;
            towers.Remove(t);
            t.pad.tower = null;
            Destroy(t.gameObject);
            Sfx("sell", 0.6f, t.Position);
            return v;
        }

        public int BuildCost(string kind) => Mathf.RoundToInt(Balance.BuildCost(kind) * Boons.CostFactor);
        public int UpgradeCost(string kind, int level) { var c = Balance.UpgradeCost(kind, level); return c < 0 ? -1 : Mathf.RoundToInt(c * Boons.CostFactor); }

        /// <summary>Segenskarte gewählt: Sofortwirkungen hier, Turmwerte über Boons.ApplyMods (Game.take_boon).</summary>
        public void TakeBoon(string id)
        {
            Boons.Take(id);
            switch (id)
            {
                case "lives": lives += 3; break;
                case "repair": lives += 5; break;
                case "treasure": gold += 150 + 50 * (wave / Boons.Every); break;
            }
            var c = Boons.Cards[id];
            Say($"Segen: {c.name}. {c.text}", 4f);
            Fire("boon:" + id);
        }

        public void Fire(string ev)
        {
            tutorial?.OnEvent(ev);
            OnEvent?.Invoke(ev);
        }

        /// <summary>Funkmeldung des Protokolls (Comms).</summary>
        public void Radio(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Say(Comms.Speaker + ": " + text, seconds);
        }

        // ------------------------------------------------------------------------------------------------
        void Win()
        {
            phase = Phase.Victory;
            Say((mission.commsEnd.Length > 0 ? mission.commsEnd + "\n" : "") + "Mission geschafft.", 999f);
            Sfx("victory", 0.9f);
            PlayMusic("theme_menu");
            Fire("victory");
            Progress.RecordMission(mission.number, true, mission.lives - lives, mission.lives, Boons.ShardFactor);
        }

        void Lose()
        {
            phase = Phase.Defeat;
            foreach (var e in enemies.ToArray()) e.Despawn();
            enemies.Clear(); _guards.Clear();
            Say("Der Kern ist gefallen.\n" + Comms.Speaker + ": " + Comms.Line("defeat"), 999f);
            Sfx("defeat", 0.9f);
            PlayMusic("theme_menu");
            Fire("defeat");
            Progress.RecordMission(mission.number, false, mission.lives - lives, mission.lives, 1f);
        }

        public void Say(string text, float seconds) { message = text; messageUntil = Time.time + seconds; }
        public string MessageNow => Time.time < messageUntil ? message : "";

        /// <summary>Klang aus Resources/Audio/SFX; gleicher Klang höchstens alle 40 ms (Stimmenbegrenzung wie Audio.gd).</summary>
        public void Sfx(string name, float volume, Vector3? at = null)
        {
            if (_sfxLast.TryGetValue(name, out var last) && Time.time - last < 0.04f) return;
            _sfxLast[name] = Time.time;
            var clip = Resources.Load<AudioClip>("Audio/SFX/" + name);
            if (clip == null) return;
            if (at.HasValue) AudioSource.PlayClipAtPoint(clip, at.Value, volume);
            else _sfx.PlayOneShot(clip, volume);
        }

        void PlayMusic(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/Music/" + name);
            if (clip == null || _music.clip == clip) return;
            _music.clip = clip;
            _music.Play();
        }

        // Effekte: Ring auf dem Deck, Linie (Blitz, Leuchtspur); kurzlebige Objekte
        public void FxRing(Vector3 at, float radiusFields, Color col)
        {
            var k = new MeshKit();
            var r = radiusFields * Balance.FieldM;
            k.Ring(at + Vector3.up * 0.03f, r - 0.08f, r, 24);
            var go = k.Spawn("Ring", NeonMaterials.Neon("fx_" + ColorUtility.ToHtmlStringRGB(col), col), _fxRoot, false);
            go.AddComponent<FxLifetime>().life = 0.35f;
        }

        public void FxLine(Vector3 a, Vector3 b, Color col, float life)
        {
            var go = new GameObject("Linie");
            go.transform.SetParent(_fxRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = 0.05f; lr.endWidth = 0.03f;
            lr.sharedMaterial = NeonMaterials.Neon("fx_" + ColorUtility.ToHtmlStringRGB(col), col);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FxLifetime>().life = life;
        }
    }

    /// <summary>Löscht ein Effektobjekt nach kurzer Zeit (schrumpfend).</summary>
    public sealed class FxLifetime : MonoBehaviour
    {
        public float life = 0.3f;
        float _t;
        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= life) Destroy(gameObject);
        }
    }
}
