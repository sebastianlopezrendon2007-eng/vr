using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Telemedicina
{
    public sealed class QuestInputField : InputField
    {
        private void Open() { if (IsInteractable() && !readOnly) QuestKeyboard.Show(this); }
        public override void OnPointerClick(PointerEventData e) { if (QuestRig.Active) Open(); else base.OnPointerClick(e); }
    }
}
