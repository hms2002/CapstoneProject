// Slime Queen boss encounter dialogue
// Source: Notion "슬라임 대사 스크립트"
// Notion ID 1004 is adapted to project NPC id 3001.

-> SLIME_QUEEN_SPIRI_ACCESS_BAN

= SLIME_QUEEN_SPIRI_ACCESS_BAN
# speaker: 3001
# face: 3001: Angry
# anim: angry
하지만 그보다 이리 와서 들어보도록! 지금 짐에게 아주 중대한 국정 현안이 있다! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.8750439a52

# face: 3001: Angry
# anim: angry
취룡 스피리가 또 짐의 백성을 술안주처럼 우물거려, 기껏 어엿하게 자란 아이를 손바닥만 한 꼬맹이로 되돌려 놓았다! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.ec64e12488

# speaker: 슬라임들
술도 얻어 마셨어요! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.b425152719
몸도 가벼워졌어요! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.ead5d0beb0

# speaker: 3001
# face: 3001: Angry
# anim: angry
그걸 좋아하면 짐은 누구에게 화를 내란 말이냐! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.1db66961d3

+ [술안주 허가제를 만들자. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.382acf40a6]
    # add_aff: 1
    -> MELTA_SPIRI_ACCESS_BAN_APPROVED

+ [스피리에게서 술을 전부 빼앗자! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.e54547855a]
    -> MELTA_SPIRI_ACCESS_BAN_REJECTED

= MELTA_SPIRI_ACCESS_BAN_APPROVED
# speaker: 3001
# face: 3001: Smile
# anim: normal
호오… 술안주 허가제라. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.0234737204

# face: 3001: Veryjoy
# anim: normal
백성의 선택도, 여왕의 관리 책임도 지키는 훌륭한 슬권 행정이구나! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.0b27058d3d
-> MELTA_SPIRI_ACCESS_BAN_END

= MELTA_SPIRI_ACCESS_BAN_REJECTED
# speaker: 3001
# face: 3001: Angry
# anim: angry
무, 무슨 소리냐! 그럼 스피리가 깨어나면 어쩌려고! 그거야말로 마왕성의 종말이다! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.b233f9ae6b

# face: 3001: Funny
# anim: normal
…짐은 방금 아무 말도 하지 않았다. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.a6ed2b7bb8
-> MELTA_SPIRI_ACCESS_BAN_END

= MELTA_SPIRI_ACCESS_BAN_END
# speaker: 3001
# face: 3001: Smile
# anim: normal
좋다. 할 일이 많군. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.2b0dceac49

# face: 3001: Funny
# anim: normal
우선 네놈부터 처리하고, 그다음 스피리 문제도 정리하겠노라! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.d2cd8de590
-> END

= SLIME_QUEEN_01
# speaker: 3001
# face: 3001: Idle
# anim: normal
호오? # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.b4e2b1e17c
네가 그 소문의 용사로구나. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.c8dd3ca62e

# face: 3001: Angry
# anim: angry
# CameraShake: Low
<size=120%>[punch]잠깐.[/punch]</size> 거기서 멈추거라! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.a36b8bef01

# face: 3001: Angry
# anim: angry
이 몸의 수로에 발을 들이기 전에 먼저 신발부터 닦는 것이 예의 아니더냐? # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.e977fa0fd2

# face: 3001: Angry
# anim: angry
그 흙 묻은 신발로 한 발짝만 더 들어오면, 짐의 백성들이 세 시간 동안 광낸 바닥이 망가지느니라. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.19d5f1996b

# face: 3001: joy
# anim: slow
[float]보아라.[/float] # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.9f003cf99c
이 투명한 물길을. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.0b624fa992

# face: 3001: Veryjoy
# anim: normal
<size=110%>[float]보아라.[/float] 이 티끌 하나 없는 타일을.</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.40cead633a

# face: 3001: VeryVeryjoy
# anim: normal
<size=120%>[wave]이것이 바로 슬라임 왕국의 긍지.[/wave]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.473f22cff4

