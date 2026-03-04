// ============================================================================
// 逃离魔塔 - 第一层 Boss AI：堕落的人型勇士 (FallenHeroBossAI)
// 专属行为模式（基于格子移动系统）：
//   阶段一（HP > 50%）：格子追击 + 勇者冲锋（高速直线步进+击退+眩晕）
//   阶段二（HP ≤ 50%）：附加处决大风车（霸体+缓速追击+AOE伤害）
//
// 设计要点：
//   - 追击移动由 MonsterBase.PursueTarget 驱动（保证 SetDirection 在
//     GridMovement.Update 之前执行，解决组件执行顺序问题）
//   - 冲锋方向锁定通过 MonsterBase.OverridePursuitDirection 实现
//   - Boss 启用后设置 ExternalAttackControl = true，碰撞攻击由 Boss AI 管理
//
// 来源：GameData_Blueprints/04_1_Floor1_Dungeon.md 第三节
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity.Monster
{
    /// <summary>
    /// Boss 战斗阶段
    /// </summary>
    public enum BossPhase
    {
        Phase1_Normal,      // HP > 50%：普攻追击 + 勇者冲锋
        Phase2_Whirlwind,   // HP ≤ 50%：附加处决大风车
    }

    /// <summary>
    /// 堕落的人型勇士 —— 第一层关底 Boss 专属 AI
    /// </summary>
    [RequireComponent(typeof(MonsterBase))]
    public class FallenHeroBossAI : MonoBehaviour
    {
        private MonsterBase _monster;
        private GridMovement _gridMovement;
        private Transform _playerTarget;

        /// <summary>当前 Boss 战斗阶段</summary>
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1_Normal;

        // === 勇者冲锋参数 ===
        private const float CHARGE_COOLDOWN = 8f;
        private const float CHARGE_SPEED = 20f;           // 冲锋移动速度（格/秒）
        private const float CHARGE_DURATION = 0.8f;        // 冲锋持续时间（秒）
        private const float CHARGE_DAMAGE_MULT = 2.5f;     // 冲锋伤害 = 250% ATK
        private const float CHARGE_STUN_DURATION = 1.0f;
        private const float CHARGE_MIN_DISTANCE = 2f;      // 冲锋最小触发距离（格）
        private float _chargeTimer;
        private bool _isCharging;
        private Vector2Int _chargeDirection;
        private float _chargeElapsed;
        private bool _chargeHit;
        private float _normalMoveSpeed;

        // === 处决大风车参数 ===
        private const float WHIRLWIND_DURATION = 5f;
        private const float WHIRLWIND_TICK_INTERVAL = 0.3f;
        private const float WHIRLWIND_DAMAGE_MULT = 0.6f;
        private const float WHIRLWIND_MOVE_SPEED = 2f;
        private const float WHIRLWIND_AOE_RADIUS = 2.5f;
        private const float WHIRLWIND_COOLDOWN = 15f;
        private bool _whirlwindActive;
        private float _whirlwindTimer;
        private float _whirlwindTickTimer;
        private float _whirlwindCooldownTimer;
        private bool _hasEnteredPhase2;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _monster = GetComponent<MonsterBase>();
            _gridMovement = GetComponent<GridMovement>();
        }

        private void Start()
        {
            // 追击移动由 MonsterBase.PursueTarget 执行（保证执行时序正确）
            // 普攻碰撞由 MonsterBase.OnGridMoveBlocked 处理（含冷却+AllowBump 管理）
            // ExternalAttackControl 仅在冲锋期间临时开启

            // 记录正常移动速度
            _normalMoveSpeed = _monster.CurrentStats.Get(StatType.MoveSpeed) * 3f;
            if (_normalMoveSpeed < 0.5f) _normalMoveSpeed = 3f;

            // 初始冲锋冷却（给玩家准备时间）
            _chargeTimer = 3f;

            // 缓存玩家引用（不激活追击，Boss 默认巡逻）
            FindPlayer();

            Debug.Log("[Boss-堕落勇士] Boss AI 已激活！默认巡逻模式，被攻击后追击。");
        }

        private void Update()
        {
            if (!_monster.IsAlive) return;

            if (_playerTarget == null) FindPlayer();
            if (_playerTarget == null) return;

            // Boss 未被激怒前保持巡逻，不执行冲锋/大风车
            if (_monster.AIState != MonsterAIState.Pursuing) return;

            UpdatePhaseTransition();

            switch (CurrentPhase)
            {
                case BossPhase.Phase1_Normal:
                    UpdatePhase1();
                    break;
                case BossPhase.Phase2_Whirlwind:
                    UpdatePhase2();
                    break;
            }
        }

        // =====================================================================
        //  阶段切换
        // =====================================================================

        private void UpdatePhaseTransition()
        {
            if (_hasEnteredPhase2) return;

            float hpRatio = _monster.CurrentStats.Get(StatType.HP) /
                            Mathf.Max(1f, _monster.CurrentStats.Get(StatType.MaxHP));

            if (hpRatio <= 0.50f)
            {
                _hasEnteredPhase2 = true;
                CurrentPhase = BossPhase.Phase2_Whirlwind;
                _whirlwindCooldownTimer = 0f;
                Debug.Log("[Boss-堕落勇士] ⚠️ 进入第二阶段！HP ≤ 50%");
            }
        }

        // =====================================================================
        //  阶段一：格子追击 + 勇者冲锋
        //  追击由 MonsterBase.PursueTarget 自动执行（Update 顺序正确）
        //  冲锋时通过 OverridePursuitDirection 锁定方向
        // =====================================================================

        private void UpdatePhase1()
        {
            float dt = Time.deltaTime;

            if (_isCharging)
            {
                ExecuteCharge(dt);
                return;
            }

            // 非冲锋期间：追击由 MonsterBase.PursueTarget 自动处理
            // 不需要手动调用 SetDirection

            // 冲锋冷却
            _chargeTimer -= dt;

            if (_chargeTimer <= 0f && _playerTarget != null)
            {
                float distance = Vector2.Distance(
                    (Vector2)(Vector2Int)_gridMovement.GridPosition,
                    (Vector2)GridMovement.WorldToGrid(_playerTarget.position));

                if (distance >= CHARGE_MIN_DISTANCE)
                {
                    StartCharge();
                }
            }
        }

        // =====================================================================
        //  勇者冲锋
        // =====================================================================

        private void StartCharge()
        {
            _isCharging = true;
            _chargeElapsed = 0f;
            _chargeHit = false;

            // 锁定冲锋方向
            Vector2 diff = _playerTarget.position - transform.position;
            if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
                _chargeDirection = new Vector2Int(diff.x > 0 ? 1 : -1, 0);
            else
                _chargeDirection = new Vector2Int(0, diff.y > 0 ? 1 : -1);

            // 临时大幅加速
            _gridMovement.SetMoveSpeed(CHARGE_SPEED);

            // 通过 OverridePursuitDirection 锁定方向
            _monster.OverridePursuitDirection = _chargeDirection;

            // 冲锋期间由 Boss AI 处理碰撞伤害（跳过 MonsterBase 的默认攻击）
            _monster.ExternalAttackControl = true;

            // 订阅碰撞事件（冲锋命中检测）
            _gridMovement.OnMoveBlocked += OnChargeBlocked;

            Debug.Log("[Boss-堕落勇士] 🗡️ 勇者冲锋！");
        }

        private void ExecuteCharge(float dt)
        {
            _chargeElapsed += dt;

            // 每帧持续设置锁定方向（MonsterBase.PursueTarget 会读取此值）
            _monster.OverridePursuitDirection = _chargeDirection;

            // 冲锋超时结束
            if (_chargeElapsed >= CHARGE_DURATION)
            {
                EndCharge();
            }
        }

        private void EndCharge()
        {
            _isCharging = false;
            _chargeTimer = CHARGE_COOLDOWN;

            // 恢复正常速度
            _gridMovement.SetMoveSpeed(_normalMoveSpeed);

            // 清除方向覆盖，恢复默认追踪
            _monster.OverridePursuitDirection = null;

            // 恢复 MonsterBase 的默认碰撞攻击
            _monster.ExternalAttackControl = false;

            // 取消冲锋碰撞事件
            _gridMovement.OnMoveBlocked -= OnChargeBlocked;
        }

        /// <summary>
        /// 冲锋碰撞命中：额外伤害 + 击退 + 眩晕
        /// </summary>
        private void OnChargeBlocked(Vector2Int blockedTile, GameObject blocker)
        {
            if (_chargeHit || blocker == null) return;

            var playerEntity = blocker.GetComponent<EntityBase>();
            if (playerEntity == null || !playerEntity.IsAlive) return;
            if (playerEntity.Faction != Faction.Player) return;

            _chargeHit = true;

            // 冲锋额外伤害（250% ATK）
            var damageResult = DamageCalculator.Calculate(
                _monster.CurrentStats,
                playerEntity.CurrentStats,
                baseDamage: 0f,
                atkScaling: CHARGE_DAMAGE_MULT,
                damageType: DamageType.Physical
            );

            if (!DamageCalculator.CheckDodge(playerEntity.CurrentStats))
            {
                playerEntity.TakeDamage(damageResult, _monster.EntityID);

                // 施加眩晕
                var statusMgr = blocker.GetComponent<StatusEffectManager>();
                if (statusMgr != null && !statusMgr.IsUnstoppable())
                {
                    statusMgr.ApplyEffect(StatusEffectType.Stun, CHARGE_STUN_DURATION, 0f, _monster.EntityID);
                }

                // 击退：格子级位移
                var playerGrid = blocker.GetComponent<GridMovement>();
                if (playerGrid != null)
                {
                    Vector2Int knockbackTarget = playerGrid.GridPosition + _chargeDirection * 2;
                    playerGrid.SetGridPosition(knockbackTarget);
                }

                Debug.Log($"[Boss-堕落勇士] 勇者冲锋命中！额外伤害={damageResult.FinalDamage:F0} +击退2格 +眩晕{CHARGE_STUN_DURATION}s");
            }

            EndCharge();
        }

        // =====================================================================
        //  阶段二：附加处决大风车
        // =====================================================================

        private void UpdatePhase2()
        {
            float dt = Time.deltaTime;

            if (_whirlwindActive)
            {
                ExecuteWhirlwind(dt);
                return;
            }

            // 非大风车期间：维持阶段一逻辑
            UpdatePhase1();

            _whirlwindCooldownTimer -= dt;
            if (_whirlwindCooldownTimer <= 0f)
            {
                StartWhirlwind();
            }
        }

        private void StartWhirlwind()
        {
            _whirlwindActive = true;
            _whirlwindTimer = 0f;
            _whirlwindTickTimer = 0f;

            // 大风车期间低速追击
            _gridMovement.SetMoveSpeed(WHIRLWIND_MOVE_SPEED);

            // 激活霸体
            var statusMgr = GetComponent<StatusEffectManager>();
            if (statusMgr != null)
            {
                statusMgr.ClearAllDebuffs();
                statusMgr.ApplyEffect(StatusEffectType.Unstoppable, WHIRLWIND_DURATION, 0f, _monster.EntityID);
            }

            Debug.Log("[Boss-堕落勇士] 🌪️ 处决大风车！霸体激活！");
        }

        private void ExecuteWhirlwind(float dt)
        {
            _whirlwindTimer += dt;
            _whirlwindTickTimer += dt;

            // 追击由 MonsterBase.PursueTarget 自动处理（低速模式）
            // 旋转视觉
            transform.Rotate(0f, 0f, 720f * dt);

            // AOE tick
            if (_whirlwindTickTimer >= WHIRLWIND_TICK_INTERVAL)
            {
                _whirlwindTickTimer = 0f;
                WhirlwindTick();
            }

            if (_whirlwindTimer >= WHIRLWIND_DURATION)
            {
                EndWhirlwind();
            }
        }

        private void EndWhirlwind()
        {
            _whirlwindActive = false;
            _whirlwindCooldownTimer = WHIRLWIND_COOLDOWN;
            _gridMovement.SetMoveSpeed(_normalMoveSpeed);
            transform.rotation = Quaternion.identity;
            Debug.Log("[Boss-堕落勇士] 大风车结束。");
        }

        private void WhirlwindTick()
        {
            if (_playerTarget == null) return;

            float distance = Vector3.Distance(transform.position, _playerTarget.position);
            if (distance <= WHIRLWIND_AOE_RADIUS)
            {
                var playerEntity = _playerTarget.GetComponent<EntityBase>();
                if (playerEntity != null && playerEntity.IsAlive)
                {
                    var damageResult = DamageCalculator.Calculate(
                        _monster.CurrentStats,
                        playerEntity.CurrentStats,
                        baseDamage: 0f,
                        atkScaling: WHIRLWIND_DAMAGE_MULT,
                        damageType: DamageType.Physical
                    );
                    playerEntity.TakeDamage(damageResult, _monster.EntityID);
                }
            }
        }

        // =====================================================================
        //  通用辅助
        // =====================================================================

        private void FindPlayer()
        {
            // 缓存守护：已找到玩家且引用有效时直接返回
            if (_playerTarget != null) return;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTarget = playerObj.transform;
                return;
            }

            var hero = FindAnyObjectByType<EscapeTheTower.Entity.Hero.HeroController>();
            if (hero != null)
            {
                _playerTarget = hero.transform;
            }
        }

        private void OnDestroy()
        {
            if (_gridMovement != null)
                _gridMovement.OnMoveBlocked -= OnChargeBlocked;
        }
    }
}
