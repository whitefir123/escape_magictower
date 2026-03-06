// ============================================================================
// 逃离魔塔 - 套装被动基类 (SetPassiveBase)
// 提供 ISetPassive 的通用实现骨架，子类只需覆写关注的方法。
// ============================================================================

using EscapeTheTower.Data;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Equipment.SetResonance
{
    /// <summary>
    /// 套装被动抽象基类 —— 减少子类样板代码
    /// </summary>
    public abstract class SetPassiveBase : ISetPassive
    {
        public abstract string SetName { get; }
        public ResonanceTier ActiveTier { get; protected set; } = ResonanceTier.None;

        protected EntityBase Owner { get; private set; }

        public virtual void Activate(ResonanceTier tier, EntityBase owner)
        {
            Owner = owner;
            ActiveTier = tier;
        }

        public virtual void Deactivate()
        {
            ActiveTier = ResonanceTier.None;
            Owner = null;
        }

        public virtual StatBlock GetStatModifiers()
        {
            return new StatBlock();
        }
    }
}
