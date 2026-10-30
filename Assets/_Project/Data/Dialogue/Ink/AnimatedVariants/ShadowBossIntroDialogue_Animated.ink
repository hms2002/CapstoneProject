// Boss encounter dialogue
// One choice raises affection, the other does not.
// Combat starts after the dialogue sequence finishes.

# speaker: 1003
# face: 1003: Normal
# anim: slow
...마왕군 소속... 침묵의 간수장... 아, 그림자 정령 클로에예요. ...여긴, 들어오시면 안 돼요. # loc:dialogue.shadowbossintrodialogue_animated.e10650e11c

# anim: whisper
...지금은 아무도 갇혀 있지 않지만요. 그래도... 제가 지켜야 하는 곳이니까요. # loc:dialogue.shadowbossintrodialogue_animated.eb6633da17

+ [간수장님께 출입 허가를 받아야 하나? # loc:dialogue.shadowbossintrodialogue_animated.d409e4c3f2]
    # add_aff: 1
    -> boss_positive_reply

+ [아무도 없는 감옥을 지킬 이유가 있나? # loc:dialogue.shadowbossintrodialogue_animated.47d02ebb8f]
    -> boss_neutral_reply

= boss_positive_reply
# face: 1003: Surprise
# anim: slow
...허가를요? 아... 그런 건, 생각해 본 적 없었어요. # loc:dialogue.shadowbossintrodialogue_animated.89503ca9fb

# face: 1003: Determined
# anim: normal
하지만... 마왕님께서 용사님을 막으라고 하셔서요. 허가해 드릴 수는 없어요. # loc:dialogue.shadowbossintrodialogue_animated.b9996f5b44
-> boss_battle_start

= boss_neutral_reply
# face: 1003: disappointment
# anim: slow
...아무도 없다고, 아무나 들어오셔도 되는 건 아니에요. # loc:dialogue.shadowbossintrodialogue_animated.190cbce2d5

# face: 1003: Determined
# anim: normal
저는... 간수장이니까요. 여기서 비켜 드릴 수는 없어요. # loc:dialogue.shadowbossintrodialogue_animated.ed53b8a839
-> boss_battle_start

= boss_battle_start
# face: 1003: Determined
# anim: whisper
...그러니까, 발밑은 조심해 주세요. 제 그림자는... 어두울수록 깊어지니까요. # loc:dialogue.shadowbossintrodialogue_animated.1c8cf83064
-> END

=== CHLOE_SILENT_WARDEN_PRACTICE ===
# speaker: 1003
# face: 1003: Thinking
# anim: slow
…그러고 보니, 요즘은 간수장답게 위엄을 갖춰 보려고 해요. 그런데… 어떻게 하는 건지 모르겠어요. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.intro

# face: 1003: Normal
# anim: slow
다른 분들은 가만히 있으면 된다고 하시는데… 저는 그냥, 말할 때를 놓친 거였거든요. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.explanation

+ [하고 싶은 말이 있으면 기다릴게. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.choice_wait]
    # add_aff: 1
    -> positive_reply

+ [침묵하는 모습도 멋진데? # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.choice_silence]
    -> neutral_reply

= positive_reply
# face: 1003: ShySmile
# anim: slow
…기다려 주시는 건가요? 그러면… 조금은 말하기 쉬울 것 같아요. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.wait_reply

# face: 1003: Determined
# anim: normal
그럼… [punch]여기서부터는 못 지나가세요[/punch]. …이 말부터 해야 했는데요. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.wait_end
-> END

= neutral_reply
# face: 1003: Thinking
# anim: slow
…그런가요? 그런데, 계속 그러면… 용사님과는 언제 이야기하죠? # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.silence_reply

# face: 1003: Panic
# anim: normal
…아, 슬슬 시작해야 하는데…! 그, 그럼… 준비해 주세요. # loc:dialogue.shadowbossintrodialogue_animated.silent_warden.silence_end
-> END

=== CHLOE_MONTHLY_PRISONER_REPORT ===
# speaker: 1003
# face: 1003: Thinking
# anim: slow
…아, 맞다. 이것 좀 봐 주실래요? 이번 달 보고서인데요. # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.intro

# face: 1003: Normal
# anim: slow
‘이번 달 수감자… 없음.’ 지난달도 똑같아요. …간수장인데, 이래도 괜찮을까요? # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.concern

+ [아무도 가둘 일이 없었다는 거잖아. # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.choice_peace]
    # add_aff: 1
    -> positive_reply

+ [그럼 나라도 가둬 볼래? # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.choice_imprison]
    -> neutral_reply

= positive_reply
# face: 1003: ShySmile
# anim: slow
…그렇게 생각해도 되는 거군요. 그러면… 이번 달도 잘한 거네요. # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.peace_reply

# face: 1003: Determined
# anim: normal
…용사님도 가두진 않을게요. 그래도… 침입자는 막아야 하니까요. …시작할게요. # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.peace_end
-> END

= neutral_reply
# face: 1003: Panic
# anim: normal
…네?! 그, 그러면… 이름부터 적나요? 감방 문을 잠그고… 아, 아직 밖에 계시는데…! # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.imprison_reply

# face: 1003: Determined
# anim: slow
…일단은, 붙잡는 것부터 해 볼게요. …그다음 순서는, 잡고 나서 생각할게요. # loc:dialogue.shadowbossintrodialogue_animated.prisoner_report.imprison_end
-> END
