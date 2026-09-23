using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DynamicParryScaling
{
    [BepInPlugin("babrushkas.staggerscaler", "Stagger Scaler", "1.2.0")]
    public class DynamicParryScalingPlugin : BaseUnityPlugin
    {
        // --- Configuration Entries ---
        public static ConfigEntry<float> ConfigDesiredPlayerDamageMult;
        public static ConfigEntry<float> ConfigDesiredEnemyDamageMult;
        public static ConfigEntry<bool> ConfigEnableDebugLogs;
        public static ConfigEntry<bool> ConfigGlobalSwitch;
        public static ConfigEntry<float> ConfigSimulatedPlayers;
        public static ConfigEntry<bool> ConfigEnableShieldScaling;
        public static ConfigEntry<bool> ConfigEnableArmorScaling;

        // --- NEW OFFENSIVE CONFIGURATIONS ---
        public static ConfigEntry<bool> ConfigEnableAttackScaling;

        public static DynamicParryScalingPlugin Instance;
        public static ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            Log = base.Logger;

            // --- General Master Toggles ---
            ConfigGlobalSwitch = Config.Bind("General", "GlobalSwitch", true,
                "Enables or disables applying modifiers to stagger values globally.");

            ConfigEnableShieldScaling = Config.Bind("General", "EnableShieldScaling", true,
                "Enables stagger scaling reduction while you are actively raising your shield/weapon to block.");

            ConfigEnableArmorScaling = Config.Bind("General", "EnableArmorScaling", true,
                "Enables passive stagger scaling protection when your shield is NOT up.");

            ConfigEnableAttackScaling = Config.Bind("General", "EnableAttackScaling", true,
                "Enables offensive stagger scaling, making your attacks fill enemy stagger bars faster or slower based on config.");

            // --- Defensive Difficulty Target ---
            ConfigDesiredEnemyDamageMult = Config.Bind("Difficulty", "Desired Enemy Damage Multiplier", 1.0f,
                "The difficulty tier you WANT your shield blocks and stagger thresholds to calculate against.\n" +
                "0.5 = Very Easy | 0.75 = Easy | 1.0 = Normal | 1.5 = Hard | 2.0 = Very Hard");

            // --- NEW: Offensive Difficulty Target ---
            ConfigDesiredPlayerDamageMult = Config.Bind("Difficulty", "Desired Player Damage Multiplier", 1.0f,
                "The difficulty tier you WANT your attacks to deal stagger damage against.\n" +
                "Setting this to 1.0 means your weapon will fill an enemy's posture bar at standard Normal Solo speeds, ignoring world sliders.\n" +
                "1.25 = Very Easy (Fills bar faster) | 1.1 = Easy | 1.0 = Normal | 0.85 = Hard | 0.7 = Very Hard (Fills bar much slower)");


            // --- Debugging & Sandbox Emulation ---
            ConfigEnableDebugLogs = Config.Bind("Debug", "EnableLogs", false,
                "Set to true to print scaling numbers directly into the F5 game console.");

            ConfigSimulatedPlayers = Config.Bind("Debug", "SimulatedPlayerCount", 0.0f,
                "Forces a mocked player count for testing. Set to 0 for normal server behavior (uses proximity loops). 1 means party of 2 (Including your current character itself). Max cap is 4 (Vanilla).");

            // --- Initialize Patches ---
            Harmony harmony = new Harmony("com.babrushka.bepinex.staggerscaler");
            harmony.PatchAll();

            Log.LogInfo("Dynamic Parry & Stagger Scaling Mod Loaded with Config Support!");
        }
    }
}
