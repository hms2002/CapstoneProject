// Upgrade NPC dialogue
// This NPC talks first, then opens the Upgrade feature through NPCFeatureController.

# speaker: 1002
# face: 1002: Normal
왔군. 이번에는 어떤 강화를 준비하고 있지?

필요한 재화만 준비되어 있다면 바로 업그레이드를 진행해 줄 수 있다.

+ [업그레이드를 부탁한다.]
    # face: 1002: Smile
    좋아. 지금 바로 업그레이드 경로를 열어 주지.
    -> open_upgrade

+ [아직 결정하지 못했다.]
    # face: 1002: Normal
    서두를 필요는 없다.
    준비가 끝나면 그때 다시 와라.
    -> upgrade_end

= open_upgrade
# face: 1002: Smile
잘 보고 신중하게 고르도록 해라.
# feature: Upgrade
-> END

= upgrade_end
# face: 1002: Normal
다음에는 더 분명한 답을 들려주길 기대하지.
-> END
