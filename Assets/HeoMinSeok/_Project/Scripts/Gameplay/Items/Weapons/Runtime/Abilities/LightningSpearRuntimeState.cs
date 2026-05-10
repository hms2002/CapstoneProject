using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class LightningSpearRuntimeState : WeaponAbilityRuntimeState
{
    private readonly List<LightningSpearMarkActor> activeMarks = new List<LightningSpearMarkActor>();
    private readonly List<Vector2> pendingPositions = new List<Vector2>();

    private AbilitySystem ownerSystem;
    private WeaponInventory2D weaponInventory;
    private PlayerAim2D aimSource;
    private MovementMotor2D movementMotor;
    private WeaponPresentationRig2D presentationRig;
    private GameObject rushRangeIndicatorInstance;
    private GameObject selectedMarkIndicatorInstance;
    private bool cursorInteractableSet;

    private void Awake()
    {
        CacheOwnerReferences(null);
    }

    private void Update()
    {
        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout))
        {
            ClearFeedback();
            return;
        }

        RefreshMarkFeedback(loadout, ResolveSkill1Data(loadout));
    }

    private void OnDisable()
    {
        ClearAllMarks();
        ClearFeedback();
        DestroyFeedbackObjects();
    }

    private void OnDestroy()
    {
        ClearAllMarks();
        ClearFeedback();
        DestroyFeedbackObjects();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheOwnerReferences(null);

        if (previousWeapon != newWeapon && previousWeapon?.abilityLoadout is LightningSpearLoadout)
            ClearAllMarks();
    }

    public IEnumerator ExecuteSkill1(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        CacheOwnerReferences(system);

        if (system == null || spec == null || loadout == null)
            yield break;

        Vector2 ownerPosition = system.transform.position;
        Vector2 cursorWorld = ResolveCursorWorld(system);
        LightningSpearMarkActor selectedMark = FindSelectableMark(loadout, data, ownerPosition, cursorWorld);

        if (selectedMark == null)
        {
            Vector2 direction = ResolveAimDirection(system);
            int facingSideSign = ResolveFacingSideSign(system, direction);
            TryPlayAnimationTrigger(system, spec.Definition, GetNoMarkSweepAnimationTrigger(data));
            yield return WaitForAnimationEventOrDelay(
                system,
                spec,
                GetNoMarkSweepHitEventTag(data),
                GetNoMarkSweepHitEventTimeout(data),
                GetNoMarkSweepFallbackHitDelay(data));
            SpawnHitbox(GetNoMarkSweepHit(loadout, data), system, spec, ownerPosition, direction, facingSideSign);
            yield break;
        }

        TryPlayAnimationTrigger(system, spec.Definition, GetMarkRushAnimationTrigger(data));
        yield return ExecuteMarkRush(system, spec, loadout, data, selectedMark);
    }

    public IEnumerator ExecuteSkill2(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        CacheOwnerReferences(system);

        if (system == null || spec == null || loadout == null || GetMarkPrefab(loadout, data) == null)
            yield break;

        TryPlayAnimationTrigger(system, spec.Definition, GetMarkRainAnimationTrigger(data));
        yield return WaitForAnimationEventOrDelay(
            system,
            spec,
            GetMarkRainSpawnEventTag(data),
            GetMarkRainSpawnEventTimeout(data),
            GetMarkRainFallbackSpawnDelay(data));

        Vector2 ownerPosition = system.transform.position;
        MonsterRoomArea2D room = FindRoomContaining(ownerPosition);
        List<Vector2> positions = GenerateMarkPositions(loadout, data, ownerPosition, room);

        for (int i = 0; i < positions.Count; i++)
            SpawnMark(loadout, data, positions[i], room, system, spec);
    }

    public void RegisterMark(LightningSpearMarkActor mark)
    {
        if (mark == null || activeMarks.Contains(mark))
            return;

        activeMarks.Add(mark);
    }

    public void UnregisterMark(LightningSpearMarkActor mark)
    {
        if (mark == null)
            return;

        activeMarks.Remove(mark);
    }

    public void HandleMarkActivated(
        LightningSpearMarkActor mark,
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout)
    {
        if (mark == null || system == null || spec == null || loadout == null)
            return;

        LightningSpearSkill2Data data = ResolveSkill2Data(loadout);
        SpawnHitbox(GetLandingHit(loadout, data), system, spec, mark.transform.position, ResolveAimDirection(system));
    }

    private IEnumerator ExecuteMarkRush(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark)
    {
        Vector2 start = system.transform.position;
        Vector2 destination = mark.transform.position;
        Vector2 delta = destination - start;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.0001f ? delta / distance : ResolveAimDirection(system);
        int facingSideSign = ResolveFacingSideSign(system, direction);

        try
        {
            mark.Consume();
            SpawnMarkRushTrail(loadout, data, start, destination);
            MoveOwnerToMark(system, destination);

            float hitDelay = GetMarkRushArrivalHitDelay(loadout, data);
            if (hitDelay > 0f)
                yield return new WaitForSeconds(hitDelay);

            SpawnHitbox(GetMarkRushHit(loadout, data), system, spec, destination, direction, facingSideSign);
            RefreshMarkFeedback(loadout, data);
        }
        finally
        {
            QueueSkill1CooldownReset(system, spec, loadout, data);
        }
    }

    private void QueueSkill1CooldownReset(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        ResetSkill1Cooldown(system, spec, loadout, data);

        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            StartCoroutine(ResetSkill1CooldownNextFrame(system, spec, loadout, data));
    }

    private IEnumerator ResetSkill1CooldownNextFrame(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        yield return null;

        ResetSkill1Cooldown(system, spec, loadout, data);
    }

    private static void ResetSkill1Cooldown(
        AbilitySystem system,
        AbilitySpec spec,
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (system == null || loadout == null)
            return;

        AbilityDefinition ability = loadout.MarkRushOrSweep != null
            ? loadout.MarkRushOrSweep
            : spec?.Definition;

        system.TrySetCooldownRemaining(ability, 0f);
        system.SetNextActivationDelay(spec, GetMarkRushInternalDelay(loadout, data));
    }

    private void MoveOwnerToMark(AbilitySystem system, Vector2 destination)
    {
        if (movementMotor != null)
        {
            movementMotor.WarpTo(destination);
            return;
        }

        if (system == null)
            return;

        system.transform.position = new Vector3(destination.x, destination.y, system.transform.position.z);
    }

    private static void SpawnMarkRushTrail(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        Vector2 start,
        Vector2 destination)
    {
        LightningSpearDashStabTrailEffect prefab = GetMarkRushTrailEffectPrefab(loadout, data);
        if (prefab == null)
            return;

        LightningSpearDashStabTrailEffect effect =
            UnityEngine.Object.Instantiate(prefab);
        if (effect == null)
            return;

        effect.Play(start, destination);
    }

    private void SpawnMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 position,
        MonsterRoomArea2D room,
        AbilitySystem system,
        AbilitySpec spec)
    {
        LightningSpearMarkActor markPrefab = GetMarkPrefab(loadout, data);
        LightningSpearMarkActor mark = Instantiate(markPrefab, position, Quaternion.identity);
        if (mark == null)
            return;

        RegisterMark(mark);
        mark.Initialize(
            this,
            room,
            GetMarkLifetimeSeconds(loadout, data),
            GetMarkRainDelay(loadout, data),
            system,
            spec,
            loadout);
    }

    private List<Vector2> GenerateMarkPositions(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition,
        MonsterRoomArea2D room)
    {
        pendingPositions.Clear();

        int targetCount = GetMarkRainCount(loadout, data);
        if (targetCount <= 0)
            return pendingPositions;

        int sampleCount = Mathf.Max(GetCandidateSamples(loadout, data), targetCount * 12);
        for (int i = 0; i < sampleCount && pendingPositions.Count < targetCount; i++)
        {
            Vector2 candidate = room != null
                ? SampleRoomCandidate(room)
                : SampleFallbackCandidate(ownerPosition, GetFallbackCombatRadius(loadout, data));

            if (!ValidateMarkCandidate(loadout, data, ownerPosition, room, candidate))
                continue;

            pendingPositions.Add(candidate);
        }

        return pendingPositions;
    }

    private Vector2 SampleRoomCandidate(MonsterRoomArea2D room)
    {
        Collider2D areaCollider = room != null ? room.AreaCollider : null;
        if (areaCollider == null)
            return transform.position;

        Bounds bounds = areaCollider.bounds;
        return new Vector2(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y));
    }

    private static Vector2 SampleFallbackCandidate(Vector2 origin, float radius)
    {
        return origin + Random.insideUnitCircle * Mathf.Max(0.01f, radius);
    }

    private bool ValidateMarkCandidate(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition,
        MonsterRoomArea2D room,
        Vector2 candidate)
    {
        if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y))
            return false;

        if (room != null)
        {
            if (!room.Contains(candidate))
                return false;
        }
        else
        {
            if (Vector2.Distance(ownerPosition, candidate) > GetFallbackCombatRadius(loadout, data))
                return false;

            if (HasBlocker(ownerPosition, candidate, loadout.MarkRushBodyRadius, loadout.StrictRushBlockMask))
                return false;
        }

        if (Vector2.Distance(ownerPosition, candidate) < GetMinPlayerDistance(loadout, data))
            return false;

        if (!IsLandingValid(loadout, data, candidate))
            return false;

        if (!HasRequiredSpacing(loadout, data, candidate))
            return false;

        return true;
    }

    private bool HasRequiredSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 candidate)
    {
        float minSpacing = GetMinMarkSpacing(loadout, data);
        if (minSpacing <= 0f)
            return true;

        float minSpacingSqr = minSpacing * minSpacing;
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (((Vector2)mark.transform.position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        for (int i = 0; i < pendingPositions.Count; i++)
        {
            if ((pendingPositions[i] - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        return true;
    }

    private static void TryPlayAnimationTrigger(AbilitySystem system, AbilityDefinition definition, string trigger)
    {
        if (system == null || definition == null || string.IsNullOrWhiteSpace(trigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(trigger), definition);
    }

    private static IEnumerator WaitForAnimationEventOrDelay(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayTag eventTag,
        float eventTimeout,
        float fallbackDelay)
    {
        if (system == null || spec == null)
            yield break;

        if (eventTag != null)
        {
            yield return AbilityTasks.WaitGameplayEvent(
                system,
                spec,
                eventTag,
                onReceived: null,
                timeout: eventTimeout,
                predicate: data => data.Spec == spec);
            yield break;
        }

        if (fallbackDelay > 0f)
            yield return AbilityTasks.WaitDelay(system, spec, fallbackDelay);
    }

    private static LightningSpearSkill1Data ResolveSkill1Data(LightningSpearLoadout loadout)
    {
        return loadout != null && loadout.MarkRushOrSweep != null
            ? loadout.MarkRushOrSweep.sourceObject as LightningSpearSkill1Data
            : null;
    }

    private static LightningSpearSkill2Data ResolveSkill2Data(LightningSpearLoadout loadout)
    {
        return loadout != null && loadout.MarkRain != null
            ? loadout.MarkRain.sourceObject as LightningSpearSkill2Data
            : null;
    }

    private static string GetMarkRushAnimationTrigger(LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushAnimationTrigger : null;
    }

    private static string GetNoMarkSweepAnimationTrigger(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepAnimationTrigger : null;
    }

    private static GameplayTag GetNoMarkSweepHitEventTag(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepHitEventTag : null;
    }

    private static float GetNoMarkSweepHitEventTimeout(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepHitEventTimeout : 0f;
    }

    private static float GetNoMarkSweepFallbackHitDelay(LightningSpearSkill1Data data)
    {
        return data != null ? data.NoMarkSweepFallbackHitDelay : 0f;
    }

    private static float GetCursorSelectRadius(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.CursorSelectRadius : loadout.CursorSelectRadius;
    }

    private static float GetMarkRushRange(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushRange : loadout.MarkRushRange;
    }

    private static float GetMarkRushBodyRadius(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushBodyRadius : loadout.MarkRushBodyRadius;
    }

    private static float GetMarkRushArrivalHitDelay(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushArrivalHitDelay : loadout.MarkRushArrivalHitDelay;
    }

    private static float GetMarkRushInternalDelay(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        return data != null ? data.MarkRushInternalDelay : loadout.MarkRushInternalDelay;
    }

    private static LightningSpearDashStabTrailEffect GetMarkRushTrailEffectPrefab(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.MarkRushTrailEffectPrefab != null)
            return data.MarkRushTrailEffectPrefab;

        return loadout != null ? loadout.MarkRushTrailEffectPrefab : null;
    }

    private static LightningSpearHitConfig GetMarkRushHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.MarkRushHit != null)
            return data.MarkRushHit;

        return loadout != null ? loadout.MarkRushHit : null;
    }

    private static LightningSpearHitConfig GetNoMarkSweepHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data)
    {
        if (data != null && data.NoMarkSweepHit != null)
            return data.NoMarkSweepHit;

        return loadout != null ? loadout.NoMarkSweepHit : null;
    }

    private static string GetMarkRainAnimationTrigger(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainAnimationTrigger : null;
    }

    private static GameplayTag GetMarkRainSpawnEventTag(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainSpawnEventTag : null;
    }

    private static float GetMarkRainSpawnEventTimeout(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainSpawnEventTimeout : 0f;
    }

    private static float GetMarkRainFallbackSpawnDelay(LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainFallbackSpawnDelay : 0f;
    }

    private static LightningSpearMarkActor GetMarkPrefab(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        if (data != null && data.MarkPrefab != null)
            return data.MarkPrefab;

        return loadout != null ? loadout.MarkPrefab : null;
    }

    private static float GetMarkLifetimeSeconds(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkLifetimeSeconds : loadout.MarkLifetimeSeconds;
    }

    private static int GetMarkRainCount(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainCount : loadout.MarkRainCount;
    }

    private static float GetMarkRainDelay(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MarkRainDelay : loadout.MarkRainDelay;
    }

    private static float GetFallbackCombatRadius(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.FallbackCombatRadius : loadout.FallbackCombatRadius;
    }

    private static float GetMinPlayerDistance(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MinPlayerDistance : loadout.MinPlayerDistance;
    }

    private static float GetMinMarkSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.MinMarkSpacing : loadout.MinMarkSpacing;
    }

    private static float GetLandingProbeRadius(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.LandingProbeRadius : loadout.LandingProbeRadius;
    }

    private static int GetCandidateSamples(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
    {
        return data != null ? data.CandidateSamples : loadout.CandidateSamples;
    }

    private static LightningSpearHitConfig GetLandingHit(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data)
    {
        if (data != null && data.LandingHit != null)
            return data.LandingHit;

        return loadout != null ? loadout.LandingHit : null;
    }

    private void SpawnHitbox(
        LightningSpearHitConfig hitConfig,
        AbilitySystem system,
        AbilitySpec spec,
        Vector2 origin,
        Vector2 direction,
        int facingSideSignOverride = 0)
    {
        if (hitConfig == null || !hitConfig.HasHitbox || system == null || spec == null)
            return;

        CombatHitPayload payload = hitConfig.BuildPayload(system, spec);
        if (payload == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : ResolveAimDirection(system);
        Vector2 center = origin + safeDirection * hitConfig.ForwardOffset;
        int visualSideSign = facingSideSignOverride != 0
            ? (facingSideSignOverride < 0 ? -1 : 1)
            : ResolveFacingSideSign(system, safeDirection);

        MeleeHitboxActor hitbox = Instantiate(hitConfig.HitboxPrefab, center, Quaternion.identity);
        if (hitbox == null)
            return;

        var context = new MeleeHitboxSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = hitConfig.ActiveTime,
            wallLayers = hitConfig.WallLayers,
            damageLayers = hitConfig.HitLayers,
            hitPayload = payload,
            worldPosition = center,
            hitboxSize = hitConfig.HitboxSize,
            hitOncePerTarget = true,
            destroyOnFirstHit = false,
            direction = safeDirection,
            flipVisualX = visualSideSign < 0
        };

        hitbox.Setup(context);
    }

    private void RefreshMarkFeedback(LightningSpearLoadout loadout, LightningSpearSkill1Data data)
    {
        if (loadout == null || ownerSystem == null)
        {
            ClearFeedback();
            return;
        }

        bool skillReady = IsSkill1Ready(loadout);
        Vector2 ownerPosition = ownerSystem.transform.position;
        Vector2 cursorWorld = ResolveCursorWorld(ownerSystem);
        LightningSpearMarkActor selected = skillReady
            ? FindSelectableMark(loadout, data, ownerPosition, cursorWorld)
            : null;
        bool hasActiveMark = false;

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (!mark.IsActive)
            {
                mark.SetFeedback(false, false);
                continue;
            }

            hasActiveMark = true;
            bool inRushRange = skillReady && IsMarkInsideRushRange(loadout, data, mark, ownerPosition);
            mark.SetFeedback(inRushRange, mark == selected);
        }

        UpdateRangeIndicator(loadout, data, skillReady && hasActiveMark);
        UpdateSelectedMarkIndicator(loadout, selected);
        UpdateCursorFeedback(selected != null);
    }

    private void ClearFeedback()
    {
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            mark.SetFeedback(false, false);
        }

        if (rushRangeIndicatorInstance != null)
            rushRangeIndicatorInstance.SetActive(false);

        if (selectedMarkIndicatorInstance != null)
            selectedMarkIndicatorInstance.SetActive(false);

        UpdateCursorFeedback(false);
    }

    private void UpdateRangeIndicator(LightningSpearLoadout loadout, LightningSpearSkill1Data data, bool active)
    {
        if (!active || loadout == null || ownerSystem == null || loadout.RushRangeIndicatorPrefab == null)
        {
            if (rushRangeIndicatorInstance != null)
                rushRangeIndicatorInstance.SetActive(false);
            return;
        }

        if (rushRangeIndicatorInstance == null)
            rushRangeIndicatorInstance = Instantiate(loadout.RushRangeIndicatorPrefab);

        rushRangeIndicatorInstance.transform.SetParent(null, true);
        rushRangeIndicatorInstance.transform.position = ownerSystem.transform.position;
        rushRangeIndicatorInstance.transform.rotation = Quaternion.identity;
        rushRangeIndicatorInstance.transform.localScale = Vector3.one;

        if (rushRangeIndicatorInstance.TryGetComponent(out LightningSpearRushRangeIndicator rangeIndicator))
            rangeIndicator.SetRadius(GetMarkRushRange(loadout, data));
        else
            rushRangeIndicatorInstance.transform.localScale = Vector3.one * (GetMarkRushRange(loadout, data) * 2f);

        rushRangeIndicatorInstance.SetActive(true);
    }

    private void UpdateSelectedMarkIndicator(LightningSpearLoadout loadout, LightningSpearMarkActor selected)
    {
        if (selected == null || loadout == null || loadout.SelectedMarkIndicatorPrefab == null)
        {
            if (selectedMarkIndicatorInstance != null)
                selectedMarkIndicatorInstance.SetActive(false);
            return;
        }

        if (selectedMarkIndicatorInstance == null)
            selectedMarkIndicatorInstance = Instantiate(loadout.SelectedMarkIndicatorPrefab);

        selectedMarkIndicatorInstance.transform.position = selected.transform.position;
        selectedMarkIndicatorInstance.SetActive(true);
    }

    private void UpdateCursorFeedback(bool active)
    {
        if (cursorInteractableSet == active)
            return;

        cursorInteractableSet = active;
        MouseCursorService.Instance?.SetInteractable(this, active);
    }

    private void DestroyFeedbackObjects()
    {
        if (rushRangeIndicatorInstance != null)
        {
            Destroy(rushRangeIndicatorInstance);
            rushRangeIndicatorInstance = null;
        }

        if (selectedMarkIndicatorInstance != null)
        {
            Destroy(selectedMarkIndicatorInstance);
            selectedMarkIndicatorInstance = null;
        }
    }

    private LightningSpearMarkActor FindSelectableMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        Vector2 ownerPosition,
        Vector2 cursorWorld)
    {
        LightningSpearMarkActor bestMark = null;
        float cursorSelectRadius = GetCursorSelectRadius(loadout, data);
        float selectRadiusSqr = cursorSelectRadius * cursorSelectRadius;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (!mark.IsActive)
                continue;

            Vector2 markPosition = mark.transform.position;
            float cursorDistanceSqr = (markPosition - cursorWorld).sqrMagnitude;
            if (cursorDistanceSqr > selectRadiusSqr)
                continue;

            if (!CanRushToMark(loadout, data, mark, ownerPosition))
                continue;

            if (cursorDistanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = cursorDistanceSqr;
            bestMark = mark;
        }

        return bestMark;
    }

    private static bool IsMarkInsideRushRange(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark,
        Vector2 ownerPosition)
    {
        if (loadout == null || mark == null || !mark.IsActive)
            return false;

        Vector2 offset = (Vector2)mark.transform.position - ownerPosition;
        float range = GetMarkRushRange(loadout, data);
        return offset.sqrMagnitude <= range * range;
    }

    private bool CanRushToMark(
        LightningSpearLoadout loadout,
        LightningSpearSkill1Data data,
        LightningSpearMarkActor mark,
        Vector2 ownerPosition)
    {
        if (loadout == null || mark == null || !mark.IsActive)
            return false;

        Vector2 markPosition = mark.transform.position;
        if (Vector2.Distance(ownerPosition, markPosition) > GetMarkRushRange(loadout, data))
            return false;

        if (!IsLandingValid(loadout, ResolveSkill2Data(loadout), markPosition))
            return false;

        MonsterRoomArea2D ownerRoom = FindRoomContaining(ownerPosition);
        MonsterRoomArea2D markRoom = ResolveMarkRoom(mark, markPosition);
        int blockerMask;

        if (ownerRoom != null)
        {
            if (markRoom != ownerRoom)
                return false;

            blockerMask = loadout.HardBlockMask;
        }
        else
        {
            blockerMask = loadout.StrictRushBlockMask;
        }

        return !HasBlocker(ownerPosition, markPosition, GetMarkRushBodyRadius(loadout, data), blockerMask);
    }

    private MonsterRoomArea2D ResolveMarkRoom(LightningSpearMarkActor mark, Vector2 markPosition)
    {
        if (mark != null && mark.RoomArea != null && mark.RoomArea.Contains(markPosition))
            return mark.RoomArea;

        return FindRoomContaining(markPosition);
    }

    private bool IsLandingValid(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 position)
    {
        float radius = GetLandingProbeRadius(loadout, data);

        if (loadout.RequiredGroundLayers.value != 0 &&
            Physics2D.OverlapCircle(position, radius, loadout.RequiredGroundLayers.value) == null)
        {
            return false;
        }

        int blockedMask = loadout.LandingBlockedMask;
        if (blockedMask != 0 &&
            Physics2D.OverlapCircle(position, radius, blockedMask) != null)
        {
            return false;
        }

        return true;
    }

    private static bool HasBlocker(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (radius > 0f)
            return Physics2D.CircleCast(start, radius, direction, distance, layerMask).collider != null;

        return Physics2D.Linecast(start, end, layerMask).collider != null;
    }

    private MonsterRoomArea2D FindRoomContaining(Vector2 position)
    {
        MonsterRoomArea2D[] rooms = FindRooms();
        for (int i = 0; i < rooms.Length; i++)
        {
            MonsterRoomArea2D room = rooms[i];
            if (room != null && room.Contains(position))
                return room;
        }

        return null;
    }

    private static MonsterRoomArea2D[] FindRooms()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<MonsterRoomArea2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<MonsterRoomArea2D>();
