using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using static System.Net.Mime.MediaTypeNames;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static Version;


namespace StaggerScaler
{
    // Thread-safe instance maps to cleanly isolate data between multiple players in multiplayer

    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    public static class Humanoid_BlockAttack_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Humanoid __instance, HitData hit, Character attacker, float ___m_blockTimer)
        {

            if (!__instance.IsPlayer()) return;

            ItemDrop.ItemData currentBlocker = HelperFunctions.GetCurrentBlocker(__instance);
            if (currentBlocker == null) return;

            float MPdifficulty = Game.instance.GetDifficultyDamageScalePlayer(__instance.transform.position);
            float MPDiffCoef = ((MPdifficulty - 1.0f) * StaggerScalerPlugin.ConfigDesiredEnemyDamageMP.Value + 1.0f);
            float DamageReduceFactor = StaggerScalerPlugin.ConfigDesiredEnemyDamageMult.Value / Game.m_enemyDamageRate;


            bool parry = ___m_blockTimer != -1f && ___m_blockTimer < 0.25f && currentBlocker.m_shared.m_timedBlockBonus > 1f;
            
            HitData revHit = hit.Clone();
            float totalVanillaDmg = revHit.GetTotalDamage();

            revHit.ApplyModifier(1.0f * DamageReduceFactor * MPDiffCoef/MPdifficulty);

            float totalModdedDmg = revHit.GetTotalDamage();

            float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Blocking);
            float timedBlockBonus = currentBlocker.GetBlockPower(skillFactor);
            if (parry)
            {
                timedBlockBonus *= currentBlocker.m_shared.m_timedBlockBonus;
                __instance.GetSEMan().ModifyTimedBlockBonus(ref timedBlockBonus);
            }


            if (currentBlocker.m_shared.m_damageModifiers.Count > 0)
            {
                HitData.DamageModifiers modifiers = default(HitData.DamageModifiers);
                modifiers.Apply(currentBlocker.m_shared.m_damageModifiers);
                revHit.ApplyResistance(modifiers, out var _);
            }

            HitData.DamageTypes damageTypes = revHit.m_damage.Clone();
            damageTypes.ApplyArmor(timedBlockBonus);

            float totalBlockableDamageWithoutShield = revHit.GetTotalBlockableDamage();
            float totalBlockableDamageWithShield = damageTypes.GetTotalBlockableDamage();
            float blockableDamage = totalBlockableDamageWithoutShield - totalBlockableDamageWithShield;

            float totalStaggerDamage = damageTypes.GetTotalStaggerDamage();

            float maxStagger = __instance.m_staggerDamageFactor * __instance.GetMaxHealth();
            float moddedStagger = (totalStaggerDamage / maxStagger) * 100f;

            DamageContext.hitTracker.AddOrUpdate(__instance, revHit);
            var block = DamageContext.blockedDamageTracker.GetOrCreateValue(__instance);
            block.Value = blockableDamage;
            var stagger = DamageContext.staggerBlockTracker.GetOrCreateValue(__instance);
            stagger.Value = totalStaggerDamage;
            HelperFunctions.blockLog(__instance, totalVanillaDmg, totalModdedDmg, timedBlockBonus, blockableDamage, totalStaggerDamage,moddedStagger);

        }

        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance)
        {
            if (!__instance.IsPlayer()) return;

            if (!DamageContext.staggerBlockTracker.TryGetValue(__instance, out StrongBox<float>stagger)) return;
            
            if (!DamageContext.hitTracker.TryGetValue(__instance, out HitData revHit)) return;
            if (!DamageContext.blockedDamageTracker.TryGetValue(__instance, out StrongBox<float>block)) return;
            
            float totalStaggerDamage = stagger.Value;
            float blockableDamage = block.Value;
            
            bool stamina = __instance.HaveStamina();
            bool staggerOverflow = totalStaggerDamage >= __instance.GetMaxHealth() * __instance.m_staggerDamageFactor;
            if (__instance.m_staggerDamageFactor <= 0f) staggerOverflow = false;

            if (stamina && !staggerOverflow)
            {
                revHit.BlockDamage(blockableDamage);
            }

            DamageContext.hitTracker.AddOrUpdate(__instance, revHit);
            DamageContext.blockedDamageTracker.Remove(__instance);
            DamageContext.staggerBlockTracker.Remove(__instance);

        }
    }
}