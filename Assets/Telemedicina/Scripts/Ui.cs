using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Telemedicina
{
    // UI de Unity en un plano del mundo: se puede sustituir la cámara y el método
    // de interacción por XR después, sin cambiar ApiClient ni el contrato clínico.
    internal static class Ui
    {
        internal static readonly Color Ink = new Color32(232, 242, 247, 255);
        internal static readonly Color Muted = new Color32(153, 177, 190, 255);
        internal static readonly Color Teal = new Color32(71, 123, 255, 255);
        internal static readonly Color PanelColor = new Color32(32, 43, 66, 240);
        private static Font font;
        internal static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        internal static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        internal static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, Color? color = null, bool raycast = false)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var img = rect.gameObject.AddComponent<Image>(); img.sprite = SpatialStyle.Rounded; img.type = Image.Type.Sliced; img.color = color ?? PanelColor; img.raycastTarget = raycast;
            return rect;
        }
        internal static Text Text(Transform parent, string name, string content, float x, float y, float w, float h, int size = 19, Color? color = null)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var text = rect.gameObject.AddComponent<Text>(); text.font = Font; text.fontSize = size;
            text.color = color ?? Ink; text.text = content; text.supportRichText = false;
            text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
            return text;
        }
        internal static Button Button(Transform parent, string caption, float x, float y, float w, float h, UnityAction click, bool accent = false)
        {
            var rect = Panel(caption, parent, x, y, w, h, accent ? Teal : new Color32(70, 82, 106, 245), true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(.75f, .75f, .75f); colors.disabledColor = new Color(.45f, .45f, .45f, .8f);
            button.colors = colors; button.onClick.AddListener(click);
            var label = Text(rect, "Etiqueta", caption, 8, 4, w - 16, h - 8, 18, accent ? Ink : Ink);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        internal static InputField Input(Transform parent, string name, string hint, float x, float y, float w, float h, bool password = false, bool multiline = false)
        {
            var rect = Panel(name, parent, x, y, w, h, new Color32(9, 26, 38, 255), true);
            var text = Text(rect, "Contenido", "", 12, 8, w - 24, h - 16);
            var placeholder = Text(rect, "Ayuda", hint, 12, 8, w - 24, h - 16, 18, Muted);
            var field = rect.gameObject.AddComponent<QuestInputField>();
            field.targetGraphic = rect.GetComponent<Image>(); field.textComponent = text; field.placeholder = placeholder;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 10000 : password ? 128 : 254;
            field.selectionColor = new Color(.1f, .7f, .65f, .4f); field.customCaretColor = true; field.caretColor = Ink;
            field.caretBlinkRate = .65f;
            return field;
        }
        internal static ScrollRect Scroll(Transform parent, string name, float x, float y, float w, float h, out RectTransform content)
        {
            var root = Panel(name, parent, x, y, w, h, new Color32(14, 32, 45, 255), true);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Área visible", root, 0, 0, w, h);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Contenido", viewport, 0, 0, w - 12, h);
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            var barRoot = Panel("Desplazamiento", root, w - 9, 0, 9, h, new Color32(31, 50, 62, 255), true);
            var slide = Rect("Carril", barRoot, 0, 0, 9, h);
            var handle = Panel("Indicador", slide, 0, 0, 9, h, Muted, true);
            var bar = barRoot.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); scroll.verticalScrollbar = bar;
            return scroll;
        }
        internal static void Clear(Transform parent)
        {
            foreach (Transform child in parent)
            { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
        }
    }
}
