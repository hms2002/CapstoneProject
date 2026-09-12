using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class QuestHudRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    public string QuestId { get; private set; }
    public bool IsLeaving { get; set; }
    public Tween Motion { get; set; }
    public RectTransform Rect => (RectTransform)transform;
    public void SetText(string id, string title, string description)
    {
        QuestId = id;
        if (titleLabel != null) titleLabel.gameObject.SetActive(false);
        if (descriptionLabel != null) descriptionLabel.text = description;
    }
    public void KillMotion() { Motion?.Kill(); Motion = null; }
    private void OnDestroy() => KillMotion();
}
