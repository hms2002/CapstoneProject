using System;
using UnityEngine;
using CapstoneAudio;

/// <summary>
/// 책임 :
/// - 플레이어의 1회용 아이템 4칸 인벤토리를 관리하고 획득, 제거, 사용 흐름을 제공한다.
/// - 장착형 아이템과 분리된 고정 슬롯 인벤토리로 동작한다.
/// </summary>
public class PlayerConsumableInventory : MonoBehaviour
{
    /// <summary>
    /// 책임 :
    /// - 1회용 아이템 사용 성공 시 재생할 공용 사운드 키를 한 곳에 고정한다.
    /// - 입력 처리 계층이 아닌 실제 사용 성공 계층에서만 사운드가 울리게 해 실패 시 무음 규칙을 보장한다.
    /// </summary>
    private static readonly SoundRef EatPotionSound = SoundRef.FromKey("item.consume.potion");

    /// <summary>
    /// 책임 :
    /// - 1회용 아이템 획득 시도가 어떤 결과로 끝났는지 도메인 수준에서 구분한다.
    /// - 상위 호출부가 인벤토리 가득 참 같은 실패 사유를 UI 경고로 정확히 연결할 수 있게 한다.
    /// </summary>
    public enum AcquireResult
    {
        Success = 0,
        InvalidDefinition,
        InventoryFull
    }

    public event Action OnChanged;

    [Header("Slots")]
    [SerializeField] private ConsumableDefinition[] slots = new ConsumableDefinition[4];

    [Header("Heal Presentation")]
    [SerializeField] private ParticleSystem healParticlePrefab;
    [SerializeField] private Vector3 healParticleLocalOffset = Vector3.zero;

    public int Capacity => slots != null ? slots.Length : 0;
    public int SlotCount => Capacity;

    public static PlayerConsumableInventory GetOrAdd(Transform owner)
    {
        if (owner == null)
            return null;

        var inventory = owner.GetComponent<PlayerConsumableInventory>();
        return inventory != null ? inventory : owner.gameObject.AddComponent<PlayerConsumableInventory>();
    }

    public ConsumableDefinition GetConsumableInSlot(int slotIndex)
        => IsValidSlot(slotIndex) ? slots[slotIndex] : null;

    /// <summary>
    /// 책임 :
    /// - 1회용 아이템 획득 시도의 상세 결과를 반환한다.
    /// - 기존 bool API를 유지하면서도 실패 사유를 상위 흐름에 전달할 수 있게 한다.
    /// </summary>
    public AcquireResult TryAcquireDetailed(ConsumableDefinition consumable)
    {
        if (consumable == null)
            return AcquireResult.InvalidDefinition;

        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex < 0)
            return AcquireResult.InventoryFull;

