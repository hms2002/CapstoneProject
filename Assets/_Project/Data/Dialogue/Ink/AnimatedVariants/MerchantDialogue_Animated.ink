// Merchant NPC dialogue
// This NPC only talks. Shop interaction remains outside the dialogue flow.

# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와. 오늘은 어떤 물건을 찾고 있지?

# anim: normal
이 근처를 도는 자들에게 필요한 건 대체로 준비해 두고 있네.

+ [오늘은 뭐가 있나요?]
    # face: 1001: Normal
    # anim: normal
    급하게 쓸 만한 도구와 전투에 도움 되는 물건들이 있지.
    # anim: cold
    너무 오래 망설이면 다른 누가 먼저 집어 갈지도 모르네.
    -> merchant_end

+ [그냥 둘러보다가 왔어요.]
    # face: 1001: Normal
    # anim: normal
    그럼 천천히 둘러보게.
    # anim: normal
    마음이 바뀌면 언제든 다시 말을 걸어도 좋네.
    -> merchant_end

= merchant_end
# face: 1001: Normal
# anim: normal
필요한 게 생기면 다시 찾아오게.
-> END
