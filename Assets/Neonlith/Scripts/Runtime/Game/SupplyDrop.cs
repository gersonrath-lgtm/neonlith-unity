using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Nachschub (scripts/game/supply_drop.gd, 4.49): ein kleines Transportschiff zieht während einer Welle aus dem
    /// Nebel über die Karte und verschwindet wieder; darunter hängt eine Kiste. Schießt der Spieler sie ab, gibt es
    /// Essenz (Grundwert plus je Welle). Nur die Kiste ist treffbar, Türme schießen nicht auf sie.
    /// </summary>
    public sealed class SupplyDrop : MonoBehaviour
    {
        const float FlyTime = 17f, Range = 0.66f, Side = 4f, Height = 2.9f, CrateDrop = 0.62f, HpSeconds = 0.9f, BreakTime = 1.2f;
        public const int FirstWave = 1; public const float Chance = 0.6f; public static readonly Vector2 Delay = new Vector2(5f, 13f);
        const int ValueBase = 35, ValueWave = 6;

        public GameSession session;
        public float hp, maxHp, t, breakT;
        public int value, hits;
        public bool broken, escaped;
        Vector3 _a, _b;
        Transform _ship, _crate;

        public static SupplyDrop Create(GameSession s, MissionData m, int wave, float dps, WorldTheme theme)
        {
            var go = new GameObject("Nachschub");
            var d = go.AddComponent<SupplyDrop>();
            d.session = s;
            var F = Balance.FieldM;
            var size = m.gridSize;
            // Höchste Fahrbahn: Deckhöhe aus den Wegen
            var deckY = 0f;
            foreach (var p in m.paths) foreach (var v in p) deckY = Mathf.Max(deckY, v.y);
            var c = new Vector3(size * 0.5f * F, deckY + (Height + CrateDrop) * F, size * 0.5f * F);
            var ang = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var side = new Vector3(-dir.z, 0f, dir.x) * Random.Range(-Side, Side) * F;
            var r = Mathf.Max(size * Range, (size * 0.5f + 4f + Side) / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z))) * F;
            d._a = c - dir * r + side; d._b = c + dir * r + side;
            d.maxHp = Mathf.Max(1f, dps * HpSeconds); d.hp = d.maxHp;
            d.value = ValueBase + ValueWave * Mathf.Max(0, wave);
            d.Build(theme);
            return d;
        }

        void Build(WorldTheme theme)
        {
            var white = new MeshKit(); var dark = new MeshKit(); var acc = new MeshKit();
            white.Box(Vector3.zero, new Vector3(1.6f, 0.5f, 2.6f));
            white.Box(new Vector3(0f, 0.35f, -0.4f), new Vector3(0.9f, 0.3f, 1.2f));
            dark.Box(new Vector3(-1.1f, -0.1f, 0.2f), new Vector3(0.5f, 0.3f, 1.0f));
            dark.Box(new Vector3(1.1f, -0.1f, 0.2f), new Vector3(0.5f, 0.3f, 1.0f));
            acc.Ring(new Vector3(-1.1f, -0.26f, 0.2f), 0.12f, 0.2f, 8); acc.Ring(new Vector3(1.1f, -0.26f, 0.2f), 0.12f, 0.2f, 8);
            _ship = new GameObject("Schiff").transform; _ship.SetParent(transform, false);
            white.Spawn("Rumpf", NeonMaterials.Lit("ship_white", new Color(0.9f, 0.91f, 0.94f), 0.4f, 0.5f), _ship, false);
            dark.Spawn("Triebwerke", NeonMaterials.Lit("ship_dark", new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.4f), _ship, false);
            acc.Spawn("Leuchten", NeonMaterials.Neon("neon", theme.neon), _ship, false);
            var crate = new MeshKit(); var cacc = new MeshKit();
            crate.Box(Vector3.zero, new Vector3(1.2f, 0.96f, 1.2f));
            cacc.Box(new Vector3(0f, 0f, 0f), new Vector3(1.24f, 0.08f, 0.08f)); cacc.Box(new Vector3(0f, 0f, 0f), new Vector3(0.08f, 0.08f, 1.24f));
            _crate = new GameObject("Kiste").transform; _crate.SetParent(transform, false);
            crate.Spawn("Kiste", NeonMaterials.Lit("crate", new Color(0.25f, 0.27f, 0.3f), 0.5f, 0.4f), _crate, false);
            cacc.Spawn("Band", NeonMaterials.Neon("crate_band", new Color(1f, 0.78f, 0.35f)), _crate, false);
            var col = _crate.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.28f, 1.04f, 1.28f);
            _crate.gameObject.layer = GameSession.RaidLayer;
            _crate.gameObject.AddComponent<ShotTarget>().onShot = Damage;
            // Leine
            var rope = new MeshKit(); rope.Box(new Vector3(0f, -CrateDrop * Balance.FieldM * 0.5f, 0f), new Vector3(0.03f, CrateDrop * Balance.FieldM, 0.03f));
            rope.Spawn("Leine", NeonMaterials.Lit("ship_dark", new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.4f), _ship, false);
            Place();
        }

        public bool Alive => !broken && !escaped;
        public bool Done => escaped || (broken && breakT >= BreakTime);
        float Progress => Mathf.Clamp01(t / FlyTime);
        Vector3 Dir => (_b - _a).normalized;

        void Place()
        {
            var p = Vector3.Lerp(_a, _b, Progress) + Vector3.up * (Mathf.Sin(t * 1.7f) * 0.16f);
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(Dir, Vector3.up);
            _crate.position = p - Vector3.up * (CrateDrop * Balance.FieldM) + Vector3.right * (Mathf.Sin(t * 2.3f) * 0.06f);
            _crate.rotation = transform.rotation;
        }

        public void Step(float dt)
        {
            t += dt;
            if (broken) breakT += dt;
            else if (t >= FlyTime) escaped = true;
            Place();
        }

        bool Damage(float dmg)
        {
            if (!Alive) return false;
            hits++;
            hp -= dmg;
            if (hp <= 0f)
            {
                hp = 0f; broken = true;
                _crate.gameObject.SetActive(false);
                session.SupplyBroken(this);
                return true;
            }
            return false;
        }
    }
}
