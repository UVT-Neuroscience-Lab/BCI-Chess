using UnityEngine;

namespace BciChess.Unicorn
{
    /// <summary>
    /// Hidden fallback for Unicorn mode: numpad key N selects the current target on stimulus slot N
    /// (1-9, 0 = slot 10) through <see cref="UnicornBciSelector.TrySelectSlot"/>. Deliberately not shown in the UI.
    /// </summary>
    public sealed class UnicornKeypadOverride : MonoBehaviour
    {
        private static readonly KeyCode[] Keypad =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0
        };

        private UnicornBciSelector _selector;

        public void Initialize(UnicornBciSelector selector)
        {
            _selector = selector;
        }

        private void Update()
        {
            if (_selector == null || !_selector.IsSelecting)
                return;

            for (int i = 0; i < Keypad.Length; i++)
            {
                if (Input.GetKeyDown(Keypad[i]))
                {
                    _selector.TrySelectSlot(i);
                    return;
                }
            }
        }
    }
}
