// ============================================================================
// 逃离魔塔 - 装备对比弹窗 (EquipmentComparePopup)
// 当悬停背包装备时，自动在 Tooltip 旁显示同槽位已穿戴装备的详情。
// 复用 EquipmentTooltip 的渲染逻辑，独立管理另一份弹窗实例。
//
// 来源：DesignDocs/07_UI_and_UX.md §1.3a（装备对比弹窗）
// ============================================================================

using UnityEngine;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 装备对比弹窗 —— 在主 Tooltip 旁边显示已穿戴装备的详情
    /// 内部持有一个独立的 EquipmentTooltip 实例
    /// </summary>
    public class EquipmentComparePopup : MonoBehaviour
    {
        // 内部复用 Tooltip 组件
        private EquipmentTooltip _compareTooltip;

        private void Awake()
        {
            // 创建一个独立的 Tooltip 子对象用于对比显示
            var tooltipObj = new GameObject("CompareTooltipInstance");
            tooltipObj.transform.SetParent(transform, false);
            _compareTooltip = tooltipObj.AddComponent<EquipmentTooltip>();
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 在主 Tooltip 旁边显示对比装备
        /// </summary>
        /// <param name="equippedItem">当前已穿戴的装备</param>
        /// <param name="mainTooltipRect">主 Tooltip 的 RectTransform（用于定位）</param>
        public void Show(EquipmentData equippedItem, RectTransform mainTooltipRect)
        {
            if (equippedItem == null || mainTooltipRect == null)
            {
                Hide();
                return;
            }

            // 计算对比弹窗位置：在主 Tooltip 的左侧（避免遮挡）
            // 主 Tooltip 的 pivot 是左上角(0,1)，所以在其左侧需要偏移
            Vector3[] corners = new Vector3[4];
            mainTooltipRect.GetWorldCorners(corners);
            // corners[0]=左下, corners[1]=左上, corners[2]=右上, corners[3]=右下

            // 取主 Tooltip 左侧中心点的屏幕坐标
            Vector2 leftCenter = new Vector2(corners[0].x - 8f, (corners[1].y + corners[0].y) / 2f);

            // 对比弹窗的 pivot 设为右上角，使其出现在主 Tooltip 左侧
            var compareRect = _compareTooltip.GetTooltipRect();
            if (compareRect != null)
            {
                compareRect.pivot = new Vector2(1f, 1f);
            }

            _compareTooltip.Show(equippedItem, leftCenter);
        }

        /// <summary>隐藏对比弹窗</summary>
        public void Hide()
        {
            if (_compareTooltip != null)
                _compareTooltip.Hide();
        }

        /// <summary>对比弹窗是否正在显示</summary>
        public bool IsShowing => _compareTooltip != null && _compareTooltip.IsShowing;
    }
}
