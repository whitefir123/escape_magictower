// ============================================================================
// 逃离魔塔 - 迷你地图 UI (MinimapUI)
// 右上角常驻小地图：显示房间拓扑、玩家位置、房间类型颜色。
// 与战争迷雾联动，仅显示已探索的房间。
//
// 来源：DesignDocs/07_UI_and_UX.md §1.2
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Map;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 迷你地图 UI —— 纯代码创建，右上角常驻
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        // === 配置 ===
        private const float MAP_SIZE = 180f;          // 小地图容器尺寸（像素）
        private const float ROOM_DOT_SIZE = 18f;      // 房间点尺寸
        private const float CONNECTION_WIDTH = 2f;    // 连线宽度
        private const float MARGIN = 15f;             // 与屏幕边距
        private const float BLINK_SPEED = 3f;         // 玩家标记闪烁速度

        // === UI 元素 ===
        private RectTransform _container;
        private readonly Dictionary<int, Image> _roomDots = new();
        private readonly List<Image> _connectionLines = new();
        private Image _playerMarker;
        private float _blinkTimer;

        // === 数据引用 ===
        private MapManager _mapManager;

        // === 房间类型颜色映射 ===
        private static readonly Dictionary<RoomType, Color> RoomColors = new()
        {
            { RoomType.Combat,   new Color(0.55f, 0.55f, 0.60f) },   // 灰色 - 战斗
            { RoomType.Boss,     new Color(0.90f, 0.20f, 0.20f) },   // 红色 - Boss
            { RoomType.Treasure, new Color(0.90f, 0.75f, 0.15f) },   // 金色 - 宝藏
            { RoomType.Stairs,   new Color(0.30f, 0.85f, 0.40f) },   // 绿色 - 楼梯
            { RoomType.Merchant, new Color(0.30f, 0.70f, 0.90f) },   // 蓝色 - 商人
            { RoomType.Forge,    new Color(0.80f, 0.50f, 0.20f) },   // 橙色 - 铁匠
            { RoomType.Campfire, new Color(0.90f, 0.60f, 0.30f) },   // 暖橙 - 篝火
            { RoomType.Shrine,   new Color(0.70f, 0.50f, 0.90f) },   // 紫色 - 神龛
        };

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化迷你地图（由 HUDManager 调用）
        /// </summary>
        /// <param name="canvasTransform">HUD Canvas 的 Transform</param>
        public void Initialize(Transform canvasTransform)
        {
            _mapManager = FindAnyObjectByType<MapManager>();

            BuildMinimapContainer(canvasTransform);

            Debug.Log("[MinimapUI] 迷你地图初始化完成");
        }

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildMinimapContainer(Transform parent)
        {
            // 半透明背景容器（右上角）
            var containerObj = new GameObject("Minimap_Container", typeof(RectTransform));
            containerObj.transform.SetParent(parent, false);
            _container = containerObj.GetComponent<RectTransform>();
            _container.anchorMin = new Vector2(1, 1);
            _container.anchorMax = new Vector2(1, 1);
            _container.pivot = new Vector2(1, 1);
            _container.anchoredPosition = new Vector2(-MARGIN, -50f); // 偏下一点避开楼层文字
            _container.sizeDelta = new Vector2(MAP_SIZE, MAP_SIZE);

            // 半透明背景
            var bgImg = containerObj.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.45f);

            // 边框
            var outline = containerObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            outline.effectDistance = new Vector2(1, -1);

            // 标题
            var titleObj = new GameObject("Minimap_Title", typeof(RectTransform));
            titleObj.transform.SetParent(containerObj.transform, false);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, 2);
            titleRect.sizeDelta = new Vector2(MAP_SIZE, 16);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "MAP";
            titleText.fontSize = 11;
            titleText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
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
            if (_mapManager == null)
            {
                _mapManager = FindAnyObjectByType<MapManager>();
                if (_mapManager == null) return;
            }

            var floorData = _mapManager.CurrentFloorData;
            if (floorData == null || floorData.Rooms == null || floorData.Rooms.Count == 0) return;

            // 重建地图（房间数量变化时，如楼层切换）
            if (_roomDots.Count != CountExploredRooms(floorData))
            {
                RebuildMap(floorData);
            }

            // 更新玩家位置标记
            UpdatePlayerMarker(floorData, _mapManager.CurrentRoomID);
        }

        // =====================================================================
        //  地图重建
        // =====================================================================

        private void RebuildMap(FloorData floorData)
        {
            // 清除旧元素
            foreach (var dot in _roomDots.Values)
                if (dot != null) Destroy(dot.gameObject);
            _roomDots.Clear();

            foreach (var line in _connectionLines)
                if (line != null) Destroy(line.gameObject);
            _connectionLines.Clear();

            if (_playerMarker != null)
                Destroy(_playerMarker.gameObject);

            // 计算坐标映射（房间网格坐标 → 小地图像素坐标）
            Vector2Int gridMin = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int gridMax = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var room in floorData.Rooms)
            {
                if (!room.IsExplored) continue;
                gridMin = Vector2Int.Min(gridMin, room.GridPosition);
                gridMax = Vector2Int.Max(gridMax, room.GridPosition);
            }

            Vector2Int gridSize = gridMax - gridMin;
            if (gridSize.x == 0) gridSize.x = 1;
            if (gridSize.y == 0) gridSize.y = 1;

            float padding = 20f;
            float drawArea = MAP_SIZE - padding * 2;

            // 画连线（已探索房间之间）
            var drawnConnections = new HashSet<(int, int)>();
            foreach (var room in floorData.Rooms)
            {
                if (!room.IsExplored) continue;
                foreach (int connId in room.ConnectedRoomIDs)
                {
                    var connRoom = floorData.Rooms.Find(r => r.RoomID == connId);
                    if (connRoom == null || !connRoom.IsExplored) continue;

                    int a = Mathf.Min(room.RoomID, connId);
                    int b = Mathf.Max(room.RoomID, connId);
                    if (!drawnConnections.Add((a, b))) continue;

                    Vector2 posA = GridToMinimap(room.GridPosition, gridMin, gridSize, drawArea, padding);
                    Vector2 posB = GridToMinimap(connRoom.GridPosition, gridMin, gridSize, drawArea, padding);
                    CreateConnectionLine(posA, posB);
                }
            }

            // 画房间点
            foreach (var room in floorData.Rooms)
            {
                if (!room.IsExplored) continue;
                Vector2 pos = GridToMinimap(room.GridPosition, gridMin, gridSize, drawArea, padding);
                CreateRoomDot(room, pos);
            }

            // 创建玩家标记
            CreatePlayerMarker();
        }

        /// <summary>
        /// 房间网格坐标 → 迷你地图本地坐标
        /// </summary>
        private static Vector2 GridToMinimap(Vector2Int gridPos, Vector2Int gridMin,
            Vector2Int gridSize, float drawArea, float padding)
        {
            float nx = (gridPos.x - gridMin.x) / (float)gridSize.x;
            float ny = (gridPos.y - gridMin.y) / (float)gridSize.y;
            // 小地图中 Y 轴向上，但 RectTransform 从左上角出发
            return new Vector2(
                padding + nx * drawArea,
                -(padding + (1 - ny) * drawArea)
            );
        }

        private void CreateRoomDot(RoomNode room, Vector2 localPos)
        {
            var obj = new GameObject($"Room_{room.RoomID}", typeof(RectTransform));
            obj.transform.SetParent(_container, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPos;

            // Boss 房稍大
            float dotSize = room.Type == RoomType.Boss ? ROOM_DOT_SIZE * 1.4f : ROOM_DOT_SIZE;
            rect.sizeDelta = new Vector2(dotSize, dotSize);

            var img = obj.AddComponent<Image>();
            img.color = GetRoomColor(room);

            _roomDots[room.RoomID] = img;
        }

        private void CreateConnectionLine(Vector2 from, Vector2 to)
        {
            var obj = new GameObject("Connection", typeof(RectTransform));
            obj.transform.SetParent(_container, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);

            // 计算线段
            Vector2 diff = to - from;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(length, CONNECTION_WIDTH);
            rect.localRotation = Quaternion.Euler(0, 0, angle);

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.45f, 0.45f, 0.50f, 0.5f);

            _connectionLines.Add(img);
        }

        private void CreatePlayerMarker()
        {
            var obj = new GameObject("PlayerMarker", typeof(RectTransform));
            obj.transform.SetParent(_container, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ROOM_DOT_SIZE * 0.6f, ROOM_DOT_SIZE * 0.6f);

            _playerMarker = obj.AddComponent<Image>();
            _playerMarker.color = Color.white;
        }

        // =====================================================================
        //  玩家位置更新
        // =====================================================================

        private void UpdatePlayerMarker(FloorData floorData, int currentRoomID)
        {
            if (_playerMarker == null) return;

            // 找到当前房间的点
            if (_roomDots.TryGetValue(currentRoomID, out var dot) && dot != null)
            {
                _playerMarker.rectTransform.anchoredPosition =
                    dot.rectTransform.anchoredPosition;
            }

            // 闪烁效果
            _blinkTimer += Time.deltaTime * BLINK_SPEED;
            float alpha = 0.5f + 0.5f * Mathf.Sin(_blinkTimer);
            _playerMarker.color = new Color(1f, 1f, 1f, alpha);
        }

        // =====================================================================
        //  辅助
        // =====================================================================

        private static Color GetRoomColor(RoomNode room)
        {
            if (RoomColors.TryGetValue(room.Type, out var color))
            {
                // 已通关房间变暗
                if (room.IsCleared)
                    return color * 0.5f;
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
    }
}
