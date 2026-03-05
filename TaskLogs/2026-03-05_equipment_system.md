# 2026-03-05 装备系统引入

> **摘要**：从零搭建装备系统全链路（数据模型→生成引擎→穿戴管线→自动化工具），并修缮日志展示与精英怪标识。

## 1. 装备生成与穿戴管线
- **数据模型**：`AffixDefinition_SO`（词缀定义）、`AffixDatabase_SO`（词缀注册表，按部位+类型自动索引）、`EquipmentData`（装备实例，含 `ToStatBlock()` 直接输入属性管线层级3）。
- **六步生成引擎 (`EquipmentGenerator`)**：iPwr→品质Roll（7档+MagicFind）→部位→底座白值→词缀（前后缀交替抽取排重）→镶孔。内置8套品质权重预设（普通/精英/Boss/各级宝箱）。
- **掉落对接**：`LootTableHelper` 全部装备占位替换为生成器调用；`PickupItem` 扩展装备初始化、品质颜色渲染；事件 `OnItemPickedUpEvent` 携带装备数据。
- **穿戴管线**：`HeroEquipmentManager` 管理6槽+背包，自动收纳拾取装备。`HeroController` 重写 `RecalculateStats()` 注入装备属性到管线层级3。

## 2. 自动化工具
- **Editor 词缀生成器 (`AffixAssetGenerator`)**：菜单一键生成27条词缀SO + 数据库SO。
- **运行时引导器 (`EquipmentSystemBootstrap`)**：自动加载词缀数据库注入 `LootTableHelper`，监听楼层切换同步深度。修复了 `TestSceneSetup` 中引导器创建晚于首层事件广播的时序缺陷。

## 3. 日志格式与精英标识
- **装备名称**：`前缀1·前缀2的部位之后缀1·后缀2`（如 `抗魔·闪避的鞋靴之贪婪`），清除所有"…"省略号残留。
- **StatType中文映射**：`StatCN()` 输出英文+中文格式（如 `MDEF魔抗`），Console调试友好。
- **精英怪标识**：`FloorTransitionManager` 精英怪命名加"精英"前缀，所有战斗日志自动继承。

## 4. 装备与背包 UI (Phase 2)
- **纯代码 UI 构建 (`UIHelper`)**：摒弃散碎 Prefab，提供一套无侵入性的 Canvas 工厂方法。支持动态锚点、全屏分辨率适配、统一色彩视觉规范。
- **全功能交互看板 (`EquipmentPanelUI`)**：
  - **纸娃娃系统（6 槽位）**：支持 Hover 悬停展示详情、右键双击卸下。
  - **翻页式背包（5 页共 150 格）**：左键双击自动穿戴，右键浮动菜单进行操作。支持对比弹窗（已穿戴对比背包内）。
  - **16项详细属性显示**：根据 `StatType` 动态请求英雄数值，小数保留至后两位（`F2`）展示真实数据。
- **关键 Bug 解决**：
  - **Input System 冲突**：全面废弃了旧版 `Input.GetKeyDown` 与 `Input.mousePosition`，改用新 `Input System`（`Keyboard.current`/`Mouse.current`），解决了启动时 999+ 严重报错。
  - **交互防遮挡与事件失效**：在 Start 阶段自举检测并生成 `EventSystem` + `InputSystemUIInputModule`，并修补了全屏遮罩 `Overlay` 的射线拦截（`raycastTarget=false`）。
  - **拾取投递链断裂修复**：移除完全依赖 `EventBus` 的装备拾取逻辑（由于生命周期切换引发的脱钩），改为在 `HeroController.OnArrivedAtTile` 处与其他类型掉落物同步进行**直接方法调用**。彻底打通了拾取→自动穿戴/进包→UI刷新的数据闭环。
