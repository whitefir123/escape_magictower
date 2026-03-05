// ============================================================================
// 逃离魔塔 - 装备生成引擎 (EquipmentGenerator)
// 负责从"掉落触发"到"生成一件完整装备数据"的全流程。
//
// 生成管线（六步）：
//   1. 计算 iPwr（楼层深度 + 怪物阶级 + 难度修正）
//   2. Roll 品质（使用品质权重表 + MagicFind 干预）
//   3. Roll 部位（等权随机 6 槽位）
//   4. Roll 底座白值（部位固定属性 + 品质波动区间）
//   5. Roll 词缀（数量由品质决定，从 AffixDatabase_SO 抽取）
//   6. Roll 镶孔数（品质决定区间）
//
// 来源：DesignDocs/04_Equipment_and_Forge.md
//       GameData_Blueprints/06_Equipment_Affix_System.md
//       GameData_Blueprints/09_Consumable_and_Drop_Items.md
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 装备生成引擎 —— 从掉落触发到完整装备数据的全链路生成器
    /// </summary>
    public static class EquipmentGenerator
    {
        // =====================================================================
        //  主入口
        // =====================================================================

        /// <summary>
        /// 生成一件完整的随机装备
        /// </summary>
        /// <param name="floorLevel">当前楼层深度（1~9）</param>
        /// <param name="monsterTier">怪物阶级（影响 iPwr 基数）</param>
        /// <param name="qualityWeights">品质权重表（7元素：白/绿/蓝/紫/黄/红/彩）</param>
        /// <param name="magicFind">玩家寻宝率（0 = 无加成）</param>
        /// <param name="affixDB">词缀数据库 SO 引用</param>
        /// <param name="rng">随机数生成器</param>
        /// <param name="forceSlot">强制指定部位（null = 随机）</param>
        public static EquipmentData Generate(
            int floorLevel,
            MonsterTierForEquip monsterTier,
            float[] qualityWeights,
            float magicFind,
            AffixDatabase_SO affixDB,
            System.Random rng,
            EquipmentSlot? forceSlot = null)
        {
            if (affixDB == null)
            {
                Debug.LogError("[EquipmentGenerator] AffixDatabase_SO 为 null，无法生成装备！");
                return null;
            }

            var equip = new EquipmentData
            {
                instanceID = Guid.NewGuid().ToString("N"),
                affixes = new List<AffixInstance>(),
            };

            // === 步骤1：计算 iPwr ===
            equip.itemPower = CalculateItemPower(floorLevel, monsterTier, rng);

            // === 步骤2：Roll 品质（MagicFind 干预） ===
            float[] adjustedWeights = ApplyMagicFind(qualityWeights, magicFind);
            equip.quality = RollQuality(adjustedWeights, rng);

            // === 步骤3：Roll 部位 ===
            equip.slot = forceSlot ?? RollSlot(rng);

            // === 步骤4：Roll 底座白值 ===
            RollBaseStats(equip, rng);

            // === 步骤5：Roll 词缀 ===
            RollAffixes(equip, affixDB, rng);

            // === 步骤6：Roll 镶孔数 ===
            equip.socketCount = RollSocketCount(equip.quality, rng);

            Debug.Log($"[装备生成] {equip}");
            return equip;
        }

        // =====================================================================
        //  怪物阶级枚举（装备生成专用）
        // =====================================================================

        /// <summary>
        /// 怪物阶级（装备iPwr计算用）
        /// </summary>
        public enum MonsterTierForEquip
        {
            Normal = 0,     // 普通怪
            Elite = 1,      // 精英怪
            Boss = 2,       // Boss
            Chest = 3,      // 宝箱（非怪物来源）
        }

        // =====================================================================
        //  步骤1：iPwr 计算
        // =====================================================================

        /// <summary>
        /// 计算物品强度（iPwr）
        /// 公式：basePwr = floorLevel * 10 + monsterTierBonus + randomVariance
        /// </summary>
        private static int CalculateItemPower(int floorLevel, MonsterTierForEquip tier, System.Random rng)
        {
            int basePwr = floorLevel * 10;

            int tierBonus = tier switch
            {
                MonsterTierForEquip.Normal => 0,
                MonsterTierForEquip.Elite => 15,
                MonsterTierForEquip.Boss => 30,
                MonsterTierForEquip.Chest => 5,
                _ => 0,
            };

            // ±10% 随机浮动
            int variance = rng.Next(-basePwr / 10, basePwr / 10 + 1);

            return Mathf.Max(1, basePwr + tierBonus + variance);
        }

        // =====================================================================
        //  步骤2：品质 Roll + MagicFind 干预
        // =====================================================================

        /// <summary>
        /// MagicFind 干预品质权重
        /// 压缩低品质（白/绿）扇区，将概率推移至高品质
        /// 红/彩品质的校正系数极低以严控通胀
        /// </summary>
        private static float[] ApplyMagicFind(float[] baseWeights, float magicFind)
        {
            if (baseWeights == null || baseWeights.Length < 7)
            {
                Debug.LogWarning("[EquipmentGenerator] 品质权重表不足 7 个元素，使用默认");
                return new[] { 0.30f, 0.30f, 0.25f, 0.10f, 0.04f, 0.009f, 0.001f };
            }

            if (magicFind <= 0f) return (float[])baseWeights.Clone();

            // 各品质的 MF 校正系数（越高品质越低，防止顶级过度通胀）
            // 白、绿、蓝、紫、黄、红、彩
            float[] mfCoefficients = { 0f, 0f, 1.0f, 0.8f, 0.5f, 0.15f, 0.05f };

            float[] adjusted = new float[7];

            // 先放大高品质权重
            for (int i = 0; i < 7; i++)
            {
                adjusted[i] = baseWeights[i] * (1f + magicFind * mfCoefficients[i]);
            }

            // 归一化
            float total = 0f;
            for (int i = 0; i < 7; i++) total += adjusted[i];

            if (total > 0f)
            {
                for (int i = 0; i < 7; i++) adjusted[i] /= total;
            }

            return adjusted;
        }

        /// <summary>
        /// 按品质权重表 Roll 品质
        /// </summary>
        private static QualityTier RollQuality(float[] weights, System.Random rng)
        {
            float roll = (float)rng.NextDouble();
            float cumulative = 0f;

            QualityTier[] tiers =
            {
                QualityTier.White,
                QualityTier.Green,
                QualityTier.Blue,
                QualityTier.Purple,
                QualityTier.Yellow,
                QualityTier.Red,
                QualityTier.Rainbow,
            };

            for (int i = 0; i < weights.Length && i < tiers.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return tiers[i];
            }

            return QualityTier.White;
        }

        // =====================================================================
        //  步骤3：部位随机
        // =====================================================================

        private static readonly EquipmentSlot[] AllSlots =
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Helmet,
            EquipmentSlot.Armor,
            EquipmentSlot.Gloves,
            EquipmentSlot.Boots,
            EquipmentSlot.Accessory,
        };

        private static EquipmentSlot RollSlot(System.Random rng)
        {
            return AllSlots[rng.Next(AllSlots.Length)];
        }

        // =====================================================================
        //  步骤4：底座白值 Roll
        //  来源：GameData_Blueprints/06_Equipment_Affix_System.md §1.4
        // =====================================================================

        /// <summary>
        /// 根据部位确定底座属性类型并 Roll 基础值 + 品质波动
        /// </summary>
        private static void RollBaseStats(EquipmentData equip, System.Random rng)
        {
            // 部位 → (属性1类型, 属性2类型, 基础范围)
            var (stat1, stat2, baseMin1, baseMax1, baseMin2, baseMax2) = equip.slot switch
            {
                // 武器：ATK + MATK
                EquipmentSlot.Weapon => (StatType.ATK, StatType.MATK, 5f, 12f, 3f, 8f),
                // 头盔：DEF + MaxHP
                EquipmentSlot.Helmet => (StatType.DEF, StatType.MaxHP, 3f, 8f, 10f, 25f),
                // 胸甲：DEF + MDEF
                EquipmentSlot.Armor => (StatType.DEF, StatType.MDEF, 4f, 10f, 3f, 8f),
                // 手套：ATK + CritRate
                EquipmentSlot.Gloves => (StatType.ATK, StatType.CritRate, 2f, 6f, 0.01f, 0.03f),
                // 鞋靴：MDEF + Dodge
                EquipmentSlot.Boots => (StatType.MDEF, StatType.Dodge, 3f, 8f, 0.01f, 0.02f),
                // 首饰：MaxHP + MaxMP
                EquipmentSlot.Accessory => (StatType.MaxHP, StatType.MaxMP, 8f, 20f, 5f, 15f),
                _ => (StatType.ATK, StatType.DEF, 1f, 5f, 1f, 5f),
            };

            equip.baseStat1Type = stat1;
            equip.baseStat2Type = stat2;

            // 基础值：在范围内随机
            equip.baseStat1Base = Mathf.Round(Lerp(baseMin1, baseMax1, rng) * 10f) / 10f;
            equip.baseStat2Base = Mathf.Round(Lerp(baseMin2, baseMax2, rng) * 10f) / 10f;

            // 品质波动加成
            var (qMin, qMax) = GetQualityBonusRange(equip.quality);
            equip.baseStat1QualityBonus = Mathf.Round(Lerp(qMin, qMax, rng) * 10f) / 10f;
            equip.baseStat2QualityBonus = Mathf.Round(Lerp(qMin, qMax, rng) * 10f) / 10f;
        }

        /// <summary>
        /// 品质波动区间（来源：06_Equipment_Affix_System.md §1.4）
        /// </summary>
        private static (float min, float max) GetQualityBonusRange(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => (0f, 1f),
                QualityTier.Green => (1f, 3f),
                QualityTier.Blue => (3f, 6f),
                QualityTier.Purple => (6f, 10f),
                QualityTier.Yellow => (11f, 16f),
                QualityTier.Red => (17f, 25f),
                QualityTier.Rainbow => (26f, 40f),
                _ => (0f, 1f),
            };
        }

        // =====================================================================
        //  步骤5：词缀 Roll
        //  来源：06_Equipment_Affix_System.md §1.2
        // =====================================================================

        /// <summary>
        /// 根据品质决定词缀数量并从词缀池中抽取
        /// </summary>
        private static void RollAffixes(EquipmentData equip, AffixDatabase_SO affixDB, System.Random rng)
        {
            var (minAffixes, maxAffixes) = GetAffixCountRange(equip.quality);
            int affixCount = rng.Next(minAffixes, maxAffixes + 1);

            if (affixCount <= 0) return;

            // 获取该部位所有可用词缀
            var availablePrefixes = affixDB.GetAffixesForSlot(equip.slot, AffixSlotType.Prefix);
            var availableSuffixes = affixDB.GetAffixesForSlot(equip.slot, AffixSlotType.Suffix);

            if (availablePrefixes.Count == 0 && availableSuffixes.Count == 0)
            {
                Debug.LogWarning($"[EquipmentGenerator] 部位 {equip.slot} 没有可用词缀");
                return;
            }

            // 已使用词缀 ID 集合（排重）
            var usedAffixIDs = new HashSet<string>();

            // 交替优先：先前缀再后缀
            bool preferPrefix = true;

            for (int i = 0; i < affixCount; i++)
            {
                AffixDefinition_SO chosen = null;

                // 尝试按优先顺序抽取
                if (preferPrefix && availablePrefixes.Count > 0)
                {
                    chosen = PickRandomAffix(availablePrefixes, usedAffixIDs, rng);
                }

                if (chosen == null && availableSuffixes.Count > 0)
                {
                    chosen = PickRandomAffix(availableSuffixes, usedAffixIDs, rng);
                }

                if (chosen == null && availablePrefixes.Count > 0)
                {
                    chosen = PickRandomAffix(availablePrefixes, usedAffixIDs, rng);
                }

                if (chosen == null)
                {
                    // 所有候选已耗尽
                    break;
                }

                usedAffixIDs.Add(chosen.affixID);
                float rolledValue = chosen.RollValue(rng);
                equip.affixes.Add(new AffixInstance(chosen, rolledValue));

                preferPrefix = !preferPrefix; // 交替
            }
        }

        /// <summary>
        /// 从候选列表中随机抽取一条未使用的词缀
        /// </summary>
        private static AffixDefinition_SO PickRandomAffix(
            List<AffixDefinition_SO> candidates,
            HashSet<string> usedIDs,
            System.Random rng)
        {
            // 筛选出未使用的
            var available = new List<AffixDefinition_SO>();
            foreach (var affix in candidates)
            {
                if (affix != null && !usedIDs.Contains(affix.affixID))
                    available.Add(affix);
            }

            if (available.Count == 0) return null;
            return available[rng.Next(available.Count)];
        }

        /// <summary>
        /// 品质→词缀数量区间（来源：06_Equipment_Affix_System.md §1.2）
        /// </summary>
        private static (int min, int max) GetAffixCountRange(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => (0, 1),
                QualityTier.Green => (0, 2),
                QualityTier.Blue => (1, 3),     // 蓝装开始保底 1 条
                QualityTier.Purple => (2, 4),
                QualityTier.Yellow => (3, 5),
                QualityTier.Red => (4, 6),
                QualityTier.Rainbow => (5, 6),
                _ => (0, 1),
            };
        }

        // =====================================================================
        //  步骤6：镶孔数 Roll
        //  来源：DesignDocs/04_Equipment_and_Forge.md §1.3
        // =====================================================================

        /// <summary>
        /// 品质→镶孔数区间
        /// </summary>
        private static int RollSocketCount(QualityTier quality, System.Random rng)
        {
            var (min, max) = quality switch
            {
                QualityTier.White => (0, 1),
                QualityTier.Green => (0, 1),
                QualityTier.Blue => (1, 2),
                QualityTier.Purple => (1, 2),
                QualityTier.Yellow => (2, 4),
                QualityTier.Red => (2, 4),
                QualityTier.Rainbow => (3, 5),
                _ => (0, 1),
            };
            return rng.Next(min, max + 1);
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private static float Lerp(float min, float max, System.Random rng)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        // =====================================================================
        //  常用品质权重表预设（来源：09_Consumable_and_Drop_Items.md §四）
        // =====================================================================

        /// <summary>普通怪装备品质权重：白75% 绿20% 蓝4.9% 紫0.09% 黄0.009% 红0.0009% 彩0.0001%</summary>
        public static readonly float[] NormalMonsterEquipWeights =
            { 0.75f, 0.20f, 0.049f, 0.0009f, 0.00009f, 0.000009f, 0.000001f };

        /// <summary>精英怪装备品质权重：绿40% 蓝45% 紫13% 黄1.9% 红0.09% 彩0.01%</summary>
        public static readonly float[] EliteMonsterEquipWeights =
            { 0f, 0.40f, 0.45f, 0.13f, 0.019f, 0.0009f, 0.0001f };

        /// <summary>Boss装备品质权重：紫30% 黄45% 红20% 彩5%</summary>
        public static readonly float[] BossEquipWeights =
            { 0f, 0f, 0f, 0.30f, 0.45f, 0.20f, 0.05f };

        /// <summary>无门房间宝箱装备品质权重</summary>
        public static readonly float[] NoDoorChestEquipWeights =
            { 0.70f, 0.25f, 0.049f, 0.0009f, 0.00009f, 0.000009f, 0.000001f };

        /// <summary>铜门房间宝箱装备品质权重</summary>
        public static readonly float[] BronzeChestEquipWeights =
            { 0.10f, 0.60f, 0.25f, 0.049f, 0.0009f, 0.00009f, 0.00001f };

        /// <summary>银门房间宝箱装备品质权重</summary>
        public static readonly float[] SilverChestEquipWeights =
            { 0f, 0.45f, 0.35f, 0.15f, 0.049f, 0.0009f, 0.0001f };

        /// <summary>金门房间宝箱装备品质权重</summary>
        public static readonly float[] GoldChestEquipWeights =
            { 0f, 0f, 0.45f, 0.30f, 0.20f, 0.049f, 0.001f };

        /// <summary>Boss房宝箱装备品质权重</summary>
        public static readonly float[] BossChestEquipWeights =
            { 0f, 0f, 0f, 0.20f, 0.50f, 0.25f, 0.05f };

        /// <summary>路途宝箱装备品质权重</summary>
        public static readonly float[] CorridorChestEquipWeights =
            { 0.75f, 0.20f, 0.049f, 0.0009f, 0.00009f, 0.000009f, 0.000001f };
    }
}
