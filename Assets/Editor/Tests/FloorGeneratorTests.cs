// ============================================================================
// 逃离魔塔 - FloorGenerator 单元测试
// 测试迷宫生成的核心约束：连通性、房间数量、出入口、门分配
// ============================================================================

using NUnit.Framework;
using EscapeTheTower.Map;

namespace EscapeTheTower.Tests
{
    [TestFixture]
    public class FloorGeneratorTests
    {
        // 所有测试使用固定种子保证可重复
        private const int TEST_SEED = 12345;
        private const int MAP_WIDTH = 51;
        private const int MAP_HEIGHT = 51;

        /// <summary>生成一张默认配置的测试地图</summary>
        private FloorGrid GenerateTestMap(int seed = TEST_SEED, FloorGenConfig config = null)
        {
            return FloorGenerator.Generate(MAP_WIDTH, MAP_HEIGHT, seed, config);
        }

        // =====================================================================
        //  基础生成
        // =====================================================================

        [Test]
        public void Generate_返回非空FloorGrid()
        {
            var grid = GenerateTestMap();
            Assert.IsNotNull(grid);
            Assert.IsNotNull(grid.Tiles);
            Assert.IsNotNull(grid.Rooms);
        }

        [Test]
        public void Generate_地图尺寸正确()
        {
            var grid = GenerateTestMap();
            // 输入 51 是奇数，应原样保持
            Assert.AreEqual(MAP_WIDTH, grid.Width);
            Assert.AreEqual(MAP_HEIGHT, grid.Height);
        }

        [Test]
        public void Generate_偶数尺寸自动修正为奇数()
        {
            var grid = FloorGenerator.Generate(50, 50, TEST_SEED);
            Assert.AreEqual(51, grid.Width, "偶数宽度应被修正为奇数");
            Assert.AreEqual(51, grid.Height, "偶数高度应被修正为奇数");
        }

        // =====================================================================
        //  房间数量
        // =====================================================================

        [Test]
        public void Generate_至少有1个房间()
        {
            var grid = GenerateTestMap();
            Assert.GreaterOrEqual(grid.Rooms.Count, 1, "至少应有 1 个房间");
        }

        [Test]
        public void Generate_不同种子产生不同地图()
        {
            var grid1 = GenerateTestMap(seed: 111);
            var grid2 = GenerateTestMap(seed: 222);

            // 房间数量或出生点应不同（概率极高）
            bool different = grid1.Rooms.Count != grid2.Rooms.Count
                          || grid1.SpawnPoint != grid2.SpawnPoint;
            Assert.IsTrue(different, "不同种子应产生不同地图");
        }

        // =====================================================================
        //  出生点和楼梯
        // =====================================================================

        [Test]
        public void Generate_出生点在地图范围内()
        {
            var grid = GenerateTestMap();
            Assert.IsTrue(grid.InBounds(grid.SpawnPoint.x, grid.SpawnPoint.y),
                "出生点应在地图范围内");
        }

        [Test]
        public void Generate_出生点是可通行格()
        {
            var grid = GenerateTestMap();
            Assert.IsTrue(grid.IsWalkable(grid.SpawnPoint.x, grid.SpawnPoint.y),
                "出生点应是可通行格");
        }

        [Test]
        public void Generate_楼梯在地图范围内()
        {
            var grid = GenerateTestMap();
            Assert.IsTrue(grid.InBounds(grid.StairsPoint.x, grid.StairsPoint.y),
                "楼梯应在地图范围内");
        }

        [Test]
        public void Generate_楼梯是可通行格()
        {
            var grid = GenerateTestMap();
            Assert.IsTrue(grid.IsWalkable(grid.StairsPoint.x, grid.StairsPoint.y),
                "楼梯应是可通行格");
        }

        [Test]
        public void Generate_出生点和楼梯不重合()
        {
            var grid = GenerateTestMap();
            Assert.AreNotEqual(grid.SpawnPoint, grid.StairsPoint,
                "出生点和楼梯不应重合");
        }

        // =====================================================================
        //  连通性（BFS 验证从出生点可达楼梯）
        // =====================================================================

