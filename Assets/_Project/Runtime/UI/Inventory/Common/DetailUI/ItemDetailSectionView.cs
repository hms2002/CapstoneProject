using TMPro;
using UnityEngine;

public class ItemDetailSectionView : MonoBehaviour
{
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;

    public void Set(string header, string body, System.Action<string> onGlossaryClick)
    {
        if (headerText) headerText.text = header;

        if (bodyText)
        {
            bodyText.text = body;

            var handler = bodyText.GetComponent<TmpLinkClickHandler>();
            if (handler == null) handler = bodyText.gameObject.AddComponent<TmpLinkClickHandler>();
            handler.onGlossaryKeyClicked = onGlossaryClick;
        }
    }
}
