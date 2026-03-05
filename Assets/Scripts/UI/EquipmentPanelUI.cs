// ============================================================================
// 逃离魔塔 - 装备面板 UI (EquipmentPanelUI)
// 全屏阻塞式装备与背包界面，B键开关。
// 纯代码构建 Canvas UI，无需预制体。
//
// 布局：
//   左侧(45%)：英雄立绘居中 + 两侧各3装备槽 + 下方属性面板
//   右侧(55%)：5页翻页背包网格(5×6=30格/页，共150容量)
//
// 交互：
//   双击背包装备 → 自动穿戴
//   右键背包物品 → 弹出菜单（装备/丢弃）
//   右键已穿戴装备 → 自动卸下到背包
//   悬停 → 显示 Tooltip + 对比弹窗
//
// 来源：DesignDocs/07_UI_and_UX.md §1.3a
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using EscapeTheTower.Core;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 装备面板 UI —— 完整的装备与背包管理界面
    /// </summary>
    public class EquipmentPanelUI : MonoBehaviour
    {
        // =====================================================================
        //  常量
        // =====================================================================

        private const int GRID_COLS = 5;
        private const int GRID_ROWS = 6;
        private const int ITEMS_PER_PAGE = GRID_COLS * GRID_ROWS;    // 30
        private const int TOTAL_PAGES = 5;
        private const float SLOT_SIZE = 64f;
        private const float SLOT_GAP = 6f;

        // 双击检测间隔（秒，使用 unscaledTime 因为面板打开时 timeScale=0）
        private const float DOUBLE_CLICK_INTERVAL = 0.4f;

        // =====================================================================
        //  UI 引用
        // =====================================================================

        private Canvas _canvas;
        private GameObject _panelRoot;
        private bool _isVisible;

        // --- 纸娃娃槽位 ---
        private readonly Dictionary<EquipmentSlot, GameObject> _slotObjects
            = new Dictionary<EquipmentSlot, GameObject>();
        private readonly Dictionary<EquipmentSlot, Text> _slotLabels
            = new Dictionary<EquipmentSlot, Text>();
        private readonly Dictionary<EquipmentSlot, Image> _slotBorders
            = new Dictionary<EquipmentSlot, Image>();

        // --- 属性面板 ---
        private readonly Dictionary<StatType, Text> _statLabels
            = new Dictionary<StatType, Text>();

        // --- 背包网格 ---
        private readonly GameObject[] _gridSlots = new GameObject[ITEMS_PER_PAGE];
        private readonly Text[] _gridLabels = new Text[ITEMS_PER_PAGE];
        private readonly Image[] _gridBorders = new Image[ITEMS_PER_PAGE];
        private int _currentPage = 0;
        private Text _pageLabel;

        // --- 子组件引用 ---
        private EquipmentTooltip _tooltip;
        private EquipmentComparePopup _comparePopup;
        private EquipmentContextMenu _contextMenu;

        // --- 英雄引用 ---
        private HeroEquipmentManager _equipManager;

        // --- 双击检测 ---
        private int _lastClickedGridIndex = -1;
        private float _lastClickTime;

        // --- 悬停追踪 ---
        private int _hoveredGridIndex = -1;
        private EquipmentSlot? _hoveredSlot = null;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Start()
        {
            // 确保场景中存在 EventSystem（新 Input System 必需）
            EnsureEventSystem();

            BuildUI();
            _panelRoot.SetActive(false);

            // 订阅事件
            EventManager.Subscribe<OnEquipmentPanelToggleEvent>(OnPanelToggle);
            EventManager.Subscribe<OnEquipmentChangedEvent>(OnEquipmentChanged);

            // 创建子组件
            var tooltipObj = new GameObject("EquipmentTooltipRoot");
            tooltipObj.transform.SetParent(transform, false);
            _tooltip = tooltipObj.AddComponent<EquipmentTooltip>();

            var compareObj = new GameObject("EquipmentCompareRoot");
            compareObj.transform.SetParent(transform, false);
            _comparePopup = compareObj.AddComponent<EquipmentComparePopup>();

            var contextObj = new GameObject("EquipmentContextMenuRoot");
            contextObj.transform.SetParent(transform, false);
            _contextMenu = contextObj.AddComponent<EquipmentContextMenu>();
        }

        /// <summary>
        /// 确保场景中存在 EventSystem + InputSystemUIInputModule
        /// 没有 EventSystem，所有 UI 交互（Button/EventTrigger）都无法生效
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            // 新 Input System 需要 InputSystemUIInputModule 代替 StandaloneInputModule
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[EquipmentPanelUI] 自动创建 EventSystem + InputSystemUIInputModule。");
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEquipmentPanelToggleEvent>(OnPanelToggle);
            EventManager.Unsubscribe<OnEquipmentChangedEvent>(OnEquipmentChanged);
        }

        private void Update()
        {
            if (!_isVisible) return;

            // 悬停检测（使用 unscaledDeltaTime 因为 timeScale=0）
            UpdateHoverDetection();
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        private void OnPanelToggle(OnEquipmentPanelToggleEvent evt)
        {
            if (evt.IsOpen)
                ShowPanel();
            else
                HidePanel();
        }

        private void OnEquipmentChanged(OnEquipmentChangedEvent evt)
        {
            if (_isVisible)
            {
                RefreshAll();
            }
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>设置英雄装备管理器引用</summary>
        public void SetEquipmentManager(HeroEquipmentManager manager)
        {
            _equipManager = manager;
        }

        /// <summary>显示面板</summary>
        public void ShowPanel()
        {
            if (_equipManager == null)
            {
                // 尝试从场景中查找
                _equipManager = FindAnyObjectByType<HeroEquipmentManager>();
                if (_equipManager == null)
                {
                    Debug.LogWarning("[EquipmentPanelUI] 未找到 HeroEquipmentManager！");
                    return;
                }
                Debug.Log("[EquipmentPanelUI] 已通过查找获取 HeroEquipmentManager 引用。");
            }

            _panelRoot.SetActive(true);
            _isVisible = true;
            Time.timeScale = 0f;
            _currentPage = 0;
            RefreshAll();
            Debug.Log("[EquipmentPanelUI] 装备面板打开。");
        }

        /// <summary>隐藏面板</summary>
        public void HidePanel()
        {
            _panelRoot.SetActive(false);
            _isVisible = false;
            Time.timeScale = 1f;
            _tooltip?.Hide();
            _comparePopup?.Hide();
            _contextMenu?.Hide();
            Debug.Log("[EquipmentPanelUI] 装备面板关闭。");
        }

        /// <summary>面板是否正在显示</summary>
        public bool IsVisible => _isVisible;

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildUI()
        {
            // --- Canvas ---
            _canvas = UIHelper.CreateScreenCanvas(transform, "EquipmentPanelCanvas", 150);

            // --- 根节点（全屏） ---
            _panelRoot = UIHelper.CreatePanel(_canvas.transform, "PanelRoot",
                Color.clear, Vector2.zero, Vector2.one, Vector2.zero);

            // --- 半透明遮罩（不拦截射线，否则会挡住所有子元素交互） ---
            var overlay = UIHelper.CreatePanel(_panelRoot.transform, "Overlay",
                UIHelper.OverlayColor, Vector2.zero, Vector2.one, Vector2.zero);
            overlay.GetComponent<Image>().raycastTarget = false;

            // --- 主容器（居中 85%×90%） ---
            var mainContainer = UIHelper.CreatePanel(_panelRoot.transform, "MainContainer",
                UIHelper.PanelBgDark, new Vector2(0.075f, 0.05f), new Vector2(0.925f, 0.95f), Vector2.zero);

            // --- 标题栏 ---
            UIHelper.CreateText(mainContainer.transform, "Title", "装备与背包",
                UIHelper.FontSizeTitle, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.93f), new Vector2(1f, 1f));

            // --- 左侧区域（纸娃娃 + 属性） ---
            var leftArea = UIHelper.CreatePanel(mainContainer.transform, "LeftArea",
                Color.clear, new Vector2(0.01f, 0.04f), new Vector2(0.44f, 0.92f), Vector2.zero);

            BuildPaperDoll(leftArea.transform);
            BuildAttributePanel(leftArea.transform);

            // --- 右侧区域（背包网格） ---
            var rightArea = UIHelper.CreatePanel(mainContainer.transform, "RightArea",
                new Color(0.12f, 0.12f, 0.16f, 0.8f),
                new Vector2(0.46f, 0.04f), new Vector2(0.99f, 0.92f), Vector2.zero);

            BuildBackpackGrid(rightArea.transform);

            // --- 关闭按钮 ---
            UIHelper.CreateButton(mainContainer.transform, "CloseButton", "关闭 (B)",
                UIHelper.FontSizeNormal, new Color(0.3f, 0.15f, 0.15f, 0.9f), Color.white,
                new Vector2(0.4f, 0.0f), new Vector2(0.6f, 0.04f), HidePanel);
        }

        // =====================================================================
        //  纸娃娃构建
        // =====================================================================

        private void BuildPaperDoll(Transform parent)
        {
            // 英雄立绘（占位方块，后续替换）
            var portrait = UIHelper.CreatePanel(parent, "HeroPortrait",
                new Color(0.25f, 0.25f, 0.30f, 0.8f),
                new Vector2(0.3f, 0.48f), new Vector2(0.7f, 0.95f), Vector2.zero);
            UIHelper.CreateText(portrait.transform, "PortraitLabel", "英雄立绘\n(待接入)",
                UIHelper.FontSizeSmall, UIHelper.TextDimColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);

            // 左侧 3 槽位：武器、手套、首饰
            CreateEquipSlot(parent, EquipmentSlot.Weapon, new Vector2(0.02f, 0.80f), new Vector2(0.27f, 0.95f));
            CreateEquipSlot(parent, EquipmentSlot.Gloves, new Vector2(0.02f, 0.63f), new Vector2(0.27f, 0.78f));
            CreateEquipSlot(parent, EquipmentSlot.Accessory, new Vector2(0.02f, 0.46f), new Vector2(0.27f, 0.61f));

            // 右侧 3 槽位：头盔、胸甲、鞋靴
            CreateEquipSlot(parent, EquipmentSlot.Helmet, new Vector2(0.73f, 0.80f), new Vector2(0.98f, 0.95f));
            CreateEquipSlot(parent, EquipmentSlot.Armor, new Vector2(0.73f, 0.63f), new Vector2(0.98f, 0.78f));
            CreateEquipSlot(parent, EquipmentSlot.Boots, new Vector2(0.73f, 0.46f), new Vector2(0.98f, 0.61f));
        }

        private void CreateEquipSlot(Transform parent, EquipmentSlot slot,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var slotObj = UIHelper.CreatePanel(parent, $"Slot_{slot}",
                UIHelper.SlotEmptyColor, anchorMin, anchorMax, Vector2.zero);

            // 边框高亮层
            var border = UIHelper.CreatePanel(slotObj.transform, "Border",
                UIHelper.SeparatorColor, Vector2.zero, Vector2.one, Vector2.zero);
            _slotBorders[slot] = border.GetComponent<Image>();

            // 内容区
            var content = UIHelper.CreatePanel(border.transform, "Content",
                UIHelper.SlotEmptyColor,
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero);

            // 槽位文字标签
            var label = UIHelper.CreateText(content.transform, "Label",
                GetSlotShortName(slot),
                UIHelper.FontSizeSmall, UIHelper.TextDimColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);
            _slotLabels[slot] = label;

            // 添加事件触发器处理右键和悬停
            var trigger = slotObj.AddComponent<EventTrigger>();

            // 右键 → 卸除
            var rightClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            rightClick.callback.AddListener((data) =>
            {
                var pointerData = (PointerEventData)data;
                if (pointerData.button == PointerEventData.InputButton.Right)
                {
                    OnSlotRightClick(slot);
                }
            });
            trigger.triggers.Add(rightClick);

            // 悬停进入
            var hoverEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hoverEnter.callback.AddListener((_) => OnSlotHoverEnter(slot));
            trigger.triggers.Add(hoverEnter);

            // 悬停离开
            var hoverExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            hoverExit.callback.AddListener((_) => OnSlotHoverExit());
            trigger.triggers.Add(hoverExit);

            _slotObjects[slot] = slotObj;
        }

        // =====================================================================
        //  属性面板构建
        // =====================================================================

        private void BuildAttributePanel(Transform parent)
        {
            var attrPanel = UIHelper.CreatePanel(parent, "AttributePanel",
                new Color(0.12f, 0.12f, 0.16f, 0.8f),
                new Vector2(0.02f, 0.0f), new Vector2(0.98f, 0.44f), Vector2.zero);

            UIHelper.CreateText(attrPanel.transform, "AttrTitle", "── 角色属性 ──",
                UIHelper.FontSizeSmall, UIHelper.TextDimColor, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.92f), new Vector2(1f, 1f));

            // 属性按两列排布
            var stats = new (StatType type, string name)[]
            {
                (StatType.MaxHP, "生命"), (StatType.MaxMP, "法力"),
                (StatType.ATK, "物攻"), (StatType.MATK, "魔攻"),
                (StatType.DEF, "物防"), (StatType.MDEF, "魔抗"),
                (StatType.CritRate, "暴击率"), (StatType.CritMultiplier, "暴伤"),
                (StatType.ArmorPen, "物穿"), (StatType.MagicPen, "魔穿"),
                (StatType.Dodge, "闪避"), (StatType.AttackSpeed, "攻速"),
                (StatType.LifeSteal, "吸血"), (StatType.ManaRegen, "回蓝"),
                (StatType.MoveSpeed, "移速"), (StatType.BonusCCResist, "控抗"),
            };

            int rows = (stats.Length + 1) / 2;
            float rowH = 0.88f / rows;

            for (int i = 0; i < stats.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float xMin = col == 0 ? 0.03f : 0.52f;
                float xMax = col == 0 ? 0.5f : 0.97f;
                float yMax = 0.90f - row * rowH;
                float yMin = yMax - rowH;

                var label = UIHelper.CreateText(attrPanel.transform, $"Stat_{stats[i].type}",
                    $"{stats[i].name}：--",
                    UIHelper.FontSizeSmall, UIHelper.TextNormalColor, TextAnchor.MiddleLeft,
                    new Vector2(xMin, yMin), new Vector2(xMax, yMax));

                _statLabels[stats[i].type] = label;
            }
        }

        // =====================================================================
        //  背包网格构建
        // =====================================================================

        private void BuildBackpackGrid(Transform parent)
        {
            // 背包标题
            UIHelper.CreateText(parent.transform, "BackpackTitle", "背包",
                UIHelper.FontSizeNormal, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.93f), new Vector2(1f, 1f));

            // 网格区域
            var gridArea = UIHelper.CreatePanel(parent, "GridArea",
                Color.clear, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.92f), Vector2.zero);

            float colW = 1f / GRID_COLS;
            float rowH = 1f / GRID_ROWS;
            float cellPadding = 0.005f;

            for (int i = 0; i < ITEMS_PER_PAGE; i++)
            {
                int col = i % GRID_COLS;
                int row = i / GRID_COLS;
                // 行从上往下排列
                float xMin = col * colW + cellPadding;
                float xMax = (col + 1) * colW - cellPadding;
                float yMax = 1f - row * rowH - cellPadding;
                float yMin = 1f - (row + 1) * rowH + cellPadding;

                var cellObj = UIHelper.CreatePanel(gridArea.transform, $"Cell_{i}",
                    UIHelper.SlotEmptyColor,
                    new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero);

                // 边框
                var borderObj = UIHelper.CreatePanel(cellObj.transform, "Border",
                    UIHelper.SeparatorColor, Vector2.zero, Vector2.one, Vector2.zero);
                _gridBorders[i] = borderObj.GetComponent<Image>();

                // 内容区
                var content = UIHelper.CreatePanel(borderObj.transform, "Content",
                    UIHelper.SlotEmptyColor,
                    new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero);

                // 装备名称标签
                var label = UIHelper.CreateText(content.transform, "Label", "",
                    11, UIHelper.TextNormalColor, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one);
                _gridLabels[i] = label;

                // 事件触发器
                var trigger = cellObj.AddComponent<EventTrigger>();
                int capturedIndex = i;

                // 左键点击（双击检测）
                var leftClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                leftClick.callback.AddListener((data) =>
                {
                    var pointerData = (PointerEventData)data;
                    if (pointerData.button == PointerEventData.InputButton.Left)
                        OnGridLeftClick(capturedIndex);
                    else if (pointerData.button == PointerEventData.InputButton.Right)
                        OnGridRightClick(capturedIndex, pointerData.position);
                });
                trigger.triggers.Add(leftClick);

                // 悬停
                var hoverEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hoverEnter.callback.AddListener((_) => OnGridHoverEnter(capturedIndex));
                trigger.triggers.Add(hoverEnter);

                var hoverExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                hoverExit.callback.AddListener((_) => OnGridHoverExit());
                trigger.triggers.Add(hoverExit);

                _gridSlots[i] = cellObj;
            }

            // 翻页控件
            var pageArea = UIHelper.CreatePanel(parent, "PageArea",
                Color.clear, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.09f), Vector2.zero);

            UIHelper.CreateButton(pageArea.transform, "PrevPage", "◀",
                UIHelper.FontSizeNormal, new Color(0.25f, 0.25f, 0.30f, 0.9f), Color.white,
                new Vector2(0.0f, 0.0f), new Vector2(0.25f, 1f), OnPrevPage);

            _pageLabel = UIHelper.CreateText(pageArea.transform, "PageLabel", "1/5",
                UIHelper.FontSizeNormal, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.0f), new Vector2(0.7f, 1f));

            UIHelper.CreateButton(pageArea.transform, "NextPage", "▶",
                UIHelper.FontSizeNormal, new Color(0.25f, 0.25f, 0.30f, 0.9f), Color.white,
                new Vector2(0.75f, 0.0f), new Vector2(1f, 1f), OnNextPage);
        }

        // =====================================================================
        //  数据刷新
        // =====================================================================

        private void RefreshAll()
        {
            if (_equipManager == null)
            {
                Debug.LogWarning("[EquipmentPanelUI] RefreshAll 跳过：_equipManager 为 null！");
                return;
            }

            // 诊断日志
            var inv = _equipManager.GetInventory();
            int equippedCount = 0;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (_equipManager.GetEquippedItem(slot) != null) equippedCount++;
            }
            Debug.Log($"[EquipmentPanelUI] RefreshAll | 已穿戴={equippedCount} | 背包={inv.Count}件");

            RefreshPaperDoll();
            RefreshBackpackGrid();
            RefreshAttributePanel();
        }

        private void RefreshPaperDoll()
        {
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var equip = _equipManager.GetEquippedItem(slot);

                if (equip != null)
                {
                    Color qColor = EquipmentData.GetQualityColor(equip.quality);
                    _slotBorders[slot].color = qColor;
                    // 显示缩略名（取前4字符避免溢出）
                    string displayName = equip.GetDisplayName();
                    _slotLabels[slot].text = displayName.Length > 6
                        ? displayName[..6] + "…"
                        : displayName;
                    _slotLabels[slot].color = qColor;
                }
                else
                {
                    _slotBorders[slot].color = UIHelper.SeparatorColor;
                    _slotLabels[slot].text = GetSlotShortName(slot);
                    _slotLabels[slot].color = UIHelper.TextDimColor;
                }
            }
        }

        private void RefreshBackpackGrid()
        {
            var inventory = _equipManager.GetInventory();
            int startIndex = _currentPage * ITEMS_PER_PAGE;

            for (int i = 0; i < ITEMS_PER_PAGE; i++)
            {
                int itemIndex = startIndex + i;
                if (itemIndex < inventory.Count)
                {
                    var equip = inventory[itemIndex];
                    Color qColor = EquipmentData.GetQualityColor(equip.quality);
                    _gridBorders[i].color = qColor;
                    string displayName = equip.GetDisplayName();
                    _gridLabels[i].text = displayName.Length > 6
                        ? displayName[..6] + "…"
                        : displayName;
                    _gridLabels[i].color = qColor;
                }
                else
                {
                    _gridBorders[i].color = UIHelper.SeparatorColor;
                    _gridLabels[i].text = "";
                    _gridLabels[i].color = UIHelper.TextDimColor;
                }
            }

            // 更新页码
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)inventory.Count / ITEMS_PER_PAGE));
            totalPages = Mathf.Max(totalPages, TOTAL_PAGES);
            _pageLabel.text = $"{_currentPage + 1}/{TOTAL_PAGES}";
        }

        private void RefreshAttributePanel()
        {
            // 从 HeroController.CurrentStats 获取最终属性
            var hero = _equipManager.GetComponent<EscapeTheTower.Entity.Hero.HeroController>();
            if (hero == null || hero.CurrentStats == null) return;

            foreach (var kvp in _statLabels)
            {
                float val = hero.CurrentStats.Get(kvp.Key);
                string name = GetStatShortName(kvp.Key);
                bool isPercent = IsPercentStat(kvp.Key);

                if (isPercent)
                    kvp.Value.text = $"{name}：{val:F2}%";
                else
                    kvp.Value.text = $"{name}：{val:F2}";
            }
        }

        // =====================================================================
        //  交互回调
        // =====================================================================

        /// <summary>右键纸娃娃槽位 → 卸下装备</summary>
        private void OnSlotRightClick(EquipmentSlot slot)
        {
            if (_equipManager == null) return;
            _equipManager.Unequip(slot);
            _tooltip?.Hide();
            _comparePopup?.Hide();
            RefreshAll();
        }

        /// <summary>悬停纸娃娃槽位</summary>
        private void OnSlotHoverEnter(EquipmentSlot slot)
        {
            _hoveredSlot = slot;
            if (_equipManager == null) return;
            var equip = _equipManager.GetEquippedItem(slot);
            if (equip != null)
            {
                _tooltip?.Show(equip, GetMouseScreenPosition());
            }
        }

        private void OnSlotHoverExit()
        {
            _hoveredSlot = null;
            _tooltip?.Hide();
            _comparePopup?.Hide();
        }

        /// <summary>左键点击背包格子（双击检测）</summary>
        private void OnGridLeftClick(int gridIndex)
        {
            float currentTime = Time.unscaledTime;

            if (_lastClickedGridIndex == gridIndex &&
                (currentTime - _lastClickTime) < DOUBLE_CLICK_INTERVAL)
            {
                // 双击 → 自动穿戴
                OnGridDoubleClick(gridIndex);
                _lastClickedGridIndex = -1;
                return;
            }

            _lastClickedGridIndex = gridIndex;
            _lastClickTime = currentTime;
        }

        /// <summary>双击背包装备 → 自动穿戴</summary>
        private void OnGridDoubleClick(int gridIndex)
        {
            var equip = GetInventoryItemAtGrid(gridIndex);
            if (equip == null) return;
            _equipManager.Equip(equip);
            _tooltip?.Hide();
            _comparePopup?.Hide();
            _contextMenu?.Hide();
            RefreshAll();
        }

        /// <summary>右键背包格子 → 弹出菜单</summary>
        private void OnGridRightClick(int gridIndex, Vector2 screenPos)
        {
            var equip = GetInventoryItemAtGrid(gridIndex);
            if (equip == null) return;

            _contextMenu?.Show(screenPos,
                ("装备", () =>
                {
                    _equipManager.Equip(equip);
                    RefreshAll();
                }),
                ("丢弃", () =>
                {
                    _equipManager.DiscardFromInventory(equip);
                    RefreshAll();
                })
            );
        }

        /// <summary>悬停背包格子</summary>
        private void OnGridHoverEnter(int gridIndex)
        {
            _hoveredGridIndex = gridIndex;
            var equip = GetInventoryItemAtGrid(gridIndex);
            if (equip == null)
            {
                _tooltip?.Hide();
                _comparePopup?.Hide();
                return;
            }

            _tooltip?.Show(equip, GetMouseScreenPosition());

            // 对比弹窗：显示同槽位已穿戴装备
            var equipped = _equipManager.GetEquippedItem(equip.slot);
            if (equipped != null && _tooltip != null)
            {
                _comparePopup?.Show(equipped, _tooltip.GetTooltipRect());
            }
            else
            {
                _comparePopup?.Hide();
            }
        }

        private void OnGridHoverExit()
        {
            _hoveredGridIndex = -1;
            _tooltip?.Hide();
            _comparePopup?.Hide();
        }

        // =====================================================================
        //  翻页
        // =====================================================================

        private void OnPrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshBackpackGrid();
            }
        }

        private void OnNextPage()
        {
            if (_currentPage < TOTAL_PAGES - 1)
            {
                _currentPage++;
                RefreshBackpackGrid();
            }
        }

        // =====================================================================
        //  悬停更新（运行在 timeScale=0 下）
        // =====================================================================

        private void UpdateHoverDetection()
        {
            // Tooltip 跟随鼠标位置实时更新
            if (_hoveredGridIndex >= 0)
            {
                var equip = GetInventoryItemAtGrid(_hoveredGridIndex);
                if (equip != null)
                {
                    _tooltip?.Show(equip, GetMouseScreenPosition());
                    var equipped = _equipManager?.GetEquippedItem(equip.slot);
                    if (equipped != null && _tooltip != null)
                        _comparePopup?.Show(equipped, _tooltip.GetTooltipRect());
                }
            }
            else if (_hoveredSlot.HasValue)
            {
                var equip = _equipManager?.GetEquippedItem(_hoveredSlot.Value);
                if (equip != null)
                {
                    _tooltip?.Show(equip, GetMouseScreenPosition());
                }
            }
        }

        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>获取鼠标屏幕坐标（新 Input System）</summary>
        private static Vector2 GetMouseScreenPosition()
        {
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }

        /// <summary>获取背包中对应网格位置的装备数据</summary>
        private EquipmentData GetInventoryItemAtGrid(int gridIndex)
        {
            if (_equipManager == null) return null;
            var inventory = _equipManager.GetInventory();
            int itemIndex = _currentPage * ITEMS_PER_PAGE + gridIndex;
            if (itemIndex < 0 || itemIndex >= inventory.Count) return null;
            return inventory[itemIndex];
        }

        /// <summary>槽位缩略名</summary>
        private static string GetSlotShortName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "武器",
                EquipmentSlot.Helmet => "头盔",
                EquipmentSlot.Armor => "胸甲",
                EquipmentSlot.Gloves => "手套",
                EquipmentSlot.Boots => "鞋靴",
                EquipmentSlot.Accessory => "首饰",
                _ => "未知",
            };
        }

        /// <summary>属性缩略名</summary>
        private static string GetStatShortName(StatType stat)
        {
            return stat switch
            {
                StatType.MaxHP => "生命",
                StatType.MaxMP => "法力",
                StatType.ATK => "物攻",
                StatType.MATK => "魔攻",
                StatType.DEF => "物防",
                StatType.MDEF => "魔抗",
                StatType.CritRate => "暴击",
                StatType.CritMultiplier => "暴伤",
                StatType.ArmorPen => "物穿",
                StatType.MagicPen => "魔穿",
                StatType.Dodge => "闪避",
                StatType.AttackSpeed => "攻速",
                StatType.LifeSteal => "吸血",
                StatType.ManaRegen => "回蓝",
                StatType.MoveSpeed => "移速",
                StatType.BonusCCResist => "控抗",
                _ => stat.ToString(),
            };
        }

        /// <summary>判断是否为百分比属性</summary>
        private static bool IsPercentStat(StatType stat)
        {
            return stat switch
            {
                StatType.CritRate or StatType.CritMultiplier or
                StatType.ArmorPen or StatType.MagicPen or
                StatType.Dodge or StatType.AttackSpeed or
                StatType.LifeSteal or StatType.MoveSpeed => true,
                _ => false,
            };
        }
    }
}
