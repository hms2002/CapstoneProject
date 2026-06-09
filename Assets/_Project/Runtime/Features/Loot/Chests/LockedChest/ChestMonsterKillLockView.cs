using TMPro;
using UnityEngine;

/// <summary>
/// 책임 : ChestMonsterKillLock의 현재 상태를 시각적으로 표현한다.
/// 잠금 이펙트와 남은 몬스터 수 텍스트를 갱신하는 표시 역할만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChestMonsterKillLockView : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private ChestMonsterKillLock targetLock;

    [Header("View")]
    [Tooltip("잠금 상태일 때만 켜 둘 이펙트/오브젝트")]
    [SerializeField] private GameObject lockEffectObject;

    [Tooltip("상자 위에 남은 몬스터 수를 표시할 TMP 텍스트")]
    [SerializeField] private TMP_Text remainingCountText;

    [Header("Text")]
    [SerializeField] private string lockedFormat = "남은 몬스터 : {0}";
    [SerializeField] private string unlockedText = "";

    [Header("Policy")]
    [SerializeField] private bool hideCountTextWhenUnlocked = true;
    [SerializeField] private bool hideLockEffectWhenUnlocked = true;

    private void Reset()
    {
        if (targetLock == null)
            targetLock = GetComponent<ChestMonsterKillLock>();
    }

    private void Awake()
    {
        if (targetLock == null)
            targetLock = GetComponent<ChestMonsterKillLock>();
    }

    private void OnEnable()
    {
        if (targetLock != null)
        {
            targetLock.OnRemainingCountChanged += HandleRemainingCountChanged;
            targetLock.OnLockStateChanged += HandleLockStateChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (targetLock != null)
        {
            targetLock.OnRemainingCountChanged -= HandleRemainingCountChanged;
            targetLock.OnLockStateChanged -= HandleLockStateChanged;
        }
    }

    /// <summary>
    /// 책임 : 잠금 상태 변경 이벤트를 받아 잠금 이펙트와 텍스트 표시 상태를 갱신한다.
    /// </summary>
    private void HandleLockStateChanged(bool isUnlocked)
    {
        RefreshLockEffect(isUnlocked);
        RefreshText(isUnlocked, targetLock != null ? targetLock.RemainingAliveCount : 0);
    }

    /// <summary>
    /// 책임 : 남은 몬스터 수 변경 이벤트를 받아 텍스트 내용을 갱신한다.
    /// </summary>
    private void HandleRemainingCountChanged(int remainingCount)
    {
        bool unlocked = targetLock == null || targetLock.IsUnlocked;
        RefreshText(unlocked, remainingCount);
    }

    /// <summary>
    /// 책임 : 현재 타깃 잠금 상태 전체를 읽어 잠금 이펙트와 텍스트를 한 번에 동기화한다.
    /// </summary>
    private void RefreshAll()
    {
        bool unlocked = targetLock == null || targetLock.IsUnlocked;
        int remainingCount = targetLock != null ? targetLock.RemainingAliveCount : 0;

        RefreshLockEffect(unlocked);
        RefreshText(unlocked, remainingCount);
    }

    /// <summary>
    /// 책임 : 잠금 상태에 따라 잠금 이펙트 오브젝트의 활성 여부를 결정한다.
    /// </summary>
    private void RefreshLockEffect(bool isUnlocked)
    {
        if (lockEffectObject == null)
            return;

        if (isUnlocked)
        {
            lockEffectObject.SetActive(!hideLockEffectWhenUnlocked);
        }
        else
        {
            lockEffectObject.SetActive(true);
        }
    }

    /// <summary>
    /// 책임 : 잠금 상태와 남은 몬스터 수에 따라 표시 텍스트와 활성 여부를 갱신한다.
    /// </summary>
    private void RefreshText(bool isUnlocked, int remainingCount)
    {
        if (remainingCountText == null)
            return;

        if (isUnlocked)
        {
            if (hideCountTextWhenUnlocked)
            {
                remainingCountText.gameObject.SetActive(false);
            }
            else
            {
                remainingCountText.gameObject.SetActive(true);
                remainingCountText.text = unlockedText;
            }
        }
        else
        {
            remainingCountText.gameObject.SetActive(true);
            remainingCountText.text = string.Format(lockedFormat, remainingCount);
        }
    }
}