---
status: active
authority: guide
category: dialogue-authoring
last_reviewed: 2026-05-29
---

# Gemini Ink Dialogue Authoring Guide

## Purpose

이 문서는 Gemini Gem에 지식으로 넣기 위한 대사 작성 가이드다. 목표는 Gemini가 프로젝트의 Ink 대사 파일을 직접 쓸 때, 문체와 연출 태그를 과하게 만들지 않고 Unity 런타임이 처리할 수 있는 형식만 출력하게 하는 것이다.

Gemini의 역할은 대사 작성자다. Gemini는 `.ink` 원문을 작성하거나 수정한다. `.json`은 Unity Ink Integration 또는 별도 컴파일러가 생성하므로 Gemini가 직접 손으로 만들지 않는다.

## Core Rules For Gemini

- 기존 대사를 수정할 때는 의미, 사건 순서, 선택지 기능을 최대한 유지한다.
- 대사 본문에 `마왕:`, `상인:` 같은 visible speaker prefix를 쓰지 않는다. 화자 표시는 `# speaker:` 태그로 한다.
- 지원되는 태그만 쓴다. 새 태그를 invent하지 않는다.
- `# anim:` 값은 `normal`, `slow`, `angry`, `whisper`, `cold`만 사용한다.
- `fear`, `shock`, `madness` 같은 값은 현재 런타임 enum에 없으므로 쓰지 않는다.
- `# face: npcId: label`의 `label`은 반드시 제공받은 available face label 중 하나만 사용한다.
- face label을 모르면 `# face` 태그를 쓰지 않는다.
- `# face` 태그에서 `npcId`는 NPCData를 찾기 위한 id이고, SpriteLibrary category는 런타임에서 `Face`를 사용한다. Gemini는 category를 쓰지 않는다.
- RichText는 색/크기/굵기 정도만 짧은 구간에 제한적으로 사용한다.
- RichText 색상은 named color만 사용한다. `<color=#A855F7>` 같은 hex color는 Ink가 `#`를 tag marker로 해석할 수 있으므로 쓰지 않는다.
- RunSpecial NPC는 별도 SpeechBubble/SO 흐름이다. 특별히 요청받지 않는 한 Ink portrait dialogue로 작성하지 않는다.
- `.json`, Unity scene, prefab, ScriptableObject, C# 코드는 작성하지 않는다.

## Supported Ink Tags

### Speaker

```ink
# speaker: 1005
```

NPC id를 알 때 사용한다.

```ink
# speaker: ???
```

정체를 숨겨야 하는 화자나 임시 화자에만 사용한다.

### Face

```ink
# face: 1005: Normal
# face: 1005: Smile
# face: 1005: Sad
```

규칙:
- `npcId`는 NPCData id다.
- `label`은 해당 NPC의 SpriteLibrary `Face` category 안에 있는 label이어야 한다.
- 사용 가능한 표정 목록을 입력으로 받지 못했다면 face 태그를 생략한다.
- `default`, `CloseEye`, `Angry` 같은 label을 임의로 만들지 않는다.

### Dialogue Animation

```ink
# anim: normal
# anim: slow
# anim: angry
# anim: whisper
# anim: cold
```

의미:

| anim | Use when | Tone |
| --- | --- | --- |
| `normal` | 일반 대화 | 기본 typewriter |
| `slow` | 진지함, 슬픔, 고백, 패배 | 느린 출력과 긴 pause |
| `angry` | 분노, 당황, 강한 반응 | 빠른 출력, 단어 강조 |
| `whisper` | 속삭임, 비밀, 약한 목소리 | 느린 출력, 조용한 느낌 |
| `cold` | 냉정함, 위압감, 무감정 | 일정한 출력, 효과 최소 |

`anim`은 줄 단위의 말맛을 정하는 태그다. 모든 줄에 화려한 움직임을 넣기 위한 태그가 아니다.

### Feature And Affection

기능 실행이나 호감도 보상이 이미 필요한 흐름에서만 사용한다.

```ink
# feature: Upgrade
# add_aff: 1
# choice_fail
```

규칙:
- `# feature`는 프로젝트에 이미 존재하는 NPC 기능명만 사용한다.
- `# add_aff`는 선택지 결과나 대사 흐름에서 호감도를 지급해야 할 때만 사용한다.
- 선택지 중 성공/실패를 만들 때는 성공 선택지 결과에 `# add_aff: 1`을 넣고, 실패 선택지는 비워두거나 명시적으로 `# choice_fail`을 사용한다.

## Supported Inline Markup

### Pause

```ink
우리는... [pause=0.45]돌아갈 수 없는 곳까지 온 것 같아.
```

권장값:

| Use | Value |
| --- | ---: |
| 짧은 호흡 | `0.2` |
| 생각이 끊기는 느낌 | `0.35` |
| 무거운 침묵 | `0.45`-`0.7` |

