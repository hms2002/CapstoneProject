using UnityEngine;

public class DamagePopupSpawner2D : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private DamagePopupUI popupPrefab;

    [Tooltip("팝업이 생성될 Canvas의 RectTransform (예: DamagePopupCanvas/Root)")]
    [SerializeField] private RectTransform canvasRoot;

    [Tooltip("월드->스크린 변환에 사용할 카메라 (비우면 Camera.main)")]
    [SerializeField] private Camera worldCamera;

    [Header("Offset (World)")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Random (UI px)")]
    [SerializeField] private float randomX = 25f;
    [SerializeField] private float randomY = 10f;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    public void Spawn(float amount, Vector3 worldPos)
    {
        if (popupPrefab == null || canvasRoot == null || worldCamera == null) return;

        // 실제 감소량 기반이므로 정수로 올림/반올림 취향대로
        int dmgInt = Mathf.Max(1, Mathf.CeilToInt(amount));

        // 월드 -> 스크린
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos + worldOffset);

        // 스크린 -> 캔버스 로컬(anchoredPosition)
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, screenPos, null, out Vector2 localPoint))
            return;

        localPoint += new Vector2(Random.Range(-randomX, randomX), Random.Range(-randomY, randomY));

        var inst = Instantiate(popupPrefab, canvasRoot);
        inst.Setup(dmgInt, localPoint);
    }
}
