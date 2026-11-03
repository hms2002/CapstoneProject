# speaker: 1005
# face: 1005: Normal
# anim: cold
드디어 왔군. 용사. [pause=0.25]후후, 참 [punch]느려 빠졌네[/punch]. # loc:dialogue.demonkingintrodialogue_animated.ce94cdee5f

# face: 1005: Smile
# anim: slow
자, 어때? 네 오랜 소꿉친구가 [punch]마왕[/punch]이 되어있는데. # loc:dialogue.demonkingintrodialogue_animated.1560532b2b

+ [그게 무슨...! 세뇌라도 당한거냐! # loc:dialogue.demonkingintrodialogue_animated.b111fd4809]

# add_aff: 1

    -> boss_positive_reply
+ [... 너 그 옷 진짜 안 어울려... # loc:dialogue.demonkingintrodialogue_animated.d9f43bf549]
    -> boss_neutral_reply

= boss_positive_reply

# face: 1005: Smile
# anim: normal
역시 좋은 반응이네~ [pause=0.2]뭐, 세뇌 같은 건 아니니까 안심하라고. # loc:dialogue.demonkingintrodialogue_animated.a80d35a5fc

# anim: slow
뭐, 이쪽도 나름 사정이 있거든. [pause=0.25]아직 말해줄 수 없지만. # loc:dialogue.demonkingintrodialogue_animated.67e99dc731
-> boss_battle_start

= boss_neutral_reply

# face: 1005: Sad
# anim: angry
[shake]하아!?![/shake] 그게 무슨 반응이야!? # loc:dialogue.demonkingintrodialogue_animated.19b85f0a98

# anim: angry
기껏 열심히 골랐는데 조금은 [punch]칭찬하라고[/punch]! # loc:dialogue.demonkingintrodialogue_animated.2287eb5beb
-> boss_battle_start

= boss_battle_start

# face: 1005: Normal
# anim: cold
…어쨌든... [pause=0.35]봐주지 않을 거야. 나도 일단은 마왕이니까. [pause=0.2]자, [punch]춤춰보자고[/punch], 용사! # loc:dialogue.demonkingintrodialogue_animated.874f5a98a7
-> END

= DEMONKING_LESSON

# speaker: 1005
# face: 1005: Normal
# anim: slow
크흠. …아무튼. # loc:dialogue.demonkingintrodialogue_animated.lesson.clear_throat

# face: 1005: Normal
# anim: cold
용사여! 네 발밑의 그림자조차, 내 허락 없이는 움직이지 못한다! # loc:dialogue.demonkingintrodialogue_animated.lesson.dignified_line

# speaker: 검?
# anim: normal
손을 조금 더 높이. # loc:dialogue.demonkingintrodialogue_animated.lesson.sword_pose

# speaker: 1005
# face: 1005: Surprise
# anim: slow
…이 정도면 됐지? # loc:dialogue.demonkingintrodialogue_animated.lesson.check_pose

+ [멋지다. 정말 마왕 같아. # loc:dialogue.demonkingintrodialogue_animated.lesson.choice_praise]
    # add_aff: 1

    # speaker: 1005
    # face: 1005: Smile
    # anim: normal
    그렇지? …아니, 당연하지. 나는 마왕이니까. # loc:dialogue.demonkingintrodialogue_animated.lesson.praise_reply

    # speaker: 검?
    # anim: normal
    칭찬을 들으니 자세가 흐트러졌는데. # loc:dialogue.demonkingintrodialogue_animated.lesson.sword_tease

    # speaker: 1005
    # face: 1005: Normal
    # anim: cold
    시끄러워. 그래도 용사는 알아보는군. 좋아, 이제 제대로 상대해 주마! # loc:dialogue.demonkingintrodialogue_animated.lesson.praise_end
    -> END

+ [그거 좀 이상한데… # loc:dialogue.demonkingintrodialogue_animated.lesson.choice_weird]
    # choice_fail

    # speaker: 1005
    # face: 1005: Panic
    # anim: angry
    봤지? 이상하잖아! 이 녀석이 시킨 거라니까. # loc:dialogue.demonkingintrodialogue_animated.lesson.weird_reply

    # speaker: 검?
    # anim: normal
    용사가 뭘 모르는 거야. # loc:dialogue.demonkingintrodialogue_animated.lesson.sword_defense

    # speaker: 1005
    # face: 1005: Panic
    # anim: angry
    너는 좀 조용히 해! …됐어, 빨리 싸우자! # loc:dialogue.demonkingintrodialogue_animated.lesson.weird_end
    -> END

= DEMONKING_REPAIR_COST

# speaker: 1005
# face: 1005: Normal
# anim: slow
그러고 보니… 여기까지 오면서 이것저것 망가뜨렸더라. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.intro

# face: 1005: Panic
# anim: angry
복도 벽, 석상, 문짝… 드워프 석공 길드의 수리 견적서가 전부 내 책상으로 왔어. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.estimate

# speaker: 검?
# anim: normal
아아, 감회가 새롭네! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.sword_nostalgic

# anim: normal
예전에는 성문을 부수고 기둥을 뽑아 놓고, “길이 막혔으니 어쩔 수 없었다”고 했지! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.sword_reminder

# speaker: 1005
# face: 1005: Panic
# anim: angry
그 얘기는 왜 하는데! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.protest

# face: 1005: Normal
# anim: slow
…아무튼, 너는 마왕성을 너무 엉망으로 만들지 말아줘. 서류는 내가 처리해야 하니까. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.request

+ [가능한 조심할게. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.choice_careful]
    # add_aff: 1

    # speaker: 1005
    # face: 1005: Smile
    # anim: slow
    그래? 그럼 언젠가는 수리 장부 없이 잠드는 날도 오겠네. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.careful_hope

    # speaker: 검?
    # anim: normal
    두 장만 와도 밤새던 네가 그런 꿈을 다 꾸고. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.sword_hope_tease

    # speaker: 1005
    # face: 1005: Smile
    # anim: cold
    이번에는 기대해도 돼. 그 약속을 지킬 실력부터 증명해 봐! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.careful_end
    -> END

+ [부순 만큼 더 멋지게 고치면 되겠네. # loc:dialogue.demonkingintrodialogue_animated.repair_cost.choice_fancy]
    # choice_fail

    # speaker: 1005
    # face: 1005: Panic
    # anim: angry
    그 “더 멋지게”에 드는 돈이 문제라고! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.fancy_cost

    # speaker: 검?
    # anim: normal
    금박도 두르고, 분수도 세우고, 기둥은 더 화려하게 뽑아 보자고! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.sword_fancy_tease

    # speaker: 1005
    # face: 1005: Normal
    # anim: cold
    너까지 왜 신나는데! 좋아, 이 성의 소중함을 직접 배우게 해주지! # loc:dialogue.demonkingintrodialogue_animated.repair_cost.fancy_end
    -> END
