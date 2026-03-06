// ============================================================================
// 流浪剑客 - 技能二：旋风斩
// CD 12秒，消耗 30 MP，原地 AOE 2秒，每 0.5s 对周围敌人造成 (15+0.6*ATK) 物伤
// 期间移速降低 30% 但免疫击退打断
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md §流浪剑客
// ============================================================================

using System.Collections;
using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat.Skills.Vagabond
{
    /// <summary>
    /// 旋风斩 —— 持续 AOE 多段打击 + 霸体
    /// </summary>
    public class VagabondWhirlwind : SkillExecutor
    {
        private const float DURATION = 2.0f;
        private const float TICK_INTERVAL = 0.5f;
        private const float AOE_RADIUS = 2.0f;

        protected override void OnExecute()
        {
            StartCoroutine(WhirlwindRoutine());
        }

        private IEnumerator WhirlwindRoutine()
        {
            IsExecuting = true;
            float elapsed = 0f;
            float tickTimer = 0f;

            // 标记英雄霸体状态（免疫击退打断）
            Hero.SetSuperArmor(DURATION + 0.1f); // 多留 0.1s 安全裕量

            // 降低移速 30%（通过管线层级6临时 Buff 实现）
            var slowBuff = new StatBlock();
            slowBuff.Set(StatType.MoveSpeed, -0.3f);
            Hero.AddTempBuff(slowBuff);

            Debug.Log($"[剑客] 旋风斩开始！持续 {DURATION}s（霸体+减速30%）");

            while (elapsed < DURATION)
            {
                tickTimer += Time.deltaTime;
                elapsed += Time.deltaTime;

                if (tickTimer >= TICK_INTERVAL)
                {
                    tickTimer -= TICK_INTERVAL;

                    // 每 tick 对周围敌人造成伤害
                    var targets = SkillTargeting.FindEnemiesInRadius(
                        Hero.transform.position, AOE_RADIUS, Hero.Faction);

                    DealDamageToTargets(targets);

                    if (targets.Count > 0)
                    {
                        Debug.Log($"[剑客] 旋风斩 tick！命中={targets.Count}");
                    }
                }

                yield return null;
            }

            // 恢复移速、取消霸体
            Hero.RemoveTempBuff(slowBuff);
            Hero.ClearCombatStates();
            IsExecuting = false;
            Debug.Log("[剑客] 旋风斩结束");
        }
    }
}
