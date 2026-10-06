using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    public enum ButtonStyle
    {
        /// <summary>Main call to action (green).</summary>
        Primary,

        /// <summary>Regular button.</summary>
        Secondary,

        /// <summary>Text-only button that shows a background on hover.</summary>
        Ghost
    }

    /// <summary>A rounded panel with a soft drop shadow. Position <see cref="Root"/>; put content in <see cref="Body"/>.</summary>
    public readonly struct UiCard
    {
        public UiCard(RectTransform root, Image body)
        {
            Root = root;
            Body = body;
        }

        public RectTransform Root { get; }
        public Image Body { get; }
        public RectTransform BodyRect => Body.rectTransform;
    }

    /// <summary>Small helpers for building uGUI hierarchies from code, plus generated sprites and fonts.</summary>
    public static class UiFactory
    {
        private const int RoundedSpriteRadius = 32;

        private static Font _uiFont;
        private static Font _semibold;
        private static Font _display;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _frame;
        private static Sprite _rounded;
        private static Sprite _roundedOutline;
        private static Sprite _shadow;
        private static Sprite _glow;

        /// <summary>Called whenever a factory-made button is clicked (used for the click sound).</summary>
        public static Action ButtonClickFeedback;

        /// <summary>Body text font (Segoe UI on Windows, the built-in font elsewhere).</summary>
        public static Font UiFont => _uiFont != null ? _uiFont : (_uiFont = OsFont("Segoe UI"));

        /// <summary>Font for headings and labels.</summary>
        public static Font Semibold => _semibold != null ? _semibold : (_semibold = OsFont("Segoe UI Semibold", "Segoe UI"));

        /// <summary>Heavy font for the title.</summary>
        public static Font Display => _display != null ? _display : (_display = OsFont("Segoe UI Black", "Segoe UI Semibold", "Segoe UI"));

        public static Font CreateGlyphFont(BoardTheme theme)
        {
            if (theme.glyphStyle == PieceGlyphStyle.Letters || theme.glyphFontNames == null || theme.glyphFontNames.Length == 0)
                return UiFont;
            return Font.CreateDynamicFontFromOSFont(theme.glyphFontNames, 96);
        }

        private static Font OsFont(params string[] names)
        {
            var installed = Font.GetOSInstalledFontNames();
            foreach (var name in names)
            {
                if (Array.IndexOf(installed, name) >= 0)
                    return Font.CreateDynamicFontFromOSFont(name, 32);
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateRadialSprite(128, 0f));
        public static Sprite Ring => _ring != null ? _ring : (_ring = CreateRadialSprite(128, 0.8f));
        public static Sprite Frame => _frame != null ? _frame : (_frame = CreateFrameSprite(32, 6));

        /// <summary>White rounded rectangle for 9-slicing; use <see cref="SetRounded"/> to pick the corner radius.</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = CreateRoundedSprite(0f));

        /// <summary>Outline of a rounded rectangle (3 px at radius 32).</summary>
        public static Sprite RoundedOutline =>
            _roundedOutline != null ? _roundedOutline : (_roundedOutline = CreateRoundedSprite(3f));

        /// <summary>Soft blurred rectangle used as a drop shadow.</summary>
        public static Sprite Shadow => _shadow != null ? _shadow : (_shadow = CreateShadowSprite());

        /// <summary>Radial glow: opaque centre fading to transparent at the edge.</summary>
        public static Sprite Glow => _glow != null ? _glow : (_glow = CreateGlowSprite(128));

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre-anchored rect at a fixed position and size.</summary>
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null,
            bool raycastTarget = false)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>Gives <paramref name="image"/> rounded corners of <paramref name="radius"/> canvas units.</summary>
        public static Image SetRounded(Image image, float radius, bool outline = false)
        {
            image.sprite = outline ? RoundedOutline : Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = RoundedSpriteRadius / Mathf.Max(1f, radius);
            return image;
        }

        public static Image CreateRounded(string name, Transform parent, Color color, float radius,
            bool raycastTarget = false)
        {
            return SetRounded(CreateImage(name, parent, color, null, raycastTarget), radius);
        }

        /// <summary>Rounded panel with a drop shadow. The shadow is a sibling drawn behind the body.</summary>
        public static UiCard CreateCard(string name, Transform parent, Color color, float radius = 18f,
            float shadowStrength = 0.45f)
        {
            var root = CreateRect(name, parent);
            if (shadowStrength > 0f)
            {
                var shadow = CreateImage("Shadow", root, new Color(0f, 0f, 0f, shadowStrength), Shadow);
                shadow.type = Image.Type.Sliced;
                Stretch(shadow.rectTransform, -26f);
                shadow.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            }
            var body = CreateRounded("Body", root, color, radius);
            Stretch(body.rectTransform);
            return new UiCard(root, body);
        }

        /// <summary>Full-rect vertical gradient from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
        public static Image CreateGradient(string name, Transform parent, Color top, Color bottom)
        {
            var texture = new Texture2D(1, 256, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "GeneratedGradient"
            };
            for (int y = 0; y < 256; y++)
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 255f));
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f), 100f);
            var image = CreateImage(name, parent, Color.white, sprite);
            Stretch(image.rectTransform);
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment, Font font = null)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font != null ? font : UiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        /// <summary>Small upper-case section label.</summary>
        public static Text CreateLabel(string name, Transform parent, string content, BoardTheme theme, int fontSize = 15)
        {
            var text = CreateText(name, parent, content.ToUpperInvariant(), fontSize, theme.mutedText,
                TextAnchor.LowerLeft, Semibold);
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, BoardTheme theme,
            UnityAction onClick, int fontSize = 24, ButtonStyle style = ButtonStyle.Secondary)
        {
            var image = CreateRounded(name, parent, Color.white, 10f, raycastTarget: true);
            var button = image.gameObject.AddComponent<Button>();
            ApplyButtonStyle(button, theme, style);
            DisableKeyboardSubmit(button);
            button.onClick.AddListener(() => ButtonClickFeedback?.Invoke());
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var text = CreateText("Label", image.transform, label, fontSize,
                style == ButtonStyle.Ghost ? theme.mutedText : theme.buttonText, TextAnchor.MiddleCenter, Semibold);
            Stretch(text.rectTransform, 4f);
            return button;
        }

        public static void ApplyButtonStyle(Button button, BoardTheme theme, ButtonStyle style)
        {
            Color normal;
            switch (style)
            {
                case ButtonStyle.Primary: normal = theme.primary; break;
                case ButtonStyle.Ghost: normal = new Color(1f, 1f, 1f, 0f); break;
                default: normal = theme.button; break;
            }
            var hover = style == ButtonStyle.Ghost
                ? new Color(1f, 1f, 1f, 0.08f)
                : Color.Lerp(normal, Color.white, 0.14f);
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.selectedColor = normal;
            colors.pressedColor = style == ButtonStyle.Ghost ? new Color(1f, 1f, 1f, 0.14f) : Color.Lerp(normal, Color.black, 0.2f);
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        /// <summary>
        /// Keyboard input is handled explicitly by KeyboardBoardInput, so a clicked button must not stay
        /// selected, or Space/Enter would click it again.
        /// </summary>
        public static void DisableKeyboardSubmit(Selectable selectable)
        {
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            if (selectable is Button button)
            {
                button.onClick.AddListener(() =>
                {
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(null);
                });
            }
        }

        /// <summary>Horizontal slider with a rounded track, an accent fill and a round handle.</summary>
        public static Slider CreateSlider(string name, Transform parent, BoardTheme theme, float min, float max,
            float value, UnityAction<float> onChanged)
        {
            var root = CreateRect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var track = CreateRounded("Track", root, theme.button, 5f);
            SetAnchors(track.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f));

            var fillArea = CreateRect("FillArea", root);
            SetAnchors(fillArea, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f));
            var fill = CreateRounded("Fill", fillArea, theme.accent, 5f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = CreateRect("HandleArea", root);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);
            var handle = CreateImage("Handle", handleArea, Color.white, Circle, raycastTarget: true);
            handle.rectTransform.sizeDelta = new Vector2(26f, 0f);
            var shadow = handle.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -2f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onChanged != null)
                slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static LayoutElement AddLayout(Component component, float preferredHeight, float flexibleHeight = 0f)
        {
            var element = component.gameObject.GetComponent<LayoutElement>();
            if (element == null)
                element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = Mathf.Min(preferredHeight, 20f);
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static VerticalLayoutGroup AddVerticalLayout(Component component, float spacing, RectOffset padding = null)
        {
            var layout = component.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(Component component, float spacing,
            bool expandWidth = true)
        {
            var layout = component.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        /// <summary>Anti-aliased white disc, or ring when <paramref name="innerRadius"/> &gt; 0 (fraction of radius).</summary>
        private static Sprite CreateRadialSprite(int size, float innerRadius)
        {
            var texture = NewTexture(size, size, innerRadius > 0f ? "GeneratedRing" : "GeneratedCircle");
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - d);
                    if (innerRadius > 0f)
                        alpha *= Mathf.Clamp01(d - innerRadius * radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateGlowSprite(int size)
        {
            var texture = NewTexture(size, size, "GeneratedGlow");
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>White square outline usable as a 9-sliced frame.</summary>
        private static Sprite CreateFrameSprite(int size, int border)
        {
            var texture = NewTexture(size, size, "GeneratedFrame");
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = new Color32(255, 255, 255, edge ? (byte)255 : (byte)0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Rounded rectangle (radius 32) as a 9-sliced sprite; an outline when <paramref name="outlineWidth"/> &gt; 0.</summary>
        private static Sprite CreateRoundedSprite(float outlineWidth)
        {
            const int r = RoundedSpriteRadius;
            const int size = r * 2 + 4;
            var texture = NewTexture(size, size, outlineWidth > 0f ? "GeneratedRoundedOutline" : "GeneratedRounded");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, r);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (outlineWidth > 0f)
                        alpha *= Mathf.Clamp01(d + outlineWidth + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r + 1, r + 1, r + 1, r + 1));
        }

        private static Sprite CreateShadowSprite()
        {
            const int size = 128;
            const int blur = 26;
            var texture = NewTexture(size, size, "GeneratedShadow");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to a rounded rect inset by the blur width, faded over the blur width.
                    float d = RoundedRectDistance(x + 0.5f - blur, y + 0.5f - blur, size - 2 * blur, size - 2 * blur, 18f);
                    float t = Mathf.Clamp01(1f - (d + blur * 0.5f) / blur);
                    float alpha = t * t * (3f - 2f * t);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            const int border = blur + 20;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Signed distance from (x, y) to a rounded rect at the origin (negative inside).</summary>
        private static float RoundedRectDistance(float x, float y, float width, float height, float radius)
        {
            float qx = Mathf.Abs(x - width * 0.5f) - (width * 0.5f - radius);
            float qy = Mathf.Abs(y - height * 0.5f) - (height * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name
            };
        }
    }
}
