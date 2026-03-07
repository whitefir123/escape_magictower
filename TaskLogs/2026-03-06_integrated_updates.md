# 2026-03-06 综合开发日志

> **摘要**：当日核心工作覆盖八大系统：①战斗引擎集成（DOT/Buff/Debuff/疲劳/元素反应管线全闭合）；②元素反应系统实装（7种反应规则+感电溅射+致盲Miss）；③符文三选一全链路打通（资产生成→品质抽取→UI面板+暂停）；④技能系统框架从零搭建（数据驱动架构+流浪剑客全套5技能实装）；⑤浮动文字反馈系统（对象池+颜色规范+伤害/治疗/拾取全覆盖）；⑥胜利画面（第9层通关触发）；⑦怪物SO批量导出（2-9层74怪物+8群系BiomeConfig）；⑧楼层怪物池加载修复（FloorTransitionManager按楼层匹配BiomeConfig）。

## 1. 战斗引擎集成 Phase A1 + A2
- **DOT 引擎 (EntityBase)**：新增 `TickDOT()` 驱动中毒/灼烧/流血三类持续伤害。中毒为固定真伤，灼烧按最大HP 5%/层/秒，流血移动中翻2.5倍。DOT致死立即中断后续结算。灼烧附带治疗衰减（每层10%上限60%）。
- **Buff/Debuff 属性注入**：`RecalculateStats()` 合并 `StatusEffects.GenerateBuffStatBlock()` 到管线层级6，破甲/魔碎/减速/潮湿等 Debuff 真实影响属性面板。
- **怪物攻击附带效果**：`MonsterData_SO` 新增 `OnHitEffect` 结构体（effectType/chance/duration/stacks），Inspector 可编辑。`MonsterBase` 攻击命中后按概率施加，支持免疫检查。
- **疲劳注入**：怪物攻守均注入疲劳倍率+穿甲，使用临时 `StatBlock` 拷贝叠加不污染原始属性。

## 2. 元素反应系统实装
- **7 反应规则**：`ElementalReaction.cs` 重写，含 `ExecuteReactionDamage()` + `ApplyAOEDamage()`（阵营过滤）
- **自动触发**：`StatusEffectManager.ApplyEffect()` 内部自动检测反应 + 递归防护 + `StatusEffectToElement()` + 虚弱属性修正
- **感电溅射**：`EntityBase.TickDOT()` 新增 Shock 感电 tick + `TickShockChainDamage()` 溅射逻辑
- **致盲 Miss**：`MonsterBase.PerformAttack()` / `OnHitByPlayer()` 致盲 Miss 检查
- **集成简化**：`HeroCombatHandler.cs` 移除显式 `CheckAndTrigger` 调用，反应统一走 `ApplyEffect()` 内部检测

## 3. 符文三选一系统
- **符文资产批量生成 (`RuneAssetGenerator`)**：编辑器菜单一键生成 84 个 `RuneData_SO`（50属性+26机制+8职业专属），自动挂载到 `RuneManager` 三个池并赋值 `GameBootstrapper` 引用。
- **属性符文五档品质**：10种属性 × 5档品质（凡品65%/稀有25%/罕见6%/史诗3.5%/传说0.5%），两阶段抽取（先roll品质 → 再选类型，防重复）。
- **三选一 UI 面板 (`RuneSelectionPanel`)**：纯代码运行时构建 Canvas，触发时暂停游戏（`Time.timeScale=0`），品质颜色映射（白/绿/蓝/紫/橙）。
- **关键修复**：DontDestroyOnLoad 时序缺陷（`Start()` 被场景加载跳过）→ 移入 Awake 阶段初始化；Canvas 渲染失败 → 改为场景根级对象。

