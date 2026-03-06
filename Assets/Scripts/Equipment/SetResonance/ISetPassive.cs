// ============================================================================
// 逃离魔塔 - 套装被动接口 (ISetPassive)
// 所有套装共鸣效果的统一接口。每套独立实现，完全解耦。
//
// 设计原则：
//   1. 被动参数从 SetResonanceDefinition_SO 读取（Inspector 可调）
//   2. 事件驱动：通过 EventManager 订阅战斗事件，不侵入主循环
//   3. 单文件单套：修改某套效果只碰一个文件
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三
// ============================================================================

using EscapeTheTower.Data;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Equipment.SetResonance
{
    /// <summary>
    /// 套装共鸣激活层级
    /// </summary>
    public enum ResonanceTier
    {
        None    = 0,
        Two     = 2,    // 2 件共鸣
        Four    = 4,    // 4 件共鸣
        Six     = 6,    // 6 件共鸣
    }

    /// <summary>
    /// 套装被动效果接口
    /// 每套共鸣实现一个该接口的类，由 SetResonanceEngine 管理生命周期
    /// </summary>
    public interface ISetPassive
    {
        /// <summary>
        /// 套装内部标识名（用于日志和调试）
        /// </summary>
        string SetName { get; }

        /// <summary>
        /// 当前激活的共鸣层级
        /// </summary>
        ResonanceTier ActiveTier { get; }

        /// <summary>
        /// 激活或更新共鸣层级
        /// 当装备变更导致共鸣件数变化时调用
        /// </summary>
        /// <param name="tier">新的共鸣层级</param>
        /// <param name="owner">装备持有者（英雄实体）</param>
        void Activate(ResonanceTier tier, EntityBase owner);

        /// <summary>
        /// 完全停用（共鸣件数降至 0 时调用）
        /// 必须清理所有事件订阅和临时状态
        /// </summary>
        void Deactivate();

        /// <summary>
        /// 获取当前层级提供的属性修正（纯属性类被动）
        /// 由属性管线在重算时调用
        /// </summary>
        /// <returns>属性修正块，无修正返回空 StatBlock</returns>
        StatBlock GetStatModifiers();
    }
}
