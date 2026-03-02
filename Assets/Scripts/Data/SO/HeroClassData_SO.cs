// ============================================================================
// 逃离魔塔 - 英雄职业数据 ScriptableObject
// 继承 EntityData_SO，额外包含职业专属配置。
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 法力回复模式 —— 不同职业获取法力的方式不同
    /// </summary>
    public enum ManaRegenMode
    {
        /// <summary>自然回复（法师：高额基础回复速度）</summary>
        NaturalRegen,

        /// <summary>普攻回蓝（剑客：每次普攻命中恢复固定法力）</summary>
        AttackRegen,

        /// <summary>受击回蓝（骑士：每次被攻击时恢复固定法力）</summary>
        OnHitRegen,

        /// <summary>混合回复（刺客：低额自然回复 + 普攻回复少量）</summary>
        HybridRegen,
    }

    /// <summary>
    /// 英雄职业数据 SO —— 继承 EntityData_SO，包含职业特性
    /// </summary>
    [CreateAssetMenu(fileName = "NewHeroClassData", menuName = "EscapeTheTower/Data/HeroClassData")]
    public class HeroClassData_SO : EntityData_SO
    {
        [Header("=== 职业标识 ===")]
        [Tooltip("职业枚举")]
        public HeroClass heroClass;

        [Header("=== 法力回复机制 ===")]
        [Tooltip("法力回复模式")]
        public ManaRegenMode manaRegenMode;

        [Tooltip("普攻命中回蓝量（仅 AttackRegen/HybridRegen 模式有效）")]
        public float manaPerAttack;

        [Tooltip("受击回蓝量（仅 OnHitRegen 模式有效）")]
        public float manaOnHit;

        [Header("=== 专属闪避 ===")]
        [Tooltip("闪避技能名称")]
        public string evasionName;

        [Tooltip("闪避冷却时间 (秒)")]
        public float evasionCooldown;

        [Tooltip("闪避无敌帧持续时间 (秒)")]
        public float evasionInvincibleDuration;

        [Tooltip("闪避法力消耗")]
        public float evasionManaCost;

        [Header("=== 经验系统 ===")]
        [Tooltip("初始等级")]
        public int startingLevel = 1;

        [Tooltip("初始经验值")]
        public int startingExp = 0;

        [Header("=== 技能引用 ===")]
        [Tooltip("技能数据列表（5 槽：2被动 + 2主动 + 1大招）")]
        public List<SkillData_SO> skills = new List<SkillData_SO>();
    }
}
