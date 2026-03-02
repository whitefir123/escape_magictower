// ============================================================================
// 逃离魔塔 - 伤害结算器 (DamageCalculator)
//
// 完整的伤害流转链路：
//   1. 威力判定 → 2. 暴击检算 → 3. 防御减免（穿甲综合 + 递减防御）→ 4. 最终修正
//
// 这是一个纯逻辑工具类，不持有状态，所有输入通过参数传递。
//
// 来源：DesignDocs/03_Combat_System.md 第 1.3 节
//       DesignDocs/02_Entities_and_Stats.md 第 1.4 节
// ============================================================================

using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 伤害计算结果——封装单次攻击的完整结算数据
    /// </summary>
    public struct DamageResult
    {
        /// <summary>最终实际伤害值（扣血量）</summary>
        public float FinalDamage;

        /// <summary>是否暴击</summary>
        public bool IsCritical;

        /// <summary>伤害类型</summary>
        public DamageType DamageType;

        /// <summary>元素类型</summary>
        public ElementType ElementType;

        /// <summary>攻击方总穿透率（含跨界穿透）</summary>
        public float TotalPenetration;

        /// <summary>防守方有效防御力</summary>
        public float EffectiveDefense;

        /// <summary>减伤比例</summary>
        public float DamageReduction;
    }

    /// <summary>
    /// 伤害结算器 —— 静态工具类
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 递减防御公式中的常数参数
        /// 使用经典的 D / (D + K) 公式，K 值决定防御效率曲线
        /// </summary>
        private const float DEFENSE_CONSTANT_K = 100f;

        /// <summary>
        /// 完整的伤害结算链路
        /// </summary>
        /// <param name="attackerStats">攻击方的最终属性块</param>
        /// <param name="defenderStats">防守方的最终属性块</param>
        /// <param name="baseDamage">技能/普攻的基础伤害值</param>
        /// <param name="atkScaling">物理攻击力缩放系数</param>
        /// <param name="matkScaling">魔法攻击力缩放系数</param>
        /// <param name="damageType">伤害类型</param>
        /// <param name="elementType">元素类型</param>
        /// <param name="forceCrit">是否强制暴击（某些技能如背刺）</param>
        /// <param name="bonusMultiplier">额外乘算倍率（种族克制等）</param>
        /// <param name="flatBonusDamage">额外固定真伤附加（装备词缀等）</param>
        /// <returns>完整的伤害计算结果</returns>
        public static DamageResult Calculate(
            StatBlock attackerStats,
            StatBlock defenderStats,
            float baseDamage,
            float atkScaling = 0f,
            float matkScaling = 0f,
            DamageType damageType = DamageType.Physical,
            ElementType elementType = ElementType.None,
            bool forceCrit = false,
            float bonusMultiplier = 1f,
            float flatBonusDamage = 0f)
        {
            var result = new DamageResult
            {
                DamageType = damageType,
                ElementType = elementType,
            };

            // =================================================================
            // 步骤 1：威力判定
            // 提取攻击侧面板及技能倍率，判断走物理池还是魔法池
            // =================================================================
            float rawDamage = baseDamage;
            float atk = attackerStats.Get(StatType.ATK);
            float matk = attackerStats.Get(StatType.MATK);

            rawDamage += atk * atkScaling;
            rawDamage += matk * matkScaling;

            // =================================================================
            // 步骤 2：暴击检算
            // 暴击不仅吃暴击伤害倍率乘区，还额外附带隐性穿甲加权
            // =================================================================
            float critRate = attackerStats.Get(StatType.CritRate);
            float critMultiplier = attackerStats.Get(StatType.CritMultiplier);

            bool isCrit = forceCrit || (Random.value < critRate);
            result.IsCritical = isCrit;

            if (isCrit)
            {
                rawDamage *= critMultiplier;
            }

            // =================================================================
            // 步骤 3：防御减免计算
            // =================================================================
            if (damageType == DamageType.True)
            {
                // 真实伤害无视一切防御
                result.TotalPenetration = 1f;
                result.EffectiveDefense = 0f;
                result.DamageReduction = 0f;
            }
            else
            {
                // 3a. 穿甲综合结算
                //     总穿透率 = 面板基础穿透率 + 跨界转换穿透率
                float basePen, crossPen;

                if (damageType == DamageType.Physical)
                {
                    basePen = attackerStats.Get(StatType.ArmorPen);
                    // 暴击隐性穿甲加权：暴击时额外获得 10% 穿透
                    if (isCrit) basePen += 0.10f;
                    crossPen = 0f; // 跨界物穿已在管线层级5中合并到 ArmorPen
                }
                else // Magical / Holy
                {
                    basePen = attackerStats.Get(StatType.MagicPen);
                    if (isCrit) basePen += 0.10f;
                    crossPen = 0f; // 跨界魔穿已在管线层级5中合并到 MagicPen
                }

                float totalPen = Mathf.Clamp01(basePen + crossPen);
                result.TotalPenetration = totalPen;

                // 3b. 有效防御力
                //     有效防御 = 面板防御 * (1 - 总穿透率)
                float rawDefense = (damageType == DamageType.Physical)
                    ? defenderStats.Get(StatType.DEF)
                    : defenderStats.Get(StatType.MDEF);

                float effectiveDefense = rawDefense * (1f - totalPen);
                result.EffectiveDefense = effectiveDefense;

                // 3c. 递减防御公式
                //     减伤率 = EffDef / (EffDef + K)，上限 99%
                //     当 EffDef 为负时（被破甲），减伤率为负 = 伤害加深
                float damageReduction;
                if (effectiveDefense >= 0f)
                {
                    damageReduction = effectiveDefense / (effectiveDefense + DEFENSE_CONSTANT_K);
                    damageReduction = Mathf.Min(damageReduction, GameConstants.MAX_DAMAGE_REDUCTION);
                }
                else
                {
                    // 负防御：伤害加深（反向递减，上限额外伤害 50%）
                    damageReduction = effectiveDefense / (Mathf.Abs(effectiveDefense) + DEFENSE_CONSTANT_K);
                    damageReduction = Mathf.Max(damageReduction, -0.50f);
                }
                result.DamageReduction = damageReduction;

                // 应用减免
                rawDamage *= (1f - damageReduction);
            }

            // =================================================================
            // 步骤 4：最终修正
            // 附加种族克制/元素反应倍率/固定真伤等特殊补正
            // =================================================================
            rawDamage *= bonusMultiplier;
            rawDamage += flatBonusDamage;

            // 最终伤害不可为负数（至少造成 0 伤害）
            result.FinalDamage = Mathf.Max(0f, rawDamage);

            return result;
        }

        /// <summary>
        /// 闪避判定 —— 在伤害计算前调用
        /// </summary>
        /// <param name="defenderStats">防守方属性</param>
        /// <returns>是否闪避成功（完全规避）</returns>
        public static bool CheckDodge(StatBlock defenderStats)
        {
            float dodgeRate = defenderStats.Get(StatType.Dodge);
            return Random.value < dodgeRate;
        }

        /// <summary>
        /// 计算升级所需经验值
        /// 公式：基底100 + (等级-1)*20 + (等级-1)^2 * 小额递增
        /// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
        /// </summary>
        public static int GetExpRequiredForLevel(int currentLevel)
        {
            int lvlOffset = Mathf.Max(0, currentLevel - 1);
            return GameConstants.BASE_EXP_REQUIRED
                   + lvlOffset * GameConstants.EXP_LINEAR_INCREMENT
                   + lvlOffset * lvlOffset * 2; // 小额二次方递增
        }
    }
}
