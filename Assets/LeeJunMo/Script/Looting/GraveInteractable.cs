using CapstoneAudio;
using UnityEngine;

public enum GraveType { Weapon, Relic }

public class GraveInteractable : InteractableBase
{
    private static readonly SoundRef OpenSound = SoundRef.FromKey("sound_grave_Open");

    [Header("유해 설정")]
    public GraveType graveType;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "조사하기";
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("이펙트")]
    public GameObject destroyEffect;

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private bool isLooted;

    [HideInInspector] public int bonusMinDropCount;
    [HideInInspector] public int bonusMaxDropCount;
    [HideInInspector] public float bonusRareChance;
    [HideInInspector] public float bonusEpicChance;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        if (spriteRenderer == null || isLooted) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public override void OnUnHighlight()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public override bool CanInteract(IPlayerInteractor player) => !isLooted && player != null && player.CurrentState == InteractState.Idle;
    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player)) return;

        isLooted = true;
        OnUnHighlight();

        SoundPlaybackUtility.Play(OpenSound, causer: gameObject, position: transform.position, sourceObject: this);

        if (LootManager.Instance != null)
            LootManager.Instance.SpawnGraveLoot(transform.position, graveType, bonusMinDropCount, bonusMaxDropCount, bonusRareChance, bonusEpicChance);

        if (destroyEffect != null)
            Instantiate(destroyEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
