using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cosecha.View
{
    /// <summary>Palette for UI and world presentation (not balancing).</summary>
    public static class Pal
    {
        public static readonly Color PanelBg = new Color32(250, 246, 236, 240);
        public static readonly Color PanelBorder = new Color32(120, 86, 52, 255);
        public static readonly Color Card = new Color32(255, 252, 244, 255);
        public static readonly Color CardDisabled = new Color32(214, 208, 196, 255);
        public static readonly Color Text = new Color32(58, 42, 30, 255);
        public static readonly Color TextMuted = new Color32(130, 112, 96, 255);
        public static readonly Color Button = new Color32(86, 140, 64, 255);
        public static readonly Color ButtonText = Color.white;
        public static readonly Color ButtonAlt = new Color32(70, 110, 160, 255);
        public static readonly Color ButtonDanger = new Color32(168, 82, 60, 255);
        public static readonly Color Good = new Color32(60, 150, 60, 255);
        public static readonly Color Bad = new Color32(190, 70, 60, 255);
        public static readonly Color Gold = new Color32(214, 160, 30, 255);
        public static readonly Color Dim = new Color32(20, 20, 30, 150);
        public static readonly Color InputBg = Color.white;
        public static readonly Color InputLockedGood = new Color32(200, 240, 196, 255);
        public static readonly Color InputWrong = new Color32(250, 214, 206, 255);
        public static readonly Color PreviewValid = new Color(0.3f, 1f, 0.3f, 0.55f);
        public static readonly Color PreviewInvalid = new Color(1f, 0.25f, 0.2f, 0.55f);
        public static readonly Color TargetGlow = new Color(1f, 0.95f, 0.3f, 0.6f);
    }

    /// <summary>Small helpers to build uGUI hierarchies from code.</summary>
    public static class Ui
    {
        public static SpriteSet Sprites;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(string name, Transform parent, Color? color = null) =>
            Image(name, parent, color ?? Pal.PanelBg, Sprites != null ? Sprites.panel : null);

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Sprites != null && Sprites.font != null) t.font = Sprites.font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Pal.Text;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, float fontSize, Action onClick, Color? color = null)
        {
            var img = Image(name, parent, color ?? Pal.Button, Sprites != null ? Sprites.button : null);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            b.colors = colors;
            var t = Text("Label", img.transform, label, fontSize, Pal.ButtonText, TextAlignmentOptions.Center);
            t.rectTransform.Stretch(2);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static void SetLabel(this Button b, string label) => b.GetComponentInChildren<TextMeshProUGUI>().text = label;

        public static TMP_InputField Input(string name, Transform parent, float fontSize, string placeholder = "")
        {
            var bg = Image(name, parent, Pal.InputBg, Sprites != null ? Sprites.button : null);
            var input = bg.gameObject.AddComponent<TMP_InputField>();

            var area = Rect("Text Area", bg.transform);
            area.Stretch();
            area.offsetMin = new Vector2(6, 2);
            area.offsetMax = new Vector2(-6, -2);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Text("Placeholder", area, placeholder, fontSize, Pal.TextMuted);
            ph.rectTransform.Stretch();
            ph.fontStyle = FontStyles.Italic;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            ph.alignment = TextAlignmentOptions.MidlineLeft;

            var txt = Text("Text", area, "", fontSize, Pal.Text);
            txt.rectTransform.Stretch();
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            if (Sprites != null && Sprites.font != null) input.fontAsset = Sprites.font;
            input.pointSize = fontSize;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onFocusSelectAll = false;
            input.resetOnDeActivation = false;
            input.restoreOriginalTextOnEscape = false;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.caretColor = Pal.Text;
            return input;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static VerticalLayoutGroup VLayout(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperCenter, bool expandWidth = true)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }
    }

    /// <summary>Shows a tooltip while the pointer is over a UI element.</summary>
    public class UiTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> Text;
        public static Action<string> Show;
        public static Action Hide;
        bool over;

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            var s = Text?.Invoke();
            if (!string.IsNullOrEmpty(s)) Show?.Invoke(s);
        }

        public void OnPointerExit(PointerEventData e)
        {
            over = false;
            Hide?.Invoke();
        }

        void OnDisable()
        {
            if (over) Hide?.Invoke();
            over = false;
        }
    }
}
