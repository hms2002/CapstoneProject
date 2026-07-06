using System;

/// <summary>
/// 책임 : TagSystem이 직접 부여된 태그 count를 저장/복원하기 위해 사용하는 Core 스냅샷 DTO다.
/// </summary>
[Serializable]
public sealed class ExplicitTagSnapshot
{
    public string tagName;
    public int count;
}
