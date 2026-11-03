// Merchant NPC dialogue
// This NPC only talks. Shop interaction remains outside the dialogue flow.

-> affection_0_a

=== affection_0_a ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와, 용사님! 오늘도 반짝반짝한 마정석은 두둑하게 챙겨 왔지? # loc:dialogue.merchant.001

# anim: normal
무기부터 유물까지 없는 것 빼고 다 있어! 어디서 구했는지는 묻지 말고, 헤헤! # loc:dialogue.merchantdialogue_animated.95e636eb88

+ [오늘은 뭘 파는데? # loc:dialogue.merchant.003]
    # add_aff: 1
    # face: 1001: Happy
    # anim: normal
    오오, 역시 보는 눈이 있네! 전부 마왕성에서 엄선한 귀한 물건들이야! # loc:dialogue.merchantdialogue_animated.470dbb5cba
    # anim: normal
    조금 전까지 누구 물건이었는지는 중요하지 않잖아? 지금은 내 거니까! # loc:dialogue.merchantdialogue_animated.5526e51532
    -> merchant_interest_end

+ [그냥 구경만 할게. # loc:dialogue.merchant.006]
    # face: 1001: Surprised
    # choice_fail
    # anim: normal
    뭐어?! 진짜 그냥 가게?! 자, 잠깐만! 구경은 공짜니까 조금만 더 보고 가! # loc:dialogue.merchantdialogue_animated.ca8d0de524
    # anim: normal
    보다 보면 분명 사고 싶어질 거라구! # loc:dialogue.merchantdialogue_animated.9098a11458
    -> merchant_browse_end

= merchant_interest_end
-> END

= merchant_browse_end
-> END

=== affection_0_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와, 용사님! 오늘은 그 술주정뱅이 용 옆에서 골라 왔어. 반짝이는 건 많더라! # loc:dialogue.merchant.run.affection_0_b.0
다들 왜 그렇게 무서워하지? 취해서 자는 동안 살짝 가져오면 되는데. 안 깨우면 되는 거잖아? # loc:dialogue.merchant.run.affection_0_b.5
+ [너, 들키면 어쩌려고? # loc:dialogue.merchant.run.affection_0_b.1]
    # face: 1001: Happy
    # add_aff: 1
    헤헤, 내 걱정이야? 괜찮아, 도망갈 길도 다 봐 뒀어! 오래 장사하려면 그 정도는 해야지. # loc:dialogue.merchant.run.affection_0_b.2
    -> END
+ [정말 눈치 못 챈 걸까? # loc:dialogue.merchant.run.affection_0_b.3]
    # face: 1001: Surprised
    # choice_fail
    ……알면서 그냥 뒀다고? 왜? ……아무튼, 용사님이 사면 이제 용사님 물건이야! # loc:dialogue.merchant.run.affection_0_b.4
    -> END

=== affection_0_c ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
용사님이 정문에서 소란을 피우면 나는 뒷문으로 나오지. 좋은 분업 아니야? # loc:dialogue.merchant.run.affection_0_c.0
+ [너도 무사히 나와야지. # loc:dialogue.merchant.run.affection_0_c.1]
    # add_aff: 1
    내 걱정까지? 용사님, 거래 조건이 너무 좋은데? # loc:dialogue.merchant.run.affection_0_c.2
    -> END
+ [오늘은 물건 안 살 건데. # loc:dialogue.merchant.run.affection_0_c.3]
    # face: 1001: Surprised
    # choice_fail
    잠깐! 그러면 분업의 마지막 단계가 빠지잖아! # loc:dialogue.merchant.run.affection_0_c.4
    -> END

=== affection_1_a ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
또 만났네! 이번에도 내 가게부터 찾아온 거야? # loc:dialogue.merchant.run.affection_1_a.0
+ [네 추천을 들어 보고 싶어서. # loc:dialogue.merchant.run.affection_1_a.1]
    # add_aff: 1
    헤헤, 그럼 오늘은 더 꼼꼼하게 골라 줘야겠네! # loc:dialogue.merchant.run.affection_1_a.2
    -> END
+ [그냥 길에 있길래. # loc:dialogue.merchant.run.affection_1_a.3]
    # face: 1001: Surprised
    # choice_fail
    길에 있는 가게도 손님을 기다리는 마음은 있다고! # loc:dialogue.merchant.run.affection_1_a.4
    -> END

=== affection_1_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
장사 끝나면 뭘 하냐고? 다음에 팔 물건을 찾지! # loc:dialogue.merchant.run.affection_1_b.0
+ [가끔은 쉬어도 괜찮아. # loc:dialogue.merchant.run.affection_1_b.1]
    # add_aff: 1
    어? 내 걱정을 다 해 주네. 오늘은 조금 일찍 쉴까? # loc:dialogue.merchant.run.affection_1_b.2
    -> END
+ [그럼 더 싸게 팔 수도 있겠네. # loc:dialogue.merchant.run.affection_1_b.3]
    # face: 1001: Surprised
    # choice_fail
    지금 쉬는 얘기가 아니라 흥정하는 거였어?! # loc:dialogue.merchant.run.affection_1_b.4
    -> END

=== affection_1_c ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와, 용사님! 왜 이렇게 싸냐고? 헤헤, 내 장사엔 다 계산이 있지! # loc:dialogue.merchant.run.affection_1_c.0
매입비는 안 들었지, 용사님이 시선도 끌어 줬지, 싸게 팔면 또 찾아올 테지! 그러니까 이 물건은 깎아 줘도 남는 장사야. 헤헤, 빈틈없지? # loc:dialogue.merchant.run.affection_1_c.1
+ [그걸 나한테 설명해도 돼? # loc:dialogue.merchant.run.affection_1_c.2]
    # face: 1001: Happy
    # add_aff: 1
    왜 안 돼? 알아도 살 거잖아. 내가 골랐고, 가격까지 깎아 줬는데! # loc:dialogue.merchant.run.affection_1_c.3
    -> END
+ [그래도 안 살 건데. # loc:dialogue.merchant.run.affection_1_c.4]
    # face: 1001: Surprised
    # choice_fail
    왜?! 가격도 깎았고, 설명도 공짜였는데?! ……설명을 더 해야 하나? # loc:dialogue.merchant.run.affection_1_c.5
    -> END

=== affection_2_a ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
용사님 몫은 따로 챙겨 뒀어. 특별 대우라고! # loc:dialogue.merchant.run.affection_2_a.0
+ [기억해 줘서 고마워. # loc:dialogue.merchant.run.affection_2_a.1]
    # add_aff: 1
    뭘 그런 걸로! 다음에도 무사히 와. 물건은 내가 챙길게. # loc:dialogue.merchant.run.affection_2_a.2
    -> END
+ [다른 손님한테도 그러겠지. # loc:dialogue.merchant.run.affection_2_a.3]
    # face: 1001: Surprised
    # choice_fail
    아무한테나 이렇게 신경 쓰는 건 아니거든! # loc:dialogue.merchant.run.affection_2_a.4
    -> END

=== affection_2_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
이상하지? 용사님 발소리는 멀리서도 알겠더라. # loc:dialogue.merchant.run.affection_2_b.0
+ [나도 네 목소리가 들리면 안심돼. # loc:dialogue.merchant.run.affection_2_b.1]
    # add_aff: 1
    그, 그래? 그럼 다음에도 크게 불러 줄게. 어서 오라고! # loc:dialogue.merchant.run.affection_2_b.2
    -> END
+ [동전 소리만 듣는 줄 알았는데. # loc:dialogue.merchant.run.affection_2_b.3]
    # face: 1001: Surprised
    # choice_fail
    동전 소리랑 손님 발소리는 구별하거든! # loc:dialogue.merchant.run.affection_2_b.4
    -> END

=== greeting_a ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와! 구경은 천천히, 결정은 확실하게! # loc:dialogue.merchant.run.greeting_a
-> END

=== greeting_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
다치지 말고 돌아와. 다음 장사도 해야 하니까! # loc:dialogue.merchant.run.greeting_b
-> END

=== greeting_c ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
오늘도 좋은 물건들이야. 보는 눈은 여전하지? # loc:dialogue.merchant.run.greeting_c
-> END

=== greeting_full_a ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
가방이 꽉 찼잖아! 내 물건 담을 자리부터 만들어 와! # loc:dialogue.merchant.run.greeting_full_a
-> END

=== greeting_full_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
살 마음은 있어도 넣을 자리는 없네. 가방부터 정리해! # loc:dialogue.merchant.run.greeting_full_b
-> END
