// ============================================================================
// 逃离魔塔 - Boss 血条 HUD (BossHPBarUI)
// 屏幕顶部居中的 Boss 专用血条。
// 进入 Boss 房间时自动显示，Boss 死亡后淡出消失。
//
// 来源：DesignDocs/07_UI_and_UX.md §1.2
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Entity.Monster;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// Boss 血条 HUD —— 纯代码创建，屏幕顶部居中
    /// </summary>
    public class BossHPBarUI : MonoBehaviour
    {
        // === 配置 ===
        private const float BAR_WIDTH = 500f;
        private const float BAR_HEIGHT = 20f;
        private const float BAR_Y_OFFSET = -40f;     // 距屏幕顶端
        private const float FADE_DURATION = 1.5f;     // 死亡后淡出时间

        // === UI 元素 ===
        private GameObject _root;
        private RectTransform _fillRect;
        private Text _nameText;
        private Text _hpText;
        private Image _fillImage;
        private Image _frameImage;
        private CanvasGroup _canvasGroup;
        private float _fillMaxWidth;

        // === 追踪状态 ===
        private MonsterBase _trackedBoss;
        private bool _isFading;
        private float _fadeTimer;
        private bool _initialized;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化 Boss 血条（由 HUDManager 调用）
        /// </summary>
        public void Initialize(Transform canvasTransform)
        {
            BuildBossBar(canvasTransform);

            // 订阅实体死亡事件
            EventManager.Subscribe<OnEntityDeathEvent>(OnEntityDeath);

            // 初始隐藏
            _root.SetActive(false);
            _initialized = true;

            Debug.Log("[BossHPBar] Boss 血条 HUD 初始化完成");
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityDeathEvent>(OnEntityDeath);
        }

        // =====================================================================
        //  UI 构建
        // =====================================================================

        private void BuildBossBar(Transform parent)
        {
            // 根容器
            _root = new GameObject("BossHPBar_Root", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1);
            rootRect.anchorMax = new Vector2(0.5f, 1);
            rootRect.pivot = new Vector2(0.5f, 1);
            rootRect.anchoredPosition = new Vector2(0, BAR_Y_OFFSET);
            rootRect.sizeDelta = new Vector2(BAR_WIDTH + 20, BAR_HEIGHT + 40);

            _canvasGroup = _root.AddComponent<CanvasGroup>();

            // Boss 名称（血条上方）
            var nameObj = new GameObject("BossName", typeof(RectTransform));
            nameObj.transform.SetParent(_root.transform, false);
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.pivot = new Vector2(0.5f, 1);
            nameRect.anchoredPosition = new Vector2(0, 0);
            nameRect.sizeDelta = new Vector2(BAR_WIDTH, 22);
            _nameText = nameObj.AddComponent<Text>();
            _nameText.text = "BOSS";
            _nameText.fontSize = 16;
            _nameText.color = new Color(1f, 0.3f, 0.3f);
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_nameText.font == null)
                _nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var nameOutline = nameObj.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1, -1);

            // 血条背景框
            var frameObj = new GameObject("BossHP_Frame", typeof(RectTransform));
            frameObj.transform.SetParent(_root.transform, false);
            var frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 1);
            frameRect.anchorMax = new Vector2(0.5f, 1);
            frameRect.pivot = new Vector2(0.5f, 1);
            frameRect.anchoredPosition = new Vector2(0, -24);
            frameRect.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
            _frameImage = frameObj.AddComponent<Image>();
            _frameImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            // 血条边框装饰
            var borderObj = new GameObject("BossHP_Border", typeof(RectTransform));
            borderObj.transform.SetParent(frameObj.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-1, -1);
            borderRect.offsetMax = new Vector2(1, 1);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.6f, 0.15f, 0.15f, 0.7f);
            // 将边框放到最下层
            borderObj.transform.SetAsFirstSibling();

            // 血条填充
            var fillObj = new GameObject("BossHP_Fill", typeof(RectTransform));
            fillObj.transform.SetParent(frameObj.transform, false);
            _fillRect = fillObj.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0, 0);
            _fillRect.anchorMax = new Vector2(0, 1);
            _fillRect.pivot = new Vector2(0, 0.5f);
            _fillRect.offsetMin = new Vector2(2, 2);
            _fillRect.offsetMax = new Vector2(0, -2);
            _fillMaxWidth = BAR_WIDTH - 4f;
            _fillRect.sizeDelta = new Vector2(_fillMaxWidth, 0f);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.color = new Color(0.85f, 0.15f, 0.15f);

            // HP 数值文字（叠加在血条上）
            var hpObj = new GameObject("BossHP_Text", typeof(RectTransform));
            hpObj.transform.SetParent(frameObj.transform, false);
            var hpRect = hpObj.GetComponent<RectTransform>();
            hpRect.anchorMin = Vector2.zero;
            hpRect.anchorMax = Vector2.one;
            hpRect.offsetMin = Vector2.zero;
            hpRect.offsetMax = Vector2.zero;
            _hpText = hpObj.AddComponent<Text>();
            _hpText.text = "";
            _hpText.fontSize = 13;
            _hpText.color = Color.white;
            _hpText.alignment = TextAnchor.MiddleCenter;
            _hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_hpText.font == null)
                _hpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var hpOutline = hpObj.AddComponent<Outline>();
            hpOutline.effectColor = new Color(0, 0, 0, 0.8f);
            hpOutline.effectDistance = new Vector2(1, -1);
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 开始追踪一个 Boss
        /// </summary>
        public void TrackBoss(MonsterBase boss)
        {
            if (boss == null) return;

            _trackedBoss = boss;
            _isFading = false;
            _fadeTimer = 0f;

            // 设置名称
            _nameText.text = boss.gameObject.name ?? "BOSS";

            // 显示
            _root.SetActive(true);
            _canvasGroup.alpha = 1f;

            Debug.Log($"[BossHPBar] 开始追踪: {boss.gameObject.name}");
        }

        // =====================================================================
        //  每帧更新
        // =====================================================================

        private void Update()
        {
            if (!_initialized) return;

            // 淡出动画
            if (_isFading)
            {
                _fadeTimer += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, _fadeTimer / FADE_DURATION);
                if (_fadeTimer >= FADE_DURATION)
                {
                    _root.SetActive(false);
                    _isFading = false;
                    _trackedBoss = null;
                }
                return;
            }

            // 更新血条
            if (_trackedBoss == null || !_root.activeSelf) return;

            float hp = _trackedBoss.CurrentStats.Get(StatType.HP);
            float maxHP = _trackedBoss.CurrentStats.Get(StatType.MaxHP);
            float ratio = maxHP > 0 ? Mathf.Clamp01(hp / maxHP) : 0f;

            // 更新填充宽度
            var sd = _fillRect.sizeDelta;
            sd.x = _fillMaxWidth * ratio;
            _fillRect.sizeDelta = sd;

            // 更新文字
            _hpText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHP)}";

            // 血条颜色随血量变化（红→橙→黄）
            if (ratio < 0.25f)
                _fillImage.color = new Color(0.9f, 0.1f, 0.1f);
            else if (ratio < 0.5f)
                _fillImage.color = new Color(0.9f, 0.4f, 0.1f);
            else
                _fillImage.color = new Color(0.85f, 0.15f, 0.15f);
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        /// <summary>
        /// 实体死亡事件：如果是被追踪的 Boss，触发淡出
        /// </summary>
        private void OnEntityDeath(OnEntityDeathEvent evt)
        {
            if (_trackedBoss == null) return;
            if (evt.EntityID == _trackedBoss.EntityID)
            {
                Debug.Log($"[BossHPBar] Boss 已击杀：{_trackedBoss.gameObject.name}，开始淡出");
                _isFading = true;
                _fadeTimer = 0f;
            }
        }
    }
}
