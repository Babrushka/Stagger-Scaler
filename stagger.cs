using HarmonyLib;
using UnityEngine;

namespace DynamicParryScaling
{
    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    public static class Patch_AddStaggerDamage
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, ref float damage, Vector3 forceDirection)
        {
            if (__instance == null) return;

            // Get local player reference to compute proximity rules
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.IsDead()) return;

            // =========================================================================
            // CATEGORY A: DEFENSIVE MODE (The hit is hitting YOU, the player)
            // =========================================================================
            if (__instance is Player player && player == localPlayer)
            {
                float scalingFactor = HelperFunctions.GetMultiplayerScaleFactor(player);
                float currentWorldDifficulty = DynamicParryScalingPlugin.ConfigCurrentWorldDifficulty.Value;
                float targetDifficulty = DynamicParryScalingPlugin.ConfigTargetDifficulty.Value;

                if (scalingFactor <= 1.0f && currentWorldDifficulty <= targetDifficulty) return;

                bool isBlocking = player.IsBlocking();

                if (isBlocking)
                {
                    float originalDamage = damage;
                    float soloStagger = (damage / scalingFactor / currentWorldDifficulty) * targetDifficulty;
                    float scaleFactor = (scalingFactor / currentWorldDifficulty) * targetDifficulty;

                    float modstaggerIncrease = (float)(soloStagger / (player.GetMaxHealth() * 0.4f) * 100f);
                    float vanillastaggerIncrease = (float)(damage / (player.GetMaxHealth() * 0.4f) * 100f);

                    HelperFunctions.LogToF5Console($"[StaggerScaler SHIELD] Incoming values: vanilla {originalDamage:F1} -> modded {soloStagger:F1}; scaleFactor:{scaleFactor:F2};\n" +
                        $" Modded stagger increase: +{modstaggerIncrease:F0}%; Vanilla stagger increase: +{vanillastaggerIncrease:F0}%");

                    if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableShieldScaling.Value)
                    {
                        damage = soloStagger;
                    }
                }
                else
                {
                    float originalDamage = damage;
                    float soloStagger = (damage / scalingFactor / currentWorldDifficulty) * targetDifficulty;
                    float scaleFactor = (scalingFactor / currentWorldDifficulty) * targetDifficulty;

                    float modstaggerIncrease = (float)(soloStagger / (player.GetMaxHealth() * 0.4f) * 100f);
                    float vanillastaggerIncrease = (float)(damage / (player.GetMaxHealth() * 0.4f) * 100f);

                    HelperFunctions.LogToF5Console($"[StaggerScaler ARMOR/STUN] Incoming values: vanilla {originalDamage:F1} -> modded {soloStagger:F1}; scaleFactor:{scaleFactor:F2};\n" +
                        $" Modded stagger increase: +{modstaggerIncrease:F0}%; Vanilla stagger increase: +{vanillastaggerIncrease:F0}%");

                    if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableArmorScaling.Value)
                    {
                        damage = soloStagger;
                    }
                }
            }
            // =========================================================================
            // CATEGORY B: OFFENSIVE MODE (YOU are hitting a monster!)
            // =========================================================================
            else if (!__instance.IsPlayer())
            {
                // 1. Calculate how many players are nearby to determine monster HP group scaling (+30% per player)
                float rawScaleFactor = HelperFunctions.GetMultiplayerScaleFactor(localPlayer);

                // Reconstruct player count based on our damage per player scale math helper
                int extraPlayers = Mathf.RoundToInt((rawScaleFactor - 1.0f) / (DynamicParryScalingPlugin.ConfigDamagePerPlayer.Value / 100f));

                // Calculate the true vanilla monster HP modifier (+30% per extra player)
                float monsterHpScalingFactor = 1.0f + (extraPlayers * (DynamicParryScalingPlugin.ConfigHpBonusPerPlayerPercent.Value / 100f));

                float currentWorldDifficulty = DynamicParryScalingPlugin.ConfigPlayerDamageMult.Value;
                float attackTargetDifficulty = DynamicParryScalingPlugin.ConfigAttackTargetDifficulty.Value;

                // Skip if no modifiers apply to your attack sequence
                //if (monsterHpScalingFactor <= 1.0f && currentWorldDifficulty <= attackTargetDifficulty) return;

                float originalDamage = damage;

                // --- THE OFFENSIVE MATHEMATICAL ENGINE ---
                float modifiedAttackStagger = (damage * attackTargetDifficulty) / currentWorldDifficulty * monsterHpScalingFactor;
                float attackScaleModifier = (attackTargetDifficulty)/ currentWorldDifficulty * monsterHpScalingFactor;

                // FIX: Dynamically extract the exact native limit percentage directly from the monster instance!
                float staggerFactor = __instance.m_staggerDamageFactor;
                if (staggerFactor <= 0f) staggerFactor = 0.3f; // Absolute safe fallback if unassigned

                // Calculate the true maximum posture limit using the engine variable (e.g., MaxHealth * 0.5 for Brutes)
                float monsterMaxStaggerLimit = __instance.GetMaxHealth() * staggerFactor;
                if (monsterMaxStaggerLimit <= 0f) monsterMaxStaggerLimit = 1.0f;

                // Standard, sequential inline math for precise rounding
                float modstaggerIncrease = (float)(modifiedAttackStagger / monsterMaxStaggerLimit * 100f);
                float vanillastaggerIncrease = (float)(originalDamage / monsterMaxStaggerLimit * 100f);

                HelperFunctions.LogToF5Console($"[StaggerScaler ATTACK] Hitting monster '{__instance.m_name} (m_staggerDamageFactor: {__instance.m_staggerDamageFactor:F1}) '. Stagger dealt: vanilla {originalDamage:F1} -> modded {modifiedAttackStagger:F1}; scaleFactor:{attackScaleModifier:F2};\n" +
                    $" Modded enemy stagger: +{modstaggerIncrease:F0}%; Vanilla enemy stagger: +{vanillastaggerIncrease:F0}%");

                if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableAttackScaling.Value)
                {
                    damage = modifiedAttackStagger;
                }
            }
        }
    }
}
