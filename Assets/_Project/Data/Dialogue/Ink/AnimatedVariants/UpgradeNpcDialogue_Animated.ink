// Upgrade NPC dialogue
// This NPC talks first, then opens the Upgrade feature through NPCFeatureController.

# speaker: 1002
# face: 1002: Normal
# anim: normal
오, 왔어? 오늘은 또 뭘 두들겨 줄까!

# anim: normal
마정석만 두둑하게 챙겨왔다면, 내 망치로 아주 화끈하게 벼려주지!

+ [업그레이드를 부탁한다.]
    # face: 1002: Normal
    # anim: normal
    좋아, 시원시원해서 마음에 드네!
    -> open_upgrade

+ [아직 결정하지 못했다.]
    # face: 1002: Normal
    # anim: normal
    뭐야, 기껏 망치질할 생각에 신나서 화로까지 벌겋게 달궈 놨더니!
    # anim: normal
    ...쳇, 알았어. 맘 정해지면 다시 와.
    -> upgrade_end

= open_upgrade
# face: 1002: Normal
# anim: normal
당장 화로에 불부터 지필 테니까, 뭘 어떻게 뜯어고칠지 똑바로 골라보라고.
# feature: Upgrade
-> END

= upgrade_end
# face: 1002: Normal
# anim: normal
다음엔 사람 기대하게 만들어 놓고 빼기 없기다?
-> END
