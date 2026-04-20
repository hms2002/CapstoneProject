using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 장착 무기 프리팹의 생성/캐싱/활성화를 관리한다.
/// - 커서의 좌우 위치에 따라 무기 인스턴스를 좌/우 손 pivot 중 적절한 소켓 아래에 배치한다.
/// - 무기 회전 자체는 Hand/PlayerAim2D가 맡고, 이 컨트롤러는 배치 소켓 선택과 비주얼 수명,
///   그리고 WeaponVisualSetup을 통한 좌/우 손별 비주얼 포즈 적용을 담당한다.
/// </summary>
public class WeaponEquipController : MonoBehaviour, IWeaponRuntimeStateProvider
{
    private enum HandSide
    {
        Right,
        Left
    }

    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform leftWeaponSocket;
    [SerializeField] private Transform rightWeaponSocket;
    [SerializeField] private Transform ownerTransform;
    [SerializeField] private PlayerAim2D aimSource;

    [Header("Side Switch")]
    [Tooltip("커서가 플레이어 중심선을 이 값만큼 넘기기 전에는 현재 손을 유지한다.")]
    [SerializeField, Min(0f)] private float sideSwitchDeadZone = 0.1f;

    [Header("Cache")]
    [Tooltip("무기 교체 시 Instantiate/Destroy 대신 캐시(비활성/활성)로 처리")]
    [SerializeField] private bool useCache = true;

    [Tooltip("캐시 최대 개수. 무기 2슬롯이면 2 추천")]
    [SerializeField] private int cacheLimit = 2;

    private GameObject currentPrefab;
    private GameObject currentWeaponGO;
    private WeaponVisualSetup currentVisualSetup;
    private WeaponAbilityRuntimeState currentRuntimeState;
    private HandSide currentSide = HandSide.Right;
    private int currentAttackSideSign = 1;

    // prefab -> instance
    private readonly Dictionary<GameObject, GameObject> cache = new();