남발하지 않는다. 한 줄에 pause는 보통 0-2개면 충분하다.

### Scoped Motion Tags

태그는 단어 또는 짧은 구절에만 건다.

```ink
네가 [shake]배신[/shake]한 거잖아.
잠깐, 너 [tremble]그 몸으로[/tremble] 싸우려고?
자, [punch]춤춰보자고[/punch], 용사!
[wave]크으~~[/wave]. 이게 인생이지...
이제... [float]끝[/float]이구나.
```

권장 사용:

| Tag | Use when | Caution |
| --- | --- | --- |
| `[shake]...[/shake]` | 분노, 충격 단어 | 전체 문장에 걸지 않는다 |
| `[tremble]...[/tremble]` | 공포, 불안, 약해진 목소리 | 진지한 장면에서는 아주 조금만 |
| `[punch]...[/punch]` | 강한 단어, 도발, 외침 | 한 줄에 하나 정도 |
| `[wave]...[/wave]` | 취기, 장난, 능청 | 심각한 장면에는 쓰지 않는다 |
| `[float]...[/float]` | 사라짐, 여운, 몽환 | 짧은 단어에만 |

지원은 되지만 기본 권장하지 않는 태그: `[jitter]`, `[pop]`, `[emphasis]`, `[wobble]`, `[drift]`. 필요할 때만 사용하고, 먼저 위의 5개로 해결한다.

### TMP RichText

Dialogue text is rendered by TextMeshPro, so simple TMP RichText can be used in Ink body text. Use it as writing emphasis, not as a replacement for UI styling.

Allowed by default:

```ink
<b>중요한 말</b>
<i>작게 흘리는 혼잣말</i>
<color=purple>???</color>
<color=red>위험</color>
<size=90%>쉿...</size>
<size=110%>지금</size> 당장 멈춰.
```

Recommended color names:

| Use | Tag |
| --- | --- |
| hidden or uncanny voice | `<color=purple>...</color>` |
| danger, blood, warning | `<color=red>...</color>` |
| weak, quiet, aside | `<color=grey>...</color>` |
| holy, important, bright clue | `<color=yellow>...</color>` |
| cold, system-like line | `<color=cyan>...</color>` |

Size rules:
- Prefer relative sizes: `<size=90%>`, `<size=95%>`, `<size=105%>`, `<size=110%>`.
- Avoid extreme values below `80%` or above `120%` unless the line is a one-off special beat.
- Do not use size changes to make whole paragraphs bigger or smaller. The dialogue UI owns base typography.

Do:

```ink
# anim: whisper
<size=90%>쉿...</size> [pause=0.35]지금은 말하지 마.

# anim: cold
<color=purple>처음부터 알고 있었다.</color>

# anim: angry
그 말, <b>[punch]취소해[/punch]</b>.
```

Do not:

```ink
<color=#A855F7>???</color>
<size=200%>내가 전부 끝내주마!</size>
<b><i><color=red><size=130%>너는 끝이야!</size></color></i></b>
```

RichText safety rules:
- Always close every RichText tag on the same line.
- Do not wrap a RichText tag across a choice, knot, or `-> END`.
- Do not put `#` inside body text unless the generated JSON has been verified.
- Keep nesting shallow. One RichText tag plus one scoped motion tag is usually the maximum.
- If RichText and motion both apply to the same word, put RichText outside the motion tag: `<b>[punch]취소해[/punch]</b>`.
- Do not combine RichText with unsupported HTML/CSS-like styling. TMP RichText is not HTML.

## Rhythm Rules

대사 애니메이션은 화려함이 아니라 말맛을 만드는 도구다.

- 평범한 대사: `normal`, 효과 없음, 문장부호만으로 호흡.
- 진지한 대사: `slow`, 긴 pause, 흔들림 없음.
- 분노한 대사: `angry`, 특정 단어만 `[shake]` 또는 `[punch]`.
- 불안한 대사: `slow` 또는 `normal`, 특정 짧은 단어만 `[tremble]`.
- 속삭임: `whisper`, 흔들림 없음.
- 냉정한 캐릭터: `cold`, pause와 motion을 최소화.

나쁜 예:

```ink
# anim: angry
[shake]너 지금 그걸 말이라고 하는 거야?![/shake]
```

좋은 예:

```ink
# anim: angry
너 지금 그걸 [punch]말[/punch]이라고 하는 거야?
```

## Ink Structure Rules

### Basic File

```ink
// File: NPC_1005_DemonKing_Ending.ink

=== demonking_terminal_death ===
# speaker: 1005
# face: 1005: Sad
# anim: slow
네가... [pause=0.45]다치지 않아서 다행이야.

# anim: whisper
이제 됐어. [pause=0.5]나는 여기까지야.

# anim: slow
미안해, 용사. [pause=0.4]그리고... 고마워.
-> END
```

