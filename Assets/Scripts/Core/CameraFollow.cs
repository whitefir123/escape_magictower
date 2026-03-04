// ============================================================================
// 逃离魔塔 - 摄像机跟随 (CameraFollow)
// LateUpdate 平滑跟随玩家，适配大型迷宫地图探索。
// 支持边界限制，防止摄像机超出地图范围看到空白区域。
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 摄像机跟随玩家 —— 平滑追踪 + 边界限制
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("=== 跟随参数 ===")]
        [Tooltip("跟随目标")]
        [SerializeField] private Transform target;

        [Tooltip("平滑速度（越大越快）")]
        [SerializeField] private float smoothSpeed = 8f;

        [Tooltip("摄像机 Z 轴偏移（2D 游戏保持负值）")]
        [SerializeField] private float zOffset = -10f;

        // === 边界限制 ===
        private bool _hasBounds;
        private float _minX, _maxX, _minY, _maxY;

        /// <summary>设置跟随目标（运行时动态绑定）</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// 设置摄像机活动边界（世界坐标）。
        /// 摄像机可视范围不会超出此边界。
        /// </summary>
        /// <param name="mapMinX">地图左边界</param>
        /// <param name="mapMaxX">地图右边界</param>
        /// <param name="mapMinY">地图下边界</param>
        /// <param name="mapMaxY">地图上边界</param>
        public void SetBounds(float mapMinX, float mapMaxX, float mapMinY, float mapMaxY)
        {
            _hasBounds = true;
            _minX = mapMinX;
            _maxX = mapMaxX;
            _minY = mapMinY;
            _maxY = mapMaxY;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPos = new Vector3(
                target.position.x,
                target.position.y,
                zOffset);

            Vector3 smoothed = Vector3.Lerp(
                transform.position,
                desiredPos,
                smoothSpeed * Time.deltaTime);

            // 边界限制：确保摄像机可视范围不超出地图
            if (_hasBounds)
            {
                var cam = GetComponent<Camera>();
                if (cam != null && cam.orthographic)
                {
                    float halfH = cam.orthographicSize;
                    float halfW = halfH * cam.aspect;

                    smoothed.x = Mathf.Clamp(smoothed.x, _minX + halfW, _maxX - halfW);
                    smoothed.y = Mathf.Clamp(smoothed.y, _minY + halfH, _maxY - halfH);
                }
            }

            transform.position = smoothed;
        }
    }
}
