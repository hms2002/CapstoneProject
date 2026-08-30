using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Lean 무기 표현과 무기 교체 시 일시 오브젝트 정리를 소유합니다.</summary>
[DisallowMultipleComponent]
public sealed class CrimsonBoundaryRuntimeState : WeaponAbilityRuntimeState
{
    private readonly List<GameObject> transients = new();

    private void Awake()
    {
        GameObject visual = CrimsonBoundaryUtility.CreateSquare(
            "CrimsonBoundary_WeaponSquare",
            transform.position,
            new Vector2(1.15f, 0.22f),
            new Color(0.65f, 0.04f, 0.02f, 1f),
            "Entity",
            0);
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0.55f, 0f, 0f);
    }

    private void OnDisable() => ClearTransients();
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
        if (transient != null)
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
