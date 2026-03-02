// ============================================================================
// 逃离魔塔 - 全局枚举定义
// 所有系统共用的类型标识，集中管理，禁止在各模块内散落定义。
// ============================================================================

namespace EscapeTheTower
{
    /// <summary>
    /// 属性类型枚举 —— 覆盖所有面板与战斗属性
    /// </summary>
    public enum StatType
    {
        // === 主面板属性 ===
        HP,
        MaxHP,
        MP,
        MaxMP,
        ATK,        // 物理攻击
        MATK,       // 魔法攻击
        DEF,        // 物理防御
        MDEF,       // 魔法防御（魔法抗性）

        // === 战斗资源 ===
        Rage,       // 怒气值
        MaxRage,    // 怒气上限

        // === 进阶战斗百分比 ===
        CritRate,       // 暴击率
        CritMultiplier, // 暴击伤害倍率
        ArmorPen,       // 物理穿透率
        MagicPen,       // 魔法穿透率
        Dodge,          // 闪避率
        MoveSpeed,      // 移动速度
        AttackSpeed,    // 攻击速度

        // === 跨界协同派生属性（由管线层级5自动计算，不可直接赋值） ===
        BonusMagicPen,    // 力量破法：物攻→额外魔穿
        BonusArmorPen,    // 魔力附刃：魔攻→额外物穿
        BonusCCResist,    // 重甲霸体：物防→额外控制抗性
        BonusCritRate,    // 魔场感知：魔防→额外暴击率

        // === 回复系相关 ===
        ManaRegen,      // 法力回复速度（点/秒）
        LifeSteal,      // 物理吸血比例

        // === 杂项 ===
        MagicFind,      // 寻宝率（影响装备品质判定权重）
        ExpMultiplier,  // 经验倍率
        GoldMultiplier, // 金币倍率
    }

    /// <summary>
    /// 伤害类型枚举
    /// </summary>
    public enum DamageType
    {
        Physical,   // 物理伤害（走物防减免）
        Magical,    // 魔法伤害（走魔抗减免）
        True,       // 真实伤害（无视一切防御）
        Holy,       // 神圣伤害（特殊类型，部分怪物弱化/免疫）
    }

    /// <summary>
    /// 元素类型枚举 —— 五大基础元素
    /// </summary>
    public enum ElementType
    {
        None,
        Fire,       // 火（灼烧）
        Water,      // 水（潮湿）
        Ice,        // 冰（冰封）
        Lightning,  // 雷（感电）
        Poison,     // 毒（中毒）
    }

    /// <summary>
    /// 怪物种族/特性标签 —— 用于克制、天赋、装备触发条件
    /// </summary>
    [System.Flags]
    public enum MonsterTag
    {
        None        = 0,
        Undead      = 1 << 0,   // 亡灵
        Beast       = 1 << 1,   // 野兽
        Construct   = 1 << 2,   // 构装体
        Humanoid    = 1 << 3,   // 人型
        Elemental   = 1 << 4,   // 元素
        Demon       = 1 << 5,   // 恶魔
        Natural     = 1 << 6,   // 自然
        Flying      = 1 << 7,   // 飞行
        Spirit      = 1 << 8,   // 无实体/幽魂
        Elite       = 1 << 9,   // 精英（动态追加）
        Boss        = 1 << 10,  // 首领
        Tiny        = 1 << 11,  // 微型
        Melee       = 1 << 12,  // 近战
        Ranged      = 1 << 13,  // 远程
        Caster      = 1 << 14,  // 法师
        Assassin    = 1 << 15,  // 刺客
        Support     = 1 << 16,  // 辅助
        HeavyArmor  = 1 << 17,  // 重装
    }

    /// <summary>
    /// 装备品质层级 —— 七彩品质 + 山海
    /// </summary>
    public enum QualityTier
    {
        White,      // 普通 (Common)
        Green,      // 稀有 (Uncommon)
        Blue,       // 精良 (Rare)
        Purple,     // 史诗 (Epic)
        Yellow,     // 顶级 (Legendary)
        Red,        // 神话 (Mythic)
        Rainbow,    // 幻彩 (Prismatic)
        Shanhai,    // 山海神铸（独立体系）
    }

    /// <summary>
    /// 符文稀有度
    /// </summary>
    public enum RuneRarity
    {
        Common,         // 凡品
        Rare,           // 稀有
        Exceptional,    // 罕见
        Epic,           // 史诗
        Legendary,      // 传说
        Cursed,         // 诅咒（特殊事件产出）
    }

