using Neonlith.Data;
using Neonlith.View;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Ein Gegner (scripts/game/enemy.gd): Bewegung entlang des Wegs mit Querlage, Statuseffekte (Verlangsamung,
    /// Einfrieren, Brand mit Rüstungsbruch, Verwundbarkeit), Schild mit Nachladen, Fähigkeiten (Hetzer-Sprint,
    /// Schilder-Abschirmung, Nebelweberin-Heilung, Spieher-Markierung, Packträger-Platzen, Eisfürst-Regeneration,
    /// Boss-Rufe und Wut), Eliten (Balance.ELITES), Flieger, Barrikaden-Halt. Darstellung: rote Form nach
    /// Balance "swarm" (alle Gegner rot: WorldTheme.Threat), Status als Tönung, Lebensleiste für Eliten und Bosse.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyType type;
        public GameSession session;
        public float hp, maxHp, armor;
        public int bounty, damage, pathId, seq;
        public bool alive = true, leaked, flying, cloaked;
        public string elite = "";
        public float dist, lateral;        // Wegstrecke und Querlage (m)
        // Status
        public float slowTime, slowAmount, freezeTime, burnTime, burnDps, armorBreak, vulnTime, vulnAmount, wildfireDmg;
        public float shield, shieldMax, shieldRegen, shieldDelay = 3f, sinceHit = 99f;
        public float physMult = 1f, elemMult = 1f, slowResist, maxSlow = 0.7f, slowDurMult = 1f;
        public float guarded, dashT, dashCd, markT, healTimer, summonT, age;
        public int summonIndex;
        public bool enraged, held;
        public float lastHeal = -99f, healGlow;

        Vector3[] _path;
        float[] _cum;
        float _total, _baseSpeed, _speedMult = 1f, _bodyScale = 1f;
        float _dotAcc, _dotT, _flash, _hover, _fade;
        Renderer _r;
        MaterialPropertyBlock _mpb;
        Transform _bar, _barFill, _shieldVis;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        Vector3 _fwd = Vector3.forward, _right = Vector3.right;

        public static Enemy Spawn(GameSession s, EnemyType t, Vector3[] path, int pathId, float maxHp, float atDist, int seq, Transform parent)
        {
            var go = new GameObject("Gegner_" + t.id);
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<Enemy>();
            e.session = s; e.type = t; e.pathId = pathId; e.seq = seq;
            e.maxHp = maxHp; e.hp = maxHp; e.armor = t.armor; e.bounty = t.bounty; e.damage = t.damage;
            e.flying = t.flying;
            e.slowResist = t.slowResist; e.maxSlow = Mathf.Min(t.maxSlow, Balance.SlowCap); e.slowDurMult = t.slowDurMult;
            e.physMult = t.physMult; e.elemMult = t.elemMult;
            e._path = path;
            e._cum = new float[path.Length];
            for (var i = 1; i < path.Length; i++) e._cum[i] = e._cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            e._total = e._cum[path.Length - 1];
            e._baseSpeed = t.speed * Balance.EnemySpeedK * Balance.FieldM;
            if (!t.noVariation && !t.boss) { e._speedMult = Random.Range(0.95f, 1.05f); e._bodyScale = Random.Range(0.92f, 1.08f); }
            // Schild wächst mit den Lebenspunkten, die Ladezeit bleibt
            var grow = maxHp / Mathf.Max(1f, t.hp);
            e.shieldMax = t.shield * grow; e.shield = e.shieldMax; e.shieldRegen = t.shieldRegen * grow; e.shieldDelay = t.shieldDelay > 0 ? t.shieldDelay : 3f;
            e.healTimer = t.healInterval * Random.Range(0.5f, 1f);
            e.summonT = t.summonEvery;
            e.markT = t.markEvery * Random.Range(0.4f, 0.8f);
            // Querlage: breite Gegner näher zur Mitte, Schwarm auf fester Spur
            var halfBody = (t.halfWidth > 0 ? t.halfWidth : 0.27f * t.size) * e._bodyScale;
            var room = Mathf.Max(0f, MissionData.RoadWidth * 0.5f - 0.35f - halfBody);
            e.lateral = t.boss && t.fillRoad ? 0f : Random.Range(-room, room) * Balance.FieldM;
            e.dist = atDist;
            e.BuildVisual();
            e.Place();
            return e;
        }

        /// <summary>Elite-Variante: viel mehr Leben, größer, mehr Kopfgeld und eine Eigenschaft.</summary>
        public void MakeElite(string kind)
        {
            if (!Balance.Elites.TryGetValue(kind, out var el) || type.boss || (type.Has("Swarm") && type.eliteHp <= 0f)) return;
            elite = kind;
            var k = type.eliteHp > 0f ? type.eliteHp : (type.Has("Tank") ? Balance.EliteHpTank : Balance.EliteHp);
            k *= el.hpMult;
            maxHp *= k; hp = maxHp;
            bounty *= Balance.EliteBounty;
            _bodyScale *= type.eliteSize > 0f ? type.eliteSize : Balance.EliteSize;
            if (el.shield > 0f) { shieldMax = maxHp * el.shield; shield = shieldMax; shieldRegen = maxHp * el.shieldRegen; shieldDelay = el.shieldDelay; }
            if (el.physMult != 1f) physMult = el.physMult;
            if (el.maxSlow >= 0f) maxSlow = Mathf.Min(maxSlow, el.maxSlow);
            slowDurMult = el.slowDurMult;
            armor += el.armorAdd;
            cloaked = el.cloak;
            RebuildVisual();
        }

        void BuildVisual()
        {
            var size = type.size * _bodyScale;
            Vector3 box;
            switch (type.swarm)
            {
                case "small": box = new Vector3(0.45f, 0.45f, 0.45f) * (size * 2f); break;
                case "long": box = new Vector3(0.5f, 0.5f, 1.4f) * size; break;
                case "heavy": box = new Vector3(1.0f, 0.9f, 1.0f) * size; break;
                default: box = new Vector3(0.8f, 0.8f, 0.8f) * size; break;
            }
            if (type.boss) box = new Vector3(1.2f, 1.1f, 1.2f) * size;
            _hover = box.y * 0.5f + 0.25f + (flying ? 2.5f : 0f);
            var vis = GameObject.CreatePrimitive(flying ? PrimitiveType.Sphere : PrimitiveType.Cube);
            vis.name = "Form";
            vis.transform.SetParent(transform, false);
            vis.transform.localPosition = Vector3.up * _hover;
            vis.transform.localScale = box;
            _r = vis.GetComponent<Renderer>();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _r.receiveShadows = false;
            _r.sharedMaterial = NeonMaterials.Glow("enemy", WorldTheme.Threat * 0.6f, WorldTheme.Threat, 1.6f);
            _mpb = new MaterialPropertyBlock();
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            gameObject.layer = GameSession.EnemyLayer; vis.layer = GameSession.EnemyLayer;
            if (type.boss || type.miniBoss) BuildBar();
        }

        void RebuildVisual()
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            BuildVisual();
            if (_bar == null) BuildBar();
        }

        void BuildBar()
        {
            _bar = new GameObject("Leben").transform;
            _bar.SetParent(transform, false);
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(_bar, false);
            back.transform.localScale = new Vector3(1.6f, 0.12f, 1f);
            back.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Neon("bar_back", new Color(0.1f, 0.1f, 0.1f));
            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(_bar, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            fill.transform.localScale = new Vector3(1.6f, 0.1f, 1f);
            fill.GetComponent<Renderer>().sharedMaterial = NeonMaterials.Neon("bar_fill", WorldTheme.ThreatHi);
            _barFill = fill.transform;
        }

        public Vector3 Center => transform.position + Vector3.up * _hover;
        public Vector3 Position => transform.position;
        public bool IsTargetable => alive && dist >= MissionData.EntryLead * Balance.FieldM * 0.5f;
        public bool TowerTargetable => IsTargetable && !cloaked;
        public bool IsSlowed => slowTime > 0f;
        public bool IsBurning => burnTime > 0f;

        public float CurrentSpeed()
        {
            if (freezeTime > 0f) return 0f;
            var s = _baseSpeed * _speedMult;
            if (slowTime > 0f) s *= 1f - slowAmount;
            if (enraged) s *= type.enrageSpeed;
            if (dashT > 0f) s *= type.dashSpeed;
            return s;
        }

        /// <summary>Hetzer: ein Treffer löst einen kurzen Sprint aus.</summary>
        public void OnHit()
        {
            if (dashCd <= 0f && type.dashTime > 0f && freezeTime <= 0f) { dashT = type.dashTime; dashCd = type.dashCd; }
        }

        public void HitFlash() => _flash = 0.09f;

        /// <summary>Simulationsschritt (Game.sim_step → Enemy.sim_step).</summary>
        public void Step(float dt)
        {
            if (!alive) return;
            age += dt; sinceHit += dt;
            if (_fade < 1f) _fade = Mathf.Min(1f, _fade + dt * 2.5f);
            if (shieldMax > 0f && shield < shieldMax && sinceHit >= shieldDelay) shield = Mathf.Min(shieldMax, shield + shieldRegen * dt);
            if (freezeTime > 0f) freezeTime = Mathf.Max(0f, freezeTime - dt);
            if (vulnTime > 0f) { vulnTime -= dt; if (vulnTime <= 0f) { vulnTime = 0f; vulnAmount = 0f; } }
            if (slowTime > 0f) { slowTime -= dt; if (slowTime <= 0f) { slowTime = 0f; slowAmount = 0f; } }
            var dot = 0f;
            if (burnTime > 0f)
            {
                var bt = Mathf.Min(dt, burnTime);
                _dotAcc += burnDps * bt; _dotT -= dt; burnTime -= dt;
                if (burnTime <= 0f) { burnTime = 0f; burnDps = 0f; wildfireDmg = 0f; armorBreak = 0f; }
                if (_dotT <= 0f || burnTime <= 0f) { dot = _dotAcc; _dotAcc = 0f; _dotT = Balance.DotTick; }
            }
            if (dot > 0f) { session.DamageEnemy(this, dot, "dot", false); if (!alive) return; }
            if (type.regenPct > 0f && burnTime <= 0f && hp < maxHp) hp = Mathf.Min(maxHp, hp + maxHp * type.regenPct * dt);
            if (dashT > 0f) dashT = Mathf.Max(0f, dashT - dt);
            if (dashCd > 0f) dashCd = Mathf.Max(0f, dashCd - dt);
            if (guarded > 0f) guarded = Mathf.Max(0f, guarded - dt * 2f);
            if (type.markEvery > 0f && IsTargetable)
            {
                markT -= dt;
                if (markT <= 0f) markT = session.SpotterMark(this) ? type.markEvery : 0.5f;
            }
            if (type.healRatio > 0f)
            {
                healTimer -= dt;
                if (healTimer <= 0f) { healTimer += type.healInterval; session.HealPulse(this); }
            }
            if (type.boss) BossLogic(dt);
            Move(dt);
            if (!alive || leaked) return;
            if (healGlow > 0f) healGlow = Mathf.Max(0f, healGlow - dt * 2f);
        }

        void BossLogic(float dt)
        {
            var ratio = hp / maxHp;
            if (summonT > 0f)
            {
                summonT -= dt;
                if (summonT <= 0f) { summonT = type.summonEvery; session.BossSummon(this, type.summonType, type.summonCount, true); }
            }
            while (summonIndex < type.summonAt.Length && ratio <= type.summonAt[summonIndex])
            {
                summonIndex++;
                session.BossSummon(this, type.summonAtType.Length > 0 ? type.summonAtType : type.summonType, type.summonAtCount > 0 ? type.summonAtCount : type.summonCount, false);
            }
            if (!enraged && type.enrageAt > 0f && ratio <= type.enrageAt) { enraged = true; session.BossEnraged(this); }
        }

        void Move(float dt)
        {
            var sp = CurrentSpeed();
            var nd = dist + sp * dt;
            held = false;
            if (!flying)
            {
                var stop = session.BarrierStop(this, dist, nd);
                if (stop < nd) { held = true; nd = Mathf.Max(dist, stop); }
            }
            dist = nd;
            if (dist >= _total)
            {
                dist = _total;
                session.EnemyReached(this);
                return;
            }
            Place();
        }

        void Place()
        {
            var i = 1;
            while (i < _cum.Length - 1 && _cum[i] < dist) i++;
            var seg = Mathf.Max(_cum[i] - _cum[i - 1], 1e-4f);
            var t = Mathf.Clamp01((dist - _cum[i - 1]) / seg);
            var d = _path[i] - _path[i - 1];
            var flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 1e-4f)
            {
                _fwd = flat.normalized;
                _right = Vector3.Cross(Vector3.up, _fwd);
            }
            transform.position = Vector3.Lerp(_path[i - 1], _path[i], t) + _right * lateral;
            transform.rotation = Quaternion.LookRotation(_fwd, Vector3.up);
        }

        void Update()
        {
            if (!alive || _r == null) return;
            if (_flash > 0f) _flash -= Time.deltaTime;
            // Tönung: Treffer weiß, Frost blau, Brand orange, Verwundbar heller, Schild cyan Rand, Tarnung dunkel
            var k = Mathf.Clamp01(_flash / 0.09f);
            var baseCol = WorldTheme.Threat * 0.6f;
            var emit = WorldTheme.Threat.linear * 1.6f;
            if (slowTime > 0f) { baseCol = Color.Lerp(baseCol, new Color(0.55f, 0.8f, 1f), 0.5f); emit = Color.Lerp(emit, new Color(0.3f, 0.6f, 1f), 0.5f); }
            if (burnTime > 0f) { var f = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f); emit = Color.Lerp(emit, new Color(1f, 0.55f, 0.15f) * 2f, 0.4f + 0.3f * f); }
            if (vulnTime > 0f) emit *= 1.4f;
            if (shield > 0f) baseCol = Color.Lerp(baseCol, new Color(0.3f, 0.9f, 1f), 0.35f);
            if (cloaked) { baseCol *= 0.25f; emit *= 0.1f; }
            if (healGlow > 0f) emit = Color.Lerp(emit, new Color(0.6f, 1f, 0.4f) * 2f, healGlow * 0.7f);
            var a = _fade;
            _mpb.SetColor(BaseColor, Color.Lerp(baseCol, Color.white, k) * a);
            _mpb.SetColor(EmissionColor, Color.Lerp(emit, Color.white * 3f, k) * a);
            _r.SetPropertyBlock(_mpb);
            if (_bar != null)
            {
                var cam = Camera.main;
                _bar.position = Center + Vector3.up * (_hover * 0.8f + 0.4f);
                if (cam != null) _bar.rotation = Quaternion.LookRotation(_bar.position - cam.transform.position, Vector3.up);
                var r = Mathf.Clamp01(hp / maxHp);
                _barFill.localScale = new Vector3(1.6f * r, 0.1f, 1f);
                _barFill.localPosition = new Vector3(-0.8f * (1f - r), 0f, -0.005f);
            }
        }

        public void Despawn()
        {
            alive = false;
            Destroy(gameObject);
        }
    }
}
