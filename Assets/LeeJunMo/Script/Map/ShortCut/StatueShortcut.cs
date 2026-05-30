using CapstoneAudio;
using TMPro;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 제물 비용 타입에 따라 석상 비주얼을 전환하고, 지불 성공 후 석상 애니메이션 이벤트로 숏컷 개방을 완료합니다.
/// </summary>
public class StatueShortcut : TemporaryShortcut
{
    private static readonly SoundRef OfferMagicStoneSound = SoundRef.FromKey("sound_player_OfferMagicstone");
    private static readonly SoundRef OfferBloodSound = SoundRef.FromKey("sound_player_OfferBlood");

    public enum CostType
    {
        MagicStone,
        HP
    }

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OfferTriggerID = Animator.StringToHash("Offer");
    private static readonly int OfferingStateID = Animator.StringToHash("OfferingState");

    private const int OfferingStateEmpty = 0;
    private const int OfferingStateFilling = 1;
    private const int OfferingStateFilled = 2;

    /// <summary>
    /// 석상 비용 타입 하나에 대응하는 본체 스프라이트와 AnimatorController 세트를 보관합니다.
    /// </summary>
    [System.Serializable]
    private struct StatueVisualProfile
    {
        [SerializeField] private Sprite statueSprite;
        [SerializeField] private RuntimeAnimatorController animatorController;

        public Sprite StatueSprite => statueSprite;
        public RuntimeAnimatorController AnimatorController => animatorController;
    }

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

    [Header("석상 애니메이션")]
    [SerializeField] private Animator statueAnimator;
    [SerializeField] private bool waitForOfferingFillAnimationEvent = true;

    [Header("비용 타입별 비주얼")]
    [SerializeField] private SpriteRenderer statueRenderer;
    [SerializeField] private StatueVisualProfile magicStoneVisual;
    [SerializeField] private StatueVisualProfile hpVisual;

    [Header("하이라이트")]
    [SerializeField] private SpriteRenderer highlightRenderer;
    [SerializeField] private GameObject highlightTarget;

    private MaterialPropertyBlock propBlock;
    private bool waitingForOfferingFill;
    private IPlayerInteractor pendingSuccessPlayer;

    protected override void Awake()
    {
        base.Awake();

        propBlock = new MaterialPropertyBlock();
        ApplyCostTypeVisual();
        RefreshVisualState();
        OnUnHighlight();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ApplyCostTypeVisual();
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
        return !waitingForOfferingFill && base.CanInteract(player);
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
                PlayPaymentSound(OfferMagicStoneSound, player);
                break;

            case CostType.HP:
                var attributeSet = player != null ? player.Transform.GetComponent<AttributeSet>() : null;
                if (attributeSet != null && healthAttribute != null)
                {
                    attributeSet.TryModifyAttributeValue(healthAttribute, -costAmount, this);
                    PlayPaymentSound(OfferBloodSound, player);
                }
                break;
        }
    }

    /// <summary>석상 지불이 성공했을 때 플레이어 위치 기준으로 지불 사운드를 재생합니다.</summary>
    private void PlayPaymentSound(SoundRef sound, IPlayerInteractor player)
    {
        GameObject instigator = player != null && player.Transform != null
            ? player.Transform.gameObject
            : null;

        SoundPlaybackUtility.Play(sound, instigator: instigator, causer: gameObject, position: transform.position, sourceObject: this);
    }

    protected override bool TryBeginDeferredSuccess(IPlayerInteractor player)
    {
        if (!waitForOfferingFillAnimationEvent || statueAnimator == null)
            return false;

        waitingForOfferingFill = true;
        pendingSuccessPlayer = player;

        if (requirementRoot != null)
            requirementRoot.SetActive(false);

        if (interactPromptRoot != null)
            interactPromptRoot.SetActive(false);

        if (highlightTarget != null)
            highlightTarget.SetActive(false);

        statueAnimator.SetInteger(OfferingStateID, OfferingStateFilling);
        statueAnimator.ResetTrigger(OfferTriggerID);
        statueAnimator.SetTrigger(OfferTriggerID);

        return true;
    }

    public void CompleteOfferingFillAnimation()
    {
        if (!waitingForOfferingFill)
            return;

        IPlayerInteractor player = pendingSuccessPlayer;
        waitingForOfferingFill = false;
        pendingSuccessPlayer = null;

        if (statueAnimator != null)
            statueAnimator.SetInteger(OfferingStateID, OfferingStateFilled);

        CompleteSuccessfulInteraction(player);
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

        if (statueAnimator != null && !waitingForOfferingFill)
            statueAnimator.SetInteger(OfferingStateID, activated ? OfferingStateFilled : OfferingStateEmpty);
    }

    private void ApplyCostTypeVisual()
    {
        if (requirementAmountText != null)
            requirementAmountText.text = costAmount.ToString();

        StatueVisualProfile visual = costType == CostType.MagicStone ? magicStoneVisual : hpVisual;

        if (requirementIconRenderer != null)
            requirementIconRenderer.sprite = costType == CostType.MagicStone ? magicStoneIcon : hpIcon;

        if (statueRenderer != null && visual.StatueSprite != null)
            statueRenderer.sprite = visual.StatueSprite;

        if (statueAnimator != null && visual.AnimatorController != null)
            statueAnimator.runtimeAnimatorController = visual.AnimatorController;
    }
}
