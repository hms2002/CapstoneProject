// AffectionManager / RewardDisplay / Choice 흐름 테스트용 Ink
// 사용 권장:
// - affectionRewards가 설정된 NPCData에 연결
// - speaker id는 필요 시 인스펙터에 맞게 수정

# speaker: 1001
# face: 1001: Normal
호감도, 선택지, 보상 지급 흐름을 한 번에 확인하는 테스트 대화야.

어떤 테스트를 해볼까?

+ [호감도 +1]
    좋아. 먼저 작은 수치부터 올려볼게.
    # add_aff: 1
    -> after_first_test

+ [호감도 +5]
    이번에는 조금 크게 올려볼게.
    # add_aff: 5
    -> after_first_test

+ [호감도 +20]
    보상 구간을 한 번에 넘기는 상황도 확인해보자.
    # add_aff: 20
    -> after_first_test

+ [아무 것도 하지 않기]
    이번엔 호감도 변화 없이 선택지만 확인할게.
    -> after_first_test

= after_first_test
# face: 1001: Smile
첫 번째 테스트는 끝났어.

한 번 더 진행할까?

+ [추가로 +1]
    마지막으로 한 번만 더 올려볼게.
    # add_aff: 1
    -> finish

+ [대화 종료]
    여기서 마무리하자.
    -> finish

= finish
# face: 1001: Normal
테스트 종료. 값 저장, UI 애니메이션, 보상 지급이 정상인지 확인해줘.
-> END
