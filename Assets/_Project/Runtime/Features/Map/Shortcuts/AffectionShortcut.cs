using UnityEngine;

public class AffectionShortcut : PermanentShortcut
{
    [Header("프롬프트")]
    [SerializeField] private string interactPromptText = "살펴보기";

    [Header("호감도 설정")]
    [SerializeField] private int targetBossID;
    [SerializeField] private int requiredAffection;

    protected override void Start()
    {
        base.Start();

        if (targetDoor != null && targetDoor.IsOpen)
            return;

        if (AffectionManager.Instance != null)
            CheckAndAutoOpen(AffectionManager.Instance.GetAffection(targetBossID));

        if (AffectionManager.Instance != null)
            AffectionManager.Instance.OnAffectionChanged += HandleAffectionChange;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (AffectionManager.Instance != null)
            AffectionManager.Instance.OnAffectionChanged -= HandleAffectionChange;
    }

    private void HandleAffectionChange(int npcId, int currentAffection)
    {
        if (npcId == targetBossID)
            CheckAndAutoOpen(currentAffection);
    }

    private void CheckAndAutoOpen(int currentAffection)
    {
        if (targetDoor != null && !targetDoor.IsOpen && currentAffection >= requiredAffection)
        {
            OnSuccess();

            if (AffectionManager.Instance != null)
                AffectionManager.Instance.OnAffectionChanged -= HandleAffectionChange;
        }
    }

    protected override bool CheckCondition(IPlayerInteractor player)
    {
        int current = AffectionManager.Instance != null ? AffectionManager.Instance.GetAffection(targetBossID) : 0;
        Debug.Log($"[안내] {targetBossID}번 보스의 호감도가 부족합니다. (현재:{current} / 필요:{requiredAffection})");
        return false;
    }

    public override string GetInteractDescription() => interactPromptText;
}
