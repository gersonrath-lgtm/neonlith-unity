using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Flugalien (scripts/game/gun_raider.gd, 4.49): greift im Schwarm die Plattform des Spielers an. Kommt aus dem
    /// Nebel, schwingt in einem Bogen heran, bleibt an einem Angriffspunkt nahe der Plattform stehen, lädt sichtbar
    /// auf und schießt einen Bolzen. Danach ein neuer Angriffspunkt; nach drei Schüssen dreht es ab. Nur ein Treffer
    /// direkt auf den Körper zählt. Türme schießen nicht auf Flugaliens.
    /// </summary>
    public sealed class GunRaider : MonoBehaviour
    {
        public enum State { Wait, Approach, Charge, Relocate, Flee, Dead }
        const float ApproachTime = 4.6f, RelocateTime = 1.7f, ChargeTime = 1.9f, FleeTime = 3f, AttackDist = 0.2f, AttackLift = 0.07f, HpSeconds = 0.22f;
        const int Shots = 3;

        public GameSession session;
        public State state = State.Wait;
        public float hp, maxHp, radius, size, t, wait, charge, phase;
        public int shotsLeft = Shots, hits;
        Vector3 _from, _ctrl, _to;
        float _dur = 1f;
        Transform _body, _wingL, _wingR;
        Renderer _core;
        MaterialPropertyBlock _mpb;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public static GunRaider Create(GameSession s, Vector3 gun, float radiusM, float sizeM, float dps, float delay, WorldTheme theme)
        {
            var go = new GameObject("Flugalien");
            var r = go.AddComponent<GunRaider>();
            r.session = s; r.radius = radiusM; r.size = sizeM; r.wait = delay;
            r.phase = Random.value * Mathf.PI * 2f;
            r.maxHp = Mathf.Max(0.5f, dps * HpSeconds); r.hp = r.maxHp;
            var ang = Random.value * Mathf.PI * 2f;
            var lift = Random.Range(0.15f, 0.45f) * radiusM;
            go.transform.position = gun + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radiusM + Vector3.up * lift;
            r.Build(theme);
            return r;
        }

        void Build(WorldTheme theme)
        {
            _body = new GameObject("Koerper").transform; _body.SetParent(transform, false);
            var dark = new MeshKit(); dark.Prism(new Vector3(0f, -size * 0.35f, 0f), size * 0.55f, size * 0.7f, 6);
            dark.Spawn("Rumpf", NeonMaterials.Lit("raider_dark", new Color(0.1f, 0.05f, 0.08f), 0.3f, 0.5f), _body, false);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(_body, false); core.transform.localScale = Vector3.one * size * 0.5f; core.transform.localPosition = Vector3.forward * size * 0.3f;
            _core = core.GetComponent<Renderer>();
            _core.sharedMaterial = NeonMaterials.Glow("raider_core", WorldTheme.Threat * 0.5f, WorldTheme.Threat, 1.4f);
            _mpb = new MaterialPropertyBlock();
            var wing = new MeshKit(); wing.Box(new Vector3(size * 0.6f, 0f, 0f), new Vector3(size * 1.2f, 0.03f, size * 0.5f));
            _wingL = wing.Spawn("FluegelL", NeonMaterials.Lit("raider_dark", new Color(0.1f, 0.05f, 0.08f), 0.3f, 0.5f), _body, false).transform;
            _wingL.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _wingR = wing.Spawn("FluegelR", NeonMaterials.Lit("raider_dark", new Color(0.1f, 0.05f, 0.08f), 0.3f, 0.5f), _body, false).transform;
            var col = gameObject.AddComponent<SphereCollider>(); col.radius = size;
            gameObject.layer = GameSession.RaidLayer;
            gameObject.AddComponent<ShotTarget>().onShot = Damage;
            foreach (Transform c in _body) c.gameObject.layer = GameSession.RaidLayer;
        }

        public bool Alive => state != State.Dead;
        public bool Targetable => state != State.Dead && state != State.Wait;
        public bool Gone => state == State.Dead || (state == State.Flee && t >= FleeTime);

        /// <summary>Ein Schritt; true, wenn in diesem Schritt ein Schuss fällt.</summary>
        public bool Step(float dt, Vector3 gun, bool gunOk)
        {
            if (state == State.Dead) return false;
            phase += dt; t += dt;
            var shot = false;
            switch (state)
            {
                case State.Wait:
                    if (t >= wait) { Fly(AttackPoint(gun), ApproachTime * Random.Range(0.85f, 1.15f), 0.6f); state = State.Approach; }
                    break;
                case State.Approach:
                case State.Relocate:
                    transform.position = Bez(Mathf.Clamp01(t / _dur));
                    if (!gunOk) Flee();
                    else if (t >= _dur) { state = State.Charge; t = 0f; charge = 0f; }
                    break;
                case State.Charge:
                    transform.position = _to + Vector3.up * (Mathf.Sin(phase * 2.6f) * size * 0.12f);
                    charge = Mathf.Clamp01(t / ChargeTime);
                    if (!gunOk) Flee();
                    else if (t >= ChargeTime)
                    {
                        charge = 0f; shotsLeft--; shot = true;
                        if (shotsLeft <= 0) Flee();
                        else { Fly(AttackPoint(gun), RelocateTime, 0.35f); state = State.Relocate; }
                    }
                    break;
                case State.Flee:
                    transform.position = Bez(Mathf.Clamp01(t / _dur));
                    break;
            }
            // Darstellung: Blick zur Plattform, Flügelschlag, Aufladen glüht
            var look = gun - transform.position;
            if (look.sqrMagnitude > 1e-4f) _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-dt * 5f));
            var flap = Mathf.Sin(phase * 9f) * 28f;
            _wingL.localRotation = Quaternion.Euler(0f, 180f, -flap); _wingR.localRotation = Quaternion.Euler(0f, 0f, flap);
            _mpb.SetColor(EmissionColor, Color.Lerp(WorldTheme.Threat.linear * 1.4f, Color.white * 4f, charge * charge));
            _core.SetPropertyBlock(_mpb);
            return shot;
        }

        bool Damage(float dmg)
        {
            if (!Targetable) return false;
            hits++; hp -= dmg;
            if (hp <= 0f) { hp = 0f; state = State.Dead; session.RaiderKilled(this); return true; }
            return false;
        }

        Vector3 AttackPoint(Vector3 gun)
        {
            var ang = Random.value * Mathf.PI * 2f;
            return gun + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius * AttackDist * Random.Range(0.8f, 1.2f) + Vector3.up * (radius * AttackLift * Random.Range(0.6f, 1.4f));
        }

        void Fly(Vector3 p, float dur, float swing)
        {
            _from = transform.position; _to = p; _dur = Mathf.Max(dur, 0.1f); t = 0f;
            var mid = (_from + _to) * 0.5f;
            var d = _to - _from;
            var side = Vector3.Cross(d, Vector3.up);
            if (side.magnitude < 1e-4f) side = Vector3.right;
            side = side.normalized * (Random.value < 0.5f ? 1f : -1f);
            _ctrl = mid + side * d.magnitude * swing + Vector3.up * (d.magnitude * 0.15f);
        }

        void Flee()
        {
            var away = transform.position - _to; away.y = 0f;
            if (away.magnitude < 1e-3f) away = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            var p = transform.position + away.normalized * radius * 1.2f + Vector3.up * (radius * 0.5f);
            Fly(p, FleeTime, 0.4f);
            state = State.Flee;
        }

        Vector3 Bez(float u) => Vector3.Lerp(Vector3.Lerp(_from, _ctrl, u), Vector3.Lerp(_ctrl, _to, u), u);
    }
}
