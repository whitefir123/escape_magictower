// ============================================================================
// 流浪剑客 - 技能一：疾风突刺
// CD 8秒，无消耗，向面朝方向突刺（不穿墙），路径上敌人受 (40+1.5*ATK) 物伤
// 每命中一个目标恢复 10 MP
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md §流浪剑客
// ============================================================================

using System.Collections;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Entity;
using EscapeTheTower.Map;

namespace EscapeTheTower.Combat.Skills.Vagabond
{
    /// <summary>
    /// 疾风突刺 —— 线性突刺穿透攻击 + 每命中回蓝（碰墙提前停止）
    /// </summary>
    public class VagabondDashStrike : SkillExecutor
    {
        private const float DASH_DISTANCE = 3.0f;
        private const float DASH_DURATION = 0.15f; // 突刺动画时间
        private const float LINE_WIDTH = 0.8f;     // 碰撞半宽

        protected override void OnExecute()
        {
            StartCoroutine(DashRoutine());
        }

        private IEnumerator DashRoutine()
        {
            IsExecuting = true;

            Vector2 dir = GetFacingDirection();
            Vector2Int gridDir = new Vector2Int(
                Mathf.RoundToInt(dir.x),
                Mathf.RoundToInt(dir.y));

            // 防止对角
            if (gridDir.x != 0 && gridDir.y != 0)
            {
                if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                    gridDir.y = 0;
                else
                    gridDir.x = 0;
            }

            if (gridDir == Vector2Int.zero) gridDir = Vector2Int.right;

            // 逐格检测墙壁
            int maxGridDistance = Mathf.RoundToInt(DASH_DISTANCE);
            Vector2Int startGrid = GridMovement.WorldToGrid(Hero.transform.position);
            int actualDistance = 0;

            for (int step = 1; step <= maxGridDistance; step++)
            {
                Vector2Int checkPos = startGrid + gridDir * step;
                var provider = TilemapCollisionProvider.Instance;
                if (provider != null && provider.IsWall(checkPos))
                    break;
                actualDistance = step;
            }

            Vector3 startPos = Hero.transform.position;
            Vector3 endPos = startPos + new Vector3(
                gridDir.x * actualDistance, gridDir.y * actualDistance, 0f);

            // 整条路径上查找目标（使用实际距离）
            float lineDistance = Mathf.Max(actualDistance, 1f);
            var targets = SkillTargeting.FindEnemiesOnLine(
                startPos, dir, lineDistance, LINE_WIDTH, Hero.Faction);

            // 平滑突刺位移
            if (actualDistance > 0)
            {
                float elapsed = 0f;
                while (elapsed < DASH_DURATION)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / DASH_DURATION);
                    Hero.transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }

                Hero.transform.position = endPos;

                // 同步 GridMovement
                var gridMovement = Hero.GetComponent<GridMovement>();
                gridMovement?.SnapToGrid();
            }

            // 对路径上的目标造成伤害
            int hitCount = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.IsAlive) continue;

                var result = CalculateSkillDamageResult(target.CurrentStats);
                target.TakeDamage(result, Hero.EntityID);
                hitCount++;

                // 每命中一个目标恢复 10 MP
                Hero.RestoreMana(Data.manaRestoreOnHit);

                // 怒气积攒
                Hero.AddRage(3f);
            }

            float displayDmg = Data.baseDamage + Data.atkScaling * Hero.CurrentStats.Get(StatType.ATK);
            Debug.Log($"[剑客] 疾风突刺！伤害≈{displayDmg:F1} 命中={hitCount}个目标 " +
                      $"实际距离={actualDistance}格 回蓝={hitCount * Data.manaRestoreOnHit}");

            IsExecuting = false;
        }
    }
}
