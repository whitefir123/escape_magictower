// ============================================================================
// 逃离魔塔 - 怪物数据 ScriptableObject
// 继承 EntityData_SO，额外包含怪物种族标签、经验值、掉落等配置。
// 怪物面板使用 Min~Max 波动范围（由同心圆距离法则运行时插值生成）。
//
// 来源：GameData_Blueprints/04_1_Floor1_Dungeon.md
//       GameData_Blueprints/04_01_Monster_Spawn_Logic.md
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 怪物数据 SO —— 继承 EntityData_SO，包含怪物特有配置
    /// 注意：基类中的属性字段代表 Min 值，本类额外提供 Max 值
    /// 实际生成时由同心圆距离法则在 Min~Max 之间插值
    /// </summary>
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "EscapeTheTower/Data/MonsterData")]
    public class MonsterData_SO : EntityData_SO
    {
        [Header("=== 种族标签 ===")]
        [Tooltip("怪物种族/特性标签（Flags 组合）")]
        public MonsterTag tags;

        [Header("=== 面板属性上限（Max 值）===")]
        [Tooltip("注：基类字段代表 Min 值，以下为 Max 值")]
        public float maxHP_Max;
        public float maxATK_Max;
        public float maxMATK_Max;
        public float maxDEF_Max;
        public float maxMDEF_Max;

        [Header("=== 闪避与暴击抗性（Max 值）===")]
        [Range(0f, 1f)]
        public float dodge_Max;
        [Range(0f, 1f)]
        public float critResist;

        [Header("=== 行为参数 ===")]
        [Tooltip("攻击间隔 (秒/次)")]
        public float attackInterval = 1.5f;

        [Tooltip("移动速度描述性标签（实际数值由 baseMoveSpeed 决定）")]
        public string moveSpeedDescription;

        [Header("=== 经验与掉落 ===")]
        [Tooltip("基础经验值（由怪物类型决定基准区间）")]
        public int baseExpReward;

        [Tooltip("基础金币掉落最小值")]
        public int goldDropMin;

        [Tooltip("基础金币掉落最大值")]
        public int goldDropMax;

        [Header("=== 机制描述 ===")]
        [Tooltip("特性机制的文本描述（供调试与策划查看）")]
        [TextArea(2, 5)]
        public string mechanicDescription;

        [Header("=== 状态免疫 ===")]
        [Tooltip("免疫的状态效果类型")]
        public StatusEffectType[] immuneToEffects;

        [Header("=== 硬控抗性 ===")]
        [Tooltip("是否拥有硬控递减机制（Boss 专用）")]
        public bool hasCCDiminishing = false;

        [Tooltip("是否拥有永久霸体（高阶 Boss 专用）")]
        public bool hasPermUnstoppable = false;

        /// <summary>
        /// 根据距离系数（0.0=内环近端, 1.0=外环远端）生成插值后的属性块
        /// 实现同心圆距离法则：GameData_Blueprints/04_01_Monster_Spawn_Logic.md
        /// </summary>
        /// <param name="distanceFactor">距离系数 [0, 1]，0=出生点附近，1=Boss门口</param>
        /// <param name="floorMultiplier">楼层全局系数</param>
        public StatBlock CreateScaledStatBlock(float distanceFactor, float floorMultiplier)
        {
            distanceFactor = Mathf.Clamp01(distanceFactor);

            var stats = new StatBlock();

            // 根据同心圆距离在 Min~Max 之间线性插值
            stats.Set(StatType.MaxHP, Mathf.Lerp(baseMaxHP, maxHP_Max, distanceFactor) * floorMultiplier);
            stats.Set(StatType.HP, stats.Get(StatType.MaxHP));
            stats.Set(StatType.ATK, Mathf.Lerp(baseATK, maxATK_Max, distanceFactor) * floorMultiplier);
            stats.Set(StatType.MATK, Mathf.Lerp(baseMATK, maxMATK_Max, distanceFactor) * floorMultiplier);
            stats.Set(StatType.DEF, Mathf.Lerp(baseDEF, maxDEF_Max, distanceFactor) * floorMultiplier);
            stats.Set(StatType.MDEF, Mathf.Lerp(baseMDEF, maxMDEF_Max, distanceFactor) * floorMultiplier);

            // 非插值型属性直接引用基类值
            stats.Set(StatType.CritRate, baseCritRate);
            stats.Set(StatType.CritMultiplier, baseCritMultiplier);
            stats.Set(StatType.Dodge, Mathf.Lerp(baseDodge, dodge_Max, distanceFactor));
            stats.Set(StatType.MoveSpeed, baseMoveSpeed);
            stats.Set(StatType.AttackSpeed, 1f / attackInterval); // 攻击间隔转为攻速

            return stats;
        }
    }
}
