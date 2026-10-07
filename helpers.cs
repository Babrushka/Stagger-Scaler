using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static UnityEngine.GraphicsBuffer;
using HarmonyLib;

namespace StaggerScaler
{
    public static class DamageContext
    {
        // Tracks cumulative combat log strings assigned strictly to individual player instances
        public static readonly ConditionalWeakTable<Character, string> LogTracker = new ConditionalWeakTable<Character, string>();

        /// <summary>
        /// Tracks Hit Data. 
        /// <para><b>Key:</b> Player <see cref="Character"/>.</para>
        /// <para><b>Value:</b> HitData itself <see cref="Character"/>.</para>
        /// </summary>
        public static readonly ConditionalWeakTable<Character, HitData> hitTracker = new ConditionalWeakTable<Character, HitData>();


        /// <summary>
        /// Stagger value (Block path). 
        /// <para><b>Key:</b> Player <see cref="Character"/>.</para>
        /// <para><b>Value:</b> totalStaggerDamage <see cref="StrongBox<float>"/>.</para>
        /// </summary>
        public static readonly ConditionalWeakTable<Character, StrongBox<float>> staggerBlockTracker = new ConditionalWeakTable<Character, StrongBox<float>>();

        /// <summary>
        /// Stagger value (Armor path). 
        /// <para><b>Key:</b> Player <see cref="Character"/>.</para>
        /// <para><b>Value:</b> totalStaggerDamage <see cref="StrongBox<float>"/>.</para>
        /// </summary>
        public static readonly ConditionalWeakTable<Character, StrongBox<float>> staggerArmorTracker = new ConditionalWeakTable<Character, StrongBox<float>>();

        /// <summary>
        /// Stagger value. 
        /// <para><b>Key:</b> Player <see cref="Character"/>.</para>
        /// <para><b>Value:</b> BlockedDamage <see cref="StrongBox<float>"/>.</para>
        /// </summary>
        public static readonly ConditionalWeakTable<Character, StrongBox<float>> blockedDamageTracker = new ConditionalWeakTable<Character, StrongBox<float>>();
    }
    public static class HelperFunctions
    {
        private const string RpcEventName = "StaggerScaler_ReceiveServerLog";
        private static bool _rpcRegistered = false;

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        public static class ZNetScene_Awake_Patch
        {
            public static void Postfix()
            {
                // Fires safely when the networking scene and routing hashes are ready
                HelperFunctions.InitNetworkHandlers();
            }
        }
        public static void InitNetworkHandlers()
        {
            if (_rpcRegistered) return;
            
            // Clients register this listener to print any message sent directly to them
            ZRoutedRpc.instance.Register<string>(RpcEventName, (sender, message) =>
            {
                if (ZNet.instance != null && ZNet.instance.IsServer() )
                {
                    if (StaggerScalerPlugin.ConfigEnableServerDebugLogs.Value)
                    {
                        LogToF5Console(message);
                    }
                }
                else
                {
                    if (StaggerScalerPlugin.ConfigEnableClientDebugLogs.Value)
                    {
                        LogToF5Console(message);
                    }
                }
                
            });

            _rpcRegistered = true;
        }

