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

    private MaterialPropertyBlock _propBlock;
    private bool _isPlayerNearby;
    private bool _lastActivatedState;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    protected override void Awake()
    {
        base.Awake();

        _propBlock = new MaterialPropertyBlock();
        _lastActivatedState = IsActivated;

        ApplyRequirementVisual();
        RefreshVisualState();
        SetOutline(false);
    }

    private void LateUpdate()
    {
        bool activated = IsActivated;
        if (_lastActivatedState == activated)
            return;

        _lastActivatedState = activated;
        RefreshVisualState();
    }

    public override void OnPlayerNearby()
    {
        _isPlayerNearby = true;
        RefreshVisualState();
    }

    public override void OnPlayerLeave()
    {
        _isPlayerNearby = false;
        RefreshVisualState();
    }

    public override void OnHighlight()
    {
        if (IsActivated)
            return;

        if (highlightTarget != null)
            highlightTarget.SetActive(true);

        SetOutline(true);
    }

    public override void OnUnHighlight()
    {
        if (highlightTarget != null)
            highlightTarget.SetActive(false);

        SetOutline(false);
    }

    public override string GetInteractDescription()
    {
        if (IsActivated)
            return "이미 열려 있다";

        string typeName = costType == CostType.MagicStone ? "마정석" : "체력";
        return $"{typeName} {costAmount} 바치기";
    }

    protected override bool CheckCondition(IPlayerInteractor player)
    {
        switch (costType)
        {
            case CostType.MagicStone:
                int currentStone = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetMagicStone() : 0;
                bool canSpendStone = currentStone >= costAmount;
                if (!canSpendStone)
                    Debug.Log($"[StatueShortcut:{name}] 마정석이 부족하다. 현재={currentStone}, 필요={costAmount}");
                return canSpendStone;

            case CostType.HP:
                if (healthAttribute == null)
                {
                    Debug.LogWarning($"[StatueShortcut:{name}] healthAttribute가 연결되지 않았다.");
                    return false;
                }

                if (!TryGetPlayerAttributeSet(player, out var attributeSet))
                {
                    Debug.LogWarning($"[StatueShortcut:{name}] 플레이어 AttributeSet을 찾지 못했다.");
                    return false;
                }

                float currentHp = attributeSet.GetAttributeValue(healthAttribute);
                bool canSpendHp = allowLethalPayment
                    ? currentHp >= costAmount
                    : currentHp > costAmount;

                if (!canSpendHp)
                {
                    string ruleText = allowLethalPayment ? "현재 HP 이상 필요" : "최소 1 HP는 남아야 함";
                    Debug.Log($"[StatueShortcut:{name}] 체력이 부족하다. 현재={currentHp}, 필요={costAmount}, 규칙={ruleText}");
                }

                return canSpendHp;
        }

        return false;
    }

    protected override bool ConsumeCondition(IPlayerInteractor player)
    {
        switch (costType)
        {
            case CostType.MagicStone:
                return CurrencyManager.Instance != null && CurrencyManager.Instance.SpendMagicStone(costAmount);

            case CostType.HP:
                if (healthAttribute == null)
                    return false;

                if (!TryGetPlayerAttributeSet(player, out var attributeSet))
                    return false;

                return attributeSet.TryModifyAttributeValue(healthAttribute, -costAmount, this);
        }

        return false;
    }

    protected override void OnSuccess()
    {
        base.OnSuccess();
        RefreshVisualState();
        SetOutline(false);
    }

    private bool TryGetPlayerAttributeSet(IPlayerInteractor player, out AttributeSet attributeSet)
    {
        attributeSet = player != null ? player.Transform.GetComponent<AttributeSet>() : null;
        return attributeSet != null;
    }

    private void ApplyRequirementVisual()
    {
        if (requirementIconRenderer != null)
        {
            requirementIconRenderer.sprite = costType == CostType.MagicStone ? magicStoneIcon : hpIcon;
        }

        if (requirementAmountText != null)
        {
            requirementAmountText.text = costAmount.ToString();
        }
    }

    private void RefreshVisualState()
    {
        bool activated = IsActivated;

        if (requirementRoot != null)
            requirementRoot.SetActive(!activated);

        if (interactPromptRoot != null)
            interactPromptRoot.SetActive(!activated && _isPlayerNearby);

        if (activatedRoot != null)
            activatedRoot.SetActive(activated);

        if (highlightTarget != null && activated)
            highlightTarget.SetActive(false);
    }

    private void SetOutline(bool enabled)
    {
        if (highlightRenderer == null)
            return;

        highlightRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        highlightRenderer.SetPropertyBlock(_propBlock);
    }
}
