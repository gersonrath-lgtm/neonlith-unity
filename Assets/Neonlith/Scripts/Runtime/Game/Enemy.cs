using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Ein Gegner (Logik aus scripts/game/enemy.gd, Darstellung aus EnemySwarm/EnemyVisual, beides stark
    /// vereinfacht): läuft den Weg entlang (Felder je Sekunde mal ENEMY_SPEED_K), hat Leben und Rüstung, leuchtet
    /// beim Treffer kurz auf, kostet am Kern Leben. Form nach Balance "swarm": cube, small, long, heavy.
    /// Alle Gegner sind rot (WorldTheme.Threat: die Bedrohung ist in jeder Welt gleich codiert).
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyType type;
        public float hp, maxHp;
        public int pathId;
        public bool alive = true;
        public GameSession session;

        Vector3[] _path;
        float[] _cum;
        float _dist, _speed, _total;
        float _flash;
        Renderer _r;
        MaterialPropertyBlock _mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        Color _emit;
        float _hover;

        public static Enemy Spawn(GameSession s, EnemyType t, Vector3[] path, int pathId, float maxHp, Transform parent)
        {
            var go = new GameObject("Gegner_" + t.id);
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<Enemy>();
            e.session = s;
            e.type = t;
            e.pathId = pathId;
            e.maxHp = maxHp;
            e.hp = maxHp;
            e._path = path;
            e._speed = t.speed * Balance.EnemySpeedK * Balance.FieldM * Random.Range(0.96f, 1.04f);
            e._cum = new float[path.Length];
            for (var i = 1; i < path.Length; i++) e._cum[i] = e._cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            e._total = e._cum[path.Length - 1];
            e.BuildVisual();
            e.Place();
            return e;
        }

        void BuildVisual()
        {
            var size = type.size;
            Vector3 box;
            switch (type.swarm)
            {
                case "small": box = new Vector3(0.45f, 0.45f, 0.45f) * (size * 2f); break;   // size 0.5 → 0,45 m
                case "long": box = new Vector3(0.5f, 0.5f, 1.4f) * size; break;
                case "heavy": box = new Vector3(1.0f, 0.9f, 1.0f) * size; break;
                default: box = new Vector3(0.8f, 0.8f, 0.8f) * size; break;
            }
            if (type.boss) box = new Vector3(1.2f, 1.1f, 1.2f) * size;
            _hover = box.y * 0.5f + 0.25f;
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Form";
            vis.transform.SetParent(transform, false);
            vis.transform.localPosition = Vector3.up * _hover;
            vis.transform.localScale = box;
            _r = vis.GetComponent<Renderer>();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _r.receiveShadows = false;
            _emit = WorldTheme.Threat;
            _r.sharedMaterial = NeonMaterials.Glow("enemy", WorldTheme.Threat * 0.6f, WorldTheme.Threat, 1.6f);
            _mpb = new MaterialPropertyBlock();
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            gameObject.layer = GameSession.EnemyLayer;
            vis.layer = GameSession.EnemyLayer;
        }

        void Update()
        {
            if (!alive) return;
            var dt = session != null ? session.DeltaTime : Time.deltaTime;
            _dist += _speed * dt;
            if (_dist >= _total)
            {
                session.EnemyReached(this);
                return;
            }
            Place();
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                var k = Mathf.Clamp01(_flash / 0.08f);
                _mpb.SetColor(BaseColor, Color.Lerp(WorldTheme.Threat * 0.6f, Color.white, k));
                _mpb.SetColor(EmissionColor, Color.Lerp(_emit.linear * 1.6f, Color.white * 3f, k));
                _r.SetPropertyBlock(_mpb);
            }
        }

        void Place()
        {
            var i = 1;
            while (i < _cum.Length - 1 && _cum[i] < _dist) i++;
            var seg = Mathf.Max(_cum[i] - _cum[i - 1], 1e-4f);
            var t = Mathf.Clamp01((_dist - _cum[i - 1]) / seg);
            var p = Vector3.Lerp(_path[i - 1], _path[i], t);
            transform.position = p;
            var d = _path[i] - _path[i - 1]; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        /// <summary>Direkter Treffer (Game.damage_enemy mit Combat.direct_damage). Liefert den angerichteten Schaden.</summary>
        public float Hit(float amount)
        {
            if (!alive) return 0f;
            var dmg = Balance.DirectDamage(type, amount);
            hp -= dmg;
            _flash = 0.08f;
            if (hp <= 0f) session.EnemyKilled(this);
            return dmg;
        }

        public void Despawn()
        {
            alive = false;
            Destroy(gameObject);
        }

        /// <summary>Mitte der Form (Zielpunkt).</summary>
        public Vector3 Center => transform.position + Vector3.up * _hover;
    }
}
