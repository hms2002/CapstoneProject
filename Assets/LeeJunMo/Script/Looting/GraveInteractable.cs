using UnityEngine;

public enum GraveType { Weapon, Relic }

public class GraveInteractable : MonoBehaviour, IInteractable
{
    [Header("유해 설정")]
    public GraveType graveType;
    [SerializeField] private GameObject visualCue;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("이펙트")]
    public GameObject destroyEffect;

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private bool isLooted = false;

    // Spawner가 부여해 준 업그레이드 보너스 스탯
    [HideInInspector] public int bonusDropCount = 0;
    [HideInInspector] public float bonusRareChance = 0f;
    [HideInInspector] public float bonusEpicChance = 0f;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (visualCue != null) visualCue.SetActive(false);
        OnUnHighlight();
    }

    public void OnPlayerNearby() { if (visualCue != null && !isLooted) visualCue.SetActive(true); }
    public void OnPlayerLeave() { if (visualCue != null) visualCue.SetActive(false); }

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

    public bool CanInteract(IPlayerInteractor player) => !isLooted && player.CurrentState == InteractState.Idle;
    public InteractState GetInteractType() => InteractState.Idle;

    // 코드는 Grave지만, 유저(기획)에게 보여지는 텍스트는 한국어 "유해"로 유지합니다!
    public string GetInteractDescription() => graveType == GraveType.Weapon ? "무기 유해 조사" : "유물 유해 조사";
    public void GetInteract(string text) { }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player)) return;

        isLooted = true;
        OnUnHighlight();
        if (visualCue != null) visualCue.SetActive(false);

        // 상호작용 시 책임은 LootManager로 위임
        if (LootManager.Instance != null)
        {
            LootManager.Instance.SpawnGraveLoot(transform.position, graveType, bonusDropCount, bonusRareChance, bonusEpicChance);
        }

        if (destroyEffect != null) Instantiate(destroyEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}