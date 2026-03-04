// ============================================================================
// 逃离魔塔 - 英雄物品与经验系统 (HeroInventory)
// 从 HeroController 拆分而来，负责管理金币、钥匙、经验和升级。
//
// 来源：DesignDocs/08_Economy_and_Loot.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄物品与经验系统 —— 管理金币、钥匙持有量、经验升级
    /// 挂载在与 HeroController 相同的 GameObject 上
    /// </summary>
    public class HeroInventory : MonoBehaviour
    {
        // === 主控制器引用 ===
        private HeroController _hero;

        // === 经验与等级 ===
        public int CurrentLevel { get; private set; } = 1;
        public int CurrentExp { get; private set; } = 0;
        public int ExpToNextLevel { get; private set; }

        // === 金币 ===
        public int Gold { get; set; } = 0;

        // === 钥匙持有量 ===
        public int KeyBronze { get; private set; } = 0;
        public int KeySilver { get; private set; } = 0;
        public int KeyGold { get; private set; } = 0;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 由 HeroController 初始化时调用
        /// </summary>
        public void Initialize(HeroController hero)
        {
            _hero = hero;

            CurrentLevel = 1;
            ExpToNextLevel = DamageCalculator.GetExpRequiredForLevel(1);

            // 订阅事件
            EventManager.Subscribe<OnEntityKillEvent>(OnEnemyKilled);
            EventManager.Subscribe<OnExpGainedEvent>(OnExpGained);
            EventManager.Subscribe<OnGoldGainedEvent>(OnGoldGained);
            EventManager.Subscribe<OnKeyDroppedEvent>(OnKeyDropped);
        }

        /// <summary>怪物掉落钥匙 → 自动拾取</summary>
        private void OnKeyDropped(OnKeyDroppedEvent evt)
        {
            AddKey(evt.KeyTier);
        }

        // =====================================================================
        //  经验与升级
        // =====================================================================

        private void OnExpGained(OnExpGainedEvent evt)
        {
            CurrentExp += evt.Amount;

            // 检查升级
            while (CurrentExp >= ExpToNextLevel)
            {
                CurrentExp -= ExpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            CurrentLevel++;
            ExpToNextLevel = DamageCalculator.GetExpRequiredForLevel(CurrentLevel);

            // 升级奖励：全基础属性 +1
            // 通过符文层级实现，创建一个升级加成 StatBlock
            var levelUpBonus = new StatBlock();
            levelUpBonus.Set(StatType.MaxHP, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.ATK, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.MATK, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.DEF, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.MDEF, GameConstants.LEVEL_UP_STAT_BONUS);
            _hero.AddRuneStat(levelUpBonus); // 走管线层级4叠加

            // 广播升级事件（触发机制符文三选一 UI）
            EventManager.Publish(new OnPlayerLevelUpEvent
            {
                Meta = new EventMeta(_hero.EntityID),
                NewLevel = CurrentLevel,
            });

            Debug.Log($"[HeroInventory] 升级！Lv.{CurrentLevel} 全属性+1" +
                      $" 下一级需要 {ExpToNextLevel} EXP");
        }

        // =====================================================================
        //  击杀事件
        // =====================================================================

        private void OnEnemyKilled(OnEntityKillEvent evt)
        {
            if (evt.KillerEntityID != _hero.EntityID) return;
            _hero.SetBattleState(false); // 击杀后暂时视为脱战
        }

        private void OnGoldGained(OnGoldGainedEvent evt)
        {
            Gold += evt.Amount;
        }

        // =====================================================================
        //  钥匙管理
        // =====================================================================

        /// <summary>增加钥匙</summary>
        public void AddKey(DoorTier tier)
        {
            switch (tier)
            {
                case DoorTier.Bronze: KeyBronze++; break;
                case DoorTier.Silver: KeySilver++; break;
                case DoorTier.Gold: KeyGold++; break;
            }
            Debug.Log($"[HeroInventory] 获得 {tier} 钥匙！" +
                      $"铜={KeyBronze} 银={KeySilver} 金={KeyGold}");
        }

        /// <summary>消耗钥匙（开门时调用），返回是否成功</summary>
        public bool ConsumeKey(DoorTier tier)
        {
            switch (tier)
            {
                case DoorTier.Bronze:
                    if (KeyBronze <= 0) return false;
                    KeyBronze--;
                    break;
                case DoorTier.Silver:
                    if (KeySilver <= 0) return false;
                    KeySilver--;
                    break;
                case DoorTier.Gold:
                    if (KeyGold <= 0) return false;
                    KeyGold--;
                    break;
                default:
                    return false;
            }
            Debug.Log($"[HeroInventory] 使用 {tier} 钥匙 | 剩余：铜={KeyBronze} 银={KeySilver} 金={KeyGold}");
            return true;
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityKillEvent>(OnEnemyKilled);
            EventManager.Unsubscribe<OnExpGainedEvent>(OnExpGained);
            EventManager.Unsubscribe<OnGoldGainedEvent>(OnGoldGained);
        }
    }
}
