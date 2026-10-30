// Dragon boss encounter dialogue
// Source: Notion "취룡 대사 - 작성 중"
// Entry knot is selected by DragonDialogueStartKnotSelector.

-> DRAGON_01

= DRAGON_01
# speaker: 2001
# face: 2001: Spiri_Dragon
# anim: cold
...네가 용사인가. 나는 스피리다. 붉은 재앙의 용... [pause=0.25]스피리. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.9d356ac5a4

# face: 2001: Spiri_Dragon
# anim: slow
이유를 모르겠군. 네놈에게선... [pause=0.35]아무 냄새도 나지 않아. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.4adb1425bb

# face: 2001: Spiri_Dragon
# anim: cold
공포도. 살의도. 생존 본능조차. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.2a68c617ea

# face: 2001: Spiri_Dragon
# anim: slow
...그 정도로 약한 건가? [pause=0.25]아니면 이미 각오를 끝낸 건가. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.6e65686948

# face: 2001: Spiri_Dragon
# anim: slow
... # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.6eae3a5b06

# face: 2001: Spiri_Dragon
# anim: slow
...미안하지만 잠시 기다려주겠나. 지금의 나는... [pause=0.35]그다지 바람직한 상태가 아니라서. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.07de708c80

# face: 2001: Spiri_Drink
# anim: slow
# CameraShake: Low
(<size=110%>벌컥</size>) # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.77b65f9777

# face: 2001: Spiri_Drink
# anim: slow
# CameraShake: Middle
(<size=115%>벌컥</size>) # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.3c6d679039

# face: 2001: Spiri_Drink
# anim: angry
# CameraShake: High
(<size=110%>벌컥</size><size=115%>벌컥</size><size=120%>벌컥벌컥--!!</size>) # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.77bb4c842f

# face: 2001: Spiri_DrunkShout
# anim: angry
[slowshake][rand_size=95,112]푸하아아아아아----[/rand_size][/slowshake]!!! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.749045a08e

# face: 2001: Spiri_DrunkShout
# anim: normal
[slowshake][rand_size=95,110]크으으으으~[/rand_size][/slowshake]! 역시 인간들의 [rand_size=95,108]술이[/rand_size] 최고라니까아~! [rand_size=95,106]살 것[/rand_size] 같네에에에~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.8cbae641ef

# face: 2001: Spiri_Idle
# anim: normal
오오~ 용사! [slowshake][rand_size=95,110]아직 안 갔네에에[/rand_size][/slowshake]~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.d1284e8056

# face: 2001: Spiri_DrunkRelax
# anim: normal
자자, 그렇게 굳어 있지 말고~ [pause=0.2][rand_size=95,108]한 잔[/rand_size] 받아라! 싸움은 [slowshake]취해서[/slowshake] 해야 제맛이라고? # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.d1cd9d5f8e

