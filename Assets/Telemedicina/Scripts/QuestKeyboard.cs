using UnityEngine;
using UnityEngine.UI;

namespace Telemedicina
{
    public sealed class QuestKeyboard : MonoBehaviour
    {
        private static QuestKeyboard instance;
        private InputField target;
        private RectTransform panel;
        private bool upper;
        private Text fieldName;
        public void Create(Transform desk, Camera camera)
        {
            instance = this;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            gameObject.AddComponent<GraphicRaycaster>();
            panel = (RectTransform)transform; panel.SetParent(desk, false);
            panel.sizeDelta = new Vector2(1000, 315); panel.localScale = Vector3.one * .0016f;
            panel.localPosition = new Vector3(0, -.86f, -.25f); panel.localRotation = Quaternion.Euler(18, 0, 0);
            Ui.Panel("Fondo teclado", panel, 0, 0, 1000, 315, Ui.PanelColor, true);
            fieldName = Ui.Text(panel, "Campo", "", 16, 10, 965, 30, 21, Ui.Teal);
            string[] rows = { "1234567890@", "qwertyuiop", "asdfghjklñ", "zxcvbnm._-" };
            for (int row = 0; row < rows.Length; row++)
                for (int column = 0; column < rows[row].Length; column++)
                {
                    char letter = rows[row][column];
                    Ui.Button(panel, letter.ToString(), 16 + column * 86, 45 + row * 47, 78, 40, () => Add(upper ? char.ToUpperInvariant(letter).ToString() : letter.ToString()));
                }
            Ui.Button(panel, "Mayús", 16, 242, 143, 50, () => { upper = !upper; fieldName.text = target ? target.name + (upper ? " · MAYÚSCULAS" : "") : ""; });
            Ui.Button(panel, "Espacio", 171, 242, 235, 50, () => Add(" "));
            Ui.Button(panel, "Borrar", 418, 242, 145, 50, Backspace);
            Ui.Button(panel, "Enter", 575, 242, 146, 50, () => { if (target && target.lineType == InputField.LineType.MultiLineNewline) Add("\n"); });
            Ui.Button(panel, "Listo", 733, 242, 249, 50, Close, true);
            gameObject.SetActive(false);
        }
        public static void Show(InputField input)
        {
            if (!instance) return;
            instance.target = input; instance.upper = false; instance.fieldName.text = input.name;
            instance.gameObject.SetActive(true);
        }
        public static void Close() { if (instance) { instance.target = null; instance.gameObject.SetActive(false); } }
        private void Add(string text)
        {
            if (!target || !target.IsInteractable() || target.readOnly) { Close(); return; }
            if (target.characterLimit > 0 && target.text.Length + text.Length > target.characterLimit) return;
            target.text += text;
        }
        private void Backspace()
        {
            if (!target || !target.IsInteractable() || target.readOnly) { Close(); return; }
            if (target.text.Length > 0) target.text = target.text.Substring(0, target.text.Length - 1);
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
