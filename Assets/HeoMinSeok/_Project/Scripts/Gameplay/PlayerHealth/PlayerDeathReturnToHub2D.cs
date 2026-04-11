using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어 HP가 0 이하가 되면 사망 시퀀스를 시작한다.
/// - 사망 직후 입력 차단은 TagSet으로 통일 적용하고, 능력/물리/충돌만 직접 정리한다.
/// - 사망 연출 후 허브 복귀 전환을 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDeathReturnToHub2D : MonoBehaviour
{
    private const string DeadStateTagSetResourcePath = "Tags/TagSet/TS_PlayerDeadState";

    [Header("Refs")]
    [SerializeField] private AttributeSet attributeSet;
    [SerializeField] private AttributeDefinition hpDef;
    [SerializeField] private PlayerInteractor2D player;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private PlayerHitFeedback2D hitFeedback;
    [SerializeField] private PlayerDeathPresentation2D deathPresentation;
    [SerializeField] private GameplayTagSet deathStateTagSet;

    [Header("Transition")]
    [SerializeField] private string hubSceneName = "ProtoTypeHub";
    [SerializeField] private float fallbackDelaySeconds = 1.25f;

    [Header("Optional Extra Blockers")]
    [SerializeField] private Behaviour[] additionalBehavioursToDisable;
    [SerializeField] private Collider2D[] collidersToDisable;

    private bool isDeathSequenceRunning;
    private readonly HashSet<GameplayTag> deathTagsBuffer = new();

    private void Awake()
    {
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
        if (player == null) player = GetComponent<PlayerInteractor2D>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (hitFeedback == null) hitFeedback = GetComponent<PlayerHitFeedback2D>();
        if (deathPresentation == null) deathPresentation = GetComponent<PlayerDeathPresentation2D>();
        if (deathStateTagSet == null) deathStateTagSet = Resources.Load<GameplayTagSet>(DeadStateTagSetResourcePath);

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider2D>(includeInactive: false);
    }

    private void OnEnable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged += HandleAttributeChanged;

        TryStartDeathSequenceFromCurrentHp();
    }

    private void OnDisable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= HandleAttributeChanged;
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute != hpDef || isDeathSequenceRunning)
            return;

        if (newValue > 0f)
            return;

        StartCoroutine(CoDeathSequence());
    }

    private void TryStartDeathSequenceFromCurrentHp()
    {
        if (isDeathSequenceRunning || attributeSet == null || hpDef == null)
            return;

        if (attributeSet.GetAttributeValue(hpDef) <= 0f)
            StartCoroutine(CoDeathSequence());
    }

    private IEnumerator CoDeathSequence()
    {
        if (isDeathSequenceRunning)
            yield break;

        isDeathSequenceRunning = true;

        BlockPlayerControl();

        if (deathPresentation != null)
        {
            yield return deathPresentation.Play();
        }
        else if (fallbackDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(fallbackDelaySeconds);
        }

        ReturnToHub();
    }

    private void BlockPlayerControl()
    {
        ApplyDeathStateTags();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPopups();
            UIManager.Instance.HideHoverImmediate();
            UIManager.Instance.HideWorldPrompt();
        }

        hitFeedback?.ForceEndReaction();

        if (abilitySystem != null)
        {
            abilitySystem.CancelCasting(force: true);
            abilitySystem.CancelExecution(force: true);
            abilitySystem.enabled = false;
        }

        if (player != null)
            player.SetInteractState(InteractState.None);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (collidersToDisable != null)
        {
            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                if (collidersToDisable[i] != null)
                    collidersToDisable[i].enabled = false;
            }
        }

        if (additionalBehavioursToDisable != null)
        {
            for (int i = 0; i < additionalBehavioursToDisable.Length; i++)
            {
                if (additionalBehavioursToDisable[i] != null)
                    additionalBehavioursToDisable[i].enabled = false;
            }
        }
    }

    private void ReturnToHub()
    {
        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(RunEndReason.Defeat);

        SceneManager.LoadScene(hubSceneName);
    }

    /// <summary>
    /// 책임 :
    /// - 사망 상태에서 항상 같이 다녀야 하는 태그 묶음을 GameplayTagSet으로 전개해 적용한다.
    /// - 개별 AddTag 호출을 흩뿌리지 않고 사망 규칙의 단일 진실 원천을 유지한다.
    /// </summary>
    private void ApplyDeathStateTags()
    {
        if (tagSystem == null || deathStateTagSet == null)
            return;

        deathTagsBuffer.Clear();
        deathStateTagSet.CollectTags(deathTagsBuffer);

        foreach (var tag in deathTagsBuffer)
        {
            if (tag == null)
                continue;

            tagSystem.AddTag(tag, 1);
        }
    }
}
