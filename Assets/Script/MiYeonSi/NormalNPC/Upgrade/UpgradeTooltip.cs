using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UpgradeTooltip : MonoBehaviour
{
    public static UpgradeTooltip Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private RectTransform backgroundRect;

    [Header("Settings")]
    public float offset = 20f; // 슬롯과 툴팁 사이 간격

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        Hide();
    }

    // [중요] 위치(targetPos)를 받아서 고정시킴
    public void Show(string title, string content, Vector3 targetPos)
    {
        titleText.text = title;
        contentText.text = content;

        // 내용 크기에 맞춰 배경 리빌딩
        LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);

        // 위치 계산 및 적용
        SetPositionIdeally(targetPos);

        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }

    private void SetPositionIdeally(Vector3 targetPos)
    {
        // 1. 일단 타겟 위치로 이동
        rectTransform.position = targetPos;

        // 2. 화면 좌표로 변환하여 어느 사분면에 있는지 확인
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, targetPos);

        float pivotX = (screenPoint.x / Screen.width);
        float pivotY = (screenPoint.y / Screen.height);

        // 3. 화면 오른쪽에 있으면 툴팁을 왼쪽으로 펼치기 (Pivot X = 1)
        //    화면 위쪽에 있으면 툴팁을 아래로 펼치기 (Pivot Y = 1)
        float setPivotX = (pivotX > 0.5f) ? 1f : 0f;
        float setPivotY = (pivotY > 0.5f) ? 1f : 0f;

        rectTransform.pivot = new Vector2(setPivotX, setPivotY);

        // 4. 슬롯과 겹치지 않게 오프셋 적용 (Pivot 방향 반대로 밀기)
        float dirX = (setPivotX == 0) ? 1 : -1;
        float dirY = (setPivotY == 0) ? 1 : -1;

        rectTransform.position += new Vector3(offset * dirX, offset * dirY, 0);
    }
}