using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Column beside the board holding the BCI targets that are not squares ("Cancel", "Back"), so they can
    /// flash like everything else. Tiles are shown only while they are targets.
    /// </summary>
    public sealed class BciCommandBar : MonoBehaviour
    {
        public BciTargetVisual CancelVisual { get; private set; }
        public BciTargetVisual BackVisual { get; private set; }

        private GameObject _cancelTile;
        private GameObject _backTile;

        public void Build(BoardTheme theme, StimulusVisualSettings settings)
        {
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            (_backTile, BackVisual) = CreateTile("Back", "Back", theme, settings);
            (_cancelTile, CancelVisual) = CreateTile("Cancel", "Cancel", theme, settings);
            Show(false, false);
        }

        public void Show(bool cancel, bool back)
        {
            _cancelTile.SetActive(cancel);
            _backTile.SetActive(back);
        }

        private (GameObject, BciTargetVisual) CreateTile(string name, string label, BoardTheme theme,
            StimulusVisualSettings settings)
        {
            var tile = UiFactory.CreateRounded(name, transform, theme.button, 12f);
            var element = tile.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 130f;

            var text = UiFactory.CreateText("Label", tile.transform, label, 30, theme.buttonText, TextAnchor.MiddleCenter,
                UiFactory.Semibold);
            UiFactory.Stretch(text.rectTransform);

            var visual = BciTargetVisual.Attach(tile.rectTransform, settings, withBadge: true);
            return (tile.gameObject, visual);
        }
    }
}