+ [방금 그건 대체 뭐였지...? # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.b4231187ec]
    -> DRAGON_01_CONFUSED

+ [좋아. 술자리라면 사양 안 하지! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.d0e30cc584]
    # add_aff: 1
    -> DRAGON_01_DRINKING_PARTNER

= DRAGON_01_CONFUSED
# face: 2001: Spiri_DrunkRelax
# anim: normal
방금~? 으히히... [pause=0.25][slowshake][rand_size=95,108]글쎄에[/rand_size][/slowshake]~? # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.efe9839691

# face: 2001: Spiri_Smile
# anim: normal
못 본 걸로 해주라~ [pause=0.2]나도 [rand_size=95,105]기억하기[/rand_size] 싫거든. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.9a75233460

-> DRAGON_01_COMMON

= DRAGON_01_DRINKING_PARTNER
# face: 2001: Spiri_DrunkShout
# anim: angry
오오오~!! 분위기 탈 줄 아는 [slowshake][rand_size=95,112]녀석이잖냐[/rand_size][/slowshake]~!! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.e9518c12c0

# face: 2001: Spiri_DrunkShout
# anim: normal
좋다 좋아! 오늘은 특별히 [rand_size=95,108]용사 환영주[/rand_size]다~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.76ba5bd81d

-> DRAGON_02_COMMON

= DRAGON_01_COMMON
# face: 2001: Spiri_Idle
# anim: normal
자, 몸도 [rand_size=95,108]달아올랐고[/rand_size] 슬슬 [slowshake]시작해볼까아[/slowshake]~ # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.9ee2fcbd71

-> END

= DRAGON_02_COMMON
# face: 2001: Spiri_DrunkRelax
# anim: whisper
어라. [pause=0.25]손이 왜 [slowshake][rand_size=95,108]세 개로[/rand_size][/slowshake] 보이지. 아무튼 간드아아아~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.4e56cae1a6

-> END

= SPIRI_SLIME_SNACK
# speaker: 2001
# face: 2001: Spiri_Idle
# anim: normal
그러고 보니, 멜타가 또 화났어~. 슬라임 몇 마리를 안주처럼 씹었다고! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_intro

# speaker: 슬라임들
“술 맛 나!”, “몸이 가벼워!” # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_slime_taste

# speaker: 2001
# face: 2001: Spiri_DrunkShout
# anim: normal
나는 살짝 쫀득한지 확인만 했어! 봐봐, 애들도 좋아하잖아~. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_defense

+ [슬라임은 안주가 아니야. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_choice_not_snack]
    # face: 2001: Spiri_DrunkRelax
    # anim: whisper
    안다고오~! 내가 조절하고 있잖아. 봐, 얼마나 멀쩡해! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_negative_insists

    # face: 2001: Spiri_DrunkShout
    # anim: angry
    내가 얼마나 제정신인지 보여줄게! 이리 와~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_negative_end
    -> END

+ [다음엔 내가 진짜 안주를 가져다줄게. # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_choice_real_snack]
    # add_aff: 1
    # face: 2001: Spiri_DrunkShout
    # anim: normal
    진짜로? 와아, 그거 좋다아! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_positive_delight

    # face: 2001: Spiri_Smile
    # anim: angry
    좋아! 그럼 얼른 널 쓰러뜨려야, 다음엔 안주 들고 다시 찾아오겠네~! 간다아아아! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.slime_snack_positive_end
    -> END

= SPIRI_EMPTY_KEG_ERRAND
# speaker: 2001
# face: 2001: Spiri_Idle
# anim: normal
아, 맞다. 마왕님 심부름 중이야~. 빈 술통은 전부 치우라고 하셨거든! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_intro

# face: 2001: Spiri_DrunkRelax
# anim: whisper
원래는 널브러진 채로 뒀겠지만~. 마왕님 부탁이니까! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_usual

# face: 2001: Spiri_DrunkShout
# anim: normal
근데 비어야 치우지! 그러니까 같이 비우자아~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_empty_first

+ [빈 술통을 만들라는 뜻은 아니잖아? # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_choice_reason]
    # face: 2001: Spiri_DrunkShout
    # anim: angry
    그럴 리가 없잖아! 내가 맞아! 벌컥벌컥— 으읏, 몸이 휘청휘청…! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_negative_right

    # face: 2001: Spiri_DrunkShout
    # anim: angry
    봐아아아았지~ 아직 괜찮아아~! 그러니까 너도 한 잔 받아! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_negative_end
    -> END

+ [함께 빈 술통을 만들자! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_choice_together]
    # add_aff: 1
    # face: 2001: Spiri_DrunkShout
    # anim: normal
    오오! 역시 용사! 자, 받아라아! 빈 술통 만들기 1호다아~! # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_positive_hero

    (스피리가 맥주통을 던졌다.) # loc:dialogue.dragonbossintrodialogue_spirinotion_animated.empty_keg_throw
    -> END
