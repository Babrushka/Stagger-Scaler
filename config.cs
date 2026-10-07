using System.Diagnostics;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
// REMOVED: using System; to fix the abstract 'Version' type collision

namespace StaggerScaler
{
    [BepInPlugin("babrushkas.staggerscaler", "Stagger Scaler", "1.8")]
    [BepInDependency(ConditionalConfigSyncAPI.ConfigSync.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class StaggerScalerPlugin : BaseUnityPlugin
    {
        // --- Configuration Entries ---
        public static ConfigEntry<float> ConfigDesiredPlayerDamageMult;
        public static ConfigEntry<float> ConfigDesiredEnemyDamageMult;
        public static ConfigEntry<float> ConfigDesiredEnemyDamageMP;
        public static ConfigEntry<float> ConfigDesiredEnemyHealthMP;
        public static ConfigEntry<bool> ConfigEnableClientDebugLogs;
        public static ConfigEntry<bool> ConfigEnableServerDebugLogs;
        public static ConfigEntry<bool> ConfigEnableBroadcastLogs;
        public static ConfigEntry<bool> ConfigGlobalSwitch;
        public static ConfigEntry<float> ConfigSimulatedPlayers;
        public static ConfigEntry<bool> ConfigEnableShieldScaling;
        public static ConfigEntry<bool> ConfigEnableArmorScaling;
        public static ConfigEntry<bool> ConfigEnableAttackScaling;

        public static StaggerScalerPlugin Instance;
        public static ManualLogSource Log;

        private static Harmony _harmonyInstance;

        // The compiler now successfully reads these string inputs without naming conflicts
        internal static readonly ConditionalConfigSyncAPI.ConfigSync configSync =
            new ConditionalConfigSyncAPI.ConfigSync("babrushkas.staggerscaler", "StaggerScaler", "1.8", minimumRequiredVersion: "1.8", modRequired: true);

        private void Awake()
        {
            Instance = this;
            Log = base.Logger;
           
            UnityEngine.Debug.developerConsoleVisible = false;
            UnityEngine.Debug.developerConsoleEnabled = false;

            ConfigGlobalSwitch = configSync.Bind(Config, "General", "GlobalSwitch", true,
                "Enables or disables applying modifiers to stagger values globally.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableShieldScaling = configSync.Bind(Config, "General", "EnableShieldScaling", true,
                "Enables stagger scaling reduction while you are actively raising your shield/weapon to block.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableArmorScaling = configSync.Bind(Config, "General", "EnableArmorScaling", true,
                "Enables passive stagger scaling protection when your shield is NOT up.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableAttackScaling = configSync.Bind(Config, "General", "EnableAttackScaling", true,
                "Enables offensive stagger scaling, making your attacks fill enemy stagger bars faster or slower based on config.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigDesiredEnemyDamageMult = configSync.Bind(Config, "World Difficulty Targets", "Desired enemy damage multiplier", 1.0f,
                new ConfigDescription("The difficulty tier you WANT your shield blocks and stagger thresholds to calculate against.\n" +
                "0.5 = Very Easy | 0.75 = Easy | 1.0 = Normal | 1.5 = Hard | 2.0 = Very Hard", new AcceptableValueRange<float>(0.0f, 4.0f)),
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigDesiredPlayerDamageMult = configSync.Bind(Config, "World Difficulty Targets", "Desired player damage multiplier", 1.0f,
                new ConfigDescription("The difficulty tier you WANT your attacks to deal stagger damage against.\n" +
                "Setting this to 1.0 means your weapon will fill an enemy's posture bar at standard Normal Solo speeds, ignoring world sliders.\n" +
                "1.25 = Very Easy (Fills bar faster) | 1.1 = Easy | 1.0 = Normal | 0.85 = Hard | 0.7 = Very Hard (Fills bar much slower)", new AcceptableValueRange<float>(0.1f, 2.0f)),
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigDesiredEnemyDamageMP = configSync.Bind(Config, "Multiplayer Difficulty Targets", "Mobs MP bonus damage multiplier", 0.0f,
                new ConfigDescription("Default mobs damage is increased by 4% for each extra player. If u playing in group of 4 players, mobs damage will be 1+0.04*3=1.12 (12% increased).\n" +
                "This multiplier will be applied to 12% i.e. setting it to 0.5 will result into 1.06 final rate.\n" +
                "Setting the value to 0.0 will result into pure singlplayer behaviour for players staggerbars.", new AcceptableValueRange<float>(0.0f, 5.0f)),
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigDesiredEnemyHealthMP = configSync.Bind(Config, "Multiplayer Difficulty Targets", "Mobs MP bonus health multiplier", 0.0f,
                new ConfigDescription("Default mobs health is increased by 30% for each extra player. If u playing in group of 4 players, mobs effective health will be increased by 90%\n" +
                "This multiplier will be applied to that value i.e. setting it to 0.5 will result into 45% final hp bonus.\n" +
                "Setting the value to 0.0 result into pure singlplayer HP scaling calculations only.", new AcceptableValueRange<float>(0.0f, 5.0f)),
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableServerDebugLogs = configSync.Bind(Config, "Debug", "1 Enable console output for server", true,
                "Set to true to print scaling numbers directly into the F5 game console for host player if server hosted via in-game GUI, or to console if server is dedicated server.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableBroadcastLogs = configSync.Bind(Config, "Debug", "2 Enable broadcast logs to clients", false,
                "Set to true to allow server/host to broadcast damage numbers to clients.",
                ConditionalConfigSyncAPI.SyncMode.AlwaysServerControlled);

            ConfigEnableClientDebugLogs = configSync.Bind(Config, "Debug", "3 Enable console output for clients", false,
                "Set to true to print scaling numbers directly into the F5 game console. Each client can turn it on/off by himself in solo mode, or if broadcast is enabled.\n",
                ConditionalConfigSyncAPI.SyncMode.AlwaysClientControlled);

            _harmonyInstance = new Harmony("com.babrushka.bepinex.staggerscaler");
            _harmonyInstance.PatchAll();
            Log.LogInfo("Stagger Scaling Mod Loaded with Config Support!");
        }

        void OnDestroy()
        {
            if (_harmonyInstance != null)
            {
                _harmonyInstance.UnpatchSelf();
                _harmonyInstance = null;
            }
            Log.LogInfo("Old Instance completely wiped!");
        }
    }
}
