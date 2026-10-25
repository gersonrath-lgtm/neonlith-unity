using System.Collections.Generic;
using Neonlith.Game;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.UI
{
    /// <summary>
    /// Tafel in der Welt mit Knöpfen (VRPanel + VRButton aus scripts/ui/vr, in Graustufen nach VRStyle): dunkles
    /// Glas, heller Rand, Knöpfe als Flächen mit Schrift. Der Strahl der freien Hand (BuildController) hebt hervor
    /// und wählt. Alle offenen Tafeln stehen in <see cref="All"/>, damit ein Strahl sie findet.
    /// </summary>
    public sealed class MenuPanel
    {
        public sealed class Button
        {
            public GameObject go; public Renderer bg; public TextMesh label; public System.Action action; public bool enabled = true; public MenuPanel panel;
        }

        public static readonly List<MenuPanel> All = new List<MenuPanel>();
        public static readonly Color Panel = new Color(0.086f, 0.086f, 0.086f);   // VRStyle.PANEL
        public static readonly Color Edge = new Color(0.56f, 0.56f, 0.56f);       // VRStyle.EDGE
        public static readonly Color Cyan = new Color(0.92f, 0.98f, 1f);         // VRStyle.CYAN (Fokus)

        public GameObject root;
        public readonly List<Button> buttons = new List<Button>();
        public bool faceHead = true;

        public static MenuPanel Show(Vector3 at, string title, List<(string label, System.Action action, bool enabled)> rows, float width = 1.6f, int columns = 1)
        {
            var m = new MenuPanel();
            m.root = new GameObject("Tafel");
            m.root.transform.position = at;
            var rowCount = Mathf.CeilToInt(rows.Count / (float)columns);
            var titleLines = title.Split('\n').Length;
            var titleExtra = 0.07f * (titleLines - 1);
            var h = 0.16f * rowCount + 0.3f + titleExtra;
            var w = width;
            var k = new MeshKit();
            k.Box(new Vector3(0f, h * 0.5f, 0.01f), new Vector3(w, h, 0.01f));
            k.Spawn("Glas", NeonMaterials.Neon("panel", Panel), m.root.transform, false);
            var frame = new MeshKit();
            frame.Box(new Vector3(0f, h * 0.5f, 0.015f), new Vector3(w + 0.02f, h + 0.02f, 0.005f));
            frame.Spawn("Rand", NeonMaterials.Neon("edge", Edge), m.root.transform, false);
            Text(m.root.transform, title, new Vector3(0f, h - 0.1f - titleExtra * 0.5f, -0.005f), 0.05f, Color.white);
            var colW = (w - 0.12f) / columns;
            for (var i = 0; i < rows.Count; i++)
            {
                var (label, action, enabled) = rows[i];
                var r = i / columns; var c = i % columns;
                var y = h - 0.26f - titleExtra - 0.16f * r;
                var x = -w * 0.5f + 0.06f + colW * (c + 0.5f);
                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.name = "Knopf";
                bg.transform.SetParent(m.root.transform, false);
                bg.transform.localPosition = new Vector3(x, y, -0.004f);
                bg.transform.localScale = new Vector3(colW - 0.04f, 0.13f, 1f);
                bg.layer = GameSession.MenuLayer;
                bg.GetComponent<Collider>().isTrigger = true;
                var b = new Button { go = bg, bg = bg.GetComponent<Renderer>(), action = action, enabled = enabled, panel = m };
                b.label = Text(m.root.transform, label, new Vector3(x, y, -0.012f), 0.042f, Color.white);
                m.buttons.Add(b);
            }
            m.SetHover(null);
            All.Add(m);
            return m;
        }

        /// <summary>Zeilenumbruch für TextMesh (kein eigener Umbruch): nach etwa so vielen Zeichen am Wortende.</summary>
        public static string Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var outLines = new List<string>();
            foreach (var para in text.Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    if (line.Length > 0 && line.Length + 1 + word.Length > width) { outLines.Add(line); line = word; }
                    else line = line.Length == 0 ? word : line + " " + word;
                }
                outLines.Add(line);
            }
            return string.Join("\n", outLines);
        }

        public static TextMesh Text(Transform parent, string s, Vector3 local, float size, Color col)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var t = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            t.fontSize = 64; t.characterSize = size * 0.08f;
            t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            t.text = s; t.color = col;
            return t;
        }

        public static Button Find(GameObject go)
        {
            foreach (var m in All) foreach (var b in m.buttons) if (b.go == go) return b;
            return null;
        }

        public void SetHover(Button hover)
        {
            foreach (var b in buttons)
            {
                var hot = b == hover;
                b.bg.sharedMaterial = NeonMaterials.Neon(hot ? "btn_hot" : (b.enabled ? "btn" : "btn_off"), hot ? Cyan : (b.enabled ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.06f, 0.06f, 0.06f)));
                b.label.color = hot ? Color.black : (b.enabled ? new Color(0.9f, 0.9f, 0.92f) : new Color(0.45f, 0.45f, 0.47f));
            }
        }

        public void Face(Transform head)
        {
            if (!faceHead || root == null || head == null) return;
            var look = root.transform.position - head.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        public void Close()
        {
            All.Remove(this);
            if (root != null) Object.Destroy(root);
            root = null; buttons.Clear();
        }
    }
}
