# 화룡의 송곳니·휴대용 화로·태양의 파편 적용 — 2026-11-01

첨부한 32×32 PNG 3개를 그대로 적용했습니다. 작열하는 송곳의 표시 이름을 화룡의 송곳니로 바꾸고 기존 이름 키의 5개 언어 값을 함께 갱신했습니다. 저장 ID·정의 이름/파일명·GUID·효과·레벨·기존 번역 키와 entry ID는 유지했습니다.

| 유물 | 추가한 원본 PNG / 메타데이터 | 연결을 수정한 유물 정의 |
| --- | --- | --- |
| 화룡의 송곳니 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_FireDragonFang.png) · [.meta](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_FireDragonFang.png.meta) | [RD_ScorchingSong.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_ScorchingSong.asset) |
| 휴대용 화로 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_PortableBrazier.png) · [.meta](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_PortableBrazier.png.meta) | [RD_PortableBrazier.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_PortableBrazier.asset) |
| 태양의 파편 | [PNG](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SunFragment.png) · [.meta](../../../Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SunFragment.png.meta) | [RD_SunFragment.asset](../../../Assets/_Project/Data/Items/Relics/Definitions/RD_SunFragment.asset) |

## 이름 번역

| 언어 | 새 표시 이름 |
| --- | --- |
| ko | 화룡의 송곳니 |
| en | Fire Dragon's Fang |
| ja | 火竜の牙 |
| zh-Hans | 火龙之牙 |
| zh-Hant | 火龍之牙 |

외국어 이름은 프로젝트의 기존 번역 정책에 따라 언어 검수 전 초안입니다.

## 번역 파일 변경

[GameText.csv](../../../DataSheets/Localization/GameText.csv)의 대상 행 1개와 아래 StringTable의 대상 값 1개씩을 변경했습니다. 기존 다른 행과 테이블 항목은 바이트 수준으로 유지했습니다.
- [GameText_ko.asset](../../../Assets/_Project/Data/Localization/GameText_ko.asset)
- [GameText_en.asset](../../../Assets/_Project/Data/Localization/GameText_en.asset)
- [GameText_ja.asset](../../../Assets/_Project/Data/Localization/GameText_ja.asset)
- [GameText_zh-Hans.asset](../../../Assets/_Project/Data/Localization/GameText_zh-Hans.asset)
- [GameText_zh-Hant.asset](../../../Assets/_Project/Data/Localization/GameText_zh-Hant.asset)

## 검증

- 원본 PNG 바이트/SHA-256 일치 3/3, 실제 32×32 RGBA, 각 6색, 이진 알파. 원본의 모양·여백·색상은 재가공하지 않았습니다.
- 새 메타데이터는 기존 전용 아이콘과 동일한 Point / 32 PPU / 단일 스프라이트 / 밉맵 없음 / 압축 없음 설정이며, PNG보다 먼저 작성했습니다. 신규 GUID 사전 충돌 검사를 통과했습니다.
- 정의는 icon 3개와 표시 이름 1개, CSV는 1행, StringTable은 동일 entry의 값 5개만 변경했습니다.
- 기타 보호 파일 387개, 기존 공용 Flame Artifact.png/메타데이터, 먼저 적용한 유물 5종, Shared Data, 자동 생성 런타임 키 대응표, AssetInventory.json은 그대로입니다.
- Unity import·컴파일·Play Mode·실제 언어 전환/화면 확인은 실행하지 않았습니다. C# 변경이 없습니다.

[검증 결과](verification.json) · [적용 계획/보존 기준](plan.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-01.md)

Doc Impact Check: SessionLog. 이 작업으로 ErrorLog·DecisionLog·StructureMemory·Presentation HTML은 변경하지 않았으며 HTML stale candidate도 없습니다.
