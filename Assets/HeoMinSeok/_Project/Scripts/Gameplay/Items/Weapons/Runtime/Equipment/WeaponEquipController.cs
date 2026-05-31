using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Owns weapon prefab instancing, caching, animator relay binding, and runtime
/// state lookup. Aim-side positioning is owned by WeaponPresentationRig2D.
/// </summary>
public class WeaponEquipController : MonoBehaviour, IWeaponRuntimeStateProvider
{
    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponPresentationRig2D presentationRig;

    [Header("Fallback")]
    [SerializeField] private Transform weaponSocket;

    [Header("Cache")]
    [SerializeField] private bool useCache = true;
    [SerializeField] private int cacheLimit = 2;

    private GameObject currentPrefab;
    private GameObject currentWeaponGO;
    private WeaponVisualRig2D currentVisualRig;
    private WeaponAbilityRuntimeState currentRuntimeState;
    private int currentAttackSideSign = 1;

    private readonly Dictionary<GameObject, GameObject> cache = new();
    private readonly LinkedList<GameObject> lru = new();
    private readonly Dictionary<GameObject, LinkedListNode<GameObject>> lruNodes = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        TryApplyCurrentFacingMirror();
    }

    public void Equip(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return;

        ResolveReferences();
        presentationRig?.ResetAimPresentationOverrideForWeaponChange();
        abilitySystem?.OnWeaponEquipped();

        if (currentPrefab == weaponPrefab && currentWeaponGO != null)
        {
            ActivateInstance(currentWeaponGO, weaponPrefab);
            CacheCurrentWeaponComponents();
            RegisterAnimatorAndRelays(currentWeaponGO);
            ApplyCurrentVisualRig();
            return;
        }

        DeactivateCurrent();

        currentPrefab = weaponPrefab;
        currentWeaponGO = GetOrCreateInstance(weaponPrefab);

        ActivateInstance(currentWeaponGO, weaponPrefab);
        CacheCurrentWeaponComponents();
        RegisterAnimatorAndRelays(currentWeaponGO);
        ApplyCurrentVisualRig();
    }

    public void Clear()
    {
        ResolveReferences();
        presentationRig?.ResetAimPresentationOverrideForWeaponChange();
        abilitySystem?.OnWeaponEquipped();

        DeactivateCurrent();
        currentPrefab = null;
        currentWeaponGO = null;
        currentRuntimeState = null;
        currentVisualRig = null;
        currentAttackSideSign = 1;

        if (abilitySystem != null)
            abilitySystem.RegisterWeaponAnimator(null);
    }

    public void SetAttackVisualSideSign(int sideSign)
    {
        currentAttackSideSign = sideSign == 0 ? 1 : sideSign;
        ApplyCurrentVisualRig();
    }

    public WeaponAbilityRuntimeState GetCurrentWeaponRuntimeState()
    {
        if (currentRuntimeState == null && currentWeaponGO != null)
            currentRuntimeState = currentWeaponGO.GetComponentInChildren<WeaponAbilityRuntimeState>(true);

        return currentRuntimeState;
    }

    private void ResolveReferences()
    {
        if (abilitySystem == null)
            abilitySystem = GetComponentInParent<AbilitySystem>();

        if (presentationRig == null)
            presentationRig = GetComponentInChildren<WeaponPresentationRig2D>(true);

        if (weaponSocket == null)
            weaponSocket = transform;
    }

    private void DeactivateCurrent()
    {
        if (currentWeaponGO == null) return;

        if (!useCache)
            Destroy(currentWeaponGO);
        else
            currentWeaponGO.SetActive(false);

        currentWeaponGO = null;
        currentPrefab = null;
        currentVisualRig = null;
        currentRuntimeState = null;
        currentAttackSideSign = 1;

        if (abilitySystem != null)
            abilitySystem.RegisterWeaponAnimator(null);
    }

    private GameObject GetOrCreateInstance(GameObject prefab)
    {
        Transform mount = GetWeaponMount();

        if (!useCache)
            return Instantiate(prefab, mount);

        if (cache.TryGetValue(prefab, out var inst) && inst != null)
        {
            Touch(prefab);
            return inst;
        }

        inst = Instantiate(prefab, mount);
        inst.SetActive(false);

        cache[prefab] = inst;
        Touch(prefab);
        TrimCache();

        return inst;
    }

    private void ActivateInstance(GameObject instance, GameObject prefabKey)
    {
        if (instance == null) return;

        instance.transform.SetParent(GetWeaponMount(), false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        instance.SetActive(true);

        var anim = instance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        Touch(prefabKey);
    }

    private Transform GetWeaponMount()
    {
        ResolveReferences();
        if (presentationRig != null)
            return presentationRig.WeaponMount;

        return weaponSocket != null ? weaponSocket : transform;
    }

    private void CacheCurrentWeaponComponents()
    {
        currentVisualRig = currentWeaponGO != null
            ? currentWeaponGO.GetComponentInChildren<WeaponVisualRig2D>(true)
            : null;

        currentRuntimeState = currentWeaponGO != null
            ? currentWeaponGO.GetComponentInChildren<WeaponAbilityRuntimeState>(true)
            : null;
    }

    private void ApplyCurrentVisualRig()
    {
        if (currentWeaponGO == null)
            return;

        if (TryApplyCurrentFacingMirror())
            return;

        var legacyVisualSetup = currentWeaponGO.GetComponentInChildren<WeaponVisualSetup>(true);
        if (legacyVisualSetup != null)
            legacyVisualSetup.ApplyAttackSideSign(currentAttackSideSign);
    }

    private bool TryApplyCurrentFacingMirror()
    {
        if (currentWeaponGO == null)
            return false;

        if (currentVisualRig == null)
            currentVisualRig = currentWeaponGO.GetComponentInChildren<WeaponVisualRig2D>(true);

        if (currentVisualRig == null)
            return false;

        if (presentationRig == null)
            ResolveReferences();

        presentationRig?.RefreshNow();

        int facingSideSign = presentationRig != null ? presentationRig.CurrentSideSign : 1;
        currentVisualRig.SetFacingSideSign(facingSideSign);
        return true;
    }

    private void RegisterAnimatorAndRelays(GameObject weaponGO)
    {
        if (weaponGO == null || abilitySystem == null) return;

        var weaponAnim = weaponGO.GetComponentInChildren<Animator>();
        abilitySystem.RegisterWeaponAnimator(weaponAnim);

        var relays = weaponGO.GetComponentsInChildren<AbilityAnimationEventRelay>(true);
        foreach (var relay in relays)
            relay.Bind(abilitySystem);
    }

    private void Touch(GameObject prefab)
    {
        if (prefab == null) return;

        if (lruNodes.TryGetValue(prefab, out var node))
        {
            lru.Remove(node);
            lru.AddFirst(node);
            return;
        }

        var newNode = lru.AddFirst(prefab);
        lruNodes[prefab] = newNode;
    }

    private void TrimCache()
    {
        if (cacheLimit < 0) cacheLimit = 0;

        while (cache.Count > cacheLimit && lru.Last != null)
        {
            var key = lru.Last.Value;
            lru.RemoveLast();
            lruNodes.Remove(key);

            if (!cache.TryGetValue(key, out var inst))
                continue;

            cache.Remove(key);
            if (inst != null)
                Destroy(inst);
        }
    }
}
