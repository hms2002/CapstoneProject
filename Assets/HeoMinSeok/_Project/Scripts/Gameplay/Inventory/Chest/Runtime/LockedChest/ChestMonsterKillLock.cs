using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 특정 상자에 연결된 몬스터 처치 기반 잠금 상태를 관리한다.
/// 스폰된 몬스터를 등록받아 살아 있는 대상 수를 추적하고,
/// 모두 제거되면 잠금이 해제되도록 판정하는 규칙만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChestMonsterKillLock : MonoBehaviour
{
    private readonly List<GameObject> trackedMonsters = new();

    [Header("Presentation")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private WorldObjectPresentationDefinition unlockPresentation = new();

    private bool isUnlocked = true;
    private int remainingAliveCount = 0;
    private WorldObjectPresentationRuntime unlockPresentationRuntime;

    /// <summary>
    /// 책임 : 잠금 상태가 바뀌었을 때 외부 뷰나 연출 시스템에 알린다.
    /// bool 인자는 현재 잠금 해제 여부이다.
    /// </summary>
    public event Action<bool> OnLockStateChanged;

    /// <summary>
    /// 책임 : 남은 몬스터 수가 바뀌었을 때 외부 뷰에 알린다.
    /// int 인자는 현재 살아 있는 대상 수이다.
    /// </summary>
    public event Action<int> OnRemainingCountChanged;

    /// <summary>
    /// 책임 : 현재 상자가 잠금 해제된 상태인지 외부에 제공한다.
    /// </summary>
    public bool IsUnlocked => isUnlocked;

    /// <summary>
    /// 책임 : 현재 살아 있는 잠금 대상 몬스터 수를 외부에 제공한다.
    /// </summary>
    public int RemainingAliveCount => remainingAliveCount;

    private void Awake()
    {
        unlockPresentationRuntime = new WorldObjectPresentationRuntime(gameObject);
        RecalculateState(raiseEvents: false);
    }

    private void Update()
    {
        int oldCount = trackedMonsters.Count;
        CompactDeadEntries();

        if (oldCount != trackedMonsters.Count)
            RecalculateState(raiseEvents: true);
    }

    /// <summary>
    /// 책임 : 새로 스폰된 몬스터를 잠금 해제 대상에 등록한다.
    /// null 또는 중복 등록은 무시한다.
    /// </summary>
    public void RegisterMonster(GameObject monster)
    {
        if (monster == null)
            return;

        if (trackedMonsters.Contains(monster))
            return;

        trackedMonsters.Add(monster);
        RecalculateState(raiseEvents: true);
    }

    /// <summary>
    /// 책임 : 현재 등록된 잠금 대상 몬스터를 모두 비운다.
    /// 디버그나 강제 초기화 용도로 사용한다.
    /// </summary>
    [ContextMenu("Clear Registered Monsters")]
    public void ClearRegisteredMonsters()
    {
        trackedMonsters.Clear();
        RecalculateState(raiseEvents: true);
    }

    /// <summary>
    /// 책임 : 등록된 몬스터 목록에서 이미 파괴된 항목(null)을 제거한다.
    /// </summary>
    private void CompactDeadEntries()
    {
        for (int i = trackedMonsters.Count - 1; i >= 0; i--)
        {
            if (trackedMonsters[i] == null)
                trackedMonsters.RemoveAt(i);
        }
    }

    /// <summary>
    /// 책임 : 현재 등록 목록을 기준으로 남은 수와 잠금 상태를 다시 계산하고,
    /// 값이 바뀌었을 때 필요한 이벤트를 발행한다.
    /// </summary>
    private void RecalculateState(bool raiseEvents)
    {
        CompactDeadEntries();

        int newRemainingCount = trackedMonsters.Count;
        bool newUnlocked = newRemainingCount == 0;

        bool countChanged = remainingAliveCount != newRemainingCount;
        bool lockStateChanged = isUnlocked != newUnlocked;

        remainingAliveCount = newRemainingCount;
        isUnlocked = newUnlocked;

        if (!raiseEvents)
            return;

        if (countChanged)
            OnRemainingCountChanged?.Invoke(remainingAliveCount);

        if (lockStateChanged)
        {
            OnLockStateChanged?.Invoke(isUnlocked);
            if (isUnlocked)
                PlayUnlockPresentation();
        }
    }

    private void PlayUnlockPresentation()
    {
        unlockPresentationRuntime?.PlayExecuteOnly(
            unlockPresentation,
            target: gameObject,
            anchor: presentationAnchor != null ? presentationAnchor : transform,
            sourceObject: this);
    }
}
