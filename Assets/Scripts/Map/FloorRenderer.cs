// ============================================================================
// 逃离魔塔 - 楼层渲染器 (FloorRenderer)
// 将 FloorGrid 数据渲染到 Unity Tilemap，并注册墙壁到碰撞系统。
// 支持单格增量更新（开门等交互场景）。
//
// 来源：DesignDocs/06_Map_and_Modes.md
// ============================================================================

using UnityEngine;
using UnityEngine.Tilemaps;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 楼层渲染器 —— 将 FloorGrid 转为可视 Tilemap
    /// </summary>
    public class FloorRenderer : MonoBehaviour
    {
        // === Tilemap 引用 ===
        private Tilemap _floorTilemap;
        private Tilemap _wallTilemap;
        private Grid _grid;

        // === 程序化 Tile 资源（缓存复用） ===
        private Tile _wallTile;
        private Tile _floorTile;
        private Tile _roomFloorTile;
        private Tile _doorBronzeTile;
        private Tile _doorSilverTile;
        private Tile _doorGoldTile;
        private Tile _spawnTile;
        private Tile _stairsTile;

        // 缓存纹理以便清理（避免内存泄漏）
        private Texture2D[] _cachedTextures;

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>渲染完整楼层地图</summary>
        public void RenderFloor(FloorGrid grid)
        {
            EnsureComponents();
            CreateTileAssets();
            ClearFloor();

            var collisionProvider = TilemapCollisionProvider.Instance;
            collisionProvider?.ClearManualWalls();

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var cellPos = new Vector3Int(x, y, 0);
                    var tile = GetTileForType(grid.Tiles[x, y]);
                    var tileType = grid.Tiles[x, y];

                    if (tileType == TileType.Wall)
                    {
                        _wallTilemap.SetTile(cellPos, tile);
                        collisionProvider?.RegisterWall(new Vector2Int(x, y));
                    }
                    else if (DoorInteraction.IsDoorTile(tileType))
                    {
                        // 门渲染在墙壁层，并注册为碎撞障碍（不可通行，直到开门）
                        _wallTilemap.SetTile(cellPos, tile);
                        collisionProvider?.RegisterWall(new Vector2Int(x, y));
                    }
                    else
                    {
                        _floorTilemap.SetTile(cellPos, tile);
                    }
                }
            }

            Debug.Log($"[FloorRenderer] 渲染完毕：{grid.Width}×{grid.Height}");
        }

        /// <summary>
        /// 增量更新单个格子（用于开门、楼梯解锁等动态交互）
        /// </summary>
        public void UpdateTile(int x, int y, TileType oldType, TileType newType)
        {
            var cellPos = new Vector3Int(x, y, 0);
            var collisionProvider = TilemapCollisionProvider.Instance;

            // 清除旧 Tile
            bool oldIsWallLayer = (oldType == TileType.Wall || DoorInteraction.IsDoorTile(oldType));
            if (oldIsWallLayer)
                _wallTilemap.SetTile(cellPos, null);
            else
                _floorTilemap.SetTile(cellPos, null);

            // 设置新 Tile
            var tile = GetTileForType(newType);
            bool newIsWallLayer = (newType == TileType.Wall || DoorInteraction.IsDoorTile(newType));
            if (newIsWallLayer)
            {
                _wallTilemap.SetTile(cellPos, tile);
                collisionProvider?.RegisterWall(new Vector2Int(x, y));
            }
            else
            {
                _floorTilemap.SetTile(cellPos, tile);
                // 从墙/门变为非墙，移除碞撞注册
                if (oldIsWallLayer)
                    collisionProvider?.UnregisterWall(new Vector2Int(x, y));
            }
        }

        /// <summary>清理旧地图</summary>
        public void ClearFloor()
        {
            if (_floorTilemap != null) _floorTilemap.ClearAllTiles();
            if (_wallTilemap != null) _wallTilemap.ClearAllTiles();
        }

        private void OnDestroy()
        {
            // 释放缓存纹理的 Native 内存
            if (_cachedTextures != null)
            {
                foreach (var tex in _cachedTextures)
                    if (tex != null) Destroy(tex);
            }
        }

        // =====================================================================
        //  Tile 类型映射
        // =====================================================================

        private Tile GetTileForType(TileType type)
        {
            return type switch
            {
                TileType.Wall => _wallTile,
                TileType.Floor => _floorTile,
                TileType.RoomFloor => _roomFloorTile,
                TileType.DoorBronze => _doorBronzeTile,
                TileType.DoorSilver => _doorSilverTile,
                TileType.DoorGold => _doorGoldTile,
                TileType.Spawn => _spawnTile,
                TileType.StairsDown => _stairsTile,
                _ => _floorTile,
            };
        }

        // =====================================================================
        //  组件初始化
        // =====================================================================

        private void EnsureComponents()
        {
            if (_grid != null) return;

            var gridObj = new GameObject("FloorGrid");
            _grid = gridObj.AddComponent<Grid>();
            _grid.cellSize = Vector3.one;

            // 地面层
            var floorObj = new GameObject("Floor");
            floorObj.transform.SetParent(gridObj.transform);
            _floorTilemap = floorObj.AddComponent<Tilemap>();
            _floorTilemap.tileAnchor = Vector3.zero;
            var floorRenderer = floorObj.AddComponent<TilemapRenderer>();
            floorRenderer.sortingOrder = 0;

            // 墙壁层
            var wallObj = new GameObject("Walls");
            wallObj.transform.SetParent(gridObj.transform);
            _wallTilemap = wallObj.AddComponent<Tilemap>();
            _wallTilemap.tileAnchor = Vector3.zero;
            var wallRenderer = wallObj.AddComponent<TilemapRenderer>();
            wallRenderer.sortingOrder = 1;
        }

        /// <summary>程序化创建 Tile 资源并缓存纹理引用</summary>
        private void CreateTileAssets()
        {
            if (_wallTile != null) return;

            _cachedTextures = new Texture2D[8];

            _wallTile = MakeTile(new Color(0.15f, 0.12f, 0.2f), 0);       // 深紫灰墙壁
            _floorTile = MakeTile(new Color(0.35f, 0.30f, 0.25f), 1);      // 棕色走廊
            _roomFloorTile = MakeTile(new Color(0.40f, 0.35f, 0.30f), 2);  // 暖棕房间
            _doorBronzeTile = MakeTile(new Color(0.72f, 0.45f, 0.20f), 3); // 铜色
            _doorSilverTile = MakeTile(new Color(0.75f, 0.75f, 0.80f), 4); // 银色
            _doorGoldTile = MakeTile(new Color(0.90f, 0.75f, 0.20f), 5);   // 金色
            _spawnTile = MakeTile(new Color(0.3f, 0.8f, 0.3f), 6);         // 绿色出生点
            _stairsTile = MakeTile(new Color(0.3f, 0.5f, 0.9f), 7);        // 蓝色楼梯
        }

        private Tile MakeTile(Color color, int texIndex)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _cachedTextures[texIndex] = tex;

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            tile.color = Color.white;
            return tile;
        }
    }
}
