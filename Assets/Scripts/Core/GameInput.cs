using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace LastFight
{
    /// <summary>
    /// Единая точка ввода. Работает и со старым Input Manager, и с новым Input System,
    /// поэтому проект не зависит от настроек Player Settings.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool AttackPressed
        {
            get { Mouse m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; }
        }

        public static bool PausePressed
        {
            get { Keyboard k = Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame; }
        }

        public static bool RestartPressed
        {
            get { Keyboard k = Keyboard.current; return k != null && k.rKey.wasPressedThisFrame; }
        }

        public static Vector2 PointerPosition
        {
            get { Mouse m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; }
        }
#else
        public static Vector2 Move
        {
            get
            {
                float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool AttackPressed => Input.GetMouseButtonDown(0);
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.R);
        public static Vector2 PointerPosition => Input.mousePosition;
#endif
    }
}
