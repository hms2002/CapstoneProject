# speaker : 1001
어서 오게나! 마정석은 좀 모아 왔는가? # face: 1001: Smile

무엇을 도와줄까?

+ [업그레이드를 하고 싶습니다.]
    좋아, 자네의 능력을 한 단계 끌어올려 주지!
    // 아래 태그는 유저님의 DialogueManager에서 파싱하여 UpgradeManager.Instance.ToggleUI()를 호출하도록 연결해주세요!
    # feature : Upgrade 
    -> DONE

+ [그냥 인사하러 왔습니다.]
    허허, 싱거운 녀석. 언제든 마정석이 모이면 다시 찾아오게. # face: 1001: Normal
    -> DONE

+ [상점(유물/무기)을 보고 싶습니다.]
    // 나중에 상점 UI 테스트용으로 쓰실 수 있게 남겨둡니다.
    그건 아직 준비 중이라네. 조금만 기다려 주게나.
    -> DONE