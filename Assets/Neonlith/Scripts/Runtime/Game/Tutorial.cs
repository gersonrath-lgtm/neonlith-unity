using System.Collections.Generic;
using Neonlith.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Geführte erste Welle (scripts/ui/tutorial.gd, Texte 1:1 mit Gizmo als Sprecherin): jeder Schritt wartet auf
    /// eine Spielaktion (Ereignisse der GameSession), Schritte mit Zeitgrenze gehen auch ohne weiter. Läuft nur, bis
    /// er einmal beendet wurde (Progress "tutorial_done"). Der Text erscheint im HUD, die Taste als Beschriftung.
    /// </summary>
    public sealed class Tutorial
    {
        sealed class Step { public string text, wait, label; public float timeout; public bool buildMode; }

        static readonly Step[] Steps =
        {
            new Step { text = "Hallo Rook, ich bin Gizmo, deine Begleitdrohne. Willkommen auf {0}! Gegner laufen die Straße entlang zu deiner Basis. Die leuchtenden Felder sind Bauplätze: Zeige mit der FREIEN Hand auf eines und drücke den Abzug.", wait = "placing:", label = "Abzug: Feld wählen", buildMode = true },
            new Step { text = "Im Baumenü: auf die Gatling zeigen und Abzug drücken. Der Ring zeigt die Reichweite. B geht zurück.", wait = "built:", label = "Abzug: Bauen" },
            new Step { text = "Gut! Baue gern noch einen Turm. Starte dann die erste Welle mit X an der linken Hand.", wait = "wave_started", label = "X: Welle starten" },
            new Step { text = "Dein Gewehr: Ziel über Kimme und Korn auf einen Gegner und drück den Abzug der Waffenhand. Ist das Magazin leer, halte die Waffe kurz nach unten, dann lädt sie nach.", wait = "ship_hit", label = "Abzug: Feuer", timeout = 40f },
            new Step { text = "Besiegte Gegner hinterlassen Essenz. Sie fließt von selbst zu dir: Essenz ist dein Baumaterial.", wait = "essence", label = "", timeout = 45f },
            new Step { text = "Zeige mit der freien Hand auf deinen Turm und drücke den Abzug: Dort verbesserst oder verkaufst du ihn.", wait = "selected", label = "Abzug: Turm wählen", timeout = 45f },
            new Step { text = "Leben, Essenz und Welle stehen über deinem Blick. Die Menütaste pausiert. Viel Erfolg!", wait = "", label = "Menü: Pause" },
        };

        public bool visible;
        public int step = -1;
        public string text = "", label = "";
        public bool wantBuildMode;
        float _t;
        readonly string _world;
        public System.Action finished;

        public Tutorial(string worldName) { _world = worldName; }

        public static bool Done => Progress.Data["tutorial_done"]?.Value<bool>() ?? false;

        public void Start() { visible = true; step = -1; Next(); }

        void Next()
        {
            step++;
            _t = 0f;
            if (step >= Steps.Length) { Finish(); return; }
            var s = Steps[step];
            text = step == 0 ? string.Format(s.text, _world) : s.text;
            label = s.label;
            wantBuildMode = s.buildMode;
            if (s.wait.Length == 0) _t = -12f;    // letzter Hinweis: 12 s stehen lassen
        }

        public void Update(float dt)
        {
            if (!visible || step < 0 || step >= Steps.Length) return;
            var s = Steps[step];
            if (s.wait.Length == 0) { _t += dt; if (_t >= 0f) Finish(); return; }
            if (s.timeout > 0f) { _t += dt; if (_t >= s.timeout) Next(); }
        }

        public void OnEvent(string ev)
        {
            if (!visible || step < 0 || step >= Steps.Length) return;
            var w = Steps[step].wait;
            if (w.Length > 0 && ev.StartsWith(w)) Next();
            else if (step < 3 && ev == "wave_started") { step = 2; Next(); }
        }

        public void Finish()
        {
            if (!visible) return;
            visible = false; text = ""; label = ""; wantBuildMode = false;
            Progress.Data["tutorial_done"] = true;
            Progress.Save();
            finished?.Invoke();
        }
    }
}
