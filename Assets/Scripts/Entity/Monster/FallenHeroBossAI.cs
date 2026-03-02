// ============================================================================
// 逃离魔塔 - 第一层 Boss AI：堕落的人型勇士 (FallenHeroBossAI)
// 专属行为模式：
//   阶段一（HP > 50%）：普攻 + 勇者冲锋（直线冲撞+击退+眩晕）
//   阶段二（HP ≤ 50%）：触发处决大风车（霸体持续AOE）
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
        Phase1_Normal,      // HP > 50%：普攻 + 勇者冲锋
        Phase2_Whirlwind,   // HP ≤ 50%：处决大风车模式
    }

    /// <summary>
    /// 堕落的人型勇士 —— 第一层关底 Boss 专属 AI
    /// </summary>
    [RequireComponent(typeof(MonsterBase))]
    public class FallenHeroBossAI : MonoBehaviour
    {
        private MonsterBase _monster;
        private Transform _playerTarget;

        /// <summary>当前 Boss 战斗阶段</summary>
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1_Normal;

        // === 勇者冲锋参数 ===
        private float _chargeCooldown = 8f;
        private float _chargeTimer;
        private bool _isCharging;
        private Vector3 _chargeDirection;
        private float _chargeSpeed = 8f;
        private float _chargeDuration = 0.5f;
        private float _chargeElapsed;
        private float _chargeDamageMultiplier = 2.5f; // 冲锋伤害 = 250% ATK
        private float _chargeStunDuration = 1.0f;
        private bool _chargeHit; // 防止多次命中

        // === 处决大风车参数 ===
        private bool _whirlwindActive;
        private float _whirlwindDuration = 5f;
        private float _whirlwindTimer;
        private float _whirlwindTickInterval = 0.3f; // 每0.3秒一次AOE判定
        private float _whirlwindTickTimer;
        private float _whirlwindDamageMultiplier = 0.6f; // 每跳 60% ATK
        private float _whirlwindMoveSpeed = 1.5f; // 大风车期间移速
        private float _whirlwindCooldown = 15f;
        private float _whirlwindCooldownTimer;
        private bool _hasEnteredPhase2;

        // === 普攻计时器 ===
        private float _attackTimer;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _monster = GetComponent<MonsterBase>();
        }

        private void Start()
        {
            _chargeTimer = 3f; // 开场 3 秒后第一次冲锋
        }

        private void Update()
        {
            if (_monster == null || !_monster.IsAlive) return;
            if (_playerTarget == null) FindPlayer();
            if (_playerTarget == null) return;

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
        //  阶段切换检测
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
                _whirlwindCooldownTimer = 0f; // 立即可用

                Debug.Log("[Boss-堕落勇士] ⚠️ 进入第二阶段！HP ≤ 50%");
            }
        }

        // =====================================================================
        //  阶段一：普攻 + 勇者冲锋
        // =====================================================================

        private void UpdatePhase1()
        {
            float dt = Time.deltaTime;

            if (_isCharging)
            {
                ExecuteCharge(dt);
                return;
            }

            // 追击玩家
            PursuePlayer(1.0f);

            // 普攻
            _attackTimer -= dt;
            float distance = Vector3.Distance(transform.position, _playerTarget.position);
            if (distance <= 1.5f && _attackTimer <= 0f)
            {
                PerformNormalAttack();
                _attackTimer = _monster.CurrentStats.Get(StatType.AttackSpeed) > 0f
                    ? 1f / _monster.CurrentStats.Get(StatType.AttackSpeed)
                    : 1.5f;
            }

            // 冲锋冷却
            _chargeTimer -= dt;
            if (_chargeTimer <= 0f && distance > 2f)
            {
                StartCharge();
            }
        }

        /// <summary>
        /// 启动勇者冲锋
        /// 锁定玩家当前位置并发起直线冲撞
        /// </summary>
        private void StartCharge()
        {
            _isCharging = true;
            _chargeElapsed = 0f;
            _chargeHit = false;
            _chargeDirection = (_playerTarget.position - transform.position).normalized;

            Debug.Log("[Boss-堕落勇士] 🗡️ 勇者冲锋！");
        }

        private void ExecuteCharge(float dt)
        {
            _chargeElapsed += dt;

            // 高速直线位移
            transform.position += (Vector3)(_chargeDirection * _chargeSpeed * dt);

            // 碰撞检测（简化：检测与玩家的距离）
            if (!_chargeHit && _playerTarget != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTarget.position);
                if (dist <= 1.0f)
                {
                    _chargeHit = true;
                    OnChargeHitPlayer();
                }
            }

            // 冲锋结束
            if (_chargeElapsed >= _chargeDuration)
            {
                _isCharging = false;
                _chargeTimer = _chargeCooldown;
            }
        }

        /// <summary>
        /// 冲锋命中玩家：巨额伤害 + 击退 + 1秒眩晕
        /// </summary>
        private void OnChargeHitPlayer()
        {
            var playerEntity = _playerTarget.GetComponent<EntityBase>();
            if (playerEntity == null || !playerEntity.IsAlive) return;

            float atk = _monster.CurrentStats.Get(StatType.ATK);
            var damageResult = DamageCalculator.Calculate(
                _monster.CurrentStats,
                playerEntity.CurrentStats,
                baseDamage: 0f,
                atkScaling: _chargeDamageMultiplier,
                damageType: DamageType.Physical
            );

            if (!DamageCalculator.CheckDodge(playerEntity.CurrentStats))
            {
                playerEntity.TakeDamage(damageResult, _monster.EntityID);

                // 施加眩晕
                var statusMgr = _playerTarget.GetComponent<StatusEffectManager>();
                if (statusMgr != null && !statusMgr.IsUnstoppable())
                {
                    statusMgr.ApplyEffect(StatusEffectType.Stun, _chargeStunDuration, 0f, _monster.EntityID);
                }

                // 击退效果
                Vector3 knockback = _chargeDirection * 3f;
                _playerTarget.position += knockback;

                Debug.Log($"[Boss-堕落勇士] 勇者冲锋命中！伤害={damageResult.FinalDamage:F0} +击退 +眩晕{_chargeStunDuration}s");
            }
        }

        // =====================================================================
        //  阶段二：处决大风车
        // =====================================================================

        private void UpdatePhase2()
        {
            float dt = Time.deltaTime;

            if (_whirlwindActive)
            {
                ExecuteWhirlwind(dt);
                return;
            }

            // 非大风车期间：和阶段一一样普攻+追击+冲锋
            UpdatePhase1();

            // 大风车冷却
            _whirlwindCooldownTimer -= dt;
            if (_whirlwindCooldownTimer <= 0f)
            {
                StartWhirlwind();
            }
        }

        /// <summary>
        /// 启动处决大风车
        /// 原地旋转重剑变钢铁风暴，免疫所有控制（霸体）
        /// </summary>
        private void StartWhirlwind()
        {
            _whirlwindActive = true;
            _whirlwindTimer = 0f;
            _whirlwindTickTimer = 0f;

            // 激活霸体
            var statusMgr = GetComponent<StatusEffectManager>();
            if (statusMgr != null)
            {
                statusMgr.ClearAllDebuffs();
                statusMgr.ApplyEffect(StatusEffectType.Unstoppable, _whirlwindDuration, 0f, _monster.EntityID);
            }

            Debug.Log("[Boss-堕落勇士] 🌪️ 处决大风车！霸体激活！");
        }

        private void ExecuteWhirlwind(float dt)
        {
            _whirlwindTimer += dt;
            _whirlwindTickTimer += dt;

            // 缓慢向玩家移动
            if (_playerTarget != null)
            {
                Vector3 dir = (_playerTarget.position - transform.position).normalized;
                transform.position += dir * (_whirlwindMoveSpeed * dt);
            }

            // 每 tick 对范围内敌人造成伤害
            if (_whirlwindTickTimer >= _whirlwindTickInterval)
            {
                _whirlwindTickTimer = 0f;
                WhirlwindTick();
            }

            // 大风车结束
            if (_whirlwindTimer >= _whirlwindDuration)
            {
                _whirlwindActive = false;
                _whirlwindCooldownTimer = _whirlwindCooldown;
                Debug.Log("[Boss-堕落勇士] 大风车结束。");
            }
        }

        /// <summary>
        /// 大风车每跳 AOE 伤害
        /// </summary>
        private void WhirlwindTick()
        {
            if (_playerTarget == null) return;

            float distance = Vector3.Distance(transform.position, _playerTarget.position);
            float aoeRadius = 2.5f;

            if (distance <= aoeRadius)
            {
                var playerEntity = _playerTarget.GetComponent<EntityBase>();
                if (playerEntity != null && playerEntity.IsAlive)
                {
                    var damageResult = DamageCalculator.Calculate(
                        _monster.CurrentStats,
                        playerEntity.CurrentStats,
                        baseDamage: 0f,
                        atkScaling: _whirlwindDamageMultiplier,
                        damageType: DamageType.Physical
                    );

                    playerEntity.TakeDamage(damageResult, _monster.EntityID);
                }
            }
        }

        // =====================================================================
        //  通用方法
        // =====================================================================

        private void PursuePlayer(float speedMultiplier)
        {
            if (_playerTarget == null) return;
            float moveSpeed = _monster.CurrentStats.Get(StatType.MoveSpeed) * 2.5f * speedMultiplier;
            Vector3 dir = (_playerTarget.position - transform.position).normalized;
            transform.position += dir * (moveSpeed * Time.deltaTime);
        }

        private void PerformNormalAttack()
        {
            if (_playerTarget == null) return;
            var playerEntity = _playerTarget.GetComponent<EntityBase>();
            if (playerEntity == null || !playerEntity.IsAlive) return;

            var damageResult = DamageCalculator.Calculate(
                _monster.CurrentStats,
                playerEntity.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Physical
            );

            if (!DamageCalculator.CheckDodge(playerEntity.CurrentStats))
            {
                playerEntity.TakeDamage(damageResult, _monster.EntityID);
            }
        }

        private void FindPlayer()
        {
            // 通过标签搜索玩家（也可通过 ServiceLocator 注册的 HeroController 获取）
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTarget = playerObj.transform;
                _monster.EngageTarget(_playerTarget);
            }
        }
    }
}
