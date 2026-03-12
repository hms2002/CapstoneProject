using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponAbilityBlockView : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private Image iconImage;

    [Header("Meta")]
    [SerializeField] private TMP_Text inputHintText;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Body")]
    [SerializeField] private TMP_Text bodyText;

    public void Set(string header, Sprite icon, string inputHint, float cooldownSeconds, string body, System.Action<string> onGlossaryClick = null)
    {
        if (headerText != null) headerText.text = header;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (inputHintText != null)
            inputHintText.text = string.IsNullOrEmpty(inputHint) ? "" : inputHint;

        if (cooldownText != null)
        {
            if (cooldownSeconds > 0f)
                cooldownText.text = $"{cooldownSeconds:0.##}s";
            else
                cooldownText.text = "";
        }

        if (bodyText != null)
        {
            bodyText.text = body ?? "";

            // glossary link click support (DetailTextFormatter.ApplyGlossaryLinks)
            var handler = bodyText.GetComponent<TmpLinkClickHandler>();
            if (handler == null) handler = bodyText.gameObject.AddComponent<TmpLinkClickHandler>();
            handler.onGlossaryKeyClicked = onGlossaryClick;
        }
    }
}
