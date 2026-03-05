// ============================================================================
// 逃离魔塔 - 局内 HUD 管理器 (HUDManager)
// 管理所有局内常驻 UI 元素的显示与更新。
// 运行时纯代码创建 Canvas UI（无需预制体）。
//
// 包含：
//   - HP/MP/怒气/经验 四条属性条
//   - 钥匙持有量（铜/银/金）
//   - 当前楼层编号
//   - 拾取浮动提示
//
// 来源：DesignDocs/07_UI_and_UX.md 第二节
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Entity.Hero;
using EscapeTheTower.Map;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 局内 HUD 管理器 —— 管理所有常驻屏幕元素
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("=== 引用 ===")]
        [Tooltip("英雄控制器引用")]
        [SerializeField] private HeroController heroReference;

        // === UI 元素引用 ===
        private Canvas _canvas;

        // 条的 RectTransform（通过缩放宽度实现缩短效果）
        private RectTransform _hpFillRect, _mpFillRect, _rageFillRect, _expFillRect;
        private Text _hpText, _mpText, _rageText, _expText;

        // 条的最大宽度（用于计算缩放）
        private float _hpBarMaxWidth, _mpBarMaxWidth, _rageBarMaxWidth, _expBarMaxWidth;

        // === 钥匙显示 ===
        private Text _keyBronzeText, _keySilverText, _keyGoldText;

        // === 楼层显示 ===
        private Text _floorText;
        private int _displayedFloorLevel = 0;

        // === 浮动提示 ===
        private Transform _canvasTransform;

        // =====================================================================
        //  生命周期
        // =====================================================================

        // 英雄查找限时重试间隔（避免每帧调用 FindAnyObjectByType 的反射开销）
        private float _heroSearchTimer;
        private const float HERO_SEARCH_INTERVAL = 1.0f;

        private void Start()
        {
            BuildHUD();

            // 首次尝试查找英雄引用
            heroReference = FindAnyObjectByType<HeroController>();
            if (heroReference != null)
                Debug.Log("[HUDManager] Start 中已找到英雄引用。");

            // 订阅楼层切换事件
            EventManager.Subscribe<OnFloorEnterEvent>(OnFloorEnter);
            // 订阅拾取事件（用于浮动提示）
            EventManager.Subscribe<OnItemPickedUpEvent>(OnItemPickedUp);
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnFloorEnterEvent>(OnFloorEnter);
            EventManager.Unsubscribe<OnItemPickedUpEvent>(OnItemPickedUp);
        }

        // 装备面板状态追踪
        private bool _isEquipmentPanelOpen;

        private void Update()
        {
            // --- B 键：装备面板开关 ---
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                _isEquipmentPanelOpen = !_isEquipmentPanelOpen;
                EventManager.Publish(new OnEquipmentPanelToggleEvent
                {
                    Meta = new EventMeta(0),
                    IsOpen = _isEquipmentPanelOpen,
                });
            }

            if (heroReference == null)
            {
                // 限时重试：每秒最多查找一次，避免每帧反射开销
                _heroSearchTimer -= Time.deltaTime;
                if (_heroSearchTimer > 0f) return;
                _heroSearchTimer = HERO_SEARCH_INTERVAL;

                heroReference = FindAnyObjectByType<HeroController>();
                if (heroReference == null) return;
                Debug.Log("[HUDManager] 已找到英雄引用。");
            }
            UpdateHUDData();
        }

        // =====================================================================
        //  运行时 UI 构建
        // =====================================================================

        private void BuildHUD()
        {
            // --- Canvas ---
            var canvasObj = new GameObject("HUDCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            _canvasTransform = canvasObj.transform;

            float barWidth = 300f;
            float barHeight = 24f;
            float startY = -20f;
            float spacing = 32f;

            // --- HP 条（绿色/红色） ---
            _hpFillRect = CreateBar(canvasObj.transform, "HP",
                new Color(0.2f, 0.8f, 0.2f),       // 填充色
                new Color(0.15f, 0.15f, 0.15f, 0.8f), // 框色
                new Vector2(20, startY), barWidth, barHeight);
            _hpBarMaxWidth = barWidth - 4f; // 减去内边距
            _hpText = CreateLabel(canvasObj.transform, "HP_Text",
                "HP: --/--", new Vector2(20 + barWidth / 2, startY), 16, Color.white);

            // --- MP 条（蓝色） ---
            _mpFillRect = CreateBar(canvasObj.transform, "MP",
                new Color(0.3f, 0.5f, 1.0f),
                new Color(0.15f, 0.15f, 0.15f, 0.8f),
                new Vector2(20, startY - spacing), barWidth * 0.8f, barHeight * 0.85f);
            _mpBarMaxWidth = barWidth * 0.8f - 4f;
            _mpText = CreateLabel(canvasObj.transform, "MP_Text",
                "MP: --/--", new Vector2(20 + barWidth * 0.4f, startY - spacing), 14, Color.white);

            // --- 怒气条（橙色） ---
            _rageFillRect = CreateBar(canvasObj.transform, "Rage",
                new Color(1.0f, 0.4f, 0.1f),
                new Color(0.15f, 0.15f, 0.15f, 0.8f),
                new Vector2(20, startY - spacing * 2), barWidth * 0.8f, barHeight * 0.85f);
            _rageBarMaxWidth = barWidth * 0.8f - 4f;
            _rageText = CreateLabel(canvasObj.transform, "Rage_Text",
                "怒气: --/--", new Vector2(20 + barWidth * 0.4f, startY - spacing * 2), 14, Color.white);

            // --- 经验条（黄色） ---
            _expFillRect = CreateBar(canvasObj.transform, "EXP",
                new Color(1.0f, 0.85f, 0.0f),
                new Color(0.15f, 0.15f, 0.15f, 0.8f),
                new Vector2(20, startY - spacing * 3), barWidth * 0.8f, barHeight * 0.7f);
            _expBarMaxWidth = barWidth * 0.8f - 4f;
            _expText = CreateLabel(canvasObj.transform, "EXP_Text",
                "EXP: --/--", new Vector2(20 + barWidth * 0.4f, startY - spacing * 3), 12, Color.white);

            // --- 钥匙持有量（属性条下方） ---
            float keyY = startY - spacing * 4 - 10f;
            _keyBronzeText = CreateLabel(canvasObj.transform, "Key_Bronze",
                "🔑 铜×0", new Vector2(55, keyY), 14, new Color(0.72f, 0.45f, 0.20f));
            _keySilverText = CreateLabel(canvasObj.transform, "Key_Silver",
                "🔑 银×0", new Vector2(155, keyY), 14, new Color(0.75f, 0.75f, 0.80f));
            _keyGoldText = CreateLabel(canvasObj.transform, "Key_Gold",
                "🔑 金×0", new Vector2(255, keyY), 14, new Color(0.90f, 0.75f, 0.20f));

            // --- 楼层显示（右上角） ---
            _floorText = CreateLabel(canvasObj.transform, "Floor_Text",
                "第 1 层", new Vector2(-120, -30f), 22, Color.white);
            // 设为右上角锚点
            var floorRect = _floorText.GetComponent<RectTransform>();
            floorRect.anchorMin = new Vector2(1, 1);
            floorRect.anchorMax = new Vector2(1, 1);
            floorRect.pivot = new Vector2(1, 1);
            floorRect.anchoredPosition = new Vector2(-20, -20);

            Debug.Log("[HUDManager] HUD UI 构建完成（HP/MP/怒气/经验/钥匙/楼层）。");
        }

        /// <summary>
        /// 创建一个"框+条"样式的属性条
        /// 通过修改填充条的 RectTransform 宽度实现缩短效果
        /// </summary>
        private RectTransform CreateBar(Transform parent, string name, Color fillColor,
            Color frameColor, Vector2 position, float width, float height)
        {
            // 外框（深色背景）
            var frameObj = new GameObject(name + "_Frame", typeof(RectTransform));
            frameObj.transform.SetParent(parent, false);
            var frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0, 1);
            frameRect.anchorMax = new Vector2(0, 1);
            frameRect.pivot = new Vector2(0, 1);
            frameRect.anchoredPosition = position;
            frameRect.sizeDelta = new Vector2(width, height);
            var frameImg = frameObj.AddComponent<Image>();
            frameImg.color = frameColor;

            // 边框（浅色线框，比外框稍大的视觉效果）
            var borderObj = new GameObject(name + "_Border", typeof(RectTransform));
            borderObj.transform.SetParent(frameObj.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(1, 1);
            borderRect.offsetMax = new Vector2(-1, -1);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);

            // 填充条（从左向右缩放）
            var fillObj = new GameObject(name + "_Fill", typeof(RectTransform));
            fillObj.transform.SetParent(frameObj.transform, false);
            var fillRect = fillObj.GetComponent<RectTransform>();
            // 锚点固定在左侧，通过 sizeDelta.x 控制宽度
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(0, -2);
            fillRect.sizeDelta = new Vector2(width - 4f, 0f); // 初始满条
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = fillColor;

            return fillRect;
        }

        /// <summary>
        /// 创建文字标签（叠加在条上方）
        /// </summary>
        private Text CreateLabel(Transform parent, string name, string content,
            Vector2 position, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(300, 30);
            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);
            return text;
        }

        // =====================================================================
        //  数据刷新
        // =====================================================================

        private void UpdateHUDData()
        {
            var stats = heroReference.CurrentStats;
            if (stats == null) return;

            float hp = stats.Get(StatType.HP);
            float maxHP = stats.Get(StatType.MaxHP);
            float mp = stats.Get(StatType.MP);
            float maxMP = stats.Get(StatType.MaxMP);
            float rage = stats.Get(StatType.Rage);
            float maxRage = stats.Get(StatType.MaxRage);

            // --- HP ---
            float hpRatio = maxHP > 0 ? Mathf.Clamp01(hp / maxHP) : 0f;
            UpdateBarWidth(_hpFillRect, hpRatio, _hpBarMaxWidth);
            if (_hpText != null)
                _hpText.text = $"HP: {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHP)}";
            // 低血变红
            var hpImg = _hpFillRect?.GetComponent<Image>();
            if (hpImg != null)
                hpImg.color = hpRatio < 0.3f ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.2f);

            // --- MP ---
            float mpRatio = maxMP > 0 ? Mathf.Clamp01(mp / maxMP) : 0f;
            UpdateBarWidth(_mpFillRect, mpRatio, _mpBarMaxWidth);
            if (_mpText != null)
                _mpText.text = $"MP: {Mathf.CeilToInt(mp)}/{Mathf.CeilToInt(maxMP)}";

            // --- 怒气 ---
            float rageRatio = maxRage > 0 ? Mathf.Clamp01(rage / maxRage) : 0f;
            UpdateBarWidth(_rageFillRect, rageRatio, _rageBarMaxWidth);
            if (_rageText != null)
                _rageText.text = $"怒气: {Mathf.CeilToInt(rage)}/{Mathf.CeilToInt(maxRage)}";
            var rageImg = _rageFillRect?.GetComponent<Image>();
            if (rageImg != null)
                rageImg.color = (rage >= maxRage && maxRage > 0)
                    ? new Color(1.0f, 0.8f, 0.0f)
                    : new Color(1.0f, 0.4f, 0.1f);

            // --- 经验 ---
            int currentExp = heroReference.CurrentExp;
            int expToNext = heroReference.ExpToNextLevel;
            float expRatio = expToNext > 0 ? Mathf.Clamp01((float)currentExp / expToNext) : 0f;
            UpdateBarWidth(_expFillRect, expRatio, _expBarMaxWidth);
            if (_expText != null)
                _expText.text = $"Lv.{heroReference.CurrentLevel} EXP: {currentExp}/{expToNext}";

            // --- 钥匙 ---
            if (_keyBronzeText != null)
                _keyBronzeText.text = $"🔑 铜×{heroReference.KeyBronze}";
            if (_keySilverText != null)
                _keySilverText.text = $"🔑 银×{heroReference.KeySilver}";
            if (_keyGoldText != null)
                _keyGoldText.text = $"🔑 金×{heroReference.KeyGold}";

            // --- 楼层（仅在变化时更新） ---
            var ftm = FloorTransitionManager.Instance;
            if (ftm != null && ftm.CurrentFloorLevel != _displayedFloorLevel)
            {
                _displayedFloorLevel = ftm.CurrentFloorLevel;
                if (_floorText != null)
                    _floorText.text = $"第 {_displayedFloorLevel} 层";
            }
        }

        /// <summary>
        /// 通过修改 RectTransform 的 sizeDelta.x 实现条的缩放
        /// </summary>
        private void UpdateBarWidth(RectTransform barRect, float ratio, float maxWidth)
        {
            if (barRect == null) return;
            var sd = barRect.sizeDelta;
            sd.x = maxWidth * ratio;
            barRect.sizeDelta = sd;
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        private void OnFloorEnter(OnFloorEnterEvent evt)
        {
            _displayedFloorLevel = evt.FloorNumber;
            if (_floorText != null)
                _floorText.text = $"第 {evt.FloorNumber} 层";
        }

        /// <summary>拾取物品时显示浮动提示</summary>
        private void OnItemPickedUp(OnItemPickedUpEvent evt)
        {
            string text = evt.ItemType switch
            {
                PickupType.HealthPotion => $"+{evt.Value} HP",
                PickupType.ManaPotion => $"+{evt.Value} MP",
                PickupType.GoldPile => $"+{evt.Value} 金币",
                PickupType.KeyBronze => "+1 铜钥匙",
                PickupType.KeySilver => "+1 银钥匙",
                PickupType.KeyGold => "+1 金钥匙",
                _ => null,
            };

            if (text != null)
                ShowFloatText(text);
        }

        /// <summary>显示浮动提示文字（在玩家头顶，2秒后自动销毁）</summary>
        private void ShowFloatText(string message)
        {
            if (_canvasTransform == null) return;

            var obj = new GameObject("FloatText", typeof(RectTransform));
            obj.transform.SetParent(_canvasTransform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 40);

            // 将玩家世界坐标转换为 Canvas 局部坐标（头顶偏上）
            Vector2 anchoredPos = Vector2.zero;
            if (heroReference != null && Camera.main != null)
            {
                // 玩家头顶偏移（世界坐标 Y+0.8）
                Vector3 worldPos = heroReference.transform.position + Vector3.up * 0.8f;
                Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasTransform as RectTransform, screenPos, null, out anchoredPos);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPos;

            var text = obj.AddComponent<Text>();
            text.text = message;
            text.fontSize = 20;
            text.color = new Color(1f, 1f, 0.6f);
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 附加浮动动画组件
            obj.AddComponent<FloatTextAnimation>();
        }

        // =====================================================================
        //  公共方法
        // =====================================================================

        /// <summary>设置英雄引用</summary>
        public void SetHeroReference(HeroController hero)
        {
            heroReference = hero;
        }

        /// <summary>显示/隐藏 HUD</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 浮动文字动画 —— 向上漂浮并淡出
    /// </summary>
    internal class FloatTextAnimation : MonoBehaviour
    {
        private RectTransform _rect;
        private Text _text;
        private float _elapsed;
        private const float DURATION = 2.0f;
        private const float RISE_SPEED = 30f;

        private void Start()
        {
            _rect = GetComponent<RectTransform>();
            _text = GetComponent<Text>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // 向上漂浮
            if (_rect != null)
            {
                var pos = _rect.anchoredPosition;
                pos.y += RISE_SPEED * Time.deltaTime;
                _rect.anchoredPosition = pos;
            }

            // 淡出
            if (_text != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, _elapsed / DURATION);
                var c = _text.color;
                c.a = alpha;
                _text.color = c;
            }

            if (_elapsed >= DURATION)
            {
                Destroy(gameObject);
            }
        }
    }
}
