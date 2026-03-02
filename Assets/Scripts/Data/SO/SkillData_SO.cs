// ============================================================================
// 逃离魔塔 - 技能数据 ScriptableObject
// 5 槽技能标准配置模板（2 被动 + 2 主动 + 1 大招）
//
// 来源：DesignDocs/03_Combat_System.md 第 1.2 节
//       GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 技能数据 SO —— 定义单个技能的全部配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "EscapeTheTower/Data/SkillData")]
    public class SkillData_SO : ScriptableObject
    {
        [Header("=== 基础信息 ===")]
        [Tooltip("技能名称")]
        public string skillName;

        [Tooltip("技能英文标识")]
        public string skillID;

        [Tooltip("技能槽位类型")]
        public SkillSlotType slotType;

        [Tooltip("技能描述")]
        [TextArea(2, 5)]
        public string description;

        [Header("=== 冷却与消耗 ===")]
        [Tooltip("冷却时间 (秒)，被动技为 0")]
        public float cooldown;

        [Tooltip("法力消耗")]
        public float manaCost;

        [Tooltip("是否消耗满怒气（大招专用）")]
        public bool requiresFullRage;

        [Header("=== 伤害参数 ===")]
        [Tooltip("基础固定伤害值")]
        public float baseDamage;

        [Tooltip("物理攻击力缩放系数 (如 0.8 表示 80% * ATK)")]
        public float atkScaling;

        [Tooltip("魔法攻击力缩放系数")]
        public float matkScaling;

        [Tooltip("伤害类型")]
        public DamageType damageType = DamageType.Physical;

        [Tooltip("元素属性")]
        public ElementType elementType = ElementType.None;

        [Header("=== 打击参数 ===")]
        [Tooltip("打击段数（多段技能）")]
        public int hitCount = 1;

        [Tooltip("每段间隔时间 (秒)")]
        public float hitInterval;

        [Header("=== AOE 参数 ===")]
        [Tooltip("是否为 AOE 技能")]
        public bool isAOE;

        [Tooltip("AOE 半径（身位单位）")]
        public float aoeRadius;

        [Tooltip("AOE 角度（扇形技 120° 等，360° = 全范围）")]
        public float aoeAngle = 360f;

        [Header("=== 特殊效果 ===")]
        [Tooltip("附带的状态效果类型（None 表示无）")]
        public StatusEffectType appliedEffect = StatusEffectType.Stun;

        [Tooltip("附带状态效果的持续时间 (秒)")]
        public float effectDuration;

        [Tooltip("施放期间是否拥有霸体")]
        public bool grantsSuperArmor;

        [Tooltip("施放期间是否免疫伤害")]
        public bool grantsInvincibility;

        [Tooltip("吸血比例 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float lifeStealPercent;

        [Header("=== 位移参数 ===")]
        [Tooltip("是否包含位移")]
        public bool hasMovement;

        [Tooltip("位移距离 (身位)")]
        public float movementDistance;

        [Header("=== 法力回复附带 ===")]
        [Tooltip("命中目标时额外回复的法力值")]
        public float manaRestoreOnHit;
    }
}
