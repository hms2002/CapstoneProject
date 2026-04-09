using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 경고 팝업의 텍스트 표시와 간단한 표시/숨김 수명을 담당하는 실제 UI 뷰다.
/// - WarningPopupService가 전달한 메시지를 받아 화면에 잠시 보여주고 닫힘 시점을 다시 서비스에 알린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WarningPopupUI : MonoBehaviour
{
    public const float DefaultDuration = 1.4f;

    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private GameObject root;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float defaultDuration = DefaultDuration;

    private Coroutine activeRoutine;

    public bool IsShowing => activeRoutine != null;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (root == null)
            root = canvasGroup != null ? canvasGroup.gameObject : gameObject;

        HideImmediate();
    }

    /// <summary>
    /// 책임 :
    /// - WarningPopupService와의 연결 지점을 유지해 향후 제어 주체를 바꾸더라도 동일한 authoring 규칙을 쓸 수 있게 한다.
    /// - 현재 덮어쓰기 모델에선 별도 상태 동기화 없이 서비스와 뷰의 결합만 명시한다.
    /// </summary>
    public void BindService(WarningPopupService service)
    {
        // 현재는 overwrite 모델이라 추가 상태 저장이 필요 없다.
    }

    /// <summary>
    /// 책임 :
    /// - 전달받은 경고 문자열을 화면에 일정 시간 표시한다.
    /// - 중복 요청이 들어오면 이전 표시를 즉시 종료하고 새 요청을 우선 반영한다.
    /// </summary>
    public void ShowWarning(string message, float duration = DefaultDuration)
    {
        if (messageText == null)
            return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        messageText.text = message;
        activeRoutine = StartCoroutine(ShowRoutine(Mathf.Max(0.1f, duration > 0f ? duration : defaultDuration)));
    }

    /// <summary>
    /// 책임 :
    /// - 현재 표시 상태를 즉시 숨김으로 되돌린다.
    /// - 씬 로드 직후나 초기 authoring 상태에서 잔상이 남지 않도록 보장한다.
    /// </summary>
    public void HideImmediate()
    {
        if (root != null && root != gameObject)
            root.SetActive(false);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private IEnumerator ShowRoutine(float duration)
    {
        if (root != null && root != gameObject)
            root.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        yield return new WaitForSecondsRealtime(duration);

        HideImmediate();
        activeRoutine = null;
    }
}
