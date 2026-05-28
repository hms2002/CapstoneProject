using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class TreasureChest : MonoBehaviour
{
    private static readonly Color RewardRevealDustGizmoColor = new Color(1f, 0.82f, 0.12f, 0.95f);
    private const float RewardRevealDustGizmoRadius = 0.08f;

    [Header("Inventory")]
    [SerializeField] private int capacity = 16;

    [Header("Loot Override")]
    [SerializeField] private ChestLootMode lootMode = ChestLootMode.StageTable;
    [SerializeField] private ChestLootOverrideProfile lootOverrideProfile = new();

    [Header("Open Presentation")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private string openedStateName = "Opened";
    [SerializeField] private float openPreludeFallbackDuration = 0.5f;
    [SerializeField] private bool freezeTimeOnFirstOpen = true;
    [SerializeField] private ParticleSystem[] openEffects;
    [SerializeField] private SpriteRenderer chestSpriteRenderer;
    [SerializeField] private Sprite openedSprite;

    [Header("Reward Reveal")]
    [SerializeField] private ParticleSystem rewardRevealDustParticle;
    [SerializeField] private Transform rewardRevealDustAnchor;
    [SerializeField] private Vector3 rewardRevealDustLocalOffset = new Vector3(0f, -0.35f, 0f);
    [SerializeField] private bool clearRewardRevealDustBeforePlay = true;
    [SerializeField, Min(0f)] private float spawnedRewardRevealDustDestroyDelay = 2f;

    [Header("Interaction Presentation")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private WorldObjectPresentationDefinition openPresentation = new();

    private ChestInventory inventory;
    private bool isOpened;
    private bool isGenerated;
    private bool isOpening;
    private bool isPreludeTimeFrozen;
    private GameFlowInputBlocker openingInputBlocker;
    private float preludePreviousTimeScale = 1f;
    private WorldObjectPresentationRuntime openPresentationRuntime;
    private readonly List<ChestLootSnapshot> refreshGuard = new List<ChestLootSnapshot>();
    private readonly List<ParticleSystem> spawnedRewardRevealParticles = new List<ParticleSystem>();
    private int refreshCountUsed;
    private bool hasRaisedFirstOpenedUi;
    public int Capacity => capacity;
    public bool IsOpened => isOpened;
    public int RefreshCountLimit => ChestRewardPolicy.ResolveRefreshLimit();
    public int RefreshCountUsed => refreshCountUsed;
    public int RemainingRefreshCount => ChestRewardPolicy.ResolveRemainingRefreshCount(
        isGenerated,
        inventory,
        LootManager.Instance != null,
        refreshCountUsed,
        refreshGuard);
    public event Action<TreasureChest> OpenedUi;
    public event Action<TreasureChest> FirstOpenedUi;

    private void Awake()
    {
        inventory = new ChestInventory(capacity);

        if (chestAnimator == null)
            chestAnimator = GetComponentInChildren<Animator>();

        if (chestAnimator != null)
            chestAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        if (chestSpriteRenderer == null)
            chestSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        openPresentationRuntime = new WorldObjectPresentationRuntime(gameObject);
        ConfigureOpenEffects();
    }

    private void OnDisable()
    {
        ReleaseOpeningUiInputBlockIfNeeded();
        RestorePreludeTimeIfNeeded();
        DestroySpawnedRewardRevealParticles(immediate: true);
        isOpening = false;
    }

    public void InitializeWithLoot(List<ScriptableObject> loots)
    {
        if (inventory == null)
            inventory = new ChestInventory(capacity);

        inventory.Clear();
        FillInventoryWithLoot(loots);
        RecordRefreshGuard();

        isGenerated = true;
    }

    public void PlayRewardReveal()
    {
        PlayRewardRevealDust();
    }

    public bool Open(IPlayerInteractor player = null)
    {
        if (!isGenerated)
        {
            GenerateSelfLoot();
            isGenerated = true;
        }

        if (isOpening)
            return true;

        if (isOpened)
        {
            bool openedUi = TryOpenUi();
            if (openedUi)
                RaiseOpenedUiEvents();

            return openedUi;
        }

        StartCoroutine(OpenRoutine(player != null ? player.Transform.gameObject : null));
        return true;
    }

    private IEnumerator OpenRoutine(GameObject instigator)
    {
        bool opened = false;
        isOpening = true;
        try
        {
            AcquireOpeningUiInputBlockIfNeeded();
            FreezePreludeTimeIfNeeded();
            PlayOpenPresentation(instigator);

            float duration = GetOpenPreludeDuration();
            if (duration > 0f)
                yield return new WaitForSecondsRealtime(duration);

            isOpened = true;
            HoldOpenedVisualState();
            RestorePreludeTimeIfNeeded();

            opened = TryOpenUi(playSlideFadePresentation: false, inputBlocker: openingInputBlocker);
            if (opened)
                RaiseOpenedUiEvents();
        }
        finally
        {
            ReleaseOpeningUiInputBlockIfNeeded();
            RestorePreludeTimeIfNeeded();
            isOpening = false;
        }

        if (!opened && PlayerInteractor2D.Instance != null)
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);
    }

    private void RaiseOpenedUiEvents()
    {
        OpenedUi?.Invoke(this);

        if (hasRaisedFirstOpenedUi)
            return;

        hasRaisedFirstOpenedUi = true;
        FirstOpenedUi?.Invoke(this);
    }

    private bool TryOpenUi(
        bool playSlideFadePresentation = true,
        GameFlowInputBlocker inputBlocker = null)
    {
        if (ChestUIManager.Instance == null)
            return false;

        return ChestUIManager.Instance.OpenChest(this, playSlideFadePresentation, inputBlocker);
    }

    private void GenerateSelfLoot()
    {
        if (LootManager.Instance == null)
            return;

        ChestLootResult result = LootManager.Instance.GenerateChestLootResult(BuildLootRequest());
        FillInventoryWithLoot(result.Items);
        RecordRefreshGuard();
    }

    public ChestInventory GetInventory() => inventory;

    public bool CanRefreshLoot()
    {
        return ChestRewardPolicy.CanRefreshLoot(
            isGenerated,
            inventory,
            LootManager.Instance != null,
            refreshCountUsed,
            refreshGuard);
    }

    public bool TryRefreshLoot()
    {
        if (!CanRefreshLoot())
            return false;

        LootManager lootManager = LootManager.Instance;
        if (lootManager == null)
            return false;

        ChestLootResult result = lootManager.GenerateChestLootResult(BuildLootRequest());

        inventory.Clear();
        FillInventoryWithLoot(result.Items);
        refreshCountUsed++;
        RecordRefreshGuard();
        return true;
    }

    private void FillInventoryWithLoot(IReadOnlyList<ScriptableObject> loots)
    {
        if (inventory == null || loots == null)
            return;

        for (int i = 0; i < loots.Count; i++)
        {
            ScriptableObject item = loots[i];
            if (item != null)
                TryAddLootItem(item);
        }
    }

    private bool TryAddLootItem(ScriptableObject item)
    {
        if (item is RelicDefinition relic)
            return inventory.TryAddRelicWithLevel(relic, ChestRewardPolicy.ResolveChestRelicLevel(relic));

        return inventory.TryAdd(item);
    }

    private ChestLootRequest BuildLootRequest()
    {
        if (lootMode != ChestLootMode.OverrideProfile || lootOverrideProfile == null)
            return ChestLootRequest.Default;

        return new ChestLootRequest(default, LootPoolContext.PlayerInventory, lootOverrideProfile);
    }

    private void RecordRefreshGuard()
    {
        ChestRewardPolicy.RecordRefreshGuard(inventory, refreshGuard);
    }

    private void PlayOpenPresentation(GameObject instigator)
    {
        SetAnimatorSpeed(1f);

        if (!string.IsNullOrWhiteSpace(openStateName))
            PlayAnimatorState(openStateName, 0f);

        openPresentationRuntime?.PlayExecuteOnly(
            openPresentation,
            instigator: instigator,
            target: gameObject,
            anchor: ResolvePresentationAnchor(),
            sourceObject: this);

        if (openEffects == null)
            return;

        for (int i = 0; i < openEffects.Length; i++)
        {
            var effect = openEffects[i];
            if (effect == null)
                continue;

            effect.gameObject.SetActive(true);
            effect.Play(true);
        }
    }

    private void PlayRewardRevealDust()
    {
        ParticleSystem particle = ResolvePlayableRewardRevealDust();
        if (particle == null)
            return;

        ParticleSystem.MainModule main = particle.main;
        main.useUnscaledTime = true;
        particle.gameObject.SetActive(true);
        if (clearRewardRevealDustBeforePlay)
            particle.Clear(withChildren: true);
        particle.Play(withChildren: true);
    }

    private ParticleSystem ResolvePlayableRewardRevealDust()
    {
        if (rewardRevealDustParticle == null)
            return null;

        if (rewardRevealDustParticle.gameObject.scene.IsValid() &&
            rewardRevealDustParticle.gameObject.scene.isLoaded)
        {
            return rewardRevealDustParticle;
        }

        ParticleSystem instance = Instantiate(
            rewardRevealDustParticle,
            ResolveRewardRevealDustSpawnPosition(),
            ResolveRewardRevealDustSpawnRotation());
        spawnedRewardRevealParticles.Add(instance);
        Destroy(instance.gameObject, ResolveRewardRevealDustDestroyDelay(instance));
        return instance;
    }

    private Vector3 ResolveRewardRevealDustSpawnPosition()
    {
        Transform anchor = ResolveRewardRevealDustAnchor();
        return anchor.TransformPoint(rewardRevealDustLocalOffset);
    }

    private Quaternion ResolveRewardRevealDustSpawnRotation()
    {
        return ResolveRewardRevealDustAnchor().rotation;
    }

    private Transform ResolveRewardRevealDustAnchor()
    {
        return rewardRevealDustAnchor != null ? rewardRevealDustAnchor : transform;
    }

    private float ResolveRewardRevealDustDestroyDelay(ParticleSystem particle)
    {
        if (spawnedRewardRevealDustDestroyDelay > 0f)
            return spawnedRewardRevealDustDestroyDelay;

        if (particle == null)
            return 0f;

        ParticleSystem.MainModule main = particle.main;
        return Mathf.Max(0.1f, main.duration + main.startLifetime.constantMax);
    }

    private void DestroySpawnedRewardRevealParticles(bool immediate)
    {
        for (int i = 0; i < spawnedRewardRevealParticles.Count; i++)
        {
            ParticleSystem particle = spawnedRewardRevealParticles[i];
            if (particle == null)
                continue;

            if (immediate && !Application.isPlaying)
                DestroyImmediate(particle.gameObject);
            else
                Destroy(particle.gameObject);
        }

        spawnedRewardRevealParticles.Clear();
    }

    private void HoldOpenedVisualState()
    {
        bool heldWithAnimator = false;

        if (chestAnimator != null)
        {
            SetAnimatorSpeed(1f);

            if (!string.IsNullOrWhiteSpace(openedStateName) && PlayAnimatorState(openedStateName, 0f))
            {
                heldWithAnimator = true;
            }
            else if (!string.IsNullOrWhiteSpace(openStateName) && PlayAnimatorState(openStateName, 1f))
            {
                chestAnimator.Update(0f);
                SetAnimatorSpeed(0f);
                heldWithAnimator = true;
            }
        }

        if (!heldWithAnimator)
            ApplyOpenedSpriteFallback();
    }

    private bool PlayAnimatorState(string stateName, float normalizedTime)
    {
        if (chestAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        if (!chestAnimator.HasState(0, stateHash))
            return false;

        chestAnimator.Play(stateHash, 0, normalizedTime);
        chestAnimator.Update(0f);
        return true;
    }

    private float GetOpenPreludeDuration()
    {
        AnimationClip clip = ResolveAnimatorClip(openStateName);
        if (clip != null)
            return clip.length;

        return HasOpenEffects() ? Mathf.Max(0f, openPreludeFallbackDuration) : 0f;
    }

    private AnimationClip ResolveAnimatorClip(string stateOrClipName)
    {
        if (chestAnimator == null || string.IsNullOrWhiteSpace(stateOrClipName))
            return null;

        RuntimeAnimatorController controller = chestAnimator.runtimeAnimatorController;
        if (controller == null || controller.animationClips == null)
            return null;

        AnimationClip bestMatch = null;
        for (int i = 0; i < controller.animationClips.Length; i++)
        {
            AnimationClip clip = controller.animationClips[i];
            if (clip == null)
                continue;

            if (string.Equals(clip.name, stateOrClipName, System.StringComparison.OrdinalIgnoreCase))
                return clip;

            if (bestMatch == null &&
                clip.name.IndexOf(stateOrClipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bestMatch = clip;
            }
        }

        return bestMatch;
    }

    private void FreezePreludeTimeIfNeeded()
    {
        if (!freezeTimeOnFirstOpen || isPreludeTimeFrozen)
            return;

        preludePreviousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPreludeTimeFrozen = true;
    }

    private void RestorePreludeTimeIfNeeded()
    {
        if (!isPreludeTimeFrozen)
            return;

        Time.timeScale = preludePreviousTimeScale;
        preludePreviousTimeScale = 1f;
        isPreludeTimeFrozen = false;
    }

    private void AcquireOpeningUiInputBlockIfNeeded()
    {
        if (openingInputBlocker != null && openingInputBlocker.IsBlocking)
            return;

        openingInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        openingInputBlocker?.Acquire();
    }

    private void ReleaseOpeningUiInputBlockIfNeeded()
    {
        openingInputBlocker?.Release();
    }

    private void ConfigureOpenEffects()
    {
        if (openEffects == null)
            return;

        for (int i = 0; i < openEffects.Length; i++)
        {
            ParticleSystem effect = openEffects[i];
            if (effect == null)
                continue;

            var main = effect.main;
            main.useUnscaledTime = true;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private bool HasOpenEffects()
    {
        if (openEffects == null)
            return false;

        for (int i = 0; i < openEffects.Length; i++)
        {
            if (openEffects[i] != null)
                return true;
        }

        return false;
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (chestAnimator != null)
            chestAnimator.speed = speed;
    }

    private void ApplyOpenedSpriteFallback()
    {
        if (chestSpriteRenderer == null || openedSprite == null)
            return;

        chestSpriteRenderer.sprite = openedSprite;
    }

    private Transform ResolvePresentationAnchor()
    {
        if (presentationAnchor != null)
            return presentationAnchor;

        if (chestSpriteRenderer != null)
            return chestSpriteRenderer.transform;

        return transform;
    }

    private void OnDrawGizmos()
    {
        DrawRewardRevealDustGizmo(drawUnconfigured: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawRewardRevealDustGizmo(drawUnconfigured: true);
    }

    private void DrawRewardRevealDustGizmo(bool drawUnconfigured)
    {
        if (!drawUnconfigured &&
            rewardRevealDustParticle == null &&
            rewardRevealDustAnchor == null &&
            rewardRevealDustLocalOffset == Vector3.zero)
        {
            return;
        }

        Transform anchor = rewardRevealDustAnchor != null ? rewardRevealDustAnchor : transform;
        Vector3 spawnPosition = anchor.TransformPoint(rewardRevealDustLocalOffset);
        Gizmos.color = RewardRevealDustGizmoColor;
        Gizmos.DrawLine(anchor.position, spawnPosition);
        Gizmos.DrawSphere(spawnPosition, RewardRevealDustGizmoRadius);
    }

}
