using UnityEngine;

namespace UnityGAS
{
    /// <summary>마우스/패드 등 현재 조준 지점의 월드 좌표를 노출하는 중립 계약입니다.</summary>
    public interface ICursorWorldSource2D
    {
        Vector2 CursorWorld { get; }
    }
}
