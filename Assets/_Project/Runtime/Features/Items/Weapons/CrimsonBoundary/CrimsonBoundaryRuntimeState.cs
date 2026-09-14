using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Lean 무기 표현과 무기 교체 시 일시 오브젝트 정리를 소유합니다.</summary>
[DisallowMultipleComponent]
public sealed class CrimsonBoundaryRuntimeState : WeaponAbilityRuntimeState
{
    private readonly List<GameObject> transients = new();

    [Header("Basic attack motion")]
    [SerializeField] private Transform motionRoot;
    [SerializeField] private AnimationClip openingSwing;
    [SerializeField] private AnimationClip downwardSwing;
    [SerializeField] private AnimationClip upwardSwing;

    private PlayerCombatInput2D combatInput;
    private AbilitySystem ownerSystem;
    private AnimationClip currentSwing;
    private float swingTime;
    private float swingSpeed = 1f;
    private float idleHoldTime;
    private bool nextUpward;
    private bool releasePending;

    public float SwingTime => swingTime;

    private void OnEnable()
    {
        combatInput = GetComponentInParent<PlayerCombatInput2D>();
        ownerSystem = GetComponentInParent<AbilitySystem>();
        ResetSwing();
    }

    public void BeginSwing(float speed)
    {
        currentSwing = currentSwing == null ? openingSwing : nextUpward ? upwardSwing : downwardSwing;
        nextUpward = !nextUpward;
        swingTime = 0f;
        idleHoldTime = 0f;
        releasePending = true;
        swingSpeed = Mathf.Max(0.0001f, speed);
        SampleSwing(0f);
    }

    private void Update()
    {
        if (currentSwing == null) return;
        if (ownerSystem != null && CombatHitPause2D.IsPausedOn(ownerSystem.gameObject)) return;
        float previousSwingTime = swingTime;
        swingTime += Time.deltaTime * swingSpeed;
        if (combatInput != null && combatInput.IsHoldingPrimaryAttack)
            idleHoldTime = 0f;
        else if (!releasePending && swingTime >= currentSwing.length)
        {
            // Count only time after the swing finishes, independently of attack speed.
            idleHoldTime += Mathf.Min(Time.deltaTime,
                (swingTime - Mathf.Max(previousSwingTime, currentSwing.length)) / swingSpeed);
            if (idleHoldTime >= 0.4f)
            {
                ResetSwing();
                return;
            }
        }
        SampleSwing(Mathf.Min(swingTime, currentSwing.length));
    }

    private void SampleSwing(float time)
    {
        if (currentSwing != null) currentSwing.SampleAnimation(gameObject, time);
    }

    public void MarkProjectileReleased()
    {
        releasePending = false;
    }

    public void ResetSwing()
    {
        currentSwing = null;
        releasePending = false;
        nextUpward = false;
        idleHoldTime = 0f;
        if (motionRoot == null) return;
        motionRoot.localPosition = Vector3.zero;
        motionRoot.localRotation = Quaternion.Euler(0f, 0f, 25f);
    }

    private void OnDisable()
    {
        ResetSwing();
        ClearTransients();
    }
    private void OnDestroy() => ClearTransients();

    public override bool TryHandleAbilityInput(WeaponDefinition weapon, WeaponAbilitySlot slot, AbilityDefinition ability)
    {
        if (slot != WeaponAbilitySlot.Skill1 || ability == null)
            return false;

        // 대상이 없으면 입력만 소비하여 Ability commit/쿨다운이 발생하지 않게 합니다.
        return !CrimsonBoundaryUtility.HasBurnTargetInViewport();
    }

    public void Register(GameObject transient)
    {
        transients.RemoveAll(item => item == null);
        if (transient != null && !transients.Contains(transient))
            transients.Add(transient);
    }

    public void Forget(GameObject transient)
    {
        if (transient != null)
            transients.Remove(transient);
    }

    private void ClearTransients()
    {
        for (int i = transients.Count - 1; i >= 0; i--)
            if (transients[i] != null) Destroy(transients[i]);
        transients.Clear();
    }
}
