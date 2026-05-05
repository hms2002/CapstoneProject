---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-05-05
---

# Dialogue Architecture

이 문서는 현재 프로젝트의 미연시/대화/NPC/호감도 시스템을 큰 그림으로 설명합니다.

## 어디서 시작하나

- 대화 시작/등록 진입점:
  - [DialogueService.cs](../../Assets/LeeJunMo/Script/Dialogue/DialogueService.cs)
  - `DialogueController`

- 보스 대화 시퀀스:
  - [BossDialogueRunner.cs](../../Assets/LeeJunMo/Script/Dialogue/BossDialogueRunner.cs)
  - [BossEncounterDirector.cs](../../Assets/LeeJunMo/Script/Dialogue/BossEncounterDirector.cs)
  - [BossTalkManager.cs](../../Assets/LeeJunMo/Script/Dialogue/BossTalkManager.cs)

- NPC 데이터/기능:
  - `NPCData`
  - `NPCDatabase`
  - `NPCFeatureController`

- 호감도 시스템:
  - `AffectionManager`
  - `AffectionProgressStore`
  - `AffectionRewardProcessor`

## 핵심 책임 분리

- `DialogueService`
  - 현재 씬의 `DialogueController`를 찾아 대화를 시작하는 공용 진입점입니다.
  - 대화 재생 중 런 타이머 pause 동기화도 함께 맡습니다.

- `DialogueController`
  - 실제 Ink 대화 재생, UI 표시, 진행 제어를 담당합니다.

- `BossDialogueRunner`
  - 보스용 NPC 데이터와 Ink 자산을 준비해 `DialogueService`로 넘기는 어댑터 역할입니다.

- `BossEncounterDirector / BossTalkManager`
  - 보스 등장 연출, 카메라, 플레이어 잠금, 대화, 전투 시작까지 이어지는 시퀀스를 조율합니다.

- `NPCData`
  - 대화에 필요한 NPC의 기본 데이터와 Primary Ink를 가집니다.

- `NPCFeatureController`
  - 상점, 업그레이드 같은 NPC별 기능 확장 포인트를 묶습니다.

- `Affection*`
  - 호감도 진행, 저장, 보상 처리, UI 표시를 맡습니다.

## 현재 구조에서 기억할 점

- 대화 시작은 가능하면 `DialogueService`를 통하게 유지하는 게 좋습니다.
- 보스 대화는 단순 대화가 아니라 카메라/플레이어 잠금/전투 시작과 함께 묶여 있습니다.
- 대화 시스템은 NPC 기능, 호감도, 시네마틱 연출과 맞물려 있으니 한쪽만 고치면 체감이 어긋날 수 있습니다.

## 어떤 수정이 어디에 가까운가

### Ink 대화를 시작/종료하는 공용 경로를 바꾸고 싶다
- `DialogueService`
- `DialogueController`

### 보스 등장 대화 흐름을 바꾸고 싶다
- `BossEncounterDirector`
- `BossDialogueRunner`
- `BossTalkManager`

### 특정 NPC의 대사 자산/기본 데이터 구성을 바꾸고 싶다
- `NPCData`
- `NPCDatabase`

### NPC 상점/업그레이드 같은 기능을 바꾸고 싶다
- `NPCFeatureController`
- 각 feature 하위 폴더

### 호감도 누적/보상/UI를 바꾸고 싶다
- `AffectionManager`
- `AffectionProgressStore`
- `AffectionRewardProcessor`
- `AffectionUI`

## 주의할 점

- 대화 재생 중에는 플레이어 상태와 런 타이머가 함께 영향을 받습니다.
- 보스 대화는 일반 NPC 대화보다 시퀀스 의존성이 크므로, `BossEncounterDirector`와 분리해서 보면 놓치기 쉽습니다.
