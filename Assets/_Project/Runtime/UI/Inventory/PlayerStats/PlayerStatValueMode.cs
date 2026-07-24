using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 한 줄이 어떤 경로로 값을 읽을지 구분한다.
/// - 단일 Attribute, 현재/최대 Attribute 쌍, 최종 StatId 조회를 공통 규약으로 제공한다.
/// </summary>
public enum PlayerStatValueMode
{
    AttributeCurrent = 0,
    AttributeBase = 1,
    CurrentAndMaxAttribute = 2,
    StatId = 3,
}
