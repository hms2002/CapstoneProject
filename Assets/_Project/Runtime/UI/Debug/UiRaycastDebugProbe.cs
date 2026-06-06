using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 책임 :
/// - 현재 마우스 커서 아래의 UI 레이캐스트 결과를 수집해, 어떤 UI가 hover를 가로채는지 디버그 로그로 보여준다.
/// - 상태 HUD hover가 기대대로 동작하지 않을 때 EventSystem 기준 최상단 hit 순서를 빠르게 확인하게 만든다.
/// </summary>
public sealed class UiRaycastDebugProbe : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private KeyCode logKey = KeyCode.F8;
    [SerializeField] private bool logTopOnly = false;
    [SerializeField] private bool includeParents = true;

    private readonly List<RaycastResult> raycastResults = new();

    private void Update()
    {
        if (!Input.GetKeyDown(logKey))
            return;

        LogCurrentPointerHits();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 마우스 위치로 EventSystem UI 레이캐스트를 수행하고 hit 결과를 사람이 읽기 쉬운 로그 문자열로 변환한다.
    /// - 최상단 하나만 볼지, 전체 순서를 볼지 설정에 따라 디버그 밀도를 조절한다.
    /// </summary>
    public void LogCurrentPointerHits()
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[UiRaycastDebugProbe] EventSystem.current is null.");
            return;
        }

        PointerEventData pointerData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count == 0)
        {
            Debug.Log($"[UiRaycastDebugProbe] No UI hit at mouse position {Input.mousePosition}.");
            return;
        }

        StringBuilder builder = new();
        builder.AppendLine($"[UiRaycastDebugProbe] UI hits at {Input.mousePosition} (count={raycastResults.Count})");

        int count = logTopOnly ? 1 : raycastResults.Count;
        for (int i = 0; i < count; i++)
        {
            RaycastResult hit = raycastResults[i];
            builder.Append(i + 1)
                .Append(". ")
                .Append(hit.gameObject.name)
                .Append(" | module=")
                .Append(hit.module != null ? hit.module.GetType().Name : "null")
                .Append(" | sortingLayer=")
                .Append(hit.sortingLayer)
                .Append(" | sortingOrder=")
                .Append(hit.sortingOrder)
                .Append(" | depth=")
                .Append(hit.depth)
                .Append(" | distance=")
                .Append(hit.distance);

            if (includeParents)
            {
                builder.Append(" | path=")
                    .Append(BuildTransformPath(hit.gameObject.transform));
            }

            builder.AppendLine();
        }

        Debug.Log(builder.ToString(), this);
    }

    /// <summary>
    /// 책임 :
    /// - hit된 UI 오브젝트의 부모 계층 경로를 문자열로 구성해 어떤 패널/캔버스 아래에 있는지 한 번에 파악하게 만든다.
    /// - 이름만으로 구분하기 어려운 UI 중첩 구조 디버깅을 쉽게 한다.
    /// </summary>
    private static string BuildTransformPath(Transform target)
    {
        if (target == null)
            return "null";

        Stack<string> path = new();
        Transform current = target;
        while (current != null)
        {
            path.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", path);
    }
}
