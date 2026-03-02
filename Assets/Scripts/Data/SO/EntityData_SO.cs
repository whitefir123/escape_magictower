// ============================================================================
// 逃离魔塔 - 实体基础数据 ScriptableObject
// 所有战斗实体（英雄、怪物）的数据配置模板基类。
// 严禁在 .cs 代码中 hardcode 任何数值，全部从 SO 资产读取。
//
// 来源：DesignDocs/02_Entities_and_Stats.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 实体基础数据 SO —— 所有战斗参与者的共用配置模板
    /// </summary>
    [CreateAssetMenu(fileName = "NewEntityData", menuName = "EscapeTheTower/Data/EntityData")]
    public class EntityData_SO : ScriptableObject
    {
        [Header("基本信息")]
        [Tooltip("实体中文名称")]
        public string entityName;

        [Tooltip("实体英文标识（用于查表索引）")]
        public string entityID;

        [Header("基础面板属性")]
        [Tooltip("最大生命值")]
        public float baseMaxHP;

        [Tooltip("最大法力值")]
        public float baseMaxMP;

        [Tooltip("物理攻击力")]
        public float baseATK;

        [Tooltip("魔法攻击力")]
        public float baseMATK;

        [Tooltip("物理防御力")]
        public float baseDEF;

        [Tooltip("魔法防御/抗性")]
        public float baseMDEF;

        [Header("进阶战斗属性")]
        [Tooltip("基础暴击率 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float baseCritRate;

        [Tooltip("暴击伤害倍率 (如 1.5 = 150%)")]
        public float baseCritMultiplier = 1.5f;

        [Tooltip("物理穿透率 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float baseArmorPen;

        [Tooltip("魔法穿透率 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float baseMagicPen;

        [Tooltip("闪避率 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float baseDodge;

        [Tooltip("移动速度 (基准 1.0)")]
        public float baseMoveSpeed = 1.0f;

        [Tooltip("攻击速度 (次/秒)")]
        public float baseAttackSpeed = 1.0f;

        [Header("资源系统")]
        [Tooltip("怒气上限")]
        public float baseMaxRage = 100f;

        [Tooltip("法力回复速度 (点/秒)")]
        public float baseManaRegen;

        /// <summary>
        /// 将 SO 中的基础属性填充到 StatBlock
        /// </summary>
        public virtual StatBlock CreateBaseStatBlock()
        {
            var stats = new StatBlock();
            stats.Set(StatType.MaxHP, baseMaxHP);
            stats.Set(StatType.HP, baseMaxHP);
            stats.Set(StatType.MaxMP, baseMaxMP);
            stats.Set(StatType.MP, baseMaxMP);
            stats.Set(StatType.ATK, baseATK);
            stats.Set(StatType.MATK, baseMATK);
            stats.Set(StatType.DEF, baseDEF);
            stats.Set(StatType.MDEF, baseMDEF);
            stats.Set(StatType.CritRate, baseCritRate);
            stats.Set(StatType.CritMultiplier, baseCritMultiplier);
            stats.Set(StatType.ArmorPen, baseArmorPen);
            stats.Set(StatType.MagicPen, baseMagicPen);
            stats.Set(StatType.Dodge, baseDodge);
            stats.Set(StatType.MoveSpeed, baseMoveSpeed);
            stats.Set(StatType.AttackSpeed, baseAttackSpeed);
            stats.Set(StatType.MaxRage, baseMaxRage);
            stats.Set(StatType.Rage, 0f);
            stats.Set(StatType.ManaRegen, baseManaRegen);
            return stats;
        }
    }
}
