// ============================================================================
// 逃离魔塔 - 迷你地图 UI (MinimapUI) v4
// 圆形常驻小地图，实时跟随玩家世界坐标滚动。
//
// 坐标系说明：
//   GridMovement: GridToWorld(gridPos) = (gridPos.x, gridPos.y, 0)
//   FloorRenderer: SetTile(new Vector3Int(x, y, 0))
//   RoomNode.GridPosition = roomData.Center (tile coord)
//   HeroController.transform.position = (tileX, tileY, 0)
//   → 全部统一在"瓦片坐标"空间，1 unit = 1 tile
//
// 特性：
//   - 圆形遮罩容器（程序化圆形 Sprite + Mask）
//   - 玩家居中（呼吸绿色圆点固定遮罩中心）
//   - 已探索房间 = 彩色方块，位置基于 RoomNode.GridPosition
//   - 出生点→当前房间最短路径（BFS，红色细线）
//
// 来源：DesignDocs/07_UI_and_UX.md §1.2
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Map;
using EscapeTheTower.Entity.Hero;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 迷你地图 UI —— 圆形、玩家实时居中、路径导航
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        // === 布局配置 ===
        private const float MAP_DIAMETER = 180f;       // 圆形容器直径（像素）
        private const float ROOM_BLOCK_SIZE = 16f;     // 房间方块尺寸（像素）
        private const float PATH_LINE_WIDTH = 2f;      // 路径线宽度
        private const float PLAYER_DOT_SIZE = 10f;     // 玩家圆点直径
        private const float BLINK_SPEED = 2.5f;        // 呼吸速度
        private const float MARGIN = 15f;              // 与屏幕右/上边距

        // === 缩放：1 tile = 多少像素 ===
        // 可视半径 = MAP_DIAMETER / 2 / PIXELS_PER_TILE = 90 / 5 = 18 tiles
        // 折中值：能看到相邻房间，又不会太挤
        private const float PIXELS_PER_TILE = 5f;

        // === UI 元素 ===
        private RectTransform _contentRoot;
        private readonly Dictionary<int, RectTransform> _roomRects = new();
        private readonly List<GameObject> _pathLineObjs = new();
        private Image _playerDot;
        private float _blinkTimer;

        // === 缓存引用 ===
        private MapManager _mapManager;
        private HeroController _hero;

        // === 脏标记（避免不必要的重建） ===
        private int _lastExploredCount;
        private int _lastCurrentRoomID = -1;
        private FloorData _lastFloorData;

        // === 已绘制路径的房间（避免重复绘制） ===
        private readonly HashSet<int> _pathDrawnForRooms = new();

        // === 房间类型颜色 ===
        private static readonly Dictionary<RoomType, Color> RoomColors = new()
        {
            { RoomType.Combat,   new Color(0.55f, 0.55f, 0.60f) },
            { RoomType.Boss,     new Color(0.90f, 0.20f, 0.20f) },
            { RoomType.Treasure, new Color(0.90f, 0.75f, 0.15f) },
            { RoomType.Stairs,   new Color(0.30f, 0.85f, 0.40f) },
            { RoomType.Merchant, new Color(0.30f, 0.70f, 0.90f) },
            { RoomType.Forge,    new Color(0.80f, 0.50f, 0.20f) },
            { RoomType.Campfire, new Color(0.90f, 0.60f, 0.30f) },
            { RoomType.Shrine,   new Color(0.70f, 0.50f, 0.90f) },
        };

        // =====================================================================
        //  初始化
        // =====================================================================

        public void Initialize(Transform canvasTransform)
        {
            _mapManager = FindAnyObjectByType<MapManager>();
            _hero = FindAnyObjectByType<HeroController>();
            BuildUI(canvasTransform);
            Debug.Log("[MinimapUI] 圆形迷你地图 v4 初始化完成");
        }

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildUI(Transform parent)
        {
            // ── 1. 圆形边框（底层装饰） ───────────────────
            var borderObj = CreateUIObject("Minimap_Border", parent);
            SetAnchorTopRight(borderObj, new Vector2(-MARGIN + 2, -48f),
                              new Vector2(MAP_DIAMETER + 4, MAP_DIAMETER + 4));
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = GetCircleSprite();
            borderImg.color = new Color(0.35f, 0.35f, 0.40f, 0.6f);

            // ── 2. 圆形遮罩容器 ──────────────────────────
            var maskObj = CreateUIObject("Minimap_Mask", parent);
            SetAnchorTopRight(maskObj, new Vector2(-MARGIN, -50f),
                              new Vector2(MAP_DIAMETER, MAP_DIAMETER));
            var maskImg = maskObj.AddComponent<Image>();
            maskImg.sprite = GetCircleSprite();
            maskImg.color = new Color(0.05f, 0.05f, 0.08f, 0.65f);
            var mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // 确保渲染顺序正确
            borderObj.transform.SetAsFirstSibling();
            maskObj.transform.SetAsLastSibling();

            // ── 3. 内容层（可平移） ───────────────────────
            var contentObj = CreateUIObject("Minimap_Content", maskObj.transform);
            _contentRoot = contentObj.GetComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.sizeDelta = new Vector2(4000, 4000); // 足够大的画布
            _contentRoot.anchoredPosition = Vector2.zero;

            // ── 4. 玩家圆点（固定在遮罩中心，不随内容移动） ─
            var playerObj = CreateUIObject("PlayerDot", maskObj.transform);
            var playerRect = playerObj.GetComponent<RectTransform>();
            playerRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerRect.pivot = new Vector2(0.5f, 0.5f);
            playerRect.anchoredPosition = Vector2.zero;
            playerRect.sizeDelta = new Vector2(PLAYER_DOT_SIZE, PLAYER_DOT_SIZE);
            _playerDot = playerObj.AddComponent<Image>();
            _playerDot.sprite = GetCircleSprite();
            _playerDot.color = new Color(0.2f, 0.9f, 0.3f);
            playerObj.transform.SetAsLastSibling();

            // ── 5. 标题文字 ──────────────────────────────
            var titleObj = CreateUIObject("Minimap_Title", parent);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(1, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(1, 1);
            titleRect.anchoredPosition = new Vector2(-MARGIN - MAP_DIAMETER / 2 + 20, -38f);
            titleRect.sizeDelta = new Vector2(60, 14);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "MAP";
            titleText.fontSize = 10;
            titleText.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleText.font == null)
                titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // =====================================================================
        //  每帧刷新
        // =====================================================================

        private void Update()
        {
            // 惰性获取引用
            if (_mapManager == null)
            {
                _mapManager = FindAnyObjectByType<MapManager>();
                if (_mapManager == null) return;
            }
            if (_hero == null)
            {
                _hero = FindAnyObjectByType<HeroController>();
                if (_hero == null) return;
            }

            // === 一次性诊断：检查坐标系对齐 ===
            if (!_diagnosticDone)
            {
                _diagnosticDone = true;
                RunCoordinateDiagnostic();
            }

            var floorData = _mapManager.CurrentFloorData;
            if (floorData == null || floorData.Rooms == null || floorData.Rooms.Count == 0) return;

            // --- 检测 FloorData 对象是否被替换（SyncWithGrid 会创建新实例） ---
            // 如果引用变了，说明地图底层数据全部更新，必须强制重建
            if (floorData != _lastFloorData)
            {
                _lastFloorData = floorData;
                _lastExploredCount = -1;
                _lastCurrentRoomID = -1;
                _diagnosticDone = false;

                // 换层时清除旧层路径线段和已绘制记录
                foreach (var obj in _pathLineObjs)
                    if (obj != null) Destroy(obj);
                _pathLineObjs.Clear();
                _pathDrawnForRooms.Clear();

                Debug.Log("[MinimapUI] 检测到 FloorData 更换，强制重建小地图");
            }

            int currentRoomID = _mapManager.CurrentRoomID;
            int exploredCount = CountExploredRooms(floorData);

            // --- 脏检测：探索数或当前房间变化 → 重建房间方块 + 路径 ---
            if (exploredCount != _lastExploredCount || currentRoomID != _lastCurrentRoomID)
            {
                _lastExploredCount = exploredCount;
                _lastCurrentRoomID = currentRoomID;
                RebuildContent(floorData, currentRoomID);
            }

            // --- 每帧：根据玩家世界坐标平移内容层 ---
            // 玩家世界坐标 = 瓦片坐标（GridToWorld 为恒等映射）
            Vector3 heroWorld = _hero.transform.position;
            Vector2 heroPixel = new Vector2(heroWorld.x * PIXELS_PER_TILE,
                                            heroWorld.y * PIXELS_PER_TILE);
            _contentRoot.anchoredPosition = -heroPixel;

            // --- 玩家呼吸动画 ---
            AnimatePlayerDot();
        }

        private bool _diagnosticDone;

        /// <summary>
        /// 运行时坐标诊断 —— 只执行一次，输出所有关键坐标便于排查偏移问题
        /// </summary>
        private void RunCoordinateDiagnostic()
        {
            Debug.Log("=== [MinimapUI] 坐标系诊断 ===");

            // 1. 检查 Grid/Tilemap 位置偏移
            var gridObj = GameObject.Find("FloorGrid");
            if (gridObj != null)
            {
                Debug.Log($"  Grid对象位置: {gridObj.transform.position}");
                var grid = gridObj.GetComponent<UnityEngine.Grid>();
                if (grid != null)
                    Debug.Log($"  Grid.cellSize: {grid.cellSize}");
            }
            else
            {
                Debug.LogWarning("  [!] 未找到FloorGrid对象！");
            }

            // 2. 玩家坐标
            Vector3 heroPos = _hero.transform.position;
            var heroGrid = _hero.GetComponent<EscapeTheTower.Core.GridMovement>();
            Debug.Log($"  玩家 transform.position: ({heroPos.x:F2}, {heroPos.y:F2}, {heroPos.z:F2})");
            if (heroGrid != null)
                Debug.Log($"  玩家 GridMovement.GridPosition: ({heroGrid.GridPosition.x}, {heroGrid.GridPosition.y})");

            // 3. 当前房间
            int curRoomID = _mapManager.CurrentRoomID;
            Debug.Log($"  MapManager.CurrentRoomID: {curRoomID}");

            // 4. 所有房间坐标
            var floorData = _mapManager.CurrentFloorData;
            if (floorData != null)
            {
                foreach (var room in floorData.Rooms)
                {
                    string explored = room.IsExplored ? "✓探索" : "✗未探";
                    Debug.Log($"  房间 {room.RoomID}({room.Type}) GridPos=({room.GridPosition.x},{room.GridPosition.y}) {explored} 连接=[{string.Join(",", room.ConnectedRoomIDs)}]");
                }
            }

            // 5. FloorGrid.SpawnPoint
            var currentGrid = _mapManager.CurrentGrid;
            if (currentGrid != null)
            {
                Debug.Log($"  FloorGrid.SpawnPoint: ({currentGrid.SpawnPoint.x}, {currentGrid.SpawnPoint.y})");
                // 检查玩家位置是否在出生房间内
                var spawnRoom = currentGrid.GetRoomAt(currentGrid.SpawnPoint.x, currentGrid.SpawnPoint.y);
                if (spawnRoom != null)
                    Debug.Log($"  出生房间: ID={spawnRoom.RoomID}, Bounds=({spawnRoom.Bounds.x},{spawnRoom.Bounds.y},{spawnRoom.Bounds.width},{spawnRoom.Bounds.height}), Center=({spawnRoom.Center.x},{spawnRoom.Center.y})");

                // 检查玩家当前所在格子对应的房间
                int px = Mathf.RoundToInt(heroPos.x), py = Mathf.RoundToInt(heroPos.y);
                int roomAtPlayer = currentGrid.InBounds(px, py) ? currentGrid.RoomMap[px, py] : -1;
                Debug.Log($"  玩家脚下 RoomMap[{px},{py}] = {roomAtPlayer}");
            }

            Debug.Log("=== [MinimapUI] 诊断结束 ===");
        }

        // =====================================================================
        //  内容重建
        // =====================================================================

        private void RebuildContent(FloorData floorData, int currentRoomID)
        {
            // 清旧房间方块（每次重建都清除）
            foreach (var kvp in _roomRects)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _roomRects.Clear();

            // 路径线段：只追加，不清除旧路径
            // - 进入非出生房间 → 从出生点画到当前位置的路径（叠加在旧路径上）
            // - 返回出生房间 → 不画新路径（保留所有已有路径）
            // - 换层时在 FloorData 变化检测中统一清除
            bool isSpawnRoom = (currentRoomID == floorData.StartRoomID);
            if (!isSpawnRoom && !_pathDrawnForRooms.Contains(currentRoomID))
            {
                _pathDrawnForRooms.Add(currentRoomID);
                DrawTileLevelPath();
            }

            // 调试日志
            if (_hero != null)
            {
                Vector3 heroPos = _hero.transform.position;
                Debug.Log($"[MinimapUI] 重建 | 玩家坐标=({heroPos.x:F1},{heroPos.y:F1}) | 房间ID={currentRoomID} | 路径保留={isSpawnRoom}");
            }

            // 画房间方块（在路径上层）
            foreach (var room in floorData.Rooms)
            {
                if (!room.IsExplored) continue;
                CreateRoomBlock(room);
            }
        }

        private void CreateRoomBlock(RoomNode room)
        {
            var obj = CreateUIObject($"Room_{room.RoomID}", _contentRoot);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // GridPosition 瓦片坐标 → 像素坐标
            rect.anchoredPosition = TileToPixel(room.GridPosition);

            float size = room.Type == RoomType.Boss ? ROOM_BLOCK_SIZE * 1.3f : ROOM_BLOCK_SIZE;
            rect.sizeDelta = new Vector2(size, size);

            var img = obj.AddComponent<Image>();
            img.color = GetRoomColor(room);

            _roomRects[room.RoomID] = rect;
        }

        // =====================================================================
        //  瓦片级 BFS 最短路径 + 绘制
        // =====================================================================

        /// <summary>
        /// 在 FloorGrid.Tiles 上执行 BFS，找到从出生点到玩家当前位置的
        /// 实际可行走最短路径，然后在小地图上绘制。
        /// </summary>
        private void DrawTileLevelPath()
        {
            var grid = _mapManager.CurrentGrid;
            if (grid == null) return;

            // 起点：出生点
            Vector2Int start = grid.SpawnPoint;

            // 终点：玩家当前瓦片位置
            Vector3 heroPos = _hero.transform.position;
            Vector2Int end = new Vector2Int(
                Mathf.RoundToInt(heroPos.x),
                Mathf.RoundToInt(heroPos.y));

            // 边界保护
            if (!grid.InBounds(start.x, start.y) || !grid.InBounds(end.x, end.y)) return;
            if (start == end) return;

            // BFS 在瓦片地图上寻路
            var tilePath = TileBFS(grid, start, end);
            if (tilePath == null || tilePath.Count < 2) return;

            // 简化路径：移除共线点，只保留拐弯处
            var simplified = SimplifyPath(tilePath);

            // 绘制路径线段
            Color pathColor = new Color(0.9f, 0.15f, 0.15f, 0.7f);
            for (int i = 0; i < simplified.Count - 1; i++)
            {
                CreateLine(TileToPixel(simplified[i]),
                           TileToPixel(simplified[i + 1]),
                           pathColor);
            }
        }

        /// <summary>
        /// 瓦片级 BFS：在 FloorGrid 上沿可通行格子寻找最短路径
        /// </summary>
        private static List<Vector2Int> TileBFS(FloorGrid grid, Vector2Int start, Vector2Int end)
        {
            int w = grid.Width, h = grid.Height;
            var visited = new bool[w, h];
            var parentX = new int[w, h];
            var parentY = new int[w, h];

            // 标记无父节点
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                { parentX[x, y] = -1; parentY[x, y] = -1; }

            visited[start.x, start.y] = true;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();

                if (cur == end)
                {
                    // 回溯路径
                    var path = new List<Vector2Int>();
                    Vector2Int trace = end;
                    while (trace != start)
                    {
                        path.Add(trace);
                        int px = parentX[trace.x, trace.y];
                        int py = parentY[trace.x, trace.y];
                        trace = new Vector2Int(px, py);
                    }
                    path.Add(start);
                    path.Reverse();
                    return path;
                }

                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d], ny = cur.y + dy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (visited[nx, ny]) continue;
                    if (grid.Tiles[nx, ny] == TileType.Wall) continue;

                    visited[nx, ny] = true;
                    parentX[nx, ny] = cur.x;
                    parentY[nx, ny] = cur.y;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return null; // 无路径
        }

        /// <summary>
        /// 简化路径：移除共线的中间点，只保留起点、终点和拐弯处
        /// </summary>
        private static List<Vector2Int> SimplifyPath(List<Vector2Int> path)
        {
            if (path.Count <= 2) return path;

            var result = new List<Vector2Int> { path[0] };

            for (int i = 1; i < path.Count - 1; i++)
            {
                // 当前点的方向 = 前一段 vs 后一段
                Vector2Int dirPrev = path[i] - path[i - 1];
                Vector2Int dirNext = path[i + 1] - path[i];
                // 方向变了 → 拐弯点，保留
                if (dirPrev != dirNext)
                    result.Add(path[i]);
            }

            result.Add(path[^1]);
            return result;
        }

        private void DrawPathLines(FloorData floorData, List<int> path)
        {
            // 已废弃：保留空方法以备后用
        }

        private void CreateLine(Vector2 from, Vector2 to, Color color)
        {
            var obj = CreateUIObject("PathLine", _contentRoot);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);

            Vector2 diff = to - from;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(length, PATH_LINE_WIDTH);
            rect.localRotation = Quaternion.Euler(0, 0, angle);

            var img = obj.AddComponent<Image>();
            img.color = color;

            // 路径线在最底层
            obj.transform.SetAsFirstSibling();

            _pathLineObjs.Add(obj);
        }

        // =====================================================================
        //  呼吸动画
        // =====================================================================

        private void AnimatePlayerDot()
        {
            if (_playerDot == null) return;

            _blinkTimer += Time.deltaTime * BLINK_SPEED;
            float t = 0.5f + 0.5f * Mathf.Sin(_blinkTimer);
            _playerDot.color = new Color(0.2f, 0.9f, 0.3f, 0.6f + 0.4f * t);
            float scale = 0.85f + 0.3f * t;
            _playerDot.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        /// <summary>
        /// 瓦片坐标 → 内容层像素坐标
        /// </summary>
        private static Vector2 TileToPixel(Vector2Int tilePos)
        {
            return new Vector2(tilePos.x * PIXELS_PER_TILE,
                               tilePos.y * PIXELS_PER_TILE);
        }

        private static Color GetRoomColor(RoomNode room)
        {
            if (RoomColors.TryGetValue(room.Type, out var color))
            {
                if (room.IsCleared) return color * 0.6f;
                return color;
            }
            return new Color(0.5f, 0.5f, 0.5f);
        }

        private static int CountExploredRooms(FloorData floorData)
        {
            int count = 0;
            foreach (var r in floorData.Rooms)
                if (r.IsExplored) count++;
            return count;
        }

        /// <summary>创建 UI GameObject 的工厂方法</summary>
        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        /// <summary>设置 RectTransform 锚定到右上角</summary>
        private static void SetAnchorTopRight(GameObject obj, Vector2 position, Vector2 size)
        {
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // =====================================================================
        //  程序化圆形 Sprite（全局缓存）
        // =====================================================================

        private static Sprite _circleSprite;

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            float center = res / 2f;
            float radius = center - 1f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius - 1f)
                        tex.SetPixel(x, y, Color.white);
                    else if (dist <= radius)
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - dist + 1f)));
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            _circleSprite = Sprite.Create(tex,
                new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), 100f);

            return _circleSprite;
        }
    }
}
