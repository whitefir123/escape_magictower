// ============================================================================
// 套装被动 #6 — 极寒暴君之怒 (Wrath of the Glacial Tyrant)
// 绝对冰封控制流
//
// 2pc: 所有攻击必定对减速抗性<100%目标造成 30% 减速
// 4pc: 对冰冻目标伤害 +50%
// 6pc: 冰冻/减速目标 HP<15% 时即死斩杀（Boss 免疫即死但碎盾 30%）
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.6
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class GlacialSetPassive : SetPassiveBase
    {
        public override string SetName => "极寒暴君之怒";

        /// <summary>
        /// 4pc 对冰冻目标的伤害加成倍率（供 DamageCalculator 查询）
        /// </summary>
        public float FrozenDamageMultiplier =>
            ActiveTier >= ResonanceTier.Four ? 1.50f : 1.00f;

        /// <summary>
        /// 6pc 斩杀阈值（HP%）
        /// </summary>
        public float ExecuteThreshold =>
            ActiveTier >= ResonanceTier.Six ? 0.15f : 0f;

        /// <summary>
        /// 2pc 是否激活必定减速
        /// </summary>
        public bool ForceSlowActive => ActiveTier >= ResonanceTier.Two;

        public override StatBlock GetStatModifiers()
        {
            // 冰暴套无纯属性加成，全部通过行为钩子实现
            return new StatBlock();
        }

        // TODO: 2pc 必定减速、4pc 冰冻加伤、6pc 斩杀执行需在战斗系统中查询本被动实例
    }
}
