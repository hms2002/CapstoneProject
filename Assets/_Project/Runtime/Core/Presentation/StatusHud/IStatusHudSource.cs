using System.Collections.Generic;

/// <summary>
/// 책임 :
/// - 상태를 소유한 시스템이 HUD 표시용 엔트리를 수집 버퍼에 투영하도록 하는 공통 계약을 정의한다.
/// - HUD가 무기, 유물, 적, 상호작용 상태를 직접 몰라도 source/provider를 통해 같은 방식으로 읽게 만든다.
/// </summary>
public interface IStatusHudSource
{
    void CollectStatusHudEntries(List<StatusHudEntry> buffer);
}
