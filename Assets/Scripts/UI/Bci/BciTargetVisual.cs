using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// BCI stimulation layer for one on-screen element (a board square, a promotion button, a command tile):
    /// a slot-coloured frame and number while it is a target, the flash overlay, and a pulse when chosen.
    /// Independent of the chess piece and square renderers; it only draws on top of its host.
    /// </summary>
    public sealed class BciTargetVisual : MonoBehaviour
    {
        private StimulusVisualSettings _settings;
        private Image _frame;
        private Image _flash;
        private Image _chosen;
        private Image _badge;
        private Text _badgeText;
        private bool _isTarget;
        private float _chosenTimeLeft;

        /// <summary>Adds a stimulation layer covering <paramref name="host"/>, drawn above its existing children.</summary>
        public static BciTargetVisual Attach(RectTransform host, StimulusVisualSettings settings, bool withBadge)
        {
            var root = UiFactory.CreateRect("BciTarget", host);
            UiFactory.Stretch(root);
            root.SetAsLastSibling();
            var visual = root.gameObject.AddComponent<BciTargetVisual>();
            visual.Build(settings, withBadge);
            return visual;
        }

        public bool IsTarget => _isTarget;

        private void Build(StimulusVisualSettings settings, bool withBadge)
        {
            _settings = settings;

            _flash = UiFactory.CreateImage("Flash", transform, settings.flashColor);
            UiFactory.Stretch(_flash.rectTransform);
            _flash.enabled = false;

            _chosen = UiFactory.CreateImage("Chosen", transform, settings.chosenColor);
            UiFactory.Stretch(_chosen.rectTransform);
            _chosen.enabled = false;

            _frame = UiFactory.CreateImage("Frame", transform, Color.white, UiFactory.Frame);
            _frame.type = Image.Type.Sliced;
            UiFactory.Stretch(_frame.rectTransform, 2f);
            _frame.enabled = false;

            if (withBadge)
            {
                _badge = UiFactory.CreateImage("Badge", transform, Color.white, UiFactory.Circle);
                UiFactory.SetAnchors(_badge.rectTransform, new Vector2(0.62f, 0.62f), new Vector2(0.96f, 0.96f));
                _badge.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

                _badgeText = UiFactory.CreateText("Slot", _badge.transform, "", 22, Color.black, TextAnchor.MiddleCenter);
                _badgeText.fontStyle = FontStyle.Bold;
                _badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _badgeText.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.Stretch(_badgeText.rectTransform);
                _badge.gameObject.SetActive(false);
            }
        }

        /// <summary>Marks the element as a target of <paramref name="slotIndex"/>, or clears the marking.</summary>
        public void SetTarget(bool isTarget, int slotIndex = 0, string slotLabel = "")
        {
            _isTarget = isTarget;
            var color = _settings.SlotColor(slotIndex);

            _frame.enabled = isTarget;
            _frame.color = color;
            if (!isTarget)
                _flash.enabled = false;

            if (_badge != null)
            {
                _badge.gameObject.SetActive(isTarget && _settings.showSlotNumbers);
                _badge.color = color;
                _badgeText.text = slotLabel;
            }
        }

        public void SetLit(bool lit)
        {
            _flash.enabled = lit && _isTarget;
        }

        /// <summary>Brief highlight confirming the BCI picked this element. Survives the target being cleared.</summary>
        public void PlayChosen()
        {
            _chosenTimeLeft = _settings.chosenFeedbackSeconds;
            UpdateChosen();
        }

        private void Update()
        {
            if (_chosenTimeLeft <= 0f)
                return;
            _chosenTimeLeft -= Time.unscaledDeltaTime;
            UpdateChosen();
        }

        private void UpdateChosen()
        {
            float strength = Mathf.Clamp01(_chosenTimeLeft / _settings.chosenFeedbackSeconds);
            _chosen.enabled = strength > 0f;
            var color = _settings.chosenColor;
            color.a *= strength;
            _chosen.color = color;
        }
    }
}
