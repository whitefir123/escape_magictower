# 2026-03-07 综合开发日志

> **摘要**：当日核心工作覆盖两大系统：①装备锻造+套装共鸣系统实装（铁砧强化核心+7套被动+批量SO生成）；②迷你地图全面修复（双重地图生成根因修复+瓦片级BFS路径绘制+路径持久化去重+FloorData引用追踪重建）。

---

## 一、装备锻造 + 套装共鸣系统

### 1. 铁砧强化系统（ForgeSystem）
- 概率曲线 / 金币公式 / 保护卷轴
- `EquipmentData.cs` 新增 `enhanceLevel` + `ToStatBlock()` 强化乘算

### 2. 套装共鸣系统
- **ISetPassive 接口 + SetPassiveBase 抽象基类**：统一被动生命周期
- **SetResonanceDefinition_SO**：套装 SO 数据容器（词缀匹配 + 参数外置）
- **SetResonanceEngine**：共鸣总调度器（匹配→激活→属性汇总）
- **7 套被动实装**：狂战士之怒、星体观测者、不毁钢屏障、召唤使仪典（空壳）、时光刺客信条、极寒暴君、财阀黑心科技
- **HeroEquipmentManager 集成**：`OnEquipmentChanged` 自动重算共鸣

### 装备系统新增文件

| 文件 | 功能 |
|------|------|
| `ForgeSystem.cs` | 铁砧强化核心（概率曲线 / 金币公式 / 保护卷轴） |
| `ISetPassive.cs` | 套装被动统一接口 |
| `SetPassiveBase.cs` | 被动抽象基类 |
| `SetResonanceDefinition_SO.cs` | 套装 SO 数据容器 |
| `SetResonanceEngine.cs` | 共鸣总调度器 |
| `Passives/BerserkerSetPassive.cs` | #1 狂战士之怒 |
| `Passives/StargazerSetPassive.cs` | #2 星体观测者 |
| `Passives/AegisSetPassive.cs` | #3 不毁钢屏障 |
| `Passives/SummonerSetPassive.cs` | #4 召唤使仪典（空壳） |
| `Passives/ChronoSetPassive.cs` | #5 时光刺客信条 |
| `Passives/GlacialSetPassive.cs` | #6 极寒暴君 |
| `Passives/PlutocratSetPassive.cs` | #7 财阀黑心科技 |
| `Editor/SetResonanceAssetGenerator.cs` | 批量生成 7 套 SO |

### 装备系统修改文件

| 文件 | 变更 |
|------|------|
| `EquipmentData.cs` | 新增 `enhanceLevel` + `ToStatBlock()` 强化乘算 |
| `HeroEquipmentManager.cs` | 集成 `SetResonanceEngine`，`OnEquipmentChanged` 自动重算 |

### 装备系统待测试

| # | 项目 | 预期 |
|---|------|------|
| 1 | 强化 +1~+3 | 100% 成功 |
| 2 | 强化概率衰减 | +4 起开始有失败 |
| 3 | 失败降级 | +4 以上失败降 1 级 |
| 4 | 穿戴含指定词缀的 2 件装备 | Console 输出共鸣激活 |
| 5 | 穿戴 4 件 → 6 件 | 共鸣层级递进升级 |
| 6 | 卸除装备后共鸣件数不足 | Console 输出共鸣失效 |

---

## 二、迷你地图全面修复

### 1. 双重地图生成根因修复

**问题**：`MapManager.StartNewRun()` 和 `FloorTransitionManager.TransitionToNextFloor()` 各自独立调用 `FloorGenerator.Generate()` 使用**不同种子**，产出两套不同的地图。玩家走的是 FTM 的地图，小地图读的是 MapManager 的房间数据 → 坐标完全不匹配。

**修复**：
- `MapManager` 新增 `SyncWithGrid(FloorGrid, int)` 公共方法，接收外部 FloorGrid 并重建 CurrentFloorData
- `FloorTransitionManager.TransitionToNextFloor()` 步骤 7.5 调用 `SyncWithGrid()`，将实际渲染的地图同步给 MapManager
- 两个系统共享同一个 FloorGrid 实例

### 2. 房间连接图修复（BuildRoomConnections）

**问题**：旧算法只检测 `RoomMap` 中物理相邻格子属于不同房间，但房间之间隔着走廊（`RoomMap=0`），导致 `ConnectedRoomIDs` 基本为空。

**修复**：改用入口 BFS 桥接算法 — 从每个房间的 `Entrances` 和边界相邻可通行格出发 BFS，沿走廊格探索，遇到不同房间即记录连接。

### 3. FloorData 引用追踪

**问题**：`SyncWithGrid()` 创建新 FloorData 后，小地图脏检测数值未变，不触发重建，导致出生房方块停留在旧位置。

**修复**：`MinimapUI.Update()` 新增 `_lastFloorData` 引用比较，实例变化时重置脏标记并清除旧路径，强制重建。

### 4. 瓦片级 BFS 路径绘制

**问题**：旧路径是房间级 BFS 找 `ConnectedRoomIDs` 路径，然后在房间**中心之间画直线** — 穿墙且不符合实际走廊走向。

**修复**：
- 新增 `TileBFS(FloorGrid, start, end)` — 在 `FloorGrid.Tiles` 上做四方向 BFS，跳过 Wall 格
- 新增 `SimplifyPath()` — 移除共线中间点，只保留拐弯处
- `DrawTileLevelPath()` 从 SpawnPoint 到玩家当前瓦片位置画实际可行走路径

### 5. 路径持久化 & 去重

- **只追加不清除**：进入新房间时追加路径线段，不删除旧路径，累积显示探索历史
- **出生房保护**：返回出生房时跳过路径绘制，保留所有已有路径
- **房间去重**：`_pathDrawnForRooms` HashSet 记录已绘制路径的房间 ID，同一房间从不同门进入不重复绘制
- **换层清除**：FloorData 变化时统一清除路径线段和去重记录

### 6. PIXELS_PER_TILE 调整

从 `3.5` → `8` → 最终 `5`。可视半径约 18 tiles，平衡远近视野。

### 小地图修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `MapManager.cs` | 新增 `SyncWithGrid()` 方法；重写 `BuildRoomConnections()` 为入口 BFS 桥接 |
| `FloorTransitionManager.cs` | 步骤 7.5 调用 `mapMgr.SyncWithGrid()` |
| `MinimapUI.cs` | FloorData 引用追踪；瓦片级 BFS 路径；路径持久化+去重；PIXELS_PER_TILE=5；诊断日志 |

### 已知遗留

- `BuildRoomConnections` 的 BFS 会找到所有可达房间（传递连接），每个房间连接数偏多。对小地图无影响（已改用瓦片级路径），但如有其他系统依赖 `ConnectedRoomIDs` 表示直接邻居，需后续优化
- 运行时诊断日志（`RunCoordinateDiagnostic`）仍保留，确认功能稳定后可移除
