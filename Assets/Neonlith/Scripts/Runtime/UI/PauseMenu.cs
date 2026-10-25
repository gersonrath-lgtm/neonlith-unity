using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.Game;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith.UI
{
    /// <summary>
    /// Pausenmenü und Missionswahl (VRMenus, VRCampaignMap, Arsenal, Einstellungen auf das Nötige reduziert):
    /// Menü-Taste pausiert und öffnet eine Tafel vor dem Spieler: Weiter, Mission neu starten, Missionen (1 bis 20
    /// mit Freischaltung und Sternen), Waffe (Pistole, Sturmgewehr ab Welt I, Sniper ab Welt II), Waffenhand.
    /// Nach Sieg oder Niederlage öffnet sich die Tafel von selbst (nächste Mission, noch einmal, Missionen).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public GameSession session;
        public Transform head;
        MenuPanel _panel;
        bool _menuLatch;
        GameSession.Phase _seenPhase;

        public static PauseMenu Create(GameSession s, Transform head)
        {
            var go = new GameObject("Pausenmenue");
            var p = go.AddComponent<PauseMenu>();
            p.session = s; p.head = head; p._seenPhase = s != null ? s.phase : GameSession.Phase.Intro;
            return p;
        }

        bool Ended => session != null && (session.phase == GameSession.Phase.Victory || session.phase == GameSession.Phase.Defeat);
        void Notify(string text, float secs) { if (session != null) session.Say(text, secs); else HubSession.Instance?.Say(text, secs); }
        void Sound(string name, float vol) { if (session != null) session.Sfx(name, vol); else HubSession.Instance?.Sfx(name, vol); }

        void Update()
        {
            var mnu = XRInput.Pause;
            if (mnu && !_menuLatch)
            {
                if (_panel != null) Resume();
                else if (!Ended) OpenPause();
            }
            _menuLatch = mnu;
            if (session != null && session.phase != _seenPhase)
            {
                _seenPhase = session.phase;
                if (Ended) Invoke(nameof(OpenEnd), 2.5f);
            }
            _panel?.Face(head);
        }

        Vector3 Spot() { var f = head.forward; f.y = 0f; f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward; return head.position + f * 1.3f - Vector3.up * 0.55f; }

        void OpenPause()
        {
            if (session != null) { session.paused = true; session.pauseMenuOpen = true; }
            var rows = new List<(string, System.Action, bool)> { ("Weiter", Resume, true) };
            if (session != null)
            {
                rows.Add(("Mission neu starten", () => Load(session.mission.number), true));
                rows.Add(("Zur Halle der Tore", () => Load(0), true));
            }
            rows.AddRange(new List<(string, System.Action, bool)>
            {
                ("Missionen", OpenMissions, true),
                ($"Basis: Fertigkeiten ({Progress.Shards} {Progress.Currency})", OpenBase, true),
                ("Waffe: " + CurrentArmName(), CycleWeapon, true),
                ("Waffenhand: " + (XRInput.LeftHanded ? "links" : "rechts"), ToggleHand, true),
                ("Einstellungen", OpenSettings, true),
            });
            Show(session != null ? "Pause" : "Halle der Tore", rows);
        }

        /// <summary>Einstellungen (Settings in Godot, das Nötige): Musik, Effekte, Drehen, Einführung.</summary>
        void OpenSettings()
        {
            Close();
            _panel = MenuPanel.Show(Spot(), "Einstellungen", new List<(string, System.Action, bool)>
            {
                ($"Musik: {Mathf.RoundToInt(Progress.MusicVolume * 100f)} %", () => { Progress.MusicVolume = (Progress.MusicVolume + 0.1f) > 1.01f ? 0f : Progress.MusicVolume + 0.1f; session?.ApplyVolumes(); HubSession.Instance?.ApplyVolumes(); OpenSettings(); }, true),
                ($"Effekte: {Mathf.RoundToInt(Progress.SfxVolume * 100f)} %", () => { Progress.SfxVolume = (Progress.SfxVolume + 0.1f) > 1.01f ? 0f : Progress.SfxVolume + 0.1f; Sound("ui_click", 0.6f); OpenSettings(); }, true),
                ("Drehen: " + (Progress.SnapTurn ? "Stufen von 30°" : "stufenlos"), () => { Progress.SnapTurn = !Progress.SnapTurn; OpenSettings(); }, true),
                ("Einführung beim nächsten Start von Mission 1 wiederholen", () => { Progress.Data["tutorial_done"] = false; Progress.Save(); Notify("Einführung wird wiederholt", 2f); OpenSettings(); }, true),
                ("Zurück", Reopen, true),
            }, 2.0f, 1);
        }

        /// <summary>Basis (VRSkillWall): Bäume wählen, Knoten kaufen, Pfad zurücksetzen.</summary>
        public void OpenBase()
        {
            Close();
            var rows = new List<(string, System.Action, bool)>();
            foreach (var id in SkillTrees.Order)
            {
                var t = SkillTrees.Trees[id];
                var tid = id;
                var open = id == "core" || id == "ship" || Progress.Won(1);
                rows.Add(($"{t.title}  ({Progress.SpentInTree(id, false)} Punkte)", () => OpenTree(tid), open));
            }
            rows.Add(("Zurück", Reopen, true));
            _panel = MenuPanel.Show(Spot(), $"Basis: {Progress.Shards} {Progress.Currency}\nStamm, dann ein Pfad A oder B", rows, 2.0f, 2);
        }

        void OpenTree(string tree)
        {
            Close();
            var t = SkillTrees.Trees[tree];
            var rows = new List<(string, System.Action, bool)>();
            foreach (var n in t.nodes)
            {
                var nd = n;
                var r = Progress.Rank(tree, n.id);
                var cost = Progress.NextCost(tree, n.id);
                var block = Progress.BuyBlock(tree, n.id);
                var slot = n.slot == "trunk" ? "Stamm" : (n.slot == "A" ? "A " + t.pathAName : "B " + t.pathBName);
                var label = $"[{slot}] {n.name} {r}/{n.Ranks}" + (cost > 0 ? $"  {cost}" : "") + "\n" + MenuPanel.Wrap(n.text, 48);
                rows.Add((label, () =>
                {
                    var why = Progress.BuyBlock(tree, nd.id);
                    if (why.Length > 0) Notify(why, 3f);
                    else { Progress.Buy(tree, nd.id); Sound("upgrade", 0.6f); Notify($"{nd.name} gelernt. Wirkt ab der nächsten Mission.", 3f); }
                    OpenTree(tree);
                }, block.Length == 0));
            }
            if (Progress.ChosenPath(tree).Length > 0)
            {
                var fee = Progress.RespecFee(tree);
                rows.Add(($"Pfad zurücksetzen (Gebühr {fee})", () => { var back = Progress.Respec(tree); Notify($"Pfad zurückgesetzt, {back} {Progress.Currency} erstattet", 3f); OpenTree(tree); }, true));
            }
            rows.Add(("Zurück", OpenBase, true));
            _panel = MenuPanel.Show(Spot(), $"{t.building}: {t.title}  ·  {Progress.Shards} {Progress.Currency}" + (t.text.Length > 0 ? "\n" + t.text : ""), rows, 2.4f, 2);
        }

        void OpenEnd()
        {
            if (_panel != null) return;
            var won = session.phase == GameSession.Phase.Victory;
            var n = session.mission.number;
            var rows = new List<(string, System.Action, bool)>();
            if (won && n < 20) rows.Add(($"Weiter zu Mission {n + 1}", () => Load(n + 1), true));
            rows.Add((won ? "Noch einmal" : "Noch einmal versuchen", () => Load(n), true));
            rows.Add(("Zur Halle der Tore", () => Load(0), true));
            rows.Add(("Missionen", OpenMissions, true));
            rows.Add(("Waffe: " + CurrentArmName(), CycleWeapon, true));
            Show(won ? $"Mission {n} geschafft: {new string('★', Progress.StarsOf(n))}" : "Der Kern ist gefallen", rows);
        }

        void OpenMissions()
        {
            Close();
            var rows = new List<(string, System.Action, bool)>();
            for (var n = 1; n <= 20; n++)
            {
                var m = n;
                var open = Progress.Unlocked(n);
                var stars = Progress.StarsOf(n);
                var label = open ? $"{n}  {new string('★', stars)}{new string('☆', 3 - stars)}" : $"{n}  gesperrt";
                rows.Add((label, () => Load(m), open));
            }
            rows.Add(("Zurück", () => { Close(); if (Ended) OpenEnd(); else OpenPause(); }, true));
            _panel = MenuPanel.Show(Spot(), "Missionen: vier Welten, je fünf Level", rows, 2.0f, 4);
        }

        string CurrentArmName()
        {
            var id = Progress.Weapon;
            return Balance.Arms.TryGetValue(id, out var a) ? a.name : "Pistole";
        }

        public void CycleWeapon()
        {
            var order = new[] { "pistol", "rifle", "sniper" };
            var i = System.Array.IndexOf(order, Progress.Weapon);
            for (var k = 1; k <= order.Length; k++)
            {
                var id = order[(i + k) % order.Length];
                if (Balance.Arms[id].unlockWorld <= Progress.WorldsDone()) { Progress.Weapon = id; break; }
            }
            var locked = Balance.Arms.TryGetValue(order[(i + 1) % order.Length], out var next) && next.unlockWorld > Progress.WorldsDone();
            Notify(locked ? $"{next.name} wird nach Welt {next.unlockWorld} frei" : $"Waffe: {CurrentArmName()} (gilt ab der nächsten Mission)", 3f);
            if (_panel != null) Reopen();
        }

        public void ToggleHand()
        {
            Progress.LeftHanded = !Progress.LeftHanded;
            Notify("Waffenhand gilt ab der nächsten Mission", 3f);
            if (_panel != null) Reopen();
        }

        void Reopen() { var end = Ended; Close(); if (end) OpenEnd(); else OpenPause(); }

        void Show(string title, List<(string, System.Action, bool)> rows) { Close(); _panel = MenuPanel.Show(Spot(), title, rows, 1.6f, 1); }

        void Resume() { Close(); if (session != null) { session.paused = false; session.pauseMenuOpen = false; } }

        void Close() { _panel?.Close(); _panel = null; }

        void Load(int mission)
        {
            Close();
            XR.LoadVeil.Load(mission, mission > 0 ? $"Mission {mission}" : "Halle der Tore");
        }
    }
}
