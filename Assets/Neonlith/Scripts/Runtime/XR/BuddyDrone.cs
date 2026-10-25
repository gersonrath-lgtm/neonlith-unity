using Neonlith.Game;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.XR
{
    /// <summary>
    /// Gizmo, die Begleitdrohne (scripts/xr/buddy_drone.gd, 4.99.57): keine Waffe, keine Steuerung. Schwebt links
    /// vorn im Blickfeld (34° links, 0,85 m, 10 cm unter den Augen), hält Abstand von Blick- und Zielkegel, folgt mit
    /// einer weichen Feder. Kleiner Kopf derselben Bauart wie Rook: weiße Schale, dunkles Visier mit zwei Sehschlitzen
    /// in der Weltfarbe, Ohrkapseln als Triebwerke, Antenne. Reaktionen: Welle beginnt (überrascht), erste Begegnung
    /// (neugierig, Ring über dem Gegner), Treffer am Kern (erschrocken, weicht zurück), Welle geschafft (Hüpfer und
    /// Pirouette), Boss, Sieg, Niederlage, Schüsse (Zusammenzucken).
    /// </summary>
    public sealed class BuddyDrone : MonoBehaviour
    {
        const float IdleYaw = 34f, IdleDist = 0.85f, IdleDrop = 0.1f, AvoidGaze = 24f, Spring = 26f, Damp = 8f, Bob = 0.012f;
        public Transform head;
        public GameSession session;
        public string act = "idle", mood = "neutral";
        public bool talking;
        Vector3 _pos, _vel;
        float _actT, _actLen, _t, _spin;
        Transform _body, _eyeL, _eyeR, _ant;
        Renderer _eyeRl, _eyeRr;
        float _quiet = 99f;
        Vector3 _pingAt; float _pingT; Transform _ping;

        public static BuddyDrone Create(Transform head, GameSession session, WorldTheme theme)
        {
            var go = new GameObject("Gizmo");
            var d = go.AddComponent<BuddyDrone>();
            d.head = head; d.session = session;
            d.Build(theme);
            session.OnEvent += d.React;
            return d;
        }

        void Build(WorldTheme theme)
        {
            _body = new GameObject("Kopf").transform;
            _body.SetParent(transform, false);
            var white = new MeshKit(); var dark = new MeshKit(); var accent = new MeshKit();
            white.Prism(new Vector3(0f, -0.06f, 0f), 0.065f, 0.12f, 10);                     // Schale
            dark.Box(new Vector3(0f, 0.005f, 0.05f), new Vector3(0.1f, 0.035f, 0.03f));     // Visier
            white.Box(new Vector3(-0.08f, -0.01f, 0f), new Vector3(0.03f, 0.05f, 0.05f));   // Ohrkapseln (Triebwerke)
            white.Box(new Vector3(0.08f, -0.01f, 0f), new Vector3(0.03f, 0.05f, 0.05f));
            accent.Ring(new Vector3(-0.08f, -0.036f, 0f), 0.012f, 0.02f, 8);
            accent.Ring(new Vector3(0.08f, -0.036f, 0f), 0.012f, 0.02f, 8);
            white.Spawn("Schale", NeonMaterials.Lit("buddy_white", new Color(0.92f, 0.93f, 0.95f), 0.35f, 0.55f), _body, false);
            dark.Spawn("Visier", NeonMaterials.Lit("buddy_dark", new Color(0.05f, 0.05f, 0.06f), 0.2f, 0.6f), _body, false);
            accent.Spawn("Akzent", NeonMaterials.Neon("neon", theme.neon), _body, false);
            _eyeL = Eye(-0.022f, theme); _eyeR = Eye(0.022f, theme);
            var ant = new MeshKit();
            ant.Box(new Vector3(0f, 0.09f, -0.02f), new Vector3(0.004f, 0.06f, 0.004f));
            ant.Box(new Vector3(0f, 0.122f, -0.02f), new Vector3(0.012f, 0.012f, 0.012f));
            _ant = ant.Spawn("Antenne", NeonMaterials.Neon("hi", theme.hi), _body, false).transform;
            var ping = new MeshKit();
            ping.Ring(Vector3.zero, 0.5f, 0.6f, 24);
            _ping = ping.Spawn("Hinweisring", NeonMaterials.Neon("hi", theme.hi), null, false).transform;
            _ping.gameObject.SetActive(false);
        }

        Transform Eye(float x, WorldTheme theme)
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.016f, 0.006f, 0.004f));
            var go = k.Spawn("Auge", NeonMaterials.Neon("neon", theme.neon), _body, false);
            go.transform.localPosition = new Vector3(x, 0.005f, 0.066f);
            return go.transform;
        }

        /// <summary>Reaktion auf ein Spielereignis (BuddyDrone.react).</summary>
        public void React(string kind)
        {
            switch (kind)
            {
                case "wave_started": Start("alert", "surprised", 1.1f); break;
                case "wave_cleared": Start("happy", "happy", 2.2f); break;
                case "lives_lost": Start("scared", "scared", 1.3f); break;
                case "boss": Start("alert", "surprised", 1.6f); Ping(true); break;
                case "boss_defeated": case "victory": Start("happy", "happy", 3.2f); break;
                case "defeat": Start("sad", "sad", 4f); break;
                case "shot": _quiet = 0f; if (act == "idle" || act == "focus") Start("flinch", "surprised", 0.3f); break;
                default:
                    if (kind.StartsWith("first_seen")) { Start("curious", "curious", 1.8f); Ping(false); }
                    break;
            }
        }

        void Start(string a, string m, float secs) { act = a; mood = m; _actT = secs; _actLen = secs; if (a == "happy") _spin = 0f; }

        void Ping(bool boss)
        {
            Enemy best = null; var bd = float.MaxValue;
            foreach (var e in session.enemies)
            {
                if (!e.alive || (boss && !e.type.boss)) continue;
                var d = Vector3.Distance(e.Position, head.position);
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return;
            _pingAt = best.Center + Vector3.up * 1.2f; _pingT = 1.6f;
            _ping.gameObject.SetActive(true);
        }

        void Update()
        {
            if (head == null) return;
            var dt = Time.deltaTime;
            _t += dt; _quiet += dt;
            if (_actT > 0f) { _actT -= dt; if (_actT <= 0f) { act = session.phase == GameSession.Phase.Wave ? "focus" : "idle"; mood = "neutral"; } }
            // Ankerpunkt links vorn, nie in der Blickmitte
            var fwd = head.forward; var flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            var yaw = IdleYaw + (act == "scared" ? 14f : 0f);
            var dir = Quaternion.AngleAxis(-yaw, Vector3.up) * flat;
            var dist = IdleDist + (act == "scared" ? 0.25f : 0f) + (act == "curious" ? -0.12f : 0f);
            var anchor = head.position + dir * dist + Vector3.up * (-IdleDrop + (act == "happy" ? 0.12f * Mathf.Abs(Mathf.Sin(_t * 9f)) : 0f));
            var toAnchor = anchor - head.position;
            if (Vector3.Angle(toAnchor, fwd) < AvoidGaze) anchor = head.position + Quaternion.AngleAxis(-AvoidGaze, Vector3.up) * flat * dist;
            if (_pos == Vector3.zero) _pos = anchor;
            var acc = (anchor - _pos) * Spring - _vel * Damp;
            _vel += acc * dt; _pos += _vel * dt;
            transform.position = _pos + Vector3.up * (Bob * Mathf.Sin(_t * 2.1f));
            // Blick: zum Spieler, im Kampf zu den Gegnern, bei Pirouette drehen
            var look = head.position - transform.position;
            if ((act == "focus" || act == "alert") && session.enemies.Count > 0) look = session.enemies[0].Center - transform.position;
            if (_pingT > 0f) look = _pingAt - transform.position;
            if (act == "happy") { _spin += dt * 540f; transform.rotation = Quaternion.Euler(0f, _spin, 0f); }
            else if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, act == "curious" ? 18f * Mathf.Sin(_t * 3f) : 0f), 1f - Mathf.Exp(-dt * 6f));
            _body.localRotation = Quaternion.Euler(Vector3.Dot(_vel, transform.forward) * -25f, 0f, Vector3.Dot(_vel, transform.right) * 25f);
            // Augen nach Stimmung: neutral schmal, überrascht groß, glücklich hoch, erschrocken weit auseinander, traurig geneigt
            var eyeScale = mood == "surprised" ? new Vector3(1.2f, 2.2f, 1f) : mood == "happy" ? new Vector3(1.1f, 1.4f, 1f) : mood == "scared" ? new Vector3(0.9f, 2.6f, 1f) : mood == "sad" ? new Vector3(1.1f, 0.6f, 1f) : Vector3.one;
            _eyeL.localScale = eyeScale; _eyeR.localScale = eyeScale;
            var tilt = mood == "sad" ? 12f : mood == "curious" ? -8f : 0f;
            _eyeL.localRotation = Quaternion.Euler(0f, 0f, -tilt); _eyeR.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _ant.localRotation = Quaternion.Euler(Mathf.Clamp(-_vel.z * 40f, -30f, 30f), 0f, Mathf.Clamp(_vel.x * 40f, -30f, 30f));
            if (_pingT > 0f)
            {
                _pingT -= dt;
                _ping.position = _pingAt + Vector3.up * (0.2f * Mathf.Sin(_t * 6f));
                _ping.localScale = Vector3.one * (0.8f + 0.2f * Mathf.Sin(_t * 8f));
                if (_pingT <= 0f) _ping.gameObject.SetActive(false);
            }
        }
    }
}