#endif
    }

    private bool IsSkill1Ready(LightningSpearLoadout loadout)
    {
        if (ownerSystem == null || loadout == null || loadout.MarkRushOrSweep == null)
            return false;

        return ownerSystem.GetCooldownRemaining(loadout.MarkRushOrSweep) <= 0f &&
               ownerSystem.GetNextActivationRemaining(loadout.MarkRushOrSweep) <= 0f;
    }

    private Vector2 ResolveCursorWorld(AbilitySystem system)
    {
        if (aimSource != null)
            return aimSource.MouseWorld;

        Camera camera = Camera.main;
        if (camera != null)
            return InputBindingService.EnsureInstance().GetPointerWorldPosition(camera, 0f);

        return system != null ? (Vector2)system.transform.position : Vector2.zero;
    }

    private Vector2 ResolveAimDirection(AbilitySystem system)
    {
        Vector2 direction = AbilityAimResolver2D.Resolve(
            system != null ? system.gameObject : gameObject,
            Vector2.right);

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private int ResolveFacingSideSign(AbilitySystem system, Vector2 fallbackDirection)
    {
        CacheOwnerReferences(system);

        if (presentationRig != null)
        {
            presentationRig.RefreshNow();
            return presentationRig.CurrentSideSign < 0 ? -1 : 1;
        }

        if (aimSource != null && system != null)
        {
            float deltaX = aimSource.MouseWorld.x - system.transform.position.x;
            if (deltaX < -0.0001f)
                return -1;

            if (deltaX > 0.0001f)
                return 1;
        }

        return fallbackDirection.x < -0.0001f ? -1 : 1;
    }

    private bool TryResolveActiveLoadout(out LightningSpearLoadout loadout)
    {
        CacheOwnerReferences(null);

        WeaponDefinition activeWeapon = weaponInventory != null ? weaponInventory.ActiveWeapon : null;
        loadout = activeWeapon != null ? activeWeapon.abilityLoadout as LightningSpearLoadout : null;
        return loadout != null && ownerSystem != null;
    }

    private void CacheOwnerReferences(AbilitySystem explicitSystem)
    {
        if (explicitSystem != null)
            ownerSystem = explicitSystem;

        if (ownerSystem == null)
            ownerSystem = GetComponentInParent<AbilitySystem>();

        if (weaponInventory == null && ownerSystem != null)
            weaponInventory = ownerSystem.GetComponent<WeaponInventory2D>();

        if (aimSource == null && ownerSystem != null)
            aimSource = ownerSystem.GetComponent<PlayerAim2D>();

        if (movementMotor == null && ownerSystem != null)
            movementMotor = ownerSystem.GetComponent<MovementMotor2D>();

        if (presentationRig == null)
        {
            presentationRig = GetComponentInParent<WeaponPresentationRig2D>();
            if (presentationRig == null && ownerSystem != null)
                presentationRig = ownerSystem.GetComponentInChildren<WeaponPresentationRig2D>(true);
        }
    }

    private void ClearAllMarks()
    {
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark != null)
                Destroy(mark.gameObject);
        }

        activeMarks.Clear();
    }
}
