using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.UI;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Bauen per Strahl (PlayerLaser + Baumenü + Turm-Tafel aus XRMain, auf das Nötige reduziert): Die freie Hand
    /// trägt einen dünnen Strahl. Auf einem Bauplatz (Holo-Ring) öffnet der Abzug dieser Hand das Baumenü mit den
    /// Türmen der Mission (und Sperren auf Sperrplätzen), auf einem Turm die Turm-Tafel (Stufe, Ausbau, Verkauf,
    /// Zielwahl). Knöpfe sind Flächen in der Welt mit Schrift; Zeigen hebt hervor, Abzug wählt, B schließt.
    /// A zeigt alle Bauplätze (Bau-Modus), sonst nur die in der Nähe des Strahls.
    /// </summary>
    public sealed class BuildController : MonoBehaviour
    {
        public GameSession session;
        public MissionData mission;
        public Transform hand, head;
        LineRenderer _ray;
        readonly List<GameObject> _padMarks = new List<GameObject>();
        readonly Dictionary<Collider, BuildPad> _padBy = new Dictionary<Collider, BuildPad>();
        MenuPanel _menu;
        BuildPad _menuPad;
        Tower _menuTower, _shownRange;
        bool _trigLatch, _bLatch, _aLatch, _buildMode;
        MenuPanel.Button _hover;
        Material _rayIdle, _rayHot;

        public static BuildController Create(GameSession s, MissionData m, Transform hand, Transform head, WorldTheme theme)
        {
            var go = new GameObject("Bauen");
            var b = go.AddComponent<BuildController>();
            b.session = s; b.mission = m; b.hand = hand; b.head = head;
            b._rayIdle = NeonMaterials.Neon("ray_idle", new Color(0.82f, 0.82f, 0.85f));   // XRMain.RAY_IDLE (weiß wie die Menüs)
            b._rayHot = NeonMaterials.Neon("ray_hot", Color.white);
            var rg = new GameObject("Strahl");
            rg.transform.SetParent(go.transform, false);
            b._ray = rg.AddComponent<LineRenderer>();
            b._ray.useWorldSpace = true; b._ray.positionCount = 2;
            b._ray.startWidth = 0.004f; b._ray.endWidth = 0.002f;
            b._ray.sharedMaterial = b._rayIdle;
            b._ray.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.BuildPadMarks(theme);
            return b;
        }

        /// <summary>Holo-Ring je Bauplatz (HoloMark in Godot), Sperrplätze als Querbalken. Mit Kollision für den Strahl.</summary>
        void BuildPadMarks(WorldTheme theme)
        {
            var padMat = NeonMaterials.Neon("pad", theme.pad);
            var trapMat = NeonMaterials.Neon("trap", WorldTheme.Threat);
            foreach (var p in mission.pads)
            {
                var k = new MeshKit();
                if (p.trap) k.Box(Vector3.up * 0.02f, new Vector3(MissionData.RoadWidth * Balance.FieldM - 0.6f, 0.02f, 0.25f));
                else k.Ring(Vector3.up * 0.012f, 0.42f * Balance.FieldM, 0.5f * Balance.FieldM, 8, Mathf.PI / 8f);
                var go = k.Spawn(p.trap ? "Sperrplatz" : "Bauplatz", p.trap ? trapMat : padMat, transform, false);
                go.transform.position = p.position;
                go.transform.rotation = Quaternion.Euler(0f, p.yawDeg, 0f);
                go.layer = GameSession.PadLayer;
                var col = go.AddComponent<BoxCollider>();
                col.center = Vector3.up * 0.3f;
                col.size = p.trap ? new Vector3(MissionData.RoadWidth * Balance.FieldM - 0.6f, 0.6f, 0.8f) : new Vector3(1.1f * Balance.FieldM, 0.6f, 1.1f * Balance.FieldM);
                col.isTrigger = true;                       // Schüsse der Waffe gehen hindurch
                _padBy[col] = p;
                _padMarks.Add(go);
            }
        }

        void Update()
        {
            if (hand == null) return;
            var a = XRInput.Confirm;
            if (a && !_aLatch && _menu == null) _buildMode = !_buildMode;
            _aLatch = a;
            var bBtn = XRInput.Back;
            if (bBtn && !_bLatch) CloseMenu();
            _bLatch = bBtn;

            var origin = hand.position;
            var dir = hand.forward;
            var end = origin + dir * 60f;
            _hover = null;
            BuildPad hitPad = null; Tower hitTower = null;
            var mask = (1 << GameSession.PadLayer) | (1 << GameSession.TowerLayer) | (1 << GameSession.MenuLayer);
            if (Physics.Raycast(origin, dir, out var hit, 60f, mask, QueryTriggerInteraction.Collide))
            {
                end = hit.point;
                if (hit.collider.gameObject.layer == GameSession.MenuLayer) _hover = MenuPanel.Find(hit.collider.gameObject);
                else if (_padBy.TryGetValue(hit.collider, out var p)) hitPad = p.tower != null ? null : p;
                if (hit.collider.gameObject.layer == GameSession.TowerLayer) hitTower = hit.collider.GetComponentInParent<Tower>();
            }
            else if (Physics.Raycast(origin, dir, out var any, 60f, ~(1 << GameSession.EnemyLayer), QueryTriggerInteraction.Ignore)) end = any.point;
            _ray.SetPosition(0, origin); _ray.SetPosition(1, end);
            _ray.sharedMaterial = (_hover != null || hitPad != null || hitTower != null) ? _rayHot : _rayIdle;

            // Bauplätze zeigen: im Bau-Modus alle, sonst nur nahe am Strahl
            foreach (var kv in _padBy)
            {
                var show = _buildMode || _menu != null || kv.Value.tower == null && Vector3.Distance(kv.Key.transform.position, end) < 6f;
                if (kv.Value.tower != null) show = false;
                kv.Key.gameObject.SetActive(show);
            }
            // Reichweite des gezeigten Turms
            var showRange = hitTower ?? _menuTower;
            if (showRange != _shownRange) { _shownRange?.ShowRange(false); _shownRange = showRange; _shownRange?.ShowRange(true); }
            foreach (var m in MenuPanel.All) m.SetHover(_hover != null && _hover.panel == m ? _hover : null);

            var trig = XRInput.OtherTrigger > 0.6f;
            if (trig && !_trigLatch)
            {
                if (_hover != null) { if (_hover.enabled) _hover.action?.Invoke(); }
                else if (session.paused || session.pauseMenuOpen) { }
                else if (hitPad != null) OpenBuildMenu(hitPad);
                else if (hitTower != null) OpenTowerMenu(hitTower);
                else if (_menu != null) CloseMenu();
            }
            _trigLatch = trig;
            _menu?.Face(head);
        }

        void OpenBuildMenu(BuildPad pad)
        {
            CloseMenu();
            _menuPad = pad;
            var kinds = new List<string>(pad.trap ? mission.traps : mission.towers);
            if (!pad.trap && !kinds.Contains("teahouse")) kinds.Add("teahouse");
            if (pad.trap && kinds.Count == 0) kinds.AddRange(Balance.TrapOrder);
            var title = pad.trap ? "Sperre bauen" : "Turm bauen";
            var rows = new List<(string, System.Action, bool)>();
            foreach (var k in kinds)
            {
                var st = Balance.Structure(k);
                if (st == null) continue;
                var cost = Balance.BuildCost(k);
                var kind = k;
                rows.Add(($"{st.name}   {cost}", () => { if (session.Build(kind, pad) != null) CloseMenu(); }, session.gold >= cost));
            }
            _menu = MenuPanel.Show(pad.position + Vector3.up * 1.2f, title, rows);
        }

        void OpenTowerMenu(Tower t)
        {
            CloseMenu();
            _menuTower = t;
            var up = Balance.UpgradeCost(t.Kind, t.level);
            var rows = new List<(string, System.Action, bool)>();
            var txt = t.level + 1 < t.type.upgradeTexts.Count ? t.type.upgradeTexts[t.level + 1] : "";
            rows.Add((up >= 0 ? $"Ausbauen   {up}" : "Höchste Stufe", () => { if (session.Upgrade(t)) OpenTowerMenu(t); }, up >= 0 && session.gold >= up));
            rows.Add(($"Verkaufen   +{session.SellValue(t)}", () => { session.Sell(t); CloseMenu(); }, true));
            if (t.IsDefense) rows.Add(($"Ziel: {t.TargetModeName}", () => { t.CycleTargetMode(); OpenTowerMenu(t); }, true));
            var head = $"{t.type.name}  Stufe {t.level + 1}" + (txt.Length > 0 ? "\n" + txt : "");
            _menu = MenuPanel.Show(t.Position + Vector3.up * 2.4f, head, rows);
        }

        void CloseMenu()
        {
            _menu?.Close();
            _menu = null; _menuPad = null; _menuTower = null; _hover = null;
        }
    }
}
