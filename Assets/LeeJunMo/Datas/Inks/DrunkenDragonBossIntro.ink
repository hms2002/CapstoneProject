// Drunken Dragon boss encounter dialogue
// Source: 취룡_대사.xlsx
// Entry knot is selected by DrunkenDragonDialogueStartKnotSelector.

-> DRUNK_01

= DRUNK_01
# speaker: 2001
# face: 2001: 취_헤벌쭉
크으~~. 이게 인생이지... 응? 뭐야, 용사?

# face: 2001: 취_신남
이 깊은 곳까지 기어들어 온 거야? 나를 찾아온 녀석은 네가 처음도 아니고... 마지막도 아닐 거지만! 딸꾹! 그래도 기특하니까 한 잔 따라줄까?

+ [나도 한 잔 줘!]
    # add_aff: 1
    -> DRUNK_01_POSITIVE

+ [마왕이나 불러내, 주정뱅이!]
    -> DRUNK_01_NEUTRAL

= DRUNK_01_POSITIVE
# face: 2001: 취_신남
오오! 술맛을 아는 녀석이구나! 좋아, 맘에 들었어! 그럼 진탕 마시기 전에 땀부터 좀 빼볼까?! 덤벼라!
-> END

= DRUNK_01_NEUTRAL
# face: 2001: 취_짜증
에잉, 팍팍한 녀석! 안주 거리도 안 되겠어! 확 불태워버리고 나 혼자 마실 테다! 간다!
-> END