using BciChess.Bci;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>
    /// Keyboard front end of <see cref="FakeBciSelector"/>: number key N simulates the player attending to
    /// stimulus slot N (1-9, 0 = slot 10; top row or keypad).
    /// </summary>
    public sealed class FakeBciKeyboardInput : MonoBehaviour
    {
        private static readonly KeyCode[] TopRow =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        private static readonly KeyCode[] Keypad =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0
        };

        private FakeBciSelector _selector;

        public void Initialize(FakeBciSelector selector)
        {
            _selector = selector;
        }

        /// <summary>On-screen label for a slot: its key (1-9, 0), or its 1-based number beyond the tenth slot.</summary>
        public static string KeyLabel(int slotIndex) => slotIndex == 9 ? "0" : (slotIndex + 1).ToString();

        private void Update()
        {
            if (_selector == null || !_selector.IsSelecting)
                return;

            for (int i = 0; i < TopRow.Length; i++)
            {
                if (Input.GetKeyDown(TopRow[i]) || Input.GetKeyDown(Keypad[i]))
                {
                    _selector.TrySelectSlot(i);
                    return;
                }
            }
        }
    }
}
