// ============================================================================
// 逃离魔塔 - 死亡结算界面 (DeathScreen)
// 玩家死亡后弹出的全屏结算 UI。
// 运行时纯代码创建 Canvas UI（无需预制体），与 HUDManager 风格统一。
//
// 显示内容：
//   - "你已阵亡" 标题
//   - 本次成绩：等级、击杀数、存活时间、金币
//   - "重新开始" 按钮
//
// 来源：DesignDocs/07_UI_and_UX.md
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 死亡结算界面 —— 玩家阵亡后全屏展示
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        // === UI 元素引用 ===
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Image _overlayImage;
        private Text _titleText;
        private Text _statsText;
        private Button _restartButton;

        // === 数据 ===
        private bool _isShowing;

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 显示死亡结算界面
        /// </summary>
        /// <param name="level">当前等级</param>
        /// <param name="gold">持有金币</param>
        /// <param name="survivalTime">存活时间（秒）</param>
        public void Show(int level, int gold, float survivalTime)
        {
            if (_isShowing) return;
            _isShowing = true;

            BuildUI();
            PopulateStats(level, gold, survivalTime);

            // 淡入动画
            StartCoroutine(FadeIn(0.5f));

            Debug.Log("[DeathScreen] 死亡结算界面已显示。");
        }

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildUI()
        {
            // --- Canvas ---
            var canvasObj = new GameObject("DeathScreenCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 确保覆盖 HUD
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // CanvasGroup 用于淡入控制
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            // --- 半透明黑幕 ---
            var overlayObj = new GameObject("Overlay", typeof(RectTransform));
            overlayObj.transform.SetParent(canvasObj.transform, false);
            var overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _overlayImage = overlayObj.AddComponent<Image>();
            _overlayImage.color = new Color(0f, 0f, 0f, 0.75f);

            // --- 中央面板 ---
            var panelObj = new GameObject("Panel", typeof(RectTransform));
            panelObj.transform.SetParent(canvasObj.transform, false);
            var panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500, 360);
            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // --- 面板边框 ---
            var borderObj = new GameObject("Border", typeof(RectTransform));
            borderObj.transform.SetParent(panelObj.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            // 让边框在面板后面
            borderObj.transform.SetAsFirstSibling();

            // --- 标题 ---
            _titleText = CreateText(panelObj.transform, "Title",
                "你已阵亡",
                new Vector2(0, 120), 42, new Color(0.9f, 0.2f, 0.2f));

            // --- 分隔线 ---
            var lineObj = new GameObject("Separator", typeof(RectTransform));
            lineObj.transform.SetParent(panelObj.transform, false);
            var lineRect = lineObj.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = new Vector2(0, 80);
            lineRect.sizeDelta = new Vector2(400, 2);
            var lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // --- 成绩统计 ---
            _statsText = CreateText(panelObj.transform, "Stats",
                "加载中...",
                new Vector2(0, 10), 22, Color.white);

            // --- 重新开始按钮 ---
            var btnObj = new GameObject("RestartButton", typeof(RectTransform));
            btnObj.transform.SetParent(panelObj.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0, -120);
            btnRect.sizeDelta = new Vector2(220, 50);
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.8f, 0.25f, 0.25f, 1f);

            _restartButton = btnObj.AddComponent<Button>();
            _restartButton.targetGraphic = btnImg;

            // 按钮悬停颜色
            var colors = _restartButton.colors;
            colors.normalColor = new Color(0.8f, 0.25f, 0.25f);
            colors.highlightedColor = new Color(1f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.6f, 0.15f, 0.15f);
            _restartButton.colors = colors;

            // 按钮文字
            var btnText = CreateText(btnObj.transform, "BtnText",
                "重新开始",
                Vector2.zero, 24, Color.white);
            var btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            btnTextRect.anchoredPosition = Vector2.zero;

            _restartButton.onClick.AddListener(OnRestartClicked);
        }

        /// <summary>填充成绩数据</summary>
        private void PopulateStats(int level, int gold, float survivalTime)
        {
            // 格式化存活时间
            int minutes = Mathf.FloorToInt(survivalTime / 60f);
            int seconds = Mathf.FloorToInt(survivalTime % 60f);

            string stats =
                $"等  级：Lv.{level}\n" +
                $"金  币：{gold}\n" +
                $"存活时间：{minutes:00}:{seconds:00}";

            if (_statsText != null)
                _statsText.text = stats;
        }

        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>创建文字元素</summary>
        private Text CreateText(Transform parent, string name, string content,
            Vector2 position, int fontSize, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(450, Mathf.Max(fontSize * 6, 120));

            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.lineSpacing = 1.5f;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 描边增强可读性
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            return text;
        }

        /// <summary>淡入协程</summary>
        private IEnumerator FadeIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 以兼容 TimeScale=0
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        /// <summary>重新开始按钮点击回调</summary>
        private void OnRestartClicked()
        {
            Debug.Log("[DeathScreen] 重新开始！");
            // 确保 TimeScale 恢复正常
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
