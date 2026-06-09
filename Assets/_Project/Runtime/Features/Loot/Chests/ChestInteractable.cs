using UnityEngine;

/// <summary>
/// 책임 : 보물상자를 상호작용 대상으로 노출하고,
/// 잠금 상태에 따른 프롬프트와 하이라이트를 관리하며 열기 상호작용을 연결한다.
/// </summary>
[RequireComponent(typeof(TreasureChest))]
public class ChestInteractable : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string openPromptText = "상자 열기";
    [SerializeField] private string lockedPromptFormat = "잠김 ({0})";
    [SerializeField] private SpriteRenderer spriteRenderer;

    private TreasureChest chest;
    private ChestMonsterKillLock killLock;
    private MaterialPropertyBlock outlinePropertyBlock;

    private void Awake()
    {
        chest = GetComponent<TreasureChest>();
        killLock = GetComponent<ChestMonsterKillLock>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        outlinePropertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle)
            return false;

        if (killLock != null && !killLock.IsUnlocked)
            return false;

        return true;
    }

    public override InteractState GetInteractType() => InteractState.Shopping;

    public override void OnHighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }

    public override void OnUnHighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }

    public override string GetInteractDescription()
    {
        if (killLock != null && !killLock.IsUnlocked)
            return string.Format(lockedPromptFormat, killLock.RemainingAliveCount);

        return openPromptText;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (chest == null || player == null)
            return;

        if (!CanInteract(player))
            return;

        if (chest.Open(player))
            player.SetInteractState(InteractState.Shopping);
    }
}
