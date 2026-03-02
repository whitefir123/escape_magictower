// ============================================================================
// 逃离魔塔 - 局内 HUD 管理器 (HUDManager)
// 管理所有局内常驻 UI 元素的显示与更新。
// MVP 阶段使用 Debug.Log 输出占位，后续接入 Canvas + TextMeshPro。
//
// 来源：DesignDocs/07_UI_and_UX.md 第二节
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Entity.Hero;

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

        // === HUD 数据缓存（避免每帧刷新） ===
        private float _cachedHP, _cachedMaxHP;
        private float _cachedMP, _cachedMaxMP;
        private float _cachedRage, _cachedMaxRage;
        private int _cachedLevel;
        private int _cachedExp, _cachedExpToNext;
        private int _cachedGold;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Start()
        {
            // 订阅属性变更事件
            EventManager.Subscribe<OnEntityStatChangedEvent>(OnStatChanged);
            EventManager.Subscribe<OnPlayerLevelUpEvent>(OnLevelUp);
            EventManager.Subscribe<OnGoldGainedEvent>(OnGoldChanged);
        }

        private void Update()
        {
            if (heroReference == null) return;
            UpdateHUDData();
        }

        // =====================================================================
        //  数据刷新
        // =====================================================================

        private void UpdateHUDData()
        {
            var stats = heroReference.CurrentStats;
            float hp = stats.Get(StatType.HP);
            float maxHP = stats.Get(StatType.MaxHP);
            float mp = stats.Get(StatType.MP);
            float maxMP = stats.Get(StatType.MaxMP);
            float rage = stats.Get(StatType.Rage);
            float maxRage = stats.Get(StatType.MaxRage);

            // 仅在值变化时更新 UI（避免无意义的 UI 重绘）
            bool changed = false;

            if (!Mathf.Approximately(hp, _cachedHP) || !Mathf.Approximately(maxHP, _cachedMaxHP))
            {
                _cachedHP = hp;
                _cachedMaxHP = maxHP;
                changed = true;
                // TODO: 更新生命槽 UI 填充比例和文字
            }

            if (!Mathf.Approximately(mp, _cachedMP) || !Mathf.Approximately(maxMP, _cachedMaxMP))
            {
                _cachedMP = mp;
                _cachedMaxMP = maxMP;
                changed = true;
                // TODO: 更新法力槽 UI
            }

            if (!Mathf.Approximately(rage, _cachedRage) || !Mathf.Approximately(maxRage, _cachedMaxRage))
            {
                _cachedRage = rage;
                _cachedMaxRage = maxRage;
                changed = true;
                // TODO: 更新怒气槽 UI（满怒时高亮/发光）
            }

            if (heroReference.CurrentLevel != _cachedLevel)
            {
                _cachedLevel = heroReference.CurrentLevel;
                changed = true;
                // TODO: 更新等级文字
            }

            if (heroReference.CurrentExp != _cachedExp || heroReference.ExpToNextLevel != _cachedExpToNext)
            {
                _cachedExp = heroReference.CurrentExp;
                _cachedExpToNext = heroReference.ExpToNextLevel;
                changed = true;
                // TODO: 更新经验条 UI
            }

            if (heroReference.Gold != _cachedGold)
            {
                _cachedGold = heroReference.Gold;
                changed = true;
                // TODO: 更新金币文字
            }
        }

        // =====================================================================
        //  事件监听
        // =====================================================================

        private void OnStatChanged(OnEntityStatChangedEvent evt)
        {
            // 仅响应玩家属性变更
            if (heroReference != null && evt.EntityID == heroReference.EntityID)
            {
                UpdateHUDData();
            }
        }

        private void OnLevelUp(OnPlayerLevelUpEvent evt)
        {
            Debug.Log($"[HUD] ⬆️ 等级提升！Lv.{evt.NewLevel}");
            // TODO: 播放升级特效 / 提示
        }

        private void OnGoldChanged(OnGoldGainedEvent evt)
        {
            // 金币变化已在 Update 中通过缓存对比检测
        }

        // =====================================================================
        //  公共方法
        // =====================================================================

        /// <summary>
        /// 设置英雄引用（由 GameBootstrapper 或场景管理器调用）
        /// </summary>
        public void SetHeroReference(HeroController hero)
        {
            heroReference = hero;
        }

        /// <summary>
        /// 显示/隐藏 HUD
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityStatChangedEvent>(OnStatChanged);
            EventManager.Unsubscribe<OnPlayerLevelUpEvent>(OnLevelUp);
            EventManager.Unsubscribe<OnGoldGainedEvent>(OnGoldChanged);
        }
    }
}
