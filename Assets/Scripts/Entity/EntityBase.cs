// ============================================================================
// 逃离魔塔 - 实体基类 (EntityBase)
// 所有参与战斗的实体（英雄、怪物）的公共基础组件。
// 管理 HP/MP/Rage、属性管线、状态效果、死亡回调。
//
// 来源：DesignDocs/02_Entities_and_Stats.md
//       DesignDocs/09_DataFlow_and_Status.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity
{
    /// <summary>
    /// 实体基类 —— 所有战斗参与者的公共组件
    /// </summary>
    public class EntityBase : MonoBehaviour
    {
        [Header("=== 数据配置 ===")]
        [Tooltip("实体数据 SO（由子类或 Inspector 赋值）")]
        [SerializeField] protected EntityData_SO entityData;

        /// <summary>运行时唯一标识（用于事件系统的 SourceID / TargetID）</summary>
        public int EntityID { get; private set; }

        /// <summary>实体阵营</summary>
        public Faction Faction { get; set; } = Faction.Enemy;

        /// <summary>实体是否存活</summary>
        public bool IsAlive => CurrentStats.Get(StatType.HP) > 0f;

        /// <summary>当前最终属性表（经过六层管线全量重算后的镜像）</summary>
        public StatBlock CurrentStats { get; private set; }

        /// <summary>实体数据 SO 的公共访问器</summary>
        public EntityData_SO EntityData => entityData;

        // === 属性管线 ===
        protected AttributePipeline _pipeline;

        // === 管线各层级的数据输入（子类或外部系统填充） ===
        protected StatBlock _talentBonuses;                  // 层级2：天赋树
        protected List<StatBlock> _equipmentStats;           // 层级3：装备
        protected List<StatBlock> _runeStats;                // 层级4：符文
        protected List<StatBlock> _tempBuffStats;            // 层级6：临时 Buff

        // === ID 分配器 ===
        private static int _nextEntityID = 1;

        // =====================================================================
        //  生命周期
        // =====================================================================

        protected virtual void Awake()
        {
            EntityID = _nextEntityID++;
            _pipeline = new AttributePipeline();
            _equipmentStats = new List<StatBlock>();
            _runeStats = new List<StatBlock>();
            _tempBuffStats = new List<StatBlock>();
            CurrentStats = new StatBlock();
        }

        protected virtual void Start()
        {
            if (entityData != null)
            {
                RecalculateStats();
            }
        }

        // =====================================================================
        //  属性管线
        // =====================================================================

        /// <summary>
        /// 触发六层管线全量重算，刷新 CurrentStats
        /// 任何装备/符文/Buff 变动后必须调用此方法
        /// </summary>
        public virtual void RecalculateStats()
        {
            if (entityData == null)
            {
                Debug.LogError($"[EntityBase] 实体 {gameObject.name} 的 entityData 为 null，无法重算属性！");
                return;
            }

            // 保留重算前的 HP/MP/Rage 当前值（用于非满血时保持比例）
            float prevMaxHP = CurrentStats.Get(StatType.MaxHP);
            float prevHP = CurrentStats.Get(StatType.HP);
            float prevMaxMP = CurrentStats.Get(StatType.MaxMP);
            float prevMP = CurrentStats.Get(StatType.MP);
            float prevRage = CurrentStats.Get(StatType.Rage);

            // 全量重算
            CurrentStats = _pipeline.RecalculateAll(
                entityData,
                _talentBonuses,
                _equipmentStats,
                _runeStats,
                _tempBuffStats
            );

            // 智能恢复 HP/MP：如果之前是满血则保持满血，否则按比例缩放
            float newMaxHP = CurrentStats.Get(StatType.MaxHP);
            float newMaxMP = CurrentStats.Get(StatType.MaxMP);

            if (prevMaxHP > 0f)
            {
                // 如果之前是满血，维持满血
                float hpRatio = (Mathf.Approximately(prevHP, prevMaxHP)) ? 1f : (prevHP / prevMaxHP);
                CurrentStats.Set(StatType.HP, Mathf.Clamp(newMaxHP * hpRatio, 0f, newMaxHP));
            }

            if (prevMaxMP > 0f)
            {
                float mpRatio = (Mathf.Approximately(prevMP, prevMaxMP)) ? 1f : (prevMP / prevMaxMP);
                CurrentStats.Set(StatType.MP, Mathf.Clamp(newMaxMP * mpRatio, 0f, newMaxMP));
            }

            // 怒气保留原值（不随管线变化按比例缩放）
            CurrentStats.Set(StatType.Rage, Mathf.Clamp(prevRage, 0f, CurrentStats.Get(StatType.MaxRage)));

            // 广播属性变更事件
            EventManager.Publish(new OnEntityStatChangedEvent
            {
                Meta = new EventMeta(EntityID),
                EntityID = EntityID,
            });
        }

        // =====================================================================
        //  伤害与治疗
        // =====================================================================

        /// <summary>
        /// 受到伤害（经过完整结算后的最终扣除）
        /// </summary>
        /// <param name="damage">DamageCalculator 计算出的结果</param>
        /// <param name="attackerID">攻击方实体 ID</param>
        public virtual void TakeDamage(DamageResult damage, int attackerID)
        {
            if (!IsAlive) return;

            // === 承伤前事件（允许被劫持，如免死金牌） ===
            var beforeEvent = new OnEntityTakeDamageBeforeEvent
            {
                Meta = new EventMeta(attackerID),
                TargetEntityID = EntityID,
                AttackerEntityID = attackerID,
                DamageAmount = damage.FinalDamage,
                DamageType = damage.DamageType,
                IsIntercepted = false,
            };
            EventManager.Publish(beforeEvent);

            if (beforeEvent.IsIntercepted)
            {
                return; // 伤害被劫持（如圣骑免死金牌）
            }

            // 实际扣血
            float currentHP = CurrentStats.Get(StatType.HP);
            float finalDamage = Mathf.Max(0f, beforeEvent.DamageAmount);
            currentHP -= finalDamage;
            CurrentStats.Set(StatType.HP, Mathf.Max(0f, currentHP));

            // 受伤积攒怒气（受到伤害的 10% 转化为怒气）
            float rageGain = finalDamage * 0.10f;
            AddRage(rageGain);

            // 广播受伤事件
            EventManager.Publish(new OnEntityDamagedEvent
            {
                Meta = new EventMeta(attackerID),
                TargetEntityID = EntityID,
                AttackerEntityID = attackerID,
                FinalDamage = finalDamage,
                DamageType = damage.DamageType,
                WasCritical = damage.IsCritical,
            });

            // 死亡检测
            if (!IsAlive)
            {
                OnDeath(attackerID);
            }
        }

        /// <summary>
        /// 治疗/回复生命值
        /// </summary>
        public virtual void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            float maxHP = CurrentStats.Get(StatType.MaxHP);
            float currentHP = CurrentStats.Get(StatType.HP);
            CurrentStats.Set(StatType.HP, Mathf.Min(currentHP + amount, maxHP));
        }

        // =====================================================================
        //  资源管理
        // =====================================================================

        /// <summary>
        /// 消耗法力值，返回是否消耗成功
        /// </summary>
        public bool ConsumeMana(float amount)
        {
            float currentMP = CurrentStats.Get(StatType.MP);
            if (currentMP < amount) return false;

            CurrentStats.Set(StatType.MP, currentMP - amount);
            return true;
        }

        /// <summary>
        /// 恢复法力值
        /// </summary>
        public void RestoreMana(float amount)
        {
            float maxMP = CurrentStats.Get(StatType.MaxMP);
            float currentMP = CurrentStats.Get(StatType.MP);
            CurrentStats.Set(StatType.MP, Mathf.Min(currentMP + amount, maxMP));
        }

        /// <summary>
        /// 增加怒气值
        /// </summary>
        public void AddRage(float amount)
        {
            float maxRage = CurrentStats.Get(StatType.MaxRage);
            float currentRage = CurrentStats.Get(StatType.Rage);
            CurrentStats.Set(StatType.Rage, Mathf.Min(currentRage + amount, maxRage));
        }

        /// <summary>
        /// 怒气是否已满（可释放大招）
        /// </summary>
        public bool IsRageFull()
        {
            float rage = CurrentStats.Get(StatType.Rage);
            float maxRage = CurrentStats.Get(StatType.MaxRage);
            return rage >= maxRage && maxRage > 0f;
        }

        /// <summary>
        /// 消耗全部怒气（大招释放后调用）
        /// </summary>
        public void ConsumeAllRage()
        {
            CurrentStats.Set(StatType.Rage, 0f);
        }

        // =====================================================================
        //  符文管理（层级4输入）
        // =====================================================================

        /// <summary>
        /// 添加一枚符文的属性增益到管线层级4
        /// </summary>
        public void AddRuneStat(StatBlock runeBonus)
        {
            if (runeBonus == null) return;
            _runeStats.Add(runeBonus);
            RecalculateStats();
        }

        // =====================================================================
        //  临时 Buff 管理（层级6输入）
        // =====================================================================

        /// <summary>
        /// 添加临时 Buff 属性到管线层级6
        /// </summary>
        public void AddTempBuff(StatBlock buffBonus)
        {
            if (buffBonus == null) return;
            _tempBuffStats.Add(buffBonus);
            RecalculateStats();
        }

        /// <summary>
        /// 移除临时 Buff 并触发重算
        /// </summary>
        public void RemoveTempBuff(StatBlock buffBonus)
        {
            if (_tempBuffStats.Remove(buffBonus))
            {
                RecalculateStats();
            }
        }

        // =====================================================================
        //  死亡处理
        // =====================================================================

        /// <summary>
        /// 死亡回调 —— 子类可重写以实现不同的死亡行为
        /// </summary>
        protected virtual void OnDeath(int killerID)
        {
            // 广播死亡事件
            EventManager.Publish(new OnEntityDeathEvent
            {
                Meta = new EventMeta(EntityID),
                EntityID = EntityID,
            });

            // 广播击杀事件（给击杀者用）
            EventManager.Publish(new OnEntityKillEvent
            {
                Meta = new EventMeta(killerID),
                KillerEntityID = killerID,
                VictimEntityID = EntityID,
            });

            // 将销毁操作写入指令缓存队列，LateUpdate 统一处理
            if (ServiceLocator.TryGet<CommandBuffer>(out var cmdBuffer))
            {
                cmdBuffer.Enqueue(new DestroyEntityCommand(gameObject));
            }
        }

        // =====================================================================
        //  调试
        // =====================================================================

        /// <summary>
        /// 打印当前属性面板（调试用）
        /// </summary>
        [ContextMenu("打印当前属性")]
        public void DebugPrintStats()
        {
            Debug.Log($"[Entity:{entityData?.entityName ?? "Unknown"}] ID={EntityID} " +
                      $"Alive={IsAlive}\n{CurrentStats}");
        }
    }
}
