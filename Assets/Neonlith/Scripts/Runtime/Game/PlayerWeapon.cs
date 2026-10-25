using Neonlith.Data;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Waffe in der Hand (XRRifle + Weapon + Arsenal, docs/STAND.md): Kind der Zielpose des Controllers. Pistole:
    /// ein Schuss je Abzug. Sturmgewehr: Dauerfeuer, Ladeschuss mit dem Griff der Waffenhand (laden, loslassen feuert
    /// mit Flächenschaden, kostet 6 Schuss), zweite Hand am Vordergriff richtet die Waffe vom Griff zur zweiten Hand
    /// aus. Sniper: Einzelschuss, breiter Strahl, kräftiges Rütteln. Treffer sofort per Strahl, Leuchtspur huscht
    /// zum Ziel, Magazin und Nachladen mit der Waffe nach unten, Vibration je Schuss. Modell aus Kästen.
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        public ArmType arm;
        public GameSession session;
        public Transform otherHand;
        public int ammo;
        public bool reloading;
        public float reloadT, chargeT;
        public bool charging;
        public int shots;

        Transform _muzzle;
        LineRenderer _tracer;
        float _tracerT, _cooldown;
        bool _semiLatch, _emptyLatch, _gripLatch;
        Transform _impact;
        float _impactT;
        AudioSource _audio;
        AudioClip _shot, _empty, _chargeShot;
        Vector3 _kick;

        public static PlayerWeapon Attach(Transform hand, Transform otherHand, ArmType arm, GameSession session, WorldTheme theme)
        {
            var go = new GameObject("Waffe");
            go.transform.SetParent(hand, false);
            var w = go.AddComponent<PlayerWeapon>();
            w.arm = arm; w.session = session; w.otherHand = otherHand;
            w.ammo = arm.mag;
            w.BuildModel(theme);
            w._audio = go.AddComponent<AudioSource>();
            w._audio.spatialBlend = 1f; w._audio.minDistance = 0.5f;
            w._shot = Resources.Load<AudioClip>("Audio/SFX/" + (arm.id == "pistol" ? "pistol_shot" : arm.id == "sniper" ? "sniper_shot" : "kanzel_shot"));
            w._chargeShot = Resources.Load<AudioClip>("Audio/SFX/kanzel_charge_shot");
            w._empty = Resources.Load<AudioClip>("Audio/SFX/lock_click");
            return w;
        }

        void BuildModel(WorldTheme theme)
        {
            var white = new MeshKit(); var black = new MeshKit(); var accent = new MeshKit();
            var len = arm.id == "pistol" ? 0.21f : arm.id == "sniper" ? 1.1f : 0.62f;
            var barrelZ = len * 0.55f;
            white.Box(new Vector3(0f, 0.015f, barrelZ * 0.5f), new Vector3(0.03f, 0.03f, barrelZ));
            black.Box(new Vector3(0f, -0.005f, barrelZ * 0.45f), new Vector3(0.028f, 0.012f, barrelZ * 0.9f));
            black.Box(new Vector3(0f, -0.06f, -0.015f), new Vector3(0.028f, 0.1f, 0.035f));
            black.Box(new Vector3(0f, 0.018f, barrelZ + 0.01f), new Vector3(0.012f, 0.012f, 0.03f));
            accent.Box(new Vector3(0f, 0.034f, barrelZ - 0.005f), new Vector3(0.004f, 0.006f, 0.006f));
            accent.Box(new Vector3(0f, 0.034f, 0.01f), new Vector3(0.012f, 0.004f, 0.004f));
            accent.Strip(new Vector3(0f, 0.031f, 0.02f), new Vector3(0f, 0.031f, barrelZ - 0.02f), 0.004f);
            if (arm.id != "pistol")
            {
                black.Box(new Vector3(0f, -0.01f, -0.16f), new Vector3(0.03f, 0.05f, 0.22f));
                black.Box(new Vector3(0f, -0.045f, barrelZ * 0.5f), new Vector3(0.026f, 0.07f, 0.03f));
                if (arm.id == "rifle") black.Box(new Vector3(0f, -0.07f, 0.04f), new Vector3(0.024f, 0.09f, 0.05f));   // Magazin
                if (arm.id == "sniper") { black.Box(new Vector3(0f, 0.05f, 0.1f), new Vector3(0.03f, 0.03f, 0.2f)); accent.Ring(new Vector3(0f, 0.05f, 0.2f), 0.012f, 0.018f, 8); }
            }
            white.Spawn("Weiss", NeonMaterials.Lit("weapon_white", new Color(0.92f, 0.93f, 0.95f), 0.35f, 0.55f), transform, false);
            black.Spawn("Schwarz", NeonMaterials.Lit("weapon_black", new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.4f), transform, false);
            accent.Spawn("Akzent", NeonMaterials.Neon("neon", theme.neon), transform, false);
            _muzzle = new GameObject("Muendung").transform;
            _muzzle.SetParent(transform, false);
            _muzzle.localPosition = new Vector3(0f, 0.018f, barrelZ + 0.03f);

            var tr = new GameObject("Leuchtspur");
            _tracer = tr.AddComponent<LineRenderer>();
            _tracer.useWorldSpace = true; _tracer.positionCount = 2;
            _tracer.startWidth = 0.012f; _tracer.endWidth = 0.006f;
            _tracer.sharedMaterial = NeonMaterials.Neon("tracer", new Color(1f, 0.12f, 0.08f));
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
            if (session != null && (session.paused || session.pauseMenuOpen)) return;
            var dt = Time.deltaTime;
            _cooldown -= dt;
            if (_tracer.enabled) { _tracerT -= dt; if (_tracerT <= 0f) _tracer.enabled = false; }
            if (_impact.gameObject.activeSelf)
            {
                _impactT -= dt;
                _impact.localScale = Vector3.one * (0.06f + 0.1f * Mathf.Clamp01(_impactT / 0.15f));
                if (_impactT <= 0f) _impact.gameObject.SetActive(false);
            }
            TwoHand();
            _kick = Vector3.Lerp(_kick, Vector3.zero, 1f - Mathf.Exp(-dt * 14f));
            transform.localPosition = _kick;
            Reload(dt);
            Charge(dt);
            Fire();
        }

        /// <summary>Zweite Hand am Vordergriff: die Waffe richtet sich vom Griff zur zweiten Hand aus (XRRifle, 4.99.65 geglättet).</summary>
        void TwoHand()
        {
            if (!arm.twoHand || otherHand == null || XRInput.ForegripHeld < 0.6f) { transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, 0.3f); return; }
            var d = otherHand.position - transform.parent.position;
            if (d.magnitude < 0.15f || d.magnitude > 0.8f || Vector3.Dot(d.normalized, transform.parent.forward) < 0.3f) return;
            var want = Quaternion.LookRotation(d.normalized, transform.parent.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 0.5f);
        }

        void Reload(float dt)
        {
            var down = Vector3.Dot(transform.forward, Vector3.down) > 0.7f;
            if (down && ammo < arm.mag)
            {
                if (!reloading) { reloading = true; reloadT = 0f; charging = false; chargeT = 0f; }
                reloadT += dt;
                if (reloadT >= arm.reload)
                {
                    ammo = arm.mag; reloading = false;
                    XRInput.Haptic(0.5f, 0.08f);
                    session?.Sfx("lock_open", 0.6f);
                }
            }
            else reloading = false;
        }

        /// <summary>Ladeschuss (Sturmgewehr): Griff halten lädt, loslassen feuert mit Flächenschaden.</summary>
        void Charge(float dt)
        {
            if (!arm.charge) return;
            var grip = XRInput.WeaponGrip > 0.6f;
            if (grip && !reloading && ammo >= Balance.ChargeAmmo)
            {
                if (!charging) { charging = true; chargeT = 0f; }
                chargeT = Mathf.Min(Balance.ChargeTime, chargeT + dt);
                if (Mathf.FloorToInt(chargeT * 10f) != Mathf.FloorToInt((chargeT - dt) * 10f)) XRInput.Haptic(0.15f + 0.4f * chargeT / Balance.ChargeTime, 0.03f);
            }
            else if (charging)
            {
                charging = false;
                if (chargeT >= Balance.ChargeTime) ChargedShot();
                chargeT = 0f;
            }
            _gripLatch = grip;
        }

        void ChargedShot()
        {
            ammo -= Balance.ChargeAmmo;
            shots++;
            var origin = _muzzle.position; var dir = transform.forward;
            var end = origin + dir * 200f;
            if (Physics.Raycast(origin, dir, out var hit, 200f, ~((1 << GameSession.PadLayer) | (1 << GameSession.MenuLayer)), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var r = Balance.ChargeSplash * Balance.FieldM;
                foreach (var e in session.EnemiesNear(hit.point, r, true)) session.DamageEnemy(e, Balance.ChargeDamage, "kanzel", true);
                var direct = hit.collider.GetComponentInParent<Enemy>();
                if (direct != null && direct.alive && GameSession.Planar(direct.Position, hit.point) > r) session.DamageEnemy(direct, Balance.ChargeDamage, "kanzel", true);
                session.FxRing(hit.point, Balance.ChargeSplash, new Color(1f, 0.3f, 0.2f));
                _impact.position = hit.point + hit.normal * 0.02f; _impact.gameObject.SetActive(true); _impactT = 0.3f;
            }
            Tracer(origin, end, 0.03f, 0.12f);
            if (_chargeShot != null) _audio.PlayOneShot(_chargeShot, 0.9f);
            XRInput.Haptic(1f, 0.15f);
            _kick = new Vector3(0f, 0.01f, -0.04f);
            _cooldown = 0.35f;
        }

        void Fire()
        {
            var pressed = XRInput.WeaponTrigger > 0.6f;
            if (!pressed) { _semiLatch = false; _emptyLatch = false; return; }
            if (arm.semi && _semiLatch) return;
            if (_cooldown > 0f || reloading || charging) return;
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
            ammo--; shots++;
            var origin = _muzzle.position; var dir = transform.forward;
            var end = origin + dir * 200f;
            var mask = ~((1 << GameSession.PadLayer) | (1 << GameSession.MenuLayer));
            var hitSomething = arm.id == "sniper"
                ? Physics.SphereCast(origin, 0.12f, dir, out var hit, 200f, mask, QueryTriggerInteraction.Ignore)
                : Physics.Raycast(origin, dir, out hit, 200f, mask, QueryTriggerInteraction.Ignore);
            if (hitSomething)
            {
                end = hit.point;
                var e = hit.collider.GetComponentInParent<Enemy>();
                if (e != null && session != null) session.DamageEnemy(e, arm.damage, "kanzel", true);
                _impact.position = hit.point + hit.normal * 0.02f; _impact.gameObject.SetActive(true); _impactT = 0.15f;
            }
            Tracer(origin, end, arm.id == "sniper" ? 0.02f : 0.012f, 0.06f);
            if (_shot != null) _audio.PlayOneShot(_shot, 0.8f);
            XRInput.Haptic(arm.id == "sniper" ? 1f : arm.id == "rifle" ? 0.5f : 0.7f, arm.id == "sniper" ? 0.12f : 0.05f);
            _kick = arm.id == "sniper" ? new Vector3(0f, 0.012f, -0.05f) : arm.id == "rifle" ? new Vector3(0f, 0.003f, -0.012f) : new Vector3(0f, 0.006f, -0.02f);
        }

        void Tracer(Vector3 a, Vector3 b, float width, float life)
        {
            _tracer.SetPosition(0, a); _tracer.SetPosition(1, b);
            _tracer.startWidth = width; _tracer.endWidth = width * 0.5f;
            _tracer.enabled = true; _tracerT = life;
        }
    }
}
