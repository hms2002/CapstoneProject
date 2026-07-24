using TMPro;
using UnityEngine;

public class GlossaryPopup : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    public void Show(string title, string body)
    {
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;
        if (root) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        else gameObject.SetActive(false);
    }
}
