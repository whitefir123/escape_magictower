// ============================================================================
// 逃离魔塔 - 全局常量定义
// 所有业务数值的硬上限、概率基数、公式参数等集中管理。
// 严禁在 .cs 文件中 hardcode 数值，必须引用本文件或 ScriptableObject。
//
// 架构说明：
//   - 系统级硬约束（const）：编译期不可变，如减伤硬上限、熔断器阈值
//   - 平衡性数值（属性）：运行时从 BalanceConfig_SO 读取，未配置时返回默认值
//     调用方无感知，仍通过 GameConstants.XXX 访问
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower
{
    /// <summary>
    /// 全局游戏常量 —— 系统级硬约束（const）+ 平衡性数值（SO 可配置）
    /// </summary>
    public static class GameConstants
    {
        // =====================================================================
        //  SO 配置源（运行时由 GameBootstrap 或初始化器注入）
        // =====================================================================

        private static BalanceConfig_SO _config;

        /// <summary>
        /// 初始化平衡性配置（游戏启动时调用一次）
        /// 未调用时所有属性返回与原 const 相同的默认值
        /// </summary>
        public static void Initialize(BalanceConfig_SO config)
        {
            _config = config;
        }

        // =====================================================================
        // 属性钳制硬上限 (Hard Caps) — 系统不可变
        // 来源：DesignDocs/13_Architecture_and_Operations_SLA.md
        // =====================================================================

        /// <summary>减伤率绝对硬上限（防御堆再高也不可能达到 100% 免伤）</summary>
        public const float MAX_DAMAGE_REDUCTION = 0.99f;

        /// <summary>力量破法：物攻→额外魔穿的绝对上限</summary>
        public const float MAX_CROSS_MAGIC_PEN = 0.20f;

        /// <summary>魔力附刃：魔攻→额外物穿的绝对上限</summary>
        public const float MAX_CROSS_ARMOR_PEN = 0.20f;

        /// <summary>重甲霸体：物防→额外控制抗性的绝对上限</summary>
        public const float MAX_CROSS_CC_RESIST = 0.40f;

        /// <summary>魔场感知：魔防→额外暴击率的绝对上限</summary>
        public const float MAX_CROSS_CRIT_RATE = 0.15f;

        // =====================================================================
        // 跨界协同方程参数 — 系统不可变
        // 来源：DesignDocs/02_Entities_and_Stats.md 第 1.4 节
        // =====================================================================

        /// <summary>力量破法公式分母常数：ATK / (ATK + 此值) * 20%</summary>
        public const float CROSS_MAGIC_PEN_DIVISOR = 500f;

        /// <summary>魔力附刃公式分母常数：MATK / (MATK + 此值) * 20%</summary>
        public const float CROSS_ARMOR_PEN_DIVISOR = 500f;

        /// <summary>重甲霸体公式分母常数：DEF / (DEF + 此值) * 40%</summary>
        public const float CROSS_CC_RESIST_DIVISOR = 1000f;

        /// <summary>魔场感知公式分母常数：MDEF / (MDEF + 此值) * 15%</summary>
        public const float CROSS_CRIT_RATE_DIVISOR = 800f;

        // =====================================================================
        // 被动触发熔断器 — 系统不可变
        // 来源：DesignDocs/13_Architecture_and_Operations_SLA.md
        // =====================================================================

        /// <summary>事件嵌套 Generation 上限，超过此值立即熔断</summary>
        public const int MAX_EVENT_GENERATION = 3;

        // =====================================================================
        // 以下为平衡性数值 —— 运行时从 BalanceConfig_SO 读取
        // 未初始化时返回与原 const 相同的默认值
        // =====================================================================

        // === 符文系统 ===
        public static float RUNE_DROP_CHANCE => _config != null ? _config.runeDropChance : 0.40f;
        public static int RUNE_PITY_THRESHOLD => _config != null ? _config.runePityThreshold : 15;
        public static float CLASS_RUNE_FORCE_CHANCE => _config != null ? _config.classRuneForceChance : 0.10f;

        // === 符文稀有度权重 ===
        public static float RUNE_WEIGHT_COMMON => _config != null ? _config.runeWeightCommon : 0.65f;
        public static float RUNE_WEIGHT_RARE => _config != null ? _config.runeWeightRare : 0.25f;
        public static float RUNE_WEIGHT_EXCEPTIONAL => _config != null ? _config.runeWeightExceptional : 0.06f;
        public static float RUNE_WEIGHT_EPIC => _config != null ? _config.runeWeightEpic : 0.035f;
        public static float RUNE_WEIGHT_LEGENDARY => _config != null ? _config.runeWeightLegendary : 0.005f;

        // === 精英突变 ===
        public static float ELITE_BASE_MUTATION_CHANCE => _config != null ? _config.eliteBaseMutationChance : 0.05f;
        public static float ELITE_MUTATION_CHANCE_PER_FLOOR => _config != null ? _config.eliteMutationChancePerFloor : 0.05f;
        public static float ELITE_SCALE_MULTIPLIER => _config != null ? _config.eliteScaleMultiplier : 1.5f;
        public static float ELITE_STAT_MULTIPLIER_MIN => _config != null ? _config.eliteStatMultiplierMin : 3.0f;
        public static float ELITE_STAT_MULTIPLIER_MAX => _config != null ? _config.eliteStatMultiplierMax : 5.0f;
        public static float ELITE_EXP_MULTIPLIER => _config != null ? _config.eliteExpMultiplier : 5.0f;
        public static int ELITE_MIN_HIDDEN_TRAITS => _config != null ? _config.eliteMinHiddenTraits : 1;
        public static int ELITE_MAX_HIDDEN_TRAITS => _config != null ? _config.eliteMaxHiddenTraits : 3;

        // === 装备品质掉落权重 ===
        public static float DROP_WEIGHT_COMMON => _config != null ? _config.dropWeightCommon : 0.60f;
        public static float DROP_WEIGHT_RARE => _config != null ? _config.dropWeightRare : 0.25f;
        public static float DROP_WEIGHT_EPIC => _config != null ? _config.dropWeightEpic : 0.10f;
        public static float DROP_WEIGHT_LEGENDARY => _config != null ? _config.dropWeightLegendary : 0.04f;
        public static float DROP_WEIGHT_MYTHIC => _config != null ? _config.dropWeightMythic : 0.009f;
        public static float DROP_WEIGHT_PRISMATIC => _config != null ? _config.dropWeightPrismatic : 0.001f;

        // === 铁匠强化 ===
        public static float ENHANCE_FLOOR_RATE => _config != null ? _config.enhanceFloorRate : 0.05f;

        // === 经验值曲线 ===
        public static int BASE_EXP_REQUIRED => _config != null ? _config.baseExpRequired : 100;
        public static int EXP_LINEAR_INCREMENT => _config != null ? _config.expLinearIncrement : 20;
        public static int LEVEL_UP_STAT_BONUS => _config != null ? _config.levelUpStatBonus : 1;

        // === 房间分布权重 ===
        public static float ROOM_WEIGHT_COMBAT => _config != null ? _config.roomWeightCombat : 0.70f;
        public static float ROOM_WEIGHT_TREASURE => _config != null ? _config.roomWeightTreasure : 0.10f;
        public static float ROOM_WEIGHT_EVENT => _config != null ? _config.roomWeightEvent : 0.20f;

        // === 血月死神 & 疲劳 ===
        public static float REAPER_TRIGGER_TIME_SECONDS => _config != null ? _config.reaperTriggerTimeSeconds : 45f * 60f;
        public static int FATIGUE_LETHAL_STACKS => _config != null ? _config.fatigueLethalStacks : 10;

        // === DOT 结算 ===
        // 来源：GameData_Blueprints/02_Status_Ailments.md
        public const float DOT_TICK_INTERVAL = 1.0f;                // DOT 结算间隔（秒）
        public const float BURN_PERCENT_MAX_HP = 0.05f;             // 灼烧每层每秒扣最大HP 5%
        public const float BLEED_MOVE_MULTIPLIER = 2.5f;            // 流血移动惩罚倍率
        public const float BURN_HEAL_REDUCTION_PER_STACK = 0.10f;   // 灼烧每层降低治疗 10%
        public const float BURN_HEAL_REDUCTION_MAX = 0.60f;         // 灼烧最多降低治疗 60%

        // === 指令缓冲 ===
        public static int MAX_LOOT_SPAWNS_PER_FRAME => _config != null ? _config.maxLootSpawnsPerFrame : 3;

        // === 经验值掉落基准 ===
        public static int MOB_BASE_EXP_MIN => _config != null ? _config.mobBaseExpMin : 3;
        public static int MOB_BASE_EXP_MAX => _config != null ? _config.mobBaseExpMax : 5;
        public static int BRUTE_BASE_EXP_MIN => _config != null ? _config.bruteBaseExpMin : 8;
        public static int BRUTE_BASE_EXP_MAX => _config != null ? _config.bruteBaseExpMax : 12;
        public static int BOSS_BASE_EXP_MIN => _config != null ? _config.bossBaseExpMin : 150;
        public static int BOSS_BASE_EXP_MAX => _config != null ? _config.bossBaseExpMax : 200;

        // === 暴击参数 ===
        public static float BASE_CRIT_MULTIPLIER_MIN => _config != null ? _config.baseCritMultiplierMin : 1.4f;
        public static float BASE_CRIT_MULTIPLIER_MAX => _config != null ? _config.baseCritMultiplierMax : 1.5f;
    }
}