### Branch File

Start path로 knot을 지정할 수 있도록 ASCII snake_case 이름을 쓴다.

```ink
=== first_encounter ===
# speaker: 1003
# face: 1003: Normal
# anim: slow
나를 찾아온 건 네가 처음이 아니야. [pause=0.25]그리고 마지막도 아닐 거고.
-> END

=== low_affection ===
# speaker: 1003
# face: 1003: Panic
# anim: angry
이제는 [punch]그만 좀 찾아와[/punch]!
-> END
```

### Choices

선택지 텍스트는 짧게 쓰고, 결과 대사는 선택지 아래에 둔다.

```ink
=== merchant_greeting ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
필요한 게 있으면 말해. 오늘 물건은 나쁘지 않아.

* [물건을 보여줘.]
    # feature: Merchant
    좋아. 눈으로 직접 보는 게 빠르지.
    -> END
* [그냥 지나갈게.]
    알겠어. 마음 바뀌면 다시 와.
    -> END
```

호감도 선택:

```ink
=== chloe_choice ===
# speaker: 1003
# face: 1003: Normal
# anim: normal
그래서, 너는 왜 여기까지 온 거야?

* [널 혼자 두기 싫어서.]
    # add_aff: 1
    # face: 1003: Smile
    # anim: slow
    ...그런 말을 아무렇지 않게 하네.
    -> END
* [이길 수 있을 것 같아서.]
    # choice_fail
    # face: 1003: Panic
    # anim: angry
    하, 진짜 너답게 단순하네.
    -> END
```

## Gemini Prompt Template

아래 템플릿을 Gem에 넣거나 작업마다 붙여서 사용한다.

```text
너는 Unity 2D roguelike 프로젝트의 Ink 대사 작성자다.
아래 규칙을 반드시 지켜서 .ink 원문만 작성하거나 수정한다.

[역할]
- 기존 의미와 사건 순서를 보존하면서 대사를 자연스럽게 다듬는다.
- 대사 애니메이션은 화려함이 아니라 말맛을 위한 속도, pause, 단어 강조로만 사용한다.
- 지원되지 않는 태그, face label, anim 값을 invent하지 않는다.
- JSON, C#, Unity scene/prefab 변경 내용은 출력하지 않는다.

[지원 태그]
- speaker: # speaker: 1001 또는 # speaker: ???
- face: # face: npcId: label
- anim: # anim: normal|slow|angry|whisper|cold
- pause: [pause=0.25]
- scoped motion: [shake]...[/shake], [tremble]...[/tremble], [punch]...[/punch], [wave]...[/wave], [float]...[/float]
- TMP RichText: <b>...</b>, <i>...</i>, <color=purple>...</color>, <size=90%>...</size>
- feature/add_aff/choice_fail은 입력에서 요청한 경우에만 사용한다.

[금지]
- 대사 본문에 "NPC이름:" 형태의 speaker prefix 쓰지 않기
- # anim: fear, shock, madness 쓰지 않기
- 제공되지 않은 face label 쓰지 않기
- 전체 문장에 shake/punch 남발하지 않기
- <color=#A855F7>처럼 #이 들어간 TMP hex color 쓰지 않기
- <size=200%>처럼 UI를 깨는 극단적인 크기 쓰지 않기
- 여러 RichText 태그를 과하게 중첩하지 않기
- .json을 손으로 만들지 않기

[프로젝트 정보]
- 대상 파일명:
- 목적:
- 장면/상황:
- start path 또는 knot 목록:
- NPC 목록:
  - id:
  - 이름:
  - 말투:
  - 사용 가능한 face labels:
- 유지해야 할 사건/정보:
- 감정선:
- 기능 태그 필요 여부:
- 호감도 선택지 필요 여부:
- RichText 사용 허용 여부:
- 사용 가능한 색상/강조 규칙:

[출력 형식]
- 먼저 "수정 요약" 3줄 이하.
- 그 다음 .ink 전체 내용을 하나의 fenced code block으로 출력.
- code block 밖에는 추가 대사 후보를 쓰지 않는다.
```

## Task Templates

### Existing Ink Rewrite

```text
다음 Ink를 프로젝트 규칙에 맞게 다듬어줘.
원문의 사건 순서, 선택지 구조, 기능 태그는 유지해.
대사만 자연스럽게 고치고, # anim / [pause] / 짧은 scoped motion을 맥락에 맞게 추가해.
지원되지 않는 face label은 쓰지 말고, 모르면 face 태그를 유지하거나 생략해.

[NPC]
id:
name:
voice:
available_faces:

[Context]

[Existing Ink]
```

### New Boss Intro

