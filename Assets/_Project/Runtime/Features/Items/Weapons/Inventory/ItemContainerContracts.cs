using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 인벤토리 UI와 gameplay inventory adapter가 공유하는 슬롯 컨테이너 조작 계약을 정의한다.
/// - UI 위젯이 실제 소비품, 무기, 유물 인벤토리 구현을 직접 몰라도 조회, 배치, 교환을 요청하게 한다.
/// </summary>
public interface IItemContainer
{
    int SlotCount { get; }
    event Action OnChanged;
    ScriptableObject Get(int index);
    bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1);
    bool TrySet(int index, ScriptableObject item);
    bool TrySwap(int a, int b);
}

/// <summary>
/// 책임 :
/// - 유물 슬롯처럼 같은 아이템 정의라도 슬롯별 레벨을 추가로 제공할 수 있는 컨테이너 계약을 정의한다.
/// - 상세 패널과 전송 로직이 구체 유물 인벤토리 구현을 몰라도 현재 레벨을 조회하게 한다.
/// </summary>
public interface IRelicLevelProvider
{
    bool TryGetRelicLevel(int index, out int level);
}

/// <summary>
/// 책임 :
/// - 유물 레벨을 보존한 채 특정 슬롯에 유물을 배치할 수 있는 컨테이너 계약을 정의한다.
/// - 상자와 플레이어 유물 인벤토리 사이의 이동/스왑이 concrete inventory 타입 없이 레벨 payload를 전달하게 한다.
/// </summary>
public interface IRelicSlotReceiver
{
    bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level);
}
