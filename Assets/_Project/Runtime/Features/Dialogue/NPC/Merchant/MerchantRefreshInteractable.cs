using CapstoneAudio;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class MerchantRefreshInteractable : InteractableBase, IInteractionTargetCandidate, IInteractionPromptState
{
    private static readonly SoundRef RefreshSound = SoundRef.FromKey("sound_shopRefresher_Refresh");
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [Header("Ownership")]
    [SerializeField] private MerchantNPC owner;

    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "새로고침";

    [Header("Count")]
    [SerializeField] private TMP_Text remainingCountText;

    [Header("Animation")]
    [SerializeField] private Animator refreshButtonAnimator;
    [SerializeField] private string refreshAnimationTriggerName = "Refresh";
    [SerializeField] private string refreshAnimationStateName = string.Empty;
    [SerializeField, Min(0)] private int refreshAnimationLayer;

    private int refreshAnimationTriggerHash;
    private int refreshAnimationStateHash;
    private MaterialPropertyBlock outlinePropertyBlock;
    private SpriteRenderer spriteRenderer;
    private bool hasAwakened;

    public bool IsInteractPromptDisabled => CanShowRefreshTarget() && !CanRefresh();

    private void Awake()
    {
        hasAwakened = true;
        ResolveReferences();
        CacheAnimationHashes();
        outlinePropertyBlock = new MaterialPropertyBlock();

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;

        OnUnHighlight();
        RefreshPresentation();
    }

    private void OnEnable()
    {
        if (hasAwakened)
            RefreshPresentation();
    }

    private void OnValidate()
    {
        ResolveReferences();
        CacheAnimationHashes();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               CanRefresh();
    }

    public bool CanBeInteractionTarget(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               CanShowRefreshTarget();
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (owner.TryRefreshStock())
        {
            SoundPlaybackUtility.Play(RefreshSound, causer: gameObject, position: transform.position, sourceObject: this);
            PlayRefreshButtonAnimation();
            RefreshPresentation();
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        SetOutline(true);
    }

    public override void OnUnHighlight()
    {
        SetOutline(false);
    }

    public override string GetInteractDescription()
    {
        return CanShowRefreshTarget()
            ? interactPromptText
            : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public void AssignOwner(MerchantNPC merchant)
    {
        owner = merchant;
    }

    public bool CanAssignOwner(MerchantNPC merchant)
    {
        return merchant != null && (owner == null || owner == merchant);
    }

    public bool IsAssignedOwner(MerchantNPC merchant)
    {
        return merchant != null && owner == merchant;
    }

    public void RefreshPresentation()
    {
        ResolveReferences();

        bool shouldShow = CanShowRefreshTarget();
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        RefreshRemainingCountText();
    }

    private void ResolveReferences()
    {
        if (owner == null)
            owner = GetComponentInParent<MerchantNPC>();

        if (remainingCountText == null)
            remainingCountText = GetComponentInChildren<TMP_Text>(true);

        if (refreshButtonAnimator == null)
            refreshButtonAnimator = GetComponentInChildren<Animator>(true);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheAnimationHashes()
    {
        refreshAnimationTriggerHash = string.IsNullOrWhiteSpace(refreshAnimationTriggerName)
            ? 0
            : Animator.StringToHash(refreshAnimationTriggerName);

        refreshAnimationStateHash = string.IsNullOrWhiteSpace(refreshAnimationStateName)
            ? 0
            : Animator.StringToHash(refreshAnimationStateName);
    }

    private void PlayRefreshButtonAnimation()
    {
        if (refreshButtonAnimator == null)
            return;

        if (TryPlayRefreshAnimationState())
            return;

        if (refreshAnimationTriggerHash == 0)
            return;

        refreshButtonAnimator.ResetTrigger(refreshAnimationTriggerHash);
        refreshButtonAnimator.SetTrigger(refreshAnimationTriggerHash);
    }

    private bool TryPlayRefreshAnimationState()
    {
        if (refreshAnimationStateHash == 0)
            return false;

        if (refreshAnimationLayer < 0 || refreshAnimationLayer >= refreshButtonAnimator.layerCount)
            return false;

        if (!refreshButtonAnimator.HasState(refreshAnimationLayer, refreshAnimationStateHash))
            return false;

        refreshButtonAnimator.Play(refreshAnimationStateHash, refreshAnimationLayer, 0f);
        return true;
    }

    private void RefreshRemainingCountText()
    {
        if (remainingCountText == null)
            return;

        int remainingCount = owner != null ? owner.GetRemainingRefreshCount() : 0;
        remainingCountText.text = string.Format("{0}\uBC88 \uB0A8\uC74C", remainingCount);
    }

    private bool CanShowRefreshTarget()
    {
        return owner != null && owner.CanShowRefreshInteractable();
    }

    private bool CanRefresh()
    {
        return owner != null && owner.CanRefreshStock();
    }

    private void SetOutline(bool enabled)
    {
        if (spriteRenderer == null || outlinePropertyBlock == null)
            return;

        spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }
}
