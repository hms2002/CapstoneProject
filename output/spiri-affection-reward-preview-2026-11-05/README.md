# 스피리 호감도 2단계 유물 해금 보상 창 시안

- 목적: 화룡의 송곳니, 태양의 파편, 독한 한잔을 한 번에 해금할 때의 보상 창 구성 검토.
- [시안 PNG](./spiri-tier-two-reward-preview.png): 기존 아이콘 3개, RewardPopupSkin 및 Galmuri9 글꼴을 사용한 정적 재구성이다. Unity 실행 화면 캡처가 아니며 실제 창 크기·줄바꿈·배치와 차이가 있을 수 있다.
- 제안 문구: “화룡의 송곳니, 태양의 파편, 독한 한잔이 해금되었습니다. 이제 상자와 상점에서 발견할 수 있습니다.”
- 현재 [RewardDisplayUI.ProcessAffection](../../Assets/_Project/Runtime/UI/Chest/RewardDisplayUI.cs)은 UnlockItemAffectionEffect의 각 유물마다 UnlockSlotUI를 생성하고, RewardText를 하단 설명에 붙인다. 개별 유물 이름 라벨은 슬롯에 자동 추가되지 않는다.
- [UnlockSlotUI](../../Assets/_Project/Runtime/UI/Inventory/UnlockSlotUI.cs)는 유물 아이콘을 표시하고 포인터 진입/이탈을 기존 ItemHoverController에 전달한다. 시안의 툴팁 안내는 이 기존 동작을 설명한다. PNG 자체는 상호작용하지 않는다.
- 실제 정의: [RD_ScorchingSong](../../Assets/_Project/Data/Items/Relics/Definitions/RD_ScorchingSong.asset), [RD_SunFragment](../../Assets/_Project/Data/Items/Relics/Definitions/RD_SunFragment.asset), [RD_DrunkenRush](../../Assets/_Project/Data/Items/Relics/Definitions/RD_DrunkenRush.asset). 현재 정의의 icon GUID와 메타 참조를 따라 이미지를 선택했다.
- [렌더링 스크립트](./render-preview.ps1), [사용한 아이콘 경로와 크기](./icon-sources.json).
- 게임의 호감도 보상·유물 기본 해금 목록·세이브·드롭/상점 규칙은 수정하지 않았다. 2단계 보상 연결이나 최초 현물 지급도 적용하지 않았다.
- 검토: 소스/프리팹 참조를 확인하고 생성 PNG를 열어 세 아이콘·이름·안내 문구·잘림 여부를 시각적으로 확인했다. 자동 테스트·Unity import·Play Mode는 실행하지 않았다. C# 소스 변경이 없어 MSBuild는 실행하지 않았다.