## 4. 技能系统框架 Phase B1
- **SkillExecutor 基类**：模板方法模式，`TryUse()` 统一检查 CD/法力/怒气后调用子类 `OnExecute()`。内置伤害结算（走完整 `DamageCalculator` 链）、吸血、命中回蓝、怒气积攒。
- **SkillTargeting 工具类**：三种 AOE 检测（圆形/扇形/线性穿透），基于 `Physics2D.OverlapCircleNonAlloc` + 阵营过滤。
- **HeroSkillHandler 重写**：数据驱动分发（`HeroClassData_SO.skills` → `SkillExecutor` 子类映射），SO 未配置时降级使用硬编码默认值。
- **流浪剑客 5 技能**：燕返（Space，协程翻滚+墙检）、疾风突刺（Q，突刺穿透+回蓝）、旋风斩（E，2s AOE+霸体）、极刃风暴（R，满怒8段+无敌+吸血）、剑气纵横（被动，叠层+穿透剑气）。
- **关键修复**：Update 执行顺序竞争（`[DefaultExecutionOrder(-100)]`）；位移穿墙（逐格 `IsWall` 检测）；网格漂移（`SnapToGrid()` 同步）；面朝方向（`LastFacingDirection` 记忆）；SkillSlotType 枚举值不匹配；怪物 SO 引用丢失降级。

## 5. 浮动文字反馈系统
- **FloatingTextManager**：纯代码运行时构建，对象池复用。支持 Rise+Fade 动画、暴击弹簧缩放（Spring Scale）。
- **颜色规范**：玩家伤害红色，怪物伤害白色，元素伤害橙色，治疗绿色，法力蓝色，金币金色，经验淡金色，钥匙对应颜色，Miss 灰色，免疫白色。
- **格式规范**：伤害统一 `-N`，治疗 `+N`，金币 `+N 金币`。
- **集成点**：`EntityBase.TakeDamage()`、`EntityBase.Heal()`、`HUDManager.OnItemPickedUp()` 均对接。缓存 `FloatingTextManager` 引用避免 `FindAnyObjectByType`。

## 6. 胜利画面
- **VictoryScreen**：纯代码构建金色主题 UI（复用 `DeathScreen` 布局思路），显示通关统计。
- **触发机制**：`MapManager.AdvanceFloor()` 在第 9 层触发 `OnVictoryEvent`。`EventManager.cs` 新增 `OnVictoryEvent` 结构体。

## 7. 怪物 SO 批量导出（2-9 层）
- **MonsterTag 枚举扩展**：`Enums.cs` 新增 `Insect`/`Plant`/`Mechanical`/`Aquatic`/`Summoner` 五个标签。
- **MonsterAssetGenerator（Editor 菜单工具）**：一键导出 **74 个 MonsterData_SO + 8 个 BiomeConfig_SO**。严格对照 `04_2~04_9` 蓝图数值。紧凑工厂方法 `M()` 减少重复代码。
- **怪物统计**：F2花园9+1、F3齿轮9+1、F4深海8+1、F5马戏团9+1、F6矿洞7+1、F7沙漠7+1、F8雪镇8+1、F9圣域7+1。
- **BiomeConfig 存储路径**：`Assets/Resources/Biomes/`（支持 `Resources.LoadAll` 运行时加载）。

## 8. 楼层怪物池加载修复
- **问题**：`FloorTransitionManager.SpawnMonstersInRooms()` 始终从 `FloorMonsterConfig_SO`（第1层固定配置）读怪物池，2-9层全走降级到 `Floor1MonsterRegistry`。
- **修复**：重写为按 `biomeIndex == CurrentFloorLevel` 从 `Resources` 匹配 `BiomeConfig_SO`，读取 `normalMonsterPool` + `bossData`。仅未匹配时降级。
- **Boss 修复**：不再硬编码 `FallenHeroBossAI`（仅第1层挂载），其余层 Boss 使用 BiomeConfig 的 `bossData`。
- **MapManager 补丁**：新增 `AutoLoadBiomePoolIfEmpty()` 运行时自动加载 BiomeConfig 到随机池。

