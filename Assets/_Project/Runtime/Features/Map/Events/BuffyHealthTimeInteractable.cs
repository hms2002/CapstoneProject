using System.Collections;
using UnityEngine;
using UnityGAS;

public enum BuffyWorkoutType
{
    Strength = 0,
    Wheel = 1,
    Log = 2
}

/// <summary>
/// 책임 : 세 종류의 버피 운동기구 중 이 컴포넌트에 지정된 운동 보상을 직접 1회 지급한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class BuffyHealthTimeInteractable : InteractableBase
{
    private const string EventId = "buffy_health_time";
    [Header("Rewards")]
    [SerializeField] private BuffyWorkoutType workoutType;
    [SerializeField] private AttributeDefinition attackBaseAttribute;
    [SerializeField] private AttributeDefinition moveSpeedMultiplierAttribute;
    [SerializeField] private LevelProgressionConfigSO levelProgressionConfig;
    [SerializeField] private float attackBaseBonus = 10f;
    [SerializeField] private float moveSpeedMultiplierBonus = 0.15f;

    [Header("Interaction")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "운동기구 사용하기";
    [SerializeField] private SpriteRenderer[] highlightedRenderers;

    [Header("Workout Presentation")]
    [SerializeField] private DialogueTrigger introductionSource;
    [SerializeField] private BuffyHealthTimeInteractable[] equipmentGroup;
    [SerializeField] private Transform guidanceArrow;
    [SerializeField] private ParticleSystem dustParticle;

    [SerializeField] private MonoBehaviour npcSpeechBubble;
    private ISpeechBubblePlayback speech;
    private MaterialPropertyBlock outlinePropertyBlock;
    private IPlayerInteractor activePlayer;
    private Color[] originalColors;
    private Vector3 arrowRestPosition;
    private bool resultPresented;
    private bool guidanceVisible;
    private bool disappearing;
    private float arrowTime;

    private string SelectionId => EventId + ":" + (int)workoutType;

    public BuffyWorkoutType WorkoutType => workoutType;

    private void Awake()
    {
        speech = npcSpeechBubble as ISpeechBubblePlayback;
        Collider2D interactionCollider = GetComponent<Collider2D>();
        if (interactionCollider != null)
            interactionCollider.isTrigger = true;

        if (highlightedRenderers == null || highlightedRenderers.Length == 0)
            highlightedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        outlinePropertyBlock = new MaterialPropertyBlock();
        originalColors = new Color[highlightedRenderers.Length];
        for (int i = 0; i < highlightedRenderers.Length; i++)
            if (highlightedRenderers[i] != null) originalColors[i] = highlightedRenderers[i].color;
        if (guidanceArrow != null) arrowRestPosition = guidanceArrow.localPosition;
        SetGuidance(false);
        if (dustParticle != null) dustParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Start()
    {
        if (!RunMapEventProgress.IsEventCompleted(RunSessionStore.Data, EventId)) return;
        bool hasSelection = false;
        if (equipmentGroup != null)
            foreach (BuffyHealthTimeInteractable equipment in equipmentGroup)
                if (equipment != null && RunMapEventProgress.IsEventCompleted(RunSessionStore.Data, equipment.SelectionId))
                    hasSelection = true;

        // Older completed runs have no selected-equipment marker; do not invent a choice.
        PresentResult(!hasSelection || RunMapEventProgress.IsEventCompleted(RunSessionStore.Data, SelectionId), false);
    }

    private void Update()
    {
        bool show = !resultPresented && introductionSource != null &&
                    GameDataStore.Data?.completedNpcRoomIntroductions?.Contains(introductionSource.IntroductionKey) == true &&
                    !RunMapEventProgress.IsEventCompleted(RunSessionStore.Data, EventId) &&
                    !DialoguePlayback.IsPlaying;
        if (show != guidanceVisible) SetGuidance(show);
        if (show && guidanceArrow != null)
        {
            arrowTime += TimeScalePausePlayback.PresentationDeltaTime;
            guidanceArrow.localPosition = arrowRestPosition + Vector3.up * (Mathf.Sin(arrowTime * 4f) * 0.12f);
        }
    }

    private void OnDisable()
    {
        speech?.HideActive();
        SetGuidance(false);
        StopAllCoroutines();
        if (dustParticle != null) dustParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (disappearing) gameObject.SetActive(false);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return !disappearing && player != null &&
               player.CurrentState == InteractState.Idle;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (RunMapEventProgress.IsEventCompleted(RunSessionStore.Data, EventId))
        {
            ShowSpeech("오늘의 운동은 이미 끝났어. 다음에도 건강하게 만나자!");
            return;
        }

        activePlayer = player;
        bool granted;
        try
        {
            granted = workoutType switch
            {
                BuffyWorkoutType.Strength => TryGrantAttackReward(),
                BuffyWorkoutType.Wheel => TryGrantMoveSpeedReward(),
                BuffyWorkoutType.Log => TryGrantExperienceReward(),
                _ => false
            };
        }
        finally
        {
            activePlayer = null;
        }

        if (granted)
        {
            RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, SelectionId);
            RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, EventId);
            PresentResult(true, false);
            if (equipmentGroup != null)
                foreach (BuffyHealthTimeInteractable equipment in equipmentGroup)
                    if (equipment != null && equipment != this)
                        equipment.PresentResult(false, true);
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription() => interactPromptText;

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnHighlight() => SetOutline(guidanceVisible);

    public override void OnUnHighlight() => SetOutline(guidanceVisible);

    public override void OnPlayerLeave() => OnUnHighlight();

    private bool TryGrantAttackReward()
    {
        if (!TryResolveAttributeSet(out AttributeSet attributes) || attackBaseAttribute == null)
            return ShowRewardConfigurationFailure("공격력");

        float nextValue = attributes.GetBaseValue(attackBaseAttribute) + Mathf.Max(0f, attackBaseBonus);
        if (!attributes.TrySetBaseValue(attackBaseAttribute, nextValue, this))
            return ShowRewardConfigurationFailure("공격력");

        ShowRewardPopup($"공격력 +{Mathf.Max(0f, attackBaseBonus):0.#}", "FF9933");
        return true;
    }

    private bool TryGrantMoveSpeedReward()
    {
        if (!TryResolveAttributeSet(out AttributeSet attributes) || moveSpeedMultiplierAttribute == null)
            return ShowRewardConfigurationFailure("이동속도");

        float safeBonus = Mathf.Max(0f, moveSpeedMultiplierBonus);
        float nextValue = attributes.GetBaseValue(moveSpeedMultiplierAttribute) + safeBonus;
        if (!attributes.TrySetBaseValue(moveSpeedMultiplierAttribute, nextValue, this))
            return ShowRewardConfigurationFailure("이동속도");

        ShowRewardPopup($"이동속도 +{safeBonus * 100f:0.#}%", "A6DFFF");
        return true;
    }

    private bool TryGrantExperienceReward()
    {
        LevelProgressionState state = RunLevelProgression.State;
        if (state == null || levelProgressionConfig == null)
            return ShowRewardConfigurationFailure("경험치");

        int requiredExperience = levelProgressionConfig.GetRequiredExperience(state.level);
        if (requiredExperience <= 0)
        {
            ShowSpeech("그건 안해도 되겠는데? 다른 운동을 해봐.");
            return false;
        }

        if (!RunLevelProgression.TryGrantExperience(levelProgressionConfig, requiredExperience, out _))
            return ShowRewardConfigurationFailure("경험치");

        ShowRewardPopup("레벨업 !", "B2FF99");
        return true;
    }

    private bool TryResolveAttributeSet(out AttributeSet attributes)
    {
        attributes = null;
        if (activePlayer is not Component playerComponent)
            return false;

        attributes = playerComponent.GetComponent<AttributeSet>();
        return attributes != null;
    }

    private bool ShowRewardConfigurationFailure(string rewardName)
    {
        WarningPopupPlayback.ShowMessage($"{rewardName} 보상을 적용할 수 없습니다.");
        CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[BuffyHealthTime] Could not apply {rewardName} reward.", this);
        return false;
    }

    private void ShowRewardPopup(string message, string colorHex)
    {
        if (activePlayer is Component playerComponent)
            DamagePopupPlayback.ShowText($"<color=#{colorHex}>{message}</color>", playerComponent.transform.position + Vector3.up);
    }

    private void SetGuidance(bool visible)
    {
        guidanceVisible = visible;
        SetOutline(visible);
        if (guidanceArrow != null)
        {
            guidanceArrow.gameObject.SetActive(visible);
            if (!visible) guidanceArrow.localPosition = arrowRestPosition;
        }
    }

    private void PresentResult(bool selected, bool playDust)
    {
        if (resultPresented) return;
        resultPresented = true;
        SetGuidance(false);
        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            SpriteRenderer renderer = highlightedRenderers[i];
            if (renderer == null) continue;
            if (selected)
            {
                Color color = originalColors[i];
                renderer.color = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, color.a);
            }
            else renderer.enabled = false;
        }
        if (selected) return;

        disappearing = true;
        foreach (Collider2D interactionCollider in GetComponentsInChildren<Collider2D>(true))
            interactionCollider.enabled = false;
        if (playDust && dustParticle != null && isActiveAndEnabled)
            StartCoroutine(Disappear());
        else gameObject.SetActive(false);
    }

    private IEnumerator Disappear()
    {
        dustParticle.Play(true);
        yield return null;
        while (dustParticle != null && dustParticle.IsAlive(true)) yield return null;
        gameObject.SetActive(false);
    }

    private void ShowSpeech(string message)
    {
        speech?.Speak(message, 4f);
    }

    private void SetOutline(bool enabled)
    {
        if (outlinePropertyBlock == null || highlightedRenderers == null)
            return;

        int outlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            SpriteRenderer renderer = highlightedRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetColor(Shader.PropertyToID("_OutlineColor"), Color.white);
            outlinePropertyBlock.SetFloat(outlineEnabledId, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
}
