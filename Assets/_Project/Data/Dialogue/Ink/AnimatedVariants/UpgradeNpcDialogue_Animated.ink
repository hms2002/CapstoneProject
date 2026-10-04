// Upgrade NPC dialogue
// This NPC talks first, then opens the Upgrade feature through NPCFeatureController.

# speaker: 1002
# face: 1002: Normal
# anim: normal
오, 왔어? 오늘은 또 뭘 두들겨 줄까! # loc:dialogue.upgradenpcdialogue_animated.8c59ab62c0

# anim: normal
마정석만 두둑하게 챙겨왔다면, 내 망치로 아주 화끈하게 벼려주지! # loc:dialogue.upgradenpcdialogue_animated.e151341527

+ [업그레이드를 부탁한다. # loc:dialogue.upgradenpcdialogue_animated.47e92c2bb8]
    # face: 1002: Normal
    # anim: normal
    좋아, 시원시원해서 마음에 드네! # loc:dialogue.upgradenpcdialogue_animated.d8549c651d
    -> open_upgrade

+ [아직 결정하지 못했다. # loc:dialogue.upgradenpcdialogue_animated.6c637c557b]
    # face: 1002: Normal
    # anim: normal
    뭐야, 기껏 망치질할 생각에 신나서 화로까지 벌겋게 달궈 놨더니! # loc:dialogue.upgradenpcdialogue_animated.a965c6c78e
    # anim: normal
    ...쳇, 알았어. 맘 정해지면 다시 와. 다음엔 사람 기대하게 만들어 놓고 빼기 없기다? # loc:dialogue.upgradenpcdialogue_animated.9a3b9209a7
    -> upgrade_end

= open_upgrade
# face: 1002: Normal
# anim: normal
당장 화로에 불부터 지필 테니까, 뭘 어떻게 뜯어고칠지 똑바로 골라보라고. # loc:dialogue.upgradenpcdialogue_animated.969b2dabeb
# feature: Upgrade
-> END

= upgrade_end
-> END
