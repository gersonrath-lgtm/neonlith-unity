using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.UI;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Halle der Tore (docs/WELTENTORE.md, WorldGates + BridgeLobby in Godot, vereinfacht): achteckige Nabe in einer
    /// achteckigen Halle (1 Feld = 1,4 m), in den vier Himmelsrichtungen je ein Tor für eine Welt (Norden I,
    /// Osten II, Süden III, Westen IV). Offen = Fenster mit dem Himmel der Welt, dahinter ein Balkon mit der
    /// Missionswahl; verschlossen = dunkle Tür mit Schloss in der Neonfarbe der Welt. Zeigen hebt hervor, Abzug führt
    /// hindurch (die Nabe gleitet in 1,1 s), auf dem Balkon die Missionskacheln mit Startabfrage (JA | NEIN).
    /// Links vorn die Lauftafel (Splitter, Sterne, Waffe), rechts vorn die Turmwand (Basis). Funk der Halle.
    /// </summary>
    public sealed class HubSession : MonoBehaviour
    {
        public static HubSession Instance { get; private set; }
        const float HallM = 1.4f;                 // BridgeLobby.HALL_M
        const float GateD = 27.8f * HallM;        // Torebene vom Mittelpunkt
        const float PlatR = 4f * HallM;           // Nabe
        const float OpenHW = 2.8f * HallM, OpenH = 7.5f * HallM, FrameT = 0.42f * HallM, Housing = 2.11f * HallM;
        const float CeilH = 12f * HallM, CoreY = (12f - 4.6f) * HallM, CoreR = 1.9f * HallM, RoomD = 7f * HallM;

        public XRRig rig;
        public PauseMenu pauseMenu;
        public string message = ""; public float messageUntil;
        readonly List<Gate> _gates = new List<Gate>();
        LineRenderer _ray;
        Material _rayIdle, _rayHot;
        bool _trigLatch, _bLatch;
        int _onBalcony = -1;          // Welt 0..3, -1 = Nabe
        float _glide = -1f; Vector3 _glideFrom, _glideTo;
        MenuPanel _missions, _consoleLeft, _consoleRight;
        TextMesh _hud;
        AudioSource _music;
        float _radioT = 20f;
        static readonly string[] Numerals = { "I", "II", "III", "IV" };
        static readonly string[] Hall = {
            "Zentrale an Außenposten. Tore kalibriert, Halle dicht. Nimm dir Zeit.", "Keine Signale von draußen. Die Stille hält. Nutz sie.",
            "Energieknoten der Halle stabil. Der Kern dreht, also leben wir noch.", "Zur Erinnerung: jede gehaltene Anlage kommt in die Chronik. Jede.",
            "Wir hören nichts von den anderen Anlagen. Das ist kein gutes Zeichen.", "Die Tore bleiben zu, bis du bereit bist. Niemand drängt." };

        sealed class Gate
        {
            public int world; public Vector3 dir, center; public GameObject root; public Collider col; public TextMesh title, status; public bool open; public Transform window;
        }

        public static HubSession Create(XRRig rig)
        {
            var go = new GameObject("Halle");
            var h = go.AddComponent<HubSession>();
            Instance = h;
            h.rig = rig;
            h.Build();
            h._music = go.AddComponent<AudioSource>();
            h._music.loop = true; h._music.volume = 0.45f * Progress.MusicVolume;
            var clip = Resources.Load<AudioClip>("Audio/Music/theme_menu");
            if (clip != null) { h._music.clip = clip; h._music.Play(); }
            var amb = Resources.Load<AudioClip>("Audio/SFX/amb_hall");
            if (amb != null) { var a = go.AddComponent<AudioSource>(); a.clip = amb; a.loop = true; a.volume = 0.3f; a.Play(); }
            return h;
        }

        public void Say(string text, float secs) { message = text; messageUntil = Time.time + secs; }
        public void Sfx(string name, float vol) { var c = Resources.Load<AudioClip>("Audio/SFX/" + name); if (c != null) AudioSource.PlayClipAtPoint(c, rig.head.transform.position, vol * Progress.SfxVolume); }
        public void ApplyVolumes() { if (_music != null) _music.volume = 0.45f * Progress.MusicVolume; }

        // ------------------------------------------------------------------------------------------------
        void Build()
        {
            var theme = WorldTheme.Themes[1];     // Kernstil der Halle: Welt II (Menü, Hangar)
            var metal = NeonMaterials.Lit("hall_metal", theme.ground, 0.6f, 0.45f);
            var dark = NeonMaterials.Lit("hall_dark", theme.ground2, 0.5f, 0.4f);
            var neon = NeonMaterials.Neon("hall_neon", theme.neon);
            var k = new MeshKit(); var kd = new MeshKit(); var kn = new MeshKit();
            // Nabe (Platte mit Leuchtlinie am Rand) und Boden der Halle
            k.Prism(new Vector3(0f, -0.3f, 0f), PlatR, 0.3f, 8, Mathf.PI / 8f);
            kn.Ring(new Vector3(0f, 0.004f, 0f), PlatR - 0.25f, PlatR - 0.1f, 8, Mathf.PI / 8f);
            kd.Prism(new Vector3(0f, -0.6f, 0f), GateD + 2f, 0.55f, 8, Mathf.PI / 8f);
            // Bodengitter-Ring um die Nabe
            kn.Ring(new Vector3(0f, -0.04f, 0f), 5.1f * HallM - 0.2f, 5.1f * HallM + 0.2f, 32);
            // Wände: achteckiger Ring aus Platten, Decke mit Ring und Oktaeder-Kern
            for (var i = 0; i < 8; i++)
            {
                var a0 = Mathf.PI / 8f + Mathf.PI * 2f * i / 8f; var a1 = a0 + Mathf.PI / 4f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (GateD + 1f);
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (GateD + 1f);
                var mid = (p0 + p1) * 0.5f; var len = Vector3.Distance(p0, p1);
                var wall = new GameObject("Wand" + i).transform; wall.SetParent(transform, false);
                wall.position = mid; wall.rotation = Quaternion.LookRotation(-mid.normalized, Vector3.up);
                var wk = new MeshKit();
                wk.Box(new Vector3(0f, CeilH * 0.5f, 0.3f), new Vector3(len, CeilH, 0.6f));
                wk.Spawn("Platte", dark, wall, false);
                var ek = new MeshKit();
                ek.Box(new Vector3(0f, 4.2f * HallM, -0.01f), new Vector3(len, 0.08f, 0.02f));
                ek.Spawn("Leiste", neon, wall, false);
                kd.Box(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (GateD + 1f) + Vector3.up * CeilH * 0.5f, new Vector3(2f, CeilH, 2f));   // Ecksäulen
            }
            k.Prism(new Vector3(0f, CeilH, 0f), GateD + 3f, 0.6f, 8, Mathf.PI / 8f);                       // Decke
            kn.Ring(new Vector3(0f, CeilH - 1.6f * HallM, 0f), 6f * HallM, 7.2f * HallM, 8, Mathf.PI / 8f); // Lichtring
            kd.Prism(new Vector3(0f, CoreY - CoreR, 0f), CoreR, CoreR * 2f, 4, Mathf.PI / 4f);             // Oktaeder-Kern (vereinfacht)
            kn.Ring(new Vector3(0f, CoreY, 0f), CoreR * 0.9f, CoreR * 1.05f, 4, Mathf.PI / 4f);
            k.Spawn("Nabe", metal, transform, true);
            kd.Spawn("Halle", dark, transform, true);
            kn.Spawn("Leuchtlinien", neon, transform, false);

            // Tore: Norden I (−Z), Osten II (+X), Süden III (+Z), Westen IV (−X)
            var dirs = new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            for (var w = 0; w < 4; w++) _gates.Add(BuildGate(w, dirs[w]));

            // Strahl der freien Hand
            _rayIdle = NeonMaterials.Neon("ray_idle", new Color(0.82f, 0.82f, 0.85f));
            _rayHot = NeonMaterials.Neon("ray_hot", Color.white);
            var rg = new GameObject("Strahl"); rg.transform.SetParent(transform, false);
            _ray = rg.AddComponent<LineRenderer>();
            _ray.useWorldSpace = true; _ray.positionCount = 2; _ray.startWidth = 0.004f; _ray.endWidth = 0.002f;
            _ray.sharedMaterial = _rayIdle; _ray.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // HUD-Zeile vor dem Blick
            var hudGo = new GameObject("HalleHUD");
            _hud = MenuPanel.Text(hudGo.transform, "", Vector3.zero, 0.028f, new Color(0.92f, 0.98f, 1f));
            BuildConsoles();
            Say("SILENCE PROTOCOL: Halle der Tore. Zeige auf ein Tor und drücke den Abzug der freien Hand.", 8f);
        }

        Gate BuildGate(int world, Vector3 dir)
        {
            var theme = WorldTheme.Themes[world];
            var g = new Gate { world = world, dir = dir, center = dir * GateD };
            g.root = new GameObject("Tor_" + Numerals[world]);
            g.root.transform.SetParent(transform, false);
            g.root.transform.position = g.center;
            g.root.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);     // +Z zeigt zur Mitte
            var frame = new MeshKit(); var neon = new MeshKit();
            // Rahmen (zwei Stufen), Sturz, Gehäuse der Flügel, Schwelle
            frame.Box(new Vector3(-(OpenHW + FrameT), OpenH * 0.5f, 0.6f), new Vector3(FrameT * 2f, OpenH + FrameT * 2f, 1.2f));
            frame.Box(new Vector3(OpenHW + FrameT, OpenH * 0.5f, 0.6f), new Vector3(FrameT * 2f, OpenH + FrameT * 2f, 1.2f));
            frame.Box(new Vector3(0f, OpenH + FrameT, 0.6f), new Vector3(OpenHW * 2f + FrameT * 4f, FrameT * 2f, 1.2f));
            frame.Box(new Vector3(-(OpenHW + FrameT * 2f + Housing * 0.5f), OpenH * 0.5f, 0.2f), new Vector3(Housing, OpenH + FrameT * 2f + 0.3f, 1.6f));
            frame.Box(new Vector3(OpenHW + FrameT * 2f + Housing * 0.5f, OpenH * 0.5f, 0.2f), new Vector3(Housing, OpenH + FrameT * 2f + 0.3f, 1.6f));
            frame.Box(new Vector3(0f, -0.05f, 0.4f), new Vector3(OpenHW * 2f, 0.1f, 1.2f));
            neon.Box(new Vector3(-(OpenHW + 0.06f), OpenH * 0.5f, 1.21f), new Vector3(0.06f, OpenH, 0.02f));
            neon.Box(new Vector3(OpenHW + 0.06f, OpenH * 0.5f, 1.21f), new Vector3(0.06f, OpenH, 0.02f));
            neon.Box(new Vector3(0f, OpenH + 0.06f, 1.21f), new Vector3(OpenHW * 2f + 0.12f, 0.06f, 0.02f));
            frame.Spawn("Rahmen", NeonMaterials.Lit("hall_metal", WorldTheme.Themes[1].ground, 0.6f, 0.45f), g.root.transform, true);
            neon.Spawn("Leuchtrahmen", NeonMaterials.Neon("gate_neon_" + theme.id, theme.neon), g.root.transform, false);
            // Öffnung: Fenster mit Himmel der Welt (offen) oder Tür mit Schloss (zu)
            g.open = Progress.Unlocked(world * WorldTheme.LevelsPerWorld + 1);
            var win = GameObject.CreatePrimitive(PrimitiveType.Quad);
            win.name = g.open ? "Fenster" : "Tuer";
            win.transform.SetParent(g.root.transform, false);
            win.transform.localPosition = new Vector3(0f, OpenH * 0.5f, 0.0f);
            win.transform.localScale = new Vector3(OpenHW * 2f, OpenH, 1f);
            win.transform.localRotation = Quaternion.identity;
            win.layer = GameSession.MenuLayer;
            win.GetComponent<Collider>().isTrigger = true;
            if (g.open)
            {
                var sky = Resources.Load<Material>("Materials/Sky_" + theme.id);
                var m = new Material(NeonMaterials.Neon("window_" + theme.id, theme.sky));
                if (sky != null && sky.GetTexture("_MainTex") != null) { m.SetTexture("_BaseMap", sky.GetTexture("_MainTex")); m.SetColor("_BaseColor", Color.white); }
                win.GetComponent<Renderer>().sharedMaterial = m;
            }
            else
            {
                win.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Lit("gate_door", new Color(0.05f, 0.05f, 0.06f), 0.5f, 0.5f);
                var lock_ = new MeshKit();
                lock_.Box(new Vector3(0f, OpenH * 0.45f, -0.05f), new Vector3(0.6f, 0.5f, 0.1f));
                lock_.Ring(new Vector3(0f, OpenH * 0.45f + 0.45f, -0.05f), 0.18f, 0.28f, 12);
                lock_.Spawn("Schloss", NeonMaterials.Neon("lock_" + theme.id, theme.neon), g.root.transform, false);
            }
            g.col = win.GetComponent<Collider>();
            g.window = win.transform;
            // Balkon hinter dem Tor (nur offen): Plattform mit Brüstung und Leuchtlinie
            if (g.open)
            {
                var b = new MeshKit(); var bn = new MeshKit();
                b.Box(new Vector3(0f, -0.3f, -RoomD * 0.5f), new Vector3(OpenHW * 2f + 2f, 0.6f, RoomD));
                bn.Box(new Vector3(0f, 1.05f, -RoomD + 0.1f), new Vector3(OpenHW * 2f + 2f, 0.04f, 0.04f));
                bn.Box(new Vector3(-(OpenHW + 1f), 1.05f, -RoomD * 0.5f), new Vector3(0.04f, 0.04f, RoomD));
                bn.Box(new Vector3(OpenHW + 1f, 1.05f, -RoomD * 0.5f), new Vector3(0.04f, 0.04f, RoomD));
                b.Spawn("Balkon", NeonMaterials.Lit("hall_metal", WorldTheme.Themes[1].ground, 0.6f, 0.45f), g.root.transform, true);
                bn.Spawn("Handlauf", NeonMaterials.Neon("gate_neon_" + theme.id, theme.neon), g.root.transform, false);
            }
            // Überschrift und Stand
            var won = 0; var stars = 0;
            for (var n = world * WorldTheme.LevelsPerWorld + 1; n <= (world + 1) * WorldTheme.LevelsPerWorld; n++) { if (Progress.Won(n)) won++; stars += Progress.StarsOf(n); }
            g.title = MenuPanel.Text(g.root.transform, $"{Numerals[world]} · {theme.name}", new Vector3(0f, OpenH + FrameT * 2f + 1.6f, 1.3f), 0.5f, new Color(0.85f, 0.85f, 0.87f));
            var st = g.open ? $"★ {stars} / {WorldTheme.LevelsPerWorld * 3} · {won} / {WorldTheme.LevelsPerWorld} Missionen" : $"GESPERRT · Schließe Welt {Numerals[world - 1]} ab";
            g.status = MenuPanel.Text(g.root.transform, st, new Vector3(0f, OpenH + FrameT * 2f + 0.8f, 1.3f), 0.26f, new Color(0.56f, 0.56f, 0.56f));
            return g;
        }

        void BuildConsoles()
        {
            // Lauftafel links vorn, Turmwand rechts vorn (Diagonalen zwischen den Toren), fest in der Welt
            var l = (Vector3.back + Vector3.left).normalized * (PlatR + 1.2f);
            var r = (Vector3.back + Vector3.right).normalized * (PlatR + 1.2f);
            _consoleLeft = MenuPanel.Show(l + Vector3.up * 0.9f, "Lauftafel", new List<(string, System.Action, bool)>
            {
                ($"{Progress.Shards} {Progress.Currency} · {TotalStars()} Sterne", () => { }, true),
                ("Waffe: " + ArmName(), () => pauseMenu?.CycleWeapon(), true),
                ("Waffenhand: " + (XRInput.LeftHanded ? "links" : "rechts"), () => pauseMenu?.ToggleHand(), true),
            }, 1.8f, 1);
            _consoleLeft.faceHead = false; _consoleLeft.root.transform.rotation = Quaternion.LookRotation(l.normalized, Vector3.up);
            _consoleRight = MenuPanel.Show(r + Vector3.up * 0.9f, "Turmwand", new List<(string, System.Action, bool)>
            {
                ("Basis: Fertigkeiten", () => pauseMenu?.OpenBase(), true),
                ($"Nächste Mission: {Progress.NextOpen()}", () => StartMission(Progress.NextOpen()), true),
            }, 1.8f, 1);
            _consoleRight.faceHead = false; _consoleRight.root.transform.rotation = Quaternion.LookRotation(r.normalized, Vector3.up);
        }

        int TotalStars() { var s = 0; for (var n = 1; n <= 20; n++) s += Progress.StarsOf(n); return s; }
        string ArmName() => Balance.Arms.TryGetValue(Progress.Weapon, out var a) ? a.name : "Pistole";

        // ------------------------------------------------------------------------------------------------
        void Update()
        {
            if (rig == null) return;
            var head = rig.head.transform;
            _radioT -= Time.deltaTime;
            if (_radioT <= 0f) { _radioT = Random.Range(25f, 40f); Say("SILENCE PROTOCOL: " + Hall[Random.Range(0, Hall.Length)], 5f); }
            // Gleiten zum Balkon oder zurück
            if (_glide >= 0f)
            {
                _glide += Time.deltaTime / 1.1f;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_glide));
                rig.SetCenter(Vector3.Lerp(_glideFrom, _glideTo, k), true);
                if (_glide >= 1f) { _glide = -1f; Arrived(); }
            }
            // Strahl
            var hand = rig.OtherHand;
            var origin = hand.position; var dir = hand.forward; var end = origin + dir * 80f;
            MenuPanel.Button hover = null; Gate hitGate = null;
            if (Physics.Raycast(origin, dir, out var hit, 80f, 1 << GameSession.MenuLayer, QueryTriggerInteraction.Collide))
            {
                end = hit.point;
                hover = MenuPanel.Find(hit.collider.gameObject);
                foreach (var g in _gates) if (g.col == hit.collider) hitGate = g;
            }
            else if (Physics.Raycast(origin, dir, out var any, 80f, ~0, QueryTriggerInteraction.Ignore)) end = any.point;
            _ray.SetPosition(0, origin); _ray.SetPosition(1, end);
            _ray.sharedMaterial = hover != null || hitGate != null ? _rayHot : _rayIdle;
            foreach (var g in _gates) g.title.color = g == hitGate ? Color.white : new Color(0.85f, 0.85f, 0.87f);
            foreach (var m in MenuPanel.All) m.SetHover(hover != null && hover.panel == m ? hover : null);
            var trig = XRInput.OtherTrigger > 0.6f;
            if (trig && !_trigLatch && _glide < 0f)
            {
                if (hover != null) { if (hover.enabled) hover.action?.Invoke(); }
                else if (hitGate != null) GatePress(hitGate);
            }
            _trigLatch = trig;
            var b = XRInput.Back;
            if (b && !_bLatch && _onBalcony >= 0 && _glide < 0f) GatePress(_gates[_onBalcony]);
            _bLatch = b;
            _missions?.Face(head);
            // HUD
            var fwd = head.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            _hud.transform.position = Vector3.Lerp(_hud.transform.position, head.position + fwd * 1.6f + Vector3.up * 0.42f, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            _hud.transform.rotation = Quaternion.LookRotation(_hud.transform.position - head.position, Vector3.up);
            _hud.text = Time.time < messageUntil ? MenuPanel.Wrap(message, 70) : "";
        }

        void GatePress(Gate g)
        {
            if (_onBalcony == g.world)
            {
                // zurück zur Nabe
                _missions?.Close(); _missions = null;
                _glideFrom = g.center - g.dir * (RoomD * 0.5f); _glideTo = Vector3.zero; _glide = 0f; _onBalcony = -1;
                RenderSettings.skybox = Resources.Load<Material>("Materials/Sky_w2");
                Sfx("gate_open", 0.6f);
                return;
            }
            if (_onBalcony >= 0) return;
            if (!g.open)
            {
                Say($"Welt {Numerals[g.world]} ist verschlossen. Schließe Welt {Numerals[g.world - 1]} ab.", 3f);
                Sfx("ui_error", 0.5f);
                return;
            }
            _glideFrom = Vector3.zero; _glideTo = g.center - g.dir * (RoomD * 0.5f); _glide = 0f; _onBalcony = g.world;
            g.window.gameObject.GetComponent<Renderer>().enabled = false;      // durch das Tor zurück in die Nabe schauen
            Sfx("gate_open", 0.7f);
            Sfx("gate_hum_" + g.world, 0.5f);
        }

        void Arrived()
        {
            if (_onBalcony < 0) { foreach (var g in _gates) if (g.window != null) g.window.GetComponent<Renderer>().enabled = true; return; }
            var g = _gates[_onBalcony];
            var theme = WorldTheme.Themes[g.world];
            RenderSettings.skybox = Resources.Load<Material>("Materials/Sky_" + theme.id) ?? RenderSettings.skybox;
            ShowMissions(g);
        }

        void ShowMissions(Gate g)
        {
            _missions?.Close();
            var rows = new List<(string, System.Action, bool)>();
            var first = g.world * WorldTheme.LevelsPerWorld + 1;
            for (var n = first; n < first + WorldTheme.LevelsPerWorld; n++)
            {
                var m = n;
                var open = Progress.Unlocked(n);
                var stars = Progress.StarsOf(n);
                var md = GameData.Campaign?["missions"]?[n - 1];
                var name = (string)md?["name"] ?? "";
                var label = open ? $"{n}  {name}\n{new string('★', stars)}{new string('☆', 3 - stars)}" : $"{n}  {name}\ngesperrt";
                rows.Add((label, () => Confirm(m), open));
            }
            rows.Add(("Zurück zur Nabe", () => GatePress(g), true));
            var at = g.center - g.dir * (RoomD * 0.5f) - g.dir * 1.6f + Vector3.up * 0.6f;
            _missions = MenuPanel.Show(at, $"Welt {Numerals[g.world]} · {WorldTheme.Themes[g.world].name}\n{WorldTheme.Themes[g.world].mood}", rows, 2.2f, 1);
        }

        /// <summary>Startabfrage direkt in der Kachel (4.99.65): JA | NEIN.</summary>
        void Confirm(int mission)
        {
            var g = _gates[_onBalcony];
            _missions?.Close();
            var md = GameData.Campaign?["missions"]?[mission - 1];
            var rows = new List<(string, System.Action, bool)>
            {
                ("JA", () => StartMission(mission), true),
                ("NEIN", () => ShowMissions(g), true),
            };
            var at = g.center - g.dir * (RoomD * 0.5f) - g.dir * 1.6f + Vector3.up * 0.6f;
            _missions = MenuPanel.Show(at, MenuPanel.Wrap($"Mission {mission}: {(string)md?["name"]}\n{(string)md?["text"]}\nStarten?", 44), rows, 1.6f, 2);
        }

        public void StartMission(int mission)
        {
            if (!Progress.Unlocked(mission)) { Say("Diese Mission ist noch gesperrt.", 2f); return; }
            Sfx("open_confirm", 0.6f);
            LoadVeil.Load(mission, $"Mission {mission}");
        }
    }
}
