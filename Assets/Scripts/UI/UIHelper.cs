// ============================================================================
// 逃离魔塔 - UI 工具类 (UIHelper)
// 提供运行时纯代码构建 Canvas UI 的公共方法。
// 从 RuneSelectionPanel/HUDManager 中提取的通用逻辑，消除代码重复。
//
// 所有颜色/字号等视觉参数集中定义，方便后续统一替换美术资产。
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// UI 构建工具类 —— 纯代码创建 Canvas 元素的工厂方法
    /// </summary>
    public static class UIHelper
    {
        // =====================================================================
        //  视觉常量（集中管理，后续美术替换只改这里）
        // =====================================================================

        /// <summary>面板深色背景</summary>
        public static readonly Color PanelBgDark = new Color(0.10f, 0.10f, 0.14f, 0.95f);
        /// <summary>面板中灰背景</summary>
        public static readonly Color PanelBgMid = new Color(0.15f, 0.15f, 0.20f, 0.95f);
        /// <summary>半透明遮罩</summary>
        public static readonly Color OverlayColor = new Color(0, 0, 0, 0.75f);
        /// <summary>分隔线颜色</summary>
        public static readonly Color SeparatorColor = new Color(0.35f, 0.35f, 0.40f, 0.8f);
        /// <summary>空槽位颜色</summary>
        public static readonly Color SlotEmptyColor = new Color(0.20f, 0.20f, 0.25f, 0.9f);
        /// <summary>属性增益颜色（绿色）</summary>
        public static readonly Color StatGainColor = new Color(0.2f, 0.9f, 0.2f);
        /// <summary>属性减损颜色（红色）</summary>
        public static readonly Color StatLossColor = new Color(1.0f, 0.3f, 0.2f);
        /// <summary>普通文字颜色</summary>
        public static readonly Color TextNormalColor = new Color(0.85f, 0.85f, 0.85f);
        /// <summary>灰色次要文字</summary>
        public static readonly Color TextDimColor = new Color(0.55f, 0.55f, 0.55f);

        /// <summary>默认字号</summary>
        public const int FontSizeNormal = 16;
        /// <summary>标题字号</summary>
        public const int FontSizeTitle = 24;
        /// <summary>小字号</summary>
        public const int FontSizeSmall = 14;

        // =====================================================================
        //  缓存字体引用
        // =====================================================================

        private static Font _cachedFont;

        /// <summary>
        /// 获取可用字体（LegacyRuntime → Arial 兜底）
        /// </summary>
        public static Font GetDefaultFont()
        {
            if (_cachedFont != null) return _cachedFont;

            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _cachedFont;
        }

        // =====================================================================
        //  工厂方法
        // =====================================================================

        /// <summary>
        /// 创建带 Image 的面板 RectTransform（锚点驱动布局）
        /// </summary>
        public static GameObject CreatePanel(Transform parent, string name, Color color,
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

        /// <summary>
        /// 创建带 Image 的面板（像素偏移布局，适用于固定尺寸元素）
        /// </summary>
        public static GameObject CreatePanelPixel(Transform parent, string name, Color color,
            Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var img = obj.AddComponent<Image>();
            img.color = color;
            return obj;
        }

        /// <summary>
        /// 创建文字 Text 组件
        /// </summary>
        public static Text CreateText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.font = GetDefaultFont();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>
        /// 创建按钮（带 Image 背景 + Text 标签）
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize, Color bgColor, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = CreatePanel(parent, name, bgColor, anchorMin, anchorMax, Vector2.zero);
            var btn = btnObj.AddComponent<Button>();

            var textComp = CreateText(btnObj.transform, "Label", label,
                fontSize, textColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            return btn;
        }

        /// <summary>
        /// 创建水平分隔线
        /// </summary>
        public static GameObject CreateSeparator(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            return CreatePanel(parent, name, SeparatorColor, anchorMin, anchorMax, Vector2.zero);
        }

        /// <summary>
        /// 创建全屏 Canvas（ScreenSpaceOverlay）
        /// </summary>
        public static Canvas CreateScreenCanvas(Transform parent, string name, int sortingOrder)
        {
            var canvasObj = new GameObject(name);
            canvasObj.transform.SetParent(parent, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
