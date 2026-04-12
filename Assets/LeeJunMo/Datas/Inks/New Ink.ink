// Boss encounter dialogue
// One choice raises affection, the other does not.
// Combat starts after the dialogue sequence finishes.

# speaker: 1003
# face: 1003: Normal
드디어 여기까지 올라왔군.

네가 어떤 대답을 내놓는지 보고 나서 직접 시험해 보겠다.

+ [당신을 넘어서기 위해 여기까지 왔다.]
    # add_aff: 1
    -> boss_positive_reply

+ [길을 막고 있으니 쓰러뜨릴 뿐이다.]
    -> boss_neutral_reply

= boss_positive_reply
# face: 1003: Smile
좋다. 그 눈빛은 마음에 드는군.

말뿐이 아니라 실력으로도 증명해 봐라.
-> boss_battle_start

= boss_neutral_reply
# face: 1003: Normal
냉정한 대답이군.

상관없다. 결국 남는 건 승패뿐이니까.
-> boss_battle_start

= boss_battle_start
# face: 1003: Smile
준비해라. 이제 전투를 시작하지.
-> END
