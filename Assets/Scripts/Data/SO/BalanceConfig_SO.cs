// ============================================================================
// 逃离魔塔 - 平衡性配置 ScriptableObject (BalanceConfig_SO)
// 存储所有可由策划在 Inspector 中调整的平衡性数值。
// GameConstants 静态门面类会在运行时从此 SO 读取数值。
//
// 来源：各设计文档中的概率/权重/公式参数
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 平衡性配置 SO —— 集中管理所有可调整的游戏数值参数
    /// 在 Inspector 中修改后运行时自动生效，无需改代码
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "EscapeTheTower/Data/BalanceConfig")]
    public class BalanceConfig_SO : ScriptableObject
    {
        // =====================================================================
        //  符文系统
        // =====================================================================

        [Header("=== 符文系统 ===")]
        [Tooltip("击杀怪物触发属性符文三选一的概率")]
        [Range(0f, 1f)]
        public float runeDropChance = 0.40f;

        [Tooltip("机制符文保底计数阈值（连续 N 次未出史诗+触发保底）")]
        [Min(1)]
        public int runePityThreshold = 15;

        [Tooltip("职业专属符文强制插队概率")]
        [Range(0f, 1f)]
        public float classRuneForceChance = 0.10f;

        // === 机制符文稀有度权重 ===

        [Header("=== 符文稀有度权重 ===")]
        [Tooltip("凡品机制符文基础权重")]
        [Range(0f, 1f)]
        public float runeWeightCommon = 0.55f;

        [Tooltip("稀有机制符文基础权重")]
        [Range(0f, 1f)]
        public float runeWeightRare = 0.30f;

        [Tooltip("罕见机制符文基础权重")]
        [Range(0f, 1f)]
        public float runeWeightExceptional = 0.11f;

        [Tooltip("史诗机制符文基础权重")]
        [Range(0f, 1f)]
        public float runeWeightEpic = 0.035f;

        [Tooltip("传说机制符文基础权重")]
        [Range(0f, 1f)]
        public float runeWeightLegendary = 0.005f;

        // =====================================================================
        //  精英突变
        // =====================================================================

        [Header("=== 精英突变 ===")]
        [Tooltip("第一层精英突变基础概率")]
        [Range(0f, 1f)]
        public float eliteBaseMutationChance = 0.05f;

        [Tooltip("每深入一层精英概率递增量")]
        [Range(0f, 0.5f)]
        public float eliteMutationChancePerFloor = 0.05f;

        [Tooltip("精英怪体积放大系数")]
        [Range(1f, 3f)]
        public float eliteScaleMultiplier = 1.5f;

        [Tooltip("精英怪属性膨胀最小倍率")]
        [Min(1f)]
        public float eliteStatMultiplierMin = 3.0f;

        [Tooltip("精英怪属性膨胀最大倍率")]
        [Min(1f)]
        public float eliteStatMultiplierMax = 5.0f;

        [Tooltip("精英怪经验倍率")]
        [Min(1f)]
        public float eliteExpMultiplier = 5.0f;

        [Tooltip("精英怪最少隐藏特性数")]
        [Min(0)]
        public int eliteMinHiddenTraits = 1;

        [Tooltip("精英怪最多隐藏特性数")]
        [Min(1)]
        public int eliteMaxHiddenTraits = 3;

        // =====================================================================
        //  装备品质掉落权重
        // =====================================================================

        [Header("=== 装备品质掉落权重 ===")]
        [Range(0f, 1f)] public float dropWeightCommon = 0.60f;
        [Range(0f, 1f)] public float dropWeightRare = 0.25f;
        [Range(0f, 1f)] public float dropWeightEpic = 0.10f;
        [Range(0f, 1f)] public float dropWeightLegendary = 0.04f;
        [Range(0f, 1f)] public float dropWeightMythic = 0.009f;
        [Range(0f, 1f)] public float dropWeightPrismatic = 0.001f;

        // =====================================================================
        //  铁匠强化
        // =====================================================================

        [Header("=== 铁匠强化 ===")]
        [Tooltip("强化 +16 及以上的永久锁死保底成功率")]
        [Range(0f, 1f)]
        public float enhanceFloorRate = 0.05f;

        // =====================================================================
        //  经验值曲线
        // =====================================================================

        [Header("=== 经验值曲线 ===")]
        [Tooltip("初始升级所需经验值")]
        [Min(1)]
        public int baseExpRequired = 100;

        [Tooltip("经验曲线线性递增系数")]
        [Min(0)]
        public int expLinearIncrement = 20;

        [Tooltip("升级后全基础属性强制增加值")]
        [Min(0)]
        public int levelUpStatBonus = 1;

        // =====================================================================
        //  房间分布权重
        // =====================================================================

        [Header("=== 房间分布权重 ===")]
        [Range(0f, 1f)] public float roomWeightCombat = 0.70f;
        [Range(0f, 1f)] public float roomWeightTreasure = 0.10f;
        [Range(0f, 1f)] public float roomWeightEvent = 0.20f;

        // =====================================================================
        //  血月死神 & 战斗疲劳
        // =====================================================================

        [Header("=== 血月死神 & 疲劳 ===")]
        [Tooltip("同层驻留触发血月死神的时限（秒）")]
        [Min(60f)]
        public float reaperTriggerTimeSeconds = 45f * 60f;

        [Tooltip("疲劳层数达到等效秒杀的阈值")]
        [Min(1)]
        public int fatigueLethalStacks = 10;

        // =====================================================================
        //  指令缓冲 & 生成限制
        // =====================================================================

        [Header("=== 指令缓冲 ===")]
        [Tooltip("每帧最大掉落物实例化数量（防内存熔断）")]
        [Min(1)]
        public int maxLootSpawnsPerFrame = 3;

        // =====================================================================
        //  经验值掉落基准
        // =====================================================================

        [Header("=== 经验值掉落 ===")]
        [Min(1)] public int mobBaseExpMin = 3;
        [Min(1)] public int mobBaseExpMax = 5;
        [Min(1)] public int bruteBaseExpMin = 8;
        [Min(1)] public int bruteBaseExpMax = 12;
        [Min(1)] public int bossBaseExpMin = 150;
        [Min(1)] public int bossBaseExpMax = 200;

        // =====================================================================
        //  暴击参数
        // =====================================================================

        [Header("=== 暴击参数 ===")]
        [Tooltip("暴击伤害基础倍率下限")]
        [Min(1f)]
        public float baseCritMultiplierMin = 1.4f;

        [Tooltip("暴击伤害基础倍率上限")]
        [Min(1f)]
        public float baseCritMultiplierMax = 1.5f;
    }
}