## 修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `ElementalReaction.cs` | 重写 7 反应规则 + AOE 阵营过滤 |
| `StatusEffectManager.cs` | 自动反应检测 + 递归防护 + 免疫HashSet |
| `EntityBase.cs` | DOT引擎 + 感电溅射 + 灼烧衰减 + BuffStatBlock注入 + 浮动文字对接 |
| `MonsterBase.cs` | 疲劳注入 + 致盲Miss检查 + ApplyOnHitEffects |
| `MonsterData_SO.cs` | OnHitEffect结构体 + onHitEffects字段 |
| `GameConstants.cs` | DOT常量 + 品质权重常量 |
| `HeroController.cs` | Update override + 符文事件订阅 |
| `HeroCombatHandler.cs` | 元素反应预埋 + 移除显式触发调用 |
| `HeroInputHandler.cs` | LastFacingDirection + DefaultExecutionOrder |
| `HeroSkillHandler.cs` | 数据驱动技能分发（重写） |
| `Enums.cs` | SkillSlotType 新增 Evasion + MonsterTag +5 |
| `HUDManager.cs` | 拾取物通知重定向浮动文字 |
| `Map/MapManager.cs` | AdvanceFloor 胜利触发 + AutoLoadBiomePoolIfEmpty |
| `Map/FloorTransitionManager.cs` | SpawnMonstersInRooms 重写 + 怪物加载null引用过滤 |
| `Core/EventManager.cs` | +OnVictoryEvent |
| `Combat/Skills/SkillExecutor.cs` | 新建：技能基类 |
| `Combat/Skills/SkillTargeting.cs` | 新建：AOE碰撞工具 |
| `Combat/Skills/Vagabond/*.cs` | 新建：5个剑客技能执行器 |
| `Editor/SkillAssetGenerator.cs` | 新建：技能SO导出工具 |
| `Editor/RuneAssetGenerator.cs` | 新建：符文SO导出工具 |
| `Editor/MonsterAssetGenerator.cs` | 新建：74怪物+8群系SO导出工具 |
| `UI/RuneSelectionPanel.cs` | 新建：符文三选一面板 |
| `UI/FloatingTextManager.cs` | 新建：浮动文字系统 |
| `UI/VictoryScreen.cs` | 新建：胜利画面 UI |
| `Data/RuneManager.cs` | 新建：符文池管理+抽取 |

## 待手动测试清单

| # | 测试场景 | 预期结果 | 状态 |
|---|---------|---------|------|
| 1 | 怪物受中毒 3 层 | Console 每秒输出 `[DOT] Poison 伤害`，值 = 3×valuePerStack | 🔲 |
| 2 | 怪物受灼烧 | Console 每秒输出 `[DOT] Burn 伤害`，值 = 层数×MaxHP×5% | 🔲 |
| 3 | 怪物受流血后移动 | 流血伤害 ×2.5 | 🔲 |
| 4 | 先给怪物 Poison，再打火 | 触发毒爆 `VenomBlast`，AOE 仅打怪物不打玩家 | 🔲 |
| 5 | 先给怪物 Wet，再 Ice | 触发冻结 `Freeze`，怪物被冰封 3s | 🔲 |
| 6 | 先给怪物 Wet，再 Lightning | 触发感电 `ElectroCharged`，Wet 不被移除 | 🔲 |
| 7 | 怪物身上有 Shock 状态 | 每秒对自身 + 周围同阵营实体造成雷伤 | 🔲 |
| 8 | 给怪物 Blind | 怪物攻击和反击均 Miss | 🔲 |
| 9 | 超载 AOE 范围 | 仅怪物受伤，玩家不受 AOE | 🔲 |
| 10 | 给怪物 Weakened 后攻击 | 怪物 ATK/MATK 降低 | 🔲 |
| 11 | 灼烧中治疗 | 治疗量被灼烧层数削减 | 🔲 |
