using TMPro;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 씬에서 직접 연결한 보스 엔티티에서 이름, HP, 그로기 정보를 읽어 각 보스 HUD 뷰에 배포한다.
/// - 보스 참조가 없으면 HUD 전체를 비활성화해 잘못된 정보 노출을 막는다.
/// - 각 개별 UI 뷰는 표현만 담당하고, 어떤 값을 언제 뿌릴지는 이 컨트롤러가 조율한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHudController : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("씬에서 직접 연결할 보스 엔티티입니다.")]
    [SerializeField] private BossControllerBase targetBoss;

    [Tooltip("비어 있으면 보스 오브젝트 이름을 그대로 사용합니다.")]
    [SerializeField] private string displayNameOverride;

    [Header("Views")]
    [SerializeField] private BossHealthBarUI healthBarUI;
    [SerializeField] private BossGroggyBarUI groggyBarUI;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text groggyStateText;

    [Header("Groggy Label")]
    [SerializeField] private string groggyStateLabel = "GROGGY";

    private StaggerGaugeSystem _staggerGaugeSystem;
    private GameplayEffectRunner _effectRunner;

    private void Awake()
    {
        if (targetBoss == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _staggerGaugeSystem = targetBoss.GetComponent<StaggerGaugeSystem>();
        _effectRunner = targetBoss.GetComponent<GameplayEffectRunner>();

        ApplyStaticVisuals();
        RefreshAll();
    }

    private void Update()
    {
        if (targetBoss == null)
            return;

        RefreshAll();
    }

    /// <summary>
    /// 책임 :
    /// - 보스가 바뀌지 않는 정보(표시 이름)를 뷰에 한 번 반영한다.
    /// - 이름 텍스트의 fallback 규칙을 HUD 컨트롤러 안에 모아 authoring 부담을 줄인다.
    /// </summary>
    private void ApplyStaticVisuals()
    {
        if (bossNameText == null)
        {
            ApplyGroggyLabel(false);
            return;
        }

        bossNameText.text = string.IsNullOrWhiteSpace(displayNameOverride)
            ? targetBoss.gameObject.name
            : displayNameOverride;
        ApplyGroggyLabel(targetBoss != null && targetBoss.HasGroggyTag());
    }

    private void RefreshAll()
    {
        ApplyGroggyLabel(targetBoss != null && targetBoss.HasGroggyTag());

        if (healthBarUI != null)
            healthBarUI.SetHealthRatio(targetBoss.CurrentHealthRatio);

        if (groggyBarUI != null)
        {
            bool isGroggy = targetBoss.HasGroggyTag();
            groggyBarUI.SetVisible(true);
            groggyBarUI.SetGroggyMode(isGroggy);
            groggyBarUI.SetGroggyRatio(GetGroggyRatio());
        }
    }

    /// <summary>
    /// 책임 :
    /// - 선택적으로 연결된 그로기 상태 텍스트를 현재 보스 상태에 맞춰 표시/숨김한다.
    /// - HUD 쪽에서 별도 상태 머신 없이 "지금이 딜 타임"이라는 정보를 짧게 읽히게 만든다.
    /// </summary>
    private void ApplyGroggyLabel(bool isGroggy)
    {
        if (groggyStateText == null)
            return;

        groggyStateText.gameObject.SetActive(isGroggy);
        if (isGroggy)
            groggyStateText.text = groggyStateLabel;
    }

    /// <summary>
    /// 책임 :
    /// - 기절 중이면 경과 시간 비율(0→1)을 반환해 남은 시간 표현을 기존과 반대로 뒤집는다.
    /// - 기절 중이 아니면 스태거 게이지 누적의 역비율(1→0)을 반환해 피격될수록 슬라이더가 줄어들게 만든다.
    /// </summary>
    private float GetGroggyRatio()
    {
        if (_staggerGaugeSystem == null) return 0f;

        // 기절 중 : 경과 시간 비율(남은 시간 표현 반전)
        if (_effectRunner != null)
        {
            GameplayEffect groggyEffect = _staggerGaugeSystem.staggeredEffect;
            if (groggyEffect != null && groggyEffect.duration > 0f)
            {
                float remaining = _effectRunner.GetRemainingTime(groggyEffect, targetBoss.gameObject);
                if (remaining > 0.001f)
                {
                    float elapsedNormalized = 1f - Mathf.Clamp01(remaining / groggyEffect.duration);
                    return elapsedNormalized;
                }
            }
        }

        // 기절 아닐 때 : 스태거 게이지 누적 역비율
        if (targetBoss.AttributeSet == null) return 0f;
        if (_staggerGaugeSystem.currentGaugeAttribute == null || _staggerGaugeSystem.maxGaugeAttribute == null) return 0f;

        float current = targetBoss.AttributeSet.GetAttributeValue(_staggerGaugeSystem.currentGaugeAttribute);
        float max = targetBoss.AttributeSet.GetAttributeValue(_staggerGaugeSystem.maxGaugeAttribute);

        return max > 0f ? 1f - Mathf.Clamp01(current / max) : 0f;
    }
}
