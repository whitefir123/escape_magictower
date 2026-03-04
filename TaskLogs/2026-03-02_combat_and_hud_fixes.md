# 战斗系统与 HUD 修复记录

> 初始完成时间：2026-03-02 ~ 2026-03-03
> 最近更新：2026-03-03
> 涉及模块：格子移动、战斗系统、HUD、怪物 AI

## 修改文件总览

| 文件 | 修改内容 |
|------|----------|
| `Assets/Scripts/Core/GridMovement.cs` | 帧驱动输入系统（`SetDirection`）、`AllowBump`、`SetBumpSpeed`、`SetPostKillDelay`（击杀延迟保护）、静态注册表（`OnEnable/OnDisable`）、输入缓冲窗口、匀速移动（`MoveTowards` 替代 `Lerp`）、`LateUpdate` 急停 |
| `Assets/Scripts/Entity/Hero/HeroController.cs` | 反击在 TakeDamage 前执行、攻击冷却绑定 `AttackSpeed`、冷却期间 `AllowBump=false`、死亡禁用 GridMovement、改用 `SetDirection` + 按键栈、移动速度 `MoveSpeed * 8`、回弹速度 `bumpSpeed=12` |
| `Assets/Scripts/Entity/Monster/MonsterBase.cs` | `PursueTarget` 改用 `SetDirection`（持续方向 = 丝滑追击）、`OnGridMoveBlocked` 新增攻击冷却 + `AllowBump` 控制、`OnDeath` 直接 `Destroy(gameObject)`、`OnHitByPlayer` 先 `EngageTarget` 再反击 |
| `Assets/Scripts/UI/HUDManager.cs` | 完全重写：RectTransform 宽度缩放式"框+条"，HP/MP/怒气/经验四条属性条 |
| `Assets/Scripts/Debug/TestSceneSetup.cs` | 自动创建 HUDManager |
| `Assets/Scripts/Entity/Hero/HeroInputHandler.cs` | 按键栈输出 `MoveDirection`（`Vector2Int`，四方向） |

---

## 已完成修复

### 1. 格子移动系统重构
- [x] `GridMovement` 帧驱动输入：`SetDirection` 每帧设置方向（替代原 `RequestMove` 缓存模式）
- [x] `RequestMove` / `ClearPendingDirection` 保留为兼容旧接口
- [x] 匀速移动：`MoveTowards` 替代 `Lerp`，消除缓动尾巴
- [x] 静态注册表：`OnEnable/OnDisable` 管理实例列表，`CheckTileOccupant` 无反射开销
- [x] 输入缓冲窗口：移动中变向时缓冲方向，到达目标格后执行
- [x] `LateUpdate` 急停机制：松手立即停，持续按键丝滑衔接
- [x] 击杀延迟保护：`SetPostKillDelay` 防止击杀后立即走入原怪物位置

### 2. 怪物追击系统
- [x] `MonsterBase.PursueTarget` 改用 `SetDirection`（持续设置方向 = 和玩家按住方向键一样丝滑）
- [x] `OnHitByPlayer` 先调用 `EngageTarget` 再执行反击

### 3. 被动反击（魔塔核心）
- [x] `HeroController.OnGridMoveBlocked` 中 `OnHitByPlayer` 在 `TakeDamage` 之前执行
- [x] 秒杀怪物时也会受到被动反击伤害

### 4. 攻击频率绑定 AttackSpeed
- [x] 玩家：攻击冷却 = `1 / AttackSpeed` 秒，冷却期间 `AllowBump = false`
- [x] 怪物：攻击冷却由 `_attackCooldownTimer` + `_attackInterval` 控制，同样 `AllowBump = false`
- [x] 冷却结束恢复 `AllowBump = true`
- [x] 玩家回弹 `bumpSpeed = 12`（约170ms 往返，快速利落）

### 5. 怪物死亡销毁
- [x] `MonsterBase.OnDeath` 直接 `Destroy(gameObject)`（不依赖 CommandBuffer）
- [x] 死亡 Console 日志：`[战斗] ☠️ xxx 被击杀！`
- [x] 掉落日志：`[掉落] xxx → EXP=10, 金币=5`
- [x] `OnDestroy` 清理 GridMovement 事件订阅

### 6. 玩家死亡停止战斗
- [x] `OnDeath` 禁用 `GridMovement`（`enabled = false`）
- [x] 取消 `OnMoveBlocked` 事件订阅
- [x] `enabled = false` 停止 HeroController 的 Update

### 7. HUD 显示
- [x] 完全重写 `HUDManager`：RectTransform 宽度缩放式"框+条"
- [x] HP 条（绿色，低血变红）、MP 条（蓝色）、怒气条（橙/金）、经验条（黄色）
- [x] `TestSceneSetup` 自动创建 HUDManager

### 8. 移动手感优化
- [x] `GridMovement` 用 `LateUpdate` 处理 Idle 链式移动
- [x] 持续按键 → 丝滑衔接（无帧间延迟）
- [x] 松手 → 急停（不多走一格）
- [x] 快速变向 → 即时响应
- [x] 玩家输入改用按键栈输出 `Vector2Int`，直接传递给 `SetDirection`

