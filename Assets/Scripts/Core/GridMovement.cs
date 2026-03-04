// ============================================================================
// 逃离魔塔 - 格子移动组件 (GridMovement)
// 通用的格子步进移动系统。可挂载到英雄和怪物上。
//
// 核心行为：
//   - 实体位置始终对齐到 1x1 格子中心
//   - 移动使用 MoveTowards 匀速移动（无惯性、无缓动尾巴）
//   - 按住方向键可丝滑连续移动（到达一格后立刻开始下一格）
//   - 松手后到达当前目标格即停（急停）
//   - 碰撞回弹：尝试移入被占据的格子 → 半格位移后弹回
//
// 优化（参考 Magic_Tower_Wars 项目）：
//   - 输入缓冲：150ms窗口，移动过程中按键不丢失
//   - 静态注册表：碰撞查询 O(n) → 避免 FindObjectsByType 的反射开销
//   - 击杀延迟保护：击杀后 150ms 冻结移动，防止误走
//
// 来源：DesignDocs/03_Combat_System.md §1.1.1
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 格子移动组件 —— 管理实体在离散网格上的平滑步进移动
    /// </summary>
    public class GridMovement : MonoBehaviour
    {
        [Header("=== 移动参数 ===")]
        [Tooltip("移动速度（格/秒），值越大移动越快")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("碰撞回弹动画速度（倍率）")]
        [SerializeField] private float bumpSpeed = 8f;

        /// <summary>当前所在格子坐标（逻辑位置，在开始移动时即更新）</summary>
        public Vector2Int GridPosition { get; private set; }

        /// <summary>是否正在移动中（包括回弹动画）</summary>
        public bool IsMoving => _state != MoveState.Idle;

        /// <summary>是否正在执行碰撞回弹</summary>
        public bool IsBumping => _state == MoveState.BumpForward || _state == MoveState.BumpReturn;

        /// <summary>
        /// 是否允许碰撞回弹。为 false 时，被占据的格子视为墙壁（不回弹不触发事件）
        /// 用于攻击冷却期间禁止回弹但允许自由移动
        /// </summary>
        public bool AllowBump { get; set; } = true;

        /// <summary>
        /// 碰撞回弹过滤器。返回 true = 触发回弹+事件，false = 视为墙壁（不回弹）。
        /// 为 null 时所有占据者均触发回弹。
        /// 用于怪物过滤同阵营实体（避免怪物之间互碰抽搐）。
        /// </summary>
        public System.Func<GameObject, bool> BumpFilter { get; set; }

        // === 事件回调 ===
        /// <summary>
        /// 移动被阻挡时触发（参数 = 目标格坐标, 阻挡该格的 GameObject）
        /// </summary>
        public event Action<Vector2Int, GameObject> OnMoveBlocked;

        /// <summary>
        /// 移动被墙壁阻挡时触发（参数 = 目标格坐标）
        /// 用于门交互检测：门注册为墙壁，不会触发 OnMoveBlocked，
        /// 但 HeroController 需要知道玩家试图走向了哪个墙壁格
        /// </summary>
        public event Action<Vector2Int> OnWallBlocked;

        /// <summary>
        /// 成功到达新格子时触发
        /// </summary>
        public event Action<Vector2Int> OnArrivedAtTile;

        // === 移动状态机 ===
        private enum MoveState
        {
            Idle,           // 静止在格子上
            Moving,         // 正在从 A 格滑向 B 格
            BumpForward,    // 碰撞回弹：向前半格
            BumpReturn,     // 碰撞回弹：弹回原格
        }

        private MoveState _state = MoveState.Idle;
        private Vector3 _moveOrigin;       // 移动/回弹起点（世界坐标）
        private Vector3 _moveTarget;       // 移动终点（世界坐标）
        private Vector3 _bumpPeak;         // 回弹最远点

        // === 当前帧输入方向（每帧由外部设置，帧末自动清除） ===
        private Vector2Int _currentFrameDirection;
        private bool _hasInputThisFrame;

        // === 当前正在执行的移动方向（用于判断缓冲区是否需要写入） ===
        private Vector2Int _activeMoveDirection;

        // === 输入缓冲（参考 Magic Tower Wars：150ms 缓冲窗口） ===
        // 移动过程中收到的方向输入暂存于此，到达目标格后立即消费
        private Vector2Int _bufferedDirection;
        private float _bufferTimer;
        private const float INPUT_BUFFER_WINDOW = 0.15f;

        // === 击杀后延迟保护 ===
        // 击杀怪物后短暂冻结移动输入，防止战斗节奏中误走入危险格
        private float _postKillDelay;
        private const float POST_KILL_DELAY_DURATION = 0.15f;

        // =====================================================================
        //  静态实例注册表
        //  替代每次 FindObjectsByType 的全量遍历 → 直接查 HashSet
        // =====================================================================
        private static readonly HashSet<GridMovement> _allInstances = new();

        /// <summary>
        /// 场景加载前清空静态注册表，防止 Domain Reload 关闭时残留已销毁对象
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _allInstances.Clear();
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>设置移动速度</summary>
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0.1f, speed);
        }

        /// <summary>
        /// 设置碰撞回弹动画速度
        /// bumpSpeed 越大回弹越快。总时长 ≈ 2/bumpSpeed 秒
        /// </summary>
        public void SetBumpSpeed(float speed)
        {
            bumpSpeed = Mathf.Max(0.5f, speed);
        }

        /// <summary>将实体对齐到最近的格子中心</summary>
        public void SnapToGrid()
        {
            GridPosition = WorldToGrid(transform.position);
            transform.position = GridToWorld(GridPosition);
        }

        /// <summary>强制设置格子位置</summary>
        public void SetGridPosition(Vector2Int pos)
        {
            GridPosition = pos;
            transform.position = GridToWorld(pos);
            _state = MoveState.Idle;
        }

        /// <summary>
        /// 设置本帧的移动方向。每帧持续调用 = 持续移动。
        /// 不调用 = 到达当前目标格后停止。
        /// </summary>
        public void SetDirection(Vector2Int direction)
        {
            direction = ClampToCardinal(direction);
            if (direction == Vector2Int.zero) return;
            _currentFrameDirection = direction;
            _hasInputThisFrame = true;

            // 仅在方向发生变更时写入缓冲区（同方向不缓冲，防止单次点按多走一格）
            if (_state != MoveState.Idle && direction != _activeMoveDirection)
            {
                _bufferedDirection = direction;
                _bufferTimer = INPUT_BUFFER_WINDOW;
            }
        }

        /// <summary>
        /// [兼容旧接口] 请求移动一格
        /// </summary>
        public void RequestMove(Vector2Int direction)
        {
            SetDirection(direction);
        }

        /// <summary>
        /// [兼容旧接口] 清除缓存方向
        /// </summary>
        public void ClearPendingDirection()
        {
            // 不需要操作——新机制下，不调用 SetDirection 就等于"不想移动"
        }

        /// <summary>
        /// 激活击杀后延迟保护（由外部战斗逻辑调用）
        /// </summary>
        public void SetPostKillDelay()
        {
            _postKillDelay = POST_KILL_DELAY_DURATION;
        }

        // =====================================================================
        //  内部逻辑
        // =====================================================================

        private void Start()
        {
            SnapToGrid();
        }

        private void OnEnable()
        {
            _allInstances.Add(this);
        }

        private void OnDisable()
        {
            _allInstances.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 输入缓冲计时器递减
            if (_bufferTimer > 0f)
            {
                _bufferTimer -= dt;
                if (_bufferTimer <= 0f)
                {
                    _bufferedDirection = Vector2Int.zero;
                }
            }

            // 击杀延迟保护
            if (_postKillDelay > 0f)
            {
                _postKillDelay -= dt;
                // 延迟期间不响应移动，但回弹动画仍需完成
                if (_state == MoveState.BumpForward)
                {
                    UpdateBumpForward(dt);
                }
                else if (_state == MoveState.BumpReturn)
                {
                    UpdateBumpReturn(dt);
                }
                return;
            }

            switch (_state)
            {
                case MoveState.Idle:
                    // Idle 状态下如果有输入，立即开始移动
                    if (_hasInputThisFrame)
                    {
                        TryStartMove(_currentFrameDirection);
                    }
                    break;

                case MoveState.Moving:
                    UpdateMoving(dt);
                    break;

                case MoveState.BumpForward:
                    UpdateBumpForward(dt);
                    break;

                case MoveState.BumpReturn:
                    UpdateBumpReturn(dt);
                    break;
            }
        }

        private void LateUpdate()
        {
            // 帧末清除输入标记
            // 下一帧如果 HeroController 不再调用 SetDirection，就视为松手
            _hasInputThisFrame = false;
        }

        /// <summary>尝试开始向目标格移动</summary>
        private void TryStartMove(Vector2Int direction)
        {
            Vector2Int targetGrid = GridPosition + direction;

            GameObject blocker = CheckTileOccupant(targetGrid);

            if (blocker != null)
            {
                // BumpFilter 判定：是否对该占据者触发回弹
                bool shouldBump = AllowBump && (BumpFilter == null || BumpFilter(blocker));
                if (shouldBump)
                {
                    StartBump(direction, targetGrid, blocker);
                }
                // 不回弹时视为墙壁，不移动不触发事件
            }
            else if (IsTileWall(targetGrid))
            {
                // 墙壁 → 不移动，但通知订阅者（用于门交互）
                OnWallBlocked?.Invoke(targetGrid);
            }
            else
            {
                StartMove(targetGrid);
            }
        }

        /// <summary>开始正常移动</summary>
        private void StartMove(Vector2Int targetGrid)
        {
            _activeMoveDirection = targetGrid - GridPosition; // 记录本次移动方向
            _state = MoveState.Moving;
            _moveOrigin = GridToWorld(GridPosition);
            _moveTarget = GridToWorld(targetGrid);
            GridPosition = targetGrid; // 逻辑先行：立刻占据目标格
        }

        /// <summary>匀速移动更新（MoveTowards，无缓动尾巴）</summary>
        private void UpdateMoving(float dt)
        {
            float step = moveSpeed * dt; // 每帧移动的世界距离
            transform.position = Vector3.MoveTowards(transform.position, _moveTarget, step);

            if (Vector3.Distance(transform.position, _moveTarget) < 0.001f)
            {
                // 到达目标格
                transform.position = _moveTarget;
                _state = MoveState.Idle;
                OnArrivedAtTile?.Invoke(GridPosition);

                // 优先消费输入缓冲（移动中按下的键）
                if (_bufferTimer > 0f && _bufferedDirection != Vector2Int.zero)
                {
                    Vector2Int consumed = _bufferedDirection;
                    _bufferedDirection = Vector2Int.zero;
                    _bufferTimer = 0f;
                    TryStartMove(consumed);
                }
                // 其次使用本帧实时输入（持续按住方向键的场景）
                else if (_hasInputThisFrame)
                {
                    TryStartMove(_currentFrameDirection);
                }
                // 无输入 → 停在这里（急停）
            }
        }

        /// <summary>开始碰撞回弹动画</summary>
        private void StartBump(Vector2Int direction, Vector2Int targetGrid, GameObject blocker)
        {
            _state = MoveState.BumpForward;
            _moveOrigin = GridToWorld(GridPosition);
            Vector3 targetWorld = GridToWorld(targetGrid);
            _bumpPeak = Vector3.Lerp(_moveOrigin, targetWorld, 0.4f);

            // 触发碰撞事件（战斗结算在这里触发）
            OnMoveBlocked?.Invoke(targetGrid, blocker);
        }

        /// <summary>回弹前半段（匀速冲向碰撞点）</summary>
        private void UpdateBumpForward(float dt)
        {
            float step = bumpSpeed * dt;
            transform.position = Vector3.MoveTowards(transform.position, _bumpPeak, step);

            if (Vector3.Distance(transform.position, _bumpPeak) < 0.001f)
            {
                transform.position = _bumpPeak;
                _state = MoveState.BumpReturn;
            }
        }

        /// <summary>回弹后半段（匀速弹回原点）</summary>
        private void UpdateBumpReturn(float dt)
        {
            float step = bumpSpeed * dt;
            transform.position = Vector3.MoveTowards(transform.position, _moveOrigin, step);

            if (Vector3.Distance(transform.position, _moveOrigin) < 0.001f)
            {
                transform.position = _moveOrigin;
                _state = MoveState.Idle;

                // 回弹结束后：优先消费缓冲区，其次用实时输入
                if (_bufferTimer > 0f && _bufferedDirection != Vector2Int.zero)
                {
                    Vector2Int consumed = _bufferedDirection;
                    _bufferedDirection = Vector2Int.zero;
                    _bufferTimer = 0f;
                    TryStartMove(consumed);
                }
                else if (_hasInputThisFrame)
                {
                    TryStartMove(_currentFrameDirection);
                }
            }
        }

        // =====================================================================
        //  格子工具方法
        // =====================================================================

        /// <summary>世界坐标 → 格子坐标</summary>
        public static Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
        }

        /// <summary>格子坐标 → 世界坐标（格子中心）</summary>
        public static Vector3 GridToWorld(Vector2Int gridPos)
        {
            return new Vector3(gridPos.x, gridPos.y, 0f);
        }

        /// <summary>
        /// 检测目标格子上是否有实体占据
        /// 使用静态注册表替代 FindObjectsByType，避免反射开销
        /// </summary>
        private GameObject CheckTileOccupant(Vector2Int tilePos)
        {
            foreach (var mover in _allInstances)
            {
                if (mover == this) continue;
                if (mover == null) continue; // 安全检查：对象可能已被销毁
                if (mover.GridPosition == tilePos)
                {
                    return mover.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// 检测目标格是否可通行（公共接口，供 AI 寻路前瞻使用）。
        /// 可通行 = 非墙壁 且 无不可回弹的占据者。
        /// </summary>
        public bool IsTilePassable(Vector2Int tilePos)
        {
            if (IsTileWall(tilePos)) return false;

            var occupant = CheckTileOccupant(tilePos);
            if (occupant == null) return true;

            // 如果 BumpFilter 存在且拒绝该占据者 → 视为不可通行（如同阵营怪物）
            if (BumpFilter != null && !BumpFilter(occupant))
                return false;

            // 占据者可回弹（如敌方实体）→ 视为可通行（AI 可以朝它移动并攻击）
            return true;
        }

        /// <summary>
        /// 检测目标格是否为墙壁/障碍物
        /// 通过 TilemapCollisionProvider 单例查询（支持 Tilemap 和手动注册）
        /// </summary>
        private bool IsTileWall(Vector2Int tilePos)
        {
            var provider = EscapeTheTower.Map.TilemapCollisionProvider.Instance;
            return provider != null && provider.IsWall(tilePos);
        }

        /// <summary>将输入方向限制为四方向</summary>
        private Vector2Int ClampToCardinal(Vector2Int input)
        {
            if (input == Vector2Int.zero) return Vector2Int.zero;

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                return new Vector2Int(input.x > 0 ? 1 : -1, 0);
            }
            else
            {
                return new Vector2Int(0, input.y > 0 ? 1 : -1);
            }
        }
    }
}

