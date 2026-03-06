# 2026-03-06 综合开发日志

> **摘要**：当日核心工作覆盖三大系统：①战斗引擎集成（DOT/Buff/Debuff/疲劳/元素反应管线全闭合）；②符文三选一全链路打通（资产生成→品质抽取→UI面板+暂停）；③技能系统框架从零搭建（数据驱动架构+流浪剑客全套5技能实装）。

## 1. 战斗引擎集成 Phase A1 + A2
- **DOT 引擎 (EntityBase)**：新增 `TickDOT()` 驱动中毒/灼烧/流血三类持续伤害。中毒为固定真伤，灼烧按最大HP 5%/层/秒，流血移动中翻2.5倍。DOT致死立即中断后续结算。灼烧附带治疗衰减（每层10%上限60%）。
- **Buff/Debuff 属性注入**：`RecalculateStats()` 合并 `StatusEffects.GenerateBuffStatBlock()` 到管线层级6，破甲/魔碎/减速/潮湿等 Debuff 真实影响属性面板。
- **怪物攻击附带效果**：`MonsterData_SO` 新增 `OnHitEffect` 结构体（effectType/chance/duration/stacks），Inspector 可编辑。`MonsterBase` 攻击命中后按概率施加，支持免疫检查。
- **疲劳注入**：怪物攻守均注入疲劳倍率+穿甲，使用临时 `StatBlock` 拷贝叠加不污染原始属性。

## 2. 符文三选一系统
- **符文资产批量生成 (`RuneAssetGenerator`)**：编辑器菜单一键生成 84 个 `RuneData_SO`（50属性+26机制+8职业专属），自动挂载到 `RuneManager` 三个池并赋值 `GameBootstrapper` 引用。
- **属性符文五档品质**：10种属性 × 5档品质（凡品65%/稀有25%/罕见6%/史诗3.5%/传说0.5%），两阶段抽取（先roll品质 → 再选类型，防重复）。
- **三选一 UI 面板 (`RuneSelectionPanel`)**：纯代码运行时构建 Canvas，触发时暂停游戏（`Time.timeScale=0`），品质颜色映射（白/绿/蓝/紫/橙）。
- **关键修复**：DontDestroyOnLoad 时序缺陷（`Start()` 被场景加载跳过）→ 移入 Awake 阶段初始化；Canvas 渲染失败 → 改为场景根级对象。

## 3. 技能系统框架 Phase B1
- **SkillExecutor 基类**：模板方法模式，`TryUse()` 统一检查 CD/法力/怒气后调用子类 `OnExecute()`。内置伤害结算（走完整 `DamageCalculator` 链）、吸血、命中回蓝、怒气积攒。
- **SkillTargeting 工具类**：三种 AOE 检测（圆形/扇形/线性穿透），基于 `Physics2D.OverlapCircleNonAlloc` + 阵营过滤。
- **HeroSkillHandler 重写**：数据驱动分发（`HeroClassData_SO.skills` → `SkillExecutor` 子类映射），SO 未配置时降级使用硬编码默认值。
- **流浪剑客 5 技能**：燕返（Space，协程翻滚+墙检）、疾风突刺（Q，突刺穿透+回蓝）、旋风斩（E，2s AOE+霸体）、极刃风暴（R，满怒8段+无敌+吸血）、剑气纵横（被动，叠层+穿透剑气）。
- **关键修复**：Update 执行顺序竞争（`[DefaultExecutionOrder(-100)]`）；位移穿墙（逐格 `IsWall` 检测）；网格漂移（`SnapToGrid()` 同步）；面朝方向（`LastFacingDirection` 记忆）；SkillSlotType 枚举值不匹配；怪物 SO 引用丢失降级。

## 修改文件清单
| 文件 | 修改内容 |
|------|----------|
| `EntityBase.cs` | DOT引擎 + 灼烧衰减 + BuffStatBlock注入管线L6 |
| `GameConstants.cs` | DOT常量 + 品质权重常量 |
| `MonsterBase.cs` | 疲劳注入 + ApplyOnHitEffects + 免疫初始化 |
| `MonsterData_SO.cs` | OnHitEffect结构体 + onHitEffects字段 |
| `StatusEffectManager.cs` | 免疫HashSet + ApplyEffect免疫检查 |
| `HeroController.cs` | Update override + 符文事件订阅 |
| `HeroCombatHandler.cs` | 元素反应预埋 |
| `HeroInputHandler.cs` | LastFacingDirection + DefaultExecutionOrder |
| `HeroSkillHandler.cs` | 数据驱动技能分发（重写） |
| `Enums.cs` | SkillSlotType 新增 Evasion |
| `FloorTransitionManager.cs` | 怪物加载null引用过滤 |
| `Combat/Skills/SkillExecutor.cs` | 新建：技能基类 |
| `Combat/Skills/SkillTargeting.cs` | 新建：AOE碰撞工具 |
| `Combat/Skills/Vagabond/*.cs` | 新建：5个剑客技能执行器 |
| `Editor/SkillAssetGenerator.cs` | 新建：技能SO导出工具 |
| `Editor/RuneAssetGenerator.cs` | 新建：符文SO导出工具 |
| `UI/RuneSelectionPanel.cs` | 新建：符文三选一面板 |
| `Data/RuneManager.cs` | 新建：符文池管理+抽取 |
