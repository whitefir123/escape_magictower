// ============================================================================
// 逃离魔塔 - 胜利界面 (VictoryScreen)
// 9 层通关后弹出的全屏结算 UI。
// 运行时纯代码创建 Canvas UI（无需预制体），对标 DeathScreen 风格。
//
// 显示内容：
//   - "逃出生天" 标题
//   - 本次成绩：等级、击杀数、存活时间、金币
//   - "再来一局" 按钮
//
// 来源：DesignDocs/07_UI_and_UX.md
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using EscapeTheTower.Core;
using EscapeTheTower.Entity.Hero;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 胜利结算界面 —— 通关后全屏展示
    /// </summary>
    public class VictoryScreen : MonoBehaviour
    {
        [Header("=== 引用 ===")]
        [SerializeField] private HeroController heroReference;

        // === 统计数据 ===
        private int _totalKills;
        private int _totalGold;
        private int _finalLevel;
        private float _totalTime;
        private bool _isVisible;

        // === UI 元素 ===
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Text _statsText;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            // 默认隐藏
            gameObject.SetActive(true); // 保持激活以接收事件

            EventManager.Subscribe<OnEntityKillEvent>(OnKill);
            EventManager.Subscribe<OnVictoryEvent>(OnVictory);
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityKillEvent>(OnKill);
            EventManager.Unsubscribe<OnVictoryEvent>(OnVictory);
        }

        private void Update()
        {
            if (!_isVisible)
            {
                _totalTime += Time.deltaTime;
            }
        }

        // =====================================================================
        //  事件
        // =====================================================================

        private void OnKill(OnEntityKillEvent evt)
        {
            // 统计击杀数（仅计玩家击杀）
            if (heroReference != null && evt.KillerEntityID == heroReference.EntityID)
            {
                _totalKills++;
            }
        }

        private void OnVictory(OnVictoryEvent evt)
        {
            ShowVictory();
        }

        // =====================================================================
        //  显示胜利界面
        // =====================================================================

        /// <summary>
        /// 显示胜利结算界面
        /// </summary>
        public void ShowVictory()
        {
            if (_isVisible) return;
            _isVisible = true;
            Time.timeScale = 0f; // 暂停游戏

            if (heroReference == null)
            {
                heroReference = FindAnyObjectByType<HeroController>();
            }

            if (heroReference != null)
            {
                _finalLevel = heroReference.CurrentLevel;
                _totalGold = heroReference.Gold;
            }

            Debug.Log("===========================================");
            Debug.Log("               🏆 胜利！                   ");
            Debug.Log("===========================================");
            Debug.Log($"  等级: Lv.{_finalLevel}");
            Debug.Log($"  击杀: {_totalKills}");
            Debug.Log($"  金币: {_totalGold}");
            Debug.Log($"  用时: {FormatTime(_totalTime)}");
            Debug.Log("===========================================");

            BuildUI();
        }

        // =====================================================================
        //  UI 构建（对标 DeathScreen 风格，金色边框表示胜利）
        // =====================================================================

        private void BuildUI()
        {
            // --- Canvas ---
            var canvasObj = new GameObject("VictoryScreenCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 覆盖 HUD
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // CanvasGroup 用于淡入
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
            var overlayImg = overlayObj.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.75f);

            // --- 中央面板 ---
            var panelObj = new GameObject("Panel", typeof(RectTransform));
            panelObj.transform.SetParent(canvasObj.transform, false);
            var panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500, 400);
            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // --- 金色边框 ---
            var borderObj = new GameObject("Border", typeof(RectTransform));
            borderObj.transform.SetParent(panelObj.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.9f, 0.75f, 0.2f, 0.85f); // 金色
            borderObj.transform.SetAsFirstSibling();

            // --- 标题 ---
            CreateText(panelObj.transform, "Title",
                "🏆 逃出生天！",
                new Vector2(0, 140), 38, new Color(1f, 0.85f, 0.2f));

            // --- 分隔线 ---
            var lineObj = new GameObject("Separator", typeof(RectTransform));
            lineObj.transform.SetParent(panelObj.transform, false);
            var lineRect = lineObj.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = new Vector2(0, 100);
            lineRect.sizeDelta = new Vector2(400, 2);
            var lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.9f, 0.75f, 0.2f, 0.5f);

            // --- 成绩统计 ---
            string stats =
                $"等  级：Lv.{_finalLevel}\n" +
                $"击  杀：{_totalKills}\n" +
                $"金  币：{_totalGold}\n" +
                $"用  时：{FormatTime(_totalTime)}";

            _statsText = CreateText(panelObj.transform, "Stats",
                stats,
                new Vector2(0, 20), 22, Color.white);

            // --- 再来一局按钮 ---
            var btnObj = new GameObject("RestartButton", typeof(RectTransform));
            btnObj.transform.SetParent(panelObj.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0, -140);
            btnRect.sizeDelta = new Vector2(220, 50);
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.85f, 0.7f, 0.15f, 1f); // 金色按钮

            var restartButton = btnObj.AddComponent<Button>();
            restartButton.targetGraphic = btnImg;

            // 按钮悬停颜色
            var colors = restartButton.colors;
            colors.normalColor = new Color(0.85f, 0.7f, 0.15f);
            colors.highlightedColor = new Color(1f, 0.85f, 0.3f);
            colors.pressedColor = new Color(0.65f, 0.5f, 0.1f);
            restartButton.colors = colors;

            // 按钮文字
            var btnText = CreateText(btnObj.transform, "BtnText",
                "再来一局",
                Vector2.zero, 24, new Color(0.1f, 0.1f, 0.1f));
            var btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            btnTextRect.anchoredPosition = Vector2.zero;

            restartButton.onClick.AddListener(OnContinueClicked);

            // 淡入动画
            StartCoroutine(FadeIn(0.6f));
        }

        // =====================================================================
        //  按钮回调
        // =====================================================================

        /// <summary>
        /// 点击"再来一局"
        /// </summary>
        public void OnContinueClicked()
        {
            Time.timeScale = 1f;
            _isVisible = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            int secs = (int)(seconds % 60f);
            return $"{minutes:D2}:{secs:D2}";
        }

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
            rect.sizeDelta = new Vector2(450, Mathf.Max(fontSize * 6, 140));

            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.lineSpacing = 1.5f;
            text.font = UIHelper.GetDefaultFont();

            // 描边增强可读性
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            return text;
        }

        private IEnumerator FadeIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }
    }
}
