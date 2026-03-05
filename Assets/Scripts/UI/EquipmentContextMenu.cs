// ============================================================================
// 逃离魔塔 - 右键菜单 (EquipmentContextMenu)
// 右键点击背包物品时弹出的浮动操作菜单。
// 装备类：[装备] [丢弃]  |  消耗品类：[使用] [丢弃]
// 点击外部区域或执行操作后自动关闭。
//
// 来源：DesignDocs/07_UI_and_UX.md （交互规范）
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 右键浮动菜单 —— 通用的 2~3 选项弹出菜单
    /// </summary>
    public class EquipmentContextMenu : MonoBehaviour
    {
        // =====================================================================
        //  常量
        // =====================================================================

        private const float MENU_WIDTH = 120f;
        private const float BUTTON_HEIGHT = 32f;
        private const float PADDING = 4f;

        // =====================================================================
        //  UI 引用
        // =====================================================================

        private Canvas _canvas;
        private GameObject _menuRoot;
        private RectTransform _menuRect;
        private readonly System.Collections.Generic.List<GameObject> _buttons
            = new System.Collections.Generic.List<GameObject>();

        // 点击外部关闭的遮罩
        private GameObject _clickCatcher;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _canvas = UIHelper.CreateScreenCanvas(transform, "ContextMenuCanvas", 210);

            // 全屏透明点击拦截层（点击外部关闭菜单）
            _clickCatcher = UIHelper.CreatePanel(_canvas.transform, "ClickCatcher",
                new Color(0, 0, 0, 0.01f), Vector2.zero, Vector2.one, Vector2.zero);
            var catcherBtn = _clickCatcher.AddComponent<Button>();
            catcherBtn.onClick.AddListener(Hide);
            _clickCatcher.SetActive(false);

            // 菜单容器
            _menuRoot = new GameObject("MenuRoot", typeof(RectTransform));
            _menuRoot.transform.SetParent(_canvas.transform, false);
            _menuRect = _menuRoot.GetComponent<RectTransform>();
            _menuRect.anchorMin = Vector2.zero;
            _menuRect.anchorMax = Vector2.zero;
            _menuRect.pivot = new Vector2(0f, 1f);

            var bg = _menuRoot.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            var outline = _menuRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.6f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            _menuRoot.SetActive(false);
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 在指定屏幕位置显示菜单
        /// </summary>
        /// <param name="screenPos">弹出位置（屏幕坐标）</param>
        /// <param name="options">菜单选项数组，每项为 (标签, 回调)</param>
        public void Show(Vector2 screenPos, params (string label, Action callback)[] options)
        {
            ClearButtons();

            float totalHeight = PADDING * 2 + options.Length * (BUTTON_HEIGHT + PADDING);

            for (int i = 0; i < options.Length; i++)
            {
                var opt = options[i];
                float yPos = -PADDING - i * (BUTTON_HEIGHT + PADDING);
                CreateMenuButton(opt.label, opt.callback, yPos);
            }

            _menuRect.sizeDelta = new Vector2(MENU_WIDTH, totalHeight);

            // 坐标转换
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPos);
            _menuRect.anchoredPosition = localPos;

            _clickCatcher.SetActive(true);
            _menuRoot.SetActive(true);
        }

        /// <summary>隐藏菜单</summary>
        public void Hide()
        {
            if (_menuRoot != null) _menuRoot.SetActive(false);
            if (_clickCatcher != null) _clickCatcher.SetActive(false);
        }

        /// <summary>菜单是否正在显示</summary>
        public bool IsShowing => _menuRoot != null && _menuRoot.activeSelf;

        // =====================================================================
        //  内部方法
        // =====================================================================

        private void CreateMenuButton(string label, Action callback, float yPos)
        {
            var btnObj = new GameObject($"Btn_{label}", typeof(RectTransform));
            btnObj.transform.SetParent(_menuRoot.transform, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(PADDING, yPos);
            rect.sizeDelta = new Vector2(-PADDING * 2, BUTTON_HEIGHT);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.28f, 0.9f);

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                callback?.Invoke();
                Hide();
            });

            var textComp = UIHelper.CreateText(btnObj.transform, "Label", label,
                UIHelper.FontSizeSmall, UIHelper.TextNormalColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);

            _buttons.Add(btnObj);
        }

        private void ClearButtons()
        {
            foreach (var btn in _buttons)
            {
                if (btn != null) Destroy(btn);
            }
            _buttons.Clear();
        }
    }
}
