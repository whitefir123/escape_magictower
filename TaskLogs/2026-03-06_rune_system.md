# 2026-03-06 符文三选一系统闭环

> **摘要**：从零打通符文三选一全链路（数据资产生成→事件管线→品质权重抽取→UI面板弹出+暂停），修复了 DontDestroyOnLoad 对象 Start() 被场景加载跳过的核心时序缺陷。

## 核心功能实现
- **符文资产批量生成 (`RuneAssetGenerator`)**：编辑器菜单一键生成 84 个 `RuneData_SO`（50属性+26机制+8职业专属），自动挂载到 `RuneManager` 三个池并赋值 `GameBootstrapper` 引用。
- **属性符文五档品质**：更新蓝图 `08_Destiny_Rune_System.md`，10种属性 × 5档品质（凡品65%/稀有25%/罕见6%/史诗3.5%/传说0.5%），两阶段抽取（先roll品质 → 再选类型，防重复）。品质权重同步至 `GameConstants.cs`。
- **符文选择管线**：`EventManager.OnRuneDraftCompleteEvent` 携带 `SelectedRune` 数据；`HeroController` 订阅事件将属性注入管线 L4 层。
- **三选一 UI 面板 (`RuneSelectionPanel`)**：纯代码运行时构建 Canvas（ScreenSpaceOverlay 根级对象 + 独立 DontDestroyOnLoad），触发时暂停游戏（`Time.timeScale=0`），品质颜色映射（白/绿/蓝/紫/橙）。

## 关键 Bug 修复
- **DontDestroyOnLoad 时序缺陷**：`GameBootstrapper.Start()` 中的场景加载导致同 GameObject 上其他组件的 `Start()` 被跳过。修复：将 `RuneManager.Initialize()` 和 `RuneSelectionPanel.Initialize()` 移入 `InitializeCoreServices()` 末尾（Awake 阶段，`EventManager.ClearAll()` 之后）。
- **EnsureAndRegister 增强**：改为三级查找策略（Inspector → `FindAnyObjectByType` → `AddComponent`），消除 Inspector 未拖拽引用导致的空池问题。
- **Canvas 渲染失败**：ScreenSpaceOverlay Canvas 不能作为非 Canvas 对象子级，改为场景根级对象。
