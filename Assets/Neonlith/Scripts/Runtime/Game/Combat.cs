using Neonlith.Data;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>Zentrale Kampfregeln, 1:1 aus scripts/game/combat.gd (Regeln 1 bis 12 dort).</summary>
    public static class Combat
    {
        /// <summary>Plasma-Brand zersetzt Rüstung, solange er wirkt (Rüstungsbruch "shred").</summary>
        public static float EffectiveArmor(Enemy e) => Mathf.Max(0f, e.armor - (e.burnTime > 0f ? e.armorBreak : 0f));

        /// <summary>Direkter Treffer: Rüstung zieht ab, mindestens 25 % kommen durch.</summary>
        public static float DirectDamage(Enemy e, float amount, float armorPierce = 0f)
        {
            var arm = EffectiveArmor(e) * (1f - Mathf.Clamp01(armorPierce));
            return Mathf.Max(amount - arm, amount * Balance.ArmorMinFactor);
        }

        /// <summary>Schadensfaktor nach Schadensart (physisch oder elementar) und Quelle (src_mult, "dot" = Brand).</summary>
        public static float TypeMult(Enemy e, string source)
        {
            var m = Balance.IsPhysical(source) ? e.physMult : e.elemMult;
            if (e.type.srcMult.TryGetValue(source, out var k)) m *= k;
            return m;
        }

        /// <summary>Verlangsamung stapelt nicht: der stärkste aktive Effekt zählt (max. 70 %).</summary>
        public static void ApplySlow(Enemy e, float amount, float duration)
        {
            var a = Mathf.Min(amount * (1f - e.slowResist), e.maxSlow);
            duration *= e.slowDurMult;
            if (e.slowTime <= 0f || a > e.slowAmount + 0.001f)
            {
                e.slowAmount = a;
                e.slowTime = duration;
            }
            else if (Mathf.Abs(a - e.slowAmount) <= 0.001f)
                e.slowTime = Mathf.Max(e.slowTime, duration);
        }

        /// <summary>Einfrieren (Schockfrost): Ziel bleibt kurz stehen; Boss immun, Kontrollresistente nur verlangsamt.</summary>
        public static void ApplyFreeze(Enemy e, float duration)
        {
            if (e.type.boss || e.slowResist >= 0.5f) return;
            if (e.maxSlow < 0.5f) { ApplySlow(e, 1f, duration); return; }
            e.freezeTime = Mathf.Max(e.freezeTime, duration);
        }

        /// <summary>Brand stapelt nicht: neuer Brand erneuert die Dauer, der höhere Schaden gilt; Schild blockt.</summary>
        public static void ApplyBurn(Enemy e, float dps, float duration, float shred = 0f)
        {
            if (e.shield > 0f) return;
            if (e.burnTime <= 0f) { e.burnDps = dps; e.armorBreak = shred; }
            else { e.burnDps = Mathf.Max(e.burnDps, dps); e.armorBreak = Mathf.Max(e.armorBreak, shred); }
            e.burnTime = Mathf.Max(e.burnTime, duration);
        }

        public static bool CanHeal(Enemy e)
        {
            if (Balance.BurnBlocksHealing && e.burnTime > 0f) return false;
            return e.hp < e.maxHp;
        }
    }
}
