using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using S = SafeMining.MiningUiStyle;

namespace SafeMining
{
    // Shared uGUI builders so the menu, result panel and HUD stay visually consistent.
    // Everything is layout-group driven; callers never position children by hand.
    public static class MiningUiKit
    {
        public sealed class IconText { public Text icon, label; public GameObject root; }
        public sealed class ButtonView { public Button button; public Image surface; public Text icon, label; }

        public static Image Box(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (sprite != null) { image.sprite = sprite; image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced; }
            return image;
        }

        public static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        public static LayoutElement Size(GameObject go, float width = -1, float height = -1, float flexibleWidth = -1)
        {
            var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width >= 0) e.minWidth = e.preferredWidth = width;
            if (height >= 0) e.minHeight = e.preferredHeight = height;
            e.flexibleWidth = flexibleWidth;
            return e;
        }

        public static VerticalLayoutGroup Pad(GameObject go, int horizontal, int vertical, float spacing = 0)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical); layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return layout;
        }

        public static Transform Row(Transform parent, string name, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft, bool expand = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<HorizontalLayoutGroup>(); layout.spacing = spacing; layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = expand; layout.childForceExpandHeight = false;
            return go.transform;
        }

        public static Transform Column(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<VerticalLayoutGroup>(); layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return go.transform;
        }

        public static Text Label(Transform parent, string name, string value, Font font, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = alignment; text.supportRichText = true; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.05f;
            return text;
        }

        // A glyph from the icon font in a square cell so rows of icons stay aligned.
        public static Text Icon(Transform parent, string glyph, int size, Color color)
        {
            var icon = Label(parent, "Icon", glyph, S.Icons, size, color, TextAnchor.MiddleCenter);
            icon.horizontalOverflow = HorizontalWrapMode.Overflow;
            Size(icon.gameObject, size + 2, size + 2, 0);
            return icon;
        }

        public static IconText IconLabel(Transform parent, string name, string glyph, string value, Font font, int size, Color color, float gap = 6)
        {
            var row = Row(parent, name, gap);
            return new IconText { root = row.gameObject, icon = Icon(row, glyph, size, color), label = Label(row, "Label", value, font, size, color) };
        }

        public static void Divider(Transform parent) => Size(Box(parent, "Divider", S.Line).gameObject, -1, 1, 1);

        public static IconText Chip(Transform parent, Color color)
        {
            var chip = Box(parent, "Chip", S.SurfaceRaised, S.Rounded);
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 12, 4, 4); layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false;
            var icon = Icon(chip.transform, "", S.SizeCaption, color);
            var label = Label(chip.transform, "Label", "", S.Body, S.SizeCaption, color);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return new IconText { root = chip.gameObject, icon = icon, label = label };
        }

        public static ButtonView Wire(Image surface, string glyph, string label, Color normal, Color hover, Color ink, Font font, UnityAction action, int height, TextAnchor alignment)
        {
            var button = surface.gameObject.AddComponent<Button>(); button.targetGraphic = surface; button.onClick.AddListener(action);
            surface.raycastTarget = true;
            SetColors(button, normal, hover);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var content = Row(surface.transform, "Content", 8, alignment);
            Stretch((RectTransform)content);
            content.GetComponent<LayoutElement>().ignoreLayout = true;
            content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(alignment == TextAnchor.MiddleCenter ? 12 : 0, 12, 0, 0);
            var view = new ButtonView { button = button, surface = surface };
            view.icon = Icon(content, glyph, 18, ink); view.icon.gameObject.SetActive(!string.IsNullOrEmpty(glyph));
            view.label = Label(content, "Label", label, font, 18, ink);
            view.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Size(surface.gameObject, 100, height, 1);
            return view;
        }

        public static void SetColors(Button button, Color normal, Color hover)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
            var colors = button.colors; colors.normalColor = normal; colors.highlightedColor = colors.selectedColor = hover;
            colors.pressedColor = Color.Lerp(normal, Color.black, .2f); colors.colorMultiplier = 1; colors.fadeDuration = .1f;
            button.colors = colors;
        }

        // Mutually exclusive options in one track. Selection uses neutral contrast, never a status colour.
        public sealed class Segmented
        {
            public ButtonView[] options;
            public void Select(int index)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    bool on = i == index;
                    SetColors(options[i].button, on ? S.Line : new Color(0, 0, 0, 0), on ? S.LineStrong : S.SurfaceRaised);
                    options[i].label.color = options[i].icon.color = on ? S.TextPrimary : S.TextSecondary;
                    options[i].label.font = on ? S.Strong : S.Body;
                }
            }
        }

        public static Segmented Segments(Transform parent, string name, (string glyph, string label)[] items, System.Action<int> onSelect)
        {
            var track = Box(parent, name, S.SurfaceSunken, S.Rounded);
            var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4); layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true;
            var view = new Segmented { options = new ButtonView[items.Length] };
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                view.options[i] = Wire(Box(track.transform, items[i].label + " option", Color.clear, S.Rounded), items[i].glyph, items[i].label,
                    Color.clear, S.SurfaceRaised, S.TextSecondary, S.Body, () => onSelect(index), 38, TextAnchor.MiddleCenter);
            }
            return view;
        }

        public static ButtonView Primary(Transform parent, string glyph, string label, UnityAction action, int height = 48)
            => Wire(Box(parent, label + " button", S.Safe, S.Rounded), glyph, label, S.Safe, Color.Lerp(S.Safe, Color.white, .15f), S.OnSafe, S.Strong, action, height, TextAnchor.MiddleCenter);

        public static ButtonView Secondary(Transform parent, string glyph, string label, UnityAction action, int height = 48)
        {
            var border = Box(parent, label + " button", S.LineStrong, S.Rounded);
            var fill = Box(border.transform, "Fill", S.SurfaceRaised, S.Rounded);
            Stretch(fill.rectTransform); fill.rectTransform.offsetMin = Vector2.one; fill.rectTransform.offsetMax = -Vector2.one;
            fill.GetComponent<LayoutElement>().ignoreLayout = true;
            // The border is the tinted graphic, so hover brightens the outline.
            return Wire(border, glyph, label, S.LineStrong, S.TextSecondary, S.TextPrimary, S.Body, action, height, TextAnchor.MiddleCenter);
        }

        public static ButtonView Link(Transform parent, string glyph, string label, UnityAction action, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var hit = Box(parent, label + " link", Color.clear);
            var view = Wire(hit, glyph, label, S.TextSecondary, S.TextPrimary, Color.white, S.Body, action, 30, alignment);
            // Links tint their text, not a surface: point the button at the label and keep the icon in step.
            view.button.targetGraphic = view.label; SetColors(view.button, S.TextSecondary, S.TextPrimary);
            hit.color = new Color(0, 0, 0, 0);
            view.icon.color = S.TextSecondary;
            return view;
        }
    }
}
