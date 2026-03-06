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
using EscapeTheTower.UI;

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

        // === 无敌帧 / 霸体状态 ===
        /// <summary>是否处于无敌帧状态（完全免疫一切伤害）</summary>
        public bool IsInvincible { get; private set; }

        /// <summary>是否处于霸体状态（免疫控制效果，但仍受伤害）</summary>
        public bool HasSuperArmor { get; private set; }

        private float _invincibleTimer;
        private float _superArmorTimer;

        // 飘字管理器缓存（避免每次 TakeDamage 调用反射查找）
        private FloatingTextManager _cachedFloatTextMgr;
        private bool _floatTextSearched;

        /// <summary>实体是否存活</summary>
        public bool IsAlive => CurrentStats.Get(StatType.HP) > 0f;

        /// <summary>当前最终属性表（经过六层管线全量重算后的镜像）</summary>
        public StatBlock CurrentStats { get; protected set; }

        /// <summary>实体数据 SO 的公共访问器</summary>
        public EntityData_SO EntityData => entityData;

        /// <summary>状态效果管理器（Awake 中自动挂载）</summary>
        public StatusEffectManager StatusEffects { get; private set; }

        // === 属性管线 ===
        protected AttributePipeline _pipeline;

        // === DOT 结算 ===
        private float _dotTickTimer;
        [System.NonSerialized] protected GridMovement _gridMovement;

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

            // 确保挂载 StatusEffectManager（英雄和怪物统一在基类处理）
            StatusEffects = GetComponent<StatusEffectManager>();
            if (StatusEffects == null)
                StatusEffects = gameObject.AddComponent<StatusEffectManager>();

            // 缓存 GridMovement 引用（用于流血移动检测）
            _gridMovement = GetComponent<GridMovement>();
        }

        protected virtual void Start()
        {
            if (entityData != null)
            {
                RecalculateStats();
            }
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;

            // 无敌帧计时器递减
            if (_invincibleTimer > 0f)
            {
                _invincibleTimer -= dt;
                if (_invincibleTimer <= 0f)
                {
                    _invincibleTimer = 0f;
                    IsInvincible = false;
                }
            }

            // 霸体计时器递减
            if (_superArmorTimer > 0f)
            {
                _superArmorTimer -= dt;
                if (_superArmorTimer <= 0f)
                {
                    _superArmorTimer = 0f;
                    HasSuperArmor = false;
                }
            }

            // DOT 定时结算
            _dotTickTimer += dt;
            if (_dotTickTimer >= GameConstants.DOT_TICK_INTERVAL)
            {
                _dotTickTimer -= GameConstants.DOT_TICK_INTERVAL;
                TickDOT();
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

            // 合并 StatusEffect 的 Buff/Debuff 属性到层级6
            // 每次全量重算时重新生成，确保不使用增量逻辑
            var allBuffs = new List<StatBlock>(_tempBuffStats);
            var statusBlock = StatusEffects?.GenerateBuffStatBlock();
            if (statusBlock != null) allBuffs.Add(statusBlock);

            // 全量重算
            CurrentStats = _pipeline.RecalculateAll(
                entityData,
                _talentBonuses,
                _equipmentStats,
                _runeStats,
                allBuffs
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

            // 无敌帧检查：完全免疫一切伤害
            if (IsInvincible)
            {
                // 显示"免疫"飘字
                GetFloatingTextManager()?.Show(transform.position, "免疫", FloatingTextType.Immune);
                Debug.Log($"[Entity] {gameObject.name} 处于无敌帧，伤害被完全免疫！");
                return;
            }

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

            // 显示伤害飘字
            var floatTextMgr = GetFloatingTextManager();
            if (floatTextMgr != null)
            {
                // 受击方为怪物（Enemy）→ 伤害来自玩家 → 红色
                // 受击方为玩家（Player）→ 伤害来自怪物 → 白色
                bool isPlayerDamage = (Faction == Faction.Enemy);
                floatTextMgr.ShowDamage(transform.position, finalDamage,
                    damage.IsCritical, damage.DamageType, isPlayerDamage);
            }

            // 死亡检测
            if (!IsAlive)
            {
                OnDeath(attackerID);
            }
        }

        /// <summary>
        /// 治疗/回复生命值
        /// 灼烧状态下治疗效果衰减（每层10%，上限60%）
        /// 来源：GameData_Blueprints/02_Status_Ailments.md §2
        /// </summary>
        public virtual void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            // 灼烧抑制治疗
            if (StatusEffects != null)
            {
                int burnStacks = StatusEffects.GetStacks(StatusEffectType.Burn);
                if (burnStacks > 0)
                {
                    float reduction = Mathf.Min(
                        burnStacks * GameConstants.BURN_HEAL_REDUCTION_PER_STACK,
                        GameConstants.BURN_HEAL_REDUCTION_MAX);
                    amount *= (1f - reduction);
                }
            }

            float maxHP = CurrentStats.Get(StatType.MaxHP);
            float currentHP = CurrentStats.Get(StatType.HP);
            CurrentStats.Set(StatType.HP, Mathf.Min(currentHP + amount, maxHP));
        }

        // =====================================================================
        //  DOT 结算引擎
        //  来源：GameData_Blueprints/02_Status_Ailments.md
        // =====================================================================

        /// <summary>
        /// DOT 每秒结算（中毒/灼烧/流血/感电）
        /// 中毒：每层每秒固定毒属性真实伤害
        /// 灼烧：每层每秒按目标最大HP百分比的真实伤害
        /// 流血：每层每秒固定物理伤害，移动中伤害翻倍
        /// 感电（Shock）：每 tick 对自身造成雷伤并溅射周围敌方
        /// </summary>
        private void TickDOT()
        {
            if (!IsAlive || StatusEffects == null) return;

            var effects = StatusEffects.ActiveEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                float damage = 0f;
                DamageType dtype = DamageType.True;

                switch (e.EffectType)
                {
                    case StatusEffectType.Poison:
                        // 每层每秒固定毒伤（真实伤害）
                        damage = e.Stacks * e.ValuePerStack;
                        break;

                    case StatusEffectType.Burn:
                        // 每层每秒 = 最大HP百分比
                        damage = e.Stacks * CurrentStats.Get(StatType.MaxHP)
                                 * GameConstants.BURN_PERCENT_MAX_HP;
                        break;

                    case StatusEffectType.Bleed:
                        // 每层每秒固定物理伤害
                        damage = e.Stacks * e.ValuePerStack;
                        // 移动惩罚：移动中伤害翻倍
                        if (_gridMovement != null && _gridMovement.IsMoving)
                            damage *= GameConstants.BLEED_MOVE_MULTIPLIER;
                        dtype = DamageType.Physical;
                        break;

                    case StatusEffectType.Shock:
                        // 感电连营：对自身造成小额雷伤 + 溅射周围敌方
                        // 基础伤害 = 固定值（由施加者 ATK 决定，在 ApplyEffect 时写入 ValuePerStack）
                        // 如果 ValuePerStack == 0，使用默认值 5
                        float shockDmg = e.ValuePerStack > 0 ? e.ValuePerStack : 5f;
                        damage = shockDmg;
                        dtype = DamageType.Magical;

                        // 溅射周围 3 格内的敌方实体（最多 3 个）
                        TickShockChainDamage(shockDmg, e.ApplierID);
                        break;
                }

                if (damage > 0f)
                {
                    Debug.Log($"[DOT] {gameObject.name} 受到 {e.EffectType} 伤害：{damage:F1} (层数={e.Stacks})");
                    var result = new DamageResult
                    {
                        FinalDamage = damage,
                        DamageType = dtype,
                        IsCritical = false,
                    };
                    TakeDamage(result, e.ApplierID);
                    if (!IsAlive) return; // DOT 致死，中断后续结算
                }
            }
        }

        /// <summary>
        /// 感电连营溅射：搜索周围 3 格内最多 3 个同阵营实体，各造成雷伤
        /// </summary>
        private void TickShockChainDamage(float baseDamage, int applierID)
        {
            const float CHAIN_RANGE = 3f;
            const int MAX_CHAIN_TARGETS = 3;

            var allEntities = FindObjectsByType<EntityBase>(FindObjectsSortMode.None);
            float sqrRange = CHAIN_RANGE * CHAIN_RANGE;
            int hitCount = 0;

            foreach (var entity in allEntities)
            {
                if (hitCount >= MAX_CHAIN_TARGETS) break;
                if (entity == this || !entity.IsAlive) continue;
                // 溅射同阵营的其他实体（自身身上有感电 → 伤害扩散给周围同伴）
                if (entity.Faction != Faction) continue;

                float sqrDist = (entity.transform.position - transform.position).sqrMagnitude;
                if (sqrDist > sqrRange) continue;

                var chainResult = new DamageResult
                {
                    FinalDamage = baseDamage * 0.5f, // 溅射伤害 = 50%
                    DamageType = DamageType.Magical,
                    IsCritical = false,
                };
                entity.TakeDamage(chainResult, applierID);
                hitCount++;
            }
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
        //  飘字管理器缓存
        // =====================================================================

        /// <summary>
        /// 获取飘字管理器（首次调用缓存，后续直接返回）
        /// </summary>
        private FloatingTextManager GetFloatingTextManager()
        {
            if (!_floatTextSearched)
            {
                _floatTextSearched = true;
                _cachedFloatTextMgr = FindAnyObjectByType<FloatingTextManager>();
            }
            return _cachedFloatTextMgr;
        }

        // =====================================================================
        //  无敌帧 / 霸体状态管理
        // =====================================================================

        /// <summary>
        /// 进入无敌帧状态（持续指定秒数，期间完全免疫伤害）。
        /// 多次调用取最长剩余时间。
        /// </summary>
        public void SetInvincible(float duration)
        {
            if (duration <= 0f) return;
            IsInvincible = true;
            _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
            Debug.Log($"[Entity] {gameObject.name} 进入无敌帧 {duration:F2}s");
        }

        /// <summary>
        /// 进入霸体状态（持续指定秒数，期间免疫控制效果但仍受伤害）。
        /// 多次调用取最长剩余时间。
        /// </summary>
        public void SetSuperArmor(float duration)
        {
            if (duration <= 0f) return;
            HasSuperArmor = true;
            _superArmorTimer = Mathf.Max(_superArmorTimer, duration);
            Debug.Log($"[Entity] {gameObject.name} 进入霸体 {duration:F2}s");
        }

        /// <summary>
        /// 强制清除无敌帧和霸体状态（技能结束时主动调用）
        /// </summary>
        public void ClearCombatStates()
        {
            IsInvincible = false;
            _invincibleTimer = 0f;
            HasSuperArmor = false;
            _superArmorTimer = 0f;
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
