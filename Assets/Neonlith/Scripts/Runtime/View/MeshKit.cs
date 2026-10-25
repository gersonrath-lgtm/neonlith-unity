using System.Collections.Generic;
using UnityEngine;

namespace Neonlith.View
{
    /// <summary>
    /// Netzbaukasten (Gegenstück zu scripts/view3d/mesh_kit.gd): Kästen, Platten zwischen zwei Punkten (auch als
    /// Rampe), Prismen und flache Leuchtstreifen in ein Netz sammeln. Ein Kit je Werkstoff, dann <see cref="Build"/>.
    /// Einheiten Meter, Normalen je Fläche (facettiert, keine weichen Kanten: Klarbild).
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;
        public bool IsEmpty => _t.Count == 0;

        /// <summary>Viereck aus vier Punkten gegen den Uhrzeigersinn (von außen gesehen).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            var i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (var k = 0; k < 4; k++) _n.Add(n);
            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(1, 1)); _uv.Add(new Vector2(0, 1));
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 1);
            _t.Add(i); _t.Add(i + 3); _t.Add(i + 2);
        }

        /// <summary>Kasten aus acht Ecken: 0..3 unten (gegen den Uhrzeigersinn von oben), 4..7 oben darüber.</summary>
        public void Box8(Vector3[] p)
        {
            Quad(p[4], p[5], p[6], p[7]);            // oben
            Quad(p[0], p[3], p[2], p[1]);            // unten
            Quad(p[0], p[1], p[5], p[4]);
            Quad(p[1], p[2], p[6], p[5]);
            Quad(p[2], p[3], p[7], p[6]);
            Quad(p[3], p[0], p[4], p[7]);
        }

        /// <summary>Achsparalleler Kasten um die Mitte.</summary>
        public void Box(Vector3 center, Vector3 size)
        {
            var h = size * 0.5f;
            Box8(new[]
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(h.x, h.y, -h.z),
            });
        }

        /// <summary>
        /// Platte von a nach b (Oberkanten-Mitten, beliebige Höhen: Rampe), Breite quer dazu, Dicke nach unten.
        /// Optional seitlich versetzt (offset in Breitenrichtung, +rechts in Laufrichtung).
        /// </summary>
        public void Slab(Vector3 a, Vector3 b, float width, float thickness, float offset = 0f, float lift = 0f)
        {
            var along = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (along.sqrMagnitude < 1e-6f) return;
            along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            var hw = right * (width * 0.5f);
            var off = right * offset + Vector3.up * lift;
            var a0 = a + off; var b0 = b + off;
            var dn = Vector3.down * thickness;
            Box8(new[]
            {
                a0 - hw + dn, a0 + hw + dn, b0 + hw + dn, b0 - hw + dn,
                a0 - hw, a0 + hw, b0 + hw, b0 - hw,
            });
        }

        /// <summary>Flacher Streifen von a nach b (Leuchtlinie), knapp über der Fläche, mit Normalen nach oben.</summary>
        public void Strip(Vector3 a, Vector3 b, float width, float offset = 0f, float lift = 0.004f)
        {
            var along = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (along.sqrMagnitude < 1e-6f) return;
            along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            var hw = right * (width * 0.5f);
            var off = right * offset + Vector3.up * lift;
            Quad(a + off - hw, a + off + hw, b + off + hw, b + off - hw);
        }

        /// <summary>Regelmäßiges Prisma (n Ecken, Umkreis r) um die Mitte, Höhe h nach oben, Phase: erste Ecke.</summary>
        public void Prism(Vector3 center, float r, float h, int sides, float phase = 0f)
        {
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (var i = 0; i < sides; i++)
            {
                var a = phase + Mathf.PI * 2f * i / sides;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                bottom[i] = center + p;
                top[i] = center + p + Vector3.up * h;
            }
            Fan(top, true);
            Fan(bottom, false);
            for (var i = 0; i < sides; i++)
            {
                var j = (i + 1) % sides;
                Quad(bottom[j], bottom[i], top[i], top[j]);
            }
        }

        /// <summary>Flacher Ring (Leuchtband) auf Höhe y um die Mitte, von r0 bis r1.</summary>
        public void Ring(Vector3 center, float r0, float r1, int sides, float phase = 0f)
        {
            for (var i = 0; i < sides; i++)
            {
                var a0 = phase + Mathf.PI * 2f * i / sides;
                var a1 = phase + Mathf.PI * 2f * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Quad(center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0);
            }
        }

        void Fan(Vector3[] ring, bool up)
        {
            var i0 = _v.Count;
            var n = up ? Vector3.up : Vector3.down;
            foreach (var p in ring)
            {
                _v.Add(p); _n.Add(n); _uv.Add(new Vector2(p.x, p.z));
            }
            for (var i = 1; i < ring.Length - 1; i++)
            {
                _t.Add(i0);
                if (up) { _t.Add(i0 + i + 1); _t.Add(i0 + i); }
                else { _t.Add(i0 + i); _t.Add(i0 + i + 1); }
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Netz als Objekt in die Szene, optional mit Kollision (Boden, Treffer der Waffe).</summary>
        public GameObject Spawn(string name, Material mat, Transform parent, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = Build(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }
}
