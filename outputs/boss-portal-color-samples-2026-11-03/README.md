# 보스별 포탈 색상 견본

2026-11-03 · Aseprite 견본 제작 후 사용자 승인으로 [게임 적용 완료](../boss-portal-integration-2026-11-03/README.md).

[정지 비교판](boss-portal-comparison-2x.png) · [움직이는 비교판](boss-portal-comparison.gif) · [편집 가능한 비교판](boss-portal-comparison.aseprite)

선택 확정: 사용자가 [클로에 색상 대안](chloe-options/README.md)의 **C 회보라**를 선택했다. 현재 전체 비교판과 `01-chloe-*` 파일은 C를 반영하며, 다른 보스 견본은 유지한다.

비교판은 왼쪽부터 원본, 클로에, 스피리, 멜타, 마왕이다. 위쪽은 어두운 바닥에서 동일한 파티클 움직임을 보여 주며, 아래쪽은 밝은 바닥에서 반투명 막을 확인하는 견본이다. `1X`는 비교판 확대 전 원본 픽셀 배율을 뜻한다. PNG 비교판 전체는 2배 확대했다.

| 보스 | 방향 | 막 RGB | 안쪽 빛 테두리 | 파티클 시작색 두 값 | Aseprite 원본 |
| --- | --- | --- | --- | --- | --- |
| 클로에 / 그림자 | C 회보라 — 선택 | `#79749B` | `#B7ACC9` | `#998CB6` / `#DAD0E8` | [포탈](01-chloe-portal.aseprite) · [애니메이션](01-chloe-preview.aseprite) |
| 스피리 / 드래곤 | 호박빛 | `#D99A3F` | `#FFCA68` | `#FFAF4D` / `#FFE6A8` | [포탈](02-spiri-portal.aseprite) · [애니메이션](02-spiri-preview.aseprite) |
| 멜타 / 슬라임 | 민트빛 | `#38C9A3` | `#6CF3D0` | `#43DBB1` / `#B9FFE8` | [포탈](03-melta-portal.aseprite) · [애니메이션](03-melta-preview.aseprite) |
| 마왕 | 자수정빛 | `#9C55DC` | `#C47CFF` | `#B66AFF` / `#EBC2FF` | [포탈](04-demon-king-portal.aseprite) · [애니메이션](04-demon-king-preview.aseprite) |

클로에의 색상 선택은 C로 확정하고 다른 세 보스와 함께 게임에 적용했다. 모든 막의 알파는 원본과 같은 `129/255`(약 50.6%), 빛 테두리는 `255/255`다. 파티클 시작색 두 값은 모두 알파 1을 기준으로 하며, 견본의 소멸 애니메이션은 별도로 알파를 낮춘다.

## 파일과 편집

- `*-portal.aseprite` / `*-portal.png`: 원본과 같은 50×49 RGBA. Aseprite는 석재, 막, 안쪽 빛 테두리의 3레이어다. PNG는 합성본이며 반투명 알파를 보존한다.
- `*-membrane.png`: 동일한 50×49 캔버스에 내부 막만 추출한 파일이다.
- `*-preview.aseprite` / `*-preview.gif`: 60×64, 24프레임, 프레임당 100ms, 2.4초 반복. 바닥·막·석재·빛 테두리·파티클의 5레이어로 편집한다. 바닥은 견본 전용이다.
- `boss-portal-comparison.aseprite`: 680×412, 24프레임. 레이아웃과 5개 색상 열이 분리된 비교판이다.
- `boss-portal-comparison-2x.png`: 1360×824, 최근접 확대 정지 이미지다.
- `palette-spec.json`: 색상, 알파, 크기, 프레임 정보다.
- `author-portals.lua`: 설치된 Aseprite 1.3.18.6에서 실행한 제작 스크립트다. 원본 프로젝트는 읽기만 한다. 제작에 사용한 API는 [Aseprite 공식 문서](https://aseprite.org/api/sprite)를 참고했다.
- `source-inspection.txt`: 원본과 PNG 픽셀 대조 결과다.

## 확인 및 적용 경계

입력은 [Portal.png](../../Assets/_Project/Art/Sprites/Map/Environment/Level/Portal.png)다. [ScenePortal.prefab](../../Assets/_Project/Prefabs/Map/Portal/ScenePortal.prefab) 및 [ProceduralSceneTravelPortal.prefab](../../Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab)은 이 스프라이트를 사용하고, `PortalParticle`의 `startColor`는 두 색 사이에서 선택하는 별도 설정이다.

PNG를 원본과 픽셀 단위로 대조했다. 원본 재구성은 차이 0, 네 변형 모두 석재/외곽 차이 0, 알파 차이 0이다. 변형마다 내부 막 576픽셀과 빛 테두리 88픽셀의 RGB만 바뀐다. Aseprite 파일 헤더의 크기·32비트 RGBA·프레임 수를 확인했고, 비교판 PNG를 시각적으로 검토했다.

GIF는 색상과 움직임 비교용이다. 반투명도를 표현하기 위해 바닥에 합성했으므로 스프라이트 대체 파일로 사용하지 않는다. 실제 Unity ParticleSystem의 입자 경로·발광·머티리얼·조명은 재현하지 않았다. 최초 견본 제작 단계에서는 게임 파일을 변경하지 않았으며, 이후 승인된 게임 연결 범위와 Unity 검증은 [적용 기록](../boss-portal-integration-2026-11-03/README.md)에 정리했다.

포탈별 스프라이트 참조와 ParticleSystem 시작색 연결을 완료했다. SpriteRenderer tint와 원본 석재는 유지했다. 실제 씬 조명과 플레이 중 잠금/클리어 전환은 수동 확인이 남아 있다.
