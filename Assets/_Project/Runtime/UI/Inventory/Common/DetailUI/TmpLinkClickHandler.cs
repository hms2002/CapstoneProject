using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TmpLinkClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text text;
    public System.Action<string> onGlossaryKeyClicked;

    private void Reset()
    {
        text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null) return;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (linkIndex < 0) return;

        var linkInfo = text.textInfo.linkInfo[linkIndex];
        string linkId = linkInfo.GetLinkID();

        if (linkId != null && linkId.StartsWith("glossary:"))
        {
            string key = linkId.Substring("glossary:".Length);
            onGlossaryKeyClicked?.Invoke(key);
        }
    }
}
