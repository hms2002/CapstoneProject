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
    private const string DeadControlBlockTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";
    private const string DefaultTrapCauseName = "구덩이";
    private const string DefaultMonsterCauseName = "알 수 없는 적";
    private const string TimeOverCauseName = "마왕의 인내심";

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
    private string lastDamageSourceName;
    private GameOverCauseKind lastDamageCauseKind = GameOverCauseKind.Monster;
    private GameplayTagSet deadControlBlockTagSet;
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
        if (deadControlBlockTagSet == null) deadControlBlockTagSet = Resources.Load<GameplayTagSet>(DeadControlBlockTagSetResourcePath);

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider2D>(includeInactive: false);
    }

    private void OnEnable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged += HandleAttributeChanged;

        if (abilitySystem != null)
            abilitySystem.GameplayEventRaised += HandleGameplayEvent;

        TryStartDeathSequenceFromCurrentHp();
    }

    private void OnDisable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= HandleAttributeChanged;

        if (abilitySystem != null)
            abilitySystem.GameplayEventRaised -= HandleGameplayEvent;
    }

    private void HandleGameplayEvent(GameplayTag tag, AbilityEventData data)
    {
        if (abilitySystem == null || tag != abilitySystem.DamagedTag)
            return;

        if (data.Target != null && data.Target != gameObject)
            return;

        CaptureDamageSource(data.Causer, data.Instigator);
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute != hpDef || isDeathSequenceRunning)
            return;

        if (newValue > 0f)
            return;

        TryStartDefeatSequence();
    }

    private void TryStartDeathSequenceFromCurrentHp()
    {
        if (isDeathSequenceRunning || attributeSet == null || hpDef == null)
            return;

        if (attributeSet.GetAttributeValue(hpDef) <= 0f)
            TryStartDefeatSequence();
    }

    public bool TryStartTimeOverSequence(string targetHubSceneName = null, bool useSceneTransitionService = true)
    {
        return TryStartGameOverSequence(
            GameOverCauseKind.TimeOver,
            TimeOverCauseName,
            RunEndReason.TimeOver,
            ResolveHubSceneName(targetHubSceneName),
            useSceneTransitionService);
    }

#if UNITY_EDITOR
    [ContextMenu("Editor/Set Health To 1")]
    private void EditorSetHealthToOneContextMenu()
    {
        EditorTrySetHealthToOne();
    }

    public bool EditorTrySetHealthToOne()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerDeathReturnToHub2D] Set Health To 1 is only available in Play Mode.", this);
            return false;
        }

        if (attributeSet == null)
            attributeSet = GetComponent<AttributeSet>();

        if (attributeSet == null)
        {
            Debug.LogWarning("[PlayerDeathReturnToHub2D] AttributeSet is missing.", this);
            return false;
        }

        if (hpDef == null)
        {
            Debug.LogWarning("[PlayerDeathReturnToHub2D] HP AttributeDefinition is missing.", this);
            return false;
        }

        float previousHealth = attributeSet.GetAttributeValue(hpDef);
        if (!attributeSet.TrySetCurrentValue(hpDef, 1f, this))
        {
            Debug.LogWarning("[PlayerDeathReturnToHub2D] Failed to set player health to 1.", this);
            return false;
        }

        float currentHealth = attributeSet.GetAttributeValue(hpDef);
        Debug.Log($"[PlayerDeathReturnToHub2D] Player health changed {previousHealth:0.##} -> {currentHealth:0.##}.", this);
        return true;
    }
