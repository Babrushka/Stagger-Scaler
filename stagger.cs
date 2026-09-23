using HarmonyLib;
using UnityEngine;
using Valheim.SettingsGui;

namespace DynamicParryScaling
{
    // A thread-safe way to store who the current attacker is during a damage sequence
    public static class DamageContext
    {
        public static Character CurrentAttacker = null;
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class Patch_Character_ApplyDamage
    {
        [HarmonyPrefix]
        public static void Prefix(HitData hit)
        {
            // Store the attacker context before damage and stagger calculations run
            DamageContext.CurrentAttacker = hit.GetAttacker();
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            // Clear it out immediately after to prevent memory leaks or false flags
            DamageContext.CurrentAttacker = null;
        }
    }
    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    public static class Patch_AddStaggerDamage
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, ref float damage, Vector3 forceDirection)
        {
            if (__instance == null) return;

            // Get local player reference to compute proximity rules
            //Player localPlayer = Player.m_localPlayer;
            //if (localPlayer == null || localPlayer.IsDead()) return;

            if (__instance == null) return;

            // Retrieve the attacker from our context patch
            Character attacker = DamageContext.CurrentAttacker;

            // =========================================================================
            // CHECK: Is the victim a monster AND was it hit by another monster?
            // =========================================================================
            bool victimIsMonster = !__instance.IsPlayer();
            bool attackerIsMonster = attacker != null && !attacker.IsPlayer();

            if (victimIsMonster && attackerIsMonster)
            {
                // This logic triggers ONLY when a mob hits another mob (e.g., Factions fighting)
                HelperFunctions.LogToF5Console($"[StaggerScaler] Mob-on-Mob violence detected! {attacker.m_name} hit {__instance.m_name}");
                return; 
            }


            // =========================================================================
            // CATEGORY A: DEFENSIVE MODE (The hit is hitting YOU, the player)
            // =========================================================================
            if (__instance is Player player)
            {
                float nearbyPlayersCount = HelperFunctions.GetNearbyPlayersCount(player);
                bool isBlocking = player.IsBlocking();
                float MPEnemyDamageScalingFactor = HelperFunctions.GetNearbyPlayersCount(player) * Game.instance.m_damageScalePerPlayer + 1.0f;
                
                int k = Game.instance.GetPlayerDifficulty(player.transform.position);

                float parryReduceFactor = (MPEnemyDamageScalingFactor / Game.m_enemyDamageRate) * DynamicParryScalingPlugin.ConfigDesiredEnemyDamageMult.Value;

                float soloStagger = damage / parryReduceFactor;

                float modstaggerIncrease = (float)(soloStagger / (player.GetMaxHealth() * 0.4f) * 100f);
                float vanillastaggerIncrease = (float)(damage / (player.GetMaxHealth() * 0.4f) * 100f);

                HelperFunctions.LogToF5Console($"[StaggerScaler] <Defence coefs> MPEnemyDamageScalingFactor: {MPEnemyDamageScalingFactor:F2}; Enemy Damage Mult (combat setting): {Game.m_enemyDamageRate:F2}; Desired Enemy Dmg Mult: {DynamicParryScalingPlugin.ConfigDesiredEnemyDamageMult.Value:F2}.");

                if (isBlocking)
                {
                    
                    HelperFunctions.LogToF5Console($"[StaggerScaler] <Is BLOCKING> Incoming values: vanilla {damage:F1} -> modded {soloStagger:F1}; scaleFactor:{parryReduceFactor:F3};\n" +
                        $" Modded stagger increase: +{modstaggerIncrease:F0}%; Vanilla stagger increase: +{vanillastaggerIncrease:F0}%");

                    if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableShieldScaling.Value)
                    {
                        damage = soloStagger;
                    }
                }
                else
                {
                    HelperFunctions.LogToF5Console($"[StaggerScaler] <Passive ARMOR> Incoming values: vanilla {damage:F1} -> modded {soloStagger:F1}; scaleFactor:{parryReduceFactor:F3};\n" +
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
            else if (!__instance.IsPlayer() && attacker is Player attackerplayer)
            {
                float MobHPScaleFactor = HelperFunctions.GetNearbyPlayersCount(attackerplayer)*Game.instance.m_healthScalePerPlayer+1.0f;
                float playerAttackScaleFactor = 1.0f * MobHPScaleFactor / Game.m_playerDamageRate;
                float soloDamage = damage*playerAttackScaleFactor;

                float mobStaggerFactor = __instance.m_staggerDamageFactor;
                if (mobStaggerFactor <= 0f) mobStaggerFactor = 0.3f; // Absolute safe fallback if unassigned

                // Calculate the true maximum posture limit using the engine variable (e.g., MaxHealth * 0.5 for Brutes)
                float monsterMaxStaggerLimit = __instance.GetMaxHealth() * mobStaggerFactor;
                if (monsterMaxStaggerLimit <= 0f) monsterMaxStaggerLimit = 1.0f;

                /// Standard, sequential inline math for precise rounding
                float modstaggerIncrease = (float)(soloDamage / monsterMaxStaggerLimit * 100f);
                float vanillastaggerIncrease = (float)(damage / monsterMaxStaggerLimit * 100f);
                HelperFunctions.LogToF5Console($"[StaggerScaler] <Attack coefs> MobHPScaleFactor: {MobHPScaleFactor:F2}; Game settings player damage mult: {Game.m_playerDamageRate:F2}; ");

                HelperFunctions.LogToF5Console($"[StaggerScaler] <Player ATTACK> Hitting monster '{__instance.m_name} (mob stagger bar mult (based on max mob's HP): {__instance.m_staggerDamageFactor:F1}) '. Stagger dealt: vanilla {damage:F1} -> modded {soloDamage:F1}; scaleFactor:{playerAttackScaleFactor:F2};\n" +
                   $" Modded enemy stagger: +{modstaggerIncrease:F0}%; Vanilla enemy stagger: +{vanillastaggerIncrease:F0}%");

                if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableAttackScaling.Value)
                {
                    damage = soloDamage;
                }
            }
        }
    }
}
