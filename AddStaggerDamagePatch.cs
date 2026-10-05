using HarmonyLib;
using UnityEngine;
using Valheim.SettingsGui;
using System.Runtime.CompilerServices;
using UnityEngine.Scripting;
using static System.Net.Mime.MediaTypeNames;
using static Version;

namespace StaggerScaler
{
    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    public static class Patch_AddStaggerDamage
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, ref float damage, Vector3 forceDirection, HitData hit)
        {

            if (__instance == null) return;
            if (__instance.IsPlayer())
            {
                float maxStager = __instance.GetMaxHealth() * __instance.m_staggerDamageFactor;
                float vanillaStagger = damage / maxStager*100.0f;
                HelperFunctions.updateStagger(__instance, damage, vanillaStagger);

                bool configEnabled = StaggerScalerPlugin.ConfigEnableShieldScaling.Value && StaggerScalerPlugin.ConfigGlobalSwitch.Value;
                if (DamageContext.staggerBlockTracker.TryGetValue(__instance, out var moddedBlockStagger) && configEnabled)
                {
                    damage = moddedBlockStagger.Value;
                }

                configEnabled = StaggerScalerPlugin.ConfigEnableArmorScaling.Value && StaggerScalerPlugin.ConfigGlobalSwitch.Value;
                if (DamageContext.staggerArmorTracker.TryGetValue(__instance, out var moddedArmorStagger) && configEnabled)
                {
                    damage = moddedArmorStagger.Value;
                }
            }
            else
            {
                if (hit == null) return;
                Character attacker = hit.GetAttacker();
                if (attacker == null) return;

                if (attacker.IsPlayer() && StaggerScalerPlugin.ConfigEnableAttackScaling.Value && StaggerScalerPlugin.ConfigGlobalSwitch.Value)
                {
                    damage = Attack.calc(__instance, damage, hit.GetAttacker());
                }
            }

        }

    }
}
