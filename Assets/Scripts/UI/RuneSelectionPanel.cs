// ============================================================================
// 逃离魔塔 - 符文三选一 UI 面板 (RuneSelectionPanel)
// 运行时纯代码创建 Canvas UI，无需预制体。
// 展示三张符文卡片，颜色边框区分稀有度，点击选择或放弃。
//
// 来源：DesignDocs/05_Runes_and_MetaProgression.md
//       DesignDocs/07_UI_and_UX.md 第二节
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Rune;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 符文三选一 UI 面板 —— 运行时纯代码构建
    /// </summary>
    public class RuneSelectionPanel : MonoBehaviour
    {
        // === UI 根节点 ===
        private Canvas _canvas;
        private GameObject _panelRoot;
        private GameObject[] _cardObjects = new GameObject[3];
        private Text[] _cardTitles = new Text[3];
        private Text[] _cardDescriptions = new Text[3];
        private Text[] _cardRarities = new Text[3];
        private Image[] _cardBorders = new Image[3];
        private Button[] _cardButtons = new Button[3];
        private Button _skipButton;
        private Text _headerText;

        // === 状态 ===
        private bool _isShowing;

        // =====================================================================
        //  稀有度颜色映射
        // =====================================================================

        /// <summary>
        /// 根据稀有度返回边框颜色
        /// </summary>
        private Color GetRarityColor(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common:      return new Color(0.7f, 0.7f, 0.7f);    // 灰白
                case RuneRarity.Rare:        return new Color(0.2f, 0.6f, 1.0f);    // 蓝色
                case RuneRarity.Exceptional: return new Color(0.6f, 0.2f, 0.9f);    // 紫色
                case RuneRarity.Epic:        return new Color(1.0f, 0.65f, 0.0f);   // 橙色
                case RuneRarity.Legendary:   return new Color(1.0f, 0.85f, 0.0f);   // 金色
                case RuneRarity.Cursed:      return new Color(0.8f, 0.0f, 0.2f);    // 暗红
                default:                     return Color.white;
            }
        }

        /// <summary>
        /// 稀有度中文名称
        /// </summary>
        private string GetRarityName(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common:      return "凡品";
                case RuneRarity.Rare:        return "稀有";
                case RuneRarity.Exceptional: return "罕见";
                case RuneRarity.Epic:        return "史诗";
                case RuneRarity.Legendary:   return "传说";
                case RuneRarity.Cursed:      return "诅咒";
                default:                     return "未知";
            }
        }

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Start()
        {
            BuildUI();
            _panelRoot.SetActive(false);

            // 订阅符文三选一就绪事件
            EventManager.Subscribe<OnRuneDraftReadyEvent>(OnDraftReady);
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnRuneDraftReadyEvent>(OnDraftReady);
        }

        // =====================================================================
        //  事件监听
        // =====================================================================

        private void OnDraftReady(OnRuneDraftReadyEvent evt)
        {
            ShowPanel(evt.AcquisitionType);
        }

        // =====================================================================
        //  UI 构建（运行时纯代码，无需预制体）
        // =====================================================================

        private void BuildUI()
        {
            // --- Canvas ---
            var canvasObj = new GameObject("RuneSelectionCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 确保在最顶层
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // --- 半透明背景遮罩 ---
            _panelRoot = CreatePanel(canvasObj.transform, "PanelRoot",
                Color.clear, Vector2.zero, Vector2.one, Vector2.zero);
            var overlay = CreatePanel(_panelRoot.transform, "Overlay",
                new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero);

            // --- 标题 ---
            var headerObj = CreateTextObject(_panelRoot.transform, "Header",
                "— 命运符文三选一 —", 32, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.92f));
            _headerText = headerObj.GetComponent<Text>();

            // --- 三张卡片 ---
            float cardWidth = 0.24f;
            float cardGap = 0.02f;
            float totalWidth = cardWidth * 3 + cardGap * 2;
            float startX = (1f - totalWidth) / 2f;

            for (int i = 0; i < 3; i++)
            {
                float x = startX + i * (cardWidth + cardGap);
                CreateCard(i, _panelRoot.transform, x, 0.22f, x + cardWidth, 0.76f);
            }

            // --- 放弃按钮 ---
            var skipObj = CreatePanel(_panelRoot.transform, "SkipButton",
                new Color(0.3f, 0.3f, 0.3f, 0.9f),
                new Vector2(0.42f, 0.08f), new Vector2(0.58f, 0.18f), Vector2.zero);
            _skipButton = skipObj.AddComponent<Button>();
            _skipButton.onClick.AddListener(OnSkipClicked);
            var skipText = CreateTextObject(skipObj.transform, "SkipText",
                "放弃选择", 20, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);

            Debug.Log("[RuneSelectionPanel] UI 构建完成。");
        }

        /// <summary>
        /// 创建单张符文卡片
        /// </summary>
        private void CreateCard(int index, Transform parent, float minX, float minY, float maxX, float maxY)
        {
            // 卡片边框（颜色根据稀有度变化）
            var cardObj = CreatePanel(parent, $"Card_{index}",
                new Color(0.15f, 0.15f, 0.2f, 0.95f),
                new Vector2(minX, minY), new Vector2(maxX, maxY), Vector2.zero);

            // 边框高亮层
            var borderObj = CreatePanel(cardObj.transform, "Border",
                Color.white, Vector2.zero, Vector2.one, Vector2.zero);
            _cardBorders[index] = borderObj.GetComponent<Image>();

            // 内容区（比边框缩进 4px）
            var contentObj = CreatePanel(borderObj.transform, "Content",
                new Color(0.12f, 0.12f, 0.18f, 1f),
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero);

            // 稀有度标签（顶部）
            var rarityObj = CreateTextObject(contentObj.transform, "Rarity",
                "凡品", 16, Color.gray,
                new Vector2(0.5f, 0.5f), new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.95f));
            _cardRarities[index] = rarityObj.GetComponent<Text>();

            // 符文名称（中上部）
            var titleObj = CreateTextObject(contentObj.transform, "Title",
                "符文名称", 24, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.82f));
            _cardTitles[index] = titleObj.GetComponent<Text>();
            _cardTitles[index].fontStyle = FontStyle.Bold;

            // 符文描述（中下部）
            var descObj = CreateTextObject(contentObj.transform, "Description",
                "符文效果描述", 16, new Color(0.8f, 0.8f, 0.8f),
                new Vector2(0.5f, 0.5f), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.58f));
            _cardDescriptions[index] = descObj.GetComponent<Text>();

            // 按钮组件（覆盖整个卡片区域）
            _cardButtons[index] = cardObj.AddComponent<Button>();
            int capturedIndex = index; // 闭包捕获
            _cardButtons[index].onClick.AddListener(() => OnCardClicked(capturedIndex));
            _cardObjects[index] = cardObj;
        }

        // =====================================================================
        //  UI 辅助方法
        // =====================================================================

        private GameObject CreatePanel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = color;
            return obj;
        }

        private GameObject CreateTextObject(Transform parent, string name, string content,
            int fontSize, Color color, Vector2 pivot, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // 备用字体兜底
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return obj;
        }

        // =====================================================================
        //  显示与隐藏
        // =====================================================================

        /// <summary>
        /// 显示三选一面板（暂停游戏）
        /// </summary>
        public void ShowPanel(RuneAcquisitionType type)
        {
            if (!ServiceLocator.TryGet<RuneManager>(out var runeManager)) return;

            var choices = runeManager.GetCurrentDraftChoices();
            if (choices == null) return;

            // 更新标题
            _headerText.text = type == RuneAcquisitionType.KillDrop
                ? "— 属性符文三选一 —"
                : "— 命运符文三选一 —";

            // 填充三张卡片
            for (int i = 0; i < 3; i++)
            {
                if (choices[i] != null)
                {
                    _cardTitles[i].text = choices[i].runeName ?? "未命名";
                    _cardDescriptions[i].text = BuildDescription(choices[i]);
                    _cardRarities[i].text = $"[ {GetRarityName(choices[i].rarity)} ]";
                    _cardRarities[i].color = GetRarityColor(choices[i].rarity);
                    _cardBorders[i].color = GetRarityColor(choices[i].rarity);
                    _cardObjects[i].SetActive(true);
                }
                else
                {
                    _cardObjects[i].SetActive(false);
                }
            }

            _panelRoot.SetActive(true);
            _isShowing = true;
            Time.timeScale = 0f; // 暂停游戏

            Debug.Log("[RuneSelectionPanel] 面板已显示，游戏暂停。");
        }

        /// <summary>
        /// 构建符文描述文本
        /// </summary>
        private string BuildDescription(RuneData_SO rune)
        {
            if (!string.IsNullOrEmpty(rune.description))
                return rune.description;

            // 属性符文：自动生成描述
            if (rune.acquisitionType == RuneAcquisitionType.KillDrop)
            {
                return $"{rune.statBoostType} +{rune.statBoostAmount}";
            }

            // 机制符文：使用 effectTag
            if (!string.IsNullOrEmpty(rune.effectTag))
                return rune.effectTag;

            return "效果未定义";
        }

        /// <summary>
        /// 隐藏面板并恢复游戏
        /// </summary>
        private void HidePanel()
        {
            _panelRoot.SetActive(false);
            _isShowing = false;
            Time.timeScale = 1f; // 恢复游戏

            // 注意：OnRuneDraftCompleteEvent 由 RuneManager.SelectRune/SkipDraft 统一发布，
            // 此处不再重复发布，避免下游订阅者收到两次事件

            Debug.Log("[RuneSelectionPanel] 面板已关闭，游戏恢复。");
        }

        // =====================================================================
        //  按钮回调
        // =====================================================================

        private void OnCardClicked(int index)
        {
            if (!_isShowing) return;

            if (ServiceLocator.TryGet<RuneManager>(out var runeManager))
            {
                runeManager.SelectRune(index);
            }

            HidePanel();
        }

        private void OnSkipClicked()
        {
            if (!_isShowing) return;

            if (ServiceLocator.TryGet<RuneManager>(out var runeManager))
            {
                runeManager.SkipDraft();
            }

            HidePanel();
        }
    }
}
