// ============================================================================
// 逃离魔塔 - 装备实例数据 (EquipmentData)
// 描述一件具体掉落的装备的完整运行时数据。
// 纯 C# 可序列化类，不依赖 MonoBehaviour。
//
// 核心方法 ToStatBlock() 将底座属性 + 品质波动 + 词缀加成汇总为
// 一个 StatBlock，直接输入 AttributePipeline 层级3。
//
// 来源：DesignDocs/04_Equipment_and_Forge.md
//       GameData_Blueprints/06_Equipment_Affix_System.md
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 装备实例 —— 一件已生成的装备的全部数据
    /// </summary>
    [Serializable]
    public class EquipmentData
    {
        [Header("基础标识")]
        public string instanceID;          // 唯一实例 ID (GUID)
        public EquipmentSlot slot;         // 装备部位
        public QualityTier quality;        // 品质等级
        public int itemPower;              // iPwr (物品强度)

        [Header("底座属性1")]
        public StatType baseStat1Type;     // 底座属性1类型 (如 DEF)
        public float baseStat1Base;        // 底座属性1固定基础值
        public float baseStat1QualityBonus;// 底座属性1品质波动加成

        [Header("底座属性2")]
        public StatType baseStat2Type;     // 底座属性2类型 (如 MaxHP)
        public float baseStat2Base;        // 底座属性2固定基础值
        public float baseStat2QualityBonus;// 底座属性2品质波动加成

        [Header("词缀")]
        public List<AffixInstance> affixes = new List<AffixInstance>();

        [Header("镶孔")]
        public int socketCount;
        // public List<GemData> insertedGems; // 后续迭代

        // =====================================================================
        //  属性汇总
        // =====================================================================

        /// <summary>
        /// 将本装备的全部属性汇总为 StatBlock，供属性管线层级3使用
        /// 
        /// 计算顺序：
        /// 1. 底座属性 = baseStat_Base + baseStat_QualityBonus
        /// 2. 遍历词缀：百分比词缀对关联底座做乘算叠加，固定值词缀直接加算
        /// 3. 词缀的负面代价属性直接加算
        /// </summary>
        public StatBlock ToStatBlock()
        {
            var block = new StatBlock();

            // --- 步骤1：底座属性合计 ---
            float stat1Total = baseStat1Base + baseStat1QualityBonus;
            float stat2Total = baseStat2Base + baseStat2QualityBonus;

            block.Add(baseStat1Type, stat1Total);
            block.Add(baseStat2Type, stat2Total);

            // --- 步骤2：遍历词缀 ---
            if (affixes != null)
            {
                foreach (var affix in affixes)
                {
                    if (affix == null || affix.definition == null) continue;

                    var def = affix.definition;

                    if (def.isPercentage)
                    {
                        // 百分比词缀：检查是否作用于本装备的底座属性
                        // 如果词缀的 bonusStat 与底座属性类型匹配，基于底座总值做百分比
                        // 否则将百分比值作为独立属性修正加入
                        float percentValue = affix.rolledValue / 100f;
                        float baseForCalc = 0f;

                        if (def.bonusStat == baseStat1Type)
                            baseForCalc = stat1Total;
                        else if (def.bonusStat == baseStat2Type)
                            baseForCalc = stat2Total;

                        if (baseForCalc > 0f)
                        {
                            // 基于底座乘算：(底座值 * 百分比) 作为额外加成
                            block.Add(def.bonusStat, baseForCalc * percentValue);
                        }
                        else
                        {
                            // 全局百分比修正：存入百分比类属性
                            // 由于 StatBlock 为加算模型，百分比在管线终端处理
                            block.Add(def.bonusStat, affix.rolledValue);
                        }
                    }
                    else
                    {
                        // 固定值词缀：直接加算
                        block.Add(def.bonusStat, affix.rolledValue);
                    }

                    // --- 步骤3：处理词缀负面代价 ---
                    if (def.hasPenalty)
                    {
                        block.Add(def.penaltyStat, def.penaltyValue);
                    }
                }
            }

            return block;
        }

        // =====================================================================
        //  显示辅助
        // =====================================================================

        /// <summary>
        /// 获取装备的显示名称（组合全部前后缀 + 部位基础名）
        /// 格式：前缀1·前缀2的部位名之后缀1·后缀2
        /// 例如：抗魔·闪避的鞋靴之贪婪
        /// </summary>
        public string GetDisplayName()
        {
            string baseName = GetSlotBaseName();

            var prefixes = new List<string>();
            var suffixes = new List<string>();

            if (affixes != null)
            {
                foreach (var affix in affixes)
                {
                    if (affix?.definition == null) continue;
                    string name = CleanAffixName(affix.definition.nameCN, affix.definition.slotType);
                    if (affix.definition.slotType == AffixSlotType.Prefix)
                        prefixes.Add(name);
                    else
                        suffixes.Add(name);
                }
            }

            var sb = new StringBuilder();

            // 前缀部分：用"·"连接，最后一个保留"的"字
            if (prefixes.Count > 0)
            {
                sb.Append(string.Join("·", prefixes));
                sb.Append("的");
            }

            // 部位基础名
            sb.Append(baseName);

            // 后缀部分：第一个用"之"连接，余下用"·"连接
            if (suffixes.Count > 0)
            {
                sb.Append("之");
                sb.Append(string.Join("·", suffixes));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 清理词缀名称：去除前缀的"的"字和后缀的"…之"前缀
        /// 例如："抢魔的" → "抗魔"， "…之贪婪" → "贪婪"
        /// </summary>
        private static string CleanAffixName(string rawName, AffixSlotType slotType)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;

            string name = rawName;

            if (slotType == AffixSlotType.Prefix)
            {
                // 去除末尾的"的"字（由 GetDisplayName 统一添加）
                if (name.EndsWith("的")) name = name[..^1];
            }
            else
            {
                // 去除前导的省略号和"之"字（由 GetDisplayName 统一添加）
                if (name.StartsWith("…之")) name = name[2..];
                else if (name.StartsWith("...之")) name = name[4..];
                else if (name.StartsWith("…")) name = name[1..];
            }

            return name;
        }

        /// <summary>
        /// 获取部位基础名称
        /// </summary>
        public string GetSlotBaseName()
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "武器",
                EquipmentSlot.Helmet => "头盔",
                EquipmentSlot.Armor => "胸甲",
                EquipmentSlot.Gloves => "手套",
                EquipmentSlot.Boots => "鞋靴",
                EquipmentSlot.Accessory => "首饰",
                _ => "未知装备",
            };
        }

        /// <summary>
        /// 获取品质对应颜色（用于 UI 边框/文字着色）
        /// </summary>
        public static Color GetQualityColor(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => new Color(0.8f, 0.8f, 0.8f),     // 灰白
                QualityTier.Green => new Color(0.2f, 0.9f, 0.2f),     // 翠绿
                QualityTier.Blue => new Color(0.3f, 0.5f, 1.0f),      // 亮蓝
                QualityTier.Purple => new Color(0.7f, 0.3f, 0.9f),    // 紫色
                QualityTier.Yellow => new Color(1.0f, 0.85f, 0.0f),   // 金黄
                QualityTier.Red => new Color(1.0f, 0.2f, 0.1f),       // 赤红
                QualityTier.Rainbow => new Color(1.0f, 0.5f, 0.8f),   // 幻彩粉
                QualityTier.Shanhai => new Color(0.0f, 1.0f, 0.9f),   // 青金
                _ => Color.white,
            };
        }

        /// <summary>
        /// 调试输出装备完整信息
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[{quality}] {GetDisplayName()} | {slot} iPwr={itemPower} 镶孔={socketCount}");
            sb.Append($" | 底座: {StatCN(baseStat1Type)}={baseStat1Base}+{baseStat1QualityBonus}(品质), {StatCN(baseStat2Type)}={baseStat2Base}+{baseStat2QualityBonus}(品质)");

            if (affixes != null && affixes.Count > 0)
            {
                sb.Append($" | 词缀({affixes.Count}): ");
                for (int i = 0; i < affixes.Count; i++)
                {
                    var affix = affixes[i];
                    if (affix?.definition == null) continue;
                    string cleanName = CleanAffixName(affix.definition.nameCN, affix.definition.slotType);
                    string unit = affix.definition.isPercentage ? "%" : "";
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{cleanName}({StatCN(affix.definition.bonusStat)}+{affix.rolledValue:F1}{unit})");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// StatType 英文+中文格式（Console 调试用，正式版仅显示中文）
        /// </summary>
        public static string StatCN(StatType stat)
        {
            return stat switch
            {
                StatType.HP => "HP生命",
                StatType.MaxHP => "MaxHP最大生命",
                StatType.MP => "MP法力",
                StatType.MaxMP => "MaxMP最大法力",
                StatType.ATK => "ATK物攻",
                StatType.MATK => "MATK魔攻",
                StatType.DEF => "DEF物防",
                StatType.MDEF => "MDEF魔抗",
                StatType.CritRate => "CritRate暴击率",
                StatType.CritMultiplier => "CritMul暴伤倍率",
                StatType.ArmorPen => "ArmorPen穿透",
                StatType.Dodge => "Dodge闪避",
                StatType.MoveSpeed => "MoveSpd移速",
                StatType.AttackSpeed => "AtkSpd攻速",
                StatType.LifeSteal => "LifeSteal吸血",
                StatType.ManaRegen => "ManaRegen回蓝",
                StatType.GoldMultiplier => "GoldMul金币倍率",
                StatType.BonusCCResist => "CCResist控制抗性",
                _ => stat.ToString(),
            };
        }
    }

    /// <summary>
    /// 词缀实例 —— 一条已 Roll 出具体数值的词缀
    /// </summary>
    [Serializable]
    public class AffixInstance
    {
        [Tooltip("引用的词缀定义 SO 资产")]
        public AffixDefinition_SO definition;

        [Tooltip("在 [minValue, maxValue] 区间内 Roll 出的实际数值")]
        public float rolledValue;

        public AffixInstance() { }

        public AffixInstance(AffixDefinition_SO def, float value)
        {
            definition = def;
            rolledValue = value;
        }
    }
}
