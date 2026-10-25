using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Turm, Gebäude oder Sperre auf einem Bauplatz (scripts/game/tower.gd): Zielwahl (Erster, Stärkster, Nächster,
    /// Letzter), Abklingzeit, Markierung durch den Spieher, Barrikade mit Haltbarkeit und Dornen, Laserzaun mit
    /// Schaden und Bremse im Gitter. Darstellung aus Kästen und Prismen mit der Erkennungsfarbe des Turms
    /// (Look.TOWER_COL), Stufe als Ringe, Reichweitenring bei Auswahl.
    /// </summary>
    public sealed class Tower : MonoBehaviour
    {
        public GameSession session;
        public StructureType type;
        public BuildPad pad;
        public int level, invested, targetMode, shots;
        public float cooldown, jamT, animT, totalDamage;
        public bool builtThisPhase;
        public float trapHp, trapHit = 99f;
        public int trapZap;
        float _attack;
        readonly List<Enemy> _attackers = new List<Enemy>();
        Transform _body, _rangeRing;
        Enemy _aim;
        public Enemy AimTarget => _aim;

        public string Kind => type.id;
        public Vector3 Position => transform.position;
        public Dictionary<string, float> Stats => Boons.ApplyMods(type.id, Balance.Stats(type.id, level));
        public bool IsTrap => type.IsTrap;
        public bool IsDefense => type.IsDefense;
        public bool CanTargetAir => type.air;
        public float RangeM => Stats.Get("range") * Balance.FieldM;
        public Vector3 Muzzle => transform.position + Vector3.up * 1.6f;

        public static Tower Create(GameSession s, StructureType t, BuildPad pad, Transform parent)
        {
            var go = new GameObject("Turm_" + t.id);
            go.transform.SetParent(parent, false);
            go.transform.position = pad.position;
            go.transform.rotation = Quaternion.Euler(0f, pad.yawDeg, 0f);
            var tw = go.AddComponent<Tower>();
            tw.session = s; tw.type = t; tw.pad = pad;
            tw.invested = t.Cost(0);
            tw.targetMode = t.defaultTarget;
            if (t.IsTrap) { tw.trapHp = tw.TrapMax; }
            tw.BuildVisual();
            go.layer = GameSession.TowerLayer;
            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.up * 1.0f;
            col.size = new Vector3(1.6f, 2.0f, 1.6f);
            return tw;
        }

        public float TrapMax => Stats.Get("hp");
        public bool TrapBroken => type.id == "barricade" && trapHp <= 0f;
        public void TrapAttack(Enemy e) { _attack += Mathf.Max(1, e.damage); _attackers.Add(e); }
        public void Repair() { if (type.id == "barricade") trapHp = TrapMax; }
        public void Upgrade() { level++; Repair(); BuildVisual(); }
        public void CycleTargetMode() { targetMode = (targetMode + 1) % Balance.TargetModes.Length; }
        public string TargetModeName => Balance.TargetModes[targetMode];

        public void Step(float dt)
        {
            animT += dt;
            if (IsTrap) { TrapStep(dt); return; }
            if (!IsDefense) return;
            if (jamT > 0f) { jamT = Mathf.Max(0f, jamT - dt); _aim = null; return; }
            cooldown -= dt;
            if (cooldown > 0f) return;
            var st = Stats;
            var targets = session.FindTargets(this, (int)st.Get("targets", 1f));
            if (targets.Count == 0) { cooldown = 0.1f; _aim = null; return; }
            Fire(targets);
            cooldown = st.Get("cooldown");
        }

        void TrapStep(float dt)
        {
            var st = Stats;
            trapHit += dt;
            if (type.id == "barricade")
            {
                if (_attack > 0f && trapHp > 0f)
                {
                    trapHp -= Balance.TrapHit * Mathf.Min(_attack, Balance.TrapAttackers) * dt;
                    trapHit = 0f;
                    var th = st.Get("thorns");
                    if (th > 0f) foreach (var e in _attackers) if (e != null && e.alive) totalDamage += session.DamageEnemy(e, th * dt, type.id, false);
                    if (trapHp <= 0f) { trapHp = 0f; session.TrapBroken(this); }
                }
                _attack = 0f; _attackers.Clear();
                if (_body != null) _body.localScale = new Vector3(1f, TrapBroken ? 0.25f : 1f, 1f);
            }
            else if (type.id == "laserfence")
            {
                var half = Balance.FenceHalf * Balance.FieldM;
                var dmg = st.Get("dps") * dt;
                var slow = st.Get("slow");
                trapZap = 0;
                foreach (var e in session.enemies)
                {
                    if (!e.alive || e.flying || !pad.pathDist.TryGetValue(e.pathId, out var td)) continue;
                    if (Mathf.Abs(e.dist - td) > half) continue;
                    trapZap++;
                    if (slow > 0f) Combat.ApplySlow(e, slow, 0.4f);
                    totalDamage += session.DamageEnemy(e, dmg, type.id, false);
                }
                if (trapZap > 0) trapHit = 0f;
            }
        }

        void Fire(List<Enemy> targets)
        {
            _aim = targets[0];
            switch (type.id)
            {
                case "chain": session.ChainLightning(this, targets[0]); shots++; session.PlayShot(type.id); return;
                case "sniper": session.SniperShot(this, targets[0]); shots++; session.PlayShot(type.id); return;
            }
            foreach (var t in targets) { session.SpawnProjectile(this, t); shots++; }
            session.PlayShot(type.id);
        }

        void Update()
        {
            // Waffe dreht zum Ziel (nur Darstellung)
            if (_body != null && IsDefense && _aim != null && _aim.alive)
            {
                var d = _aim.Center - _body.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 1f - Mathf.Exp(-Time.deltaTime * 8f));
            }
        }

        public void ShowRange(bool on)
        {
            if (_rangeRing != null) _rangeRing.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------------------------------------------
        /// <summary>Darstellung je Art (TowerModels in Godot sind viel reicher; hier Kästen und Prismen, Akzent in der Turmfarbe).</summary>
        void BuildVisual()
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            var col = Balance.TowerColor.TryGetValue(type.id, out var tc) ? tc : WorldTheme.Current.neon;
            var dark = new MeshKit();
            var light = new MeshKit();
            var accent = new MeshKit();
            var F = Balance.FieldM;
            if (IsTrap)
            {
                // quer zur Fahrbahn (Bauplatz dreht in Laufrichtung)
                if (type.id == "barricade")
                {
                    dark.Box(new Vector3(0f, 0.45f, 0f), new Vector3(MissionData.RoadWidth * F - 0.3f, 0.9f, 0.5f));
                    accent.Strip(new Vector3(-MissionData.RoadWidth * F * 0.5f + 0.2f, 0.92f, 0f), new Vector3(MissionData.RoadWidth * F * 0.5f - 0.2f, 0.92f, 0f), 0.08f);
                    for (var i = 1; i <= level; i++) accent.Box(new Vector3(0f, 0.3f + 0.25f * i, 0.26f), new Vector3(0.5f, 0.08f, 0.02f));
                }
                else
                {
                    var hw = MissionData.RoadWidth * F * 0.5f - 0.2f;
                    dark.Box(new Vector3(-hw, 0.6f, 0f), new Vector3(0.18f, 1.2f, 0.18f));
                    dark.Box(new Vector3(hw, 0.6f, 0f), new Vector3(0.18f, 1.2f, 0.18f));
                    for (var i = 0; i <= level + 1; i++)
                    {
                        var y = 0.25f + 0.28f * i;
                        accent.Box(new Vector3(0f, y, 0f), new Vector3(hw * 2f, 0.03f, 0.03f));
                    }
                }
            }
            else
            {
                // Sockel (füllt den Bauplatz, KlarBridge.PAD_SIZE 1,08 Felder) und Körper
                dark.Prism(new Vector3(0f, 0f, 0f), 0.5f * F, 0.25f, 8, Mathf.PI / 8f);
                accent.Ring(new Vector3(0f, 0.255f, 0f), 0.42f * F, 0.46f * F, 8, Mathf.PI / 8f);
                var bodyKit = new MeshKit();
                var bodyAcc = new MeshKit();
                switch (type.id)
                {
                    case "arrow":
                        bodyKit.Box(new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.5f, 0.6f));
                        bodyKit.Box(new Vector3(-0.12f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 0.9f));
                        bodyKit.Box(new Vector3(0.12f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 0.9f));
                        bodyAcc.Box(new Vector3(0f, 1.1f, -0.1f), new Vector3(0.5f, 0.05f, 0.3f));
                        break;
                    case "ice":
                        bodyKit.Prism(new Vector3(0f, 0.5f, 0f), 0.32f, 0.9f, 6);
                        bodyAcc.Prism(new Vector3(0f, 1.4f, 0f), 0.18f, 0.5f, 6);
                        break;
                    case "fire":
                        bodyKit.Box(new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 0.4f, 0.8f));
                        bodyKit.Prism(new Vector3(0f, 0.9f, 0.1f), 0.3f, 0.5f, 8);
                        bodyAcc.Ring(new Vector3(0f, 1.41f, 0.1f), 0.12f, 0.3f, 8);
                        break;
                    case "chain":
                        bodyKit.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 0.3f));
                        for (var i = 0; i < 4; i++) bodyAcc.Ring(new Vector3(0f, 0.5f + 0.25f * i, 0f), 0.22f, 0.36f, 8);
                        bodyAcc.Prism(new Vector3(0f, 1.2f, 0f), 0.28f, 0.12f, 8);
                        break;
                    case "sniper":
                        bodyKit.Box(new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.4f, 0.7f));
                        bodyKit.Box(new Vector3(0f, 0.95f, 0.7f), new Vector3(0.12f, 0.12f, 1.6f));
                        bodyAcc.Strip(new Vector3(0f, 1.015f, 0.1f), new Vector3(0f, 1.015f, 1.4f), 0.04f);
                        break;
                    case "mortar":
                        bodyKit.Box(new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 0.3f, 0.8f));
                        bodyKit.Box(new Vector3(0f, 1.1f, 0.15f), new Vector3(0.36f, 0.9f, 0.36f));
                        bodyAcc.Ring(new Vector3(0f, 1.56f, 0.15f), 0.1f, 0.2f, 8);
                        break;
                    case "teahouse":
                        bodyKit.Prism(new Vector3(0f, 0.25f, 0f), 0.35f, 1.6f, 4, Mathf.PI / 4f);
                        bodyAcc.Prism(new Vector3(0f, 1.85f, 0f), 0.2f, 0.3f, 4, Mathf.PI / 4f);
                        break;
                }
                for (var i = 1; i <= level; i++) accent.Ring(new Vector3(0f, 0.26f + 0.09f * i, 0f), 0.47f * F, 0.49f * F, 8, Mathf.PI / 8f);
                var body = new GameObject("Waffe").transform;
                body.SetParent(transform, false);
                _body = body;
                bodyKit.Spawn("Koerper", NeonMaterials.Lit("tower_body", new Color(0.86f, 0.88f, 0.92f), 0.5f, 0.5f), body, false);
                bodyAcc.Spawn("Akzent", NeonMaterials.Neon("tower_" + type.id, col), body, false);
                // Reichweitenring (bei Auswahl)
                if (IsDefense)
                {
                    var rk = new MeshKit();
                    var r = RangeM;
                    rk.Ring(new Vector3(0f, 0.02f, 0f), r - 0.06f, r, 48);
                    var ring = rk.Spawn("Reichweite", NeonMaterials.Neon("tower_" + type.id, col), transform, false);
                    ring.transform.rotation = Quaternion.identity;
                    ring.SetActive(false);
                    _rangeRing = ring.transform;
                }
            }
            dark.Spawn("Sockel", NeonMaterials.Lit("tower_dark", new Color(0.12f, 0.13f, 0.15f), 0.5f, 0.4f), transform, false);
            light.Spawn("Hell", NeonMaterials.Lit("tower_body", new Color(0.86f, 0.88f, 0.92f), 0.5f, 0.5f), transform, false);
            accent.Spawn("Akzent", NeonMaterials.Neon("tower_" + type.id, col), transform, false);
            if (IsTrap) _body = transform.GetChild(0);
        }
    }
}