# face: 3001: Funny
# anim: normal
<size=120%>[wave]마왕성 지하 수로의 진정한 아름다움이니라![/wave]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.e7c7f7b515

# face: 3001: Idle
# anim: normal
그리고 짐은 그 모든 [slowshake][rand_size=98,106]말캉한[/rand_size][/slowshake] 백성들의 군주. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.0bea8b5587

# face: 3001: Smile
# anim: normal
<size=120%>위대한 슬라임들의 여왕, 멜타다!</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.1ebfa9cf34

+ [정말 훌륭한 수로군. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.d2e351a22b]
    # add_aff: 1

    # face: 3001: Veryjoy
    # anim: normal
    <size=120%>[wave]오호호호호![/wave]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.460aaf1b08

    # face: 3001: VeryVeryjoy
    # anim: normal
    훌륭한 수로라. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.fdcc3af23e

    # face: 3001: VeryVeryjoy
    # anim: normal
    그래, 아주 정확한 평가로구나! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.41fc99728b

    # face: 3001: Smile
    # anim: normal
    짐의 백성들이 들으면 기뻐하겠군. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.0371118953

    # face: 3001: Smile
    # anim: whisper
    아니, 이미 듣고 있겠지. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.159eceed1a

    # face: 3001: Funny
    # anim: normal
    다들 숨어서 보고 있으니 말이다. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.afbc4e56cf

    # face: 3001: Idle
    # anim: normal
    좋아. 그대에게는 특별히 품격 있는 패배를 허락하마! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.af38258c94

    -> SLIME_QUEEN_01_END

+ [슬라임 치고는 꽤 깨끗하네. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.cf350a1bdf]
    # face: 3001: Angry
    # anim: angry
    [tremble]...슬라임 치고는?[/tremble] # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.5d7a349489

    # face: 3001: Angry
    # anim: angry
    지금 네놈, [pause=0.2] 짐의 백성들을 [tremble]얕본 것이냐?[/tremble] # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.480081097b

    # face: 3001: Angry
    # anim: angry
    <size=120%>슬라임은 샴푸도, 젤리도, 연금술 재료도 아니다!</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.835f9e4f04

    # face: 3001: Angry
    # anim: angry
    움직이고, 생각하고, # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.1a2dbbe798

    # face: 3001: Angry
    # anim: angry
    근무 교대표까지 작성하는 당당한 백성이니라! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.4da8631afa

    # face: 3001: Smile
    # anim: normal
    좋아. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.8f8d6ed284

    # face: 3001: Smile
    # anim: cold
    네놈은 특별히 예절 교육부터 받아야겠구나. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.3a62cfc8bb

    -> SLIME_QUEEN_01_END

= SLIME_QUEEN_01_END
# face: 3001: Smile
# anim: normal
마왕님께서 네놈을 적당히 봐주라 명하셨으니, 짐도 그 뜻은 따르겠다. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.8d8839faf3

# face: 3001: Smile
# anim: cold
허나 이곳은 짐의 왕국. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.565efc5a1c

# face: 3001: Smile
# anim: cold
여왕의 앞을 지나가려면, 그만한 자격을 보이거라. # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.da2fc1f539

# face: 3001: Idle
# anim: normal
자, 나의 귀여운 [slowshake][rand_size=98,106]백성들이여[/rand_size][/slowshake]! # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.2c7189b080

# face: 3001: Smile
# anim: normal
<size=110%>[punch]줄을 맞춰라![/punch]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.cd2fd4ed1d

# face: 3001: joy
# anim: normal
<size=120%>[wave]대형은 넓게![/wave]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.41b47483b7

# face: 3001: Funny
# anim: angry
# CameraShake: Middle
<size=130%>[punch]무례한 용사를 말캉하게 짓눌러라!![/punch]</size> # loc:dialogue.slimequeenintrodialogue_meltanotion_animated.e087a0eea0

-> END
