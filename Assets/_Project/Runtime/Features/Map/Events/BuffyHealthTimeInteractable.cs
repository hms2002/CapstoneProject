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

    [SerializeField] private MonoBehaviour npcSpeechBubble;
    private ISpeechBubblePlayback speech;
    private MaterialPropertyBlock outlinePropertyBlock;
    private IPlayerInteractor activePlayer;

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
        OnUnHighlight();
    }

    private void OnDisable()
    {
        speech?.HideActive();
        OnUnHighlight();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
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
            RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, EventId);
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription() => interactPromptText;

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnHighlight() => SetOutline(true);

    public override void OnUnHighlight() => SetOutline(false);

    public override void OnPlayerLeave() => OnUnHighlight();

    private bool TryGrantAttackReward()
    {
        if (!TryResolveAttributeSet(out AttributeSet attributes) || attackBaseAttribute == null)
            return ShowRewardConfigurationFailure("공격력");

        float nextValue = attributes.GetBaseValue(attackBaseAttribute) + Mathf.Max(0f, attackBaseBonus);
        if (!attributes.TrySetBaseValue(attackBaseAttribute, nextValue, this))
            return ShowRewardConfigurationFailure("공격력");

        ShowPlayerSpeech($"공격력이 {Mathf.Max(0f, attackBaseBonus):0.#} 올랐어.");
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

        ShowPlayerSpeech($"이동속도가 {safeBonus * 100f:0.#}% 올랐어.");
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

        ShowPlayerSpeech("경험치를 받았어.");
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

    private void ShowPlayerSpeech(string message)
    {
        if (activePlayer is Component playerComponent)
            playerComponent.GetComponent<ISpeechBubblePlayback>()?.Speak(message, 4f);
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
            outlinePropertyBlock.SetFloat(outlineEnabledId, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
}