---

## 未完成 / 待办

- [x] Boss 专属 AI（`FallenHeroBossAI` 已启用，格子移动驱动冲锋/大风车）
- [x] 墙壁碰撞（`TilemapCollisionProvider` 接入，测试围墙已注册）
- [x] 死亡 UI / 结算界面（`DeathScreen` 纯代码构建，显示等级/金币/存活时间）
- [ ] 点击移动功能（重构时移除，待决定是否恢复）

---

## 2026-03-03 新增修复

### 修改文件总览（本日）

| 文件 | 修改内容 |
|------|----------|
| `Assets/Scripts/Map/TilemapCollisionProvider.cs` | **新建**：单例碰撞查询（Tilemap + HashSet 双后端） |
| `Assets/Scripts/UI/DeathScreen.cs` | **新建**：纯代码死亡结算 UI（等级/金币/存活时间 + 重新开始） |
| `Assets/Scripts/Core/GridMovement.cs` | `IsTileWall` 接入 TilemapCollisionProvider；新增 `BumpFilter` 碰撞过滤器；新增 `IsTilePassable` 公共查询 |
| `Assets/Scripts/Entity/Monster/MonsterBase.cs` | 新增 `OverridePursuitDirection` + `ExternalAttackControl`；设置 `BumpFilter` 阵营过滤；`PursueTarget` 单步前瞻绕路 |
| `Assets/Scripts/Entity/Monster/FallenHeroBossAI.cs` | **重写**：格子移动驱动冲锋/大风车，通过 `OverridePursuitDirection` 控制方向 |
| `Assets/Scripts/Entity/Hero/HeroController.cs` | `OnDeath` 触发 `DeathScreen` |
| `Assets/Scripts/Debug/TestSceneSetup.cs` | `DeployWalls()` 注册 -8~8 围墙 |

### 9. 墙壁碰撞 + 地图系统接入
- [x] 新建 `TilemapCollisionProvider.cs`：单例碰撞查询提供器（Tilemap + HashSet 双后端）
- [x] `GridMovement.IsTileWall` 改为查询 `TilemapCollisionProvider.Instance.IsWall(pos)`
- [x] `TestSceneSetup.DeployWalls` 注册 -8~8 范围四面围墙

### 10. Boss 专属 AI（FallenHeroBossAI）
- [x] `FallenHeroBossAI` 完全重写为格子移动驱动：
  - 追击：由 `MonsterBase.PursueTarget` 统一执行（保证执行时序）
  - 冲锋：`SetMoveSpeed(20)` 临时加速 + `OverridePursuitDirection` 锁定方向 + `OnMoveBlocked` 命中判定
  - 大风车：`SetMoveSpeed(2)` 缓速追击 + 每0.3秒 AOE tick + Z轴旋转视觉
  - 击退：使用 `GridMovement.SetGridPosition` 格子级位移
- [x] `MonsterBase` 新增 `OverridePursuitDirection`（方向覆盖）+ `ExternalAttackControl`（攻击接管）

### 11. 死亡 UI / 结算界面
- [x] 新建 `DeathScreen.cs`：纯代码构建死亡结算 UI
  - 全屏半透明黑幕 + 红色边框中央面板
  - 显示等级、金币、存活时间
  - "重新开始"按钮（`SceneManager.LoadScene`）
  - 0.5秒淡入协程（`CanvasGroup.alpha` + `unscaledDeltaTime`）
- [x] `HeroController.OnDeath` 中自动创建并显示 `DeathScreen`

### 12. Bug 修复：Boss 移动时序问题
- [x] 根因：组件执行顺序（MonsterBase → GridMovement → FallenHeroBossAI），`SetDirection` 在 `GridMovement.Update` 之后被调用，被 `LateUpdate` 清除
- [x] 修复：Boss AI 不再直接调用 `SetDirection`，改为通过 `MonsterBase.OverridePursuitDirection` 由 `PursueTarget` 在正确时序执行

### 13. Bug 修复：Boss 攻击无伤害 + 动画抽搐
- [x] 根因：`ExternalAttackControl = true` 被永久设置，跳过 `OnGridMoveBlocked` → 无伤害、`AllowBump` 永不关闭 → 每帧回弹抽搐
- [x] 修复：`ExternalAttackControl` 仅在冲锋期间临时启用，正常碰撞攻击由 MonsterBase 处理

### 14. Bug 修复：怪物间互碰抽搐 + Boss 追击绕路
- [x] `GridMovement` 新增 `BumpFilter`（`Func<GameObject, bool>`）碰撞过滤器
- [x] `MonsterBase` 设置 `BumpFilter`：仅对非同阵营实体回弹，同阵营视为墙壁
- [x] `GridMovement` 新增 `IsTilePassable` 公共查询方法
- [x] `MonsterBase.PursueTarget` 添加单步前瞻绕路：主方向被堵时尝试次方向/垂直方向绕行
