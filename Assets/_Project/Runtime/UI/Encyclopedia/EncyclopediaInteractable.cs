using UnityEngine;

/// <summary>
/// 책임:
/// - 월드 도감 오브젝트의 상호작용 입력을 도감 UI 열기와 책 프레젠테이션으로 연결한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaInteractable : InteractableBase
{
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

    [Header("References")]
    [SerializeField] private EncyclopediaScreen screen;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private EncyclopediaCatalogSO catalog;

    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string promptText = "도감 보기";

    [Header("Highlight")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BookWorldSpriteSequencePresentation bookPresentation;

    [Header("Fallback")]
    [SerializeField] private bool resolveSceneScreenIfMissing = true;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (bookPresentation == null)
            bookPresentation = GetComponentInChildren<BookWorldSpriteSequencePresentation>(true);

        ResolveScreenIfNeeded();
        OnUnHighlight();
    }

    private void OnDisable()
    {
        SetOutlineEnabled(false);
        bookPresentation?.SnapClosed();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle)
            return false;

        ResolveScreenIfNeeded();

        if (screen == null)
            return false;

        ApplyDataSourcesToScreen();
        if (!screen.HasOpenableDataSource)
            return false;

        return UIManager.Instance == null || UIManager.Instance.CanOpenUI(screen);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        ApplyDataSourcesToScreen();
        UiCommandPlayback.HideWorldPrompt();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.TryPushUI(screen);
            return;
        }

        screen.OpenUI();
    }

    public override InteractState GetInteractType()
    {
        return InteractState.Shopping;
    }

    public override string GetInteractDescription()
    {
        return promptText;
    }

    public override Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public override void OnHighlight()
    {
        SetOutlineEnabled(true);
        bookPresentation?.PlayOpen();
    }

    public override void OnUnHighlight()
    {
        SetOutlineEnabled(false);
        bookPresentation?.PlayClose();
    }

    private void ResolveScreenIfNeeded()
    {
        if (screen != null || !resolveSceneScreenIfMissing)
            return;

        screen = FindFirstObjectByType<EncyclopediaScreen>(FindObjectsInactive.Include);
    }

    private void ApplyDataSourcesToScreen()
    {
        if (screen == null)
            return;

        if (itemDatabase != null)
            screen.SetItemDatabase(itemDatabase);

        if (catalog != null)
            screen.SetCatalog(catalog);
    }

    private void SetOutlineEnabled(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
