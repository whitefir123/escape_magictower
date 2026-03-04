# 2026-03-03 地图生成系统开发日志

> 本文档记录地图生成系统第一阶段的全部开发工作，供后续开发者参考。

---

## 一、设计文档更新

### 完成项
- **完全重写 `DesignDocs/06_Map_and_Modes.md`**，整合用户确认的地图设计：
  - 大型迷宫式格子地图（50~70 格），摄像机跟随玩家
  - 走廊宽度 1~3 格灵活变化
  - 金/银/铜钥匙门系统（60% 有门 / 40% 无门）
  - 怪物**被动巡逻制**（不主动攻击，玩家碰撞触发战斗）**← 重大设计变更，当前代码仍为追击 AI，待第三阶段修改**
  - 宝箱通关锁（清除房间怪物后才能打开）
  - 战争迷雾可选（开门前看不到房间内部，设置中可关闭）
  - Boss 被动 + 击败后楼梯解锁
  - 地图初始化掉落（血瓶/钥匙/金币散落走廊）

---

## 二、新增文件

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/Map/FloorGenerator.cs` | 程序化楼层地图生成器 v3（详见下方算法说明） |
| `Assets/Scripts/Map/FloorRenderer.cs` | FloorGrid → Unity Tilemap 双层渲染 + 碰撞注册 |
| `Assets/Scripts/Core/CameraFollow.cs` | 摄像机 LateUpdate 平滑跟随 + 地图边界限制 |

## 三、修改文件

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Core/Enums.cs` | 新增 `TileType`（8 种格子类型）、`DoorTier`（铜/银/金门）枚举 |
| `Assets/Scripts/Debug/TestSceneSetup.cs` | 重写：调用 FloorGenerator → FloorRenderer → 玩家出生 → 怪物分配到房间 → 摄像机跟随 |

---

## 四、FloorGenerator v3 — 算法设计

### 生成管线（9 步严格执行顺序）

```
1. 初始化网格 → 全部填充 WALL
2. 随机放置房间 → 碰撞检测，房间间≥1格间隙，≤12个
3. 提前标记安全房间 → Boss/宝箱/出生房（IsSecure=true）
4. 迭代式回溯迷宫雕刻 → 步长2，显式栈（防栈溢出）
5. 走廊加宽后处理 → 25%概率拓宽相邻墙至2~3格
6. 房间连接器 → 普通房间开1~3口=FLOOR，安全房间开1口=DOOR
7. 放置出生点和楼梯 → BFS路径距离最远的两个房间
8. 多路径验证 → BFS顶点不相交≥3条独立路径
9. 分配门等级 → Boss房金门，宝箱房银/铜门，普通房间60%有门
```

### 关键数据结构

- `FloorGrid`：地图数据输出（`TileType[,] Tiles`、`int[,] RoomMap`、`bool StairsLocked`）
- `RoomData`：房间数据（`Bounds`、`Type`、`IsSecure`、`DoorTier`、`Entrances`）
- `FloorGenConfig`：生成参数类（已预留由 `BiomeConfig_SO` 驱动的接口）

### 参考文档

`TaskLogs/map_generation_logic.md.resolved` — 网页版 Magic Tower Wars 的地图生成逻辑深度解析，是本实现的主要参考来源。

---

## 五、已修复的 Bug（代码审查）

### P0（必须修复）
| Bug | 根因 | 修复方式 |
|-----|------|---------|
| 栈溢出 | `CarveRecursive` 纯递归，大地图递归深度超限 | 改为 `CarveIterative` 显式 `Stack<Vector2Int>` |
| DOOR 封锁失效 | `IsSecure` 在连接器步骤时还没被赋值 | 新增步骤 3 `PreAssignSecureRooms` 提前标记 |
| 小地图崩溃 | 房间尺寸 ≥ 地图尺寸时 `rng.Next` 参数为负 | 添加 `xRange/yRange <= 0` 保护 |

