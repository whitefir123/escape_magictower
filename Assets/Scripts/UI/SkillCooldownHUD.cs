// ============================================================================
// 逃离魔塔 - 技能冷却 HUD (SkillCooldownHUD)
// 屏幕底部显示 4 个技能槽位（Space/Q/E/R）的冷却状态 + 怒气条。
// 纯代码构建 UI，与 HUDManager 同级但互不侵入。
//
// 来源：DesignDocs/07_UI_and_UX.md §技能冷却遮罩
//       GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Entity.Hero;
using EscapeTheTower.Combat.Skills;
using EscapeTheTower.Data;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 技能冷却 HUD —— 底部技能栏 + 怒气条
    /// </summary>
    public class SkillCooldownHUD : MonoBehaviour
    {
        // === 英雄引用 ===
        private HeroController _hero;
        private HeroSkillHandler _skillHandler;

        // === UI 根节点 ===
        private Canvas _canvas;
        private RectTransform _rootPanel;

        // === 技能槽位数据 ===
        private struct SkillSlotUI
        {
            public RectTransform Root;
            public Image Background;
            public Image CooldownMask;    // fillAmount 式 CD 遮罩
            public Text CooldownText;     // CD 剩余秒数
            public Text KeyLabel;         // 按键标签（Space/Q/E/R）
            public Text SkillNameLabel;   // 技能名称
            public Image LockIcon;        // 怒气锁（大招专用）
            public SkillSlotType SlotType;
        }

        private SkillSlotUI[] _slots;

        // === 怒气条 ===
        private Image _rageBarFill;
        private Text _rageText;
        private RectTransform _rageBarRoot;
        private float _rageFlashTimer;

        // === 布局常量 ===
        private const float SLOT_SIZE = 56f;
        private const float SLOT_GAP = 10f;
        private const float BAR_HEIGHT = 10f;
        private const float BOTTOM_MARGIN = 16f;

        // === 配色 ===
        private static readonly Color BG_COLOR = new Color(0.12f, 0.12f, 0.15f, 0.85f);
        private static readonly Color CD_MASK_COLOR = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color KEY_COLOR = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color NAME_COLOR = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color READY_BORDER = new Color(0.3f, 0.9f, 0.4f, 0.6f);
        private static readonly Color RAGE_LOW = new Color(0.8f, 0.4f, 0.1f, 1f);
        private static readonly Color RAGE_HIGH = new Color(1f, 0.7f, 0.1f, 1f);
        private static readonly Color RAGE_FULL = new Color(1f, 0.9f, 0.3f, 1f);
        private static readonly Color MANA_INSUFFICIENT = new Color(0.3f, 0.3f, 0.5f, 0.7f);

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 绑定英雄引用（由 HeroController 初始化时调用）
        /// </summary>
        public void SetHeroReference(HeroController hero)
        {
            _hero = hero;
            _skillHandler = hero.GetComponent<HeroSkillHandler>();
            Debug.Log("[SkillCooldownHUD] 英雄引用已绑定");
        }

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            if (_hero == null || _skillHandler == null) return;
            UpdateSlots();
            UpdateRageBar();
        }

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildUI()
        {
            // Canvas（覆盖层，独立于 HUDManager）
            var canvasObj = new GameObject("SkillCooldownCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 15; // 高于 HUDManager(10)
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 根面板（底部居中）
            var rootObj = CreateUIObject("SkillBarRoot", canvasObj.transform);
            _rootPanel = rootObj.GetComponent<RectTransform>();
            _rootPanel.anchorMin = new Vector2(0.5f, 0f);
            _rootPanel.anchorMax = new Vector2(0.5f, 0f);
            _rootPanel.pivot = new Vector2(0.5f, 0f);

            float totalWidth = SLOT_SIZE * 4 + SLOT_GAP * 3;
            float totalHeight = SLOT_SIZE + BAR_HEIGHT + 8f;
            _rootPanel.sizeDelta = new Vector2(totalWidth + 20f, totalHeight + BOTTOM_MARGIN);
            _rootPanel.anchoredPosition = new Vector2(0f, BOTTOM_MARGIN);

            // 构建 4 个技能槽位
            var slotDefs = new[]
            {
                (key: "Space", slot: SkillSlotType.Evasion, name: "闪避"),
                (key: "Q",     slot: SkillSlotType.Active1, name: "技能1"),
                (key: "E",     slot: SkillSlotType.Active2, name: "技能2"),
                (key: "R",     slot: SkillSlotType.Ultimate, name: "大招"),
            };

            _slots = new SkillSlotUI[slotDefs.Length];

            float startX = -(totalWidth / 2f) + SLOT_SIZE / 2f;
            for (int i = 0; i < slotDefs.Length; i++)
            {
                float x = startX + i * (SLOT_SIZE + SLOT_GAP);
                _slots[i] = BuildSlot(slotDefs[i].key, slotDefs[i].slot, slotDefs[i].name, x);
            }

            // 构建怒气条（技能槽位下方）
            BuildRageBar(totalWidth);
        }

        /// <summary>
        /// 构建单个技能槽位
        /// </summary>
        private SkillSlotUI BuildSlot(string keyName, SkillSlotType slotType, string defaultName, float xPos)
        {
            var slot = new SkillSlotUI { SlotType = slotType };

            // 根容器
            var rootObj = CreateUIObject($"Slot_{keyName}", _rootPanel);
            slot.Root = rootObj.GetComponent<RectTransform>();
            slot.Root.anchorMin = new Vector2(0.5f, 1f);
            slot.Root.anchorMax = new Vector2(0.5f, 1f);
            slot.Root.pivot = new Vector2(0.5f, 1f);
            slot.Root.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
            slot.Root.anchoredPosition = new Vector2(xPos, -2f);

            // 背景
            slot.Background = rootObj.AddComponent<Image>();
            slot.Background.color = BG_COLOR;

            // CD 遮罩（从上到下的填充式遮罩）
            var maskObj = CreateUIObject("CDMask", rootObj.transform);
            var maskRect = maskObj.GetComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;
            slot.CooldownMask = maskObj.AddComponent<Image>();
            slot.CooldownMask.color = CD_MASK_COLOR;
            slot.CooldownMask.type = Image.Type.Filled;
            slot.CooldownMask.fillMethod = Image.FillMethod.Vertical;
            slot.CooldownMask.fillOrigin = 0; // Bottom → Top
            slot.CooldownMask.fillAmount = 0f;

            // CD 倒计时文字（居中）
            var cdTextObj = CreateUIObject("CDText", rootObj.transform);
            var cdTextRect = cdTextObj.GetComponent<RectTransform>();
            cdTextRect.anchorMin = Vector2.zero;
            cdTextRect.anchorMax = Vector2.one;
            cdTextRect.sizeDelta = Vector2.zero;
            slot.CooldownText = cdTextObj.AddComponent<Text>();
            slot.CooldownText.text = "";
            slot.CooldownText.font = UIHelper.GetDefaultFont();
            slot.CooldownText.fontSize = 18;
            slot.CooldownText.fontStyle = FontStyle.Bold;
            slot.CooldownText.alignment = TextAnchor.MiddleCenter;
            slot.CooldownText.color = Color.white;

            // 按键标签（左上角）
            var keyObj = CreateUIObject("KeyLabel", rootObj.transform);
            var keyRect = keyObj.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0f, 1f);
            keyRect.sizeDelta = new Vector2(SLOT_SIZE, 16f);
            keyRect.anchoredPosition = new Vector2(3f, -2f);
            slot.KeyLabel = keyObj.AddComponent<Text>();
            slot.KeyLabel.text = keyName;
            slot.KeyLabel.font = UIHelper.GetDefaultFont();
            slot.KeyLabel.fontSize = 11;
            slot.KeyLabel.fontStyle = FontStyle.Bold;
            slot.KeyLabel.alignment = TextAnchor.UpperLeft;
            slot.KeyLabel.color = KEY_COLOR;

            // 技能名称（底部小字）
            var nameObj = CreateUIObject("NameLabel", rootObj.transform);
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 14f);
            nameRect.anchoredPosition = new Vector2(0f, 1f);
            slot.SkillNameLabel = nameObj.AddComponent<Text>();
            slot.SkillNameLabel.text = defaultName;
            slot.SkillNameLabel.font = UIHelper.GetDefaultFont();
            slot.SkillNameLabel.fontSize = 10;
            slot.SkillNameLabel.alignment = TextAnchor.MiddleCenter;
            slot.SkillNameLabel.color = NAME_COLOR;

            // 怒气锁图标（大招专用，默认隐藏）
            var lockObj = CreateUIObject("LockIcon", rootObj.transform);
            var lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.5f, 0.5f);
            lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(24f, 24f);
            slot.LockIcon = lockObj.AddComponent<Image>();
            slot.LockIcon.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            slot.LockIcon.gameObject.SetActive(slotType == SkillSlotType.Ultimate);

            return slot;
        }

        /// <summary>
        /// 构建怒气条
        /// </summary>
        private void BuildRageBar(float totalWidth)
        {
            // 怒气条根容器
            var barRoot = CreateUIObject("RageBarRoot", _rootPanel);
            _rageBarRoot = barRoot.GetComponent<RectTransform>();
            _rageBarRoot.anchorMin = new Vector2(0.5f, 0f);
            _rageBarRoot.anchorMax = new Vector2(0.5f, 0f);
            _rageBarRoot.pivot = new Vector2(0.5f, 0f);
            _rageBarRoot.sizeDelta = new Vector2(totalWidth, BAR_HEIGHT);
            _rageBarRoot.anchoredPosition = new Vector2(0f, 2f);

            // 背景
            var bgObj = barRoot;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.08f, 0.05f, 0.8f);

            // 填充条
            var fillObj = CreateUIObject("RageFill", barRoot.transform);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // 宽度由 sizeDelta.x 控制
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 0f);
            _rageBarFill = fillObj.AddComponent<Image>();
            _rageBarFill.color = RAGE_LOW;

            // 怒气文字
            var textObj = CreateUIObject("RageText", barRoot.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            _rageText = textObj.AddComponent<Text>();
            _rageText.text = "";
            _rageText.font = UIHelper.GetDefaultFont();
            _rageText.fontSize = 9;
            _rageText.alignment = TextAnchor.MiddleCenter;
            _rageText.color = Color.white;
        }

        // =====================================================================
        //  每帧更新
        // =====================================================================

        /// <summary>
        /// 更新 4 个技能槽位的 CD 遮罩和状态
        /// </summary>
        private void UpdateSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var exec = _skillHandler.GetExecutor(slot.SlotType);

                if (exec == null)
                {
                    slot.CooldownMask.fillAmount = 0f;
                    slot.CooldownText.text = "";
                    continue;
                }

                // 更新技能名称（首次获取后设置）
                if (exec.Data != null && slot.SkillNameLabel.text != exec.Data.skillName)
                {
                    slot.SkillNameLabel.text = exec.Data.skillName;
                }

                float cdMax = exec.Data != null ? exec.Data.cooldown : 1f;
                float cdLeft = exec.CooldownRemaining;

                if (cdLeft > 0f && cdMax > 0f)
                {
                    // CD 中：遮罩从满到空
                    slot.CooldownMask.fillAmount = cdLeft / cdMax;
                    slot.CooldownText.text = cdLeft.ToString("F1");
                    slot.Background.color = BG_COLOR;
                }
                else
                {
                    // CD 就绪
                    slot.CooldownMask.fillAmount = 0f;
                    slot.CooldownText.text = "";

                    // 法力不足检查
                    if (exec.Data != null && exec.Data.manaCost > 0f &&
                        _hero.CurrentStats.Get(StatType.MP) < exec.Data.manaCost)
                    {
                        slot.Background.color = MANA_INSUFFICIENT;
                    }
                    else
                    {
                        slot.Background.color = BG_COLOR;
                    }
                }

                // 大招怒气锁定检查
                if (slot.SlotType == SkillSlotType.Ultimate && slot.LockIcon != null)
                {
                    bool rageFull = _hero.IsRageFull();
                    slot.LockIcon.gameObject.SetActive(!rageFull);

                    if (!rageFull)
                    {
                        float rage = _hero.CurrentStats.Get(StatType.Rage);
                        float maxRage = _hero.CurrentStats.Get(StatType.MaxRage);
                        float pct = maxRage > 0f ? rage / maxRage : 0f;
                        slot.CooldownText.text = $"{(pct * 100f):F0}%";
                    }
                }
            }
        }

        /// <summary>
        /// 更新怒气条
        /// </summary>
        private void UpdateRageBar()
        {
            float rage = _hero.CurrentStats.Get(StatType.Rage);
            float maxRage = _hero.CurrentStats.Get(StatType.MaxRage);

            if (maxRage <= 0f)
            {
                _rageBarRoot.gameObject.SetActive(false);
                return;
            }

            _rageBarRoot.gameObject.SetActive(true);
            float ratio = Mathf.Clamp01(rage / maxRage);

            // 填充宽度
            float barWidth = _rageBarRoot.sizeDelta.x;
            var fillRect = _rageBarFill.rectTransform;
            fillRect.sizeDelta = new Vector2(barWidth * ratio, fillRect.sizeDelta.y);

            // 颜色渐变
            if (ratio >= 1f)
            {
                // 满怒闪烁
                _rageFlashTimer += Time.deltaTime * 4f;
                float flash = Mathf.PingPong(_rageFlashTimer, 1f);
                _rageBarFill.color = Color.Lerp(RAGE_HIGH, RAGE_FULL, flash);
                _rageText.text = "★ 怒气满 ★";
            }
            else
            {
                _rageFlashTimer = 0f;
                _rageBarFill.color = Color.Lerp(RAGE_LOW, RAGE_HIGH, ratio);
                _rageText.text = $"{rage:F0}/{maxRage:F0}";
            }
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
