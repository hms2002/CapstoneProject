# speaker: 1005

# face: 1005: Normal

드디어 왔군. 용사. 후후, 참 느려 빠졌네.

# face: 1005: smile

자, 어때? 네 오랜 소꿉친구가 마왕이 되어있는데.

+ [그게 무슨...! 세뇌라도 당한거냐!]
    
# add_aff: 1
    
    -> boss_positive_reply
+ [... 너 그 옷 진짜 안 어울려...]
    -> boss_neutral_reply

= boss_positive_reply

# face: 1005: Smile

역시 좋은 반응이네~ 뭐, 세뇌 같은 건 아니니까 안심하라고.

뭐, 이쪽도 나름 사정이 있거든. 아직 말해줄 수 없지만.
-> boss_battle_start

= boss_neutral_reply

# face: 1005: sad

하아!?! 그게 무슨 반응이야!?

기껏 열심히 골랐는데 조금은 칭찬하라고!
-> boss_battle_start

= boss_battle_start

# face: 1005: Normal

…어쨌든... 봐주지 않을 거야. 나도 일단은 마왕이니까. 자, 춤춰보자고, 용사!
-> END