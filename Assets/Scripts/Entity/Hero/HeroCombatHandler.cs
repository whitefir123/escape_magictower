// ============================================================================
// 逃离魔塔 - 英雄战斗处理器 (HeroCombatHandler)
// 从 HeroController 拆分而来，负责碰撞回弹战斗和墙壁（门）交互。
//
// 来源：DesignDocs/03_Combat_System.md §1.1.1
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄战斗处理器 —— 管理碰撞回弹战斗、墙壁交互、攻击冷却
    /// 挂载在与 HeroController 相同的 GameObject 上
    /// </summary>
    public class HeroCombatHandler : MonoBehaviour
    {
        // === 主控制器引用 ===
        private HeroController _hero;
        private HeroSkillHandler _skillHandler;
        private GridMovement _gridMovement;

        // === 攻击冷却（基于 AttackSpeed 属性） ===
        private float _normalAttackCooldownTimer;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 由 HeroController 初始化时调用
        /// </summary>
        public void Initialize(HeroController hero, HeroSkillHandler skillHandler, GridMovement gridMovement)
        {
            _hero = hero;
            _skillHandler = skillHandler;
            _gridMovement = gridMovement;

            // 注册碰撞回调
            _gridMovement.OnMoveBlocked += OnGridMoveBlocked;
            _gridMovement.OnWallBlocked += OnWallBlocked;
        }

        // =====================================================================
        //  每帧更新（由 HeroController.Update 调用）
        // =====================================================================

        /// <summary>
        /// 攻击冷却每帧 Tick
        /// </summary>
        public void Tick()
        {
            if (_normalAttackCooldownTimer > 0f)
            {
                _normalAttackCooldownTimer -= Time.deltaTime;
                if (_normalAttackCooldownTimer <= 0f)
                {
                    _gridMovement.AllowBump = true; // 冷却结束，允许回弹
                }
            }
        }

        // =====================================================================
        //  格子碰撞交互（类魔塔核心：碰撞回弹 = 主动攻击）
        //  由 GridMovement.OnMoveBlocked 事件驱动
        //  来源：DesignDocs/03_Combat_System.md §1.1.1
        // =====================================================================

        /// <summary>
        /// 格子移动被阻挡时触发（碰撞回弹战斗）
        /// </summary>
        private void OnGridMoveBlocked(Vector2Int blockedTile, GameObject blocker)
        {
            if (!_hero.IsAlive || blocker == null) return;

            // === 宝箱交互检查 ===
            var chest = blocker.GetComponent<EscapeTheTower.Entity.ChestEntity>();
            if (chest != null)
            {
                chest.TryOpen(_hero.EntityID);
                return; // 宝箱不触发战斗
            }

            // === 战斗交互（原有逻辑） ===
            var monster = blocker.GetComponent<EscapeTheTower.Entity.Monster.MonsterBase>();
            if (monster == null || !monster.IsAlive) return;

            Debug.Log($"[战斗] 碰撞回弹！玩家 → {monster.gameObject.name}");

            // 设置攻击冷却（冷却期间禁止回弹，但可自由移动）
            float atkSpeed = _hero.CurrentStats.Get(StatType.AttackSpeed);
            if (atkSpeed <= 0f) atkSpeed = 1f;
            _normalAttackCooldownTimer = 1f / atkSpeed;
            _gridMovement.AllowBump = false;

            // 玩家碰撞怪物 = 主动攻击
            _hero.SetBattleState(true);
            _skillHandler.OnEnterBattle();

            // ① 先触发怪物被动反击（魔塔核心：攻击和反击同时发生，秒杀也会被反击）
            monster.OnHitByPlayer(_hero);

            // ② 再结算玩家伤害
            var damageResult = DamageCalculator.Calculate(
                _hero.CurrentStats,
                monster.CurrentStats,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Physical
            );

            if (!DamageCalculator.CheckDodge(monster.CurrentStats))
            {
                monster.TakeDamage(damageResult, _hero.EntityID);
                _skillHandler.OnNormalAttackHit();

                Debug.Log($"[战斗] 玩家攻击 {monster.gameObject.name}，" +
                          $"伤害={damageResult.FinalDamage:F1}" +
                          (damageResult.IsCritical ? " 【暴击！】" : ""));

                // 元素反应预埋（当前普攻 ElementType.None，技能系统接入后生效）
                if (damageResult.ElementType != ElementType.None)
                {
                    var monsterStatus = monster.StatusEffects;
                    if (monsterStatus != null)
                    {
                        ElementalReaction.CheckAndTrigger(
                            monsterStatus, damageResult.ElementType,
                            monster.EntityID, _hero.EntityID);
                    }
                }
            }
            else
            {
                Debug.Log("[战斗] 目标闪避了攻击！");
            }
        }

        // =====================================================================
        //  墙壁碰撞交互（门检测）
        //  门注册为墙壁，不触发 OnMoveBlocked，而是触发 OnWallBlocked
        // =====================================================================

        private void OnWallBlocked(Vector2Int wallTile)
        {
            if (!_hero.IsAlive) return;

            var doorInteraction = EscapeTheTower.Map.DoorInteraction.Instance;
            if (doorInteraction != null)
            {
                doorInteraction.TryOpenDoor(wallTile, _hero);
            }
        }

        // =====================================================================
        //  击杀后保护
        // =====================================================================

        /// <summary>
        /// 击杀后设置延迟保护（由 HeroController 调用）
        /// </summary>
        public void OnEnemyKilled()
        {
            _gridMovement.SetPostKillDelay();
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            if (_gridMovement != null)
            {
                _gridMovement.OnMoveBlocked -= OnGridMoveBlocked;
                _gridMovement.OnWallBlocked -= OnWallBlocked;
            }
        }
    }
}
