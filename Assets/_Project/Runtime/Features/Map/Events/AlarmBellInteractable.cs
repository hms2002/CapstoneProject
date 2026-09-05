using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 경보 종 난이도 계산에 사용할 현재 인정 수를 외부 시스템에서 제공하게 하는 선택 계약이다.
/// </summary>
public interface IAlarmBellRecognitionCountProvider
{
    bool TryGetRecognitionCount(out int recognitionCount);
}

/// <summary>
/// 책임 : 경보 종 상호작용, 런 1회성 처리, 방 봉쇄 유지, 웨이브 소환과 완료 경험치 지급을 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class AlarmBellInteractable :
    InteractableBase,
    IProceduralRoomRuntimeFeature,
    IProceduralRoomEncounterRequirement
{
    private enum AlarmBellEncounterState
    {
        Unused,
        Activating,
        WaveCombat,
        Cleared
    }

    [Header("Definition")]
    [SerializeField] private AlarmBellEncounterDefinitionSO definition;
    [SerializeField] private string eventIdOverride;
    [SerializeField, Min(0)] private int fallbackRecognitionCount;
    [SerializeField] private MonoBehaviour recognitionCountProviderBehaviour;

    [Header("Interaction")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private SpriteRenderer[] highlightedRenderers;
    [SerializeField] private string alreadyUsedMessage = "이미 울린 경보 종입니다.";
    [SerializeField] private string invalidConfigurationMessage = "경보 종을 사용할 수 없습니다.";

    [Header("Spawn")]
    [SerializeField] private AlarmBellSpawnPoint[] spawnPoints;
    [SerializeField] private bool autoCollectChildSpawnPoints = true;
    [Tooltip("같은 경보 종 웨이브 안에서 몬스터들이 이 거리보다 가까운 스폰 위치를 되도록 피합니다.")]
    [SerializeField, Min(0f)] private float minimumSameWaveSpawnSeparation = 0.85f;
    [Tooltip("스폰 포인트를 다시 써야 할 때 완전 겹침을 피하기 위해 주변에 흩뿌릴 반경입니다.")]
    [SerializeField, Min(0f)] private float repeatedSpawnPointScatterRadius = 0.75f;
    [Tooltip("재사용 스폰 포인트 주변에서 방 안의 비어 보이는 위치를 찾는 시도 횟수입니다.")]
    [SerializeField, Min(1)] private int repeatedSpawnPointScatterAttempts = 8;

    [Header("Presentation")]
    [SerializeField] private Animator bellAnimator;
    [SerializeField] private string activateTrigger = "activate";
    [SerializeField] private string clearedTrigger = "cleared";
    [SerializeField] private GameObject activationVfxPrefab;
    [SerializeField] private GameObject spawnVfxPrefab;
    [SerializeField, Min(0f)] private float spawnedVfxLifetime = 2f;
    [SerializeField] private SoundRef activationSound;
    [SerializeField] private string activatedPopupMessage = "경보 종이 울렸다!";
    [SerializeField] private string clearedPopupMessage = "경보 종 전투 완료!";

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly List<GameObject> activeWaveMonsters = new();
    private readonly List<AlarmBellSpawnPoint> reusableSpawnCandidates = new();
    private readonly List<Vector3> reservedWaveSpawnPositions = new();
    private MaterialPropertyBlock outlinePropertyBlock;
    private MonsterSpawnRoomGroup roomGroup;
    private MonsterRoomArea2D roomArea;
    private Collider2D interactionCollider;
    private Coroutine encounterRoutine;
    private AlarmBellEncounterState state;
    private bool encounterHoldActive;

    public bool RequiresProceduralRoomEncounter => true;

    private string EventId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(eventIdOverride))
                return eventIdOverride;

            return definition != null ? definition.EventId : string.Empty;
        }
    }

    private void Awake()
    {
        interactionCollider = GetComponent<Collider2D>();
        if (interactionCollider != null)
            interactionCollider.isTrigger = true;

        if (bellAnimator == null)
            bellAnimator = GetComponentInChildren<Animator>(includeInactive: true);

        if (highlightedRenderers == null || highlightedRenderers.Length == 0)
            highlightedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        outlinePropertyBlock = new MaterialPropertyBlock();
        RefreshSpawnPointCache();
        RefreshPersistedState();
        OnUnHighlight();
    }

    private void OnEnable()
    {
        RefreshPersistedState();
    }

    private void OnDisable()
    {
        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }

        ReleaseEncounterHold();
        OnUnHighlight();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               state == AlarmBellEncounterState.Unused &&
               definition != null &&
               RunSessionStore.IsRunActive &&
               !IsEventAlreadyUsed() &&
               !HasPendingLevelReward();
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
        {
            if (IsEventAlreadyUsed())
                WarningPopupPlayback.ShowMessage(alreadyUsedMessage);

            return;
        }

        if (!TryValidateConfiguration(out string failureReason))
        {
            Debug.LogWarning($"[AlarmBell] {failureReason}", this);
            WarningPopupPlayback.ShowMessage(invalidConfigurationMessage);
            return;
        }

        RunMapEventProgress.MarkEventPresented(RunSessionStore.Data, EventId);
        state = AlarmBellEncounterState.Activating;
        SetInteractionEnabled(false);
        OnUnHighlight();
        encounterRoutine = StartCoroutine(RunEncounter(player));
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.InteractPromptText))
            return definition.InteractPromptText;

        return "경보 종 울리기";
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override void OnHighlight() => SetOutline(true);

    public override void OnUnHighlight() => SetOutline(false);

    public override void OnPlayerLeave() => OnUnHighlight();

    public bool TryBindProceduralRoom(
        ProceduralRoomRuntimeContext context,
        out string failureReason)
    {
        if (context == null)
        {
            failureReason = "Procedural room context is missing.";
            return false;
        }

        roomGroup = context.RoomGroup;
        roomArea = context.RoomArea;
        if (!context.HasRoomEncounter)
        {
            failureReason = "Alarm bell requires a procedural room encounter context.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private IEnumerator RunEncounter(IPlayerInteractor player)
    {
        bool completed = false;
        try
        {
            roomGroup?.NotifyPlayerEnteredEncounter();
            AcquireEncounterHold();
            PlayActivationPresentation(player);

            float activationDelay = definition != null ? definition.ActivationDelaySeconds : 0f;
            if (activationDelay > 0f)
                yield return new WaitForSeconds(activationDelay);

            int recognitionCount = ResolveRecognitionCount();
            if (!definition.TryResolveTier(recognitionCount, out AlarmBellEncounterTier tier))
            {
                Debug.LogWarning("[AlarmBell] No tier could be resolved.", this);
                yield break;
            }

            IReadOnlyList<AlarmBellWaveDefinition> waves = tier.Waves;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                AlarmBellWaveDefinition wave = waves[waveIndex];
                if (wave == null)
                    continue;

                state = AlarmBellEncounterState.WaveCombat;
                SpawnWave(wave, player);
                yield return WaitForCurrentWaveCleared();
                yield return WaitForLevelRewardFlowIdle();

                if (waveIndex < waves.Count - 1 && definition.NextWaveDelaySeconds > 0f)
                    yield return new WaitForSeconds(definition.NextWaveDelaySeconds);
            }

            RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, EventId);
            completed = true;
            ReleaseEncounterHold();
            CompleteEncounterPresentation();

            yield return WaitForLevelRewardFlowIdle();
            GrantCompletionExperience(tier);
            yield return WaitForLevelRewardFlowIdle();
        }
        finally
        {
            ReleaseEncounterHold();
            if (completed)
                CompleteEncounterPresentation();
            encounterRoutine = null;
        }
    }

    private void SpawnWave(AlarmBellWaveDefinition wave, IPlayerInteractor player)
    {
        IReadOnlyList<AlarmBellMonsterEntry> monsters = wave.Monsters;
        if (monsters == null)
            return;

        int stageIndex = MonsterRunProgression.CurrentStageIndex;
        Transform playerTransform = player != null ? player.Transform : PlayerRuntimeRegistry.GetPlayerTransform();
        reservedWaveSpawnPositions.Clear();
        for (int entryIndex = 0; entryIndex < monsters.Count; entryIndex++)
        {
            AlarmBellMonsterEntry entry = monsters[entryIndex];
            if (entry == null ||
                !entry.TryResolveMonsterPrefab(stageIndex, out GameObject monsterPrefab))
            {
                continue;
            }

            for (int spawnIndex = 0; spawnIndex < entry.Count; spawnIndex++)
                SpawnOneMonster(monsterPrefab, entry, playerTransform);
        }

        reservedWaveSpawnPositions.Clear();
    }

    private void SpawnOneMonster(
        GameObject monsterPrefab,
        AlarmBellMonsterEntry entry,
        Transform playerTransform)
    {
        if (monsterPrefab == null ||
            !TrySelectSpawnPoint(
                playerTransform,
                out AlarmBellSpawnPoint spawnPoint,
                out Vector3 spawnPosition,
                out Quaternion spawnRotation))
        {
            return;
        }

        PlaySpawnPresentation(spawnPosition, spawnRotation);
        var request = new MonsterSpawnRequest(
            monsterPrefab,
            spawnPosition,
            spawnRotation,
            roomArea,
            linkedChestKillLock: null,
            roomGroup);

        GameObject monster = MonsterSpawner.Instance != null
            ? MonsterSpawner.Instance.SpawnOne(request)
            : Instantiate(monsterPrefab, spawnPosition, spawnRotation);

        if (monster == null)
            return;

        if (MonsterSpawner.Instance == null)
            roomGroup?.NotifyMonsterSpawned(monster);

        ApplySpawnedMonsterOptions(monster, entry);
        activeWaveMonsters.Add(monster);
        LogDebug($"Spawned '{monster.name}' at '{spawnPoint.name}'.");
    }

    private bool TrySelectSpawnPoint(
        Transform playerTransform,
        out AlarmBellSpawnPoint selected,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation)
    {
        selected = null;
        spawnPosition = default;
        spawnRotation = Quaternion.identity;
        RefreshSpawnPointCache();
        if (spawnPoints == null || spawnPoints.Length == 0)
            return false;

        reusableSpawnCandidates.Clear();

        float minimumDistance = definition != null
            ? definition.MinimumPlayerSpawnDistance
            : 0f;
        float minimumSqrDistance = minimumDistance * minimumDistance;
        Vector3 playerPosition = playerTransform != null
            ? playerTransform.position
            : Vector3.positiveInfinity;

        for (int pass = 0; pass < 4; pass++)
        {
            reusableSpawnCandidates.Clear();
            bool requireUnreservedPosition = pass < 2;
            bool requireDistance = (pass == 0 || pass == 2) && playerTransform != null && minimumDistance > 0f;
            for (int pointIndex = 0; pointIndex < spawnPoints.Length; pointIndex++)
            {
                AlarmBellSpawnPoint point = spawnPoints[pointIndex];
                if (point == null || !point.IsAvailable)
                    continue;

                if (roomArea != null && !roomArea.Contains(point.Position))
                    continue;

                if (requireUnreservedPosition && IsReservedWaveSpawnPosition(point.Position))
                    continue;

                if (requireDistance &&
                    (point.Position - playerPosition).sqrMagnitude < minimumSqrDistance)
                {
                    continue;
                }

                reusableSpawnCandidates.Add(point);
            }

            if (reusableSpawnCandidates.Count > 0)
            {
                selected = reusableSpawnCandidates[Random.Range(0, reusableSpawnCandidates.Count)];
                spawnPosition = ResolveSameWaveSpawnPosition(selected.Position);
                spawnRotation = selected.Rotation;
                ReserveWaveSpawnPosition(spawnPosition);
                reusableSpawnCandidates.Clear();
                return selected != null;
            }
        }

        reusableSpawnCandidates.Clear();
        return false;
    }

    /// <summary>
    /// 책임:
    /// - 같은 경보 종 웨이브에서 이미 사용한 스폰 위치와 겹치지 않는 최종 위치를 결정한다.
    /// - 스폰 포인트 수가 몬스터 수보다 적거나 포인트 위치가 겹쳐도 완전 중첩 스폰을 최대한 피한다.
    /// </summary>
    private Vector3 ResolveSameWaveSpawnPosition(Vector3 basePosition)
    {
        if (!IsReservedWaveSpawnPosition(basePosition))
            return basePosition;

        float scatterRadius = Mathf.Max(0f, repeatedSpawnPointScatterRadius);
        if (scatterRadius <= 0.0001f)
            return basePosition;

        int attempts = Mathf.Max(1, repeatedSpawnPointScatterAttempts);
        float minimumRadius = Mathf.Min(Mathf.Max(0f, minimumSameWaveSpawnSeparation), scatterRadius);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minimumRadius, scatterRadius);
            Vector3 candidate = basePosition + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f);

            if (roomArea != null && !roomArea.Contains(candidate))
                continue;

            if (IsReservedWaveSpawnPosition(candidate))
                continue;

            return candidate;
        }

        return basePosition;
    }

    private void ReserveWaveSpawnPosition(Vector3 position)
    {
        reservedWaveSpawnPositions.Add(position);
    }

    private bool IsReservedWaveSpawnPosition(Vector3 position)
    {
        float minimumDistance = Mathf.Max(0f, minimumSameWaveSpawnSeparation);
        if (minimumDistance <= 0f)
            return false;

        float minimumSqrDistance = minimumDistance * minimumDistance;
        for (int i = 0; i < reservedWaveSpawnPositions.Count; i++)
        {
            if ((reservedWaveSpawnPositions[i] - position).sqrMagnitude < minimumSqrDistance)
                return true;
        }

        return false;
    }

    private IEnumerator WaitForCurrentWaveCleared()
    {
        while (true)
        {
            CompactActiveWaveMonsters();
            if (activeWaveMonsters.Count == 0)
                yield break;

            yield return null;
        }
    }

    private IEnumerator WaitForLevelRewardFlowIdle()
    {
        while (HasPendingLevelReward())
            yield return null;
    }

    private void GrantCompletionExperience(AlarmBellEncounterTier tier)
    {
        if (tier == null || tier.CompletionExperience <= 0)
            return;

        LevelProgressionConfigSO progressionConfig = definition.LevelProgressionConfig;
        if (progressionConfig == null)
        {
            Debug.LogWarning("[AlarmBell] Completion experience requires a LevelProgressionConfigSO.", this);
            return;
        }

        if (RunLevelProgression.TryGrantExperience(
                progressionConfig,
                tier.CompletionExperience,
                out LevelProgressionGrantResult result))
        {
            LogDebug(
                $"Completion EXP granted. amount={result.GrantedExperience}, " +
                $"level={result.PreviousLevel}->{result.CurrentLevel}, pending={result.PendingRewardCount}");
        }
    }

    private void ApplySpawnedMonsterOptions(
        GameObject monster,
        AlarmBellMonsterEntry entry)
    {
        if (monster == null || entry == null)
            return;

        if (entry.SuppressNonExperienceDrops &&
            TryResolveMob(monster, out Mob mob))
        {
            mob.SuppressMonsterLootDrop();
        }

        ApplyAdditionalHpMultiplier(monster, entry.AdditionalHpMultiplier);
        if (entry.OverrideSpriteTint)
            ApplySpriteTint(monster, entry.SpriteTint);
    }

    private void ApplyAdditionalHpMultiplier(GameObject monster, float multiplier)
    {
        if (monster == null ||
            multiplier <= 0f ||
            Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        AttributeSet attributes = ResolveComponentInMonster<AttributeSet>(monster);
        if (attributes == null)
            return;

        AbilitySystem abilitySystem = ResolveComponentInMonster<AbilitySystem>(monster);
        AttributeDefinition maxHealth = ResolveAttribute(
            attributes,
            abilitySystem,
            StatId.MaxHealth,
            "MaxHealth",
            "MaxHealthAttribute");
        if (maxHealth == null)
            return;

        AttributeDefinition health = ResolveAttribute(
            attributes,
            abilitySystem,
            StatId.Health,
            "Health",
            "HealthAttribute");

        float oldMaxHealth = Mathf.Max(0f, attributes.GetBaseValue(maxHealth));
        float newMaxHealth = oldMaxHealth * multiplier;
        attributes.TrySetBaseValue(maxHealth, newMaxHealth, this);

        if (health == null)
            return;

        float currentHealth = Mathf.Max(0f, attributes.GetBaseValue(health));
        float healthRatio = oldMaxHealth > 0.0001f
            ? Mathf.Clamp01(currentHealth / oldMaxHealth)
            : 1f;
        attributes.TrySetBaseValue(health, newMaxHealth * healthRatio, this);
    }

    private static AttributeDefinition ResolveAttribute(
        AttributeSet attributes,
        AbilitySystem abilitySystem,
        StatId statId,
        params string[] fallbackNames)
    {
        StatTypeBindings bindings = abilitySystem != null && abilitySystem.DamageProfile != null
            ? abilitySystem.DamageProfile.GetStatBindings()
            : null;

        if (bindings != null &&
            bindings.TryGetBinding(statId, out StatTypeBindings.Binding binding) &&
            binding != null &&
            binding.attribute != null)
        {
            return binding.attribute;
        }

        if (attributes == null || fallbackNames == null)
            return null;

        foreach (AttributeDefinition definition in attributes.EnumerateDefinitions())
        {
            if (definition == null)
                continue;

            for (int nameIndex = 0; nameIndex < fallbackNames.Length; nameIndex++)
            {
                string fallbackName = fallbackNames[nameIndex];
                if (string.IsNullOrWhiteSpace(fallbackName))
                    continue;

                if (string.Equals(definition.name, fallbackName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(definition.attributeName, fallbackName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }
        }

        return null;
    }

    private static void ApplySpriteTint(GameObject monster, Color tint)
    {
        if (monster == null)
            return;

        SpriteRenderer[] renderers = monster.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer renderer = renderers[rendererIndex];
            if (renderer != null)
                renderer.color = tint;
        }
    }

    private void CompactActiveWaveMonsters()
    {
        for (int i = activeWaveMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = activeWaveMonsters[i];
            if (!IsAliveMonster(monster))
                activeWaveMonsters.RemoveAt(i);
        }
    }

    private static bool IsAliveMonster(GameObject monster)
    {
        if (monster == null || !monster.activeInHierarchy)
            return false;

        Enemy enemy = ResolveComponentInMonster<Enemy>(monster);
        return enemy == null || !enemy.IsDead;
    }

    private static bool TryResolveMob(GameObject monster, out Mob mob)
    {
        mob = ResolveComponentInMonster<Mob>(monster);
        return mob != null;
    }

    /// <summary>
    /// 책임:
    /// - 몬스터 루트/자식 구조 차이를 숨기고 경보 종의 스폰 후처리와 사망 추적이 실제 전투 본체를 찾게 한다.
    /// - body/hurtbox 분리 리팩토링 이후에도 루트 프리팹 래퍼 때문에 방 봉쇄가 영구 유지되지 않도록 보호한다.
    /// </summary>
    private static T ResolveComponentInMonster<T>(GameObject monster) where T : Component
    {
        if (monster == null)
            return null;

        T component = monster.GetComponent<T>();
        if (component != null)
            return component;

        return monster.GetComponentInChildren<T>(includeInactive: true);
    }

    private void PlayActivationPresentation(IPlayerInteractor player)
    {
        TrySetAnimatorTrigger(activateTrigger);

        if (activationVfxPrefab != null)
        {
            GameObject vfx = Instantiate(activationVfxPrefab, transform.position, Quaternion.identity);
            if (spawnedVfxLifetime > 0f)
                Destroy(vfx, spawnedVfxLifetime);
        }

        SoundPlaybackUtility.Play(
            activationSound,
            instigator: player is Component component ? component.gameObject : null,
            causer: gameObject,
            position: transform.position,
            sourceObject: this);

        if (!string.IsNullOrWhiteSpace(activatedPopupMessage))
            WarningPopupPlayback.ShowMessage(activatedPopupMessage);
    }

    private void PlaySpawnPresentation(Vector3 position, Quaternion rotation)
    {
        if (spawnVfxPrefab == null)
            return;

        GameObject vfx = Instantiate(spawnVfxPrefab, position, rotation);
        if (spawnedVfxLifetime > 0f)
            Destroy(vfx, spawnedVfxLifetime);
    }

    private void CompleteEncounterPresentation()
    {
        if (state == AlarmBellEncounterState.Cleared)
            return;

        state = AlarmBellEncounterState.Cleared;
        SetInteractionEnabled(false);
        OnUnHighlight();
        TrySetAnimatorTrigger(clearedTrigger);

        if (!string.IsNullOrWhiteSpace(clearedPopupMessage))
            WarningPopupPlayback.ShowMessage(clearedPopupMessage);
    }

    private void TrySetAnimatorTrigger(string triggerName)
    {
        if (bellAnimator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        bellAnimator.SetTrigger(triggerName);
    }

    private void AcquireEncounterHold()
    {
        if (encounterHoldActive || roomGroup == null)
            return;

        roomGroup.PushEncounterHold();
        encounterHoldActive = true;
    }

    private void ReleaseEncounterHold()
    {
        if (!encounterHoldActive)
            return;

        roomGroup?.PopEncounterHold();
        encounterHoldActive = false;
    }

    private void RefreshSpawnPointCache()
    {
        if (!autoCollectChildSpawnPoints)
            return;

        if (spawnPoints != null && spawnPoints.Length > 0)
            return;

        spawnPoints = GetComponentsInChildren<AlarmBellSpawnPoint>(includeInactive: true);
    }

    private bool TryValidateConfiguration(out string failureReason)
    {
        if (definition == null)
        {
            failureReason = "Missing AlarmBellEncounterDefinitionSO.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(EventId))
        {
            failureReason = "Missing event id.";
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            failureReason = "Missing AlarmBellSpawnPoint children.";
            return false;
        }

        if (!HasAvailableSpawnPoint())
        {
            failureReason = "No available AlarmBellSpawnPoint exists.";
            return false;
        }

        int recognitionCount = ResolveRecognitionCount();
        if (!definition.TryResolveTier(recognitionCount, out AlarmBellEncounterTier tier))
        {
            failureReason = "No tier is configured.";
            return false;
        }

        IReadOnlyList<AlarmBellWaveDefinition> waves = tier.Waves;
        if (waves == null || waves.Count == 0)
        {
            failureReason = "Resolved tier has no waves.";
            return false;
        }

        if (!HasAnySpawnableMonster(waves, MonsterRunProgression.CurrentStageIndex))
        {
            failureReason = "Resolved tier has no spawnable monster entry.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private bool HasAvailableSpawnPoint()
    {
        for (int pointIndex = 0; pointIndex < spawnPoints.Length; pointIndex++)
        {
            AlarmBellSpawnPoint point = spawnPoints[pointIndex];
            if (point != null &&
                point.IsAvailable &&
                (roomArea == null || roomArea.Contains(point.Position)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnySpawnableMonster(
        IReadOnlyList<AlarmBellWaveDefinition> waves,
        int stageIndex)
    {
        if (waves == null)
            return false;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            AlarmBellWaveDefinition wave = waves[waveIndex];
            IReadOnlyList<AlarmBellMonsterEntry> monsters = wave != null ? wave.Monsters : null;
            if (monsters == null)
                continue;

            for (int monsterIndex = 0; monsterIndex < monsters.Count; monsterIndex++)
            {
                AlarmBellMonsterEntry entry = monsters[monsterIndex];
                if (entry != null && entry.TryResolveMonsterPrefab(stageIndex, out _))
                    return true;
            }
        }

        return false;
    }

    private void RefreshPersistedState()
    {
        if (definition == null || string.IsNullOrWhiteSpace(EventId))
            return;

        if (!RunSessionStore.IsRunActive)
            return;

        if (!IsEventAlreadyUsed())
            return;

        state = AlarmBellEncounterState.Cleared;
        SetInteractionEnabled(false);
        OnUnHighlight();
    }

    private bool IsEventAlreadyUsed()
    {
        GamePlayData data = RunSessionStore.Data;
        string eventId = EventId;
        return !string.IsNullOrWhiteSpace(eventId) &&
               RunMapEventProgress.IsEventCompleted(data, eventId);
    }

    private int ResolveRecognitionCount()
    {
        if (recognitionCountProviderBehaviour is IAlarmBellRecognitionCountProvider provider &&
            provider.TryGetRecognitionCount(out int providedCount))
        {
            return Mathf.Max(0, providedCount);
        }

        if (TryResolveLocalRecognitionCountProvider(out IAlarmBellRecognitionCountProvider localProvider) &&
            localProvider.TryGetRecognitionCount(out int localCount))
        {
            return Mathf.Max(0, localCount);
        }

        return Mathf.Max(0, fallbackRecognitionCount);
    }

    private bool TryResolveLocalRecognitionCountProvider(
        out IAlarmBellRecognitionCountProvider provider)
    {
        provider = null;
        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(includeInactive: true);
        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            if (behaviours[behaviourIndex] is IAlarmBellRecognitionCountProvider candidate)
            {
                provider = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool HasPendingLevelReward()
    {
        LevelProgressionState progression = RunLevelProgression.State;
        return progression != null &&
               (progression.pendingRewardCount > 0 ||
                (progression.activeRewardOffer != null && progression.activeRewardOffer.isActive));
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (interactionCollider != null)
            interactionCollider.enabled = enabled;
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

    private void LogDebug(string message)
    {
        if (!logDebug)
            return;

        Debug.Log($"[AlarmBell] {message}", this);
    }
}