### P1（重要修复）
| Bug | 根因 | 修复方式 |
|-----|------|---------|
| 出生/楼梯太近 | 用欧几里得直线距离而非实际路径距离 | 改用 `BFSDistanceMap` 路径距离 |
| 强制连接不通 | 只打通外侧 1 格墙，可能连不到走廊 | 向外持续挖 3 格直到遇到走廊 |
| 走廊固定 1 格宽 | 递归回溯步长=2 只产生 1 格走廊 | 新增 `WidenCorridors` 后处理（25% 拓宽） |
| 出生房不安全 | 未标记，后续可能生成怪物 | `rooms[0].IsSecure = true` |
| 坐标混淆 | `EnsureMultiplePaths` 双重 `rng.Next` 导致 x/y 取自不同候选 | 先选完整坐标再使用 |

### P2（优化）
| 项目 | 修复方式 |
|------|---------|
| 内存泄漏 | `FloorRenderer.OnDestroy` 释放缓存纹理 |
| 性能瓶颈 | `CollectBridgeWalls` 缓存 + Shuffle 替代逐次全图扫描 |
| 增量更新 | `FloorRenderer.UpdateTile()` 支持单格刷新 |

---

## 六、Tilemap 对齐说明

GridMovement 将实体放在整数世界坐标 `(x, y)`。Tilemap 需要 tile 视觉中心也在 `(x, y)`。

**解决方案**：`Tilemap.tileAnchor = Vector3.zero`（而非默认的 `0.5, 0.5`），使 tile 的 sprite center（pivot 0.5,0.5）对齐到 cell origin。

---

## 七、代码质量审计与重构（同日完成）

### 7.1 安全修复（11 项 · 9 个文件）

| # | 修复 | 维度 | 涉及文件 |
|---|------|------|---------|
| 1 | RuneManager SO 资产隔离（运行时使用克隆副本防污染原始数据） | 安全性 | `RuneManager.cs` |
| 2 | EventManager 事件双重发布消除 | 可靠性 | `EventManager.cs` |
| 3 | HUDManager 每帧 `Find` 改为 Awake 缓存引用 | 性能 | `HUDManager.cs` |
| 4 | 删除空方法 `TryAttack`（死代码） | 简洁性 | `HeroController.cs` |
| 5 | `FloorGrid.GetRoomAt` 线性搜索改 O(1) 字典查找 | 性能 | `FloorGenerator.cs` |
| 6 | `GridMovement` 静态字段跨场景污染清理（`OnDestroy` 重置） | 可靠性 | `GridMovement.cs` |
| 7 | 怪物占位 Sprite 共享（`CreatePlaceholderSprite` 只创建一次） | 内存 | `MonsterBase.cs` |
| 8 | `EventManager` 静态字典跨场景清理 | 可靠性 | `EventManager.cs` |
| 9 | `SaveSystem` 原子写入（先写临时文件再 `File.Move` 替换） | 安全性 | `SaveSystem.cs` |
| 10 | Boss `FindPlayer` 每帧 `FindObjectOfType` 改为缓存 | 性能 | `FallenHeroBossAI.cs` |
| 11 | 符文抽取改用 Fisher-Yates 洗牌避免重复 LINQ | 简洁性 | `RuneManager.cs` |

### 7.2 大型重构 1：HeroController 拆分

**目标**：733 行的巨型 `HeroController` 拆分为 4 个单一职责组件。

| 文件 | 职责 | 行数 |
|------|------|:----:|
| `Assets/Scripts/Entity/Hero/HeroController.cs` | 编排器 + 门面 API（保持原有公共 API 不变） | ~310 |
| `Assets/Scripts/Entity/Hero/HeroSkillHandler.cs` | **[新建]** 4 个主动技能 + 被动剑道系统 + 冷却管理 | ~285 |
| `Assets/Scripts/Entity/Hero/HeroInventory.cs` | **[新建]** 金币/钥匙/经验/升级 | ~167 |
| `Assets/Scripts/Entity/Hero/HeroCombatHandler.cs` | **[新建]** 碰撞战斗/门交互/攻击冷却 | ~171 |

**设计决策**：采用门面模式，`HeroController` 保留所有原有公共方法签名，内部委托给子组件。外部依赖（`DoorInteraction`、`HUDManager`、`MonsterBase` 等）零修改。HeroController 类添加了 `[RequireComponent]` 属性，Unity 编辑器自动挂载三个子组件。

