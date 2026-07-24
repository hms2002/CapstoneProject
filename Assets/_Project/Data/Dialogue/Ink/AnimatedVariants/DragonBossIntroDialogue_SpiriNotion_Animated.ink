// Dragon boss encounter dialogue
// Source: Notion "취룡 대사 - 작성 중"
// Entry knot is selected by DragonDialogueStartKnotSelector.

-> DRAGON_01

= DRAGON_01
# speaker: 2001
# face: 2001: Spiri_Dragon
# anim: cold
...네가 용사인가.

# face: 2001: Spiri_Dragon
# anim: cold
나는 스피리다. 붉은 재앙의 용... [pause=0.25]스피리.

# face: 2001: Spiri_Dragon
# anim: slow
이유를 모르겠군. 네놈에게선... [pause=0.35]아무 냄새도 나지 않아.

# face: 2001: Spiri_Dragon
# anim: cold
공포도. 살의도. 생존 본능조차.

# face: 2001: Spiri_Dragon
# anim: slow
...그 정도로 약한 건가? [pause=0.25]아니면 이미 각오를 끝낸 건가.

# face: 2001: Spiri_Dragon
# anim: slow
...

# face: 2001: Spiri_Dragon
# anim: slow
...미안하지만 잠시 기다려주겠나.

# face: 2001: Spiri_Dragon
# anim: slow
지금의 나는... [pause=0.35]그다지 바람직한 상태가 아니라서.

# face: 2001: Spiri_Drink
# anim: slow
# CameraShake: Low
(<size=110%>벌컥</size>)

# face: 2001: Spiri_Drink
# anim: slow
# CameraShake: Middle
(<size=115%>벌컥</size>)

# face: 2001: Spiri_Drink
# anim: angry
# CameraShake: High
(<size=110%>벌컥</size><size=115%>벌컥</size><size=120%>벌컥벌컥--!!</size>)

# face: 2001: Spiri_DrunkShout
# anim: angry
[slowshake][rand_size=95,112]푸하아아아아아----[/rand_size][/slowshake]!!!

# face: 2001: Spiri_DrunkShout
# anim: normal
[slowshake][rand_size=95,110]크으으으으~[/rand_size][/slowshake]! 역시 인간들의 [rand_size=95,108]술이[/rand_size] 최고라니까아~! [rand_size=95,106]살 것[/rand_size] 같네에에에~!

# face: 2001: Spiri_Idle
# anim: normal
오오~ 용사! [slowshake][rand_size=95,110]안 갔네에에[/rand_size][/slowshake]~!

# face: 2001: Spiri_DrunkRelax
# anim: normal
자자, 그렇게 굳어 있지 말고~ [pause=0.2][rand_size=95,108]한 잔[/rand_size] 받아라! 싸움은 [slowshake]취해서[/slowshake] 해야 제맛이라고?

+ [방금 그건 대체 뭐였지...?]
    -> DRAGON_01_CONFUSED

+ [좋아. 술자리라면 사양 안 하지!]
    # add_aff: 1
    -> DRAGON_01_DRINKING_PARTNER

= DRAGON_01_CONFUSED
# face: 2001: Spiri_DrunkRelax
# anim: normal
방금~?

# face: 2001: Spiri_DrunkRelax
# anim: whisper
으히히... [pause=0.25][slowshake][rand_size=95,108]글쎄에[/rand_size][/slowshake]~?

# face: 2001: Spiri_Smile
# anim: normal
못 본 걸로 해주라~ [pause=0.2]나도 [rand_size=95,105]기억하기[/rand_size] 싫거든.

-> DRAGON_01_COMMON

= DRAGON_01_DRINKING_PARTNER
# face: 2001: Spiri_DrunkShout
# anim: angry
오오오~!! 분위기 탈 줄 아는 [slowshake][rand_size=95,112]녀석이잖냐[/rand_size][/slowshake]~!!

# face: 2001: Spiri_DrunkShout
# anim: normal
좋다 좋아! 오늘은 특별히 [rand_size=95,108]용사 환영주[/rand_size]다~!

# face: 2001: Spiri_DrunkRelax
# anim: whisper
어라. [pause=0.25]손이 왜 [slowshake][rand_size=95,108]세 개로[/rand_size][/slowshake] 보이지.

-> DRAGON_01_COMMON

= DRAGON_01_COMMON
# face: 2001: Spiri_Idle
# anim: normal
자, 몸도 [rand_size=95,108]달아올랐고[/rand_size] 슬슬 [slowshake]시작해볼까아[/slowshake]~

# face: 2001: Spiri_Idle
# anim: normal
[rand_size=95,106]술 깨기 전에[/rand_size] 끝내야 하거든. [pause=0.2]너무 오래 끌진 말자고.

# face: 2001: Spiri_Smile
# anim: angry
안 그러면, 주변이 좀 [slowshake][rand_size=95,110]위험해져서[/rand_size][/slowshake] 말이야!

-> END
