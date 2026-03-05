// ============================================================================
// 逃离魔塔 - 词缀资产自动生成器 (Editor Only)
// 一键根据 GameData_Blueprints/06_Equipment_Affix_System.md 生成所有词缀 SO
// 和词缀数据库 SO，免去手动在 Inspector 中逐条配置。
//
// 使用方式：Unity 顶部菜单 → EscapeTheTower → 生成全部词缀资产
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using EscapeTheTower;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.Editor
{
    /// <summary>
    /// 词缀资产自动生成器 —— 基于设计文档批量创建 AffixDefinition_SO 和 AffixDatabase_SO
    /// </summary>
    public static class AffixAssetGenerator
    {
        // 输出目录
        private const string OUTPUT_DIR = "Assets/Data/Equipment/Affixes";
        private const string DB_PATH = "Assets/Data/Equipment/AffixDatabase.asset";

        [MenuItem("EscapeTheTower/生成全部词缀资产", priority = 100)]
        public static void GenerateAll()
        {
            // 确保目录存在
            EnsureDirectory(OUTPUT_DIR);
            EnsureDirectory("Assets/Data/Equipment");

            var allAffixes = new List<AffixDefinition_SO>();

            // ================================================================
            //  武器词缀池（来源：06 文档 §二）
            // ================================================================
            allAffixes.Add(CreateAffix("prefix_attacking", AffixSlotType.Prefix,
                "强攻的", "Attacking",
                new[] { EquipmentSlot.Weapon },
                StatType.ATK, true, 5f, 15f));

            allAffixes.Add(CreateAffix("prefix_heavy_blade", AffixSlotType.Prefix,
                "重刃的", "Heavy-Blade",
                new[] { EquipmentSlot.Weapon },
                StatType.ATK, true, 16f, 30f,
                true, StatType.AttackSpeed, -0.05f)); // 负面代价：攻速-5%

            allAffixes.Add(CreateAffix("prefix_critical", AffixSlotType.Prefix,
                "会心的", "Critical",
                new[] { EquipmentSlot.Weapon },
                StatType.CritRate, true, 3f, 8f));

            allAffixes.Add(CreateAffix("prefix_deadly", AffixSlotType.Prefix,
                "致命的", "Deadly",
                new[] { EquipmentSlot.Weapon },
                StatType.CritMultiplier, true, 15f, 40f));

            // 附火：特殊机制词缀，用固定值表示额外火伤数值
            allAffixes.Add(CreateAffix("prefix_fire_enchanted", AffixSlotType.Prefix,
                "附火的", "Fire-Enchanted",
                new[] { EquipmentSlot.Weapon },
                StatType.ATK, false, 10f, 30f)); // 额外火伤以ATK占位

            // 附雷：特殊机制词缀
            allAffixes.Add(CreateAffix("prefix_lightning_enchanted", AffixSlotType.Prefix,
                "附雷的", "Lightning-Enchanted",
                new[] { EquipmentSlot.Weapon },
                StatType.MATK, false, 20f, 50f)); // 闪电链伤以MATK占位

            allAffixes.Add(CreateAffix("prefix_armor_piercing", AffixSlotType.Prefix,
                "破甲的", "Armor-Piercing",
                new[] { EquipmentSlot.Weapon },
                StatType.ArmorPen, true, 10f, 25f));

            // 武器后缀
            allAffixes.Add(CreateAffix("suffix_life_steal", AffixSlotType.Suffix,
                "吸血", "of Life Steal",
                new[] { EquipmentSlot.Weapon },
                StatType.LifeSteal, true, 0.5f, 1.5f));

            // 斩杀：特殊机制词缀，用ATK百分比占位
            allAffixes.Add(CreateAffix("suffix_executing", AffixSlotType.Suffix,
                "斩杀", "of Executing",
                new[] { EquipmentSlot.Weapon },
                StatType.ATK, true, 20f, 40f));

            allAffixes.Add(CreateAffix("suffix_haste", AffixSlotType.Suffix,
                "急速", "of Haste",
                new[] { EquipmentSlot.Weapon },
                StatType.AttackSpeed, true, 10f, 20f));

            // 冰缓：特殊机制词缀，用MDEF占位
            allAffixes.Add(CreateAffix("suffix_chilling", AffixSlotType.Suffix,
                "冰缓", "of Chilling",
                new[] { EquipmentSlot.Weapon },
                StatType.MDEF, false, 1f, 1f)); // 固定15%几率，数值象征性

            // 连击：特殊机制词缀
            allAffixes.Add(CreateAffix("suffix_comboing", AffixSlotType.Suffix,
                "连击", "of Comboing",
                new[] { EquipmentSlot.Weapon },
                StatType.ATK, true, 50f, 50f)); // 50%基础攻击力真伤

            allAffixes.Add(CreateAffix("suffix_mana_steal", AffixSlotType.Suffix,
                "回蓝", "of Mana Steal",
                new[] { EquipmentSlot.Weapon },
                StatType.ManaRegen, true, 3f, 5f));

            // ================================================================
            //  防具词缀池（来源：06 文档 §三）
            // ================================================================
            var armorAll = new[] { EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots };
            var headChest = new[] { EquipmentSlot.Helmet, EquipmentSlot.Armor };
            var chestGloves = new[] { EquipmentSlot.Armor, EquipmentSlot.Gloves };
            var bootsGloves = new[] { EquipmentSlot.Boots, EquipmentSlot.Gloves };

            allAffixes.Add(CreateAffix("prefix_sturdy", AffixSlotType.Prefix,
                "坚固的", "Sturdy",
                armorAll,
                StatType.DEF, true, 10f, 20f));

            allAffixes.Add(CreateAffix("prefix_robust", AffixSlotType.Prefix,
                "壮容的", "Robust",
                headChest,
                StatType.MaxHP, true, 15f, 30f));

            allAffixes.Add(CreateAffix("prefix_resistant", AffixSlotType.Prefix,
                "抗魔的", "Resistant",
                armorAll,
                StatType.MDEF, true, 15f, 30f));

            allAffixes.Add(CreateAffix("prefix_elusive", AffixSlotType.Prefix,
                "闪避的", "Elusive",
                bootsGloves,
                StatType.Dodge, true, 3f, 8f));

            // 减伤：元素/魔法伤害减少，用MDEF百分比占位
            allAffixes.Add(CreateAffix("prefix_mitigating", AffixSlotType.Prefix,
                "减伤的", "Mitigating",
                headChest,
                StatType.MDEF, true, 8f, 15f));

            // 坚韧：硬控抗性，用BonusCCResist占位
            allAffixes.Add(CreateAffix("prefix_tenacious", AffixSlotType.Prefix,
                "坚韧的", "Tenacious",
                chestGloves,
                StatType.BonusCCResist, true, 20f, 40f));

            // 防具后缀
            allAffixes.Add(CreateAffix("suffix_movement", AffixSlotType.Suffix,
                "移速", "of Movement",
                new[] { EquipmentSlot.Boots },
                StatType.MoveSpeed, true, 8f, 18f));

            // 减CD：特殊机制词缀
            allAffixes.Add(CreateAffix("suffix_cooldown", AffixSlotType.Suffix,
                "减CD", "of Cooldown",
                new[] { EquipmentSlot.Helmet, EquipmentSlot.Gloves },
                StatType.AttackSpeed, true, 5f, 15f)); // 用攻速占位冷却缩减

            // 反伤：特殊机制词缀
            allAffixes.Add(CreateAffix("suffix_thorns", AffixSlotType.Suffix,
                "反伤", "of Thorns",
                new[] { EquipmentSlot.Armor, EquipmentSlot.Helmet },
                StatType.DEF, true, 10f, 20f)); // 反伤用DEF占位

            // 自愈：脱战回复
            allAffixes.Add(CreateAffix("suffix_healing", AffixSlotType.Suffix,
                "自愈", "of Healing",
                new[] { EquipmentSlot.Armor, EquipmentSlot.Boots },
                StatType.MaxHP, true, 1f, 3f)); // 百分比生命回复

            // 守护：特殊机制词缀
            allAffixes.Add(CreateAffix("suffix_guarding", AffixSlotType.Suffix,
                "守护", "of Guarding",
                headChest,
                StatType.DEF, true, 5f, 10f)); // 防背刺用DEF占位

            // 贪婪：金币/经验效率
            allAffixes.Add(CreateAffix("suffix_greed", AffixSlotType.Suffix,
                "贪婪", "of Greed",
                new[] { EquipmentSlot.Gloves, EquipmentSlot.Boots },
                StatType.GoldMultiplier, true, 15f, 30f));

            // ================================================================
            //  首饰词缀池（来源：06 文档 §四）
            // ================================================================
            var accessory = new[] { EquipmentSlot.Accessory };

            // 极速：特殊机制词缀，有负面代价
            allAffixes.Add(CreateAffix("prefix_fast_cooling", AffixSlotType.Prefix,
                "极速的", "Fast-Cooling",
                accessory,
                StatType.AttackSpeed, true, 10f, 25f,
                true, StatType.MaxHP, -0.10f)); // 负面代价：生命值-10%

            allAffixes.Add(CreateAffix("prefix_mana_expanding", AffixSlotType.Prefix,
                "扩蓝的", "Mana-Expanding",
                accessory,
                StatType.MaxMP, true, 15f, 30f));

            // 元素：火冰雷伤害提升，用MATK占位
            allAffixes.Add(CreateAffix("prefix_element_boosting", AffixSlotType.Prefix,
                "元素的", "Element-Boosting",
                accessory,
                StatType.MATK, true, 15f, 25f));

            allAffixes.Add(CreateAffix("prefix_sure_crit", AffixSlotType.Prefix,
                "必爆的", "Sure-Crit",
                accessory,
                StatType.CritRate, true, 5f, 12f));

            // 首饰后缀
            // 处决：对精英/Boss暴击率提升
            allAffixes.Add(CreateAffix("suffix_execution", AffixSlotType.Suffix,
                "处决", "of Execution",
                accessory,
                StatType.CritRate, true, 10f, 20f));

            // 护盾：开场白盾
            allAffixes.Add(CreateAffix("suffix_shielding", AffixSlotType.Suffix,
                "护盾", "of Shielding",
                accessory,
                StatType.MaxHP, true, 15f, 15f)); // 15%最大生命值护盾

            // 狂力：物理伤害提升
            allAffixes.Add(CreateAffix("suffix_pure_violence", AffixSlotType.Suffix,
                "狂力", "of Pure Violence",
                accessory,
                StatType.ATK, true, 10f, 20f));

            // 幻影：闪避后强制闪避
            allAffixes.Add(CreateAffix("suffix_dodging", AffixSlotType.Suffix,
                "幻影", "of Dodging",
                accessory,
                StatType.Dodge, true, 5f, 10f)); // 闪避率占位

            // ================================================================
            //  创建词缀数据库 SO
            // ================================================================
            var database = ScriptableObject.CreateInstance<AffixDatabase_SO>();
            database.allAffixes = allAffixes;

            AssetDatabase.CreateAsset(database, DB_PATH);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AffixAssetGenerator] ✅ 生成完毕！" +
                      $"\n  词缀数量: {allAffixes.Count} 条" +
                      $"\n  输出目录: {OUTPUT_DIR}" +
                      $"\n  数据库: {DB_PATH}");

            // 选中数据库资产方便检查
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        // =====================================================================
        //  辅助方法
        // =====================================================================

        private static AffixDefinition_SO CreateAffix(
            string id, AffixSlotType slotType,
            string nameCN, string nameEN,
            EquipmentSlot[] validSlots,
            StatType bonusStat, bool isPercentage,
            float minValue, float maxValue,
            bool hasPenalty = false,
            StatType penaltyStat = StatType.ATK,
            float penaltyValue = 0f)
        {
            var affix = ScriptableObject.CreateInstance<AffixDefinition_SO>();
            affix.affixID = id;
            affix.slotType = slotType;
            affix.nameCN = nameCN;
            affix.nameEN = nameEN;
            affix.validSlots = validSlots;
            affix.bonusStat = bonusStat;
            affix.isPercentage = isPercentage;
            affix.minValue = minValue;
            affix.maxValue = maxValue;
            affix.hasPenalty = hasPenalty;
            affix.penaltyStat = penaltyStat;
            affix.penaltyValue = penaltyValue;

            string fileName = $"{id}.asset";
            string fullPath = Path.Combine(OUTPUT_DIR, fileName);
            AssetDatabase.CreateAsset(affix, fullPath);

            return affix;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                // 逐级创建目录
                string[] parts = path.Split('/');
                string currentPath = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
        }
    }
}
#endif
