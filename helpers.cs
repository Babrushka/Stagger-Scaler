using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static UnityEngine.GraphicsBuffer;

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

        public static void InitNetworkHandlers()
        {
            if (_rpcRegistered) return;

            // Clients register this listener to print any message sent directly to them
            ZRoutedRpc.instance.Register<string>(RpcEventName, (sender, message) =>
            {
                LogToF5Console(message);
            });

            _rpcRegistered = true;
        }

        private static long GetCharacterPeerID(Character character)
        {
            if (character == null) return 0L;

            ZNetView nview = character.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                // Fetches the data controller and returns the owner UID
                return nview.GetZDO().GetOwner();
            }

            return 0L;
        }

        public static void LogToF5Console(Character player, string message)
        {
            if (!StaggerScalerPlugin.ConfigEnableDebugLogs.Value) return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                // IN SP: targetPeerID matches your local UID, executing instantly.
                // IN MP: Sends the payload securely over the socket ONLY to that peer ID.
                ZRoutedRpc.instance.InvokeRoutedRPC(player.GetOwner(), RpcEventName, message);
            }
            else
            {
                // Fallback for purely local execution paths
                LogToF5Console(message);
            }
        }
        public static void LogToF5Console(string message)
        {
            if (!StaggerScalerPlugin.ConfigEnableDebugLogs.Value) return;

            // FIX: Uses the newly added global warning-free static logger bridge
            if (StaggerScalerPlugin.Log != null)
            {
                StaggerScalerPlugin.Log.LogInfo(message);
            }

            if (Console.instance != null)
            {
                Console.instance.Print(message);
            }
        }

        public static void attackLog(Character p, float MPscale, float GameScale, string mobName, float mobStaggerFactor, float oldDmg, float newDmg, float vanillaStagger, float moddedStagger){
            string s = $"[StaggerScaler] [[{((Player)p).GetPlayerName()}]] <Attack> adds stagger to {mobName} (mob stagger bar is {mobStaggerFactor} of max hp)\n" +
                       $"Vanilla stagger: {oldDmg}, modded stagger: {newDmg}\n" +
                       $"Multiplayer coef (increased by: Game coef/Applied target coef: {MPscale}, difficulty coef: {GameScale};\n" +
                       $"Stagger bar: vailla +{vanillaStagger:F0}% ---> modded +{moddedStagger:F0}%.\n" +
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
