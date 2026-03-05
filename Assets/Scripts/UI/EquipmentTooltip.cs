// ============================================================================
// 逃离魔塔 - 装备详情弹窗 (EquipmentTooltip)
// 悬停时显示装备完整信息，纯代码构建 Canvas UI。
//
// 布局（依据用户提供的参考图）：
//   装备名称（品质色） → 部位 → 品质 → 前缀/后缀列表
//   ─── 分隔线 ───
//   镶嵌槽位 → 属性列表
//
// 属性格式：物防: +7（基础: 3 +4）
//   3 = baseStat_Base + baseStat_QualityBonus（白板值，品质波动已合入）
//   4 = 词缀提供的附加数值
//
// 来源：DesignDocs/07_UI_and_UX.md §1.3a
//       GameData_Blueprints/06_Equipment_Affix_System.md §1.4
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 装备详情弹窗 —— 悬停时在鼠标附近显示装备完整属性
    /// </summary>
    public class EquipmentTooltip : MonoBehaviour
    {
        // =====================================================================
        //  常量
        // =====================================================================

        private const float TOOLTIP_WIDTH = 280f;
        private const float LINE_HEIGHT = 22f;
        private const float PADDING = 10f;
        private const float SEPARATOR_HEIGHT = 2f;

        // =====================================================================
        //  UI 引用
        // =====================================================================

        private Canvas _canvas;
        private GameObject _tooltipRoot;
        private RectTransform _tooltipRect;

        // 动态文本列表（每次显示时重建内容）
        private readonly List<GameObject> _contentElements = new List<GameObject>();

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _canvas = UIHelper.CreateScreenCanvas(transform, "TooltipCanvas", 200);
            _tooltipRoot = new GameObject("TooltipRoot", typeof(RectTransform));
            _tooltipRoot.transform.SetParent(_canvas.transform, false);
            _tooltipRect = _tooltipRoot.GetComponent<RectTransform>();

            // 锚定左上角，通过 anchoredPosition 控制位置
            _tooltipRect.anchorMin = Vector2.zero;
            _tooltipRect.anchorMax = Vector2.zero;
            _tooltipRect.pivot = new Vector2(0f, 1f); // 左上角为基准

            var bg = _tooltipRoot.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.94f);

            // 添加 Outline 模拟边框
            var outline = _tooltipRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _tooltipRoot.SetActive(false);
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 显示装备详情弹窗
        /// </summary>
        /// <param name="equip">要显示的装备数据</param>
        /// <param name="screenPos">弹窗锚点屏幕位置</param>
        public void Show(EquipmentData equip, Vector2 screenPos)
        {
            if (equip == null)
            {
                Hide();
                return;
            }

            ClearContent();
            BuildContent(equip);
            PositionTooltip(screenPos);
            _tooltipRoot.SetActive(true);
        }

        /// <summary>
        /// 隐藏弹窗
        /// </summary>
        public void Hide()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        /// <summary>弹窗是否正在显示</summary>
        public bool IsShowing => _tooltipRoot != null && _tooltipRoot.activeSelf;

        // =====================================================================
        //  内容构建（依据参考图布局）
        // =====================================================================

        private void BuildContent(EquipmentData equip)
        {
            float yOffset = -PADDING;
            Color qualityColor = EquipmentData.GetQualityColor(equip.quality);

            // --- 1. 装备名称（品质色，粗体） ---
            yOffset = AddTextLine(equip.GetDisplayName(), UIHelper.FontSizeTitle,
                qualityColor, FontStyle.Bold, yOffset);

            // --- 2. 部位名称 ---
            string slotIcon = GetSlotIcon(equip.slot);
            yOffset = AddTextLine($"{slotIcon} {equip.GetSlotBaseName()}", UIHelper.FontSizeNormal,
                UIHelper.TextDimColor, FontStyle.Normal, yOffset);

            // --- 3. 品质名称（品质色） ---
            yOffset = AddTextLine(GetQualityName(equip.quality), UIHelper.FontSizeNormal,
                qualityColor, FontStyle.Normal, yOffset);

            // --- 4. 前缀/后缀列表 ---
            if (equip.affixes != null && equip.affixes.Count > 0)
            {
                foreach (var affix in equip.affixes)
                {
                    if (affix?.definition == null) continue;
                    string typeLabel = affix.definition.slotType == AffixSlotType.Prefix
                        ? "前缀" : "后缀";
                    string cleanName = CleanAffixNameForTooltip(affix.definition.nameCN,
                        affix.definition.slotType);
                    yOffset = AddTextLine($"{typeLabel}：{cleanName}",
                        UIHelper.FontSizeSmall,
                        new Color(0.6f, 0.85f, 1.0f), FontStyle.Normal, yOffset);
                }
            }

            // --- 5. 分隔线 ---
            yOffset = AddSeparator(yOffset);

            // --- 6. 镶嵌槽位 ---
            if (equip.socketCount > 0)
            {
                yOffset = AddTextLine("镶嵌槽位：", UIHelper.FontSizeSmall,
                    UIHelper.TextNormalColor, FontStyle.Bold, yOffset);

                for (int i = 0; i < equip.socketCount; i++)
                {
                    // 后续接入宝石数据后替换显示
                    yOffset = AddTextLine("  ○ [空镶嵌槽]", UIHelper.FontSizeSmall,
                        UIHelper.TextDimColor, FontStyle.Normal, yOffset);
                }

                // 镶孔与属性之间加分隔线
                yOffset = AddSeparator(yOffset);
            }

            // --- 7. 属性列表 ---
            // 计算词缀对底座属性的加成量
            var affixBonuses = CalculateAffixBonuses(equip);

            // 底座属性1
            float base1Total = equip.baseStat1Base + equip.baseStat1QualityBonus;
            float affix1Bonus = affixBonuses.TryGetValue(equip.baseStat1Type, out float v1) ? v1 : 0f;
            yOffset = AddStatLine(equip.baseStat1Type, base1Total, affix1Bonus, false, yOffset);

            // 底座属性2
            float base2Total = equip.baseStat2Base + equip.baseStat2QualityBonus;
            float affix2Bonus = affixBonuses.TryGetValue(equip.baseStat2Type, out float v2) ? v2 : 0f;
            yOffset = AddStatLine(equip.baseStat2Type, base2Total, affix2Bonus, false, yOffset);

            // 词缀独立属性（不在底座上的）
            foreach (var kvp in affixBonuses)
            {
                if (kvp.Key == equip.baseStat1Type || kvp.Key == equip.baseStat2Type) continue;
                // 检查是否为百分比类属性
                bool isPercent = IsPercentageStat(kvp.Key);
                yOffset = AddStatLine(kvp.Key, 0f, kvp.Value, isPercent, yOffset);
            }

            // --- 设置弹窗总高度 ---
            float totalHeight = Mathf.Abs(yOffset) + PADDING;
            _tooltipRect.sizeDelta = new Vector2(TOOLTIP_WIDTH, totalHeight);
        }

        // =====================================================================
        //  属性行构建
        // =====================================================================

        /// <summary>
        /// 添加一行属性：物防: +7（基础: 3 +4）
        /// </summary>
        private float AddStatLine(StatType stat, float baseVal, float affixVal,
            bool isPercent, float yOffset)
        {
            string statName = GetStatDisplayName(stat);
            float totalVal = baseVal + affixVal;
            string unit = isPercent ? "%" : "";

            string text;
            if (baseVal > 0f && affixVal > 0f)
            {
                // 完整格式：物防: +7（基础: 3 +4）
                text = $"{statName}：<color=#4CFF4C>+{totalVal:G4}{unit}</color>（基础：{baseVal:G4} +{affixVal:G4}）";
            }
            else if (baseVal > 0f)
            {
                // 仅底座值，无词缀加成
                text = $"{statName}：<color=#4CFF4C>+{totalVal:G4}{unit}</color>";
            }
            else
            {
                // 纯词缀属性（无底座）
                text = $"{statName}：<color=#4CFF4C>+{totalVal:G4}{unit}</color>";
            }

            return AddRichTextLine(text, UIHelper.FontSizeSmall, yOffset);
        }

        // =====================================================================
        //  词缀加成计算
        // =====================================================================

        /// <summary>
        /// 计算所有词缀对各属性的加成量（用于 Tooltip 分项显示）
        /// </summary>
        private Dictionary<StatType, float> CalculateAffixBonuses(EquipmentData equip)
        {
            var bonuses = new Dictionary<StatType, float>();
            if (equip.affixes == null) return bonuses;

            float base1Total = equip.baseStat1Base + equip.baseStat1QualityBonus;
            float base2Total = equip.baseStat2Base + equip.baseStat2QualityBonus;

            foreach (var affix in equip.affixes)
            {
                if (affix?.definition == null) continue;
                var def = affix.definition;

                float bonusValue;
                if (def.isPercentage)
                {
                    float percent = affix.rolledValue / 100f;
                    // 百分比词缀：如果作用于底座属性则乘算
                    if (def.bonusStat == equip.baseStat1Type && base1Total > 0)
                        bonusValue = base1Total * percent;
                    else if (def.bonusStat == equip.baseStat2Type && base2Total > 0)
                        bonusValue = base2Total * percent;
                    else
                    {
                        // 全局百分比属性，直接作为百分比显示
                        if (!bonuses.ContainsKey(def.bonusStat))
                            bonuses[def.bonusStat] = 0f;
                        bonuses[def.bonusStat] += affix.rolledValue;
                        // 处理负面代价
                        if (def.hasPenalty)
                        {
                            if (!bonuses.ContainsKey(def.penaltyStat))
                                bonuses[def.penaltyStat] = 0f;
                            bonuses[def.penaltyStat] += def.penaltyValue;
                        }
                        continue;
                    }
                }
                else
                {
                    bonusValue = affix.rolledValue;
                }

                if (!bonuses.ContainsKey(def.bonusStat))
                    bonuses[def.bonusStat] = 0f;
                bonuses[def.bonusStat] += bonusValue;

                // 处理负面代价
                if (def.hasPenalty)
                {
                    if (!bonuses.ContainsKey(def.penaltyStat))
                        bonuses[def.penaltyStat] = 0f;
                    bonuses[def.penaltyStat] += def.penaltyValue;
                }
            }

            return bonuses;
        }

        // =====================================================================
        //  UI 元素添加辅助
        // =====================================================================

        private float AddTextLine(string content, int fontSize, Color color,
            FontStyle style, float yOffset)
        {
            var obj = new GameObject("Line", typeof(RectTransform));
            obj.transform.SetParent(_tooltipRoot.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(PADDING, yOffset);

            float height = fontSize <= UIHelper.FontSizeSmall ? LINE_HEIGHT : LINE_HEIGHT + 4f;
            rect.sizeDelta = new Vector2(-PADDING * 2, height);

            var text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.font = UIHelper.GetDefaultFont();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            _contentElements.Add(obj);
            return yOffset - height;
        }

        /// <summary>添加支持 Rich Text 的文本行</summary>
        private float AddRichTextLine(string richContent, int fontSize, float yOffset)
        {
            var obj = new GameObject("RichLine", typeof(RectTransform));
            obj.transform.SetParent(_tooltipRoot.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(PADDING, yOffset);
            rect.sizeDelta = new Vector2(-PADDING * 2, LINE_HEIGHT);

            var text = obj.AddComponent<Text>();
            text.text = richContent;
            text.fontSize = fontSize;
            text.color = UIHelper.TextNormalColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.font = UIHelper.GetDefaultFont();
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            _contentElements.Add(obj);
            return yOffset - LINE_HEIGHT;
        }

        private float AddSeparator(float yOffset)
        {
            float gap = 4f;
            var obj = new GameObject("Separator", typeof(RectTransform));
            obj.transform.SetParent(_tooltipRoot.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(PADDING, yOffset - gap);
            rect.sizeDelta = new Vector2(-PADDING * 2, SEPARATOR_HEIGHT);

            var img = obj.AddComponent<Image>();
            img.color = UIHelper.SeparatorColor;

            _contentElements.Add(obj);
            return yOffset - gap - SEPARATOR_HEIGHT - gap;
        }

        // =====================================================================
        //  位置计算
        // =====================================================================

        private void PositionTooltip(Vector2 screenPos)
        {
            // 将屏幕坐标转换为 Canvas 内坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPos);

            // 检查是否会超出屏幕右边或上方，进行偏移
            float canvasWidth = _canvas.GetComponent<RectTransform>().rect.width;
            float canvasHeight = _canvas.GetComponent<RectTransform>().rect.height;

            float tooltipW = _tooltipRect.sizeDelta.x;
            float tooltipH = _tooltipRect.sizeDelta.y;

            // 默认在鼠标右下方偏移 16px
            float x = localPos.x + 16f;
            float y = localPos.y - 16f;

            // 超出右边界 → 翻到左侧
            if (x + tooltipW > canvasWidth / 2f)
                x = localPos.x - tooltipW - 16f;

            // 超出下边界 → 翻到上方
            if (y - tooltipH < -canvasHeight / 2f)
                y = localPos.y + tooltipH + 16f;

            _tooltipRect.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// 获取弹窗的 RectTransform（供对比弹窗定位使用）
        /// </summary>
        public RectTransform GetTooltipRect() => _tooltipRect;

        // =====================================================================
        //  清理
        // =====================================================================

        private void ClearContent()
        {
            foreach (var obj in _contentElements)
            {
                if (obj != null) Destroy(obj);
            }
            _contentElements.Clear();
        }

        // =====================================================================
        //  辅助映射
        // =====================================================================

        /// <summary>部位图标映射</summary>
        private static string GetSlotIcon(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "⚔",
                EquipmentSlot.Helmet => "⛑",
                EquipmentSlot.Armor => "🛡",
                EquipmentSlot.Gloves => "🧤",
                EquipmentSlot.Boots => "👢",
                EquipmentSlot.Accessory => "💍",
                _ => "◆",
            };
        }

        /// <summary>品质中文名</summary>
        private static string GetQualityName(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => "普通",
                QualityTier.Green => "稀有",
                QualityTier.Blue => "罕见",
                QualityTier.Purple => "史诗",
                QualityTier.Yellow => "传说",
                QualityTier.Red => "神话",
                QualityTier.Rainbow => "幻彩",
                QualityTier.Shanhai => "山海神铸",
                _ => "未知",
            };
        }

        /// <summary>属性中文显示名（纯中文，无英文）</summary>
        private static string GetStatDisplayName(StatType stat)
        {
            return stat switch
            {
                StatType.HP => "生命",
                StatType.MaxHP => "最大生命",
                StatType.MP => "法力",
                StatType.MaxMP => "最大法力",
                StatType.ATK => "物攻",
                StatType.MATK => "魔攻",
                StatType.DEF => "物防",
                StatType.MDEF => "魔抗",
                StatType.CritRate => "暴击率",
                StatType.CritMultiplier => "暴伤倍率",
                StatType.ArmorPen => "物理穿透",
                StatType.MagicPen => "魔法穿透",
                StatType.Dodge => "闪避率",
                StatType.MoveSpeed => "移速",
                StatType.AttackSpeed => "攻速",
                StatType.LifeSteal => "吸血",
                StatType.ManaRegen => "回蓝",
                StatType.GoldMultiplier => "金币倍率",
                StatType.BonusCCResist => "控制抗性",
                _ => stat.ToString(),
            };
        }

        /// <summary>判断属性是否为百分比类型</summary>
        private static bool IsPercentageStat(StatType stat)
        {
            return stat switch
            {
                StatType.CritRate or StatType.CritMultiplier or
                StatType.ArmorPen or StatType.MagicPen or
                StatType.Dodge or StatType.MoveSpeed or
                StatType.AttackSpeed or StatType.LifeSteal or
                StatType.GoldMultiplier => true,
                _ => false,
            };
        }

        /// <summary>清理词缀名称（与 EquipmentData 中逻辑一致）</summary>
        private static string CleanAffixNameForTooltip(string rawName, AffixSlotType slotType)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;
            string name = rawName;
            if (slotType == AffixSlotType.Prefix)
            {
                if (name.EndsWith("的")) name = name[..^1];
            }
            else
            {
                if (name.StartsWith("…之")) name = name[2..];
                else if (name.StartsWith("...之")) name = name[4..];
                else if (name.StartsWith("…")) name = name[1..];
            }
            return name;
        }
    }
}
