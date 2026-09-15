// Merchant NPC dialogue
// This NPC only talks. Shop interaction remains outside the dialogue flow.

# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와, 용사님! 오늘도 반짝반짝한 마정석은 두둑하게 챙겨 왔지?

# anim: normal
무기부터 유물까지 없는 것 빼고 다 있어! 어디서 구했는지는 묻지 말고, 헤헤!

+ [오늘은 뭘 파는데?]
    # face: 1001: Normal
    # anim: normal
    오오, 역시 보는 눈이 있네! 전부 마왕성에서 엄선한 귀한 물건들이야! 조금 전까지 누구 물건이었는지는 중요하지 않잖아? 지금은 내 거니까!
    -> merchant_interest_end

+ [그냥 구경만 할게.]
    # face: 1001: Normal
    # anim: normal
    뭐어?! 진짜 그냥 가게?! 자, 잠깐만! 구경은 공짜니까 조금만 더 보고 가!
    # anim: normal
    보다 보면 분명 사고 싶어질 거라구! 진짜 아무것도 안 사고 가는 거야?! 다음엔 지갑부터 두둑하게 채워서 와야 한다, 용사님!
    -> merchant_browse_end

= merchant_interest_end
# face: 1001: Normal
# anim: normal
좋아, 천천히 골라봐! 고민하는 동안 가격이 오를지도 모르니까 서두르는 게 좋을걸?
-> END

= merchant_browse_end
-> END
