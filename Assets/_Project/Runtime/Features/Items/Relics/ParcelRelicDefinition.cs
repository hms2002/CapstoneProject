using UnityEngine;

/// <summary>
/// 책임 : 배달 이벤트의 소포를 유물 인벤토리 슬롯을 점유하는 효과 없는 특수 유물로 식별한다.
/// 일반 유물의 중복 강화와 외부 컨테이너 이동 규칙에서는 별도로 처리된다.
/// </summary>
[CreateAssetMenu(fileName = "RD_EventParcel", menuName = "Game/Relic Definition/Event Parcel")]
public sealed class ParcelRelicDefinition : RelicDefinition
{
    public const int MaximumCarryCount = 3;
}
