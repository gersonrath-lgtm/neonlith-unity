using System.Collections;
using Neonlith.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neonlith.XR
{
    /// <summary>
    /// Ladeschleier (XRLoadVeil, 4.99.65, ohne OpenXR-Ebene): schwarze Kugel um den Kopf, die beim Start aufblendet
    /// und vor jedem Szenenwechsel abblendet, dazu die Zeile „NEOWARD“ mit Ziel. Kein Schnitt, kein hängendes Bild.
    /// </summary>
    public sealed class LoadVeil : MonoBehaviour
    {
        public static LoadVeil Instance { get; private set; }
        Renderer _r;
        MaterialPropertyBlock _mpb;
        TextMesh _text;
        float _alpha = 1f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static LoadVeil Create(Transform head)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Ladeschleier");
            go.transform.SetParent(head, false);
            var v = go.AddComponent<LoadVeil>();
            Instance = v;
            // Kugel um den Kopf, von innen sichtbar (Cull Off), vor allem anderen gezeichnet
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.6f;
            v._r = sphere.GetComponent<Renderer>();
            var mat = new Material(NeonMaterials.Neon("veil", Color.black));
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", 0);                                  // beidseitig: von innen sichtbar
            mat.renderQueue = 3900;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            v._r.sharedMaterial = mat;
            v._mpb = new MaterialPropertyBlock();
            v._text = UI.MenuPanel.Text(go.transform, "NEOWARD", new Vector3(0f, -0.05f, 0.5f), 0.06f, new Color(0.9f, 0.9f, 0.92f));
            v.Apply();
            v.StartCoroutine(v.Fade(1f, 0f, 0.7f, null));
            return v;
        }

        void Apply()
        {
            _mpb.SetColor(BaseColor, new Color(0f, 0f, 0f, _alpha));
            _r.SetPropertyBlock(_mpb);
            _text.color = new Color(0.9f, 0.9f, 0.92f, _alpha);
            _r.enabled = _alpha > 0.01f; _text.gameObject.SetActive(_alpha > 0.01f);
        }

        IEnumerator Fade(float from, float to, float secs, System.Action done)
        {
            var t = 0f;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / secs));
                Apply();
                yield return null;
            }
            _alpha = to; Apply();
            done?.Invoke();
        }

        /// <summary>Abblenden und Szene neu laden (Mission oder Halle); ohne Schleier sofort.</summary>
        public static void Load(int mission, string label)
        {
            Bootstrap.nextMission = mission;
            if (Instance == null) { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
            Instance._text.text = "NEOWARD\n" + label;
            Instance.StartCoroutine(Instance.Fade(Instance._alpha, 1f, 0.45f, () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)));
        }
    }
}
