using Neonlith.View;
using UnityEngine;

namespace Neonlith.XR
{
    /// <summary>
    /// Roboterhand über dem Controller (XRBody + RobotModels.hand in Godot, 4.99.51, vereinfacht): weißer Handrücken,
    /// dunkle Finger in Gelenkketten, die sich nach Abzug und Griff beugen (die Waffenhand hält den Griff, der
    /// Zeigefinger zieht mit dem Abzug), dazu ein Unterarm vom Handgelenk zu einem geschätzten Ellbogen (Richtung
    /// Schulter unter dem Kopf). Nahe am Auge verschwindet die Hand. Maße in Metern, sitzt an der Zielpose.
    /// </summary>
    public sealed class XRHandVisual : MonoBehaviour
    {
        const float HideNear = 0.14f, ForearmLen = 0.27f;
        public bool left, weaponHand;
        public Transform head;
        Transform _palm, _arm;
        readonly Transform[][] _fingers = new Transform[5][];
        Renderer[] _renderers;

        public static XRHandVisual Create(Transform hand, Transform head, bool left, bool weaponHand, WorldTheme theme)
        {
            var go = new GameObject("Hand");
            go.transform.SetParent(hand, false);
            // Griffhaltung: Handfläche hinter und unter der Zielpose, leicht nach innen gedreht
            go.transform.localPosition = new Vector3(0f, -0.03f, -0.07f);
            go.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            var h = go.AddComponent<XRHandVisual>();
            h.left = left; h.weaponHand = weaponHand; h.head = head;
            h.Build(theme);
            return h;
        }

        void Build(WorldTheme theme)
        {
            var white = NeonMaterials.Lit("hand_white", new Color(0.92f, 0.93f, 0.95f), 0.35f, 0.55f);
            var dark = NeonMaterials.Lit("hand_dark", new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.4f);
            var accent = NeonMaterials.Neon("neon", theme.neon);
            var side = left ? -1f : 1f;
            var palmKit = new MeshKit();
            palmKit.Box(new Vector3(0f, 0f, 0f), new Vector3(0.08f, 0.025f, 0.09f));
            var acc = new MeshKit();
            acc.Strip(new Vector3(-0.03f, 0.0135f, -0.03f), new Vector3(0.03f, 0.0135f, -0.03f), 0.004f);
            _palm = palmKit.Spawn("Handruecken", white, transform, false).transform;
            acc.Spawn("Akzent", accent, _palm, false);
            // vier Finger vorn, Daumen seitlich; je drei Glieder als Kette
            for (var f = 0; f < 4; f++)
            {
                var x = -0.03f + 0.02f * f;
                _fingers[f] = Chain(new Vector3(x * side, 0.004f, 0.045f), Quaternion.identity, new[] { 0.035f, 0.025f, 0.02f }, dark);
            }
            _fingers[4] = Chain(new Vector3(0.045f * side, -0.002f, 0.0f), Quaternion.Euler(0f, 55f * side, 0f), new[] { 0.035f, 0.025f }, dark);
            var armKit = new MeshKit();
            armKit.Box(new Vector3(0f, 0f, -ForearmLen * 0.5f), new Vector3(0.05f, 0.04f, ForearmLen));
            _arm = armKit.Spawn("Unterarm", white, null, false).transform;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        Transform[] Chain(Vector3 root, Quaternion rot, float[] lens, Material mat)
        {
            var joints = new Transform[lens.Length];
            var parent = _palm;
            var pos = root;
            for (var i = 0; i < lens.Length; i++)
            {
                var j = new GameObject("Glied" + i).transform;
                j.SetParent(parent, false);
                j.localPosition = pos; j.localRotation = i == 0 ? rot : Quaternion.identity;
                var k = new MeshKit();
                k.Box(new Vector3(0f, 0f, lens[i] * 0.5f), new Vector3(0.014f, 0.012f, lens[i]));
                k.Spawn("Glied", mat, j, false);
                joints[i] = j;
                parent = j; pos = new Vector3(0f, 0f, lens[i]);
            }
            return joints;
        }

        void Update()
        {
            // Beugung: Zeigefinger mit dem Abzug, übrige Finger mit dem Griff (Waffenhand hält den Griff)
            var trig = left == XRInput.LeftHanded ? XRInput.WeaponTrigger : XRInput.OtherTrigger;
            var grip = left ? XRInput.LeftGrip.ReadValue<float>() : XRInput.RightGrip.ReadValue<float>();
            var holdGrip = weaponHand ? 0.9f : grip;
            var curlIndex = weaponHand ? Mathf.Lerp(0.35f, 0.9f, trig) : trig;
            for (var f = 0; f < 5; f++)
            {
                var c = f == 0 ? curlIndex : (f == 4 ? 0.7f * holdGrip : holdGrip);
                var joints = _fingers[f];
                for (var i = 0; i < joints.Length; i++)
                {
                    var bend = Mathf.Lerp(0f, 70f + 15f * i, c);
                    joints[i].localRotation = (i == 0 && f == 4 ? Quaternion.Euler(0f, 55f * (left ? -1f : 1f), 0f) : Quaternion.identity) * Quaternion.Euler(bend, 0f, 0f);
                }
            }
            // Unterarm vom Handgelenk zum geschätzten Ellbogen (Richtung Schulter unter dem Kopf)
            if (head != null && _arm != null)
            {
                var wrist = _palm.position - _palm.forward * 0.05f;
                var shoulder = head.position + head.right * (left ? -0.18f : 0.18f) - Vector3.up * 0.22f;
                var dir = (wrist - shoulder).normalized;
                var elbow = wrist - dir * ForearmLen;
                _arm.position = wrist;
                _arm.rotation = Quaternion.LookRotation((wrist - elbow).normalized, Vector3.up);
                var near = Vector3.Distance(head.position, _palm.position) < HideNear;
                foreach (var r in _renderers) r.enabled = !near;
            }
        }
    }
}
