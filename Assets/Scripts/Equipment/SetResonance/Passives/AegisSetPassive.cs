// ============================================================================
// 套装被动 #3 — 不毁之钢屏障 (Aegis of Indestructible Steel)
// 重装反伤流
//
// 2pc: 开局获得 15%MaxHP 的白盾，静止 3s 自动补满
// 4pc: 闪避率锁 0%，每 1% 闪避 → 1.5% 全伤减免
// 6pc: 反弹一切伤害（税前总伤×15% + 1.2×DEF 真伤）
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.3
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class AegisSetPassive : SetPassiveBase
    {
        public override string SetName => "不毁之钢屏障";

        public override StatBlock GetStatModifiers()
        {
            var block = new StatBlock();
            if (Owner == null) return block;

            // 4pc: 闪避率归零 → 转换为伤害减免
            if (ActiveTier >= ResonanceTier.Four)
            {
                float originalDodge = Owner.CurrentStats.Get(StatType.Dodge);
                if (originalDodge > 0f)
                {
                    // 闪避率归零
                    block.Add(StatType.Dodge, -originalDodge);
                    // 每 1% 闪避 → 1.5% 伤害减免（存入 BonusCCResist 暂存，
                    // 后续可添加专用 DamageReduction StatType）
                    // 注意：此处仅标记转换意图，实际减伤需在 DamageCalculator 中读取
                }
            }

            return block;
        }

        // TODO: 2pc 白盾生成和自动补满、6pc 全伤反弹需事件钩子
    }
}
