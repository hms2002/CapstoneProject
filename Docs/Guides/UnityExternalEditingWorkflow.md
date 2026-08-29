---
status: active
authority: workflow-guide
category: guide
last_reviewed: 2026-08-18
---

# Unity External Editing Workflow

Unity Editor가 열린 상태에서 Codex나 외부 편집기가 C#과 직렬화 에셋을 수정할 때 적용하는 공통 작업 규칙입니다.

## 목적

- 불필요한 Asset Refresh와 반복 Domain Reload를 줄인다.
- 컴파일 중 Inspector와 Prefab Import가 동시에 재구축되는 상황을 피한다.
- C# 변경과 프리팹/씬 직렬화 변경의 실패 원인을 분리한다.

## 기본 작업 순서

1. 관련 파일과 직렬화 위험을 먼저 읽고 수정 범위를 확정한다.
2. C# 변경을 가능한 한 하나의 묶음으로 완료한다.
3. Unity 컴파일과 Domain Reload가 끝날 때까지 추가 프리팹/씬 변경을 시작하지 않는다.
4. 컴파일 오류가 없는 것을 확인한 뒤 승인된 프리팹 또는 씬 변경을 하나의 묶음으로 적용한다.
5. 마지막에 한 번만 Asset Refresh/Import 결과와 직렬화 참조를 검증한다.
6. Play Mode 확인이 필요한 항목은 정적 검증과 구분하여 보고한다.

## Auto Refresh 운영

- 여러 C# 파일과 프리팹을 함께 수정하는 작업에서는 Unity `Auto Refresh`를 잠시 끄는 것을 권장한다.
- 외부 편집이 모두 끝난 뒤 `Assets > Refresh`를 한 번 실행한다.
- `Auto Save`와 `Auto Refresh`는 별개다. Prefab Auto Save를 꺼도 외부 파일 변경에 의한 컴파일과 Domain Reload는 발생할 수 있다.
- Auto Refresh를 유지해야 한다면 C# 묶음의 컴파일/Domain Reload 완료를 확인한 뒤 프리팹 묶음으로 넘어간다.

## 금지 및 주의 사항

- Unity가 컴파일 또는 Domain Reload 중일 때 연속해서 프리팹/씬 YAML을 수정하지 않는다.
- 같은 작업에서 C# 한 파일, 프리팹 한 파일을 번갈아 여러 차례 저장하지 않는다.
- Inspector가 깨진 상태에서 프리팹 참조를 다시 저장하거나 레이아웃 파일을 즉시 삭제하지 않는다.
- Inspector 복구를 위해 프로젝트 런타임 코드에 UnityEditor 내부 스타일 초기화 우회 코드를 추가하지 않는다.

## Inspector 렌더링 오류 대응

다음 로그가 반복되면 Unity 내부 Inspector 스타일 초기화 실패로 분류한다.

```text
Unable to use a named GUIStyle without a current skin
UnityEditor.EditorStyles.get_toolbarButtonRight()
UnityEditor.PropertyEditor+Styles..cctor()
```

복구 순서:

1. 현재 씬과 프리팹 변경을 저장한다.
2. Unity Editor를 완전히 종료하고 다시 실행한다.
3. 지속되면 `Layout > Default` 또는 `Revert Factory Settings`를 사용한다.
4. 마지막 수단으로 Unity를 종료한 상태에서 `UserSettings/Layouts/default-6000.dwlt`를 백업한 뒤 재생성한다.

이 오류는 런타임 UI 컴포넌트 오류로 단정하지 않는다. 먼저 `Editor.log`의 최초 예외와 Domain Reload 직전 문맥을 확인한다.
