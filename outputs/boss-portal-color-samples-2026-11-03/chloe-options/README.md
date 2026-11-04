# 클로에 포탈 색상 대안

2026-11-03. 사용자 요청에 따라 클로에만 재검토한 Aseprite 견본이다. 기존 루비색과 차가운 계열 3종을 비교하며 다른 보스 견본과 게임 에셋은 수정하지 않았다.

[정지 비교판](chloe-color-options-2x.png) · [애니메이션](chloe-color-options.gif) · [편집용 Aseprite](chloe-color-options.aseprite)

| 후보 | 방향 | 막 RGB | 빛 테두리 | 파티클 두 색 | 개별 Aseprite |
| --- | --- | --- | --- | --- | --- |
| 기존 | 루비 | `#C64B78` | `#F178A5` | `#E55D91` / `#FFC0D6` | [기존](00-previous-ruby-portal.aseprite) |
| A | 청보라 | `#5367C5` | `#93AAFF` | `#6B82EA` / `#C9D5FF` | [A](01-a-blue-violet-portal.aseprite) |
| B | 어두운 남청 | `#426D92` | `#73B5D6` | `#5B98C5` / `#BAE6FA` | [B](02-b-midnight-portal.aseprite) |
| C — 선택 확정 | 낮은 채도의 회보라 | `#79749B` | `#B7ACC9` | `#998CB6` / `#DAD0E8` | [C](03-c-smoky-lilac-portal.aseprite) |

사용자가 **C 회보라**를 선택했다. [전체 보스 비교판](../boss-portal-comparison-2x.png)과 [클로에 개별 원본](../01-chloe-portal.aseprite)에 반영했고, 후속 요청으로 [게임에도 적용했다](../../boss-portal-integration-2026-11-03/README.md). 이 폴더의 대안 비교판은 선택 과정을 참고할 수 있도록 그대로 보관한다.

모든 포탈은 50×49 RGBA이며 막 알파 129/255, 원본 석재와 외곽을 유지한다. 정적 Aseprite는 석재/막/빛 테두리 3레이어, `*-preview.aseprite`는 바닥/막/석재/빛 테두리/파티클 5레이어다. 애니메이션은 24프레임, 100ms씩 총 2.4초이며 모든 후보에 같은 움직임을 사용한다. GIF 바닥은 비교용이고 실제 Unity ParticleSystem의 동작을 재현하지 않는다.

`author-chloe-options.lua`는 기존 제작 스크립트에서 클로에 후보와 비교판 제목·폭만 바꾼 별도 사본이다. `palette-spec.json`은 전체 색상값, `source-inspection.txt`는 픽셀 대조 기록이다. 원본과 비교하여 각 후보의 석재/외곽 및 알파 차이 0을 확인했고 정지 비교판을 시각적으로 검토했다. 이 후보 제작 단계에서는 Unity import/컴파일/Play Mode와 자동 테스트를 실행하지 않았으며, 후속 게임 적용의 검증 결과는 위 적용 기록을 따른다.
