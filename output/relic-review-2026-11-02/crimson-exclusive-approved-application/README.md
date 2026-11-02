# 한계선 전용 유물 2종 이미지 적용 — 2026-11-02

이름·효과 변경 전 이미지 적용 요청에 따라 첨부 순서로 1번을 한계선의 잔불 추격, 2번을 한계선의 관통 용암에 적용했습니다. 두 첨부 PNG를 바이트 단위로 그대로 복사하고 정의의 icon 한 줄씩만 변경했습니다.

| 첨부 순서 / 유물 | 추가한 이미지 / 메타데이터 | 연결을 변경한 정의 |
| --- | --- | --- |
| 1 한계선의 잔불 추격 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CrimsonKillShot.png) · [.meta](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CrimsonKillShot.png.meta) | [RD_CrimsonKillShot.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonKillShot.asset) |
| 2 한계선의 관통 용암 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CrimsonLavaBall.png) · [.meta](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_CrimsonLavaBall.png.meta) | [RD_CrimsonLavaBall.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_CrimsonLavaBall.asset) |

## 검증 및 보존

- 첨부/복사본 SHA-256 일치 2/2. 실제 32×32 RGBA, 각 6색, 투명/불투명 알파 그대로입니다. 색상·모양·여백을 수정하지 않았습니다.
- 새 메타데이터는 기존 전용 아이콘과 같은 Point / 32 PPU / 단일 스프라이트 / 밉맵 없음 / 압축 없음 설정입니다. 신규 GUID 2개는 기존 에셋 GUID 13850개와 중복되지 않으며, PNG보다 먼저 작성했습니다.
- 두 정의의 icon 외 모든 바이트를 유지했습니다. 이름·설명·효과·logic·레벨·relicId·정의 GUID·번역은 변경하지 않았습니다.
- 공용 FireStaff.png/메타데이터, 무기 정의, 다른 유물 이미지와 정의, 번역 파일 등 보호 파일 427개의 해시를 유지했습니다.
- Unity import·컴파일·Play Mode·실제 인벤토리/도감 화면 확인은 실행하지 않았습니다. C# 변경이 없습니다.

[검증 결과](verification.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-02.md)

Doc Impact Check: SessionLog. 이 작업으로 ErrorLog·DecisionLog·StructureMemory·Presentation HTML은 변경하지 않았으며 HTML stale candidate도 없습니다.
