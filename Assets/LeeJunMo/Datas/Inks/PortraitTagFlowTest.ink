// 다중 대화 / 이동 / 감정표현 / 액션 테스트용 Ink
// 사용 전 확인:
// 1. 1001, 1002 NPC가 NPCDatabase에 등록되어 있어야 함
// 2. face 라벨은 각 NPC SpriteLibrary에 존재해야 함 (기본값: Normal, Smile)
// 3. emote 이름은 IconAnimCtr state 이름과 맞아야 함 (예: Heart, Angry)

# enter:1001:left
# face:1001:Normal
# enter:1002:right
# face:1002:Normal
# speaker:1001
좋아, 이건 다중 대화와 초상 태그를 확인하는 테스트야.

# speaker:1002
나는 오른쪽에서 등장했어. 이제 서로 번갈아 말해보자.

# speaker:1001
좋아. 이번엔 네가 가운데로 이동해봐.

# move:1002:center
# speaker:1002
이제 가운데로 이동했어. 이동 태그가 잘 보이면 성공이야.

# emote:1002:Heart
# speaker:1002
하트 감정표현도 같이 띄워볼게.

# face:1001:Smile
# speaker:1001
좋아, 나는 표정을 바꿔볼게.

# emote:1001:Angry
# action:1001:shake
# speaker:1001
그리고 화난 감정표현과 흔들기 액션도 테스트해보자.

# action:1002:jump
# speaker:1002
그럼 나는 점프 액션을 해볼게.

어느 쪽 이동을 더 확인해볼까?

+ [1001을 far_left로 이동]
    # move:1001:far_left
    # speaker:1001
    이제 나는 far_left 위치에 있어.
    -> after_move_choice

+ [1002를 far_right로 이동]
    # move:1002:far_right
    # speaker:1002
    이제 나는 far_right 위치에 있어.
    -> after_move_choice

+ [이동 없이 계속]
    # speaker:1001
    좋아, 바로 퇴장 테스트로 넘어가자.
    -> after_move_choice

= after_move_choice
# speaker:1001
이제 한 명씩 퇴장시켜볼게.

# exit:1002
# speaker:1001
1002는 퇴장했어. 지금 화면에 1001만 남아 있어야 해.

# exit:1001
모든 초상 퇴장 테스트 종료.
-> END
