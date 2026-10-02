using System;
using System.Collections.Generic;
using System.Text;

namespace StaggerScaler
{
    class Attack
    {
        public static float calc(Character target, float damage, Character player)
        {
            float MPDiffCoef = ((Game.instance.GetDifficultyDamageScaleEnemy(target.transform.position) - 1.0f) * StaggerScalerPlugin.ConfigDesiredEnemyHealthMP.Value + 1.0f);
            MPDiffCoef = Game.instance.GetDifficultyDamageScaleEnemy(target.transform.position) / MPDiffCoef;

            float playerAttackScaleFactor = StaggerScalerPlugin.ConfigDesiredPlayerDamageMult.Value / Game.m_playerDamageRate * MPDiffCoef;
            float targetDamage = damage * playerAttackScaleFactor;
            float mobStaggerFactor = target.m_staggerDamageFactor;
            if (mobStaggerFactor <= 0f) mobStaggerFactor = 0.3f;
            float monsterMaxStaggerLimit = target.GetMaxHealth() * mobStaggerFactor;
            if (monsterMaxStaggerLimit <= 0f) monsterMaxStaggerLimit = 1.0f;

            float modstaggerIncrease = (float)(targetDamage / monsterMaxStaggerLimit * 100f);
            float vanillastaggerIncrease = (float)(damage / monsterMaxStaggerLimit * 100f);

            //  HelperFunctions.LogToF5Console($"[StaggerScaler] [[{player.GetPlayerName()}]] Hitting monster '{target.m_name} (mob stagger bar mult: {target.m_staggerDamageFactor:F1}) '. Stagger dealt: vanilla {damage:F1} -> modded {targetDamage:F1}; scaleFactor:{playerAttackScaleFactor:F2};\n" +
            // $" Modded enemy stagger: +{modstaggerIncrease:F0}%; Vanilla enemy stagger: +{vanillastaggerIncrease:F0}%\n");

            HelperFunctions.attackLog(player, MPDiffCoef, playerAttackScaleFactor, target.m_name, mobStaggerFactor, damage, targetDamage, vanillastaggerIncrease, modstaggerIncrease);

            return targetDamage;
        }
    }
}
