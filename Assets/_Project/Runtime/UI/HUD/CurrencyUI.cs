using UnityEngine;
using TMPro; // TextMeshPro 사용 시 필수

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private bool showRunGold;

    private void Start()
    {
        // 시작 시 초기값 표시
        if (CurrencyManager.Instance != null)
        {
            UpdateUI(showRunGold ? CurrencyManager.Instance.GetGold() : CurrencyManager.Instance.GetMagicStone());

            // 이벤트 구독 (값이 바뀔 때마다 자동 갱신)
            if (showRunGold) CurrencyManager.Instance.OnGoldChanged += UpdateUI;
            else CurrencyManager.Instance.OnMagicStoneChanged += UpdateUI;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (CurrencyManager.Instance != null)
        {
            if (showRunGold) CurrencyManager.Instance.OnGoldChanged -= UpdateUI;
            else CurrencyManager.Instance.OnMagicStoneChanged -= UpdateUI;
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