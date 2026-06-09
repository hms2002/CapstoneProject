using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 기묘한 쇳덩이가 슬롯 단위로 유지해야 하는 잔탄 상태를 보관한다.
/// - 재장전이 없는 6발 한정 무기 규칙을 장착 프리팹 수명과 분리된 persistent data로 고정한다.
/// </summary>
[Serializable]
public sealed class OddIronRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    [Serializable]
    private struct PersistedState
    {
        public int currentAmmo;
    }

    private int maxAmmo = 6;
    private int currentAmmo = 6;

    public int MaxAmmo => Mathf.Max(0, maxAmmo);
    public int CurrentAmmo => Mathf.Clamp(currentAmmo, 0, MaxAmmo);
    public bool HasAmmo => CurrentAmmo > 0;
    public string StateType => nameof(OddIronRuntimeData);

    /// <summary>
    /// 책임 :
    /// - loadout authoring 값을 새 runtime data의 기본 탄창 규칙으로 주입한다.
    /// - 새로 획득한 기묘한 쇳덩이가 항상 가득 찬 탄창에서 시작하게 한다.
    /// </summary>
    public void ApplyDefaults(OddIronLoadout loadout)
    {
        maxAmmo = loadout != null ? loadout.MaxAmmo : 6;
        maxAmmo = Mathf.Max(0, maxAmmo);
        currentAmmo = maxAmmo;
    }

    /// <summary>
    /// 책임 :
    /// - 단발 사격 시 잔탄 1발을 성공 실행 시점에 소비한다.
    /// - 선택 단계와 실제 실행 단계 사이에 상태가 바뀐 경우를 안전하게 실패로 처리한다.
    /// </summary>
    public bool TryConsumeOneRound()
    {
        if (CurrentAmmo <= 0)
            return false;

        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 전탄 난사 시작 시점에 남은 탄을 모두 소비하고 발사할 탄 수를 반환한다.
    /// - 난사 중 취소되어도 이미 탄을 쏟아낸 것으로 보는 일회용 무기 정책을 고정한다.
    /// </summary>
    public int ConsumeAllRounds()
    {
        int consumed = CurrentAmmo;
        currentAmmo = 0;
        return consumed;
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            currentAmmo = CurrentAmmo
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        PersistedState state = JsonUtility.FromJson<PersistedState>(json);
        currentAmmo = Mathf.Clamp(state.currentAmmo, 0, MaxAmmo);
    }
}
