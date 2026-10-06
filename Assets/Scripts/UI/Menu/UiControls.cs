using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>On/off switch: a pill track with a sliding knob. Clicking anywhere on the row toggles it.</summary>
    public sealed class UiSwitch : MonoBehaviour, IPointerClickHandler
    {
        private BoardTheme _theme;
        private Image _track;
        private RectTransform _knob;
        private float _position;
        private bool _isOn;

        public event Action<bool> Changed;

        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                _track.color = value ? _theme.primary : _theme.button;
            }
        }

        /// <summary>A row with <paramref name="label"/> on the left and the switch on the right.</summary>
        public static UiSwitch Create(string label, Transform parent, BoardTheme theme, bool isOn, Action<bool> onChanged)
        {
            var row = UiFactory.CreateImage(label, parent, new Color(0f, 0f, 0f, 0f), null, raycastTarget: true);
            var text = UiFactory.CreateText("Label", row.transform, label, 20, theme.text, TextAnchor.MiddleLeft);
            UiFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMax = new Vector2(-80f, 0f);

            var view = row.gameObject.AddComponent<UiSwitch>();
            view._theme = theme;
            view._track = UiFactory.CreateRounded("Track", row.transform, theme.button, 15f);
            var track = view._track.rectTransform;
            track.anchorMin = track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(58f, 30f);

            var knob = UiFactory.CreateImage("Knob", track, Color.white, UiFactory.Circle);
            view._knob = knob.rectTransform;
            view._knob.anchorMin = view._knob.anchorMax = new Vector2(0f, 0.5f);
            view._knob.sizeDelta = new Vector2(24f, 24f);
            var shadow = knob.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
            shadow.effectDistance = new Vector2(0f, -1.5f);

            view.IsOn = isOn;
            view._position = isOn ? 1f : 0f;
            view.PlaceKnob();
            if (onChanged != null)
                view.Changed += onChanged;
            return view;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            IsOn = !IsOn;
            UiFactory.ButtonClickFeedback?.Invoke();
            Changed?.Invoke(IsOn);
        }

        private void Update()
        {
            float target = _isOn ? 1f : 0f;
            if (Mathf.Approximately(_position, target))
                return;
            _position = Mathf.MoveTowards(_position, target, Time.unscaledDeltaTime * 8f);
            PlaceKnob();
        }

        private void PlaceKnob()
        {
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(15f, 43f, _position), 0f);
        }
    }

    /// <summary>A row of mutually exclusive buttons ("chips"); the chosen one is highlighted.</summary>
    public sealed class ChoiceGroup
    {
        private readonly BoardTheme _theme;
        private readonly List<(Image background, Image outline, Text label)> _items =
            new List<(Image, Image, Text)>();

        public ChoiceGroup(BoardTheme theme)
        {
            _theme = theme;
        }

        public int Selected { get; private set; } = -1;

        public event Action<int> Changed;

        /// <summary>Creates the chips as children of <paramref name="parent"/> (which should have a layout group).</summary>
        public static ChoiceGroup Create(Transform parent, BoardTheme theme, IReadOnlyList<string> labels, int selected,
            Action<int> onChanged, int fontSize = 19)
        {
            var group = new ChoiceGroup(theme);
            for (int i = 0; i < labels.Count; i++)
            {
                var chip = UiFactory.CreateRounded(labels[i], parent, Color.white, 10f, raycastTarget: true);
                var button = chip.gameObject.AddComponent<Button>();
                UiFactory.DisableKeyboardSubmit(button);
                var colors = button.colors;
                // Tints multiply the chip colour: slightly dimmed at rest, full brightness on hover.
                colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
                colors.selectedColor = colors.normalColor;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;

                var outline = UiFactory.CreateRounded("Outline", chip.transform, theme.accent, 10f);
                UiFactory.SetRounded(outline, 10f, outline: true);
                UiFactory.Stretch(outline.rectTransform);

                var label = UiFactory.CreateText("Label", chip.transform, labels[i], fontSize, theme.text,
                    TextAnchor.MiddleCenter, UiFactory.Semibold);
                UiFactory.Stretch(label.rectTransform, 4f);

                int index = i;
                button.onClick.AddListener(() =>
                {
                    UiFactory.ButtonClickFeedback?.Invoke();
                    group.Select(index);
                    group.Changed?.Invoke(index);
                });
                group._items.Add((chip, outline, label));
            }
            group.Select(selected);
            if (onChanged != null)
                group.Changed += onChanged;
            return group;
        }

        /// <summary>The chip at <paramref name="index"/> (to add content such as an icon).</summary>
        public Image GetChip(int index) => _items[index].background;

        public void Select(int index)
        {
            Selected = index;
            for (int i = 0; i < _items.Count; i++)
            {
                bool on = i == index;
                _items[i].background.color = on ? Color.Lerp(_theme.panelRaised, _theme.accent, 0.22f) : _theme.panelRaised;
                _items[i].outline.enabled = on;
                _items[i].label.color = on ? _theme.text : _theme.mutedText;
            }
        }
    }
}
