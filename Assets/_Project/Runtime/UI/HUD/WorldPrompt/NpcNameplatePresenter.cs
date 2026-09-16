using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects an NPC name and optional role icon onto an authored world canvas.</summary>
[DisallowMultipleComponent]
public sealed class NpcNameplatePresenter : MonoBehaviour
{
    [SerializeField] private NPCData npc;
    [SerializeField] private string fallbackName;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image roleIcon;
    [SerializeField] private Canvas worldCanvas;
    private const float FadeSeconds = 0.2f;
    private const float WorldScale = 0.01f;

    public Transform InteractionAnchor => interactionAnchor;

    private bool IsHidden => DialoguePlayback.IsPlaying || CinematicLetterboxOverlay.IsAnyVisible ||
        (PlayerRuntimeRegistry.CurrentPlayer != null && PlayerRuntimeRegistry.CurrentPlayer.CurrentState == InteractState.Talking);

    private void OnEnable()
    {
        if (nameText != null)
        {
            nameText.text = npc != null ? npc.npcName : fallbackName;
            nameText.raycastTarget = false;
            float width = nameText.GetPreferredValues(nameText.text).x;
            Sprite icon = npc != null ? npc.roleIcon : fallbackIcon;
            bool hasIcon = icon != null;
            nameText.rectTransform.sizeDelta = new Vector2(width + 4f, nameText.rectTransform.sizeDelta.y);
            float iconWidth = hasIcon && roleIcon != null ? roleIcon.rectTransform.sizeDelta.x : 0f;
            float iconSpace = hasIcon ? iconWidth + 8f : 0f;
            nameText.rectTransform.anchoredPosition = new Vector2(iconSpace * 0.5f, 0f);
            if (roleIcon != null)
            {
                roleIcon.gameObject.SetActive(hasIcon);
                roleIcon.sprite = icon;
                roleIcon.rectTransform.anchoredPosition = new Vector2(-(width + 8f) * 0.5f, 0f);
                roleIcon.raycastTarget = false;
            }
        }
        if (group != null)
        {
            group.alpha = IsHidden ? 0f : 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        FollowAnchor();
    }

    private void LateUpdate()
    {
        FollowAnchor();
        if (group != null)
            group.alpha = Mathf.MoveTowards(group.alpha, IsHidden ? 0f : 1f, Time.unscaledDeltaTime / FadeSeconds);
    }

    private void FollowAnchor()
    {
        if (anchor != null) transform.position = anchor.position;
        transform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(SafeScale(parentScale.x), SafeScale(parentScale.y), SafeScale(parentScale.z));
        if (worldCanvas != null && worldCanvas.worldCamera != Camera.main) worldCanvas.worldCamera = Camera.main;
    }

    private static float SafeScale(float parentScale) => Mathf.Abs(parentScale) > 0.0001f ? WorldScale / parentScale : WorldScale;
}
