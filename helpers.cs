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

        public static float GetMultiplayerScaleFactor(Player player)
        {
            float nearbyPlayersCount = 0;

            if (DynamicParryScalingPlugin.ConfigSimulatedPlayers.Value > 0)
            {
                nearbyPlayersCount = DynamicParryScalingPlugin.ConfigSimulatedPlayers.Value;
            }
            else
            {
                float radius = DynamicParryScalingPlugin.ConfigCheckRadius.Value;
                List<Player> allPlayers = Player.GetAllPlayers();

                foreach (Player otherPlayer in allPlayers)
                {
                    if (otherPlayer != player && !otherPlayer.IsDead())
                    {
                        Vector3 pos1 = player.transform.position;
                        Vector3 pos2 = otherPlayer.transform.position;
                        pos1.y = 0f;
                        pos2.y = 0f;

                        if (Vector3.Distance(pos1, pos2) <= radius)
                        {
                            nearbyPlayersCount++;
                        }
                    }
                }
            }

            if (nearbyPlayersCount > 4) nearbyPlayersCount = 4;

            float percentModifier = DynamicParryScalingPlugin.ConfigDamagePerPlayer.Value / 100f;
            return 1.0f + (nearbyPlayersCount * percentModifier);
        }
    }
}