        public static void LogToF5Console(Character player, string message)
        {
            //if (!StaggerScalerPlugin.ConfigEnableDebugLogs.Value) return;

            // 1. Are we running on the Server (Dedicated or Multiplayer Host)?
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                // Is the target player a remote client?
                if (player.GetZDOID().UserID != ZNet.GetUID())
                {

                    // DEDICATED/COOP SERVER logging remote client action.
                    if (StaggerScalerPlugin.ConfigEnableServerDebugLogs.Value)
                    {
                        LogToF5Console(message);
                    }

                    //Send to the remote client  his action log over the network
                    if (StaggerScalerPlugin.ConfigEnableBroadcastLogs.Value)
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(player.GetZDOID().UserID, RpcEventName, message);
                    }

                }
                else
                {
                    // HOST SIDE (Local server): The target player is the host themselves.
                    if (StaggerScalerPlugin.ConfigEnableClientDebugLogs.Value || StaggerScalerPlugin.ConfigEnableServerDebugLogs.Value)
                    {
                        LogToF5Console(message);
                    }

                }

            }
            //client is a remote client, 100% mp mode.
            else
            {
                //print local log.
                if (StaggerScalerPlugin.ConfigEnableClientDebugLogs.Value)
                {
                    LogToF5Console(message);
                }
                //ZRoutedRpc.instance.InvokeRoutedRPC(RpcEventName, "3.1" + message);
                ZRoutedRpc.instance.InvokeRoutedRPC(ZNet.instance.GetServerPeer().m_uid, RpcEventName, message);
                
                
                if (!player.IsOwner())  //idk why IsOwner is true while host is other client. even if it is host-peer (server for a certain area), not GAME HOST itslef.
                {
                    //broadcast log 
                    if (StaggerScalerPlugin.ConfigEnableBroadcastLogs.Value)
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(player.GetZDOID().UserID, RpcEventName, "4" + message);
                    }
                }
            }

        }

        public static void LogToF5Console(string message)
        {
            if (Console.instance != null)
            {
                // 1. This prints to the F5 overlay.
                // 2. BepInEx automatically copies this to your terminal and log file.
                Console.instance.Print(message);
            }
            else if (ZNet.instance != null && ZNet.instance.IsServer() && StaggerScalerPlugin.Log != null)
            {
                // FALLBACK: If there is no UI console (like on a dedicated server), 
                // manually push it to BepInEx so the host logs don't disappear.
                StaggerScalerPlugin.Log.LogInfo(message);
            }
        }

        public static void attackLog(Character p, float MPscale, float GameScale, string mobName, float mobStaggerFactor, float oldDmg, float newDmg, float vanillaStagger, float moddedStagger){
            string s = $"[StaggerScaler] [[{((Player)p).GetPlayerName()}]] <Attack> adds stagger to {mobName} (mob stagger bar is {mobStaggerFactor} of max hp)\n" +
                       $"Vanilla stagger: {oldDmg}, modded stagger: {newDmg}\n" +
                       $"Multiplayer coef (increased by: Game coef/Applied target coef: {MPscale}, difficulty coef: {GameScale};\n" +
                       $"Stagger bar: vailla +{vanillaStagger:F0}% ---> modded +{moddedStagger:F0}%.\n" +
                       $"Current (after the hit) {mobName}'s stagger is: currentStagger%.\n" + 
                       $"=========================DONE==============================\n";
            DamageContext.LogTracker.AddOrUpdate(p, s);
        }

        public static void blockLog(Character p, float totalVanillaDmg, float totalModdedDmg, float shieldStr, float blockableDmg, float newDmg, float moddedStagger)
        {
            string s = $"[StaggerScaler] [[{((Player)p).GetPlayerName()}]] <Block/Parry> Equipped item block power: {shieldStr:F1}; modded blocked damage: {blockableDmg:F1}\n" +
                       $"<Target multipliers phase> Incoming vanilla full damage: {totalVanillaDmg:F2} ---> moded (rollback to target settings): {totalModdedDmg:F2};\n" + 
                       $"<Values after applying block power> Vanilla stagger: vanillaStaggerDamage, modded stagger: {newDmg}\n" +
                       $"Stagger bar: vanilla +vanillaStagger% ---> moded +{moddedStagger:F0}%.\n" +
                       $"++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++\n";
            DamageContext.LogTracker.AddOrUpdate(p, s);
        }

        public static void armorLog(Character p, float totalVanillaDmg, float totalModdedDmg, float armor, float newDmg, float MPscale, float GameScale, float moddedStagger)
        {
            string old = DamageContext.LogTracker.GetValue(p, k => string.Empty);

            string s = $"[StaggerScaler] [[{((Player)p).GetPlayerName()}]] <Armor> Equipped armor: {armor:F1}\n" +
                       $"<Virtual deapply armor rollback> Incoming vanilla damage (applied resists): {totalVanillaDmg:F2} ---> moded (mod target settings + modded shield impact + armor resist): {totalModdedDmg:F2};\n" +
                       $"<Apply armor> Vanilla stagger: vanillaStaggerDamage, modded stagger: {newDmg}\n" +
                       $"Stagger bar: vanilla +vanillaStagger% ---> modded +{moddedStagger:F0}%.\n" +
                       $"------------------------------------------------------------\n" +
                       $"Multiplayer coef (reducing by: Applied target/Game coef): {MPscale}, difficulty coef (TargetSettings/GameSettings): {GameScale};\n" +
                       $"Current stagger: currentStagger%\n" +
                       $"=========================DONE==============================\n";
            DamageContext.LogTracker.AddOrUpdate(p, old+s);
        }



        public static void updateStagger(Character p, float vanillaDamage, float vanillaStagger) 
        {
            if (DamageContext.LogTracker.TryGetValue(p, out var s))
            {
                s = s.Replace("vanillaStaggerDamage", $"{vanillaDamage}");
                s = s.Replace("vanillaStagger", $"{vanillaStagger:F0}");
                DamageContext.LogTracker.AddOrUpdate(p, s);
            }  
        }

        public static float reverseDamage(float damage, float armor)
        {
            return damage < armor ? Mathf.Sqrt(damage * 4.0f * armor) : damage + armor;
        }

        public static float GetNearbyPlayersCount(Player player)
        {
            float nearbyPlayersCount = 0;
            //Game.instance.GetDifficultyDamageScalePlayer(base.transform.position);
            nearbyPlayersCount = Game.instance.GetPlayerDifficulty(player.transform.position) - 1.0f;
            if (StaggerScalerPlugin.ConfigSimulatedPlayers.Value > 0) nearbyPlayersCount = StaggerScalerPlugin.ConfigSimulatedPlayers.Value;
            if (nearbyPlayersCount > 4) nearbyPlayersCount = 4.0f;

            return nearbyPlayersCount;
        }

        public static ItemDrop.ItemData GetCurrentBlocker(Humanoid __instance)
        {
            if (__instance.LeftItem != null)
            {
                return __instance.LeftItem;
            }

            return __instance.GetCurrentWeapon();

        }
    }
}
