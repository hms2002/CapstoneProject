using TMPro;
using UnityEngine;
using UnityGAS;

public class StatueShortcut : TemporaryShortcut
{
    public enum CostType
    {
        MagicStone,
        HP
    }

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [Header("프롬프트")]
    [SerializeField] private string interactPromptText = "바치기";

    [Header("비용 설정")]
    [SerializeField] private CostType costType;
    [SerializeField] private int costAmount = 100;
    [SerializeField] private bool allowLethalPayment;

    [Header("HP 비용 설정")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Header("월드 표시")]
    [SerializeField] private GameObject requirementRoot;
    [SerializeField] private SpriteRenderer requirementIconRenderer;
    [SerializeField] private TMP_Text requirementAmountText;
    [SerializeField] private GameObject interactPromptRoot;
    [SerializeField] private GameObject activatedRoot;
    [SerializeField] private Sprite magicStoneIcon;
    [SerializeField] private Sprite hpIcon;

    [Header("하이라이트")]
    [SerializeField] private SpriteRenderer highlightRenderer;
    [SerializeField] private GameObject highlightTarget;

    private MaterialPropertyBlock propBlock;

    protected override void Awake()
    {
        base.Awake();

        propBlock = new MaterialPropertyBlock();
        ApplyRequirementVisual();
        RefreshVisualState();
        OnUnHighlight();
    }

    private void OnValidate()
    {
        ApplyRequirementVisual();
    }

    public override void OnPlayerNearby()
    {
        base.OnPlayerNearby();
        RefreshVisualState();
    }

    public override void OnPlayerLeave()
    {
        base.OnPlayerLeave();
        RefreshVisualState();
    }

    public override void OnHighlight()
    {
        if (highlightRenderer != null)
        {
            highlightRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(OutlineEnabledID, 1f);
            highlightRenderer.SetPropertyBlock(propBlock);
        }

        if (highlightTarget != null && !IsActivated)
            highlightTarget.SetActive(true);
    }

    public override void OnUnHighlight()
    {
        if (highlightRenderer != null)
        {
            highlightRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(OutlineEnabledID, 0f);
            highlightRenderer.SetPropertyBlock(propBlock);
        }

        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        RefreshVisualState();
        return base.CanInteract(player);
    }

    protected override bool CheckCondition(IPlayerInteractor player)
    {
        switch (costType)
        {
            case CostType.MagicStone:
                return CurrencyManager.Instance != null && CurrencyManager.Instance.GetMagicStone() >= costAmount;

            case CostType.HP:
                var attributeSet = player != null ? player.Transform.GetComponent<AttributeSet>() : null;
                if (attributeSet == null || healthAttribute == null)
                    return false;

                float currentHp = attributeSet.GetAttributeValue(healthAttribute);
                return allowLethalPayment ? currentHp >= costAmount : currentHp > costAmount;

            default:
                return false;
        }
    }

    protected override void ConsumeCondition(IPlayerInteractor player)
    {
        switch (costType)
        {
            case CostType.MagicStone:
                CurrencyManager.Instance?.SpendMagicStone(costAmount);
                break;

            case CostType.HP:
                var attributeSet = player != null ? player.Transform.GetComponent<AttributeSet>() : null;
                if (attributeSet != null && healthAttribute != null)
                    attributeSet.TryModifyAttributeValue(healthAttribute, -costAmount, this);
                break;
        }
    }

    protected override void OnSuccess()
    {
        base.OnSuccess();
        RefreshVisualState();
        OnUnHighlight();
    }

    public override string GetInteractDescription() => IsActivated ? string.Empty : interactPromptText;

    private bool IsActivated => targetDoor != null && targetDoor.IsOpen;

    private void RefreshVisualState()
    {
        bool activated = IsActivated;

        if (requirementRoot != null)
            requirementRoot.SetActive(!activated);

        if (interactPromptRoot != null)
            interactPromptRoot.SetActive(false);

        if (activatedRoot != null)
            activatedRoot.SetActive(activated);

        if (highlightTarget != null && activated)
            highlightTarget.SetActive(false);
    }

    private void ApplyRequirementVisual()
    {
        if (requirementAmountText != null)
            requirementAmountText.text = costAmount.ToString();

        if (requirementIconRenderer == null)
            return;

        requirementIconRenderer.sprite = costType == CostType.MagicStone ? magicStoneIcon : hpIcon;
    }
}
