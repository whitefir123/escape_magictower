// ============================================================================
// 逃离魔塔 - 符文管理器 (RuneManager)
// 管理局内符文的获取、三选一抽取、稀有度权重、保底计数。
// 两大获取途径：
//   1. 击杀掉落 → 属性符文（40% 概率触发三选一）
//   2. 升级获取 → 机制符文（必定触发三选一）
//
// 来源：DesignDocs/05_Runes_and_MetaProgression.md
//       GameData_Blueprints/08_Destiny_Rune_System.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Rune
{
    /// <summary>
    /// 符文管理器 —— 核心抽取与管理逻辑
    /// </summary>
    public class RuneManager : MonoBehaviour
    {
        [Header("=== 符文池 ===")]
        [Tooltip("全部可用的属性符文 SO 列表")]
        [SerializeField] private List<RuneData_SO> attributeRunePool = new List<RuneData_SO>();

        [Tooltip("全部可用的机制符文 SO 列表（通用）")]
        [SerializeField] private List<RuneData_SO> mechanismRunePool = new List<RuneData_SO>();

        [Tooltip("全部可用的职业专属机制符文 SO 列表")]
        [SerializeField] private List<RuneData_SO> classSpecificRunePool = new List<RuneData_SO>();

        /// <summary>当前已获取的所有符文</summary>
        public List<RuneData_SO> AcquiredRunes { get; private set; } = new List<RuneData_SO>();

        // === 保底计数器 ===
        private int _pityCounter = 0;

        // === 当前三选一候选 ===
        private RuneData_SO[] _currentDraftChoices;

        // === 当前英雄职业（用于职业专属符文过滤）===
        private HeroClass _currentHeroClass;

        // =====================================================================
        //  初始化
        // =====================================================================

        public void Initialize(HeroClass heroClass)
        {
            _currentHeroClass = heroClass;
            AcquiredRunes.Clear();
            _pityCounter = 0;

            // 订阅事件
            EventManager.Subscribe<OnEntityKillEvent>(OnEntityKilled);
            EventManager.Subscribe<OnPlayerLevelUpEvent>(OnPlayerLevelUp);

            Debug.Log($"[RuneManager] 初始化完成，职业={_currentHeroClass}");
        }

        // =====================================================================
        //  击杀触发（属性符文）
        // =====================================================================

        private void OnEntityKilled(OnEntityKillEvent evt)
        {
            // 仅响应玩家击杀
            // 40% 概率触发属性符文三选一
            if (Random.value <= GameConstants.RUNE_DROP_CHANCE)
            {
                GenerateAttributeRuneDraft();
            }
        }

        /// <summary>
        /// 生成属性符文三选一候选
        /// </summary>
        private void GenerateAttributeRuneDraft()
        {
            if (attributeRunePool.Count < 3)
            {
                Debug.LogWarning("[RuneManager] 属性符文池不足 3 个！");
                return;
            }

            _currentDraftChoices = new RuneData_SO[3];
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < 3; i++)
            {
                int idx;
                int safety = 100;
                do
                {
                    idx = Random.Range(0, attributeRunePool.Count);
                    safety--;
                } while (usedIndices.Contains(idx) && safety > 0);

                usedIndices.Add(idx);
                _currentDraftChoices[i] = attributeRunePool[idx];
            }

            Debug.Log($"[RuneManager] 属性符文三选一：" +
                      $"{_currentDraftChoices[0].runeName} | " +
                      $"{_currentDraftChoices[1].runeName} | " +
                      $"{_currentDraftChoices[2].runeName}");

            // TODO: 暂停游戏，弹出三选一 UI
        }

        // =====================================================================
        //  升级触发（机制符文）
        // =====================================================================

        private void OnPlayerLevelUp(OnPlayerLevelUpEvent evt)
        {
            GenerateMechanismRuneDraft();
        }

        /// <summary>
        /// 生成机制符文三选一候选（基于稀有度权重）
        /// </summary>
        private void GenerateMechanismRuneDraft()
        {
            _currentDraftChoices = new RuneData_SO[3];

            for (int i = 0; i < 3; i++)
            {
                // 10% 概率强制插入职业专属符文
                if (Random.value < GameConstants.CLASS_RUNE_FORCE_CHANCE && classSpecificRunePool.Count > 0)
                {
                    var filtered = classSpecificRunePool.FindAll(r => r.restrictedClass == _currentHeroClass);
                    if (filtered.Count > 0)
                    {
                        _currentDraftChoices[i] = filtered[Random.Range(0, filtered.Count)];
                        continue;
                    }
                }

                // 基于稀有度权重从通用池抽取
                RuneRarity rarity = RollRarity();
                var candidates = mechanismRunePool.FindAll(r => r.rarity == rarity);

                if (candidates.Count > 0)
                {
                    _currentDraftChoices[i] = candidates[Random.Range(0, candidates.Count)];
                }
                else
                {
                    // 回退到凡品
                    candidates = mechanismRunePool.FindAll(r => r.rarity == RuneRarity.Common);
                    if (candidates.Count > 0)
                    {
                        _currentDraftChoices[i] = candidates[Random.Range(0, candidates.Count)];
                    }
                }
            }

            Debug.Log($"[RuneManager] 机制符文三选一：" +
                      $"{_currentDraftChoices[0]?.runeName ?? "空"} | " +
                      $"{_currentDraftChoices[1]?.runeName ?? "空"} | " +
                      $"{_currentDraftChoices[2]?.runeName ?? "空"}");

            // TODO: 暂停游戏，弹出三选一 UI
        }

        // =====================================================================
        //  稀有度权重抽取 + 保底
        // =====================================================================

        /// <summary>
        /// 根据权重表抽取稀有度，含保底机制
        /// </summary>
        private RuneRarity RollRarity()
        {
            _pityCounter++;

            // 保底触发：连续 N 次未出史诗及以上，强制提升
            if (_pityCounter >= GameConstants.RUNE_PITY_THRESHOLD)
            {
                _pityCounter = 0;
                return RuneRarity.Epic; // 保底至少出史诗
            }

            // 正常权重抽取
            float roll = Random.value;
            float cumulative = 0f;

            cumulative += GameConstants.RUNE_WEIGHT_LEGENDARY;
            if (roll < cumulative)
            {
                _pityCounter = 0;
                return RuneRarity.Legendary;
            }

            cumulative += GameConstants.RUNE_WEIGHT_EPIC;
            if (roll < cumulative)
            {
                _pityCounter = 0;
                return RuneRarity.Epic;
            }

            cumulative += GameConstants.RUNE_WEIGHT_EXCEPTIONAL;
            if (roll < cumulative) return RuneRarity.Exceptional;

            cumulative += GameConstants.RUNE_WEIGHT_RARE;
            if (roll < cumulative) return RuneRarity.Rare;

            return RuneRarity.Common;
        }

        // =====================================================================
        //  选择符文
        // =====================================================================

        /// <summary>
        /// 玩家从三选一中选择一枚符文
        /// </summary>
        /// <param name="choiceIndex">选择的索引 [0, 2]</param>
        public void SelectRune(int choiceIndex)
        {
            if (_currentDraftChoices == null || choiceIndex < 0 || choiceIndex > 2)
            {
                Debug.LogError("[RuneManager] 无效的符文选择！");
                return;
            }

            var selectedRune = _currentDraftChoices[choiceIndex];
            if (selectedRune == null) return;

            // 检查是否已拥有（重复获取升级）
            var existing = AcquiredRunes.Find(r => r.runeID == selectedRune.runeID);
            if (existing != null)
            {
                existing.currentLevel++;
                Debug.Log($"[RuneManager] 符文 '{existing.runeName}' 升级至 Lv.{existing.currentLevel}");
            }
            else
            {
                AcquiredRunes.Add(selectedRune);
                Debug.Log($"[RuneManager] 获得符文 '{selectedRune.runeName}' ({selectedRune.rarity})");
            }

            _currentDraftChoices = null;

            // TODO: 关闭三选一 UI，恢复游戏
            // TODO: 通知 EntityBase 添加符文到管线层级4
        }

        /// <summary>
        /// 放弃本次三选一（不选择任何符文）
        /// </summary>
        public void SkipDraft()
        {
            _currentDraftChoices = null;
            Debug.Log("[RuneManager] 玩家放弃了本次符文选择。");
        }

        /// <summary>
        /// 获取当前三选一候选（供 UI 读取）
        /// </summary>
        public RuneData_SO[] GetCurrentDraftChoices()
        {
            return _currentDraftChoices;
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityKillEvent>(OnEntityKilled);
            EventManager.Unsubscribe<OnPlayerLevelUpEvent>(OnPlayerLevelUp);
        }
    }
}
