// ============================================================================
// 套装被动 #5 — 时光刺客信条 (Creed of the Chrono-Assassin)
// 极致减CD技能刷新流
//
// 2pc: CDR 硬上限从 40% → 65%
// 4pc: 闪避成功时削减所有小技能 1.5s CD
// 6pc: 大招 50% 概率不进 CD + 返还资源（代价：MaxHP -40%）
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.5
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class ChronoSetPassive : SetPassiveBase
    {
        public override string SetName => "时光刺客信条";

        /// <summary>
        /// 当前 CDR 硬上限（供技能系统读取）
        /// 2pc 激活时从 0.40 提升至 0.65
        /// </summary>
        public float CDRCap => ActiveTier >= ResonanceTier.Two ? 0.65f : 0.40f;

        public override StatBlock GetStatModifiers()
        {
            var block = new StatBlock();
            if (Owner == null) return block;

            // 6pc 代价：MaxHP -40%
            if (ActiveTier >= ResonanceTier.Six)
            {
                float maxHP = Owner.CurrentStats.Get(StatType.MaxHP);
                block.Add(StatType.MaxHP, -maxHP * 0.40f);
            }

            return block;
        }

        // TODO: 4pc 闪避减 CD 和 6pc 大招免 CD 需事件钩子
    }
}