#endif

    private bool TryStartDefeatSequence()
    {
        if (isDeathSequenceRunning)
            return false;

        string causeName = string.IsNullOrWhiteSpace(lastDamageSourceName)
            ? (lastDamageCauseKind == GameOverCauseKind.Trap ? DefaultTrapCauseName : DefaultMonsterCauseName)
            : lastDamageSourceName;

        return TryStartGameOverSequence(
            lastDamageCauseKind,
            causeName,
            RunEndReason.Defeat,
            ResolveHubSceneName(null),
            useSceneTransitionService: true);
    }

    private bool TryStartGameOverSequence(
        GameOverCauseKind causeKind,
        string causeName,
        RunEndReason endRunReason,
        string targetHubSceneName,
        bool useSceneTransitionService)
    {
        if (isDeathSequenceRunning)
            return false;

        isDeathSequenceRunning = true;
        StartCoroutine(CoDeathSequence(causeKind, causeName, endRunReason, targetHubSceneName, useSceneTransitionService));
        return true;
    }

    private IEnumerator CoDeathSequence(
        GameOverCauseKind causeKind,
        string causeName,
        RunEndReason endRunReason,
        string targetHubSceneName,
        bool useSceneTransitionService)
    {
        BlockPlayerControl();
        CenterCameraOnDeath();

        if (deathPresentation != null)
        {
            yield return deathPresentation.Play();
        }
        else if (fallbackDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(fallbackDelaySeconds);
        }

        if (TryShowGameOverPresentation(causeKind, causeName, endRunReason, targetHubSceneName, useSceneTransitionService))
            yield break;

        ReturnToHub(endRunReason, targetHubSceneName, useSceneTransitionService);
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

        WeaponEquipController weaponEquipController = GetComponentInChildren<WeaponEquipController>(true);
        weaponEquipController?.Clear();

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

    private void CenterCameraOnDeath()
    {
        CameraBootstrap.CenterGameplayCameraOn(transform);
    }

    private bool TryShowGameOverPresentation(
        GameOverCauseKind causeKind,
        string causeName,
        RunEndReason endRunReason,
        string targetHubSceneName,
        bool useSceneTransitionService)
    {
        GameOverPresentationRequest request = causeKind == GameOverCauseKind.TimeOver
            ? GameOverPresentationRequest.TimeOver(transform, targetHubSceneName, useSceneTransitionService)
            : GameOverPresentationRequest.Defeat(transform, causeName, causeKind, targetHubSceneName, useSceneTransitionService);

        request.CauseName = string.IsNullOrWhiteSpace(causeName) ? request.CauseName : causeName;
        request.EndRunOnReturn = true;
        request.EndRunReason = endRunReason;

        return GameOverPresentationController.TryShow(request);
    }

    private void CaptureDamageSource(object causer, object instigator)
    {
        string resolvedName = ResolveEnemyCauseName(causer) ??
                              ResolveEnemyCauseName(instigator) ??
                              ResolveCauseName(causer) ??
                              ResolveCauseName(instigator);
        if (string.IsNullOrWhiteSpace(resolvedName))
            return;

        lastDamageSourceName = resolvedName;
        lastDamageCauseKind = ResolveCauseKind(resolvedName);
    }

    private static string ResolveCauseName(object causer)
    {
        switch (causer)
        {
            case GameObject go when go != null:
                return ResolveGameObjectCauseName(go);

            case Component component when component != null:
                return ResolveGameObjectCauseName(component.gameObject);

            case UnityEngine.Object unityObject when unityObject != null:
                return SanitizeObjectName(unityObject.name);

            default:
                return null;
        }
    }

    private static string ResolveGameObjectCauseName(GameObject source)
    {
        if (source == null)
            return null;

        string enemyName = ResolveEnemyCauseName(source);
        return !string.IsNullOrWhiteSpace(enemyName)
            ? enemyName
            : SanitizeObjectName(source.name);
    }

    private static string ResolveEnemyCauseName(object source)
    {
        switch (source)
        {
            case GameObject go when go != null:
                return ResolveEnemyCauseName(go);

            case Component component when component != null:
                return ResolveEnemyCauseName(component.gameObject);

            default:
                return null;
        }
    }

    private static string ResolveEnemyCauseName(GameObject source)
    {
        if (source == null)
            return null;

        Enemy enemy = source.GetComponentInParent<Enemy>();
        return enemy != null ? SanitizeObjectName(enemy.EnemyName) : null;
    }

    private static string SanitizeObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        return objectName
            .Replace("(Clone)", string.Empty)
            .Trim();
    }

    private static GameOverCauseKind ResolveCauseKind(string causeName)
    {
        if (string.IsNullOrWhiteSpace(causeName))
            return GameOverCauseKind.Monster;

        string normalized = causeName.ToLowerInvariant();
        return normalized.Contains("pit") ||
               normalized.Contains("hole") ||
               normalized.Contains("trap") ||
               normalized.Contains("hazard") ||
               normalized.Contains("puddle") ||
               normalized.Contains("구덩") ||
               normalized.Contains("함정")
            ? GameOverCauseKind.Trap
            : GameOverCauseKind.Monster;
    }

    private string ResolveHubSceneName(string targetHubSceneName)
    {
        return string.IsNullOrWhiteSpace(targetHubSceneName) ? hubSceneName : targetHubSceneName;
    }

    private void ReturnToHub(RunEndReason endRunReason, string targetHubSceneName, bool useSceneTransitionService)
    {
        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(endRunReason);

        string resolvedHubSceneName = ResolveHubSceneName(targetHubSceneName);

        if (useSceneTransitionService)
        {
            SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.Instance;
            if (transitionCoordinator != null && transitionCoordinator.TryLoadScene(resolvedHubSceneName))
                return;
        }

        SceneManager.LoadScene(resolvedHubSceneName);
    }

    /// <summary>
    /// 책임 :
    /// - 사망 상태에서 항상 같이 다녀야 하는 태그 묶음을 GameplayTagSet으로 전개해 적용한다.
    /// - 개별 AddTag 호출을 흩뿌리지 않고 사망 규칙의 단일 진실 원천을 유지한다.
    /// </summary>
    private void ApplyDeathStateTags()
    {
        if (tagSystem == null)
            return;

        deathTagsBuffer.Clear();
        deathStateTagSet?.CollectTags(deathTagsBuffer);
        deadControlBlockTagSet?.CollectTags(deathTagsBuffer);

        foreach (var tag in deathTagsBuffer)
        {
            if (tag == null)
                continue;

            tagSystem.AddTag(tag, 1);
        }
    }
}
