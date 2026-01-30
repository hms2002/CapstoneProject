using UnityEngine;

/// <summary>
/// 런타임에서 유물 인스턴스를 구분하기 위한 토큰.
/// Source 기반 제거(RemoveModifiersFromSource / UnregisterAll)를 안전하게 하기 위해 사용합니다.
/// </summary>
public sealed class RelicRuntimeToken : ScriptableObject {}
