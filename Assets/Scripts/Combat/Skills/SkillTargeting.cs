// ============================================================================
// 逃离魔塔 - 技能目标检测工具 (SkillTargeting)
// 提供通用的 AOE 碰撞检测方法（圆形/扇形/线性），供技能执行器调用。
//
// 来源：DesignDocs/03_Combat_System.md §1.2
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Combat.Skills
{
    /// <summary>
    /// 技能目标检测工具 —— 提供圆形/扇形/线性范围内敌人查找
    /// 所有方法均基于运行时 EntityBase 查找，不依赖物理碰撞体
    /// </summary>
    public static class SkillTargeting
    {
        // 缓存列表，避免每次分配
        private static readonly List<EntityBase> _tempResults = new();

        /// <summary>
        /// 查找圆形范围内的所有敌方实体
        /// </summary>
        /// <param name="center">中心点（世界坐标）</param>
        /// <param name="radius">半径</param>
        /// <param name="casterFaction">施法者阵营（自动排除同阵营）</param>
        /// <returns>范围内的敌方实体列表（每次调用复用内部缓存，请勿长期持有引用）</returns>
        public static List<EntityBase> FindEnemiesInRadius(Vector3 center, float radius, Faction casterFaction)
        {
            _tempResults.Clear();
            float radiusSq = radius * radius;

            // 遍历场景中所有 EntityBase
            var allEntities = Object.FindObjectsByType<EntityBase>(FindObjectsSortMode.None);
            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i];
                if (!entity.IsAlive) continue;
                if (entity.Faction == casterFaction) continue;

                float distSq = (entity.transform.position - center).sqrMagnitude;
                if (distSq <= radiusSq)
                {
                    _tempResults.Add(entity);
                }
            }

            return _tempResults;
        }

        /// <summary>
        /// 查找扇形范围内的所有敌方实体
        /// </summary>
        /// <param name="origin">扇形原点</param>
        /// <param name="direction">扇形朝向（归一化方向）</param>
        /// <param name="radius">扇形半径</param>
        /// <param name="halfAngle">半角（度数，如 60 表示总张角 120°）</param>
        /// <param name="casterFaction">施法者阵营</param>
        public static List<EntityBase> FindEnemiesInCone(
            Vector3 origin, Vector2 direction, float radius, float halfAngle, Faction casterFaction)
        {
            _tempResults.Clear();
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
            direction.Normalize();

            float radiusSq = radius * radius;
            float cosHalfAngle = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

            var allEntities = Object.FindObjectsByType<EntityBase>(FindObjectsSortMode.None);
            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i];
                if (!entity.IsAlive) continue;
                if (entity.Faction == casterFaction) continue;

                Vector2 toTarget = (Vector2)(entity.transform.position - origin);
                float distSq = toTarget.sqrMagnitude;
                if (distSq > radiusSq) continue;
                if (distSq < 0.001f) { _tempResults.Add(entity); continue; } // 重叠

                float dot = Vector2.Dot(direction, toTarget.normalized);
                if (dot >= cosHalfAngle)
                {
                    _tempResults.Add(entity);
                }
            }

            return _tempResults;
        }

        /// <summary>
        /// 查找线性路径上的所有敌方实体（用于突刺/穿透剑气）
        /// </summary>
        /// <param name="origin">起点</param>
        /// <param name="direction">方向（归一化）</param>
        /// <param name="distance">最大距离</param>
        /// <param name="width">线段宽度（半宽）</param>
        /// <param name="casterFaction">施法者阵营</param>
        public static List<EntityBase> FindEnemiesOnLine(
            Vector3 origin, Vector2 direction, float distance, float width, Faction casterFaction)
        {
            _tempResults.Clear();
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
            direction.Normalize();

            // 线段终点
            Vector2 start2D = (Vector2)origin;
            Vector2 end2D = start2D + direction * distance;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x); // 垂直方向

            var allEntities = Object.FindObjectsByType<EntityBase>(FindObjectsSortMode.None);
            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i];
                if (!entity.IsAlive) continue;
                if (entity.Faction == casterFaction) continue;

                Vector2 pos = (Vector2)entity.transform.position;
                Vector2 toEntity = pos - start2D;

                // 投影到方向轴上，检查是否在 [0, distance] 范围内
                float projAlongDir = Vector2.Dot(toEntity, direction);
                if (projAlongDir < -0.5f || projAlongDir > distance + 0.5f) continue;

                // 投影到垂直轴上，检查是否在半宽内
                float projPerp = Mathf.Abs(Vector2.Dot(toEntity, perpendicular));
                if (projPerp <= width)
                {
                    _tempResults.Add(entity);
                }
            }

            return _tempResults;
        }
    }
}