### 7.3 大型重构 2：GameConstants → ScriptableObject 可配置化

**目标**：将硬编码的游戏平衡常量迁移到 SO，方便策划调参。

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/Data/SO/BalanceConfig_SO.cs` | **[新建]** ~35 个可调参数，带 `[Header]`/`[Tooltip]`/`[Range]` |
| `Assets/Scripts/Core/GameConstants.cs` | **[重写]** 系统硬约束保留为 `const`，平衡参数改为静态属性从 SO 读取 |

**设计决策**：混合方案 — `MAX_DAMAGE_REDUCTION` 等系统不变量保持 `const`；`RUNE_DROP_CHANCE` 等平衡值改为 `static` 属性 + SO 回退默认值。所有 9 个消费方文件零修改。

### 7.4 大型重构 3：MapManager / FloorGenerator 统一

**问题**：`MapManager` 内部手动分配房间类型（权重计算、蛇形布局、线性连接 ~100 行），与 `FloorGenerator` 独立的房间生成逻辑完全重复。

**解决方案**：删除 `MapManager.GenerateFloor` 中的冗余逻辑（`CreateRoom`、`GetNextGridPosition`、`ConnectRoomsLinear`），改为：
1. 调用 `FloorGenerator.Generate(width, height, seed, config)` 获取 `FloorGrid`
2. 从 `FloorGrid.Rooms` 构建 `FloorData`/`RoomNode`（`RoomData.Center` → `RoomNode.GridPosition`）
3. 新增 `BuildRoomConnections()` 基于物理连通性构建房间邻接图
4. 新增 `CurrentGrid` 公共属性暴露给渲染器/碰撞系统

### 7.5 GameBootstrap 初始化器

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/Core/GameBootstrap.cs` | **[新建]** `[DefaultExecutionOrder(-1000)]` + `DontDestroyOnLoad` 单例 |

**用法**：首场景空 GameObject → Add Component → GameBootstrap → 拖入 BalanceConfig SO。自动调用 `GameConstants.Initialize(config)`。未配置时使用默认值，不影响运行。

### 7.6 MonsterSpawner 坐标适配

MapManager 统一后，`RoomNode.GridPosition` 从抽象网格坐标变为实际瓦片坐标（`RoomData.Center`）。修复 `MonsterSpawner` 中两处 `×5f` 乘法：
- `SpawnBoss`：`room.GridPosition.x * 5f` → `room.GridPosition.x`
- `GetSpawnPosition`：`roomGridPos.x * 5f` → `roomGridPos.x`

### 7.7 SaveSystem 版本迁移管线

| 改动 | 说明 |
|------|------|
| `CURRENT_SAVE_VERSION` 常量 | 每次修改 `SaveData` 结构时递增 |
| `MigrateSaveData(SaveData)` 方法 | 按版本号逐步升级（v1→v2→v3...），含示例注释 |
| `Load()` 增强 | 旧版存档自动迁移 + 回写；高版本存档警告 |

### 7.8 自动化测试（41 个 EditMode 用例）

| 文件 | 测试目标 | 用例数 |
|------|---------|:------:|
| `Assets/Editor/Tests/StatBlockTests.cs` | StatBlock 值对象（Get/Set/Add/Multiply/MergeAdd/Clone/Reset/Has） | 14 |
| `Assets/Editor/Tests/DamageCalculatorTests.cs` | 伤害结算链路（物理/魔法/防御减伤/伤害下限/经验曲线） | 6 |
| `Assets/Editor/Tests/AttributePipelineTests.cs` | 六层属性管线（基础值/符文/Buff/跨界协同/钳制/空输入） | 7 |
| `Assets/Editor/Tests/FloorGeneratorTests.cs` | 迷宫生成（连通性 BFS/房间数/出入口/RoomMap 一致性/100 种子稳定性） | 14 |

**运行方式**：Unity → Window → General → Test Runner → EditMode → Run All

---

## 八、第二阶段：门/钥匙/宝箱/掉落物系统 ✅

> 本阶段于同日由 AI 助手实现，用户确认后进入第三阶段。

