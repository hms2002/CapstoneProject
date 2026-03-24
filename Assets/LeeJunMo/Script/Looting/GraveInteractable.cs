using UnityEngine;

public enum GraveType { Weapon, Relic }

public class GraveInteractable : MonoBehaviour, IInteractable
{
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

    [HideInInspector] public int bonusDropCount;
    [HideInInspector] public float bonusRareChance;
    [HideInInspector] public float bonusEpicChance;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }

    public void OnHighlight()
    {
        if (spriteRenderer == null || isLooted) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public void OnUnHighlight()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public bool CanInteract(IPlayerInteractor player) => !isLooted && player != null && player.CurrentState == InteractState.Idle;
    public InteractState GetInteractType() => InteractState.Idle;
    public string GetInteractDescription() => interactPromptText;
    public void GetInteract(string text) { }
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player)) return;

        isLooted = true;
        OnUnHighlight();

        if (LootManager.Instance != null)
            LootManager.Instance.SpawnGraveLoot(transform.position, graveType, bonusDropCount, bonusRareChance, bonusEpicChance);

        if (destroyEffect != null)
            Instantiate(destroyEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
