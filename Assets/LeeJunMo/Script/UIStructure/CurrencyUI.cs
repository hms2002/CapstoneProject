using UnityEngine;
using TMPro; // TextMeshPro 사용 시 필수

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;

    private void Start()
    {
        // 시작 시 초기값 표시
        if (CurrencyManager.Instance != null)
        {
            UpdateUI(CurrencyManager.Instance.GetMagicStone());

            // 이벤트 구독 (값이 바뀔 때마다 자동 갱신)
            CurrencyManager.Instance.OnMagicStoneChanged += UpdateUI;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMagicStoneChanged -= UpdateUI;
        }
    }

    private void UpdateUI(int amount)
    {
        if (amountText != null)
        {
            // 예: "1,500" 처럼 콤마 표시
            amountText.text = $"{amount:N0}";
        }
    }
}