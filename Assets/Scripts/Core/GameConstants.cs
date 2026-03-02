// ============================================================================
// 逃离魔塔 - 全局常量定义
// 所有业务数值的硬上限、概率基数、公式参数等集中管理。
// 严禁在 .cs 文件中 hardcode 数值，必须引用本文件或 ScriptableObject。
// ============================================================================

namespace EscapeTheTower
{
    /// <summary>
    /// 全局游戏常量 —— 所有不可通过数据表变更的系统级硬约束
    /// </summary>
    public static class GameConstants
    {
        // =====================================================================
        // 属性钳制硬上限 (Hard Caps)
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
        // 跨界协同方程参数
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
        // 被动触发熔断器
        // 来源：DesignDocs/13_Architecture_and_Operations_SLA.md
        // =====================================================================

        /// <summary>事件嵌套 Generation 上限，超过此值立即熔断</summary>
        public const int MAX_EVENT_GENERATION = 3;

        // =====================================================================
        // 符文系统参数
        // 来源：DesignDocs/05_Runes_and_MetaProgression.md
        // =====================================================================

        /// <summary>击杀怪物触发属性符文三选一的概率</summary>
        public const float RUNE_DROP_CHANCE = 0.40f;

        /// <summary>机制符文保底计数阈值（连续 N 次未出史诗+触发保底）</summary>
        public const int RUNE_PITY_THRESHOLD = 15;

        /// <summary>职业专属符文强制插队概率</summary>
        public const float CLASS_RUNE_FORCE_CHANCE = 0.10f;

        // =====================================================================
        // 机制符文稀有度基础抽取权重
        // 来源：GameData_Blueprints/08_Destiny_Rune_System.md
        // =====================================================================

        /// <summary>凡品机制符文基础权重</summary>
        public const float RUNE_WEIGHT_COMMON = 0.55f;

        /// <summary>稀有机制符文基础权重</summary>
        public const float RUNE_WEIGHT_RARE = 0.30f;

        /// <summary>罕见机制符文基础权重</summary>
        public const float RUNE_WEIGHT_EXCEPTIONAL = 0.11f;

        /// <summary>史诗机制符文基础权重</summary>
        public const float RUNE_WEIGHT_EPIC = 0.035f;

        /// <summary>传说机制符文基础权重</summary>
        public const float RUNE_WEIGHT_LEGENDARY = 0.005f;

        // =====================================================================
        // 精英突变参数
        // 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md
        // =====================================================================

        /// <summary>第一层精英突变基础概率</summary>
        public const float ELITE_BASE_MUTATION_CHANCE = 0.05f;

        /// <summary>每深入一层精英概率递增量</summary>
        public const float ELITE_MUTATION_CHANCE_PER_FLOOR = 0.05f;

        /// <summary>精英怪体积放大系数</summary>
        public const float ELITE_SCALE_MULTIPLIER = 1.5f;

        /// <summary>精英怪属性膨胀最小倍率</summary>
        public const float ELITE_STAT_MULTIPLIER_MIN = 3.0f;

        /// <summary>精英怪属性膨胀最大倍率</summary>
        public const float ELITE_STAT_MULTIPLIER_MAX = 5.0f;

        /// <summary>精英怪经验倍率</summary>
        public const float ELITE_EXP_MULTIPLIER = 5.0f;

        /// <summary>精英怪最少隐藏特性数</summary>
        public const int ELITE_MIN_HIDDEN_TRAITS = 1;

        /// <summary>精英怪最多隐藏特性数</summary>
        public const int ELITE_MAX_HIDDEN_TRAITS = 3;

        // =====================================================================
        // 装备品质掉落权重基准
        // 来源：DesignDocs/04_Equipment_and_Forge.md
        // =====================================================================

        /// <summary>白/绿装基础掉落权重</summary>
        public const float DROP_WEIGHT_COMMON = 0.60f;

        /// <summary>蓝装基础掉落权重</summary>
        public const float DROP_WEIGHT_RARE = 0.25f;