    /// <summary>
    /// 状态效果类型 —— 涵盖所有异常状态与增益
    /// </summary>
    public enum StatusEffectType
    {
        // === 伤害与折磨系 (DOT) ===
        Burn,           // 灼烧（无限叠加，百分比最大生命伤害）
        Poison,         // 中毒（无限叠加，固定毒伤）
        Bleed,          // 流血（无限叠加，移动加倍惩罚）

        // === 降维打击系 (Debuff) ===
        ArmorBreak,     // 破甲（无限叠加，扣除物防）
        MagicShred,     // 魔碎（无限叠加，扣除魔抗）
        Weakened,       // 虚弱（无限叠加，降低最终伤害）
        Slowed,         // 减速（有上限叠加至80%）
        Blind,          // 致盲（不可叠加，刷新时间）

        // === 控制系 (CC) ===
        Stun,           // 眩晕（不可叠加，抗性递减）
        Frozen,         // 冰封（不可叠加，抗性递减）
        Root,           // 定身（不可叠加）
        Silence,        // 沉默（不可叠加）
        Charm,          // 魅惑（不可叠加，极高抗性判定）

        // === 元素反应媒介 ===
        Wet,            // 潮湿（不可叠加，清除闪避率）
        Shock,          // 感电（不叠层数，刷新时间）

        // === 增益系 (Buff) ===
        Haste,          // 加速（不可叠加，最高位覆盖）
        Barrier,        // 护盾（数值叠加）
        Unstoppable,    // 霸体/狂暴（不可叠加，免疫控制）
    }

    /// <summary>
    /// 状态效果叠加规则类型
    /// </summary>
    public enum StackingRule
    {
        NonStackable,       // 不可叠加，刷新持续时间
        InfiniteStackable,  // 无限叠层，+1层并重置计时
        CappedStackable,    // 有上限叠加（如减速）
        ValueStackable,     // 数值叠加（如护盾）
    }

    /// <summary>
    /// 房间类型枚举
    /// </summary>
    public enum RoomType
    {
        Combat,     // 战斗遭遇房 (70%)
        Treasure,   // 纯奖励房 (10%)
        Merchant,   // 行商人
        Forge,      // 铁匠铺
        Gambler,    // 赌徒
        Arena,      // 挑战房（血斗场）
        Campfire,   // 休息室
        Trap,       // 地刺陷阱
        Shrine,     // 神龛/祭坛
        Stairs,     // 楼梯（层级切换）
        Boss,       // Boss 房
    }

    /// <summary>
    /// 装备槽位枚举 —— 严格 6 大槽位
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon,     // 武器
        Helmet,     // 头盔
        Armor,      // 护甲
        Gloves,     // 手套
        Boots,      // 鞋子
        Accessory,  // 饰品
    }

    /// <summary>
    /// 英雄职业枚举
    /// </summary>
    public enum HeroClass
    {
        VagabondSwordsman,  // 流浪剑客
        ArcaneApprentice,   // 奥术学徒
        ShadowAssassin,     // 暗影刺客
        DevoutPaladin,      // 虔诚圣骑
    }

    /// <summary>
    /// 元素反应类型 —— 七大复合反应
    /// </summary>
    public enum ElementalReactionType
    {
        None,
        Vaporize,       // 蒸发 = 灼烧(火) + 水 → 水伤翻倍
        Melt,           // 融化 = 灼烧(火) + 冰 → 超高乘区一击
        Overload,       // 超载 = 灼烧(火) + 雷 → 无视防御真实AOE爆炸
        Freeze,         // 冻结 = 潮湿(水) + 冰 → 无视抗性强行冰封
        ElectroCharged, // 感电连营 = 潮湿(水) + 雷 → 持续链式雷伤
        Shatter,        // 碎冰 = 冰封(冰) + 火/重击 → 巨额物理伤害
        VenomBlast,     // 毒爆术 = 中毒(毒) + 火 → 清空毒层AOE绿火
    }

    /// <summary>
    /// 技能槽位类型
    /// </summary>
    public enum SkillSlotType
    {
        Passive1,       // 被动一
        Passive2,       // 被动二
        Active1,        // 主动小技能一
        Active2,        // 主动小技能二
        Ultimate,       // 终极必杀技（怒气驱动）
    }

    /// <summary>
    /// 符文获取途径
    /// </summary>
    public enum RuneAcquisitionType
    {
        KillDrop,       // 击杀掉落（属性符文，40% 概率）
        LevelUp,        // 升级获取（机制符文，必定触发）
    }

    /// <summary>
    /// 实体阵营
    /// </summary>
    public enum Faction
    {
        Player,
        Enemy,
        Neutral,    // 中立NPC（商人、铁匠等）
    }
}
