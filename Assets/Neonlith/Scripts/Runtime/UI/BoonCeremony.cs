using System.Collections.Generic;
using Neonlith.Game;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.UI
{
    /// <summary>
    /// Segenskarten-Zeremonie (scripts/xr/boon_ceremony.gd, Variante B, auf das Nötige reduziert): die drei Karten
    /// erscheinen als Tafeln im Halbkreis vor der Brust (0,85 m, 28° Abstand, 30 cm unter den Augen), Seltenheit
    /// als Randfarbe. Der Strahl der freien Hand hebt hervor, der Abzug wählt; nicht gewählte Karten verschwinden.
    /// Die Bauzeit steht, solange die Wahl offen ist.
    /// </summary>
    public sealed class BoonCeremony : MonoBehaviour
    {
        public GameSession session;
        public Transform head;
        readonly List<MenuPanel> _cards = new List<MenuPanel>();
        public bool Open => _cards.Count > 0;

        public static BoonCeremony Create(GameSession s, Transform head)
        {
            var go = new GameObject("Segenskarten");
            var c = go.AddComponent<BoonCeremony>();
            c.session = s; c.head = head;
            s.OnBoonOffer += c.Show;
            return c;
        }

        void Show(List<string> ids)
        {
            Close();
            var fwd = head.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            var n = ids.Count;
            for (var i = 0; i < n; i++)
            {
                var id = ids[i];
                var card = Boons.Cards[id];
                var ang = (i - (n - 1) * 0.5f) * 28f;
                var dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                var at = head.position + dir * 0.85f - Vector3.up * 0.45f;
                var rows = new List<(string, System.Action, bool)> { ($"{card.name}\n{Boons.RarityNames[card.rarity]}", () => Choose(id), true) };
                var p = MenuPanel.Show(at, MenuPanel.Wrap(card.text, 22), rows, 0.5f, 1);
                var col = Boons.RarityColor[card.rarity];
                foreach (Transform t in p.root.transform) if (t.name == "Rand") t.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Neon("rarity_" + card.rarity, col);
                _cards.Add(p);
            }
            session.offerOpen = true;
            session.Sfx("boon_appear", 0.8f);
        }

        void Choose(string id)
        {
            session.TakeBoon(id);
            session.Sfx("boon_select", 0.9f);
            XR.XRInput.Haptic(0.6f, 0.1f, false);
            Close();
        }

        void Close()
        {
            foreach (var c in _cards) c.Close();
            _cards.Clear();
            session.offerOpen = false;
        }

        void Update() { foreach (var c in _cards) c.Face(head); }
    }
}
