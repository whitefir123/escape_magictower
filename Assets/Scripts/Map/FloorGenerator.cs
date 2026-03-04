// ============================================================================
// 逃离魔塔 - 楼层地图生成器 (FloorGenerator) v3
// 程序化生成大型迷宫式格子地图。
//
// 生成管线（严格执行顺序）：
//   1. 初始化网格：全部填充为 WALL
//   2. 随机放置房间：碰撞检测，房间间≥1格间隙
//   3. 提前标记安全房间（Boss/宝箱/出生）
//   4. 迭代式回溯迷宫雕刻：在房间外的 WALL 空间生成密集走廊
//   5. 走廊加宽后处理：随机将部分走廊段拓宽至 2~3 格
//   6. 房间连接器：普通房间开口=FLOOR，安全房间开口=DOOR
//   7. 放置出生点和楼梯：BFS 路径距离最远的两个房间
//   8. 多路径验证：BFS 顶点不相交，确保≥3条独立路径
//   9. 分配房间类型和门等级
//
// 参考：TaskLogs/map_generation_logic.md.resolved
//       DesignDocs/06_Map_and_Modes.md
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EscapeTheTower.Map
{
    // =========================================================================
    //  数据结构
    // =========================================================================

    /// <summary>楼层地图数据 — 生成器输出</summary>
    public class FloorGrid
    {
        public int Width;
        public int Height;
        public TileType[,] Tiles;
        public List<RoomData> Rooms = new();
        public Vector2Int SpawnPoint;
        public Vector2Int StairsPoint;
        /// <summary>楼梯是否锁定（击败 Boss 后解锁）</summary>
        public bool StairsLocked = true;
        /// <summary>每格对应的房间ID（走廊/墙壁=0）</summary>
        public int[,] RoomMap;
        /// <summary>拾取物生成数据（走廊掉落物位置和类型）</summary>
        public List<PickupSpawnData> PickupSpawns = new();
        /// <summary>走廊/死胡同宝箱坐标（无锁，直接可开）</summary>
        public List<Vector2Int> CorridorChestSpawns = new();

        // RoomID → RoomData 快速查找表（避免 LINQ 线性搜索）
        private Dictionary<int, RoomData> _roomLookup;

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && Tiles[x, y] != TileType.Wall;

        /// <summary>构建 RoomID 快速查找表（生成完毕后调用一次）</summary>
        public void BuildRoomLookup()
        {
            _roomLookup = new Dictionary<int, RoomData>(Rooms.Count);
            foreach (var room in Rooms)
                _roomLookup[room.RoomID] = room;
        }

        /// <summary>获取指定坐标所在的房间（不在房间内返回 null）</summary>
        public RoomData GetRoomAt(int x, int y)
        {
            if (!InBounds(x, y) || RoomMap[x, y] == 0) return null;
            // 优先使用字典 O(1) 查找，未构建时回退到线性搜索
            if (_roomLookup != null)
                return _roomLookup.TryGetValue(RoomMap[x, y], out var room) ? room : null;
            return Rooms.FirstOrDefault(r => r.RoomID == RoomMap[x, y]);
        }
    }

    /// <summary>房间数据</summary>
    public class RoomData
    {
        public int RoomID;
        public RectInt Bounds;     // 左下角 + 宽高
        public RoomType Type;
        public bool IsSecure;      // 安全房间（宝箱房/Boss房），用 DOOR 封锁
        public bool HasDoor;
        public DoorTier DoorTier;
        public List<Vector2Int> Entrances = new();

        /// <summary>运行时追踪：房间内剩余怪物数（用于通关锁判定）</summary>
        [System.NonSerialized] public int MonsterCount;

        public Vector2Int Center => new(
            Bounds.x + Bounds.width / 2,
            Bounds.y + Bounds.height / 2);
    }

    /// <summary>拾取物生成数据（地图生成器输出）</summary>
    public class PickupSpawnData
    {
        public Vector2Int Position;
        public PickupType Type;
        /// <summary>消耗品品质（钥匙/金币无品质）</summary>
        public QualityTier Quality;
        /// <summary>效果数值（血瓶回复量/金币数等）</summary>
        public int Value;
    }

    // =========================================================================
    //  生成配置（可由 BiomeConfig_SO 驱动）
    // =========================================================================

    /// <summary>楼层生成参数 — 后续可从 SO 注入</summary>
    public class FloorGenConfig
    {
        public int TargetRoomCount = 12;
        public int MaxPlaceAttempts = 200;
        public int MinRoomSize = 5;
        public int MaxRoomSize = 8;
        /// <summary>Boss 房最小尺寸</summary>
        public int BossRoomMinSize = 7;
        /// <summary>Boss 房最大尺寸</summary>
        public int BossRoomMaxSize = 10;
        public int RoomGap = 1;
        public int MinPathsRequired = 3;
        public float DoorChance = 0.60f;
        public int SecureRoomMin = 2;
        public int SecureRoomMax = 3;
        /// <summary>走廊加宽概率（0~1）</summary>
        public float CorridorWidenChance = 0.12f;

        // === 掠落物散布配置 ===
        /// <summary>楼层编号（影响掉落品质）</summary>
        public int FloorLevel = 1;
        /// <summary>血瓶散布数量范围</summary>
        public int HealthPotionMin = 8;
        public int HealthPotionMax = 15;
        /// <summary>钥匙散布数量范围</summary>
        public int KeyMin = 3;
        public int KeyMax = 6;
        /// <summary>金币堆散布数量范围</summary>
        public int GoldPileMin = 5;
        public int GoldPileMax = 10;

        /// <summary>默认配置</summary>
        public static readonly FloorGenConfig Default = new();
    }

    // =========================================================================
    //  生成器
    // =========================================================================

    public static class FloorGenerator
    {
        // 四方向（步长 1）
        private static readonly (int dx, int dy)[] Dir4 = { (0, 1), (0, -1), (1, 0), (-1, 0) };
        // 四方向（步长 2，用于迷宫雕刻）
        private static readonly (int dx, int dy)[] Dir4x2 = { (0, 2), (0, -2), (2, 0), (-2, 0) };

        // =====================================================================
        //  主入口
        // =====================================================================

        /// <summary>
        /// 生成一层完整地图
        /// </summary>
        /// <param name="width">地图宽度（格）</param>
        /// <param name="height">地图高度（格）</param>
        /// <param name="seed">随机种子</param>
        /// <param name="config">生成参数（null 使用默认值）</param>
        public static FloorGrid Generate(int width, int height, int seed, FloorGenConfig config = null)
        {
            config ??= FloorGenConfig.Default;

            // 确保宽高为奇数（迷宫算法步长2需要奇数网格）
            if (width % 2 == 0) width++;
            if (height % 2 == 0) height++;

            var rng = new System.Random(seed);
            var grid = new FloorGrid
            {
                Width = width,
                Height = height,
                Tiles = new TileType[width, height],
                RoomMap = new int[width, height],
            };

            // ── 步骤 1：全图填充 WALL ──
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    grid.Tiles[x, y] = TileType.Wall;

            // ── 步骤 2：随机放置房间 ──
            PlaceRooms(grid, rng, config);

            // ── 步骤 3：提前标记安全房间 ──
            PreAssignSecureRooms(grid, rng, config);

            // ── 步骤 4：迭代式迷宫雕刻 ──
            CarveMaze(grid, rng);

            // ── 步骤 5：走廊加宽后处理 ──
            WidenCorridors(grid, rng, config);

            // ── 步骤 6：房间连接器 ──
            ConnectRooms(grid, rng);

            // ── 步骤 7：放置出生点和楼梯 ──
            PlaceSpawnAndStairs(grid, rng);

            // ── 步骤 8：多路径验证 ──
            EnsureMultiplePaths(grid, rng, config);

            // ── 步骤 9：分配门等级 ──
            AssignDoors(grid, rng, config);

            // ── 步骤 9.5：重建房间墙壁（终极安全网）──
            RebuildRoomWalls(grid);

            // ── 清理孤立格 ──
            CleanIsolated(grid);

            // ── 步骤 10：走廊掉落物散布 ──
            ScatterCorridorDrops(grid, rng, config);

            // ── 构建 RoomID 快速查找表（所有房间就绪后一次性构建）──
            grid.BuildRoomLookup();

            Debug.Log($"[FloorGenerator] 生成完毕：{width}×{height}，" +
                      $"{grid.Rooms.Count} 个房间，{grid.PickupSpawns.Count} 个掉落物，种子={seed}");
            return grid;
        }

        // =====================================================================
        //  步骤 2：随机放置房间
        // =====================================================================

        private static void PlaceRooms(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            int placed = 0;
            for (int attempt = 0; attempt < cfg.MaxPlaceAttempts && placed < cfg.TargetRoomCount; attempt++)
            {
                // 最后一个房间用 Boss 尺寸（更大）
                bool isBossRoom = (placed == cfg.TargetRoomCount - 1);
                int minSize = isBossRoom ? cfg.BossRoomMinSize : cfg.MinRoomSize;
                int maxSize = isBossRoom ? cfg.BossRoomMaxSize : cfg.MaxRoomSize;

                int w = minSize + rng.Next(maxSize - minSize + 1);
                int h = minSize + rng.Next(maxSize - minSize + 1);

                // 强制奇数尺寸和坐标，与迷宫步长-2网格对齐
                // → 墙壁落在偶数位，走廊(奇数位)可抵达墙壁外侧 → 精确 1 格墙厚
                w |= 1;
                h |= 1;

                // [P0-Risk2] 保护：地图太小放不下时跳过
                int xRange = grid.Width - w - 4;
                int yRange = grid.Height - h - 4;
                if (xRange <= 0 || yRange <= 0) continue;

                int x = 2 + rng.Next(xRange);
                int y = 2 + rng.Next(yRange);
                x |= 1; // 强制奇数起点
                y |= 1;

                // 对齐后重新检查边界
                if (x + w >= grid.Width - 1 || y + h >= grid.Height - 1) continue;

                // 碰撞检测
                bool collides = false;
                foreach (var existing in grid.Rooms)
                {
                    if (x < existing.Bounds.xMax + cfg.RoomGap + 1 &&
                        x + w + cfg.RoomGap > existing.Bounds.x - 1 &&
                        y < existing.Bounds.yMax + cfg.RoomGap + 1 &&
                        y + h + cfg.RoomGap > existing.Bounds.y - 1)
                    {
                        collides = true;
                        break;
                    }
                }
                if (collides) continue;

                var room = new RoomData
                {
                    RoomID = placed + 1,
                    Bounds = new RectInt(x, y, w, h),
                };
                grid.Rooms.Add(room);

                // 凿空房间内部 + 填充 RoomMap
                for (int rx = x; rx < x + w; rx++)
                    for (int ry = y; ry < y + h; ry++)
                    {
                        grid.Tiles[rx, ry] = TileType.RoomFloor;
                        grid.RoomMap[rx, ry] = room.RoomID;
                    }

                placed++;
            }
        }

        // =====================================================================
        //  步骤 3：提前标记安全房间
        //  [P0-Bug2] 必须在连接器（步骤6）之前完成
        // =====================================================================

        private static void PreAssignSecureRooms(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            if (grid.Rooms.Count < 2) return;

            // 出生房 = rooms[0]（第一个放置的房间）
            // [P1-遗漏4] 标记为安全区
            grid.Rooms[0].IsSecure = true;

            // Boss 房 = rooms[last]（最后放置的房间，通常离起始最远）
            var bossRoom = grid.Rooms[^1];
            bossRoom.Type = RoomType.Boss;
            bossRoom.IsSecure = true;

            // 从中间房间随机选 2~3 个作为宝箱安全房间
            var middleRooms = grid.Rooms.Skip(1).Take(grid.Rooms.Count - 2).ToList();
            int secureCount = Mathf.Min(
                cfg.SecureRoomMin + rng.Next(cfg.SecureRoomMax - cfg.SecureRoomMin + 1),
                middleRooms.Count);

            Shuffle(middleRooms, rng);
            for (int i = 0; i < secureCount; i++)
            {
                middleRooms[i].Type = RoomType.Treasure;
                middleRooms[i].IsSecure = true;
            }
        }

        // =====================================================================
        //  步骤 4：迭代式回溯迷宫雕刻
        //  [P0-Bug1] 使用显式栈替代递归，防止栈溢出
        // =====================================================================

        private static void CarveMaze(FloorGrid grid, System.Random rng)
        {
            for (int x = 1; x < grid.Width - 1; x += 2)
            {
                for (int y = 1; y < grid.Height - 1; y += 2)
                {
                    if (grid.Tiles[x, y] != TileType.Wall) continue;
                    if (HasAdjacentNonWall(grid, x, y)) continue;

                    CarveIterative(grid, x, y, rng);
                }
            }
        }

        /// <summary>迷宫雕刻（迭代版，显式栈）</summary>
        private static void CarveIterative(FloorGrid grid, int startX, int startY, System.Random rng)
        {
            var stack = new Stack<Vector2Int>();
            grid.Tiles[startX, startY] = TileType.Floor;
            stack.Push(new Vector2Int(startX, startY));

            while (stack.Count > 0)
            {
                var current = stack.Peek();
                // 收集可雕刻的方向
                var validDirs = new List<(int dx, int dy)>();
                foreach (var (dx, dy) in Dir4x2)
                {
                    int nx = current.x + dx, ny = current.y + dy;
                    if (grid.InBounds(nx, ny) && grid.Tiles[nx, ny] == TileType.Wall
                        && !IsAdjacentToRoom(grid, nx, ny))
                        validDirs.Add((dx, dy));
                }

                if (validDirs.Count == 0)
                {
                    stack.Pop(); // 回溯
                    continue;
                }

                // 随机选一个方向
                var (cdx, cdy) = validDirs[rng.Next(validDirs.Count)];
                int mx = current.x + cdx / 2, my = current.y + cdy / 2;
                int tx = current.x + cdx, ty = current.y + cdy;

                // 保护房间边墙：中间格紧贴房间地板时不可雕刻
                if (IsAdjacentToRoom(grid, mx, my))
                    continue;

                grid.Tiles[mx, my] = TileType.Floor; // 打通中间墙
                grid.Tiles[tx, ty] = TileType.Floor; // 雕刻目标格
                stack.Push(new Vector2Int(tx, ty));
            }
        }

        private static bool HasAdjacentNonWall(FloorGrid grid, int x, int y)
        {
            foreach (var (dx, dy) in Dir4)
            {
                int nx = x + dx, ny = y + dy;
                if (grid.InBounds(nx, ny) && grid.Tiles[nx, ny] != TileType.Wall)
                    return true;
            }
            return false;
        }

        /// <summary>检查指定格子是否紧邻房间地板（8方向含对角线），用于保护 1 格墙壁环 + 墙角</summary>
        private static bool IsAdjacentToRoom(FloorGrid grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (grid.InBounds(nx, ny) && grid.Tiles[nx, ny] == TileType.RoomFloor)
                    return true;
            }
            return false;
        }

        // =====================================================================
        //  步骤 5：走廊加宽后处理
        //  [P1-遗漏1] 设计文档要求走廊宽 1~3 格
        // =====================================================================

        private static void WidenCorridors(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            // 遍历所有走廊格，随机将相邻墙壁打通形成 2~3 格宽走廊
            // 使用快照避免迭代过程中修改影响判断
            var corridorCells = new List<Vector2Int>();
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                    if (grid.Tiles[x, y] == TileType.Floor)
                        corridorCells.Add(new Vector2Int(x, y));

            foreach (var cell in corridorCells)
            {
                if (rng.NextDouble() >= cfg.CorridorWidenChance) continue;

                // 朝随机方向拓宽（优先拓宽与走廊方向垂直的墙）
                var dirs = Dir4.ToArray();
                Shuffle(dirs, rng);

                foreach (var (dx, dy) in dirs)
                {
                    int wx = cell.x + dx, wy = cell.y + dy;
                    if (!grid.InBounds(wx, wy)) continue;
                    if (grid.Tiles[wx, wy] != TileType.Wall) continue;
                    // 不打通房间边界墙（保持房间 1 格墙壁完整性）
                    if (grid.RoomMap[wx, wy] > 0 || IsAdjacentToRoom(grid, wx, wy)) continue;
                    // 不打通地图最外圈
                    if (wx <= 0 || wx >= grid.Width - 1 || wy <= 0 || wy >= grid.Height - 1) continue;

                    grid.Tiles[wx, wy] = TileType.Floor;
                    break; // 每格只拓宽一次
                }
            }
        }

        // =====================================================================
        //  步骤 6：房间连接器
        // =====================================================================

        private static void ConnectRooms(FloorGrid grid, System.Random rng)
        {
            foreach (var room in grid.Rooms)
            {
                var candidates = GetConnectorCandidates(grid, room);

                if (room.IsSecure)
                {
                    // 安全房间：仅开 1 个口，设为 DOOR（临时铜门，步骤9覆盖）
                    if (candidates.Count > 0)
                    {
                        var chosen = candidates[rng.Next(candidates.Count)];
                        grid.Tiles[chosen.x, chosen.y] = TileType.DoorBronze;
                        room.Entrances.Add(chosen);
                    }
                    else
                    {
                        ForceConnect(grid, room, rng, true);
                    }
                }
                else
                {
                    // 普通房间：开 1~2 个口（房间必须保持封闭性，最多2个出入口）
                    int openCount = Mathf.Min(1 + rng.Next(2), Mathf.Max(1, candidates.Count));
                    Shuffle(candidates, rng);

                    if (candidates.Count == 0)
                    {
                        ForceConnect(grid, room, rng, false);
                    }
                    else
                    {
                        for (int i = 0; i < openCount && i < candidates.Count; i++)
                        {
                            grid.Tiles[candidates[i].x, candidates[i].y] = TileType.Floor;
                            room.Entrances.Add(candidates[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 扫描房间四边，找外侧第 2 格为走廊的墙壁格作为连接候选
        /// </summary>
        private static List<Vector2Int> GetConnectorCandidates(FloorGrid grid, RoomData room)
        {
            var candidates = new List<Vector2Int>();
            var b = room.Bounds;

            // 下边
            for (int x = b.x; x < b.xMax; x++)
                TryAddCandidate(grid, candidates, x, b.y - 1, x, b.y - 2);
            // 上边
            for (int x = b.x; x < b.xMax; x++)
                TryAddCandidate(grid, candidates, x, b.yMax, x, b.yMax + 1);
            // 左边
            for (int y = b.y; y < b.yMax; y++)
                TryAddCandidate(grid, candidates, b.x - 1, y, b.x - 2, y);
            // 右边
            for (int y = b.y; y < b.yMax; y++)
                TryAddCandidate(grid, candidates, b.xMax, y, b.xMax + 1, y);

            return candidates;
        }

        private static void TryAddCandidate(FloorGrid grid, List<Vector2Int> candidates,
            int wallX, int wallY, int outerX, int outerY)
        {
            if (!grid.InBounds(outerX, outerY)) return;
            if (grid.Tiles[wallX, wallY] == TileType.Wall && grid.Tiles[outerX, outerY] == TileType.Floor)
                candidates.Add(new Vector2Int(wallX, wallY));
        }

        /// <summary>
        /// 强制在房间边缘中点打通连接
        /// [P1-Bug4] 向外持续挖掘直到遇到走廊（最多 3 格）
        /// </summary>
        private static void ForceConnect(FloorGrid grid, RoomData room, System.Random rng, bool isDoor)
        {
            var b = room.Bounds;
            // 四边中点 + 外挖方向
            (Vector2Int start, int dx, int dy)[] edges =
            {
                (new(b.x + b.width / 2, b.y - 1),  0, -1), // 下
                (new(b.x + b.width / 2, b.yMax),    0,  1), // 上
                (new(b.x - 1, b.y + b.height / 2), -1,  0), // 左
                (new(b.xMax, b.y + b.height / 2),   1,  0), // 右
            };

            var edgeList = edges.ToList();
            Shuffle(edgeList, rng);

            foreach (var (start, dx, dy) in edgeList)
            {
                if (!grid.InBounds(start.x, start.y)) continue;

                // 入口处设门或地面
                grid.Tiles[start.x, start.y] = isDoor ? TileType.DoorBronze : TileType.Floor;
                room.Entrances.Add(start);

                // 向外持续挖掘直到遇到走廊或房间（最多 3 格）
                for (int step = 1; step <= 3; step++)
                {
                    int ox = start.x + dx * step, oy = start.y + dy * step;
                    if (!grid.InBounds(ox, oy)) break;
                    if (grid.Tiles[ox, oy] != TileType.Wall) break; // 已到走廊
                    grid.Tiles[ox, oy] = TileType.Floor;
                }
                return; // 成功连接一个方向即可
            }
        }

        // =====================================================================
        //  步骤 7：放置出生点和楼梯
        //  [P1-Bug3] 改用 BFS 路径距离选择最远房间对
        // =====================================================================

        private static void PlaceSpawnAndStairs(FloorGrid grid, System.Random rng)
        {
            if (grid.Rooms.Count < 2)
            {
                // 保底：在走廊中找两个 FLOOR 格
                PlaceFallbackSpawnAndStairs(grid, rng);
                return;
            }

            // 出生房 = rooms[0]（步骤3中已标记）
            var spawnRoom = grid.Rooms[0];
            grid.SpawnPoint = spawnRoom.Center;
            grid.Tiles[grid.SpawnPoint.x, grid.SpawnPoint.y] = TileType.Spawn;

            // 楼梯房：用 BFS 找距出生点路径距离最远的房间
            RoomData farthestRoom = null;
            int maxPathDist = 0;

            // 先做一次 BFS 计算所有格子到出生点的距离
            var distMap = BFSDistanceMap(grid, grid.SpawnPoint);

            foreach (var room in grid.Rooms)
            {
                if (room == spawnRoom) continue;
                int d = distMap[room.Center.x, room.Center.y];
                if (d > maxPathDist)
                {
                    maxPathDist = d;
                    farthestRoom = room;
                }
            }

            if (farthestRoom == null) farthestRoom = grid.Rooms[^1];

            // 楼梯放在楼梯房的中心
            grid.StairsPoint = farthestRoom.Center;
            grid.Tiles[grid.StairsPoint.x, grid.StairsPoint.y] = TileType.StairsDown;
            farthestRoom.Type = RoomType.Stairs;
        }

        /// <summary>BFS 计算从起点到所有可达格的路径距离</summary>
        private static int[,] BFSDistanceMap(FloorGrid grid, Vector2Int start)
        {
            int[,] dist = new int[grid.Width, grid.Height];
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    dist[x, y] = -1;

            dist[start.x, start.y] = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int cd = dist[cur.x, cur.y];
                foreach (var (dx, dy) in Dir4)
                {
                    int nx = cur.x + dx, ny = cur.y + dy;
                    if (!grid.InBounds(nx, ny)) continue;
                    if (dist[nx, ny] >= 0) continue; // 已访问
                    if (grid.Tiles[nx, ny] == TileType.Wall) continue;
                    dist[nx, ny] = cd + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return dist;
        }

        /// <summary>保底出生/楼梯放置（房间不足时使用走廊格）</summary>
        private static void PlaceFallbackSpawnAndStairs(FloorGrid grid, System.Random rng)
        {
            var floorCells = new List<Vector2Int>();
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                    if (grid.Tiles[x, y] == TileType.Floor || grid.Tiles[x, y] == TileType.RoomFloor)
                        floorCells.Add(new Vector2Int(x, y));

            if (floorCells.Count < 2)
            {
                Debug.LogError("[FloorGenerator] 地图生成失败：可用格子不足！");
                grid.SpawnPoint = new Vector2Int(grid.Width / 2, grid.Height / 2);
                grid.StairsPoint = new Vector2Int(grid.Width / 2 + 1, grid.Height / 2);
                return;
            }

            Shuffle(floorCells, rng);
            grid.SpawnPoint = floorCells[0];
            grid.StairsPoint = floorCells[^1];
            grid.Tiles[grid.SpawnPoint.x, grid.SpawnPoint.y] = TileType.Spawn;
            grid.Tiles[grid.StairsPoint.x, grid.StairsPoint.y] = TileType.StairsDown;
        }

        // =====================================================================
        //  步骤 8：多路径验证
        // =====================================================================

        private static void EnsureMultiplePaths(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            int pathCount = CountDisjointPaths(grid, grid.SpawnPoint, grid.StairsPoint, cfg.MinPathsRequired);

            // 阶段 A：每轮只开 1 个房间的 1 个口（避免连接器洪泛）
            var nonSecureRooms = grid.Rooms.Where(r => !r.IsSecure).ToList();
            for (int attempt = 0; attempt < 20 && pathCount < cfg.MinPathsRequired; attempt++)
            {
                Shuffle(nonSecureRooms, rng);
                bool opened = false;
                foreach (var room in nonSecureRooms)
                {
                    // 入口上限：每个房间最多 3 个入口
                    if (room.Entrances.Count >= 3) continue;
                    var candidates = GetConnectorCandidates(grid, room);
                    if (candidates.Count == 0) continue;

                    var pos = candidates[rng.Next(candidates.Count)];
                    grid.Tiles[pos.x, pos.y] = TileType.Floor;
                    room.Entrances.Add(pos);
                    opened = true;
                    break; // 每轮只开 1 个
                }
                if (!opened) break; // 没有可开的房间了

                pathCount = CountDisjointPaths(grid, grid.SpawnPoint, grid.StairsPoint, cfg.MinPathsRequired);
            }

            // 阶段 B：随机打开有 ≥2 个 FLOOR 邻居的墙壁
            // 缓存候选列表（性能优化）
            if (pathCount < cfg.MinPathsRequired)
            {
                var wallCandidates = CollectBridgeWalls(grid);
                Shuffle(wallCandidates, rng);

                for (int i = 0; i < Mathf.Min(50, wallCandidates.Count) && pathCount < cfg.MinPathsRequired; i++)
                {
                    grid.Tiles[wallCandidates[i].x, wallCandidates[i].y] = TileType.Floor;
                    pathCount = CountDisjointPaths(grid, grid.SpawnPoint, grid.StairsPoint, cfg.MinPathsRequired);
                }
            }

            Debug.Log($"[FloorGenerator] 多路径验证：{pathCount} 条独立路径");
        }

        /// <summary>收集所有有≥2个可通行邻居的墙壁格</summary>
        private static List<Vector2Int> CollectBridgeWalls(FloorGrid grid)
        {
            var result = new List<Vector2Int>();
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    if (grid.Tiles[x, y] != TileType.Wall) continue;
                    // 不破坏房间墙壁
                    if (IsAdjacentToRoom(grid, x, y)) continue;
                    int n = 0;
                    foreach (var (dx, dy) in Dir4)
                        if (grid.IsWalkable(x + dx, y + dy)) n++;
                    if (n >= 2) result.Add(new Vector2Int(x, y));
                }
            return result;
        }

        /// <summary>计算顶点不相交路径数（BFS + 阻塞法）</summary>
        private static int CountDisjointPaths(FloorGrid grid, Vector2Int start, Vector2Int end, int maxCount)
        {
            bool[,] passable = new bool[grid.Width, grid.Height];
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    passable[x, y] = grid.Tiles[x, y] != TileType.Wall;

            int found = 0;
            for (int i = 0; i < maxCount; i++)
            {
                var path = BFSPath(passable, grid.Width, grid.Height, start, end);
                if (path == null) break;

                found++;
                for (int j = 1; j < path.Count - 1; j++)
                    passable[path[j].x, path[j].y] = false;
            }
            return found;
        }

        private static List<Vector2Int> BFSPath(bool[,] passable, int w, int h, Vector2Int start, Vector2Int end)
        {
            var visited = new bool[w, h];
            var parent = new Vector2Int?[w, h]; // 用数组替代 Dictionary，性能更好
            var queue = new Queue<Vector2Int>();

            visited[start.x, start.y] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == end)
                {
                    var path = new List<Vector2Int>();
                    Vector2Int? node = end;
                    while (node.HasValue && node.Value != start)
                    {
                        path.Add(node.Value);
                        node = parent[node.Value.x, node.Value.y];
                    }
                    path.Add(start);
                    path.Reverse();
                    return path;
                }

                foreach (var (dx, dy) in Dir4)
                {
                    int nx = cur.x + dx, ny = cur.y + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (!passable[nx, ny] || visited[nx, ny]) continue;
                    visited[nx, ny] = true;
                    parent[nx, ny] = cur;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return null;
        }

        // =====================================================================
        //  步骤 9：分配门等级
        //  房间类型已在步骤3标记，这里只处理门
        // =====================================================================

        private static void AssignDoors(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            // 给尚未分配类型的房间分配类型
            foreach (var room in grid.Rooms)
            {
                if (room.Type != default && room.Type != RoomType.Combat) continue;
                if (room.Center == grid.SpawnPoint) continue;
                if (room.IsSecure && room.Type == default) { room.Type = RoomType.Treasure; continue; }

                // 剩余房间按权重分配
                float roll = (float)rng.NextDouble();
                if (roll < 0.70f)
                    room.Type = RoomType.Combat;
                else
                {
                    var eventTypes = new[] { RoomType.Merchant, RoomType.Forge,
                        RoomType.Campfire, RoomType.Shrine };
                    room.Type = eventTypes[rng.Next(eventTypes.Length)];
                }
            }

            // 出生房标记
            var spawnRoom = grid.Rooms.FirstOrDefault(r => r.Center == grid.SpawnPoint);
            if (spawnRoom != null)
            {
                spawnRoom.Type = RoomType.Combat; // 空安全区
                spawnRoom.HasDoor = false;
                spawnRoom.DoorTier = DoorTier.None;
            }

            // 分配门等级
            foreach (var room in grid.Rooms)
            {
                if (room == spawnRoom || room.Type == RoomType.Stairs)
                {
                    room.HasDoor = false;
                    room.DoorTier = DoorTier.None;
                    continue;
                }

                if (room.IsSecure)
                {
                    room.HasDoor = true;
                    room.DoorTier = room.Type == RoomType.Boss ? DoorTier.Gold :
                                    rng.NextDouble() < 0.5 ? DoorTier.Silver : DoorTier.Bronze;
                }
                else
                {
                    room.HasDoor = rng.NextDouble() < cfg.DoorChance;
                    if (room.HasDoor)
                    {
                        float r = (float)rng.NextDouble();
                        room.DoorTier = r < 0.60f ? DoorTier.Bronze :
                                        r < 0.90f ? DoorTier.Silver : DoorTier.Gold;
                    }
                }

                // 更新入口 Tile
                if (room.HasDoor)
                {
                    TileType doorTile = room.DoorTier switch
                    {
                        DoorTier.Gold => TileType.DoorGold,
                        DoorTier.Silver => TileType.DoorSilver,
                        _ => TileType.DoorBronze,
                    };
                    foreach (var entrance in room.Entrances)
                        if (grid.InBounds(entrance.x, entrance.y))
                            grid.Tiles[entrance.x, entrance.y] = doorTile;
                }
                else
                {
                    // 普通无门房间：确保入口是 FLOOR
                    foreach (var entrance in room.Entrances)
                        if (grid.InBounds(entrance.x, entrance.y))
                            grid.Tiles[entrance.x, entrance.y] = TileType.Floor;
                }
            }
        }

        // =====================================================================
        //  步骤 9.5：重建房间墙壁（终极安全网）
        //  扫描每个房间的墙壁环，将非入口的墙壁强制恢复为 Wall。
        //  即使前面步骤有遗漏，此步骤保证房间墙壁完整。
        // =====================================================================

        private static void RebuildRoomWalls(FloorGrid grid)
        {
            foreach (var room in grid.Rooms)
            {
                var b = room.Bounds;
                var entranceSet = new HashSet<Vector2Int>(room.Entrances);

                // 扫描墙壁环（Bounds 外扩 1 格，跳过内部）
                for (int x = b.x - 1; x <= b.xMax; x++)
                    for (int y = b.y - 1; y <= b.yMax; y++)
                    {
                        // 跳过房间内部
                        if (x >= b.x && x < b.xMax && y >= b.y && y < b.yMax) continue;
                        if (!grid.InBounds(x, y)) continue;

                        var pos = new Vector2Int(x, y);
                        // 入口保留（门 / 通道），其他强制恢复为墙壁
                        if (!entranceSet.Contains(pos))
                            grid.Tiles[x, y] = TileType.Wall;
                    }
            }
        }

        // =====================================================================
        //  清理孤立格（生成缺陷修复）
        // =====================================================================

        private static void CleanIsolated(FloorGrid grid)
        {
            // 清除孤立 Floor 格（0 个可通行邻居 → 恢复为 Wall）
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    if (grid.Tiles[x, y] != TileType.Floor) continue;
                    int open = 0;
                    foreach (var (dx, dy) in Dir4)
                        if (grid.IsWalkable(x + dx, y + dy)) open++;
                    if (open == 0) grid.Tiles[x, y] = TileType.Wall;
                }

            // 清除走廊中的孤立墙柱（≥3 面可通行 → 转为 Floor）
            // 使用快照式批量清除，避免级联效应（逐格修改会导致邻居判定变化→多米诺清除）
            var pillarsToRemove = new List<Vector2Int>();
            for (int x = 1; x < grid.Width - 1; x++)
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    if (grid.Tiles[x, y] != TileType.Wall) continue;
                    if (IsAdjacentToRoom(grid, x, y)) continue; // 保护房间墙壁

                    int walkable = 0;
                    foreach (var (dx, dy) in Dir4)
                        if (grid.InBounds(x + dx, y + dy) && grid.IsWalkable(x + dx, y + dy))
                            walkable++;
                    if (walkable == 4) pillarsToRemove.Add(new Vector2Int(x, y));
                }
            foreach (var pos in pillarsToRemove)
                grid.Tiles[pos.x, pos.y] = TileType.Floor;
        }

        // =====================================================================
        //  步骤 10：走廊掉落物散布
        //  来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md
        // =====================================================================

        private static void ScatterCorridorDrops(FloorGrid grid, System.Random rng, FloorGenConfig cfg)
        {
            // 收集所有走廊可用坐标（TileType.Floor，排除出生点/楼梯附近 3 格）
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid.Tiles[x, y] != TileType.Floor) continue;

                    // 排除出生点附近
                    int distSpawn = Mathf.Abs(x - grid.SpawnPoint.x) + Mathf.Abs(y - grid.SpawnPoint.y);
                    if (distSpawn <= 3) continue;

                    // 排除楼梯附近
                    int distStairs = Mathf.Abs(x - grid.StairsPoint.x) + Mathf.Abs(y - grid.StairsPoint.y);
                    if (distStairs <= 3) continue;

                    candidates.Add(new Vector2Int(x, y));
                }
            }

            Shuffle(candidates, rng);
            int index = 0;

            // ── 血瓶散布 ──
            int potionCount = cfg.HealthPotionMin + rng.Next(cfg.HealthPotionMax - cfg.HealthPotionMin + 1);
            for (int i = 0; i < potionCount && index < candidates.Count; i++, index++)
            {
                QualityTier quality = RollPotionQuality(cfg.FloorLevel, rng);
                grid.PickupSpawns.Add(new PickupSpawnData
                {
                    Position = candidates[index],
                    Type = PickupType.HealthPotion,
                    Quality = quality,
                    Value = PickupItem.GetPotionHealAmount(quality),
                });
            }

            // ── 钥匙散布 ──
            int keyCount = cfg.KeyMin + rng.Next(cfg.KeyMax - cfg.KeyMin + 1);

            // 统计本层门数以执行钥匙约束
            int bronzeDoors = 0, silverDoors = 0, goldDoors = 0;
            foreach (var room in grid.Rooms)
            {
                if (!room.HasDoor) continue;
                switch (room.DoorTier)
                {
                    case DoorTier.Bronze: bronzeDoors++; break;
                    case DoorTier.Silver: silverDoors++; break;
                    case DoorTier.Gold: goldDoors++; break;
                }
            }

            // 确保每种钥匙数 ≥ 对应门数（怪物掉落的钥匙在运行时补充，这里至少保底）
            int bronzeKeys = Mathf.Max(bronzeDoors, Mathf.RoundToInt(keyCount * 0.60f));
            int silverKeys = Mathf.Max(silverDoors, Mathf.RoundToInt(keyCount * 0.30f));
            int goldKeys = Mathf.Max(goldDoors, Mathf.Min(1, Mathf.RoundToInt(keyCount * 0.10f)));

            for (int i = 0; i < bronzeKeys && index < candidates.Count; i++, index++)
            {
                grid.PickupSpawns.Add(new PickupSpawnData
                {
                    Position = candidates[index],
                    Type = PickupType.KeyBronze,
                    Quality = QualityTier.White,
                    Value = 1,
                });
            }
            for (int i = 0; i < silverKeys && index < candidates.Count; i++, index++)
            {
                grid.PickupSpawns.Add(new PickupSpawnData
                {
                    Position = candidates[index],
                    Type = PickupType.KeySilver,
                    Quality = QualityTier.White,
                    Value = 1,
                });
            }
            for (int i = 0; i < goldKeys && index < candidates.Count; i++, index++)
            {
                grid.PickupSpawns.Add(new PickupSpawnData
                {
                    Position = candidates[index],
                    Type = PickupType.KeyGold,
                    Quality = QualityTier.White,
                    Value = 1,
                });
            }

            // ── 金币堆散布 ──
            int goldPileCount = cfg.GoldPileMin + rng.Next(cfg.GoldPileMax - cfg.GoldPileMin + 1);
            for (int i = 0; i < goldPileCount && index < candidates.Count; i++, index++)
            {
                int goldAmount = RollGoldAmount(cfg.FloorLevel, rng);
                grid.PickupSpawns.Add(new PickupSpawnData
                {
                    Position = candidates[index],
                    Type = PickupType.GoldPile,
                    Quality = QualityTier.White,
                    Value = goldAmount,
                });
            }

            // ── 走廊/死胡同宝箱散布（设计文档要求 ~10% 宝箱在走廊中）──
            // 优先选择死胡同位置（仅 1 个可通行邻居 = 死路尽头）
            int roomChestCount = grid.Rooms.Count(r =>
                r.Type == RoomType.Combat || r.Type == RoomType.Treasure || r.Type == RoomType.Boss);
            int corridorChestCount = Mathf.Max(1, roomChestCount / 9);

            // 从剩余候选中筛选死胡同优先
            var deadEnds = new List<Vector2Int>();
            var regularCorridors = new List<Vector2Int>();

            // 优先使用已有候选的剩余位；若全部耗尽则重新扫描全图走廊
            var searchPool = index < candidates.Count
                ? candidates.GetRange(index, candidates.Count - index)
                : candidates; // 保底：复用全部候选（允许与掉落物重叠）

            foreach (var pos in searchPool)
            {
                int walkNeighbors = 0;
                foreach (var (dx, dy) in Dir4)
                    if (grid.IsWalkable(pos.x + dx, pos.y + dy)) walkNeighbors++;
                if (walkNeighbors == 1)
                    deadEnds.Add(pos);
                else
                    regularCorridors.Add(pos);
            }

            // 优先死胡同，不够则补充普通走廊位
            Shuffle(deadEnds, rng);
            Shuffle(regularCorridors, rng);
            for (int i = 0; i < corridorChestCount; i++)
            {
                Vector2Int chestPos;
                if (i < deadEnds.Count)
                    chestPos = deadEnds[i];
                else if (i - deadEnds.Count < regularCorridors.Count)
                    chestPos = regularCorridors[i - deadEnds.Count];
                else
                    break;
                grid.CorridorChestSpawns.Add(chestPos);
            }

            Debug.Log($"[FloorGenerator] 步骤10 走廊散布：" +
                      $"血瓶={potionCount} 钥匙={bronzeKeys}铜/{silverKeys}银/{goldKeys}金 " +
                      $"金币堆={goldPileCount} 走廊宝箱={grid.CorridorChestSpawns.Count} | " +
                      $"门数={bronzeDoors}铜/{silverDoors}银/{goldDoors}金");
        }

        /// <summary>
        /// 根据楼层深度随机决定血瓶品质
        /// 来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md §2.1
        /// </summary>
        private static QualityTier RollPotionQuality(int floorLevel, System.Random rng)
        {
            int roll = rng.Next(100);
            if (floorLevel <= 3)
            {
                // 1~3层：白70% 绿30%
                return roll < 70 ? QualityTier.White : QualityTier.Green;
            }
            else if (floorLevel <= 6)
            {
                // 4~6层：白30% 绿50% 蓝20%
                if (roll < 30) return QualityTier.White;
                if (roll < 80) return QualityTier.Green;
                return QualityTier.Blue;
            }
            else
            {
                // 7~9层：绿40% 蓝40% 紫20%
                if (roll < 40) return QualityTier.Green;
                if (roll < 80) return QualityTier.Blue;
                return QualityTier.Purple;
            }
        }

        /// <summary>
        /// 根据楼层深度随机决定金币堆数量
        /// 来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md §2.3
        /// </summary>
        private static int RollGoldAmount(int floorLevel, System.Random rng)
        {
            if (floorLevel <= 3) return 3 + rng.Next(6);   // 3~8
            if (floorLevel <= 6) return 5 + rng.Next(11);  // 5~15
            return 10 + rng.Next(16); // 10~25
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private static void Shuffle<T>(T[] array, System.Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void Shuffle<T>((T, int, int)[] array, System.Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
