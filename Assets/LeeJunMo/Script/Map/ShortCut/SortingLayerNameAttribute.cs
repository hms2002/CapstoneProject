using UnityEngine;

/// <summary>
/// 책임:
/// - 문자열 Sorting Layer 필드가 인스펙터에서 드롭다운으로 편집되도록 표시 의도를 제공한다.
/// - 런타임 데이터는 문자열로 유지해 기존 직렬화와 Unity SortingLayer API 사용을 단순하게 유지한다.
/// </summary>
public sealed class SortingLayerNameAttribute : PropertyAttribute
{
}
