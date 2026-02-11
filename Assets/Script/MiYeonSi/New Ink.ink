// 1. 외부 함수 선언 (필요 시 유지, 여기선 태그 위주로 설명)
EXTERNAL CallFeature(featureKey)

-> start_test

=== start_test ===

# enter : 1001 : center
# 1001 : face : Normal
# speaker : 테스트 NPC
안녕하세요! 지금부터 감정표현 시스템을 테스트해보겠습니다.
머리 위를 잘 지켜봐 주세요.

    // [수정] 선택지 옆에 바로 태그를 붙입니다. 
    // 이렇게 하면 유니티가 '다음 문장'을 읽기 전에 가로챌 수 있습니다.
    * [업그레이드 하기] # feature : upgrade
        성공적으로 업그레이드를 마쳤군! 계속 진행할까?
        -> after_event
    * [그냥 대화하기]
        좋아, 바로 다음 테스트로 넘어가지.
        -> after_event

=== after_event ===

// 하트 테스트
# 1001 : emote : Heart
# add_aff : 2
첫 번째 감정은 'Heart'입니다.
두근거리는 애니메이션이 나오고 있나요?

// 화남 테스트
# 1001 : face : Angry
# 1001 : emote : Angry
두 번째 감정은 'Angry'입니다.

# exit : 1001
테스트를 마칩니다. 안녕히 계세요!

-> END