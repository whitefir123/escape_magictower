// ============================================================================
// 逃离魔塔 - 怪物基类 (MonsterBase)
// 继承 EntityBase，实现怪物 AI 行为、仇恨追击、被动反击等核心逻辑。
//
// 核心碰撞法则（不对称法则）：
//   1. 玩家主动碰撞怪物 → 怪物立即被动反击一次
//   2. 怪物被攻击后进入追击状态 → 主动冲锋玩家
//   3. 玩家被怪物攻击时绝不自动反击
//
// 来源：DesignDocs/03_Combat_System.md 第 1.1 节
//       GameData_Blueprints/04_01_Monster_Spawn_Logic.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity.Monster
{
    /// <summary>
    /// 怪物 AI 状态枚举
    /// </summary>
    public enum MonsterAIState
    {
        Idle,       // 闲置（未被激怒）
        Pursuing,   // 追击状态（仇恨链激活）
        Attacking,  // 正在攻击
        Stunned,    // 被控制（眩晕/冰封/定身）
        Dead,       // 死亡
    }

    /// <summary>
    /// 怪物基类 —— 所有地下城怪物的公共组件
    /// </summary>
    public class MonsterBase : EntityBase
    {
        [Header("=== 怪物专属数据 ===")]
        [SerializeField] private MonsterData_SO monsterData;

        /// <summary>当前 AI 状态</summary>
        public MonsterAIState AIState { get; private set; } = MonsterAIState.Idle;

        /// <summary>怪物种族标签</summary>
        public MonsterTag Tags => monsterData != null ? monsterData.tags : MonsterTag.None;

        /// <summary>是否为精英</summary>
        public bool IsElite { get; private set; }

        /// <summary>精英属性倍率</summary>
        public float EliteMultiplier { get; private set; } = 1f;

        // === 追击目标 ===
        private Transform _pursuitTarget;
        private float _attackCooldownTimer;
        private float _attackInterval;

        // === 战斗疲劳 ===
        private int _fatigueStacks;
        private float _fatigueTimer;
        private const float FATIGUE_TICK_INTERVAL = 10f; // 每10秒叠加一层疲劳

        // === 硬控递减（Boss 专用）===
        private int _ccApplicationCount;
        private float _ccResistResetTimer;

        // =====================================================================
        //  生命周期
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();
            if (monsterData != null)
            {
                entityData = monsterData;
            }
            Faction = Faction.Enemy;
        }

        private void Update()
        {
            if (!IsAlive)
            {
                AIState = MonsterAIState.Dead;
                return;
            }

            UpdateAI();
            UpdateFatigue();
            UpdateAttackCooldown();
        }

        // =====================================================================
        //  初始化（由生成器调用）
        // =====================================================================

        /// <summary>
        /// 使用同心圆距离法则初始化怪物属性
        /// </summary>
        /// <param name="distanceFactor">[0,1] 距离系数</param>
        /// <param name="currentFloor">当前楼层</param>
        /// <param name="isEliteMutation">是否发生精英突变</param>
        /// <param name="eliteMult">精英倍率</param>
        public void Initialize(float distanceFactor, int currentFloor, bool isEliteMutation = false, float eliteMult = 1f)
        {
            if (monsterData == null)
            {
                Debug.LogError("[MonsterBase] monsterData 为 null！");
                return;
            }

            // 计算楼层乘数（需要 BiomeConfig 提供，此处用默认常量）
            float floorMultiplier = 1f + 0.15f * Mathf.Max(0, currentFloor - 1);

            // 使用同心圆法则生成属性
            CurrentStats = monsterData.CreateScaledStatBlock(distanceFactor, floorMultiplier);

            // 精英突变
            if (isEliteMutation)
            {
                ApplyEliteMutation(eliteMult);
            }

            _attackInterval = monsterData.attackInterval;
            _attackCooldownTimer = _attackInterval;

            Debug.Log($"[MonsterBase] {monsterData.entityName} 初始化完成。" +
                      $" HP={CurrentStats.Get(StatType.MaxHP):F0}" +
                      $" ATK={CurrentStats.Get(StatType.ATK):F0}" +
                      (IsElite ? " 【精英】" : ""));
        }

        /// <summary>
        /// 应用精英突变
        /// 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md
        /// </summary>
        private void ApplyEliteMutation(float multiplier)
        {
            IsElite = true;
            EliteMultiplier = multiplier;

            // 体积放大
            transform.localScale *= GameConstants.ELITE_SCALE_MULTIPLIER;

            // 属性膨胀（HP、ATK、MATK 乘以精英倍率）
            CurrentStats.Multiply(StatType.MaxHP, multiplier);
            CurrentStats.Set(StatType.HP, CurrentStats.Get(StatType.MaxHP));
            CurrentStats.Multiply(StatType.ATK, multiplier);
            CurrentStats.Multiply(StatType.MATK, multiplier);

            Debug.Log($"[MonsterBase] {monsterData.entityName} 精英突变！倍率={multiplier:F1}x");
        }

        // =====================================================================
        //  AI 行为
        // =====================================================================

        private void UpdateAI()
        {
            switch (AIState)
            {
                case MonsterAIState.Idle:
                    // 闲置：不主动攻击，等待被玩家激怒
                    break;

                case MonsterAIState.Pursuing:
                    PursueTarget();
                    TryAttack();
                    break;

                case MonsterAIState.Attacking:
                    // 攻击动画/逻辑中（由协程管理，回到 Pursuing）
                    break;

                case MonsterAIState.Stunned:
                    // 被控制中，无法行动
                    break;
            }
        }

        /// <summary>
        /// 追击目标
        /// </summary>
        private void PursueTarget()
        {
            if (_pursuitTarget == null) return;

            float moveSpeed = CurrentStats.Get(StatType.MoveSpeed) * 2.5f;
            Vector3 direction = (_pursuitTarget.position - transform.position).normalized;
            transform.position += direction * (moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 尝试攻击（基于攻击间隔）
        /// </summary>
        private void TryAttack()
        {
            if (_pursuitTarget == null) return;

            float distance = Vector3.Distance(transform.position, _pursuitTarget.position);
            float attackRange = 1.2f; // 近战攻击范围

            if (distance <= attackRange && _attackCooldownTimer <= 0f)
            {
                PerformAttack();
                _attackCooldownTimer = _attackInterval;
            }
        }

        /// <summary>
        /// 执行主动攻击
        /// </summary>
        private void PerformAttack()
        {
            if (_pursuitTarget == null) return;

            var targetEntity = _pursuitTarget.GetComponent<EntityBase>();
            if (targetEntity == null || !targetEntity.IsAlive) return;

            var damageResult = DamageCalculator.Calculate(
                CurrentStats,
                targetEntity.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,      // 100% ATK
                damageType: DamageType.Physical
            );

            // 闪避检测
            if (!DamageCalculator.CheckDodge(targetEntity.CurrentStats))
            {
                targetEntity.TakeDamage(damageResult, EntityID);
            }
        }

        // =====================================================================
        //  碰撞反击系统
        //  类魔塔核心：玩家碰撞 = 主动攻击，怪物立即被动反击
        // =====================================================================

        /// <summary>
        /// 被玩家主动攻击后，立即执行被动反击一次
        /// 来源：DesignDocs/03_Combat_System.md 第 1.1 节
        /// </summary>
        public void OnHitByPlayer(EntityBase player)
        {
            if (!IsAlive) return;

            // 被动反击：无视攻击间隔，立即打一次
            var counterDamage = DamageCalculator.Calculate(
                CurrentStats,
                player.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Physical
            );

            if (!DamageCalculator.CheckDodge(player.CurrentStats))
            {
                player.TakeDamage(counterDamage, EntityID);
            }

            // 进入追击状态
            EngageTarget(player.transform);
        }

        /// <summary>
        /// 锁定追击目标并进入追击状态
        /// </summary>
        public void EngageTarget(Transform target)
        {
            _pursuitTarget = target;
            AIState = MonsterAIState.Pursuing;
        }

        // =====================================================================
        //  战斗疲劳（软狂暴）
        //  来源：DesignDocs/03_Combat_System.md 第 1.4 节
        // =====================================================================

        private void UpdateFatigue()
        {
            if (AIState != MonsterAIState.Pursuing && AIState != MonsterAIState.Attacking) return;

            _fatigueTimer += Time.deltaTime;
            if (_fatigueTimer >= FATIGUE_TICK_INTERVAL)
            {
                _fatigueTimer = 0f;
                _fatigueStacks++;

                // 每层疲劳：攻击力 +5%，穿甲 +2%
                // 直接修改面板（临时战斗增益）
                CurrentStats.Multiply(StatType.ATK, 1.05f);
                CurrentStats.Add(StatType.ArmorPen, 0.02f);

                if (_fatigueStacks >= GameConstants.FATIGUE_LETHAL_STACKS)
                {
                    Debug.LogWarning($"[MonsterBase] {monsterData?.entityName} 疲劳层数 {_fatigueStacks} " +
                                    "已达秒杀阈值！");
                }
            }
        }

        private void UpdateAttackCooldown()
        {
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }
        }

        // =====================================================================
        //  CC 递减（Boss 硬控抗性）
        //  来源：DesignDocs/09_DataFlow_and_Status.md 第 2.2 节
        // =====================================================================

        /// <summary>
        /// 获取硬控的递减后持续时间
        /// </summary>
        /// <param name="baseDuration">原始持续时间</param>
        /// <returns>递减后的实际持续时间</returns>
        public float GetDiminishedCCDuration(float baseDuration)
        {
            if (monsterData == null) return baseDuration;
            if (monsterData.hasPermUnstoppable) return 0f; // 永久霸体，免疫一切硬控

            if (!monsterData.hasCCDiminishing) return baseDuration;

            _ccApplicationCount++;

            // 递减公式：第N次 = baseDuration / 2^(N-1)
            float diminishedDuration = baseDuration / Mathf.Pow(2f, _ccApplicationCount - 1);

            // 低于 0.3 秒视为免控（免控抵抗期）
            if (diminishedDuration < 0.3f)
            {
                diminishedDuration = 0f;
            }

            return diminishedDuration;
        }

        // =====================================================================
        //  死亡重写
        // =====================================================================

        protected override void OnDeath(int killerID)
        {
            AIState = MonsterAIState.Dead;

            // 掉落经验值
            if (monsterData != null)
            {
                int baseExp = monsterData.baseExpReward;
                float expMultiplier = IsElite ? GameConstants.ELITE_EXP_MULTIPLIER : 1f;
                int finalExp = Mathf.RoundToInt(baseExp * expMultiplier);

                EventManager.Publish(new OnExpGainedEvent
                {
                    Meta = new EventMeta(EntityID),
                    Amount = finalExp,
                });

                // 掉落金币
                int gold = Random.Range(monsterData.goldDropMin, monsterData.goldDropMax + 1);
                if (IsElite) gold = Mathf.RoundToInt(gold * 2f);

                EventManager.Publish(new OnGoldGainedEvent
                {
                    Meta = new EventMeta(EntityID),
                    Amount = gold,
                });
            }

            base.OnDeath(killerID);
        }
    }
}
