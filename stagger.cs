using HarmonyLib;
using UnityEngine;
using Valheim.SettingsGui;
using System.Runtime.CompilerServices;

namespace DynamicParryScaling
{
    // Thread-safe instance maps to cleanly isolate data between multiple players in multiplayer
    public static class DamageContext
    {
        // Tracks cumulative combat log strings assigned strictly to individual player instances
        public static readonly ConditionalWeakTable<Player, string> LogTracker = new ConditionalWeakTable<Player, string>();

        // Tracks which Character instance is currently attacking a specific victim (Player or Monster)
        public static readonly ConditionalWeakTable<Character, Character> AttackerTracker = new ConditionalWeakTable<Character, Character>();

        // Thread-safe trackers mapping blocking and parry statuses safely to specific individual player entities
        public static readonly ConditionalWeakTable<Player, StrongBox<bool>> IsBlockingTracker = new ConditionalWeakTable<Player, StrongBox<bool>>();
        public static readonly ConditionalWeakTable<Player, StrongBox<bool>> CanParryTracker = new ConditionalWeakTable<Player, StrongBox<bool>>();

        // Tracks whether the shield phase has already run for a specific player instance on this frame
        public static readonly ConditionalWeakTable<Player, StrongBox<bool>> ShieldProcessedTracker = new ConditionalWeakTable<Player, StrongBox<bool>>();
    }

    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    public static class Humanoid_BlockAttack_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Humanoid __instance, HitData hit, Character attacker, float ___m_blockTimer)
        {
            if (__instance != null && __instance.IsPlayer() && attacker != null) 
            {
                Player player = __instance as Player; 
                if (player == null) return; 

                ItemDrop.ItemData leftHandItem = player.LeftItem; 
                ItemDrop.ItemData rightHandItem = player.RightItem; 
                ItemDrop.ItemData weaponUsedToBlock = null; 

                if (leftHandItem != null && leftHandItem.m_shared.m_blockPower > 0f) 
                {
                    weaponUsedToBlock = leftHandItem; 
                }
                else if (rightHandItem != null && rightHandItem.m_shared.m_blockPower > 0f) 
                {
                    weaponUsedToBlock = rightHandItem; 
                }

                if (weaponUsedToBlock != null) 
                {
                    bool isPerfectTimed = ___m_blockTimer != -1f && ___m_blockTimer < 0.25f; 
                    bool hasParryMultiplier = weaponUsedToBlock.m_shared.m_timedBlockBonus > 1f; 

                    var blockingBox = DamageContext.IsBlockingTracker.GetOrCreateValue(player); 
                    blockingBox.Value = true; 

                    // Reset the shield processed execution tracker for this fresh hit sequence
                    var processedBox = DamageContext.ShieldProcessedTracker.GetOrCreateValue(player);
                    processedBox.Value = false;

                    if (isPerfectTimed && hasParryMultiplier) 
                    {
                        var parryBox = DamageContext.CanParryTracker.GetOrCreateValue(player); 
                        parryBox.Value = true; 
                    }
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance)
        {
            if (__instance is Player player) 
            {
                var blockingBox = DamageContext.IsBlockingTracker.GetOrCreateValue(player); 
                blockingBox.Value = false; 

                var parryBox = DamageContext.CanParryTracker.GetOrCreateValue(player); 
                parryBox.Value = false; 
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class Patch_Character_ApplyDamage
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (__instance != null && hit != null) 
            {
                Character attacker = hit.GetAttacker(); 
                if (attacker != null) 
                {
                    DamageContext.AttackerTracker.Remove(__instance); 
                    DamageContext.AttackerTracker.Add(__instance, attacker); 
                }
            }
        }
    }

    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    public static class Patch_AddStaggerDamage
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, ref float damage, Vector3 forceDirection)
        {
            if (__instance == null) return; 

            DamageContext.AttackerTracker.TryGetValue(__instance, out Character attacker); 

            bool victimIsMonster = !__instance.IsPlayer(); 
            bool attackerIsMonster = attacker != null && !attacker.IsPlayer(); 

            if (victimIsMonster && attackerIsMonster) 
            {
                HelperFunctions.LogToF5Console($"[StaggerScaler] Mob-on-Mob violence detected! {attacker?.m_name ?? "Unknown mob"} hit {__instance.m_name}\n"); 
                return;
            }

            // =========================================================================
            // CATEGORY A: DEFENSIVE MODE (The hit is hitting YOU, the player)
            // =========================================================================
            if (__instance is Player player) 
            {
                if (attacker != null && !attackerIsMonster) return; 

                string realPlayerName = player.GetPlayerName(); 
                float nearbyPlayersCount = HelperFunctions.GetNearbyPlayersCount(player); 
                float MPEnemyDamageScalingFactor = nearbyPlayersCount * Game.instance.m_damageScalePerPlayer + 1.0f; 
                float DamageReduceFactor = (MPEnemyDamageScalingFactor * Game.m_enemyDamageRate) / DynamicParryScalingPlugin.ConfigDesiredEnemyDamageMult.Value; 

                DamageContext.IsBlockingTracker.TryGetValue(player, out var blockingBox); 
                DamageContext.ShieldProcessedTracker.TryGetValue(player, out var processedBox);

                bool alreadyProcessedShield = processedBox != null && processedBox.Value;
                bool isShieldPhase = blockingBox != null && blockingBox.Value && !alreadyProcessedShield;

                if (isShieldPhase) 
                {
                    if (processedBox != null) processedBox.Value = true;

                    DamageContext.CanParryTracker.TryGetValue(player, out var parryBox); 
                    bool isParry = player.HaveStamina() && parryBox != null && parryBox.Value; 

                    float block = isParry 
                        ? HelperFunctions.GetEquippedMaxParryBlock(player) 
                        : HelperFunctions.GetEquippedMaxBlock(player); 

                    float revHit = damage < block 
                        ? Mathf.Sqrt(damage * 4.0f * block) 
                        : damage + block; 

                    float soloPureDmg = revHit / DamageReduceFactor; 
                    float reducedByShieldDmg = block >= soloPureDmg / 2.0f 
                        ? (soloPureDmg * soloPureDmg) / (4.0f * block) 
                        : Mathf.Max(0f, soloPureDmg - block); 

                    float modstaggerIncrease = (reducedByShieldDmg / (player.GetMaxHealth() * 0.4f)) * 100f; 
                    float vanillastaggerIncrease = (damage / (player.GetMaxHealth() * 0.4f)) * 100f; 

                    string shieldLog = $"[StaggerScaler] [{realPlayerName}] <Is BLOCKING/PARRYING> Passed through shield dmg: {damage:F1}; Pure full hit dmg (without shield) must be: {revHit:F1};\n" + 
                        $"DamageMult multiplayer*difficulty = {DamageReduceFactor:F2}; IsParry (otherwise block): {isParry}; Shield block: {block:F1}\n" + 
                        $"Desired pure dmg based on your target settings must be: {soloPureDmg:F2}; Finally reduced by shield dmg applied to stagger: {reducedByShieldDmg:F2}\n" + 
                        $"Vanilla stagger percentage increase: +{vanillastaggerIncrease:F0}% -> modded: +{modstaggerIncrease:F0}%\n" +
                        $"===========================================\n";

                    DamageContext.LogTracker.Remove(player); 
                    DamageContext.LogTracker.Add(player, shieldLog); 

                    if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableShieldScaling.Value) 
                    {
                        damage = reducedByShieldDmg; 
                    }
                }
                else 
                {
                    // PASSIVE ARMOR PATH
                    float bodyArmor = player.GetBodyArmor(); 

                    // SUCCESSIVE INPUT COUPLING: By using the active 'damage' parameter variable directly here, 
                    // if the shield altered it upstream, the armor phase naturally inherits the scaled bleed-through!
                    float revHit = damage < bodyArmor 
                        ? Mathf.Sqrt(damage * 4.0f * bodyArmor) 
                        : damage + bodyArmor; 

                    float reducedHit = revHit / DamageReduceFactor; 
                    float armorReducedDmg = bodyArmor >= reducedHit / 2.0f 
? (reducedHit * reducedHit) / (4.0f * bodyArmor) 
: Mathf.Max(0f, reducedHit - bodyArmor); 
                    float modstaggerIncrease = (armorReducedDmg / (player.GetMaxHealth() * 0.4f)) * 100f; 
                    float vanillastaggerIncrease = (damage / (player.GetMaxHealth() * 0.4f)) * 100f; 
                    DamageContext.LogTracker.TryGetValue(player, out string existingLog); 
                                                                                          // Concatenate the armor data thread-safely behind the active shield string record entry
                    string combinedLog = (existingLog ?? "") +
                        $"[StaggerScaler] [{realPlayerName}]  Passed through armor dmg: {damage:F1}; Pure full hit dmg (without armor) must be: {revHit:F1};\n" + 
                        $"DamageMult multiplayer*difficulty = {DamageReduceFactor:F2}; Body Armor: {bodyArmor:F1};\n" + 
                        $"Desired pure dmg based on your target settings must be: {reducedHit:F2}; Finally reduced by armor dmg applied to stagger: {armorReducedDmg:F2}\n" + 
                        $"Vanilla stagger percentage increase: +{vanillastaggerIncrease:F0}% -> modded: +{modstaggerIncrease:F0}%\n" +
                        $"===========================================\n"; 
                    DamageContext.LogTracker.Remove(player); 
                    DamageContext.LogTracker.Add(player, combinedLog); 
                    if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableArmorScaling.Value) 
                    {
                        damage = armorReducedDmg; 
                    }
                }
            }
            // =========================================================================
            // CATEGORY B: OFFENSIVE MODE (YOU are hitting a monster!)
            // =========================================================================
            else if (victimIsMonster && attacker is Player attackerplayer) 
            {
                float MobHPScaleFactor = HelperFunctions.GetNearbyPlayersCount(attackerplayer) * Game.instance.m_healthScalePerPlayer + 1.0f; 
                float playerAttackScaleFactor = 1.0f * MobHPScaleFactor / Game.m_playerDamageRate; 
                float soloDamage = damage * playerAttackScaleFactor; 
                float mobStaggerFactor = __instance.m_staggerDamageFactor; 
                if (mobStaggerFactor <= 0f) mobStaggerFactor = 0.3f; 
                float monsterMaxStaggerLimit = __instance.GetMaxHealth() * mobStaggerFactor; 
                if (monsterMaxStaggerLimit <= 0f) monsterMaxStaggerLimit = 1.0f; 
                float modstaggerIncrease = (float)(soloDamage / monsterMaxStaggerLimit * 100f); 
                float vanillastaggerIncrease = (float)(damage / monsterMaxStaggerLimit * 100f); 
                HelperFunctions.LogToF5Console($"[StaggerScaler] Hitting monster '{__instance.m_name} (mob stagger bar mult: {__instance.m_staggerDamageFactor:F1}) '. Stagger dealt: vanilla {damage:F1} -> modded {soloDamage:F1}; scaleFactor:{playerAttackScaleFactor:F2};\n" + 
                $" Modded enemy stagger: +{modstaggerIncrease:F0}%; Vanilla enemy stagger: +{vanillastaggerIncrease:F0}%\n");
                if (DynamicParryScalingPlugin.ConfigGlobalSwitch.Value && DynamicParryScalingPlugin.ConfigEnableAttackScaling.Value) 
                {
                    damage = soloDamage; 
                }
            }
        }
        [HarmonyPostfix]
        public static void Postfix(Character __instance)
        {
            if (__instance is Player player) 
            {
                string realPlayerName = player.GetPlayerName();
                if (DamageContext.LogTracker.TryGetValue(player, out string fullPlayerLog)) 
                {
                    // Print the entire unified string block along with the true final post-calculations fill rating!
                    HelperFunctions.LogToF5Console(fullPlayerLog + $"Current stagger bar fill for {realPlayerName}: {(player.GetStaggerPercentage() * 100f):F0}%. Calc DONE.\n"); 
                    DamageContext.LogTracker.Remove(player); 
                }
                DamageContext.ShieldProcessedTracker.Remove(player);
            }
            if (__instance != null) 
            {
                DamageContext.AttackerTracker.Remove(__instance); 
            }
        }
    }
}
