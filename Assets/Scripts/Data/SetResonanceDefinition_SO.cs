// ============================================================================
// 逃离魔塔 - 套装共鸣定义 SO (SetResonanceDefinition_SO)
// 数据驱动的套装共鸣配置。每套一个 SO 资产。
// Inspector 中可直接编辑词缀要求和数值参数。
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三
// ============================================================================

using System;
using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance
{
    /// <summary>
    /// 单个部位的词缀匹配要求
    /// 该部位装备只要包含 option1 或 option2 中任意一个词缀名即视为点亮
    /// </summary>
    [Serializable]
    public class SlotAffixRequirement
    {
        [Tooltip("装备部位")]
        public EquipmentSlot slot;

        [Tooltip("候选词缀名A（中文原始名，含'的'或'…之'）")]
        public string affixOption1;

        [Tooltip("候选词缀名B")]
        public string affixOption2;
    }

    /// <summary>
    /// 套装共鸣定义 —— ScriptableObject
    /// 每套一个资产，包含词缀匹配条件和被动实现类名
    /// </summary>
    [CreateAssetMenu(fileName = "NewSetResonance", menuName = "EscapeTheTower/Set Resonance Definition")]
    public class SetResonanceDefinition_SO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("套装显示名称")]
        public string setNameCN;

        [Tooltip("套装英文标识")]
        public string setNameEN;

        [Tooltip("套装描述")]
        [TextArea(2, 4)]
        public string description;

        [Header("词缀匹配条件（6 个部位）")]
        [Tooltip("6 个部位各自的候选词缀要求")]
        public SlotAffixRequirement[] slotRequirements = new SlotAffixRequirement[6];

        [Header("共鸣效果参数（供被动实现读取）")]
        [Tooltip("自定义浮点参数，由各被动实现自行解读")]
        public float[] parameters = new float[10];

        [Header("被动实现")]
        [Tooltip("ISetPassive 实现类的完整类型名（含命名空间）")]
        public string passiveClassName;

        /// <summary>
        /// 统计给定装备组合中点亮了多少个部位
        /// </summary>
        /// <param name="equippedItems">6 部位的装备数组（可含 null）</param>
        /// <returns>点亮的部位数量（0~6）</returns>
        public int CountMatchedSlots(EquipmentData[] equippedItems)
        {
            if (slotRequirements == null || equippedItems == null) return 0;

            int matched = 0;

            foreach (var req in slotRequirements)
            {
                if (req == null) continue;

                // 在装备数组中找到对应部位的装备
                EquipmentData slotEquip = null;
                foreach (var item in equippedItems)
                {
                    if (item != null && item.slot == req.slot)
                    {
                        slotEquip = item;
                        break;
                    }
                }

                if (slotEquip == null) continue;

                // 检查该装备的词缀是否包含候选词缀之一
                if (HasMatchingAffix(slotEquip, req.affixOption1) ||
                    HasMatchingAffix(slotEquip, req.affixOption2))
                {
                    matched++;
                }
            }

            return matched;
        }

        /// <summary>
        /// 检查装备是否包含指定名称的词缀
        /// 匹配规则：词缀定义的 nameCN 包含目标名称（模糊匹配，兼容清理后名称）
        /// </summary>
        private static bool HasMatchingAffix(EquipmentData equipment, string affixName)
        {
            if (string.IsNullOrEmpty(affixName)) return false;
            if (equipment.affixes == null) return false;

            // 清理匹配名（去掉可能的前后缀标记）
            string cleanTarget = CleanAffixMatchName(affixName);

            foreach (var affix in equipment.affixes)
            {
                if (affix?.definition == null) continue;
                string cleanAffix = CleanAffixMatchName(affix.definition.nameCN);
                if (cleanAffix == cleanTarget) return true;
            }

            return false;
        }

        /// <summary>
        /// 清理词缀名用于匹配（去除"的"、"…之"等装饰符）
        /// </summary>
        private static string CleanAffixMatchName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";

            string name = rawName.Trim();

            // 去除前导装饰
            if (name.StartsWith("…之")) name = name[2..];
            else if (name.StartsWith("...之")) name = name[4..];
            else if (name.StartsWith("…")) name = name[1..];

            // 去除后缀装饰
            if (name.EndsWith("的")) name = name[..^1];

            return name;
        }
    }
}
