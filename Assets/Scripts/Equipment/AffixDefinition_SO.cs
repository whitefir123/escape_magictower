// ============================================================================
// 逃离魔塔 - 词缀定义 (AffixDefinition_SO)
// 每条词缀对应一个 ScriptableObject 资产，在 Unity Inspector 中配置。
//
// 来源：GameData_Blueprints/06_Equipment_Affix_System.md
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 词缀定义 —— 描述一条前缀或后缀的静态属性规格
    /// 每条词缀创建一个 SO 资产，通过 AffixDatabase_SO 统一注册管理
    /// </summary>
    [CreateAssetMenu(fileName = "NewAffix", menuName = "EscapeTheTower/Affix Definition")]
    public class AffixDefinition_SO : ScriptableObject
    {
        [Header("基础标识")]
        [Tooltip("唯一标识符，如 prefix_attacking")]
        public string affixID;

        [Tooltip("前缀 / 后缀")]
        public AffixSlotType slotType;

        [Header("显示名称")]
        [Tooltip("中文名，如 '强攻的' 或 '...之吸血'")]
        public string nameCN;

        [Tooltip("英文名，如 'Attacking' 或 'of Life Steal'")]
        public string nameEN;

        [Header("适用部位")]
        [Tooltip("该词缀可以出现在哪些装备部位上")]
        public EquipmentSlot[] validSlots;

        [Header("属性修正")]
        [Tooltip("该词缀影响的属性类型")]
        public StatType bonusStat;

        [Tooltip("true = 百分比加成（乘算底座）；false = 固定值加成")]
        public bool isPercentage;

        [Tooltip("Roll 区间下限")]
        public float minValue;

        [Tooltip("Roll 区间上限")]
        public float maxValue;

        [Header("特殊属性（可选）")]
        [Tooltip("是否附带负面代价（如重刃的降低攻速）")]
        public bool hasPenalty;

        [Tooltip("负面代价影响的属性类型")]
        public StatType penaltyStat;

        [Tooltip("负面代价的固定值（如 -0.05 表示降低5%攻速）")]
        public float penaltyValue;

        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>
        /// 在 Roll 区间内随机生成一个词缀数值
        /// </summary>
        public float RollValue(System.Random rng)
        {
            float t = (float)rng.NextDouble();
            return Mathf.Lerp(minValue, maxValue, t);
        }

        /// <summary>
        /// 判断该词缀是否适用于指定部位
        /// </summary>
        public bool IsValidForSlot(EquipmentSlot slot)
        {
            if (validSlots == null || validSlots.Length == 0) return false;
            for (int i = 0; i < validSlots.Length; i++)
            {
                if (validSlots[i] == slot) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取显示用格式化字符串，如 "强攻的：物理攻击力 +(5%~15%)"
        /// </summary>
        public string GetDisplayString()
        {
            string suffix = isPercentage ? "%" : "";
            string prefix = slotType == AffixSlotType.Prefix ? nameCN : nameCN;
            return $"{prefix}：{bonusStat} +({minValue}{suffix}~{maxValue}{suffix})";
        }
    }
}
