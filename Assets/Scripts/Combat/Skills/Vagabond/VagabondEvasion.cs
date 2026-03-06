// ============================================================================
// 流浪剑客 - 闪避：燕返
// CD 1.5秒，无消耗，向面朝方向翻滚 2~3 格（不穿墙）
// 被动二「绝境求生」：HP < 30% 时无敌帧延长至 0.5秒
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md §流浪剑客
// ============================================================================

using System.Collections;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Map;

namespace EscapeTheTower.Combat.Skills.Vagabond
{
    /// <summary>
    /// 燕返 —— 剑客专属闪避，向面朝方向平滑翻滚位移（碰墙提前停止）
    /// </summary>
    public class VagabondEvasion : SkillExecutor
    {
        private const float ROLL_DISTANCE = 2.5f;
        private const float ROLL_DURATION = 0.2f;    // 翻滚动画持续时间（秒）
        private const float NORMAL_INVINCIBLE = 0.3f;
        private const float LOW_HP_INVINCIBLE = 0.5f;
        private const float LOW_HP_THRESHOLD = 0.30f;

        protected override void OnExecute()
        {
            StartCoroutine(RollRoutine());
        }

        private IEnumerator RollRoutine()
        {
            IsExecuting = true;

            // 翻滚方向（锁定为四方向之一）
            Vector2 dir = GetFacingDirection();
            Vector2Int gridDir = new Vector2Int(
                Mathf.RoundToInt(dir.x),
                Mathf.RoundToInt(dir.y));

            // 防止对角位移
            if (gridDir.x != 0 && gridDir.y != 0)
            {
                if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                    gridDir.y = 0;
                else
                    gridDir.x = 0;
            }

            // 确保至少有一个方向
            if (gridDir == Vector2Int.zero) gridDir = Vector2Int.down;

            // 逐格检测墙壁，计算实际可到达的最远距离
            int maxGridDistance = Mathf.RoundToInt(ROLL_DISTANCE);
            Vector2Int startGrid = GridMovement.WorldToGrid(Hero.transform.position);
            int actualDistance = 0;

            for (int step = 1; step <= maxGridDistance; step++)
            {
                Vector2Int checkPos = startGrid + gridDir * step;

                // 碰墙或门则停止
                var provider = TilemapCollisionProvider.Instance;
                if (provider != null && provider.IsWall(checkPos))
                    break;

                actualDistance = step;
            }

            // 没有可移动空间，仅触发无敌帧但不位移
            Vector3 startPos = Hero.transform.position;
            Vector3 endPos = startPos + new Vector3(
                gridDir.x * actualDistance, gridDir.y * actualDistance, 0f);

            // 被动二检测：HP < 30% 延长无敌帧
            float hpRatio = Hero.CurrentStats.Get(StatType.HP) /
                Mathf.Max(1f, Hero.CurrentStats.Get(StatType.MaxHP));
            float invDuration = hpRatio < LOW_HP_THRESHOLD ? LOW_HP_INVINCIBLE : NORMAL_INVINCIBLE;

            // 启用无敌帧状态
            Hero.SetInvincible(invDuration);

            Debug.Log($"[剑客] 燕返！方向=({gridDir.x},{gridDir.y}) 距离={actualDistance}格 " +
                      $"无敌帧={invDuration}s" +
                      (hpRatio < LOW_HP_THRESHOLD ? " 【绝境求生：延长至0.5s】" : ""));

            // 有位移距离时才执行平滑移动
            if (actualDistance > 0)
            {
                float elapsed = 0f;
                while (elapsed < ROLL_DURATION)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / ROLL_DURATION);
                    // EaseOutQuad: t*(2-t) —— 起步快、收尾慢
                    float eased = t * (2f - t);
                    Hero.transform.position = Vector3.Lerp(startPos, endPos, eased);
                    yield return null;
                }

                Hero.transform.position = endPos;

                // 同步 GridMovement 的逻辑坐标
                var gridMovement = Hero.GetComponent<GridMovement>();
                gridMovement?.SnapToGrid();
            }

            IsExecuting = false;
        }
    }
}
