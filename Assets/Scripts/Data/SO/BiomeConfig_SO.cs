// ============================================================================
// 逃离魔塔 - 生态群系配置 ScriptableObject
// 每层地图的数据驱动配置模板，支持 9 大生态层的无缝扩展。
//
// 来源：GameData_Blueprints/03_World_Themes.md
//       DesignDocs/06_Map_and_Modes.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 生态群系配置 SO —— 定义单层地图的全部生态参数
    /// </summary>
    [CreateAssetMenu(fileName = "NewBiomeConfig", menuName = "EscapeTheTower/Data/BiomeConfig")]
    public class BiomeConfig_SO : ScriptableObject
    {
        [Header("=== 基础信息 ===")]
        [Tooltip("群系名称（如：暗黑地牢）")]
        public string biomeName;

        [Tooltip("群系英文标识")]
        public string biomeID;

        [Tooltip("群系序号（第 1 层为暗黑地牢，固定为 1）")]
        public int biomeIndex;

        [Tooltip("群系描述")]
        [TextArea(2, 5)]
        public string biomeDescription;

        [Header("=== 怪物池 ===")]
        [Tooltip("该群系可刷新的普通怪物数据列表")]
        public List<MonsterData_SO> normalMonsterPool = new List<MonsterData_SO>();

        [Tooltip("该群系的关底 Boss 数据")]
        public MonsterData_SO bossData;

        [Header("=== 房间分布 ===")]
        [Tooltip("战斗房占比 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float combatRoomWeight = 0.70f;

        [Tooltip("奖励房占比 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float treasureRoomWeight = 0.10f;

        [Tooltip("事件房占比 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float eventRoomWeight = 0.20f;

        [Tooltip("每层的总房间数量范围 - 最小")]
        public int minRoomCount = 12;

        [Tooltip("每层的总房间数量范围 - 最大")]
        public int maxRoomCount = 20;

        [Header("=== 环境预告提示 ===")]
        [Tooltip("过场阶梯区域的环境装饰提示描述")]
        [TextArea(2, 4)]
        public string environmentHintDescription;

        [Header("=== 楼层数值系数 ===")]
        [Tooltip("怪物属性楼层放大常数（每层递增百分比）")]
        public float floorScalingConstant = 0.15f;

        [Tooltip("经验值楼层放大常数（略低于属性放大，确保后期仍需积累）")]
        public float expScalingConstant = 0.10f;

        /// <summary>
        /// 根据当前实际层数计算怪物属性的楼层乘数
        /// 公式：1 + floorScalingConstant * (currentFloor - 1)
        /// </summary>
        public float GetFloorMultiplier(int currentFloor)
        {
            return 1f + floorScalingConstant * Mathf.Max(0, currentFloor - 1);
        }

        /// <summary>
        /// 根据当前实际层数计算经验值的楼层乘数
        /// </summary>
        public float GetExpMultiplier(int currentFloor)
        {
            return 1f + expScalingConstant * Mathf.Max(0, currentFloor - 1);
        }
    }
}
