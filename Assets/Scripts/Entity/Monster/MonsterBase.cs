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
        Patrolling, // 被动巡逻（随机小范围移动，不主动攻击）
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

        /// <summary>当前 AI 状态（默认被动巡逻）</summary>
        public MonsterAIState AIState { get; private set; } = MonsterAIState.Patrolling;

        /// <summary>怪物种族标签</summary>
        public MonsterTag Tags => monsterData != null ? monsterData.tags : MonsterTag.None;

        /// <summary>是否为精英</summary>
        public bool IsElite { get; private set; }

        /// <summary>精英属性倍率</summary>
        public float EliteMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 外部 AI 覆盖的移动方向（如 Boss 冲锋锁定方向）。
        /// 设置后 PursueTarget 使用此方向替代默认追踪计算。
        /// 每帧消费后自动清除（设为 null = 使用默认追踪）。
        /// </summary>
        public Vector2Int? OverridePursuitDirection { get; set; }

        /// <summary>
        /// 是否由外部 AI 接管攻击判定（如 Boss AI 自行管理攻击逻辑）。
        /// 为 true 时跳过默认的 OnGridMoveBlocked 攻击流程。
        /// 注意：追击移动始终由 MonsterBase.PursueTarget 执行以保证执行时序。
        /// </summary>
        public bool ExternalAttackControl { get; set; }

        /// <summary>
        /// 所属房间 ID（0 = 走廊怪，不参与房间清除追踪）。
        /// 由 RoomTracker 在怪物生成时设置。
        /// </summary>
        public int AssignedRoomID { get; set; }

        /// <summary>
        /// 巡逻原点（出生位置），巡逻不会超出此点 + PatrolRadius 范围
        /// </summary>
        public Vector2Int PatrolOrigin { get; set; }

        /// <summary>巡逻半径（曼哈顿距离，默认 3 格）</summary>
        public int PatrolRadius { get; set; } = 3;

        // === 追击目标 ===
        private Transform _pursuitTarget;
        private float _attackCooldownTimer;
        private float _attackInterval = 1.5f; // 默认攻击间隔（秒），Initialize 中会覆盖

        // === 格子移动（使用基类 EntityBase._gridMovement） ===
        private float _pursuitMoveTimer; // 怪物移动节拍计时器

        // === 战斗疲劳 ===
        private int _fatigueStacks;
        private float _fatigueTimer;
        private const float FATIGUE_TICK_INTERVAL = 10f; // 每10秒叠加一层疲劳

        // === 硬控递减（Boss 专用）===
        private int _ccApplicationCount;
        private float _ccResistResetTimer;

        // === 被动巡逻 ===
        private float _patrolTimer;         // 距离下次巡逻移动的倒计时
        private float _patrolInterval;      // 巡逻移动间隔（秒）
        private static readonly Vector2Int[] _patrolDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // =====================================================================
        //  生命周期
        // =====================================================================

        // === 疲劳系统引用 ===
        private FatigueSystem _fatigueSystem;

        protected override void Awake()
        {
            base.Awake();
            if (monsterData != null)
            {
                entityData = monsterData;
            }
            Faction = Faction.Enemy;

            // 缓存疲劳系统引用
            _fatigueSystem = FindAnyObjectByType<FatigueSystem>();

            EnsureVisualAndGrid();
        }

        /// <summary>
        /// 确保怪物拥有可见的视觉表现和格子移动组件
        /// </summary>
        private void EnsureVisualAndGrid()
        {
            // 格子移动组件
            _gridMovement = GetComponent<GridMovement>();
            if (_gridMovement == null)
            {
                _gridMovement = gameObject.AddComponent<GridMovement>();
            }
            _gridMovement.OnMoveBlocked += OnGridMoveBlocked;

            // 怪物仅对非同阵营实体触发碰撞回弹（同阵营视为墙壁跳过，避免互碰抽搐）
            _gridMovement.BumpFilter = (blocker) =>
            {
                var entity = blocker.GetComponent<EntityBase>();
                return entity != null && entity.Faction != Faction;
            };

            // 视觉占位（所有怪物共享同一份程序化 Sprite，减少纹理内存）
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
            }
            if (sr.sprite == null)
            {
                sr.sprite = GetSharedPlaceholderSprite();
                sr.color = new Color(0.9f, 0.2f, 0.2f); // 红色占位方块
            }
        }

        // === 共享占位 Sprite（静态缓存，所有怪物实例复用） ===
        private static Sprite _sharedPlaceholderSprite;

        private static Sprite GetSharedPlaceholderSprite()
        {
            if (_sharedPlaceholderSprite != null) return _sharedPlaceholderSprite;

            Texture2D tex = new Texture2D(4, 4);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _sharedPlaceholderSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _sharedPlaceholderSprite;
        }

        protected override void Update()
        {
            base.Update(); // 驱动 EntityBase.DOT 定时器

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

            // 设置格子移动速度
            if (_gridMovement != null)
            {
                float speed = CurrentStats.Get(StatType.MoveSpeed) * 3f;
                _gridMovement.SetMoveSpeed(Mathf.Max(0.5f, speed));
                _gridMovement.SnapToGrid();
            }

            Debug.Log($"[MonsterBase] {monsterData.entityName} 初始化完成。" +
                      $" HP={CurrentStats.Get(StatType.MaxHP):F0}" +
                      $" ATK={CurrentStats.Get(StatType.ATK):F0}" +
                      (IsElite ? " 【精英】" : ""));

            // 设置状态免疫
            if (monsterData.immuneToEffects != null && StatusEffects != null)
            {
                StatusEffects.SetImmunities(monsterData.immuneToEffects);
            }
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
            // 注意：即使外部 AI 接管攻击，追击移动仍由 PursueTarget 执行
            // 以保证 SetDirection 在 GridMovement.Update 之前被调用

            switch (AIState)
            {
                case MonsterAIState.Idle:
                    // 闲置：不主动攻击，等待被玩家激怒
                    break;

                case MonsterAIState.Patrolling:
                    UpdatePatrol();
                    break;

                case MonsterAIState.Pursuing:
                    PursueTarget();
                    // 攻击逻辑由 OnGridMoveBlocked 回调处理，无需显式调用
                    break;

                case MonsterAIState.Attacking:
                    // 攻击动画/逻辑中（由协程管理，回到 Pursuing）
                    break;

                case MonsterAIState.Stunned:
                    // 被控制中，无法行动
                    break;
            }
        }

        // =====================================================================
        //  被动巡逻逻辑
        //  怪物在房间内随机小范围移动，不主动追击。
        //  被玩家击中后切换为 Pursuing。
        // =====================================================================

        private void UpdatePatrol()
        {
            if (_gridMovement == null || _gridMovement.IsMoving) return;

            _patrolTimer -= Time.deltaTime;
            if (_patrolTimer > 0f) return;

            // 重置巡逻计时器（1.5~3.0秒随机间隔）
            _patrolInterval = UnityEngine.Random.Range(1.5f, 3.0f);
            _patrolTimer = _patrolInterval;

            // 随机选择方向
            var dir = _patrolDirs[UnityEngine.Random.Range(0, _patrolDirs.Length)];

            // 边界检查：目标格不能超出巡逻半径
            var currentPos = _gridMovement.GridPosition;
            var targetPos = currentPos + dir;
            int dist = Mathf.Abs(targetPos.x - PatrolOrigin.x) + Mathf.Abs(targetPos.y - PatrolOrigin.y);
            if (dist > PatrolRadius) return; // 超出范围，放弃此次移动

            _gridMovement.SetDirection(dir);
        }

        /// <summary>
        /// 被玩家击中时调用（被动巡逻怪被激怒，切换为追击状态）
        /// </summary>
        public void Provoke(Transform aggressor)
        {
            if (AIState == MonsterAIState.Patrolling || AIState == MonsterAIState.Idle)
            {
                AIState = MonsterAIState.Pursuing;
                _pursuitTarget = aggressor;
                Debug.Log($"[怪物] {gameObject.name} 被激怒，开始追击！");
            }
        }

        /// <summary>
        /// 追击目标（格子步进）
        /// 每帧持续设置方向（等同于玩家按住方向键），由 GridMovement 负责平滑连走
        /// 攻击频率由 OnGridMoveBlocked 中的 _attackCooldownTimer 控制
        /// </summary>
        private void PursueTarget()
        {
            if (_pursuitTarget == null || _gridMovement == null) return;

            // 优先使用外部 AI 覆盖方向（如 Boss 冲锋锁定方向）
            if (OverridePursuitDirection.HasValue)
            {
                _gridMovement.SetDirection(OverridePursuitDirection.Value);
                return;
            }

            // 计算与目标的差值
            Vector2 diff = _pursuitTarget.position - transform.position;
            if (diff.sqrMagnitude < 0.01f) return;

            // 计算主方向（差值大的轴）和次方向（差值小的轴）
            Vector2Int primaryDir, secondaryDir;
            if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            {
                primaryDir = new Vector2Int(diff.x > 0 ? 1 : -1, 0);
                secondaryDir = Mathf.Abs(diff.y) > 0.01f
                    ? new Vector2Int(0, diff.y > 0 ? 1 : -1)
                    : Vector2Int.zero;
            }
            else
            {
                primaryDir = new Vector2Int(0, diff.y > 0 ? 1 : -1);
                secondaryDir = Mathf.Abs(diff.x) > 0.01f
                    ? new Vector2Int(diff.x > 0 ? 1 : -1, 0)
                    : Vector2Int.zero;
            }

            // 单步前瞻：主方向可通行则走主方向，否则尝试次方向绕路
            Vector2Int primaryTarget = _gridMovement.GridPosition + primaryDir;
            if (_gridMovement.IsTilePassable(primaryTarget))
            {
                _gridMovement.SetDirection(primaryDir);
            }
            else if (secondaryDir != Vector2Int.zero)
            {
                Vector2Int secondaryTarget = _gridMovement.GridPosition + secondaryDir;
                if (_gridMovement.IsTilePassable(secondaryTarget))
                {
                    _gridMovement.SetDirection(secondaryDir);
                }
                else
                {
                    // 两个方向都堵死 → 仍尝试主方向（等待前方让路）
                    _gridMovement.SetDirection(primaryDir);
                }
            }
            else
            {
                // 目标在同一轴线上且主方向被堵 → 随机选一个垂直方向尝试绕路
                Vector2Int perpDir = (primaryDir.x != 0)
                    ? new Vector2Int(0, Random.value > 0.5f ? 1 : -1)
                    : new Vector2Int(Random.value > 0.5f ? 1 : -1, 0);

                Vector2Int perpTarget = _gridMovement.GridPosition + perpDir;
                if (_gridMovement.IsTilePassable(perpTarget))
                {
                    _gridMovement.SetDirection(perpDir);
                }
                else
                {
                    // 垂直方向也不行，尝试反方向
                    Vector2Int oppPerp = -perpDir;
                    if (_gridMovement.IsTilePassable(_gridMovement.GridPosition + oppPerp))
                    {
                        _gridMovement.SetDirection(oppPerp);
                    }
                    else
                    {
                        _gridMovement.SetDirection(primaryDir); // 全堵死，硬等
                    }
                }
            }
        }


        /// <summary>
        /// 执行主动攻击
        /// 疲劳系统加成：攻击力乘区 + 穿甲追加
        /// </summary>
        private void PerformAttack()
        {
            if (_pursuitTarget == null) return;

            var targetEntity = _pursuitTarget.GetComponent<EntityBase>();
            if (targetEntity == null || !targetEntity.IsAlive) return;

            // 致盲检查：致盲状态下普攻必定 Miss
            // 来源：GameData_Blueprints/02_Status_Ailments.md §8
            if (StatusEffects != null && StatusEffects.HasEffect(StatusEffectType.Blind))
            {
                Debug.Log($"[战斗] {gameObject.name} 处于致盲状态，攻击 Miss！");
                return;
            }

            // 疲劳加成
            float fatigueMult = _fatigueSystem != null ? _fatigueSystem.GetAtkMultiplier() : 1f;
            float fatiguePen = _fatigueSystem != null ? _fatigueSystem.GetPenBonus() : 0f;

            // 临时叠加穿甲到攻击属性快照中（不修改 CurrentStats）
            var atkStats = fatiguePen > 0f ? new StatBlock(CurrentStats) : CurrentStats;
            if (fatiguePen > 0f) atkStats.Add(StatType.ArmorPen, fatiguePen);

            var damageResult = DamageCalculator.Calculate(
                atkStats,
                targetEntity.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,      // 100% ATK
                damageType: DamageType.Physical,
                bonusMultiplier: fatigueMult
            );

            // 闪避检测
            if (!DamageCalculator.CheckDodge(targetEntity.CurrentStats))
            {
                targetEntity.TakeDamage(damageResult, EntityID);
                ApplyOnHitEffects(targetEntity); // 攻击附带状态效果
                Debug.Log($"[战斗] {gameObject.name} 主动攻击！伤害={damageResult.FinalDamage:F1}" +
                          $" 疲劳倍率={fatigueMult:F2}");
            }
        }

        // =====================================================================
        //  格子碰撞攻击（怪物尝试移入玩家格子 → 碰撞回弹 + 主动攻击）
        //  来源：DesignDocs/03_Combat_System.md §1.1.1
        // =====================================================================

        /// <summary>
        /// 格子移动被阻挡时触发
        /// 如果阻挡者是玩家 → 执行主动攻击（玩家不反击）
        /// </summary>
        private void OnGridMoveBlocked(Vector2Int blockedTile, GameObject blocker)
        {
            if (!IsAlive || blocker == null) return;

            // 外部 AI 接管攻击时（如 Boss AI），跳过默认攻击逻辑
            if (ExternalAttackControl) return;

            var playerEntity = blocker.GetComponent<EntityBase>();
            if (playerEntity == null || !playerEntity.IsAlive) return;
            if (playerEntity.Faction != Faction.Player) return;

            // 攻击冷却检查：尚未准备好则不攻击
            if (_attackCooldownTimer > 0f) return;

            // 怪物主动攻击玩家（不对称法则：玩家不反击）
            PerformAttack();
            _attackCooldownTimer = _attackInterval; // 重置攻击 CD
            _gridMovement.AllowBump = false;        // CD 期间禁止回弹动画（防抽搐）
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

            Debug.Log($"[战斗] {gameObject.name} 被玩家攻击，触发被动反击！");

            // 先进入追击状态（确保 _pursuitTarget 已设置）
            EngageTarget(player.transform);

            // 疲劳加成
            float fatigueMult = _fatigueSystem != null ? _fatigueSystem.GetAtkMultiplier() : 1f;
            float fatiguePen = _fatigueSystem != null ? _fatigueSystem.GetPenBonus() : 0f;

            var atkStats = fatiguePen > 0f ? new StatBlock(CurrentStats) : CurrentStats;
            if (fatiguePen > 0f) atkStats.Add(StatType.ArmorPen, fatiguePen);

            // 致盲检查：致盲状态下被动反击也 Miss
            if (StatusEffects != null && StatusEffects.HasEffect(StatusEffectType.Blind))
            {
                Debug.Log($"[战斗] {gameObject.name} 处于致盲状态，反击 Miss！");
                return;
            }

            // 被动反击：无视攻击间隔，立即打一次
            var counterDamage = DamageCalculator.Calculate(
                atkStats,
                player.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Physical,
                bonusMultiplier: fatigueMult
            );

            if (!DamageCalculator.CheckDodge(player.CurrentStats))
            {
                player.TakeDamage(counterDamage, EntityID);
                ApplyOnHitEffects(player); // 反击附带状态效果
                Debug.Log($"[战斗] {gameObject.name} 被动反击成功！" +
                          $"伤害={counterDamage.FinalDamage:F1}" +
                          (counterDamage.IsCritical ? " 【暴击！】" : ""));
            }
            else
            {
                Debug.Log("[战斗] 玩家闪避了反击！");
            }
        }

        /// <summary>
        /// 攻击命中后按 SO 配置概率施加状态效果
        /// 数据驱动：MonsterData_SO.onHitEffects
        /// </summary>
        private void ApplyOnHitEffects(EntityBase target)
        {
            if (monsterData == null || monsterData.onHitEffects == null) return;
            if (target.StatusEffects == null) return;

            foreach (var effect in monsterData.onHitEffects)
            {
                if (Random.value < effect.chance)
                {
                    target.StatusEffects.ApplyEffect(
                        effect.effectType,
                        effect.duration,
                        effect.valuePerStack,
                        EntityID,
                        effect.stacks > 0 ? effect.stacks : 1
                    );
                }
            }
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

                // CD 就绪：允许回弹（下次碰到玩家触发攻击+回弹动画）
                if (_attackCooldownTimer <= 0f && _gridMovement != null)
                {
                    _gridMovement.AllowBump = true;
                }
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

            string monsterName = monsterData != null ? monsterData.entityName : gameObject.name;
            Debug.Log($"[战斗] ☠️ {monsterName} 被击杀！");

            // 掉落经验值和金币
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

                int gold = Random.Range(monsterData.goldDropMin, monsterData.goldDropMax + 1);
                if (IsElite) gold = Mathf.RoundToInt(gold * 2f);

                EventManager.Publish(new OnGoldGainedEvent
                {
                    Meta = new EventMeta(EntityID),
                    Amount = gold,
                });

                Debug.Log($"[掉落] {monsterName} → EXP={finalExp}, 金币={gold}");

                // 消耗品 + 钥匙 + 装备掉落（来源：09_Consumable_and_Drop_Items.md §4）
                bool isBoss = Tags.HasFlag(MonsterTag.Boss);
                var dropTier = isBoss
                    ? Data.LootTableHelper.MonsterDropTier.Boss
                    : IsElite
                        ? Data.LootTableHelper.MonsterDropTier.Elite
                        : Data.LootTableHelper.MonsterDropTier.Normal;

                var gridMov = GetComponent<GridMovement>();
                var deathPos = gridMov != null ? gridMov.GridPosition
                    : GridMovement.WorldToGrid(transform.position);
                var dropRng = new System.Random(
                    deathPos.x * 1000 + deathPos.y + System.Environment.TickCount);
                Data.LootTableHelper.GenerateMonsterDrop(deathPos, dropTier, dropRng);
            }

            // 广播事件
            EventManager.Publish(new OnEntityDeathEvent
            {
                Meta = new EventMeta(EntityID),
                EntityID = EntityID,
            });
            EventManager.Publish(new OnEntityKillEvent
            {
                Meta = new EventMeta(killerID),
                KillerEntityID = killerID,
                VictimEntityID = EntityID,
            });

            // 直接销毁（不再依赖 CommandBuffer）
            Destroy(gameObject);
        }

        /// <summary>
        /// 钥匙概率掉落
        /// 来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md §4
        /// 普通怪 5%（仅铜）| 精英怪 20%（铜50%/银40%/金10%）| Boss 100%
        /// </summary>
        private void RollKeyDrop(string monsterName)
        {
            bool isBoss = Tags.HasFlag(MonsterTag.Boss);
            float dropChance;
            if (isBoss)
                dropChance = 1f; // Boss 必掉
            else if (IsElite)
                dropChance = 0.20f;
            else
                dropChance = 0.05f;

            if (Random.value > dropChance) return;

            // 决定钥匙类型
            DoorTier keyTier;
            if (isBoss)
            {
                // Boss 掉落层级对应钥匙（默认银钥匙作为高价值奖励）
                keyTier = DoorTier.Silver;
            }
            else if (IsElite)
            {
                // 精英：铜50% 银40% 金10%
                float roll = Random.value;
                if (roll < 0.50f)
                    keyTier = DoorTier.Bronze;
                else if (roll < 0.90f)
                    keyTier = DoorTier.Silver;
                else
                    keyTier = DoorTier.Gold;
            }
            else
            {
                // 普通怪：仅铜钥匙
                keyTier = DoorTier.Bronze;
            }

            EventManager.Publish(new OnKeyDroppedEvent
            {
                Meta = new EventMeta(EntityID),
                KeyTier = keyTier,
            });

            string keyName = keyTier switch
            {
                DoorTier.Bronze => "铜钥匙",
                DoorTier.Silver => "银钥匙",
                DoorTier.Gold => "金钥匙",
                _ => "钥匙",
            };
            Debug.Log($"[掉落] {monsterName} → {keyName}");
        }

        private void OnDestroy()
        {
            // 清理 GridMovement 事件订阅
            if (_gridMovement != null)
                _gridMovement.OnMoveBlocked -= OnGridMoveBlocked;
        }
    }
}
