// ============================================================================
// 逃离魔塔 - Tilemap 碰撞查询提供器 (TilemapCollisionProvider)
// 单例模式，供 GridMovement.IsTileWall 查询某格是否为墙壁/障碍物。
//
// 支持两种碰撞源：
//   1. Unity Tilemap：挂载 wallTilemap 引用后，通过 HasTile 判定
//   2. 手动注册：无 Tilemap 时（如测试场景），通过 HashSet 手动注册墙壁坐标
//
// 来源：DesignDocs/03_Combat_System.md §1.1.1（目标格为墙壁 → 移动取消）
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// Tilemap 碰撞查询提供器 —— 全局单例
    /// </summary>
    public class TilemapCollisionProvider : MonoBehaviour
    {
        /// <summary>全局单例引用</summary>
        public static TilemapCollisionProvider Instance { get; private set; }

        [Header("=== Tilemap 引用（可选） ===")]
        [Tooltip("墙壁/碰撞层 Tilemap。如为 null 则仅使用手动注册的墙壁坐标")]
        [SerializeField] private Tilemap wallTilemap;

        // === 手动注册的墙壁坐标集合（用于测试场景或程序化生成） ===
        private readonly HashSet<Vector2Int> _manualWalls = new();

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TilemapCollisionProvider] 检测到重复实例，销毁自身。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // =====================================================================
        //  公共查询接口
        // =====================================================================

        /// <summary>
        /// 查询指定格子是否为墙壁/障碍物
        /// 优先检查 Tilemap，其次检查手动注册表
        /// </summary>
        /// <param name="gridPos">格子坐标</param>
        /// <returns>true = 不可通行</returns>
        public bool IsWall(Vector2Int gridPos)
        {
            // 优先查 Tilemap
            if (wallTilemap != null)
            {
                Vector3Int cellPos = new Vector3Int(gridPos.x, gridPos.y, 0);
                if (wallTilemap.HasTile(cellPos))
                {
                    return true;
                }
            }

            // 其次查手动注册表
            return _manualWalls.Contains(gridPos);
        }

        // =====================================================================
        //  手动注册接口（测试场景 / 程序化生成使用）
        // =====================================================================

        /// <summary>注册单个墙壁坐标</summary>
        public void RegisterWall(Vector2Int pos)
        {
            _manualWalls.Add(pos);
        }

        /// <summary>批量注册墙壁坐标</summary>
        public void RegisterWalls(IEnumerable<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                _manualWalls.Add(pos);
            }
        }

        /// <summary>清除所有手动注册的墙壁</summary>
        public void ClearManualWalls()
        {
            _manualWalls.Clear();
        }

        /// <summary>移除单个墙壁坐标（门打开后从碰撞系统中移除）</summary>
        public void UnregisterWall(Vector2Int pos)
        {
            _manualWalls.Remove(pos);
        }

        /// <summary>设置 Tilemap 引用（运行时动态切换）</summary>
        public void SetTilemap(Tilemap tilemap)
        {
            wallTilemap = tilemap;
        }
    }
}
