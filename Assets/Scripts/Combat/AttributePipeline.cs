// ============================================================================
// 逃离魔塔 - 六层属性管线 (AttributePipeline)
// 
// 这是整个系统最关键的模块！
// 
// 属性计算严格遵循单向流动计算链条，任何层级变动后必须全量重算。
// 严禁增量逻辑 currentAttack += newWeapon - oldWeapon！
//
// 六层管线：
//   层级1：基础职业池（HeroClassData_SO 白值）
//   层级2：天赋树（元叠加层，MVP 阶段传入空数据）
//   层级3：装备锚定层（MVP 阶段传入空数据）
//   层级4：局内符文层（命运增益）
//   层级5：跨界协同修正层（四条转化公式）
//   层级6：临时状态层（战斗中 Buff/Debuff）
//
// 来源：DesignDocs/09_DataFlow_and_Status.md 第一节
//       DesignDocs/02_Entities_and_Stats.md 第 1.4 节（跨界协同方程）
//       DesignDocs/13_Architecture_and_Operations_SLA.md（钳制边界）
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 六层属性管线 —— 全量重算引擎
    /// 每当任何层级（装备/符文/Buff 等）发生变更时，调用 RecalculateAll 重刷最终面板
    /// </summary>
    public class AttributePipeline
    {
        // =====================================================================
        //  层级1：基础职业池
        // =====================================================================

        /// <summary>
        /// 从职业 SO 数据获取基础白值
        /// </summary>
        public StatBlock GetLayer1_ClassBase(EntityData_SO classData)
        {
            if (classData == null)
            {
                Debug.LogError("[AttributePipeline] 层级1：职业数据为 null！");
                return new StatBlock();
            }
            return classData.CreateBaseStatBlock();
        }

        // =====================================================================
        //  层级2：天赋树（元叠加层）
        // =====================================================================

        /// <summary>
        /// 叠加天赋树的永久增益
        /// MVP 阶段此层传入空的 StatBlock（天赋系统属于 Extra_Features）
        /// </summary>
        public void ApplyLayer2_TalentTree(StatBlock current, StatBlock talentBonuses)
        {
            if (talentBonuses != null)
            {
                current.MergeAdd(talentBonuses);
            }
        }

        // =====================================================================
        //  层级3：装备锚定层
        // =====================================================================

        /// <summary>
        /// 叠加 6 个装备槽位的属性
        /// 先累加所有固定整数值，再套用百分比词缀
        /// MVP 阶段此层传入空列表（装备系统属于 Extra_Features）
        /// </summary>
        public void ApplyLayer3_Equipment(StatBlock current, List<StatBlock> equipmentStats)
        {
            if (equipmentStats == null) return;
            foreach (var equipStat in equipmentStats)
            {
                if (equipStat != null)
                {
                    current.MergeAdd(equipStat);
                }
            }
        }

        // =====================================================================
        //  层级4：局内符文层
        // =====================================================================

        /// <summary>
        /// 叠加所有已获取的符文属性增益
        /// </summary>
        public void ApplyLayer4_Runes(StatBlock current, List<StatBlock> runeStats)
        {
            if (runeStats == null) return;
            foreach (var runeStat in runeStats)
            {
                if (runeStat != null)
                {
                    current.MergeAdd(runeStat);
                }
            }
        }

        // =====================================================================
        //  层级5：跨界协同修正层
        //  来源：DesignDocs/02_Entities_and_Stats.md 第 1.4 节
        //  根据前 4 层汇聚的双攻双防绝对值，计算跨界派生属性
        // =====================================================================

        /// <summary>
        /// 计算跨界属性协同方程，产出额外的穿透/暴击/抗性
        /// </summary>
        public void ApplyLayer5_CrossStatSynergy(StatBlock current)
        {
            float atk = current.Get(StatType.ATK);
            float matk = current.Get(StatType.MATK);
            float def = current.Get(StatType.DEF);
            float mdef = current.Get(StatType.MDEF);

            // 1. 力量破法 (Force Breaches Magic)
            //    额外魔法穿透率 = ATK / (ATK + 500) * 20%，上限 20%
            float bonusMagicPen = 0f;
            if (atk > 0f)
            {
                bonusMagicPen = atk / (atk + GameConstants.CROSS_MAGIC_PEN_DIVISOR)
                                * GameConstants.MAX_CROSS_MAGIC_PEN;
                bonusMagicPen = Mathf.Min(bonusMagicPen, GameConstants.MAX_CROSS_MAGIC_PEN);
            }
            current.Set(StatType.BonusMagicPen, bonusMagicPen);

            // 2. 魔力附刃 (Arcane Edges)
            //    额外物理穿透率 = MATK / (MATK + 500) * 20%，上限 20%
            float bonusArmorPen = 0f;
            if (matk > 0f)
            {
                bonusArmorPen = matk / (matk + GameConstants.CROSS_ARMOR_PEN_DIVISOR)
                                * GameConstants.MAX_CROSS_ARMOR_PEN;
                bonusArmorPen = Mathf.Min(bonusArmorPen, GameConstants.MAX_CROSS_ARMOR_PEN);
            }
            current.Set(StatType.BonusArmorPen, bonusArmorPen);

            // 3. 重甲霸体 (Armor Grants Tenacity)
            //    额外控制抗性 = DEF / (DEF + 1000) * 40%，上限 40%
            float bonusCCResist = 0f;
            if (def > 0f)
            {
                bonusCCResist = def / (def + GameConstants.CROSS_CC_RESIST_DIVISOR)
                                * GameConstants.MAX_CROSS_CC_RESIST;
                bonusCCResist = Mathf.Min(bonusCCResist, GameConstants.MAX_CROSS_CC_RESIST);
            }
            current.Set(StatType.BonusCCResist, bonusCCResist);

            // 4. 魔场感知 (Ward Grants Precision)
            //    额外暴击率 = MDEF / (MDEF + 800) * 15%，上限 15%
            float bonusCritRate = 0f;
            if (mdef > 0f)
            {
                bonusCritRate = mdef / (mdef + GameConstants.CROSS_CRIT_RATE_DIVISOR)
                                * GameConstants.MAX_CROSS_CRIT_RATE;
                bonusCritRate = Mathf.Min(bonusCritRate, GameConstants.MAX_CROSS_CRIT_RATE);
            }
            current.Set(StatType.BonusCritRate, bonusCritRate);

            // 将跨界派生值合并到主属性上
            current.Add(StatType.MagicPen, bonusMagicPen);
            current.Add(StatType.ArmorPen, bonusArmorPen);
            current.Add(StatType.CritRate, bonusCritRate);
        }

        // =====================================================================
        //  层级6：临时状态层（战斗中的 Buff/Debuff）
        // =====================================================================

        /// <summary>
        /// 叠加当前生效的所有临时 Buff/Debuff 属性修正
        /// </summary>
        public void ApplyLayer6_TempBuffs(StatBlock current, List<StatBlock> buffStats)
        {
            if (buffStats == null) return;
            foreach (var buffStat in buffStats)
            {
                if (buffStat != null)
                {
                    current.MergeAdd(buffStat);
                }
            }
        }

        // =====================================================================
        //  全量重算入口
        // =====================================================================

        /// <summary>
        /// 从层级 1 开始顺序叠加至层级 6，输出最终属性表
        /// 
        /// 铁律：这是唯一允许的属性计算路径！
        /// 任何装备/符文/Buff 变动后，只需调用此方法全量重刷。
        /// </summary>
        /// <param name="classData">层级1：职业基础数据 SO</param>
        /// <param name="talentBonuses">层级2：天赋树增益（MVP 阶段传 null）</param>
        /// <param name="equipmentStats">层级3：装备属性列表（MVP 阶段传 null）</param>
        /// <param name="runeStats">层级4：符文属性列表</param>
        /// <param name="buffStats">层级6：临时 Buff/Debuff 列表</param>
        /// <returns>经过全部 6 层叠加且钳制后的最终属性表</returns>
        public StatBlock RecalculateAll(
            EntityData_SO classData,
            StatBlock talentBonuses = null,
            List<StatBlock> equipmentStats = null,
            List<StatBlock> runeStats = null,
            List<StatBlock> buffStats = null)
        {
            // 层级 1：基础职业池
            StatBlock result = GetLayer1_ClassBase(classData);

            // 层级 2：天赋树
            ApplyLayer2_TalentTree(result, talentBonuses);

            // 层级 3：装备锚定层
            ApplyLayer3_Equipment(result, equipmentStats);

            // 层级 4：局内符文层
            ApplyLayer4_Runes(result, runeStats);

            // 层级 5：跨界协同修正层（基于前 4 层的汇聚结果自动计算）
            ApplyLayer5_CrossStatSynergy(result);

            // 层级 6：临时 Buff/Debuff
            ApplyLayer6_TempBuffs(result, buffStats);

            // 全局钳制：确保所有属性在合法范围内
            ClampAllStats(result);

            return result;
        }

        // =====================================================================
        //  全局属性钳制器
        //  来源：DesignDocs/13_Architecture_and_Operations_SLA.md 第一节
        // =====================================================================

        /// <summary>
        /// 对所有最终属性施加边界钳制，防止数值溢出或负数穿模
        /// </summary>
        private void ClampAllStats(StatBlock stats)
        {
            // 生命值不可为负
            stats.Set(StatType.HP, Mathf.Max(0f, stats.Get(StatType.HP)));
            stats.Set(StatType.MaxHP, Mathf.Max(1f, stats.Get(StatType.MaxHP)));

            // 法力值不可为负
            stats.Set(StatType.MP, Mathf.Max(0f, stats.Get(StatType.MP)));
            stats.Set(StatType.MaxMP, Mathf.Max(0f, stats.Get(StatType.MaxMP)));

            // 攻击力下限为 0（不可为负攻击力）
            stats.Set(StatType.ATK, Mathf.Max(0f, stats.Get(StatType.ATK)));
            stats.Set(StatType.MATK, Mathf.Max(0f, stats.Get(StatType.MATK)));

            // 防御力允许负数（破甲机制使然），但设有底线防止极端负数
            // 极限负数设为 -9999（实际业务中由破甲层数 × 每层扣减量决定）
            stats.Set(StatType.DEF, Mathf.Max(-9999f, stats.Get(StatType.DEF)));
            stats.Set(StatType.MDEF, Mathf.Max(-9999f, stats.Get(StatType.MDEF)));

            // 暴击率 [0, 1]
            stats.Set(StatType.CritRate, Mathf.Clamp01(stats.Get(StatType.CritRate)));

            // 暴击倍率下限 1.0（不可低于原伤害）
            stats.Set(StatType.CritMultiplier, Mathf.Max(1f, stats.Get(StatType.CritMultiplier)));

            // 穿透率 [0, 1]
            stats.Set(StatType.ArmorPen, Mathf.Clamp01(stats.Get(StatType.ArmorPen)));
            stats.Set(StatType.MagicPen, Mathf.Clamp01(stats.Get(StatType.MagicPen)));

            // 闪避率 [0, 1]
            stats.Set(StatType.Dodge, Mathf.Clamp01(stats.Get(StatType.Dodge)));

            // 移动速度下限 0.1（不可静止但允许极慢）
            stats.Set(StatType.MoveSpeed, Mathf.Max(0.1f, stats.Get(StatType.MoveSpeed)));

            // 攻击速度下限 0.1
            stats.Set(StatType.AttackSpeed, Mathf.Max(0.1f, stats.Get(StatType.AttackSpeed)));

            // 怒气 [0, MaxRage]
            float maxRage = stats.Get(StatType.MaxRage);
            stats.Set(StatType.Rage, Mathf.Clamp(stats.Get(StatType.Rage), 0f, maxRage));

            // 跨界协同派生属性各自有独立硬上限（已在层级5中钳制，此处二次兜底）
            stats.Set(StatType.BonusMagicPen,
                Mathf.Clamp(stats.Get(StatType.BonusMagicPen), 0f, GameConstants.MAX_CROSS_MAGIC_PEN));
            stats.Set(StatType.BonusArmorPen,
                Mathf.Clamp(stats.Get(StatType.BonusArmorPen), 0f, GameConstants.MAX_CROSS_ARMOR_PEN));
            stats.Set(StatType.BonusCCResist,
                Mathf.Clamp(stats.Get(StatType.BonusCCResist), 0f, GameConstants.MAX_CROSS_CC_RESIST));
            stats.Set(StatType.BonusCritRate,
                Mathf.Clamp(stats.Get(StatType.BonusCritRate), 0f, GameConstants.MAX_CROSS_CRIT_RATE));
        }
    }
}
