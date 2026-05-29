// Boss encounter dialogue
// One choice raises affection, the other does not.
// Combat starts after the dialogue sequence finishes.

# speaker: 1003
# face: 1003: Normal
# anim: slow
...마왕군 소속... [pause=0.25]침묵의 간수장, 그림자 정령 클로에. ...돌아가. 여긴... 너희가 올 곳이 아니야.

# anim: whisper
...가까이 오지 마. 내 그림자에 닿아서 어떻게 돼도... [tremble]난 책임 못 져[/tremble].

+ [떨고 있는 것 같은데. 사실은 싸우고 싶지 않은 거지?]
    # add_aff: 1
    -> boss_positive_reply

+ [수작 부리지 마라! 그깟 그림자 따위 베어버리겠다!]
    -> boss_neutral_reply

= boss_positive_reply
# face: 1003: Panic
# anim: angry
...어?! 으, 응...? 아, 아니야! 나 엄청 [punch]무서운 정령[/punch]이란 말이야! 다들 나만 보면 벌벌 떤다고...!

# anim: slow
...너, 진짜 이상한 인간이야. 내 진심을... [pause=0.3]보통은 무섭다고 다 도망치는데...

# anim: whisper
...그, 그래도 안 싸울 수는 없으니까... [tremble]너무 아프게 공격하진 않을게[/tremble]...
-> boss_battle_start

= boss_neutral_reply
# face: 1003: Normal
# anim: cold
...거봐. 역시 인간들은 다 똑같아. 내 진짜 마음 같은 건... [pause=0.35]처음부터 보려고도 안 하잖아.

# anim: angry
...맨날 자기들 멋대로 오해하고... 이젠 나도 몰라. [punch]다치든 말든[/punch] 마음대로 해.
-> boss_battle_start

= boss_battle_start
# face: 1003: Surprise
# anim: slow
…많이 다쳐도 난 책임 못 져. 진짜로… [tremble]아플 텐데[/tremble]...
-> END
