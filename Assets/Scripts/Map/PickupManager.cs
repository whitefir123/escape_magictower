// ============================================================================
// 逃离魔塔 - 拾取物管理器 (PickupManager)
// 管理所有走廊掉落物的注册、查询和生成。
// 玩家踩到拾取物格子时由 HeroController 调用 TryPickup。
//
// 来源：DesignDocs/06_Map_and_Modes.md §三
//       GameData_Blueprints/09_Consumable_and_Drop_Items.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 拾取物管理器 —— 全局单例
    /// </summary>
    public class PickupManager : MonoBehaviour
    {
        public static PickupManager Instance { get; private set; }

        // === 坐标 → 拾取物实体映射 ===
        private readonly Dictionary<Vector2Int, PickupItem> _items = new();

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
        //  注册/注销
        // =====================================================================

        public void Register(Vector2Int pos, PickupItem item)
        {
            _items[pos] = item;
        }

        public void Unregister(Vector2Int pos)
        {
            _items.Remove(pos);
        }

        /// <summary>查询指定坐标是否有拾取物</summary>
        public PickupItem GetItemAt(Vector2Int pos)
        {
            _items.TryGetValue(pos, out var item);
            return item;
        }

        // =====================================================================
        //  批量生成（地图初始化时调用）
        // =====================================================================

        /// <summary>
        /// 根据 FloorGrid.PickupSpawns 批量生成拾取物实体
        /// </summary>
        public void SpawnPickups(FloorGrid grid)
        {
            foreach (var spawn in grid.PickupSpawns)
            {
                SpawnSinglePickup(spawn);
            }
            Debug.Log($"[PickupManager] 已生成 {grid.PickupSpawns.Count} 个拾取物");
        }

        /// <summary>生成单个拾取物实体</summary>
        private void SpawnSinglePickup(PickupSpawnData data)
        {
            var obj = new GameObject($"Pickup_{data.Type}_{data.Position.x}_{data.Position.y}");
            obj.transform.position = new Vector3(data.Position.x, data.Position.y, 0f);

            var pickup = obj.AddComponent<PickupItem>();
            pickup.Initialize(data.Type, data.Quality, data.Value, data.Position);

            Register(data.Position, pickup);
        }

        /// <summary>当前剩余拾取物数量</summary>
        public int RemainingCount => _items.Count;

        /// <summary>清除所有追踪数据（切换楼层时调用，实体由 FloorTransitionManager 统一销毁）</summary>
        public void Clear()
        {
            _items.Clear();
        }
    }
}
