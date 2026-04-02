// Affection + Upgrade feature integration test
// Recommended setup:
// - Assign this Ink to an NPC that has affection rewards configured
// - The same NPC should also have NPCFeatureController + UpgradeFeature

# speaker: 1001
# face: 1001: Normal
이번 테스트는 호감도 증가와 업그레이드 기능 호출을 한 번에 확인하기 위한 흐름이야.

먼저 호감도를 올린 뒤, 바로 업그레이드 창을 열어볼게.

+ [호감도 +1 후 업그레이드 열기]
    좋아. 먼저 조금만 올려보자.
    # add_aff: 1
    -> open_upgrade

+ [호감도 +5 후 업그레이드 열기]
    이번에는 조금 더 크게 올려볼게.
    # add_aff: 5
    -> open_upgrade

+ [호감도 +20 후 업그레이드 열기]
    보상 구간이 있다면 이번 선택에서 같이 확인할 수 있어.
    # add_aff: 20
    -> open_upgrade

+ [업그레이드만 열기]
    호감도 변화 없이 업그레이드 기능만 확인해보자.
    -> open_upgrade

= open_upgrade
# face: 1001: Smile
이제 업그레이드 기능을 호출할게.

# feature: Upgrade
업그레이드 기능이 정상이라면 여기서 대화가 종료되고 창이 열려야 해.

-> END