    // 간단 LRU (최근 사용 순서)
    private readonly LinkedList<GameObject> lru = new();
    private readonly Dictionary<GameObject, LinkedListNode<GameObject>> lruNodes = new();

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponentInParent<AbilitySystem>();
        if (aimSource == null) aimSource = GetComponentInParent<PlayerAim2D>();
        if (ownerTransform == null && abilitySystem != null) ownerTransform = abilitySystem.transform;
        if (weaponSocket == null) weaponSocket = transform;
        if (rightWeaponSocket == null) rightWeaponSocket = weaponSocket;
    }

    private void LateUpdate()
    {
        RefreshCurrentWeaponSocket();
    }

    /// <summary>무기 장착(교체 포함)</summary>
    public void Equip(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return;
        if (abilitySystem == null) abilitySystem = GetComponentInParent<AbilitySystem>();
        if (aimSource == null) aimSource = GetComponentInParent<PlayerAim2D>();
        if (ownerTransform == null && abilitySystem != null) ownerTransform = abilitySystem.transform;
        if (weaponSocket == null) weaponSocket = transform;
        if (rightWeaponSocket == null) rightWeaponSocket = weaponSocket;

        // 실행 중 무기 채널 정리
        abilitySystem.OnWeaponEquipped();

        // 같은 프리팹이면 재등록만
        if (currentPrefab == weaponPrefab && currentWeaponGO != null)
        {
            ActivateInstance(currentWeaponGO, weaponPrefab);
            RegisterAnimatorAndRelays(currentWeaponGO);
            ApplyCurrentVisualPose();
            return;
        }

        // 기존 무기 비주얼 정리(캐시라면 비활성, 아니면 Destroy)
        DeactivateCurrent();

        // 새 무기 인스턴스 얻기
        currentPrefab = weaponPrefab;
        currentWeaponGO = GetOrCreateInstance(weaponPrefab);
        currentVisualSetup = currentWeaponGO != null
            ? currentWeaponGO.GetComponentInChildren<WeaponVisualSetup>(true)
            : null;
        currentRuntimeState = currentWeaponGO != null
            ? currentWeaponGO.GetComponentInChildren<WeaponAbilityRuntimeState>(true)
            : null;

        ActivateInstance(currentWeaponGO, weaponPrefab);
        RegisterAnimatorAndRelays(currentWeaponGO);
        RefreshCurrentWeaponSocket();
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
        currentRuntimeState = null;
        currentAttackSideSign = 1;

        abilitySystem.RegisterWeaponAnimator(null);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 공격 단계가 요구하는 sideSign을 장착 무기 비주얼에 반영한다.
    /// - 손 소켓 포즈 적용과 별개로 공격 중 표현 계층의 좌우 반전을 유지한다.
    /// </summary>
    public void SetAttackVisualSideSign(int sideSign)
    {
        currentAttackSideSign = sideSign == 0 ? 1 : sideSign;
        ApplyCurrentVisualPose();
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
        currentVisualSetup = null;
        currentRuntimeState = null;
        currentSide = HandSide.Right;
        currentAttackSideSign = 1;

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

        instance.transform.SetParent(GetCurrentSocket(), false);
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

    /// <summary>
    /// 책임 : 현재 커서 위치를 기준으로 활성 무기 인스턴스의 부모 소켓을 좌/우 pivot 중 하나로 맞춘다.
    /// pivot이 비어 있으면 기존 단일 socket 배치 방식으로 자연스럽게 fallback 한다.
    /// </summary>
    private void RefreshCurrentWeaponSocket()
    {
        if (currentWeaponGO == null)
            return;

        var nextSide = ResolveHandSide();
        var nextSocket = GetSocket(nextSide);
        if (nextSocket == null)
            nextSocket = weaponSocket != null ? weaponSocket : transform;

        if (currentWeaponGO.transform.parent != nextSocket)
        {
            currentWeaponGO.transform.SetParent(nextSocket, false);
            currentWeaponGO.transform.localPosition = Vector3.zero;
            currentWeaponGO.transform.localRotation = Quaternion.identity;
            currentWeaponGO.transform.localScale = Vector3.one;
        }

        currentSide = nextSide;
        ApplyCurrentVisualPose();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 장착 무기 인스턴스의 WeaponVisualSetup을 찾아 좌/우 손별 비주얼 포즈를 적용한다.
    /// - 설정이 없으면 기존 authored 비주얼을 그대로 유지해 하위 호환을 보장한다.
    /// </summary>
    private void ApplyCurrentVisualPose()
    {
        if (currentWeaponGO == null)
            return;

        if (currentVisualSetup == null)
            currentVisualSetup = currentWeaponGO.GetComponentInChildren<WeaponVisualSetup>(true);

        if (currentVisualSetup == null)
            return;

        currentVisualSetup.ApplyPose(currentSide == HandSide.Left);
        currentVisualSetup.ApplyAttackSideSign(currentAttackSideSign);
    }

    /// <summary>
    /// 책임 : 커서의 월드 x 위치가 플레이어 기준 좌/우 어디인지 판정하고,
    /// 중심 근처에서는 dead zone으로 현재 손을 유지해 좌우 떨림을 줄인다.
    /// </summary>
    private HandSide ResolveHandSide()
    {
        if (aimSource == null || ownerTransform == null)
            return currentSide;

        float deltaX = aimSource.MouseWorld.x - ownerTransform.position.x;
        if (deltaX > sideSwitchDeadZone)
            return HandSide.Right;

        if (deltaX < -sideSwitchDeadZone)
            return HandSide.Left;

        return currentSide;
    }

    private Transform GetCurrentSocket()
    {
        return GetSocket(ResolveHandSide());
    }

    private Transform GetSocket(HandSide side)
    {
        if (side == HandSide.Left && leftWeaponSocket != null)
            return leftWeaponSocket;

        if (side == HandSide.Right && rightWeaponSocket != null)
            return rightWeaponSocket;

        return weaponSocket != null ? weaponSocket : transform;
    }


    private void RegisterAnimatorAndRelays(GameObject weaponGO)
    {
        if (weaponGO == null || abilitySystem == null) return;

        var weaponAnim = weaponGO.GetComponentInChildren<Animator>();
        abilitySystem.RegisterWeaponAnimator(weaponAnim);

        var relays = weaponGO.GetComponentsInChildren<AbilityAnimationEventRelay>(true);
        foreach (var r in relays) r.Bind(abilitySystem);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 활성 무기 인스턴스가 제공하는 WeaponAbilityRuntimeState를 선택 계층에 노출한다.
    /// - 상태 컴포넌트가 없는 무기는 null을 반환해 기본 슬롯 ability 선택으로 자연스럽게 fallback 하게 만든다.
    /// </summary>
    public WeaponAbilityRuntimeState GetCurrentWeaponRuntimeState()
    {
        if (currentRuntimeState == null && currentWeaponGO != null)
            currentRuntimeState = currentWeaponGO.GetComponentInChildren<WeaponAbilityRuntimeState>(true);

        return currentRuntimeState;
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