        /// <summary>紫装基础掉落权重</summary>
        public const float DROP_WEIGHT_EPIC = 0.10f;

        /// <summary>黄装基础掉落权重</summary>
        public const float DROP_WEIGHT_LEGENDARY = 0.04f;

        /// <summary>红装基础掉落权重</summary>
        public const float DROP_WEIGHT_MYTHIC = 0.009f;

        /// <summary>彩装基础掉落权重</summary>
        public const float DROP_WEIGHT_PRISMATIC = 0.001f;

        // =====================================================================
        // 铁匠强化概率阶梯
        // 来源：DesignDocs/04_Equipment_and_Forge.md
        // =====================================================================

        /// <summary>强化 +16 及以上的永久锁死保底成功率</summary>
        public const float ENHANCE_FLOOR_RATE = 0.05f;

        // =====================================================================
        // 经验值曲线参数
        // 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
        // =====================================================================

        /// <summary>初始升级所需经验值</summary>
        public const int BASE_EXP_REQUIRED = 100;

        /// <summary>经验曲线线性递增系数</summary>
        public const int EXP_LINEAR_INCREMENT = 20;

        /// <summary>升级后全基础属性强制增加值</summary>
        public const int LEVEL_UP_STAT_BONUS = 1;

        // =====================================================================
        // 房间分布权重
        // 来源：DesignDocs/06_Map_and_Modes.md
        // =====================================================================

        /// <summary>战斗遭遇房占比</summary>
        public const float ROOM_WEIGHT_COMBAT = 0.70f;

        /// <summary>纯奖励房占比</summary>
        public const float ROOM_WEIGHT_TREASURE = 0.10f;

        /// <summary>随机事件房占比</summary>
        public const float ROOM_WEIGHT_EVENT = 0.20f;

        // =====================================================================
        // 血月死神驻留惩罚
        // 来源：DesignDocs/06_Map_and_Modes.md
        // =====================================================================

        /// <summary>同层驻留触发血月死神的时限（秒）</summary>
        public const float REAPER_TRIGGER_TIME_SECONDS = 45f * 60f; // 45 分钟

        // =====================================================================
        // 战斗疲劳参数
        // 来源：DesignDocs/03_Combat_System.md
        // =====================================================================

        /// <summary>疲劳层数达到等效秒杀的阈值</summary>
        public const int FATIGUE_LETHAL_STACKS = 10;

        // =====================================================================
        // 指令缓冲安全阀
        // 来源：DesignDocs/13_Architecture_and_Operations_SLA.md
        // =====================================================================

        /// <summary>每帧最大掉落物实例化数量（防内存熔断）</summary>
        public const int MAX_LOOT_SPAWNS_PER_FRAME = 3;

        // =====================================================================
        // 经验值掉落基准
        // 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md
        // =====================================================================

        /// <summary>普通小怪基础经验最小值</summary>
        public const int MOB_BASE_EXP_MIN = 3;

        /// <summary>普通小怪基础经验最大值</summary>
        public const int MOB_BASE_EXP_MAX = 5;

        /// <summary>大型特殊怪基础经验最小值</summary>
        public const int BRUTE_BASE_EXP_MIN = 8;

        /// <summary>大型特殊怪基础经验最大值</summary>
        public const int BRUTE_BASE_EXP_MAX = 12;

        /// <summary>关底首领基础经验最小值</summary>
        public const int BOSS_BASE_EXP_MIN = 150;

        /// <summary>关底首领基础经验最大值</summary>
        public const int BOSS_BASE_EXP_MAX = 200;

        // =====================================================================
        // 暴击基础参数
        // 来源：DesignDocs/02_Entities_and_Stats.md
        // =====================================================================

        /// <summary>暴击伤害基础倍率下限</summary>
        public const float BASE_CRIT_MULTIPLIER_MIN = 1.4f;

        /// <summary>暴击伤害基础倍率上限</summary>
        public const float BASE_CRIT_MULTIPLIER_MAX = 1.5f;
    }
}
