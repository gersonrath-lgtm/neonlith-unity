using System.Collections.Generic;
using Neonlith.UI;
using UnityEngine;

namespace Neonlith.View
{
    /// <summary>
    /// Schadenszahlen am Gegner (4.59 SP2, scripts/view3d/damage_numbers.gd): steigen auf und verblassen, kritische
    /// Treffer größer und länger, Brand in Orange, Spielerwaffe in Weiß. Begrenzter Pool (Quest), TextMesh.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        const int Max = 40;
        public static DamageNumbers Instance { get; private set; }
        sealed class Num { public TextMesh text; public float t, life; public Vector3 pos; public bool big; }
        readonly List<Num> _live = new List<Num>();
        readonly Stack<TextMesh> _pool = new Stack<TextMesh>();
        Transform _head;

        public static DamageNumbers Create(Transform head)
        {
            var go = new GameObject("Schadenszahlen");
            var d = go.AddComponent<DamageNumbers>();
            d._head = head;
            Instance = d;
            return d;
        }

        public static void Show(Vector3 at, float dmg, string source, bool crit)
        {
            if (Instance == null || dmg < 0.5f) return;
            Instance.Spawn(at, dmg, source, crit);
        }

        void Spawn(Vector3 at, float dmg, string source, bool crit)
        {
            if (_live.Count >= Max) { Recycle(_live[0]); _live.RemoveAt(0); }
            var t = _pool.Count > 0 ? _pool.Pop() : MenuPanel.Text(transform, "", Vector3.zero, 0.04f, Color.white);
            t.gameObject.SetActive(true);
            t.text = Mathf.RoundToInt(dmg).ToString() + (crit ? "!" : "");
            var col = source == "dot" ? new Color(1f, 0.6f, 0.2f) : source == "kanzel" ? Color.white : source == "ice" ? new Color(0.6f, 0.85f, 1f) : new Color(0.95f, 0.95f, 0.97f);
            t.color = col;
            t.characterSize = (crit ? 0.07f : 0.04f) * 0.08f;
            var n = new Num { text = t, t = 0f, life = crit ? 1.3f : 0.8f, pos = at + new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, Random.Range(-0.2f, 0.2f)), big = crit };
            _live.Add(n);
        }

        void Recycle(Num n) { n.text.gameObject.SetActive(false); _pool.Push(n.text); }

        void Update()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var n = _live[i];
                n.t += Time.deltaTime;
                var k = n.t / n.life;
                if (k >= 1f) { Recycle(n); _live.RemoveAt(i); continue; }
                var p = n.pos + Vector3.up * (0.9f * k);
                n.text.transform.position = p;
                if (_head != null) n.text.transform.rotation = Quaternion.LookRotation(p - _head.position, Vector3.up);
                var c = n.text.color; c.a = 1f - k * k; n.text.color = c;
                // Größe mit dem Abstand, damit die Zahl in der Ferne lesbar bleibt (höchstens 3 m Entfernung wie nah)
                var d = _head != null ? Mathf.Max(3f, Vector3.Distance(p, _head.position)) : 3f;
                n.text.characterSize = (n.big ? 0.07f : 0.04f) * 0.08f * (d / 3f);
            }
        }
    }
}
