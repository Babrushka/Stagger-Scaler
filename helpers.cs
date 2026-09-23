using BepInEx;
using UnityEngine;
using System.Collections.Generic;

namespace DynamicParryScaling
{
    public static class HelperFunctions
    {
        public static void LogToF5Console(string message)
        {
            if (!DynamicParryScalingPlugin.ConfigEnableDebugLogs.Value) return;

            // FIX: Uses the newly added global warning-free static logger bridge
            if (DynamicParryScalingPlugin.Log != null)
            {
                DynamicParryScalingPlugin.Log.LogInfo(message);
            }

            if (Console.instance != null)
            {
                Console.instance.Print(message);
            }
        }

        public static float GetNearbyPlayersCount(Player player)
        {
            float nearbyPlayersCount = 0;

            nearbyPlayersCount = Game.instance.GetPlayerDifficulty(player.transform.position) - 1.0f;
            if (DynamicParryScalingPlugin.ConfigSimulatedPlayers.Value > 0) nearbyPlayersCount = DynamicParryScalingPlugin.ConfigSimulatedPlayers.Value;
            if (nearbyPlayersCount > 4) nearbyPlayersCount = 4.0f;

            return nearbyPlayersCount;
        }
        public static float GetEquippedMaxBlock(Player player)
        {
            if (player == null) return 0f;

            // Correct properties for accessing equipped items
            ItemDrop.ItemData leftItem = player.LeftItem;
            ItemDrop.ItemData rightItem = player.RightItem;

            float skillFactor = player.GetSkillFactor(Skills.SkillType.Blocking);

            // 1. Check Shield (Left hand)
            if (leftItem != null && leftItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                return leftItem.GetBlockPower(leftItem.m_quality, skillFactor);
            }

            // 2. Check 2-Handed Weapon (Usually sits in right hand slot, left hand is null)
            if (rightItem != null && rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
            {
                return rightItem.GetBlockPower(rightItem.m_quality, skillFactor);
            }

            // 3. Check 1-Handed Weapon fallback
            if (rightItem != null && rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
            {
                return rightItem.GetBlockPower(rightItem.m_quality, skillFactor);
            }

            return 0f;
        }

        // Returns the fully scaled block power IF a parry is successfully triggered
        public static float GetEquippedMaxParryBlock(Player player)
        {
            if (player == null) return 0f;

            ItemDrop.ItemData leftItem = player.LeftItem;
            ItemDrop.ItemData rightItem = player.RightItem;

            float baseBlock = GetEquippedMaxBlock(player);
            if (baseBlock <= 0f) return 0f;

            // Apply parry multiplier based on what is actively being used to block
            if (leftItem != null && leftItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                return baseBlock * leftItem.m_shared.m_timedBlockBonus;
            }

            if (rightItem != null && (rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                                      rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon))
            {
                return baseBlock * rightItem.m_shared.m_timedBlockBonus;
            }

            return baseBlock;
        }
    }
}