        slots[emptyIndex] = consumable;
        OnChanged?.Invoke();
        return AcquireResult.Success;
    }

    public bool TryAcquire(ConsumableDefinition consumable)
    {
        return TryAcquireDetailed(consumable) == AcquireResult.Success;
    }

    public int CountConsumable(ConsumableDefinition consumable)
    {
        if (consumable == null || slots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == consumable)
                count++;
        }

        return count;
    }

    public int EnsureMinimumConsumableCount(ConsumableDefinition consumable, int minimumCount)
    {
        if (consumable == null || minimumCount <= 0)
            return 0;

        int currentCount = CountConsumable(consumable);
        int addedCount = 0;

        while (currentCount < minimumCount)
        {
            if (!TryAcquire(consumable))
                break;

            currentCount++;
            addedCount++;
        }

        return addedCount;
    }

    public bool TryUseAt(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        ConsumableDefinition consumable = slots[slotIndex];
        if (consumable == null)
            return false;

        if (!consumable.TryUse(gameObject))
            return false;

        PlayerHealParticlePlayback.PlayAttached(healParticlePrefab, transform, healParticleLocalOffset);
        PlayConsumableUseSound();
        slots[slotIndex] = null;
        OnChanged?.Invoke();
        return true;
    }

    public bool CanPlaceConsumableInSlot(int slotIndex, ConsumableDefinition consumable)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        return consumable == null || consumable.Kind == InventoryItemKind.Consumable;
    }

    public bool TrySetConsumableSlot(int slotIndex, ConsumableDefinition newConsumable)
    {
        if (!CanPlaceConsumableInSlot(slotIndex, newConsumable))
            return false;

        if (slots[slotIndex] == newConsumable)
            return true;

        slots[slotIndex] = newConsumable;
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySwapConsumableSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b))
            return false;

        if (a == b)
            return true;

        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 플레이어의 consumable 슬롯 배치를 저장용 DTO로 캡처한다.
    /// - 씬 이동 직전 소지 중인 1회용 아이템 구성을 보존하는 공식 창구다.
    /// </summary>
    public ConsumableInventoryState CaptureInventoryState()
    {
        var state = new ConsumableInventoryState
        {
            slots = new ConsumableSlotState[Capacity]
        };

        for (int i = 0; i < Capacity; i++)
        {
            var consumable = slots[i];
            state.slots[i] = new ConsumableSlotState
            {
                consumableId = consumable != null ? consumable.consumableId : null
            };
        }

        return state;
    }

    /// <summary>
    /// 책임 :
    /// - 저장된 consumable 슬롯 배치를 effect 없이 현재 플레이어에 복원한다.
    /// - 사용/소모 상태만 다루며 추가 효과는 발생시키지 않는다.
    /// </summary>
    public void RestoreShellState(
        ConsumableInventoryState state,
        Func<string, ConsumableDefinition> consumableResolver)
    {
        for (int i = 0; i < Capacity; i++)
            slots[i] = null;

        if (state == null || state.slots == null || consumableResolver == null)
        {
            OnChanged?.Invoke();
            return;
        }

        int copyCount = Mathf.Min(Capacity, state.slots.Length);
        for (int i = 0; i < copyCount; i++)
        {
            var entry = state.slots[i];
            if (entry == null || string.IsNullOrEmpty(entry.consumableId))
                continue;

            slots[i] = consumableResolver(entry.consumableId);
        }

        OnChanged?.Invoke();
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
                return i;
        }

        return -1;
    }

    private bool IsValidSlot(int slotIndex)
        => slotIndex >= 0 && slotIndex < Capacity;

    /// <summary>
    /// 책임 :
    /// - 1회용 아이템 사용이 실제로 성공했을 때만 소비 사운드를 1회 재생한다.
    /// - 빈 슬롯/사용 실패 상황에서는 호출되지 않아 불필요한 오디오 피드백을 막는다.
    /// </summary>
    private void PlayConsumableUseSound()
    {
        SoundManager.EnsureInstance().Play(EatPotionSound, new SoundPlaybackContext
        {
            Instigator = gameObject,
            Causer = gameObject,
            Target = gameObject,
            Position = transform.position,
            SourceObject = this
        });
    }
}

internal static class PlayerHealParticlePlayback
{
    private const float MinimumDestroyDelay = 0.1f;

    public static void PlayAttached(ParticleSystem particlePrefab, Transform target, Vector3 localOffset)
    {
        if (particlePrefab == null || target == null)
            return;

        ParticleSystem particle = UnityEngine.Object.Instantiate(
            particlePrefab,
            target.position,
            target.rotation);
        if (particle == null)
            return;

        Transform particleTransform = particle.transform;
        particleTransform.SetParent(target, worldPositionStays: false);
        particleTransform.localPosition = localOffset;
        particleTransform.localRotation = Quaternion.identity;
        particleTransform.localScale = particlePrefab.transform.localScale;

        GameObject particleObject = particle.gameObject;
        particleObject.SetActive(true);

        ParticleSystem[] particleSystems = particleObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(withChildren: true);
        }

        UnityEngine.Object.Destroy(particleObject, ResolveLifetime(particleSystems));
    }

    private static float ResolveLifetime(ParticleSystem[] particleSystems)
    {
        float lifetime = MinimumDestroyDelay;
        if (particleSystems == null)
            return lifetime;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.MainModule main = system.main;
            float systemLifetime = main.duration + ResolveCurveMax(main.startLifetime);
            lifetime = Mathf.Max(lifetime, systemLifetime);
        }

        return lifetime;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                return curve.curveMultiplier;
            default:
                return curve.constantMax;
        }
    }
}
