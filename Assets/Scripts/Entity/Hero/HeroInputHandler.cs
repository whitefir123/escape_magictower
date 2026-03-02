// ============================================================================
// 逃离魔塔 - 玩家输入处理器 (HeroInputHandler)
// 负责处理所有玩家的键鼠输入映射。
// 使用 Unity New Input System，所有非移动按键支持自定义。
//
// 来源：DesignDocs/07_UI_and_UX.md 第三节
//       NewProject_AIGuide/Core_Features/01_MVP_Implementation_Guide.md
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 玩家输入处理器 —— 将原始输入转化为抽象的意图驱动信号
    /// </summary>
    public class HeroInputHandler : MonoBehaviour
    {
        // === 输入状态（每帧由外部系统读取） ===

        /// <summary>WASD 移动方向（已归一化）</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>是否正在移动（WASD 有输入）</summary>
        public bool IsMoving => MoveInput.sqrMagnitude > 0.01f;

        /// <summary>鼠标在世界空间中的位置</summary>
        public Vector3 MouseWorldPosition { get; private set; }

        /// <summary>鼠标右键单击（本帧按下）—— 点击移动 / 远程攻击</summary>
        public bool RightClickPressed { get; private set; }

        /// <summary>鼠标右键持续按住</summary>
        public bool RightClickHeld { get; private set; }

        /// <summary>交互键按下（F 键 / 鼠标左键）</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>技能 1 按下</summary>
        public bool Skill1Pressed { get; private set; }

        /// <summary>技能 2 按下</summary>
        public bool Skill2Pressed { get; private set; }

        /// <summary>大招按下</summary>
        public bool UltimatePressed { get; private set; }

        /// <summary>闪避按下</summary>
        public bool DodgePressed { get; private set; }

        // === 内部引用 ===
        private Camera _mainCamera;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            UpdateKeyboardInput();
            UpdateMouseInput();
        }

        /// <summary>
        /// 每帧末尾清除一次性按键标记（在 LateUpdate 中调用）
        /// </summary>
        private void LateUpdate()
        {
            // 一次性输入在下一帧重置
            RightClickPressed = false;
            InteractPressed = false;
            Skill1Pressed = false;
            Skill2Pressed = false;
            UltimatePressed = false;
            DodgePressed = false;
        }

        // =====================================================================
        //  键盘输入
        // =====================================================================

        private void UpdateKeyboardInput()
        {
            // WASD 移动
            float h = 0f, v = 0f;
            if (Keyboard.current.wKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed) v -= 1f;
            if (Keyboard.current.aKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed) h += 1f;
            MoveInput = new Vector2(h, v).normalized;

            // F 键 交互
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                InteractPressed = true;
            }

            // Q 键 技能1（默认键位，可自定义）
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                Skill1Pressed = true;
            }

            // E 键 技能2
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Skill2Pressed = true;
            }

            // R 键 大招
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                UltimatePressed = true;
            }

            // Space 键 闪避
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                DodgePressed = true;
            }
        }

        // =====================================================================
        //  鼠标输入
        // =====================================================================

        private void UpdateMouseInput()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // 鼠标世界位置（2D 场景用 z=0 平面投射）
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            mouseScreenPos.z = -_mainCamera.transform.position.z;
            MouseWorldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

            // 鼠标左键 = 交互（与 F 键等价）
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                InteractPressed = true;
            }

            // 鼠标右键
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                RightClickPressed = true;
            }
            RightClickHeld = Mouse.current.rightButton.isPressed;
        }
    }
}
