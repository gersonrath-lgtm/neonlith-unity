using Neonlith.Data;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Waffe in der Hand (Gegenstück zu XRRifle + Weapon im Standmodus, docs/STAND.md): Kind der Zielpose des
    /// Controllers, Lage und Richtung allein aus dem Tracking. Abzug feuert (Pistole: ein Schuss je Abzug), Treffer
    /// sofort per Strahl, Leuchtspur huscht zum Ziel, Magazin und Nachladen mit der Waffe nach unten (4.99.17),
    /// Vibration je Schuss. Modell vorerst aus Kästen in Weiß und Schwarz (WeaponModels kommt mit Etappe 4).
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        public ArmType arm;
        public GameSession session;
        public int ammo;
        public bool reloading;
        public float reloadT;
        public int shots;

        Transform _muzzle;
        LineRenderer _tracer;
        float _tracerT, _cooldown;
        bool _semiLatch, _emptyLatch;
        Transform _impact;
        float _impactT;
        AudioSource _audio;
        AudioClip _shot, _empty;

        public static PlayerWeapon Attach(Transform hand, ArmType arm, GameSession session, WorldTheme theme)
        {
            var go = new GameObject("Waffe");
            go.transform.SetParent(hand, false);
            var w = go.AddComponent<PlayerWeapon>();
            w.arm = arm;
            w.session = session;
            w.ammo = arm.mag;
            w.BuildModel(theme);
            w._audio = go.AddComponent<AudioSource>();
            w._audio.spatialBlend = 1f;
            w._audio.minDistance = 0.5f;
            w._shot = Resources.Load<AudioClip>("Audio/SFX/" + (arm.id == "pistol" ? "pistol_shot" : arm.id == "sniper" ? "sniper_shot" : "kanzel_shot"));
            w._empty = Resources.Load<AudioClip>("Audio/SFX/lock_click");
            return w;
        }

        /// <summary>Pistole 21 cm (Schlitten, Rahmen, geneigter Griff), Sturmgewehr und Sniper länger. Weiß, schwarzes Polymer, Akzent in der Weltfarbe.</summary>
        void BuildModel(WorldTheme theme)
        {
            var white = new MeshKit();
            var black = new MeshKit();
            var accent = new MeshKit();
            var len = arm.id == "pistol" ? 0.21f : arm.id == "sniper" ? 1.1f : 0.62f;
            var barrelZ = len * 0.55f;
            // Schlitten / Gehäuse (vorn ist +Z: Zielpose des Controllers zeigt nach vorn)
            white.Box(new Vector3(0f, 0.015f, barrelZ * 0.5f), new Vector3(0.03f, 0.03f, barrelZ));
            black.Box(new Vector3(0f, -0.005f, barrelZ * 0.45f), new Vector3(0.028f, 0.012f, barrelZ * 0.9f));
            // Griff, leicht geneigt
            black.Box(new Vector3(0f, -0.06f, -0.015f), new Vector3(0.028f, 0.1f, 0.035f));
            // Lauf und Mündung
            black.Box(new Vector3(0f, 0.018f, barrelZ + 0.01f), new Vector3(0.012f, 0.012f, 0.03f));
            // Kimme und Korn, Akzentlinie in der Weltfarbe
            accent.Box(new Vector3(0f, 0.034f, barrelZ - 0.005f), new Vector3(0.004f, 0.006f, 0.006f));
            accent.Box(new Vector3(0f, 0.034f, 0.01f), new Vector3(0.012f, 0.004f, 0.004f));
            accent.Strip(new Vector3(0f, 0.031f, 0.02f), new Vector3(0f, 0.031f, barrelZ - 0.02f), 0.004f);
            if (arm.id != "pistol")
            {
                // Schaft und Vordergriff
                black.Box(new Vector3(0f, -0.01f, -0.16f), new Vector3(0.03f, 0.05f, 0.22f));
                black.Box(new Vector3(0f, -0.045f, barrelZ * 0.5f), new Vector3(0.026f, 0.07f, 0.03f));
            }
            white.Spawn("Weiss", NeonMaterials.Lit("weapon_white", new Color(0.92f, 0.93f, 0.95f), 0.35f, 0.55f), transform, false);
            black.Spawn("Schwarz", NeonMaterials.Lit("weapon_black", new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.4f), transform, false);
            accent.Spawn("Akzent", NeonMaterials.Neon("neon", theme.neon), transform, false);

            _muzzle = new GameObject("Muendung").transform;
            _muzzle.SetParent(transform, false);
            _muzzle.localPosition = new Vector3(0f, 0.018f, barrelZ + 0.03f);

            var tr = new GameObject("Leuchtspur");
            tr.transform.SetParent(null, false);     // im Weltraum, nicht an der Hand
            _tracer = tr.AddComponent<LineRenderer>();
            _tracer.useWorldSpace = true;
            _tracer.positionCount = 2;
            _tracer.startWidth = 0.012f;
            _tracer.endWidth = 0.006f;
            _tracer.sharedMaterial = NeonMaterials.Neon("tracer", new Color(1f, 0.12f, 0.08f));  // WeaponConfig.tracer_color
            _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracer.enabled = false;

            var imp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            imp.name = "Einschlag";
            Destroy(imp.GetComponent<Collider>());
            imp.transform.SetParent(tr.transform, false);
            imp.transform.localScale = Vector3.one * 0.12f;
            imp.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Neon("hi", theme.hi);
            imp.SetActive(false);
            _impact = imp.transform;
        }

        void Update()
        {
            if (session != null && session.paused) return;
            var dt = Time.deltaTime;
            _cooldown -= dt;
            if (_tracer.enabled)
            {
                _tracerT -= dt;
                if (_tracerT <= 0f) _tracer.enabled = false;
            }
            if (_impact.gameObject.activeSelf)
            {
                _impactT -= dt;
                _impact.localScale = Vector3.one * (0.06f + 0.1f * Mathf.Clamp01(_impactT / 0.15f));
                if (_impactT <= 0f) _impact.gameObject.SetActive(false);
            }
            Reload(dt);
            Fire();
        }

        /// <summary>Nachladen, solange die Waffe nach unten zeigt (XRStand.RELOAD_DOWN 0,7 ≈ 45°) und das Magazin nicht voll ist.</summary>
        void Reload(float dt)
        {
            var down = Vector3.Dot(transform.forward, Vector3.down) > 0.7f;
            if (down && ammo < arm.mag)
            {
                if (!reloading) { reloading = true; reloadT = 0f; }
                reloadT += dt;
                if (reloadT >= arm.reload)
                {
                    ammo = arm.mag;
                    reloading = false;
                    XRInput.Haptic(0.5f, 0.08f);
                    session?.Sfx("lock_open", 0.6f);
                }
            }
            else reloading = false;
        }

        void Fire()
        {
            var pressed = XRInput.WeaponTrigger > 0.6f;
            if (!pressed) { _semiLatch = false; _emptyLatch = false; return; }
            if (arm.semi && _semiLatch) return;
            if (_cooldown > 0f || reloading) return;
            if (ammo <= 0)
            {
                if (!_emptyLatch)
                {
                    _emptyLatch = true;
                    if (_empty != null) _audio.PlayOneShot(_empty, 0.5f);
                    session?.Say("Magazin leer: Waffe nach unten halten", 1.5f);
                }
                return;
            }
            _semiLatch = true;
            _cooldown = arm.cooldown;
            ammo--;
            shots++;
            var origin = _muzzle.position;
            var dir = transform.forward;
            var end = origin + dir * 200f;
            if (Physics.Raycast(origin, dir, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var e = hit.collider.GetComponentInParent<Enemy>();
                if (e != null) e.Hit(arm.damage);
                _impact.position = hit.point + hit.normal * 0.02f;
                _impact.gameObject.SetActive(true);
                _impactT = 0.15f;
            }
            _tracer.SetPosition(0, origin);
            _tracer.SetPosition(1, end);
            _tracer.enabled = true;
            _tracerT = 0.06f;
            if (_shot != null) _audio.PlayOneShot(_shot, 0.8f);
            XRInput.Haptic(arm.id == "sniper" ? 1f : 0.7f, arm.id == "sniper" ? 0.12f : 0.05f);
        }
    }
}
