// ============================================================================
// 逃离魔塔 - 战争迷雾管理器 (FogOfWarManager)
// 管理地图可见性三态（Unseen/Explored/Visible），控制迷雾揭示。
//
// 架构：
//   数据层：VisibilityState[,] 与 FloorGrid 同尺寸
//   渲染层：由 FloorRenderer 的第三层 Tilemap 负责显示
//   触发源：玩家移动 → RevealAroundPlayer / 开门 → RevealRoom
//
// 来源：DesignDocs/06_Map_and_Modes.md §1.4
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 迷雾可见性三态
    /// </summary>
    public enum VisibilityState
    {
        Unseen,     // 未探索（纯黑）
        Explored,   // 已探索但不在视野（半透明灰）
        Visible,    // 当前可见（完全透明）
    }

    /// <summary>
    /// 战争迷雾管理器 —— 全局单例
    /// </summary>
    public class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }

        /// <summary>是否启用战争迷雾（可在设置中关闭）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>走廊/房间外视野半径（曼哈顿距离，格数）</summary>
        public int ViewRadius { get; set; } = 5;

        /// <summary>可见性数据</summary>
        public VisibilityState[,] VisibilityMap { get; private set; }

        /// <summary>地图宽度</summary>
        public int Width { get; private set; }

        /// <summary>地图高度</summary>
        public int Height { get; private set; }

        // === 渲染器引用 ===
        private FloorRenderer _renderer;
        private FloorGrid _grid;

        // === 上次玩家位置（避免原地重复计算） ===
        private Vector2Int _lastPlayerPos = new(-999, -999);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化迷雾系统（楼层生成后调用）
        /// </summary>
        /// <param name="grid">楼层数据</param>
        /// <param name="renderer">楼层渲染器</param>
        public void Initialize(FloorGrid grid, FloorRenderer renderer)
        {
            _grid = grid;
            _renderer = renderer;
            Width = grid.Width;
            Height = grid.Height;
            _lastPlayerPos = new Vector2Int(-999, -999);

            // 全部初始化为 Unseen
            VisibilityMap = new VisibilityState[Width, Height];

            if (!Enabled)
            {
                // 迷雾关闭 → 全部设为 Visible
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        VisibilityMap[x, y] = VisibilityState.Visible;
            }

            // 渲染初始迷雾
            _renderer.RenderFog(VisibilityMap, Width, Height);

            Debug.Log($"[FogOfWar] 初始化完成 {Width}×{Height}，" +
                $"迷雾{(Enabled ? "开启" : "关闭")}");
        }

        // =====================================================================
        //  揭示逻辑
        // =====================================================================

        /// <summary>
        /// 玩家移动到新位置时调用：以玩家为中心的曼哈顿圆形揭示
        /// </summary>
        public void OnPlayerMoved(Vector2Int playerPos)
        {
            if (!Enabled) return;
            if (playerPos == _lastPlayerPos) return; // 原地不重复计算
            _lastPlayerPos = playerPos;

            // 第一步：将之前 Visible 的格子降级为 Explored
            DimPreviousVisible();

            // 第二步：以玩家为中心揭示曼哈顿圆形区域
            RevealAroundPlayer(playerPos, ViewRadius);
        }

        /// <summary>
        /// 将所有 Visible 格子降级为 Explored（离开视野后变暗）
        /// </summary>
        private void DimPreviousVisible()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (VisibilityMap[x, y] == VisibilityState.Visible)
                    {
                        VisibilityMap[x, y] = VisibilityState.Explored;
                        _renderer.UpdateFogTile(x, y, VisibilityState.Explored);
                    }
                }
            }
        }

        /// <summary>
        /// 曼哈顿圆形揭示：以 center 为中心，radius 为半径
        /// </summary>
        private void RevealAroundPlayer(Vector2Int center, int radius)
        {
            // 判断玩家当前所在房间（0=走廊）
            int playerRoomId = 0;
            if (_grid != null && _grid.InBounds(center.x, center.y))
                playerRoomId = _grid.RoomMap[center.x, center.y];

            for (int dx = -radius; dx <= radius; dx++)
            {
                int maxDy = radius - Mathf.Abs(dx);
                for (int dy = -maxDy; dy <= maxDy; dy++)
                {
                    int x = center.x + dx;
                    int y = center.y + dy;
                    if (x < 0 || x >= Width || y < 0 || y >= Height) continue;

                    // 检查目标格子所属房间
                    int targetRoomId = _grid.RoomMap[x, y];

                    if (targetRoomId > 0 && targetRoomId != playerRoomId)
                    {
                        // 目标在"玩家未进入的房间"内 → 不揭示
                        // 设计文档：有门房间开门前内部完全不可见
                        continue;
                    }

                    SetVisible(x, y);
                }
            }

            // 如果玩家在房间内，揭示整个房间（含墙壁环）
            if (playerRoomId > 0)
            {
                RevealRoomByID(playerRoomId);
            }
        }

        /// <summary>
        /// 开门时调用：揭示整个房间（含墙壁环）
        /// </summary>
        public void RevealRoom(Vector2Int doorPos)
        {
            if (!Enabled || _grid == null) return;

            // 查找门连通的房间：检查门四方向相邻格子的 RoomMap
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int d = 0; d < 4; d++)
            {
                int nx = doorPos.x + dx[d];
                int ny = doorPos.y + dy[d];
                if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;

                int roomId = _grid.RoomMap[nx, ny];
                if (roomId > 0)
                {
                    RevealRoomByID(roomId);
                }
            }
        }

        /// <summary>按房间 ID 揭示整个房间区域（内部 + 墙壁环）</summary>
        private void RevealRoomByID(int roomId)
        {
            // 找到房间
            RoomData room = null;
            foreach (var r in _grid.Rooms)
            {
                if (r.RoomID == roomId) { room = r; break; }
            }
            if (room == null) return;

            // 揭示房间内部 + 墙壁环（外扩 1 格）
            var b = room.Bounds;
            for (int x = b.x - 1; x <= b.xMax; x++)
            {
                for (int y = b.y - 1; y <= b.yMax; y++)
                {
                    if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                    SetVisible(x, y);
                }
            }
        }

        /// <summary>设置单个格子为 Visible</summary>
        private void SetVisible(int x, int y)
        {
            if (VisibilityMap[x, y] == VisibilityState.Visible) return;
            VisibilityMap[x, y] = VisibilityState.Visible;
            _renderer.UpdateFogTile(x, y, VisibilityState.Visible);
        }

        /// <summary>查询指定格子是否可见</summary>
        public bool IsVisible(int x, int y)
        {
            if (!Enabled) return true;
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            return VisibilityMap[x, y] == VisibilityState.Visible;
        }

        /// <summary>查询指定格子是否已探索（含 Visible）</summary>
        public bool IsExplored(int x, int y)
        {
            if (!Enabled) return true;
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            return VisibilityMap[x, y] != VisibilityState.Unseen;
        }
    }
}
