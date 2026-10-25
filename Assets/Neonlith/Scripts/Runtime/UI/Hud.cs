using Neonlith.Game;
using UnityEngine;

namespace Neonlith.UI
{
    /// <summary>
    /// Schlankes HUD vor dem Spieler (Gegenstück zu XRHud, 4.99.65: über der Blickmitte statt im Schussfeld): Welle,
    /// Kern, Munition, Funk. Folgt dem Kopf weich. Vorerst TextMesh mit der eingebauten Schrift; Oxanium und Tafeln
    /// kommen mit den Menüs (Etappe 5). Graustufen wie VRStyle.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public GameSession session;
        public PlayerWeapon weapon;
        public Transform head;
        TextMesh _text;
        TextMesh _msg;

        public static Hud Create(GameSession s, PlayerWeapon w, Transform head)
        {
            var go = new GameObject("HUD");
            var h = go.AddComponent<Hud>();
            h.session = s; h.weapon = w; h.head = head;
            h._text = Text(go.transform, "Zeile", 0.0f, 0.03f, new Color(0.9f, 0.9f, 0.92f));
            h._msg = Text(go.transform, "Funk", -0.09f, 0.022f, new Color(0.92f, 0.98f, 1f));
            return h;
        }

        static TextMesh Text(Transform parent, string name, float y, float size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var t = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            t.fontSize = 64;
            t.characterSize = size * 0.08f;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = col;
            return t;
        }

        void LateUpdate()
        {
            if (head == null) return;
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var target = head.position + fwd * 1.6f + Vector3.up * 0.42f;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);

            var s = session;
            var w = weapon;
            var waveTxt = s.phase == GameSession.Phase.Wave ? $"Welle {s.wave + 1}/{s.mission.waves.Count}  Gegner {s.enemies.Count}"
                : s.phase == GameSession.Phase.Build || s.phase == GameSession.Phase.Intro ? $"Nächste Welle in {Mathf.CeilToInt(s.countdown)} s"
                : s.phase == GameSession.Phase.Victory ? "SIEG" : "NIEDERLAGE";
            var ammoTxt = w == null ? "" : (w.reloading ? $"  Nachladen {Mathf.Clamp01(w.reloadT / w.arm.reload) * 100f:0}%" : $"  {w.arm.name} {w.ammo}/{w.arm.mag}");
            var bossTxt = s.boss != null && s.boss.alive ? $"   {s.boss.type.name} {Mathf.CeilToInt(100f * s.boss.hp / s.boss.maxHp)}%" : "";
            if (s.gunDestroyed) bossTxt += "   PLATTFORM ZERSTÖRT";
            else if (s.gunHp < s.gunMax) bossTxt += $"   Plattform {Mathf.CeilToInt(100f * s.gunHp / s.gunMax)}%";
            _text.text = $"{waveTxt}   Kern {s.lives}   Essenz {s.gold}{ammoTxt}{bossTxt}";
            var tut = s.tutorial != null && s.tutorial.visible ? "Gizmo: " + s.tutorial.text + (s.tutorial.label.Length > 0 ? $"  [{s.tutorial.label}]" : "") : "";
            _msg.text = MenuPanel.Wrap(tut.Length > 0 ? tut + (s.MessageNow.Length > 0 ? "\n" + s.MessageNow : "") : s.MessageNow, 70);
        }
    }
}
