using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 보스 한 체에 대응하는 HUD 슬롯 프리팹의 표시 요소를 갱신한다.
/// - 이름, 체력바, 그로기바, 그로기 상태 라벨을 슬롯 내부 규칙으로만 제어한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHudSlotView : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private BossHealthBarUI healthBarUI;
    [SerializeField] private BossGroggyBarUI groggyBarUI;
    [SerializeField] private TMP_Text groggyStateText;

    [Header("Labels")]
    [SerializeField] private string groggyStateLabel = "GROGGY";
    [SerializeField] private string defeatedStateLabel = "DEFEATED";

    private bool hasInitializedHealth;

    public void Apply(BossHudSlotSnapshot snapshot)
    {
        if (bossNameText != null)
            bossNameText.text = snapshot.DisplayName;

        ApplyTheme(snapshot.HealthBarTheme);
        ApplyHealth(snapshot.HealthRatio);
        ApplyGroggy(snapshot);
        ApplyStateLabel(snapshot);
    }

    public void ResetSlot()
    {
        hasInitializedHealth = false;

        if (healthBarUI != null)
        {
            healthBarUI.SetSplitHealthPresentation(false, null, null);
        }

        if (groggyBarUI != null)
        {
            groggyBarUI.SetVisible(false);
        }

        if (groggyStateText != null)
            groggyStateText.gameObject.SetActive(false);
    }

    private void ApplyTheme(BossHudHealthBarTheme theme)
    {
        if (healthBarUI != null)
            healthBarUI.ApplyTheme(theme);
    }

    private void ApplyHealth(float healthRatio)
    {
        if (healthBarUI == null)
            return;

        healthBarUI.SetSplitHealthPresentation(false, null, null);

        if (!hasInitializedHealth)
        {
            healthBarUI.ResetToRatio(healthRatio);
            hasInitializedHealth = true;
            return;
        }

        healthBarUI.SetHealthRatio(healthRatio);
    }

    private void ApplyGroggy(BossHudSlotSnapshot snapshot)
    {
        if (groggyBarUI == null)
            return;

        groggyBarUI.SetVisible(snapshot.HasGroggyGauge);
        if (!snapshot.HasGroggyGauge)
            return;

        groggyBarUI.SetGroggyMode(snapshot.IsGroggy);
        groggyBarUI.SetGroggyRatio(snapshot.GroggyRatio);
    }

    private void ApplyStateLabel(BossHudSlotSnapshot snapshot)
    {
        if (groggyStateText == null)
            return;

        bool shouldShow = snapshot.IsDefeated || snapshot.IsGroggy;
        groggyStateText.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        groggyStateText.text = snapshot.IsDefeated ? defeatedStateLabel : groggyStateLabel;
    }
}
