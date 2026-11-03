using TMPro;
using UnityEngine;

/// <summary>Read-only projection of one registered affection reward into an authored row.</summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaAffectionRewardView : MonoBehaviour, UnityEngine.UI.ILayoutElement
{
    [SerializeField] private UnityEngine.UI.Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text descriptionText;

    private float measuredPreferredHeight = -1f;

    public float minWidth => -1f;
    public float preferredWidth => -1f;
    public float flexibleWidth => -1f;
    public float minHeight => -1f;
    public float preferredHeight => measuredPreferredHeight;
    public float flexibleHeight => -1f;
    public int layoutPriority => 1;

    public void CalculateLayoutInputHorizontal() { }

    public void CalculateLayoutInputVertical()
    {
        measuredPreferredHeight = -1f;
        if (descriptionText == null)
            return;

        RectTransform rect = descriptionText.rectTransform;
        if (rect.rect.width <= 0f)
            return;

        // The authored stretched text rect supplies the row's top/bottom inset;
        // the existing LayoutElement still owns the minimum row height.
        float textHeight = descriptionText.GetPreferredValues(descriptionText.text, rect.rect.width, Mathf.Infinity).y;
        measuredPreferredHeight = textHeight + rect.offsetMin.y - rect.offsetMax.y;
    }

    public void Bind(AffectionReward reward, int currentAffection)
    {
        gameObject.SetActive(true);
        if (levelText != null)
            levelText.text = GameText.Format("encyclopedia.affection_reward_level", "호감도 {0}", reward.targetLevel);
        if (statusText != null)
            statusText.text = currentAffection >= reward.targetLevel
                ? GameText.Get("code.encyclopediadetailpanel.d1496ce82d", "해금")
                : GameText.Get("code.encyclopediadetailpanel.b35f488f01", "미해금");
        if (descriptionText != null)
        {
            string description = reward.effect != null ? reward.effect.RewardText : null;
            descriptionText.text = !string.IsNullOrWhiteSpace(description)
                ? description
                : GameText.Get("code.encyclopediadetailpanel.00333329dd", "보상 설명 준비 중");
        }
        EncyclopediaPortraitUtility.SetSprite(iconImage, reward.effect != null ? reward.effect.rewardIcon : null);
    }

    public void Clear() => gameObject.SetActive(false);
}
