// ============================================================================
// 套装被动 #4 — 召唤使的仪典 (Ritual of the Summoner)
// 万灵长河统御套 — 空壳实现（需召唤物系统）
//
// 2pc: 每个召唤物 +5% 移速，+10% 物防/魔抗
// 4pc: 召唤物继承英雄暴击属性
// 6pc: 致死时献祭召唤物免死
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.4
// ============================================================================

using EscapeTheTower.Data;
using UnityEngine;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    /// <summary>
    /// 召唤使套装 — 空壳实现
    /// 待召唤物系统实装后填入具体逻辑
    /// </summary>
    public class SummonerSetPassive : SetPassiveBase
    {
        public override string SetName => "召唤使的仪典";

        public override StatBlock GetStatModifiers()
        {
            if (ActiveTier != ResonanceTier.None)
            {
                Debug.Log($"[共鸣] {SetName} 已激活 {(int)ActiveTier}pc，等待召唤物系统实装");
            }
            return new StatBlock();
        }
    }
}
