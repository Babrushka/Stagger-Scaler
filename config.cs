using System.Diagnostics;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;

namespace StaggerScaler
{
    [BepInPlugin("babrushkas.staggerscaler", "Stagger Scaler", "1.6")]
    public class StaggerScalerPlugin : BaseUnityPlugin
    {
        // --- Configuration Entries ---
        public static ConfigEntry<float> ConfigDesiredPlayerDamageMult;
        public static ConfigEntry<float> ConfigDesiredEnemyDamageMult;
        public static ConfigEntry<float> ConfigDesiredEnemyDamageMP;
        public static ConfigEntry<float> ConfigDesiredEnemyHealthMP;
        public static ConfigEntry<bool> ConfigEnableDebugLogs;
        public static ConfigEntry<bool> ConfigGlobalSwitch;
        public static ConfigEntry<float> ConfigSimulatedPlayers;
        public static ConfigEntry<bool> ConfigEnableShieldScaling;
        public static ConfigEntry<bool> ConfigEnableArmorScaling;

        // --- NEW OFFENSIVE CONFIGURATIONS ---
        public static ConfigEntry<bool> ConfigEnableAttackScaling;

        public static StaggerScalerPlugin Instance;
        public static ManualLogSource Log;

        private static Harmony _harmonyInstance;

        private void Awake()
        {
            Instance = this;
            Log = base.Logger;
            // --- General Master Toggles ---
            UnityEngine.Debug.developerConsoleVisible = false;
            UnityEngine.Debug.developerConsoleEnabled = false;
            ConfigGlobalSwitch = Config.Bind("General", "GlobalSwitch", true,
                "Enables or disables applying modifiers to stagger values globally.");

            ConfigEnableShieldScaling = Config.Bind("General", "EnableShieldScaling", true,
                "Enables stagger scaling reduction while you are actively raising your shield/weapon to block.");

            ConfigEnableArmorScaling = Config.Bind("General", "EnableArmorScaling", true,
                "Enables passive stagger scaling protection when your shield is NOT up.");

            ConfigEnableAttackScaling = Config.Bind("General", "EnableAttackScaling", true,
                "Enables offensive stagger scaling, making your attacks fill enemy stagger bars faster or slower based on config.");

            // --- Defensive Difficulty Target ---
            ConfigDesiredEnemyDamageMult = Config.Bind("World Difficulty Targets", "Desired enemy damage multiplier", 1.0f,
                "The difficulty tier you WANT your shield blocks and stagger thresholds to calculate against.\n" +
                "0.5 = Very Easy | 0.75 = Easy | 1.0 = Normal | 1.5 = Hard | 2.0 = Very Hard");

            // --- NEW: Offensive Difficulty Target ---
            ConfigDesiredPlayerDamageMult = Config.Bind("World Difficulty Targets", "Desired player damage multiplier", 1.0f,
                "The difficulty tier you WANT your attacks to deal stagger damage against.\n" +
                "Setting this to 1.0 means your weapon will fill an enemy's posture bar at standard Normal Solo speeds, ignoring world sliders.\n" +
                "1.25 = Very Easy (Fills bar faster) | 1.1 = Easy | 1.0 = Normal | 0.85 = Hard | 0.7 = Very Hard (Fills bar much slower)");

            ConfigDesiredEnemyDamageMP = Config.Bind("Multiplayer Difficulty Targets", "Mobs MP bonus damage multiplier", 0.0f,
                "Default mobs damage is increased by 4% for each extra player. If u playing in group of 4 players, mobs damage will be 1+0.04*3=1.12 (12% increased).\n" +
                "This multiplier will be applied to 12% i.e. setting it to 0.5 will result into 1.06 final rate.\n" +
                "Setting the value to 0.0 will result into pure singlplayer behaviour for players staggerbars.");
            
            ConfigDesiredEnemyHealthMP = Config.Bind("Multiplayer Difficulty Targets", "Mobs MP bonus health multiplier", 0.0f,
                "Default mobs health is increased by 30% for each extra player. If u playing in group of 4 players, mobs effective health will be increased by 90%\n" +
                "This multiplier will be applied to that value i.e. setting it to 0.5 will result into 45% final hp bonus.\n" +
                "Setting the value to 0.0 will result into pure singlplayer behaviour for enemy staggerbars." +
                "Dont be confused, stagger bar for every character depends on its max HP, it is 0.4*max hp for player, usually 0.3 (0.5 for some huge mobs) for enemies." +
                "So 190% HP will lead to 1.9 staggering difficulty, that in addition to extra hard combat modifier (0.7 to players damage) leads nearly to inability to stagger most mobs." +
                "This multiplier doesn't increase or decrease enemys HP bar, it only affects stagger calculations!");


            // --- Debugging & Sandbox Emulation ---
            ConfigEnableDebugLogs = Config.Bind("Debug", "EnableLogs", false,
                "Set to true to print scaling numbers directly into the F5 game console.");

            // --- Initialize Patches ---
            _harmonyInstance = new Harmony("com.babrushka.bepinex.staggerscaler");
            _harmonyInstance.PatchAll();

            Log.LogInfo("Stagger Scaling Mod Loaded with Config Support!");
        }
        void OnDestroy()
        {
            // 1. CRITICAL: Forcibly rip out ALL harmony patches registered by this assembly instance
            if (_harmonyInstance != null)
            {
                _harmonyInstance.UnpatchSelf();
                _harmonyInstance = null;
            }

            // 2. CRITICAL: Destroy any GameObject custom items you spawned
            // If you attached custom components to GameObjects, they must be cleanly destroyed here!

            Logger.LogInfo("Old Instance completely wiped!");
        }
    }
}
