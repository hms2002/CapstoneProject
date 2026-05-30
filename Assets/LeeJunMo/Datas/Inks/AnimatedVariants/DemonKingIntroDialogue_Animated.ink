# speaker: 1005

# speaker: ???
# anim: slow
조용하던 공기가... [pause=0.35]갑자기 멈췄다.

# CameraShake: Low
# anim: normal
작은 진동이 발밑에서 올라왔다.

# CameraShake: Middle
# anim: angry
아니, 이건 단순한 흔들림이 아니야.

# CameraShake: High
# anim: slow
[punch]문이 부서지듯 열렸다.[/punch]

-> END
# face: 1005: Normal
# anim: cold
드디어 왔군. 용사. [pause=0.25]후후, 참 [punch]느려 빠졌네[/punch].

# face: 1005: Smile
# anim: slow
자, 어때? 네 오랜 소꿉친구가 [punch]마왕[/punch]이 되어있는데.

+ [그게 무슨...! 세뇌라도 당한거냐!]

# add_aff: 1

    -> boss_positive_reply
+ [... 너 그 옷 진짜 안 어울려...]
    -> boss_neutral_reply

= boss_positive_reply

# face: 1005: Smile
# anim: normal
역시 좋은 반응이네~ [pause=0.2]뭐, 세뇌 같은 건 아니니까 안심하라고.

# anim: slow
뭐, 이쪽도 나름 사정이 있거든. [pause=0.25]아직 말해줄 수 없지만.
-> boss_battle_start

= boss_neutral_reply

# face: 1005: Sad
# anim: angry
[shake]하아!?![/shake] 그게 무슨 반응이야!?

# anim: angry
기껏 열심히 골랐는데 조금은 [punch]칭찬하라고[/punch]!
-> boss_battle_start

= boss_battle_start

# face: 1005: Normal
# anim: cold
…어쨌든... [pause=0.35]봐주지 않을 거야. 나도 일단은 마왕이니까. [pause=0.2]자, [punch]춤춰보자고[/punch], 용사!
-> END
