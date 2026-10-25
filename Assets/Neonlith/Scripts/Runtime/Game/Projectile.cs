using System.Collections.Generic;
using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>Geschoss mit Flugbogen (scripts/game/projectile.gd): Bodenpunkt, Fortschritt t, Bogenhöhe; Treffer meldet Game.projectile_hit.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        public GameSession session;
        public string kind;
        public Dictionary<string, float> stats;
        public Enemy target;
        public Vector3 targetPos, start;
        public float t, speed, arc, startH;
        public bool done;
        Transform _vis;

        public static Projectile Spawn(GameSession s, Tower tw, Enemy target, Transform parent)
        {
            var go = new GameObject("Geschoss_" + tw.Kind);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Projectile>();
            p.session = s; p.kind = tw.Kind; p.stats = tw.Stats; p.target = target;
            p.targetPos = target.Center;
            p.start = tw.Muzzle;
            p.startH = 0f;
            p.speed = p.stats.Get("proj_speed", 200f) / Balance.TilePath * Balance.FieldM;   // Wegpixel/s → m/s
            switch (tw.Kind) { case "arrow": p.arc = 0.4f; break; case "ice": p.arc = 0.2f; break; case "fire": p.arc = 1.2f; break; case "mortar": p.arc = 3.0f; break; }
            var col = Balance.TowerColor.TryGetValue(tw.Kind, out var c) ? c : Color.white;
            var vis = GameObject.CreatePrimitive(tw.Kind == "mortar" ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            Destroy(vis.GetComponent<Collider>());
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = Vector3.one * (tw.Kind == "mortar" ? 0.22f : tw.Kind == "fire" ? 0.18f : 0.1f);
            vis.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Neon("tower_" + tw.Kind, col);
            p._vis = vis.transform;
            go.transform.position = p.start;
            return p;
        }

        public void Step(float dt)
        {
            if (done) return;
            if (target != null && target.alive) targetPos = target.Center;
            var total = Mathf.Max(0.5f, Vector3.Distance(start, targetPos));
            t = Mathf.Min(1f, t + speed * dt / total);
            var p = Vector3.Lerp(start, targetPos, t) + Vector3.up * (4f * arc * t * (1f - t));
            if (_vis != null && (p - transform.position).sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(p - transform.position);
            transform.position = p;
            if (t >= 1f)
            {
                done = true;
                session.ProjectileHit(this);
                Destroy(gameObject);
            }
        }
    }
}
