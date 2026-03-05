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
    /// 符文运行时持有记录 —— 与 SO 资产隔离，防止编辑器数据污染
    /// </summary>
    public class AcquiredRuneEntry
    {
        /// <summary>符文配置数据（只读引用，不修改 SO 字段）</summary>
        public RuneData_SO Data { get; }
        /// <summary>当前等级（运行时叠加，初始为 1）</summary>
        public int Level { get; set; }

        public AcquiredRuneEntry(RuneData_SO data)
        {
            Data = data;
            Level = 1;
        }
    }

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

        /// <summary>当前已获取的所有符文（运行时数据，不修改 SO 资产）</summary>
        public List<AcquiredRuneEntry> AcquiredRunes { get; private set; } = new List<AcquiredRuneEntry>();

        // === 保底计数器 ===
        private int _pityCounter = 0;

        // === 当前三选一候选 ===
        private RuneData_SO[] _currentDraftChoices;

        // === 当前英雄职业（用于职业专属符文过滤）===
        private HeroClass _currentHeroClass;

        // === 是否已初始化（防止重复订阅）===
        private bool _isInitialized = false;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            // 使用 LogError 确保日志不被过滤 —— 上线前改回 Log
            Debug.LogError($"[RuneManager] Awake 触发！" +
                           $" 属性池={attributeRunePool.Count}" +
                           $" 机制池={mechanismRunePool.Count}" +
                           $" 专属池={classSpecificRunePool.Count}" +
                           $" InstanceID={GetInstanceID()}");
        }

        // =====================================================================
        //  生命周期：自初始化回退
        // =====================================================================

        /// <summary>
        /// 防御性自初始化：如果 GameBootstrapper 未调用 Initialize()，
        /// 则在 Start() 中自动回退初始化，确保事件订阅和池数据可用
        /// </summary>
        private void Start()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[RuneManager] 未通过 GameBootstrapper 初始化，执行自动回退初始化...");
                Initialize(HeroClass.VagabondSwordsman);
            }
        }

        // =====================================================================
        //  初始化
        // =====================================================================

        public void Initialize(HeroClass heroClass)
        {
            // 防止重复初始化（GameBootstrapper + Start 双保险）
            if (_isInitialized)
            {
                Debug.Log("[RuneManager] 已初始化，跳过重复调用。");
                return;
            }
            _isInitialized = true;

            _currentHeroClass = heroClass;
            AcquiredRunes.Clear();
            _pityCounter = 0;

            // 订阅事件
            EventManager.Subscribe<OnEntityKillEvent>(OnEntityKilled);
            EventManager.Subscribe<OnPlayerLevelUpEvent>(OnPlayerLevelUp);

            Debug.Log($"[RuneManager] 初始化完成，职业={_currentHeroClass}" +
                      $" | 属性池={attributeRunePool.Count}" +
                      $" | 机制池={mechanismRunePool.Count}" +
                      $" | 专属池={classSpecificRunePool.Count}" +
                      $" | InstanceID={GetInstanceID()}");
        }

        // =====================================================================
        //  击杀触发（属性符文）
        // =====================================================================

        private void OnEntityKilled(OnEntityKillEvent evt)
        {
            // 40% 概率触发属性符文三选一
            float roll = Random.value;
            Debug.Log($"[RuneManager] 击杀事件接收！roll={roll:F3} 门槛={GameConstants.RUNE_DROP_CHANCE} 属性池={attributeRunePool.Count}");
            if (roll <= GameConstants.RUNE_DROP_CHANCE)
            {
                GenerateAttributeRuneDraft();
            }
        }

        /// <summary>
        /// 生成属性符文三选一候选
        /// 两阶段抽取：先按权重roll品质，再从该品质中随机选类型（同类不重复）
        /// </summary>
        private void GenerateAttributeRuneDraft()
        {
            if (attributeRunePool.Count < 3)
            {
                Debug.LogWarning("[RuneManager] 属性符文池不足 3 个！");
                return;
            }

            _currentDraftChoices = new RuneData_SO[3];

            // 已选中的 statBoostType 集合，防止三张卡出现同一属性
            var usedTypes = new HashSet<StatType>();

            for (int i = 0; i < 3; i++)
            {
                // 第一阶段：按品质权重roll稀有度
                RuneRarity rolledRarity = RollRarity();

                // 从池中筛选：品质匹配 + 类型不重复
                var candidates = new List<RuneData_SO>();
                foreach (var rune in attributeRunePool)
                {
                    if (rune.rarity == rolledRarity && !usedTypes.Contains(rune.statBoostType))
                        candidates.Add(rune);
                }

                // 如果该品质下所有类型都已被选过，降级到任意品质的未选类型
                if (candidates.Count == 0)
                {
                    foreach (var rune in attributeRunePool)
                    {
                        if (!usedTypes.Contains(rune.statBoostType))
                            candidates.Add(rune);
                    }
                }

                if (candidates.Count == 0) break; // 极端情况：池中类型不够3种

                // 第二阶段：从候选中随机选一个
                var selected = candidates[Random.Range(0, candidates.Count)];
                _currentDraftChoices[i] = selected;
                usedTypes.Add(selected.statBoostType);
            }

            Debug.Log($"[RuneManager] 属性符文三选一：" +
                      $"{FormatRuneChoice(0)} | " +
                      $"{FormatRuneChoice(1)} | " +
                      $"{FormatRuneChoice(2)}");

            // 暂停游戏，通知 UI 弹出三选一面板
            EventManager.Publish(new OnRuneDraftReadyEvent
            {
                Meta = new EventMeta(0),
                AcquisitionType = RuneAcquisitionType.KillDrop,
            });
        }

        /// <summary>格式化符文选项日志（含品质和数值）</summary>
        private string FormatRuneChoice(int index)
        {
            var rune = _currentDraftChoices[index];
            if (rune == null) return "空";
            return $"{rune.runeName}[{rune.rarity}]+{rune.statBoostAmount}";
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

            // 暂停游戏，通知 UI 弹出三选一面板
            EventManager.Publish(new OnRuneDraftReadyEvent
            {
                Meta = new EventMeta(0),
                AcquisitionType = RuneAcquisitionType.LevelUp,
            });
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
            // 使用 AcquiredRuneEntry 隔离运行时数据，绝不修改 SO 资产
            var existing = AcquiredRunes.Find(r => r.Data.runeID == selectedRune.runeID);
            if (existing != null)
            {
                existing.Level++;
                Debug.Log($"[RuneManager] 符文 '{existing.Data.runeName}' 升级至 Lv.{existing.Level}");
            }
            else
            {
                AcquiredRunes.Add(new AcquiredRuneEntry(selectedRune));
                Debug.Log($"[RuneManager] 获得符文 '{selectedRune.runeName}' ({selectedRune.rarity})");
            }

            _currentDraftChoices = null;

            // 通知系统恢复游戏，携带选中的符文数据供英雄注入管线
            EventManager.Publish(new OnRuneDraftCompleteEvent
            {
                Meta = new EventMeta(0),
                SelectedRune = selectedRune,
            });
        }

        /// <summary>
        /// 放弃本次三选一（不选择任何符文）
        /// </summary>
        public void SkipDraft()
        {
            _currentDraftChoices = null;
            Debug.Log("[RuneManager] 玩家放弃了本次符文选择。");

            // 通知系统恢复游戏（放弃时 SelectedRune 为 null）
            EventManager.Publish(new OnRuneDraftCompleteEvent
            {
                Meta = new EventMeta(0),
                SelectedRune = null,
            });
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
