# 2026-03-04 房间结构 / 怪物AI / 宝箱逻辑 / 走廊优化

## 概述

本轮修复了地图生成系统的多项核心问题，涵盖房间墙壁完整性、怪物AI行为约束、宝箱交互逻辑、走廊美观性优化等。

---

## 已完成任务

### 1. 房间最多 2 个出入口
- **文件**: `FloorGenerator.cs` → `ConnectRooms`
- **改动**: `rng.Next(3)` → `rng.Next(2)`，普通房间开口从 1~3 个限制为 1~2 个

### 2. 出生房间不生成怪物
- **文件**: `FloorTransitionManager.cs` → `SpawnMonstersInRooms`
- **改动**: 跳过 `room.Center == CurrentFloorGrid.SpawnPoint` 的房间

### 3. 怪物巡逻范围约束
- **文件**: `MonsterBase.cs`
- **新增字段**: `PatrolOrigin`（巡逻圆心）、`PatrolRadius`（巡逻半径，默认 3 格）
- **改动**: `UpdatePatrol` 中增加曼哈顿距离检查，超出范围放弃移动
- **文件**: `FloorTransitionManager.cs`
- **改动**: `SpawnMonstersInRooms` 中为每个怪物设置 `PatrolOrigin = room.Center`

### 4. Boss 在楼梯旁游荡
- **文件**: `FloorTransitionManager.cs`
- **改动**: Boss 生成位置改为 `CurrentFloorGrid.StairsPoint`，`PatrolOrigin = stairsPos`，`PatrolRadius = 4`

### 5. 宝箱守护锁逻辑
- **文件**: `FloorTransitionManager.cs` → `SpawnChestsInRooms`
- **改动**: 检查 `RoomTracker.GetRemainingMonsters(roomID) > 0`，有怪物 → roomID 传入（Locked），无怪物 → roomID=0（Unlocked 直接可开）

### 6. 房间墙壁 1 格厚（迷宫网格对齐）
- **文件**: `FloorGenerator.cs` → `PlaceRooms`
- **改动**: 强制 `w |= 1; h |= 1; x |= 1; y |= 1` — 房间尺寸和位置均为奇数
- **原理**: 迷宫步长-2在奇数位雕刻走廊。房间对齐后，墙壁落在偶数位，走廊可抵达偶数位外侧 → 精确 1 格墙厚

### 7. IsAdjacentToRoom 8 方向保护
- **文件**: `FloorGenerator.cs`
- **改动**: `IsAdjacentToRoom` 从 4 方向改为 8 方向（含对角线），保护房间墙角不被迷宫凿穿
- **影响范围**: `CarveIterative`（validDirs + 中间格检查）、`WidenCorridors`、`CollectBridgeWalls`

### 8. EnsureMultiplePaths 连接器节流
- **文件**: `FloorGenerator.cs` → `EnsureMultiplePaths` Phase A
- **改动**:
  - 每轮只开 **1 个房间**的 **1 个口**（原来每轮对每个房间都开 1 个）
  - 入口 ≥ 3 的房间跳过
  - 迭代次数从 30 降为 20

### 9. RebuildRoomWalls 终极安全网
- **文件**: `FloorGenerator.cs`（新增方法）
- **功能**: 在 `AssignDoors` 之后，扫描每个房间的墙壁环（Bounds 外扩 1 格），将非入口的格子强制恢复为 `Wall`
- **调用位置**: `Generate()` 管线步骤 9.5

### 10. 走廊孤立墙柱清理
- **文件**: `FloorGenerator.cs` → `CleanIsolated`
- **改动**: 新增快照式批量清除 — 先收集所有 4 面被包围的 Wall 格（`walkable == 4`），然后一次性移除
- **安全保障**: `IsAdjacentToRoom`（8方向）保护房间墙壁不被清除
- **阈值**: `== 4`（仅完全包围的孤柱），避免 `≥ 3` 导致的级联效应

### 11. 走廊拓宽概率调整
- **文件**: `FloorGenerator.cs` → `FloorGenConfig`
- **改动**: `CorridorWidenChance` 从 0.25 降为 **0.12**，保留迷宫结构感

### 12. 走廊/死胡同宝箱生成
- **设计文档**: `06_Map_and_Modes.md` L82-83 — 10% 宝箱在走廊/死胡同中，无锁直开
- **文件**: `FloorGenerator.cs`
  - `FloorGrid` 新增 `CorridorChestSpawns` 列表
  - `ScatterCorridorDrops` 末尾：优先死胡同（仅 1 个可通行邻居），数量 ≈ 房间宝箱数 / 9
  - 保底机制：候选位耗尽时复用全部候选列表
- **文件**: `FloorTransitionManager.cs` → `SpawnChestsInRooms` 末尾
  - 遍历 `CorridorChestSpawns`，生成 `ChestEntity`（roomID=0 → Unlocked）

### 13. EventManager 重复定义修复
- **文件**: `EventManager.cs`
- **改动**: 移除重复的 `OnFloorEnterEvent` struct 定义

---

## 涉及文件汇总

| 文件 | 改动类型 |
|------|----------|
| `Assets/Scripts/Map/FloorGenerator.cs` | 房间对齐、墙壁保护、走廊优化、宝箱散布 |
| `Assets/Scripts/Map/FloorTransitionManager.cs` | 怪物/宝箱生成逻辑、走廊宝箱实体 |
| `Assets/Scripts/Entity/Monster/MonsterBase.cs` | 巡逻范围约束 |
| `Assets/Scripts/Core/EventManager.cs` | 去重复定义 |

---

## 设计决策记录

1. **奇数对齐方案** — 选择强制房间坐标/尺寸为奇数，利用迷宫步长-2的天然网格对齐，比保护环（RoomMap 负值标记）更简洁
2. **IsAdjacentToRoom 8方向** — 比 RoomMap 负值保护环更轻量，且自动覆盖墙角
3. **RebuildRoomWalls 安全网** — 即使前面步骤有遗漏，此步骤也能保证房间墙壁完整
4. **快照式孤柱清理** — 避免原地修改导致级联清除全部走廊墙壁
5. **阈值 ==4** — 仅移除四面包围的真·孤柱，≥3 会破坏 T/L 型走廊结构
