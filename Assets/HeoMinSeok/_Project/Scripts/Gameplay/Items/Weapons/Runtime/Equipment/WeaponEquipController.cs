using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class WeaponEquipController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private Transform weaponSocket;

    [Header("Cache")]
    [Tooltip("무기 교체 시 Instantiate/Destroy 대신 캐시(비활성/활성)로 처리")]
    [SerializeField] private bool useCache = true;

    [Tooltip("캐시 최대 개수. 무기 2슬롯이면 2 추천")]
    [SerializeField] private int cacheLimit = 2;

    private GameObject currentPrefab;
    private GameObject currentWeaponGO;

    // prefab -> instance
    private readonly Dictionary<GameObject, GameObject> cache = new();

    // 간단 LRU (최근 사용 순서)
    private readonly LinkedList<GameObject> lru = new();
    private readonly Dictionary<GameObject, LinkedListNode<GameObject>> lruNodes = new();

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponentInParent<AbilitySystem>();
        if (weaponSocket == null) weaponSocket = transform;
    }

    /// <summary>무기 장착(교체 포함)</summary>
    public void Equip(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return;
        if (abilitySystem == null) abilitySystem = GetComponentInParent<AbilitySystem>();
        if (weaponSocket == null) weaponSocket = transform;

        // 실행 중 무기 채널 정리
        abilitySystem.OnWeaponEquipped();

        // 같은 프리팹이면 재등록만
        if (currentPrefab == weaponPrefab && currentWeaponGO != null)
        {
            ActivateInstance(currentWeaponGO, weaponPrefab);
            RegisterAnimatorAndRelays(currentWeaponGO);
            return;
        }

        // 기존 무기 비주얼 정리(캐시라면 비활성, 아니면 Destroy)
        DeactivateCurrent();

        // 새 무기 인스턴스 얻기
        currentPrefab = weaponPrefab;
        currentWeaponGO = GetOrCreateInstance(weaponPrefab);

        ActivateInstance(currentWeaponGO, weaponPrefab);
        RegisterAnimatorAndRelays(currentWeaponGO);
    }

    /// <summary>무기 없음 상태(비주얼 제거/숨김)</summary>
    public void Clear()
    {
        if (abilitySystem == null) abilitySystem = GetComponentInParent<AbilitySystem>();

        // ✅ 무기 채널 실행 중이면 취소
        abilitySystem.OnWeaponEquipped();

        DeactivateCurrent();
        currentPrefab = null;
        currentWeaponGO = null;

        abilitySystem.RegisterWeaponAnimator(null);
    }


    // -----------------------
    // Internals
    // -----------------------
    private void DeactivateCurrent()
    {
        if (currentWeaponGO == null) return;

        if (!useCache)
        {
            Destroy(currentWeaponGO);
        }
        else
        {
            currentWeaponGO.SetActive(false);
        }

        currentWeaponGO = null;
        currentPrefab = null;

        // 안전: 이전 무기 Animator 참조 해제
        if (abilitySystem != null) abilitySystem.RegisterWeaponAnimator(null);
    }

    private GameObject GetOrCreateInstance(GameObject prefab)
    {
        if (!useCache)
        {
            return Instantiate(prefab, weaponSocket);
        }

        if (cache.TryGetValue(prefab, out var inst) && inst != null)
        {
            Touch(prefab);
            return inst;
        }

        inst = Instantiate(prefab, weaponSocket);
        inst.SetActive(false);

        cache[prefab] = inst;
        Touch(prefab);
        TrimCache();

        return inst;
    }

    private void ActivateInstance(GameObject instance, GameObject prefabKey)
    {
        if (instance == null) return;

        instance.transform.SetParent(weaponSocket, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        instance.SetActive(true);

        // ✅ 캐시 재사용 시 애니 상태 리셋(필요시)
        var anim = instance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        Touch(prefabKey);
    }


    private void RegisterAnimatorAndRelays(GameObject weaponGO)
    {
        if (weaponGO == null || abilitySystem == null) return;

        var weaponAnim = weaponGO.GetComponentInChildren<Animator>();
        abilitySystem.RegisterWeaponAnimator(weaponAnim);

        var relays = weaponGO.GetComponentsInChildren<AbilityAnimationEventRelay>(true);
        foreach (var r in relays) r.Bind(abilitySystem);
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

            if (cache.TryGetValue(key, out var inst))
            {
                cache.Remove(key);
                if (inst != null) Destroy(inst);
            }
        }
    }
}
