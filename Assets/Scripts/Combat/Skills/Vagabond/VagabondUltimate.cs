// ============================================================================
// 流浪剑客 - 必杀技：极刃风暴
// CD 45秒（实际由怒气驱动），消耗满怒气
// 8 段打击，每段 (30+0.8*ATK)，绝对霸体免疫伤害，15% 物理吸血
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md §流浪剑客
// ============================================================================

using System.Collections;
using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat.Skills.Vagabond
{
    /// <summary>
    /// 极刃风暴 —— 满怒气释放，多段 AOE 爆发 + 无敌 + 吸血
    /// </summary>
    public class VagabondUltimate : SkillExecutor
    {
        private const float AOE_RADIUS = 2.5f;
        private const float HIT_INTERVAL = 0.15f; // 每段间隔

        protected override void OnExecute()
        {
            StartCoroutine(UltimateRoutine());
        }

        private IEnumerator UltimateRoutine()
        {
            IsExecuting = true;
            int hitCount = Data.hitCount > 0 ? Data.hitCount : 8;

            // 绝对霸体 + 免疫伤害（多留 0.2s 安全裕量）
            float totalDuration = hitCount * HIT_INTERVAL + 0.2f;
            Hero.SetInvincible(totalDuration);
            Hero.SetSuperArmor(totalDuration);

            Debug.Log($"[剑客] 极刃风暴！{hitCount}段打击开始！（无敌+霸体 {totalDuration:F2}s）");

            for (int i = 0; i < hitCount; i++)
            {
                var targets = SkillTargeting.FindEnemiesInRadius(
                    Hero.transform.position, AOE_RADIUS, Hero.Faction);

                // 使用 DealDamageToTargets 自动处理吸血
                DealDamageToTargets(targets);

                if (targets.Count > 0)
                {
                    Debug.Log($"[剑客] 极刃风暴 第{i + 1}段 命中={targets.Count}");
                }

                yield return new WaitForSeconds(HIT_INTERVAL);
            }

            // 取消霸体和无敌
            Hero.ClearCombatStates();
            IsExecuting = false;
            Debug.Log("[剑客] 极刃风暴结束！");
        }
    }
}