### 8.1 设计文档与蓝图更新

| 文件 | 改动 |
|------|------|
| `GameData_Blueprints/09_Consumable_and_Drop_Items.md` | **[新建]** 七彩品质消耗品蓝图：血瓶固定回复值（白20→彩虹500）、走廊散布规则、宝箱奖励池、怪物掉落 |
| `DesignDocs/06_Map_and_Modes.md` | §4.6 地图掉落：引用消耗品品质表，说明走廊散布机制 |
| `DesignDocs/08_Economy_and_Loot.md` | §2.4 消耗品品质体系交叉引用 |

### 8.2 新增文件（5 个脚本）

| 文件 | 职责 |
|------|------|
| `Assets/Scripts/Map/DoorInteraction.cs` | 门交互单例：检查钥匙→消耗→修改 FloorGrid Tile→渲染更新→碰撞移除→广播事件 |
| `Assets/Scripts/Map/PickupManager.cs` | 拾取物管理器单例：注册/查询/批量生成走廊掉落物 |
| `Assets/Scripts/Entity/PickupItem.cs` | 拾取物实体组件：7 档药水品质数值、品质颜色、DisplayName、OnPickedUp 事件 |
| `Assets/Scripts/Entity/ChestEntity.cs` | 宝箱实体：订阅 OnRoomClearedEvent 自动解锁，TryOpen 交互接口 |
| `Assets/Scripts/Map/RoomTracker.cs` | 房间追踪单例：注册怪物→订阅死亡事件→怪物计数→广播 OnRoomClearedEvent |

