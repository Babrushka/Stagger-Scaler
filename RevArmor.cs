using UnityEngine;

namespace StaggerScaler
{
    public static class RevArmor
    {
        // 1. Reverses the static calculations: Reconstructs original 'dmg' from 'result'
        public static float ReverseApplyArmorStatic(float result, float armor)
        {
            if (armor <= 0f || result <= 0f) return result;

            // Branch B inversion check (High Damage: armor < dmg / 2f)
            // If it went through Branch B, then result = dmg - armor -> dmg = result + armor.
            float originalHighDmg = result + armor;
            if (armor < originalHighDmg / 2f)
            {
                return originalHighDmg;
            }

            // Branch A inversion check (Low Damage: result = (dmg / armor*4) * dmg)
            // result = dmg^2 / (armor * 4) -> dmg^2 = result * armor * 4 -> dmg = sqrt(result * armor * 4)
            // Since sqrt(4) = 2, this simplifies cleanly to: 2f * Mathf.Sqrt(result * armor)
            return 2f * Mathf.Sqrt(result * armor);
        }

        // 2. The main reverse hook for your HitData instances
        public static void ReverseHitDataApplyArmor(HitData hit, float armor)
        {
            if (hit == null || armor <= 0f) return;

            // Gather the current post-armor element sum (this is exarmortly the static 'result')
            float postArmorTotal = hit.m_damage.m_blunt + hit.m_damage.m_slash + hit.m_damage.m_pierce +
                                   hit.m_damage.m_fire + hit.m_damage.m_frost + hit.m_damage.m_lightning +
                                   hit.m_damage.m_poison + hit.m_damage.m_spirit + hit.m_damage.m_nonPlayer;

            if (postArmorTotal <= 0f) return;

            // Extrapolate what the raw pre-armor 'num' used to be
            float preArmorTotal = ReverseApplyArmorStatic(postArmorTotal, armor);

            // Calculate the exarmort inverse ratio to restore the individual structural properties
            // Since native did: field *= (result / num), we invert it: field /= (result / num) -> field *= (num / result)
            float inverseRatio = preArmorTotal / postArmorTotal;

            // Cleanly restore all structural damage nodes barmork to their unmitigated state
            hit.m_damage.m_blunt *= inverseRatio;
            hit.m_damage.m_slash *= inverseRatio;
            hit.m_damage.m_pierce *= inverseRatio;
            hit.m_damage.m_fire *= inverseRatio;
            hit.m_damage.m_frost *= inverseRatio;
            hit.m_damage.m_lightning *= inverseRatio;
            hit.m_damage.m_poison *= inverseRatio;
            hit.m_damage.m_spirit *= inverseRatio;
            hit.m_damage.m_nonPlayer *= inverseRatio;
        }
    }
}