```text
보스 첫 조우 Ink를 새로 작성해줘.
분량은 3-6개 대사 블록.
전투 직전이므로 마지막 줄은 짧고 선명하게 끝내.
도발은 가능하지만 설명문처럼 길게 쓰지 마.

[Boss]
id:
name:
voice:
available_faces:

[Scene context]

[Must reveal]

[Must not reveal]
```

### Repeat Encounter Branches

```text
보스 재조우용 branch Ink를 작성해줘.
각 knot은 독립적으로 실행되고 -> END로 끝나야 해.
각 branch는 1-2줄만 사용해.

필요 knot:
- first_encounter
- second_encounter
- low_affection
- normal_affection
- high_affection
- much_time_left
- low_time_left
- low_health
- fallback

[NPC]
id:
name:
voice:
available_faces:

[Relationship context]
```

### Terminal Ending

```text
보스 사망 후 terminal ending Ink를 작성해줘.
톤은 절제, 패배, 오래된 친구 맥락.
화려한 text effect를 피하고 slow/whisper/cold와 pause 위주로 작성해.
분량은 3-5줄.

[NPC]
id:
name:
voice:
available_faces:

[Ending context]

[Required final beat]
```

## Review Checklist

Gemini 출력물을 적용하기 전에 확인한다.

- `.ink`만 출력했는가.
- 모든 knot이 `-> END`로 끝나는가.
- start path로 쓸 knot 이름이 ASCII snake_case인가.
- 대사 본문에 visible speaker prefix가 없는가.
- `# anim` 값이 `normal|slow|angry|whisper|cold` 중 하나인가.
- `# face` label이 실제 available face label인가.
- SpriteLibrary가 없는 NPC에는 `# face`를 쓰지 않았는가.
- `[pause=]` 숫자가 소수이며 닫는 `]`가 있는가.
- scoped motion 태그가 열고 닫히는가.
- motion 태그가 한 줄 전체가 아니라 단어/짧은 구절에만 걸렸는가.
- TMP RichText 태그가 모두 같은 줄에서 닫혔는가.
- TMP hex color처럼 `#`가 들어간 rich text를 쓰지 않았는가.
- `<size=80%>`-`<size=120%>` 밖의 극단적인 크기를 쓰지 않았는가.
- RichText가 전체 문장/문단에 과하게 걸려 있지 않은가.
- 기존 `# feature`, `# add_aff`, 선택지 구조를 실수로 제거하지 않았는가.

## Post-Authoring Pipeline

Gemini 출력 후 작업자는 다음 순서로 처리한다.

1. 새 variant면 `Assets/LeeJunMo/Datas/Inks/AnimatedVariants/`에 명확한 이름으로 `.ink`를 둔다.
2. 기존 원본 Ink는 명시 지시가 없으면 직접 덮어쓰지 않는다.
3. Unity Ink Integration 또는 로컬 Ink compiler로 대응 `.json`을 생성한다.
4. `NPC Customization Hub`에서 NPCData의 `primaryInk` 또는 `bossEncounterInk`를 필요한 JSON으로 명시적으로 연결한다.
5. Hub Validation Report로 unsupported anim, malformed pause, unknown tag, missing face label을 확인한다.
6. Play Mode에서 타이핑 속도, skip behavior, 선택지, 기능 실행, outro handoff를 확인한다.

## Known Pitfalls

- Ink 본문에서 `<color=#A855F7>` 같은 TMP hex color는 `#` 때문에 Ink tag로 잘릴 수 있다. 색이 필요하면 named color를 쓰거나 JSON을 반드시 검증한다.
- RichText는 Dialogue 기본 타이포그래피를 바꾸는 도구가 아니다. 색/크기는 단어 강조, 숨겨진 목소리, 작은 속삭임 같은 짧은 구간에만 사용한다.
- RichText를 여러 겹 중첩하면 typewriter, line height, 가독성 검토가 어려워진다. `<b>[punch]단어[/punch]</b>` 정도의 얕은 조합만 허용한다.
- `# face: 1003: Angry`처럼 감정 이름을 임의로 만들면 SpriteLibrary에 없어서 검증 경고가 난다. 현재 표정 목록을 입력으로 받아라.
- SlimeQueen처럼 SpriteLibraryAsset이 없는 NPC는 face 태그를 쓰지 않는 편이 맞다.
- 진지한 장면에서 `[shake]`를 많이 쓰면 싸구려처럼 보인다. 진지함은 주로 `slow`, pause, 짧은 문장으로 만든다.
- 분노는 전체 문장 흔들림이 아니라 특정 단어 `[punch]` 또는 `[shake]`가 더 낫다.
- Animated variant 파일은 존재만으로 씬에 적용되지 않는다. Unity에서 TextAsset reference를 명시적으로 바꿔야 한다.
