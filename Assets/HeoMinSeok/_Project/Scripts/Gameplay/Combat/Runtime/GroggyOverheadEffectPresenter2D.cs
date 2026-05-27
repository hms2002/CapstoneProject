using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 대상 전투 객체의 그로기 태그를 감지해 머리 위 루프 이펙트를 표시하고 숨긴다.
/// - 대상 SpriteRenderer bounds를 기준으로 이펙트 위치와 크기를 자동 보정한다.
/// - FSM/ASC 로직을 모르고 그로기 상태의 시각 표현만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroggyOverheadEffectPresenter2D : MonoBehaviour
{
    private const string DefaultGroggyTagResourcePath = "Tags/State.Status.Groggy";
    private static readonly Dictionary<GameObject, Stack<GameObject>> PoolByPrefab = new();
    private static Transform poolRoot;

    [Header("Binding")]
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag groggyTag;
    [SerializeField] private Transform followAnchor;
    [SerializeField] private SpriteRenderer boundsSource;

    [Header("Effect")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private Transform effectParent;
    [SerializeField] private bool instantiateOnAwake = false;
    [SerializeField] private bool destroyInstanceWhenHidden = true;
    [SerializeField] private bool usePooling = true;
    [SerializeField, Min(0)] private int maxPooledInstancesPerPrefab = 16;

    [Header("Placement")]
    [SerializeField] private Vector3 worldOffset = new(0f, 0.18f, 0f);
    [SerializeField] private float fallbackHeight = 1.2f;
    [SerializeField] private bool followEveryFrame = true;

    [Header("Scale")]
    [SerializeField] private bool scaleByBoundsHeight = true;
    [SerializeField] private float heightToScaleRatio = 0.45f;
    [SerializeField] private float minScale = 0.55f;
    [SerializeField] private float maxScale = 1.8f;

    private GameObject effectInstance;
    private GameObject effectInstancePrefabKey;
    private bool isVisible;

    private void Awake()
    {
        ResolveReferences();

        if (instantiateOnAwake)
            EnsureEffectInstance();

        SetVisible(false);
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (effectInstance != null)
            ReleaseEffectInstance();
    }

    private void LateUpdate()
    {
        if (followEveryFrame && isVisible)
            ApplyEffectTransform();
    }

    /// <summary>
    /// 책임:
    /// - 외부 authoring/런타임 생성 코드가 프리팹과 기준 렌더러를 주입할 수 있게 한다.
    /// - 이미 생성된 인스턴스가 있으면 새 설정으로 즉시 위치와 크기를 재동기화한다.
    /// </summary>
    public void Configure(
        GameObject prefab,
        SpriteRenderer targetBoundsSource = null,
        Transform targetFollowAnchor = null,
        TagSystem targetTagSystem = null)
    {
        effectPrefab = prefab != null ? prefab : effectPrefab;
        boundsSource = targetBoundsSource != null ? targetBoundsSource : boundsSource;
        followAnchor = targetFollowAnchor != null ? targetFollowAnchor : followAnchor;
        tagSystem = targetTagSystem != null ? targetTagSystem : tagSystem;

        ResolveReferences();
        EnsureEffectInstance();
        RefreshVisibility();
    }

    private void ResolveReferences()
    {
        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();

        if (groggyTag == null)
            groggyTag = Resources.Load<GameplayTag>(DefaultGroggyTagResourcePath);

        if (followAnchor == null)
            followAnchor = transform;

        if (boundsSource == null)
            boundsSource = FindRepresentativeRenderer();

        if (effectParent == null)
            effectParent = transform;
    }

    private void Subscribe()
    {
        if (tagSystem == null)
            return;

        tagSystem.OnTagAdded += HandleTagAdded;
        tagSystem.OnTagRemoved += HandleTagRemoved;
    }

    private void Unsubscribe()
    {
        if (tagSystem == null)
            return;

        tagSystem.OnTagAdded -= HandleTagAdded;
        tagSystem.OnTagRemoved -= HandleTagRemoved;
    }

    private void HandleTagAdded(GameplayTag tag)
    {
        if (IsGroggyTag(tag))
            SetVisible(true);
    }

    private void HandleTagRemoved(GameplayTag tag)
    {
        if (IsGroggyTag(tag))
            SetVisible(false);
    }

    private void RefreshVisibility()
    {
        bool shouldShow = tagSystem != null && groggyTag != null && tagSystem.HasTag(groggyTag);
        SetVisible(shouldShow);
    }

    private bool IsGroggyTag(GameplayTag tag)
    {
        if (tag == null || groggyTag == null)
            return false;

        return tag == groggyTag || tag.CachedPath == groggyTag.CachedPath;
    }

    private void EnsureEffectInstance()
    {
        if (effectInstance != null || effectPrefab == null)
            return;

        Transform parent = effectParent != null ? effectParent : transform;
        effectInstance = usePooling ? RentEffectInstance(effectPrefab, parent) : Instantiate(effectPrefab, parent);
        effectInstancePrefabKey = effectPrefab;
        effectInstance.transform.SetParent(parent, worldPositionStays: false);
        effectInstance.transform.localPosition = Vector3.zero;
        effectInstance.transform.localRotation = Quaternion.identity;
        effectInstance.transform.localScale = Vector3.one;
        effectInstance.SetActive(false);
    }

    private void SetVisible(bool visible)
    {
        if (visible)
            EnsureEffectInstance();
        else if (effectInstance == null)
        {
            isVisible = false;
            return;
        }

        isVisible = visible && effectInstance != null;

        if (effectInstance == null)
            return;

        effectInstance.SetActive(isVisible);

        if (isVisible)
        {
            RestartEffectAnimation(effectInstance);
            ApplyEffectTransform();
            return;
        }

        if (destroyInstanceWhenHidden)
            ReleaseEffectInstance();
    }

    private void ReleaseEffectInstance()
    {
        if (effectInstance == null)
            return;

        GameObject instance = effectInstance;
        GameObject prefabKey = effectInstancePrefabKey != null ? effectInstancePrefabKey : effectPrefab;
        effectInstance = null;
        effectInstancePrefabKey = null;
        isVisible = false;

        if (!usePooling || prefabKey == null || maxPooledInstancesPerPrefab <= 0)
        {
            Destroy(instance);
            return;
        }

        ReturnEffectInstance(prefabKey, instance, maxPooledInstancesPerPrefab);
    }

    private void ApplyEffectTransform()
    {
        if (effectInstance == null)
            return;

        effectInstance.transform.position = ResolveWorldPosition();
        effectInstance.transform.localScale = Vector3.one * ResolveScale();
    }

    private static void RestartEffectAnimation(GameObject instance)
    {
        if (instance == null)
            return;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private Vector3 ResolveWorldPosition()
    {
        if (boundsSource != null && boundsSource.sprite != null)
        {
            Bounds bounds = boundsSource.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + worldOffset;
        }

        Transform anchor = followAnchor != null ? followAnchor : transform;
        return anchor.position + new Vector3(0f, fallbackHeight, 0f) + worldOffset;
    }

    private float ResolveScale()
    {
        if (!scaleByBoundsHeight)
            return Mathf.Clamp(heightToScaleRatio, minScale, maxScale);

        float height = fallbackHeight;
        if (boundsSource != null && boundsSource.sprite != null)
            height = Mathf.Max(0.01f, boundsSource.bounds.size.y);

        return Mathf.Clamp(height * heightToScaleRatio, minScale, maxScale);
    }

    private SpriteRenderer FindRepresentativeRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        SpriteRenderer best = null;
        float bestArea = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate.sprite == null)
                continue;

            if (effectInstance != null && candidate.transform.IsChildOf(effectInstance.transform))
                continue;

            Bounds bounds = candidate.bounds;
            float area = bounds.size.x * bounds.size.y;
            if (area <= bestArea)
                continue;

            bestArea = area;
            best = candidate;
        }

        return best;
    }

    private static GameObject RentEffectInstance(GameObject prefab, Transform parent)
    {
        if (prefab == null)
            return null;

        if (PoolByPrefab.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Pop();
                if (pooled == null)
                    continue;

                pooled.transform.SetParent(parent, worldPositionStays: false);
                return pooled;
            }
        }

        return Instantiate(prefab, parent);
    }

    private static void ReturnEffectInstance(GameObject prefab, GameObject instance, int maxRetained)
    {
        if (prefab == null || instance == null)
            return;

        if (!PoolByPrefab.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            PoolByPrefab[prefab] = pool;
        }

        if (pool.Count >= maxRetained)
        {
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), worldPositionStays: false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        pool.Push(instance);
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
            return poolRoot;

        GameObject rootObject = new("GroggyOverheadEffectPool");
        rootObject.SetActive(false);
        DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
        return poolRoot;
    }
}
