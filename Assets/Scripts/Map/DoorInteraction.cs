// ============================================================================
// 逃离魔塔 - 门交互管理器 (DoorInteraction)
// 处理玩家碰门→检查钥匙→消耗开门→更新视觉与碰撞的完整流程。
//
// 交互流程：
//   1. GridMovement.OnMoveBlocked → 识别阻挡格为门 → 委托给 DoorInteraction
//   2. TryOpenDoor → 检查钥匙库存 → 扣除 → 修改 FloorGrid.Tiles → 更新渲染+碰撞
//
// 来源：DesignDocs/06_Map_and_Modes.md §1.3
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 门交互管理器 —— 全局单例
    /// </summary>
    public class DoorInteraction : MonoBehaviour
    {
        public static DoorInteraction Instance { get; private set; }

        // === 运行时引用 ===
        private FloorGrid _floorGrid;
        private FloorRenderer _floorRenderer;

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

        /// <summary>绑定楼层数据和渲染器（楼层生成后调用）</summary>
        public void Initialize(FloorGrid grid, FloorRenderer renderer)
        {
            _floorGrid = grid;
            _floorRenderer = renderer;
        }

        // =====================================================================
        //  静态工具：判断 TileType 是否为门
        // =====================================================================

        /// <summary>判断指定 TileType 是否为门</summary>
        public static bool IsDoorTile(TileType tile)
        {
            return tile == TileType.DoorBronze ||
                   tile == TileType.DoorSilver ||
                   tile == TileType.DoorGold;
        }

        /// <summary>将 TileType 转换为对应的 DoorTier</summary>
        public static DoorTier GetDoorTier(TileType tile)
        {
            return tile switch
            {
                TileType.DoorBronze => DoorTier.Bronze,
                TileType.DoorSilver => DoorTier.Silver,
                TileType.DoorGold => DoorTier.Gold,
                _ => DoorTier.None,
            };
        }

        // =====================================================================
        //  核心交互逻辑
        // =====================================================================

        /// <summary>
        /// 尝试开门。由 HeroController.OnGridMoveBlocked 在检测到门格时调用。
        /// </summary>
        /// <param name="doorPos">门格坐标</param>
        /// <param name="heroController">玩家控制器（用于检查/消耗钥匙）</param>
        /// <returns>true = 成功开门</returns>
        public bool TryOpenDoor(Vector2Int doorPos,
            EscapeTheTower.Entity.Hero.HeroController heroController)
        {
            if (_floorGrid == null || _floorRenderer == null)
            {
                Debug.LogError("[DoorInteraction] 未初始化！");
                return false;
            }

            if (!_floorGrid.InBounds(doorPos.x, doorPos.y)) return false;

            TileType currentTile = _floorGrid.Tiles[doorPos.x, doorPos.y];
            if (!IsDoorTile(currentTile)) return false;

            DoorTier tier = GetDoorTier(currentTile);

            // 检查玩家是否持有对应钥匙
            if (!heroController.ConsumeKey(tier))
            {
                string keyName = tier switch
                {
                    DoorTier.Bronze => "铜钥匙",
                    DoorTier.Silver => "银钥匙",
                    DoorTier.Gold => "金钥匙",
                    _ => "钥匙",
                };
                Debug.Log($"[门] {keyName}不足，无法开门！当前持有：" +
                          $"铜={heroController.KeyBronze} 银={heroController.KeySilver} 金={heroController.KeyGold}");
                return false;
            }

            // 开门：修改地图数据
            TileType oldTile = currentTile;
            _floorGrid.Tiles[doorPos.x, doorPos.y] = TileType.Floor;

            // 更新视觉渲染
            _floorRenderer.UpdateTile(doorPos.x, doorPos.y, oldTile, TileType.Floor);

            // 从碰撞系统中移除
            var collisionProvider = TilemapCollisionProvider.Instance;
            collisionProvider?.UnregisterWall(doorPos);

            // 广播事件
            EventManager.Publish(new OnDoorOpenedEvent
            {
                Meta = new EventMeta(heroController.EntityID),
                DoorTier = tier,
                Position = doorPos,
            });

            string tierName = tier switch
            {
                DoorTier.Bronze => "铜门",
                DoorTier.Silver => "银门",
                DoorTier.Gold => "金门",
                _ => "门",
            };
            Debug.Log($"[门] 🔓 {tierName}已打开！位置=({doorPos.x},{doorPos.y})");

            // 揭示门后房间的迷雾
            FogOfWarManager.Instance?.RevealRoom(doorPos);

            return true;
        }
    }
}
