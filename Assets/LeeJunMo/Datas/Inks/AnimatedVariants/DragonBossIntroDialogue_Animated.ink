// Dragon boss encounter dialogue
// Source: 취룡_대사.xlsx
// Entry knot is selected by DragonDialogueStartKnotSelector.

-> DRAGON_01

= DRAGON_01
# speaker: 2001
# face: 2001: 취_헤벌쭉
# anim: normal
[wave]크으~~[/wave]. 이게 인생이지... [pause=0.2]응? 뭐야, 용사?

# face: 2001: 취_신남
# anim: angry
이 깊은 곳까지 기어들어 온 거야? 나를 찾아온 녀석은 네가 처음도 아니고... 마지막도 아닐 거지만! [punch]딸꾹![/punch] 그래도 기특하니까 한 잔 따라줄까?

+ [나도 한 잔 줘!]
    # add_aff: 1
    -> DRAGON_01_POSITIVE

+ [마왕이나 불러내, 주정뱅이!]
    -> DRAGON_01_NEUTRAL

= DRAGON_01_POSITIVE
# face: 2001: 취_신남
# anim: angry
오오! 술맛을 아는 녀석이구나! 좋아, 맘에 들었어! 그럼 진탕 마시기 전에 [punch]땀부터 좀 빼볼까?![/punch] 덤벼라!
-> END

= DRAGON_01_NEUTRAL
# face: 2001: 취_짜증
# anim: angry
에잉, 팍팍한 녀석! 안주 거리도 안 되겠어! [shake]확 불태워버리고[/shake] 나 혼자 마실 테다! 간다!
-> END
