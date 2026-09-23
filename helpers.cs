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
    }
}