        [Test]
        public void Generate_出生点可达楼梯()
        {
            var grid = GenerateTestMap();

            // BFS 从出生点搜索
            var visited = new bool[grid.Width, grid.Height];
            var queue = new System.Collections.Generic.Queue<UnityEngine.Vector2Int>();
            queue.Enqueue(grid.SpawnPoint);
            visited[grid.SpawnPoint.x, grid.SpawnPoint.y] = true;

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                if (pos == grid.StairsPoint)
                {
                    Assert.Pass("出生点可达楼梯");
                    return;
                }

                for (int d = 0; d < 4; d++)
                {
                    int nx = pos.x + dx[d];
                    int ny = pos.y + dy[d];
                    if (grid.InBounds(nx, ny) && !visited[nx, ny] && grid.IsWalkable(nx, ny))
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new UnityEngine.Vector2Int(nx, ny));
                    }
                }
            }

            Assert.Fail("出生点无法到达楼梯！地图连通性验证失败");
        }

        // =====================================================================
        //  RoomMap 一致性
        // =====================================================================

        [Test]
        public void Generate_RoomMap与Rooms一致()
        {
            var grid = GenerateTestMap();

            // 所有 RoomMap 中的非 0 值应对应一个存在的 RoomData
            var roomIds = new System.Collections.Generic.HashSet<int>();
            foreach (var room in grid.Rooms)
                roomIds.Add(room.RoomID);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    int id = grid.RoomMap[x, y];
                    if (id > 0)
                    {
                        Assert.IsTrue(roomIds.Contains(id),
                            $"RoomMap[{x},{y}]={id} 未在 Rooms 列表中找到");
                    }
                }
            }
        }

        // =====================================================================
        //  多种子稳定性
        // =====================================================================

        [Test]
        public void Generate_100个随机种子全部生成成功()
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 100; i++)
            {
                int seed = rng.Next();
                Assert.DoesNotThrow(() =>
                {
                    var grid = FloorGenerator.Generate(MAP_WIDTH, MAP_HEIGHT, seed);
                    Assert.IsNotNull(grid, $"种子 {seed} 生成失败");
                    Assert.GreaterOrEqual(grid.Rooms.Count, 1, $"种子 {seed} 房间数为 0");
                }, $"种子 {seed} 生成抛出异常");
            }
        }

        // =====================================================================
        //  出生房出口验证
        // =====================================================================

        [Test]
        public void Generate_出生房至少3个出口()
        {
            var grid = GenerateTestMap();
            Assert.GreaterOrEqual(grid.Rooms.Count, 1, "至少应有 1 个房间");
            var spawnRoom = grid.Rooms[0];
            Assert.GreaterOrEqual(spawnRoom.Entrances.Count, 3,
                $"出生房应至少有 3 个出口，实际={spawnRoom.Entrances.Count}");
        }

        [Test]
        public void Generate_出生房出口均为Floor()
        {
            var grid = GenerateTestMap();
            Assert.GreaterOrEqual(grid.Rooms.Count, 1, "至少应有 1 个房间");
            var spawnRoom = grid.Rooms[0];

            foreach (var entrance in spawnRoom.Entrances)
            {
                var tile = grid.Tiles[entrance.x, entrance.y];
                Assert.AreNotEqual(TileType.DoorBronze, tile,
                    $"出生房出口({entrance.x},{entrance.y})不应有铜门");
                Assert.AreNotEqual(TileType.DoorSilver, tile,
                    $"出生房出口({entrance.x},{entrance.y})不应有银门");
                Assert.AreNotEqual(TileType.DoorGold, tile,
                    $"出生房出口({entrance.x},{entrance.y})不应有金门");
            }
        }

        [Test]
        public void Generate_20个随机种子出生房均至少3出口()
        {
            var rng = new System.Random(99);
            for (int i = 0; i < 20; i++)
            {
                int seed = rng.Next();
                var grid = FloorGenerator.Generate(MAP_WIDTH, MAP_HEIGHT, seed);
                if (grid.Rooms.Count == 0) continue; // 极端情况跳过

                var spawnRoom = grid.Rooms[0];
                Assert.GreaterOrEqual(spawnRoom.Entrances.Count, 3,
                    $"种子 {seed}：出生房出口={spawnRoom.Entrances.Count}，应≥3");
            }
        }
    }
}
