using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;
using static Version;

namespace StaggerScaler
{
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    public static class Character_ApplyDamage_Patch
    {

        private static WeakSpot GetWeakSpot(Character instance, short index)
        {
            if (index < 0 || index >= instance.m_weakSpots.Length)
            {
                return null;
            }

            return instance.m_weakSpots[index];
        }



        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (!__instance.IsPlayer()) return;

            float MPdifficulty = Game.instance.GetDifficultyDamageScalePlayer(__instance.transform.position);
            float MPDiffCoef = ((MPdifficulty - 1.0f) * StaggerScalerPlugin.ConfigDesiredEnemyDamageMP.Value + 1.0f);
            float DamageReduceFactor = StaggerScalerPlugin.ConfigDesiredEnemyDamageMult.Value / Game.m_enemyDamageRate;
            HitData revHit;

            float armor = __instance.GetBodyArmor();

            float totalVanillaDmg = 0;
            // SCENARIO 1: The hit was blocked! Catch the revHit passed down from BlockAttack
            if (DamageContext.hitTracker.TryGetValue(__instance, out HitData blockedRevHit) && StaggerScalerPlugin.ConfigEnableShieldScaling.Value)
            {
                revHit = blockedRevHit;
                HitData rollBackHit = hit.Clone();

                WeakSpot weakSpot = GetWeakSpot(__instance, revHit.m_weakSpot);
                HitData.DamageModifiers damageModifiers = __instance.GetDamageModifiers(weakSpot);
                revHit.ApplyResistance(damageModifiers, out var significantModifier);

                RevArmor.ReverseHitDataApplyArmor(rollBackHit, armor);
                totalVanillaDmg = rollBackHit.GetTotalDamage();
            }
            // SCENARIO 2: Pure unblocked direct hit. Build a fresh rollback snapshot from scratch
            else
            {


                revHit = hit.Clone();
                RevArmor.ReverseHitDataApplyArmor(revHit, armor);
                totalVanillaDmg = revHit.GetTotalDamage();
                revHit.ApplyModifier(1.0f * DamageReduceFactor * MPDiffCoef / MPdifficulty);
            }

            
            float totalModdedDmg = revHit.GetTotalDamage();

            // -------------------------------------------------------------
            // THE UNIFIED BODY ARMOR STAGE
            // -------------------------------------------------------------
            float bodyArmor = __instance.GetBodyArmor();

            HitData.DamageTypes damageTypes = revHit.m_damage.Clone();
            damageTypes.ApplyArmor(bodyArmor);

            // Compute the true custom singleplayer stagger build-up value
            float totalStaggerDamage = damageTypes.GetTotalStaggerDamage();

            // Store it into your global tracker so AddStaggerDamage can override the game parameters!
            
            var stagger = DamageContext.staggerArmorTracker.GetOrCreateValue(__instance);
            stagger.Value = totalStaggerDamage;
            DamageContext.hitTracker.Remove(__instance);

            float maxStagger = __instance.GetMaxHealth() * __instance.m_staggerDamageFactor;
            float staggerPercent = (totalStaggerDamage / maxStagger) * 100.0f;
            HelperFunctions.armorLog(__instance, totalVanillaDmg, totalModdedDmg, bodyArmor, totalStaggerDamage, MPDiffCoef/MPdifficulty, DamageReduceFactor, staggerPercent);




        }
        public static void Postfix(Character __instance, HitData hit)
        {
            Character player = __instance.IsPlayer() ? __instance : hit.GetAttacker();
            if (player != null)
            {
                if (DamageContext.LogTracker.TryGetValue(player, out var log))
                {
                    log = log.Replace("currentStagger", $"{player.GetStaggerPercentage() * 100.0f:F0}"); 
                    DamageContext.LogTracker.Remove(player);
                    DamageContext.staggerArmorTracker.Remove(player);
                    HelperFunctions.LogToF5Console(player, log);
                }
            }
        }

    }
}