### 8.3 修改文件（8 个脚本）

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Core/Enums.cs` | +`PickupType`（6 种拾取物）+`ChestState`（3 态）+重复 `QualityTier` 修复 |
| `Assets/Scripts/Core/EventManager.cs` | +4 新事件（`OnDoorOpenedEvent`/`OnItemPickedUpEvent`/`OnChestOpenedEvent`/`OnRoomClearedEvent`）+`using UnityEngine` |
| `Assets/Scripts/Core/GridMovement.cs` | +`OnWallBlocked` 事件（门检测：门注册为墙壁后，IsTileWall 拦截导致 OnMoveBlocked 不触发，需独立事件通知） |
| `Assets/Scripts/Map/TilemapCollisionProvider.cs` | +`UnregisterWall(Vector2Int)` |
| `Assets/Scripts/Map/FloorGenerator.cs` | +`PickupSpawnData` 结构 +`FloorGrid.PickupSpawns` +步骤 10 `ScatterCorridorDrops`（血瓶品质 Roll + 钥匙≥门约束 + 金币堆）+`FloorGenConfig` 掉落参数 +Boss 房独立尺寸（7~10） |
| `Assets/Scripts/Map/FloorRenderer.cs` | 门渲染在墙壁层 + 门碰撞注册 + `UpdateTile` 正确调用 `UnregisterWall` |
| `Assets/Scripts/Entity/Hero/HeroController.cs` | +钥匙持有量（铜/银/金）+ `AddKey`/`ConsumeKey` + `OnArrivedAtTile` 拾取交互 + `OnWallBlocked` 开门 |
| `Assets/Scripts/Debug/TestSceneSetup.cs` | 整合全部子系统：初始化 DoorInteraction/PickupManager/RoomTracker → 生成掉落物 → 生成宝箱 → RoomTracker 注册怪物 → 楼梯锁定 + Boss 击败解锁 |

### 8.4 关键交互流程

```
玩家 WASD 移动
├─ 目标格有实体 → OnMoveBlocked → 宝箱 TryOpen / 怪物碰撞战斗
├─ 目标格是墙壁/门 → OnWallBlocked → DoorInteraction.TryOpenDoor（检查钥匙）
└─ 目标格可通行 → OnArrivedAtTile → PickupManager 查询拾取物 → 自动效果
```

### 8.5 走廊散布算法（ScatterCorridorDrops）

- 收集走廊 Floor 格（排除出生点/楼梯 3 格范围）
- Fisher-Yates 洗牌后线性分配
- 血瓶品质按楼层深度概率 Roll（1~3 层 白70%绿30% / 4~6 层 白30%绿50%蓝20% / 7~9 层 绿40%蓝40%紫20%）
- 钥匙约束：铜钥匙 ≥ 铜门数，银钥匙 ≥ 银门数，金钥匙 ≥ 金门数
- 金币堆量按楼层递增（1~3 层 3~8 / 4~6 层 5~15 / 7~9 层 10~25）

---

## 九、第三阶段：怪物被动化 ✅

> 战争迷雾系统因复杂度较高，推迟至后续阶段单独实现。

### 9.1 MonsterBase 改动

| 改动 | 说明 |
|------|------|
| +`Patrolling` 枚举值 | 被动巡逻状态（介于 Idle 和 Pursuing 之间） |
| 默认 AI → `Patrolling` | 怪物不再一出生就追击，改为随机游走 |
| +`UpdatePatrol()` | 每 1.5~3.0 秒随机选一个方向移动一格 |
| +`Provoke(Transform)` | 显式激怒 API（Patrolling/Idle → Pursuing） |
| `EngageTarget()` | 已有方法，OnHitByPlayer 调用后自然切换为追击 |

### 9.2 FallenHeroBossAI 改动

| 改动 | 说明 |
|------|------|
| `FindPlayer()` | 只缓存玩家引用，不再调用 `EngageTarget` |
| `Update()` | 移除强制 `EngageTarget`，未被激怒时 `return`（冲锋/大风车不触发） |
| 结果 | Boss 默认巡逻，玩家主动碰撞后才进入追击+技能循环 |

### 9.3 楼梯锁定

- `TestSceneSetup.LockStaircase()`：楼梯初始注册为碰撞墙壁
- 订阅 `OnRoomClearedEvent`，Boss 房清除后解锁楼梯（`UnregisterWall`）

---

## 十、房间尺寸调整 ✅

| 配置 | 旧值 | 新值 |
|------|:----:|:----:|
| `MinRoomSize` | 7 | 5 |
| `MaxRoomSize` | 14 | 8 |
| `BossRoomMinSize` | — | 7 |
| `BossRoomMaxSize` | — | 10 |

Boss 房间在 `PlaceRooms` 中判定为最后一个放置的房间（`placed == TargetRoomCount - 1`），使用独立的尺寸范围。

---

## 十一、用户自行完成的重构（同日）

> 以下改动由用户在 Unity 编辑器中直接完成，非 AI 助手操作，此处记录以便后续对话了解项目全貌。

### 11.1 HeroController 拆分为 4 组件

- `HeroController.cs` — 编排器 + 门面 API
- `HeroSkillHandler.cs` — **[新建]** 技能冷却 + 被动剑道
- `HeroInventory.cs` — **[新建]** 金币/钥匙/经验/升级
- `HeroCombatHandler.cs` — **[新建]** 碰撞战斗/门交互/攻击冷却

### 11.2 其他优化

| 改动 | 说明 |
|------|------|
| `FloorGrid.BuildRoomLookup()` | RoomID→RoomData 字典 O(1) 查找 |
| `GridMovement.ResetStaticState()` | `[RuntimeInitializeOnLoadMethod]` 防 Domain Reload 残留 |
| `EventManager.ResetStaticState()` | 同上，清空静态事件表 |
| `MonsterBase` 共享占位 Sprite | 所有怪物复用同一份程序化纹理 |
| `MonsterBase.TryAttack()` | 移除空方法（攻击由 OnGridMoveBlocked 回调处理） |
| `FallenHeroBossAI.FindPlayer()` | 缓存守护（`_playerTarget != null` 时直接返回） |

---

## 十二、待办事项（后续阶段）

### 第四阶段：战争迷雾
- [ ] Tilemap 遮罩层 + 递归阴影投射 FOV
- [ ] 开门前隐藏房间内部
- [ ] 设置中可关闭

### 架构对接
- [x] `FloorGenConfig` 与 `BiomeConfig_SO` 整合 ← **已通过 MapManager 统一完成**
- [x] `MapManager` 重构为楼层管理器 ← **已完成，委托 FloorGenerator**

