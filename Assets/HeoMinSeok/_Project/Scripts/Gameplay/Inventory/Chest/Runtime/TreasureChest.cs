using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class TreasureChest : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private int capacity = 16;

    [Header("Open Presentation")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private string openedStateName = "Opened";
    [SerializeField] private float openPreludeFallbackDuration = 0.5f;
    [SerializeField] private bool freezeTimeOnFirstOpen = true;
    [SerializeField] private ParticleSystem[] openEffects;
    [SerializeField] private SpriteRenderer chestSpriteRenderer;
    [SerializeField] private Sprite openedSprite;

    [Header("Interaction Presentation")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private WorldObjectPresentationDefinition openPresentation = new();

    private ChestInventory inventory;
    private bool isOpened;
    private bool isGenerated;
    private bool isOpening;
    private bool isPreludeTimeFrozen;
    private float preludePreviousTimeScale = 1f;
    private WorldObjectPresentationRuntime openPresentationRuntime;
    public int Capacity => capacity;
    public bool IsOpened => isOpened;

    private void Awake()
    {
        inventory = new ChestInventory();

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
        RestorePreludeTimeIfNeeded();
    }

    public void InitializeWithLoot(List<ScriptableObject> loots)
    {
        if (inventory == null)
            inventory = new ChestInventory();

        foreach (var item in loots)
        {
            if (item != null)
                inventory.TryAdd(item);
        }

        isGenerated = true;
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
            return TryOpenUi();

        StartCoroutine(OpenRoutine(player != null ? player.Transform.gameObject : null));
        return true;
    }

    private IEnumerator OpenRoutine(GameObject instigator)
    {
        isOpening = true;
        FreezePreludeTimeIfNeeded();
        PlayOpenPresentation(instigator);

        float duration = GetOpenPreludeDuration();
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        isOpened = true;
        HoldOpenedVisualState();
        RestorePreludeTimeIfNeeded();

        bool opened = TryOpenUi(playSlideFadePresentation: false);
        if (!opened && PlayerInteractor2D.Instance != null)
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);

        isOpening = false;
    }

    private bool TryOpenUi(bool playSlideFadePresentation = true)
    {
        if (ChestUIManager.Instance == null)
            return false;

        return ChestUIManager.Instance.OpenChest(this, playSlideFadePresentation);
    }

    private void GenerateSelfLoot()
    {
        if (LootManager.Instance == null)
            return;

        List<ScriptableObject> loots = LootManager.Instance.GenerateChestLoot();
        if (loots == null)
            return;

        foreach (var item in loots)
        {
            if (item != null)
                inventory.TryAdd(item);
        }
    }

    public ChestInventory GetInventory() => inventory;

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
}
