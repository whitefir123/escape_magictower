// ============================================================================
// 逃离魔塔 - 飘字系统 (FloatingTextManager)
// 战斗中的视觉反馈：伤害数字、治疗、经验获取等。
// 颜色/大小/动画根据伤害类型差异化显示。
//
// 伤害颜色规则（用户设定）：
//   - 玩家造成的伤害 → 红色（暴击有弹簧放大效果）
//   - 怪物造成的伤害 → 白色
//   - 元素伤害 → 橙色（不区分阵营）
//   - 免疫 → 白色
//
// 来源：DesignDocs/07_UI_and_UX.md 第三节
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 飘字类型枚举
    /// </summary>
    public enum FloatingTextType
    {
        NormalDamage,       // 普通物理伤害
        CritDamage,         // 暴击伤害（弹簧放大效果）
        MagicDamage,        // 魔法伤害
        ElementalReaction,  // 元素反应（特殊艺术字）
        Heal,               // 治疗（绿色）
        ExpGain,            // 经验获取（金色）
        GoldGain,           // 金币获取（黄色）
        Miss,               // 未命中 / 闪避
        Immune,             // 免疫
        LevelUp,            // 升级
    }

    /// <summary>
    /// 飘字管理器 —— 管理所有战斗飘字的生成与回收
    /// 纯代码创建 Canvas + 对象池，无需预制体
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        // === Canvas ===
        private Canvas _canvas;
        private RectTransform _canvasRect;

        // === 对象池 ===
        private const int POOL_SIZE = 24;
        private readonly Queue<FloatingTextInstance> _pool = new Queue<FloatingTextInstance>();

        // === 常量 ===
        private const float FLOAT_DURATION = 1.2f;    // 飘字持续时间
        private const float RISE_SPEED = 40f;          // 上浮速度（像素/秒）
        private const float FADE_START = 0.6f;         // 开始淡出的时间点（比例）

        // === 伤害颜色（用户设定） ===
        private static readonly Color PLAYER_DAMAGE_COLOR = new Color(0.95f, 0.2f, 0.15f);  // 红色
        private static readonly Color MONSTER_DAMAGE_COLOR = Color.white;                     // 白色
        private static readonly Color ELEMENTAL_COLOR = new Color(1f, 0.6f, 0f);             // 橙色
        private static readonly Color HEAL_COLOR = new Color(0.2f, 0.9f, 0.3f);              // 绿色
        private static readonly Color EXP_COLOR = new Color(1f, 0.85f, 0f);                  // 金色
        private static readonly Color GOLD_COLOR = new Color(1f, 0.9f, 0.2f);                // 黄色
        private static readonly Color MISS_COLOR = new Color(0.6f, 0.6f, 0.6f);              // 灰色
        private static readonly Color IMMUNE_COLOR = Color.white;                              // 白色
        private static readonly Color LEVEL_UP_COLOR = new Color(1f, 0.9f, 0.3f);            // 金色

        // =====================================================================
        //  初始化
        // =====================================================================

        private void Awake()
        {
            CreateCanvas();
            WarmPool();
        }

        /// <summary>
        /// 创建专属 Canvas（独立于 HUDManager）
        /// </summary>
        private void CreateCanvas()
        {
            var canvasObj = new GameObject("FloatingTextCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20; // 高于 HUD(50) 会遮挡，低于它即可
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _canvasRect = canvasObj.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        private void WarmPool()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var instance = CreateFloatingTextInstance();
                instance.Root.SetActive(false);
                _pool.Enqueue(instance);
            }
        }

        /// <summary>
        /// 创建一个飘字实例
        /// </summary>
        private FloatingTextInstance CreateFloatingTextInstance()
        {
            var obj = new GameObject("FloatText", typeof(RectTransform));
            obj.transform.SetParent(_canvasRect, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);

            var text = obj.AddComponent<Text>();
            text.font = UIHelper.GetDefaultFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 描边增强可读性
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            return new FloatingTextInstance
            {
                Root = obj,
                Rect = rect,
                Text = text,
                Outline = outline,
            };
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 在指定世界位置显示飘字
        /// </summary>
        /// <param name="worldPosition">世界坐标位置</param>
        /// <param name="text">显示文本</param>
        /// <param name="type">飘字类型</param>
        public void Show(Vector3 worldPosition, string text, FloatingTextType type)
        {
            var instance = GetFromPool();
            if (instance == null) return;

            // 设置文本和样式
            instance.Text.text = text;
            instance.Text.fontSize = GetFontSizeForType(type);
            instance.Text.color = GetColorForType(type);
            instance.Text.fontStyle = (type == FloatingTextType.CritDamage || type == FloatingTextType.LevelUp)
                ? FontStyle.Bold : FontStyle.Normal;

            // 世界坐标 → 屏幕坐标 → Canvas 局部坐标
            if (Camera.main != null)
            {
                // 随机偏移避免堆叠
                float offsetX = Random.Range(-15f, 15f);
                float offsetY = Random.Range(0f, 10f);
                Vector3 worldOffset = worldPosition + new Vector3(0f, 0.5f, 0f);
                Vector2 screenPos = Camera.main.WorldToScreenPoint(worldOffset);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screenPos, null, out Vector2 localPos);
                instance.Rect.anchoredPosition = localPos + new Vector2(offsetX, offsetY);
            }

            // 初始化动画参数
            instance.Elapsed = 0f;
            instance.Duration = FLOAT_DURATION;
            instance.IsCrit = (type == FloatingTextType.CritDamage);
            instance.IsLevelUp = (type == FloatingTextType.LevelUp);
            instance.OriginalColor = instance.Text.color;
            instance.Root.SetActive(true);

            // 暴击弹簧效果：初始缩小
            if (instance.IsCrit)
            {
                instance.Rect.localScale = Vector3.one * 0.3f;
            }
            else if (instance.IsLevelUp)
            {
                instance.Rect.localScale = Vector3.one * 0.5f;
                instance.Duration = 2.0f; // 升级飘字持续更久
            }
            else
            {
                instance.Rect.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 内部方法：显示指定颜色的飘字（绕过 GetColorForType 的默认映射）
        /// </summary>
        private void ShowWithColor(Vector3 worldPosition, string text, FloatingTextType type, Color color)
        {
            Show(worldPosition, text, type);

            // 覆盖刚刚设置的颜色（Show 中使用了默认配色）
            if (_activeInstances.Count > 0)
            {
                var lastInst = _activeInstances[_activeInstances.Count - 1];
                lastInst.Text.color = color;
                lastInst.OriginalColor = color;
            }
        }

        /// <summary>
        /// 快捷方法：显示伤害数字（根据阵营决定颜色）
        /// </summary>
        /// <param name="worldPosition">受击方的世界位置</param>
        /// <param name="damage">伤害值</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="damageType">伤害类型</param>
        /// <param name="isPlayerDamage">是否为玩家造成的伤害（true=红色，false=白色）</param>
        public void ShowDamage(Vector3 worldPosition, float damage, bool isCrit,
            DamageType damageType, bool isPlayerDamage = true)
        {
            string text = $"-{Mathf.CeilToInt(damage)}";
            FloatingTextType type;

            if (isCrit)
            {
                type = FloatingTextType.CritDamage;
                text = text + "!";
            }
            else if (damageType == DamageType.Magical)
            {
                type = FloatingTextType.MagicDamage;
            }
            else
            {
                type = FloatingTextType.NormalDamage;
            }

            // 颜色决定：玩家伤害红色，怪物伤害白色
            Color color = isPlayerDamage ? PLAYER_DAMAGE_COLOR : MONSTER_DAMAGE_COLOR;

            ShowWithColor(worldPosition, text, type, color);
        }

        /// <summary>
        /// 快捷方法：显示生命值治疗（绿色）
        /// </summary>
        public void ShowHeal(Vector3 worldPosition, float amount)
        {
            Show(worldPosition, $"+{Mathf.CeilToInt(amount)}", FloatingTextType.Heal);
        }

        /// <summary>
        /// 快捷方法：显示法力回复（蓝色）
        /// </summary>
        public void ShowManaHeal(Vector3 worldPosition, float amount)
        {
            ShowWithColor(worldPosition, $"+{Mathf.CeilToInt(amount)} MP",
                FloatingTextType.Heal, new Color(0.3f, 0.5f, 1f));
        }

        /// <summary>
        /// 快捷方法：显示闪避
        /// </summary>
        public void ShowMiss(Vector3 worldPosition)
        {
            Show(worldPosition, "MISS", FloatingTextType.Miss);
        }

        /// <summary>
        /// 快捷方法：显示经验获取
        /// </summary>
        public void ShowExpGain(Vector3 worldPosition, int amount)
        {
            Show(worldPosition, $"+{amount} EXP", FloatingTextType.ExpGain);
        }

        /// <summary>
        /// 快捷方法：显示金币获取（金色）
        /// </summary>
        public void ShowGoldGain(Vector3 worldPosition, int amount)
        {
            Show(worldPosition, $"+{amount} 金币", FloatingTextType.GoldGain);
        }

        /// <summary>
        /// 快捷方法：显示钥匙拾取（使用对应钥匙颜色）
        /// </summary>
        public void ShowKeyPickup(Vector3 worldPosition, string keyName, Color keyColor)
        {
            ShowWithColor(worldPosition, $"+1 {keyName}", FloatingTextType.GoldGain, keyColor);
        }

        /// <summary>
        /// 快捷方法：显示元素反应名称
        /// </summary>
        public void ShowReaction(Vector3 worldPosition, ElementalReactionType reaction)
        {
            string name = GetReactionName(reaction);
            Show(worldPosition, name, FloatingTextType.ElementalReaction);
        }

        // =====================================================================
        //  对象池管理
        // =====================================================================

        // 活跃实例列表
        private readonly List<FloatingTextInstance> _activeInstances = new List<FloatingTextInstance>();

        private FloatingTextInstance GetFromPool()
        {
            FloatingTextInstance instance;
            if (_pool.Count > 0)
            {
                instance = _pool.Dequeue();
            }
            else
            {
                // 池耗尽：回收最老的活跃实例
                if (_activeInstances.Count > 0)
                {
                    instance = _activeInstances[0];
                    _activeInstances.RemoveAt(0);
                    instance.Root.SetActive(false);
                }
                else
                {
                    // 极端情况：创建新实例
                    instance = CreateFloatingTextInstance();
                }
            }

            _activeInstances.Add(instance);
            return instance;
        }

        private void ReturnToPool(FloatingTextInstance instance)
        {
            instance.Root.SetActive(false);
            _activeInstances.Remove(instance);
            _pool.Enqueue(instance);
        }

        // =====================================================================
        //  每帧更新（驱动所有活跃飘字动画）
        // =====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            // 倒序遍历以便安全移除
            for (int i = _activeInstances.Count - 1; i >= 0; i--)
            {
                var inst = _activeInstances[i];
                inst.Elapsed += dt;
                float t = inst.Elapsed / inst.Duration;

                if (t >= 1f)
                {
                    ReturnToPool(inst);
                    continue;
                }

                // 上浮
                var pos = inst.Rect.anchoredPosition;
                pos.y += RISE_SPEED * dt;
                inst.Rect.anchoredPosition = pos;

                // 淡出（后半段）
                if (t > FADE_START)
                {
                    float fadeT = (t - FADE_START) / (1f - FADE_START);
                    var c = inst.OriginalColor;
                    c.a = Mathf.Lerp(1f, 0f, fadeT);
                    inst.Text.color = c;
                }

                // 弹簧缩放效果（暴击/升级）
                if (inst.IsCrit || inst.IsLevelUp)
                {
                    float springScale = EvaluateSpringScale(t, inst.IsCrit ? 1.8f : 2.0f);
                    inst.Rect.localScale = Vector3.one * springScale;
                }
            }
        }

        /// <summary>
        /// 弹簧缩放曲线：从 0.3 → peakScale → 1.0 的阻尼振荡
        /// </summary>
        private static float EvaluateSpringScale(float t, float peakScale)
        {
            // 快速冲刺阶段（0 ~ 0.15）：从小到大
            if (t < 0.15f)
            {
                float ramp = t / 0.15f;
                return Mathf.Lerp(0.3f, peakScale, ramp * ramp);
            }
            // 回弹阶段（0.15 ~ 0.4）：从大到小于正常再回弹
            else if (t < 0.4f)
            {
                float bounce = (t - 0.15f) / 0.25f;
                // 阻尼正弦衰减
                float decay = Mathf.Exp(-3f * bounce);
                float oscillation = Mathf.Cos(bounce * Mathf.PI * 2f);
                return 1f + (peakScale - 1f) * decay * oscillation * 0.3f;
            }
            // 稳定阶段（0.4 ~ 1.0）：保持正常大小
            else
            {
                return 1f;
            }
        }

        // =====================================================================
        //  内部工具
        // =====================================================================

        private Color GetColorForType(FloatingTextType type)
        {
            return type switch
            {
                // 默认伤害颜色（玩家造成的伤害用红色）
                // 怪物伤害的颜色覆盖在 ShowDamage 中进行
                FloatingTextType.NormalDamage      => PLAYER_DAMAGE_COLOR,
                FloatingTextType.CritDamage        => PLAYER_DAMAGE_COLOR,
                FloatingTextType.MagicDamage       => PLAYER_DAMAGE_COLOR,
                FloatingTextType.ElementalReaction => ELEMENTAL_COLOR,
                FloatingTextType.Heal              => HEAL_COLOR,
                FloatingTextType.ExpGain           => EXP_COLOR,
                FloatingTextType.GoldGain          => GOLD_COLOR,
                FloatingTextType.Miss              => MISS_COLOR,
                FloatingTextType.Immune            => IMMUNE_COLOR,
                FloatingTextType.LevelUp           => LEVEL_UP_COLOR,
                _ => Color.white,
            };
        }

        private int GetFontSizeForType(FloatingTextType type)
        {
            return type switch
            {
                FloatingTextType.CritDamage        => 28,
                FloatingTextType.ElementalReaction => 26,
                FloatingTextType.LevelUp           => 32,
                FloatingTextType.Heal              => 22,
                _ => 20,
            };
        }

        private string GetReactionName(ElementalReactionType reaction)
        {
            return reaction switch
            {
                ElementalReactionType.Vaporize       => "蒸发",
                ElementalReactionType.Melt           => "融化",
                ElementalReactionType.Overload       => "超载",
                ElementalReactionType.Freeze         => "冻结",
                ElementalReactionType.ElectroCharged => "感电",
                ElementalReactionType.Shatter        => "碎冰",
                ElementalReactionType.VenomBlast     => "毒爆",
                _ => "",
            };
        }

        // =====================================================================
        //  飘字实例数据
        // =====================================================================

        private class FloatingTextInstance
        {
            public GameObject Root;
            public RectTransform Rect;
            public Text Text;
            public Outline Outline;
            public float Elapsed;
            public float Duration;
            public bool IsCrit;
            public bool IsLevelUp;
            public Color OriginalColor;
        }
    }
}
