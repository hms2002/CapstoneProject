window.PROJECT_DOCS = {
  generatedOn: "2026-05-17",
  sourcePolicy: "Markdown 문서가 원본입니다. Presentation HTML은 사람이 빠르게 이해하기 위한 파생 개요판입니다.",
  authorizedMaintainer: "nadoman354",
  project: {
    title: "Unity 2D Top-Down Roguelike Action",
    subtitle: "프로젝트 문서 프레젠테이션",
    description: "이 폴더는 전체 Markdown을 HTML로 변환하지 않습니다. 핵심 문서를 짧게 묶고 시각화해서 사람이 구조를 빠르게 파악하도록 돕습니다.",
    caution: "세부 구현, 로그, 계약, 아키텍처 원문은 Markdown을 확인합니다. HTML은 source of truth가 아닙니다."
  },
  pages: [
    {
      title: "아키텍처 개요",
      href: "architecture-overview.html",
      summary: "전투, 보상, 인벤토리, 씬 저장, 인카운터, 대화, UI, 부트스트랩 구조를 사람이 읽는 설명으로 묶습니다.",
      status: "필수"
    },
    {
      title: "콘텐츠 작성 가이드",
      href: "authoring-guide.html",
      summary: "무기, 아이템, 몹, 보스, Dialogue, 업그레이드, 보상, 상점, Presentation, Audio, UI, Scene 제작 흐름을 핸드북처럼 정리합니다.",
      status: "필수"
    },
    {
      title: "리팩터 보드",
      href: "refactor-board.html",
      summary: "RefactorBacklog의 우선순위, 상태, 시작 조건을 사람이 훑어볼 수 있게 정리합니다.",
      status: "추가"
    }
  ],
  quickLinks: [
    {
      title: "CurrentTask.md",
      path: "../CurrentTask.md",
      group: "작업 범위",
      summary: "현재 작업 목표, 범위, 완료 기준입니다."
    },
    {
      title: "DecisionLog.md",
      path: "../DecisionLog.md",
      group: "결정",
      summary: "장기적으로 유지해야 하는 설계 결정을 기록합니다."
    },
    {
      title: "ErrorLog.md",
      path: "../ErrorLog.md",
      group: "오류 예방",
      summary: "반복되는 실수와 예방 규칙을 기록합니다."
    },
    {
      title: "README.md",
      path: "../README.md",
      group: "라우터",
      summary: "목적별로 어떤 Markdown 문서를 읽을지 안내합니다."
    },
    {
      title: "document-inventory.md",
      path: "../Overview/document-inventory.md",
      group: "문서 목록",
      summary: "프로젝트 문서들의 역할과 위치를 정리합니다."
    },
    {
      title: "RefactorBacklog/README.md",
      path: "../RefactorBacklog/README.md",
      group: "리팩터",
      summary: "리팩터 후보의 우선순위, 상태, 시작 조건을 정리합니다."
    }
  ],
  architectureOverview: {
    overviewDiagram: "flowchart TD\n    Player[플레이어 입력/상태] --> Combat[전투 / 무기 / Ability]\n    Combat --> Reward[루트 / 보상]\n    Reward --> Inventory[인벤토리 / 상자 UI]\n    Encounter[보스 / 몹 인카운터] --> Combat\n    Encounter --> Reward\n    Scene[씬 / 런타임 저장] --> Player\n    Scene --> Encounter\n    Dialogue[대화 / NPC / 호감도] --> Reward\n    UI[UI / Presentation] --> Player\n    Bootstrap[부트스트랩 / 런타임 소유권] --> Scene\n    Bootstrap --> UI",
    detailDiagrams: [
      {
        title: "Weapon / GAS 소유권",
        summary: "무기 상태는 inventory-owned RuntimeData가 소유하고, GAS는 선택된 실행을 담당합니다.",
        diagram: "classDiagram\n    class WeaponDefinition\n    class WeaponAbilityLoadout\n    class WeaponAbilitySelectionStrategy\n    class WeaponRuntimeData\n    class WeaponRuntimeProcessor\n    class WeaponRuntimeCoordinator\n    class WeaponAbilityExecutor\n    class AbilitySystem\n    WeaponDefinition --> WeaponAbilityLoadout\n    WeaponAbilityLoadout --> WeaponAbilitySelectionStrategy\n    WeaponRuntimeCoordinator --> WeaponRuntimeData\n    WeaponRuntimeCoordinator --> WeaponRuntimeProcessor\n    WeaponAbilitySelectionStrategy --> WeaponRuntimeData\n    WeaponAbilitySelectionStrategy --> WeaponAbilityExecutor\n    WeaponAbilityExecutor --> AbilitySystem",
        sources: [
          { label: "GameplayAbilityWeaponArchitecture.md", path: "../Architecture/GameplayAbilityWeaponArchitecture.md" },
          { label: "WeaponAuthoringPipeline.md", path: "../Guides/ContentAuthoring/WeaponAuthoringPipeline.md" }
        ]
      },
      {
        title: "피해 적용 Sequence",
        summary: "공격/투사체는 직접 HP를 깎지 않고 CombatDamageAction을 통해 GAS와 feedback 경로로 들어갑니다.",
        diagram: "sequenceDiagram\n    participant Hit as Hit Source\n    participant Damage as CombatDamageAction\n    participant GAS as AbilitySystem\n    participant Effect as GameplayEffectRunner\n    participant Gauge as Stagger/Element Gauge\n    participant UI as Feedback/UI\n    Hit->>Damage: damage request\n    Damage->>GAS: apply gameplay effect\n    GAS->>Effect: execute HP/effect changes\n    Damage->>Gauge: build stagger/element\n    Damage->>UI: hit confirmed feedback",
        sources: [
          { label: "CombatArchitecture.md", path: "../Architecture/CombatArchitecture.md" }
        ]
      },
      {
        title: "Boss Battle-End",
        summary: "보스 처치 후 고정 chest/portal은 scene-authored activation이고, magic stone/field heal은 variable-count runtime pickup입니다.",
        diagram: "flowchart TD\n    BossDeath[Boss Death] --> Handler[BossBattleEndHandler]\n    Handler --> Context[BossRewardContext]\n    Context --> Final{Final route?}\n    Final -->|yes| Suppress[Mark rewards handled without chest]\n    Final -->|no| Chest[Activate authored TreasureChest]\n    Handler --> Pickup[Spawn magic stones / field heals]\n    Handler --> Portal[Activate authored ScenePortal]\n    Portal --> Route[PortalRouteManager active plan]",
        sources: [
          { label: "BossDropResponsibilitySplit.md", path: "../RefactorBacklog/BossDropResponsibilitySplit.md" },
          { label: "LootRewardStructure.md", path: "../StructureMemory/ScriptSystems/LootRewardStructure.md" },
          { label: "DecisionLog.md", path: "../DecisionLog.md" }
        ]
      },
      {
        title: "Presentation Ownership",
        summary: "연출은 타이밍 소유자를 기준으로 AL, state/owner, Cue, runner cleanup으로 나뉩니다.",
        diagram: "flowchart TD\n    VisualNeed[Visual / Audio Need] --> Pattern{Pattern execution timing?}\n    Pattern -->|yes| AL[AL-owned Presentation]\n    Pattern -->|no| State{State lifecycle rhythm?}\n    State -->|yes| Owner[State / Owner-owned Presentation]\n    State -->|no| Reuse{Reusable finished preset?}\n    Reuse -->|yes| Cue[GameplayCue]\n    Reuse -->|no| Local[Local Presentation]\n    AL --> Cleanup[Runner handles / cleanup]\n    Owner --> Cleanup\n    Cue --> Cleanup",
        sources: [
          { label: "PresentationAuthoringContract.md", path: "../Contracts/PresentationAuthoringContract.md" },
          { label: "LoadingPresentationStructure.md", path: "../StructureMemory/ScriptSystems/LoadingPresentationStructure.md" }
        ]
      },
      {
        title: "Dialogue Feature Handoff",
        summary: "대화 이후 Upgrade/Merchant 같은 stack UI는 dialogue blocker release 후 feature가 owned open path로 열어야 합니다.",
        diagram: "sequenceDiagram\n    participant Player\n    participant Dialogue as DialogueService\n    participant Blocker as GameFlowInputBlocker\n    participant Feature as NPCFeature\n    participant UI as UIManager\n    Player->>Dialogue: start Ink dialogue\n    Dialogue->>Blocker: acquire dialogue block\n    Dialogue->>Dialogue: play Ink / tags / choices\n    Dialogue->>Blocker: release before feature UI\n    Dialogue->>Feature: execute feature\n    Feature->>UI: open owned stack UI",
        sources: [
          { label: "DialogueArchitecture.md", path: "../Architecture/DialogueArchitecture.md" },
          { label: "UIFlowInputBlocking.md", path: "../StructureMemory/UIFlowInputBlocking.md" },
          { label: "ErrorLog.md", path: "../ErrorLog.md" }
        ]
      },
      {
        title: "Scene Transition / Restore",
        summary: "Portal travel은 route resolution, transition, capture, load, restore를 분리해 runtime state ownership drift를 막습니다.",
        diagram: "sequenceDiagram\n    participant Portal as ScenePortal\n    participant Travel as ScenePortalTravelService\n    participant Route as PortalRouteManager\n    participant Capture as PlayerRuntimeCapture\n    participant Load as SceneTransition\n    participant Restore as PlayerRuntimeRestore\n    Portal->>Travel: TryTravel\n    Travel->>Route: resolve active route\n    Travel->>Capture: capture player runtime\n    Travel->>Load: load target scene\n    Load->>Restore: restore pending runtime state",
        sources: [
          { label: "RuntimeSaveArchitecture.md", path: "../Architecture/RuntimeSaveArchitecture.md" },
          { label: "SceneRuntimeSaveStructure.md", path: "../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md" }
        ]
      }
    ],
    systems: [
      {
        title: "Combat / Weapon / Ability",
        summary: "무기 실행, Ability 처리, 피해 계산, 상태 변화의 중심 경계입니다.",
        purpose: "플레이어와 적의 공격이 어떤 런타임 데이터와 Ability 경로를 통해 피해/효과로 이어지는지 이해합니다.",
        responsibilities: [
          "무기별 런타임 상태와 실행 흐름 구분",
          "GAS/Ability, damage payload, hit 처리 경계 확인",
          "상태, 버프, 디버프 적용 흐름 연결",
          "무기 교체나 강제 종료 시 cleanup 위험 관리"
        ],
        connected: ["Boss / Mob Encounter", "Loot / Reward", "UI / Presentation", "Runtime Save"],
        caution: "무기, Ability, 상태 시스템을 한 계층으로 합치면 cleanup과 저장 복원 경계가 흐려집니다.",
        diagram: "flowchart LR\n    Input[입력/AI] --> Weapon[Weapon Runtime]\n    Weapon --> Ability[Ability 실행]\n    Ability --> Damage[Damage / Effect]\n    Damage --> Status[Status / Buff / Debuff]\n    Status --> UI[HUD Projection]",
        sources: [
          { label: "CombatArchitecture.md", path: "../Architecture/CombatArchitecture.md" },
          { label: "GameplayAbilityWeaponArchitecture.md", path: "../Architecture/GameplayAbilityWeaponArchitecture.md" },
          { label: "WeaponAndGASStructure.md", path: "../StructureMemory/ScriptSystems/WeaponAndGASStructure.md" },
          { label: "WeaponCleanupContract.md", path: "../Contracts/WeaponCleanupContract.md" }
        ]
      },
      {
        title: "Loot / Reward",
        summary: "스테이지 루트, 보스 보상, pickup, 보상 전달 정책의 중심입니다.",
        purpose: "어떤 시스템이 기본 보상을 만들고, 어떤 시스템이 런타임 modifier로 보상을 더하는지 구분합니다.",
        responsibilities: [
          "StageLootTable과 LootManager 기반 기본 보상 생성",
          "보스 보상과 RouteSet 특수 보상 연결",
          "물리 pickup, 상자 보상, 재화 보상 경계 구분",
          "런타임 modifier는 asset을 바꾸지 않고 additive overlay로 처리"
        ],
        connected: ["Combat / Weapon / Ability", "Inventory / Chest UI", "Boss / Mob Encounter", "Dialogue / NPC Affection"],
        caution: "고정 배치 오브젝트와 개수가 변하는 물리 보상을 같은 배치 문제로 처리하지 않습니다.",
        diagram: "flowchart TD\n    EnemyDeath[적 사망] --> RewardPolicy[보상 정책]\n    RewardPolicy --> LootTable[StageLootTable]\n    RewardPolicy --> Modifiers[Runtime Modifiers]\n    LootTable --> RewardService[Reward Service]\n    Modifiers --> RewardService\n    RewardService --> Chest[상자]\n    RewardService --> Pickup[월드 Pickup]",
        sources: [
          { label: "LootRewardStructure.md", path: "../StructureMemory/ScriptSystems/LootRewardStructure.md" },
          { label: "LootRewardIntegrationPipeline.md", path: "../Guides/ContentAuthoring/LootRewardIntegrationPipeline.md" },
          { label: "LootRewardPolicyBoundarySplit.md", path: "../RefactorBacklog/LootRewardPolicyBoundarySplit.md" }
        ]
      },
      {
        title: "Inventory / Chest UI",
        summary: "아이템 보관, 상자 UI, HUD 진입점, 월드 드롭 상호작용을 연결합니다.",
        purpose: "획득한 보상이 플레이어 인벤토리와 UI에 어떻게 투영되는지 빠르게 파악합니다.",
        responsibilities: [
          "인벤토리 런타임 상태와 UI 투영 분리",
          "상자 첫 열림 presentation과 입력 차단 흐름 관리",
          "아이템 상세/툴팁과 HUD entry point 연결",
          "월드 드롭 pickup과 inventory delivery 연결"
        ],
        connected: ["Loot / Reward", "UI / Presentation", "Runtime Save", "Player Interaction"],
        caution: "UI는 gameplay state를 소유하지 않고 현재 상태를 투영해야 합니다.",
        diagram: "flowchart LR\n    Pickup[월드 아이템] --> Inventory[Inventory Runtime]\n    Chest[상자] --> ChestUI[Chest UI]\n    Inventory --> HUD[HUD]\n    Inventory --> Detail[Item Detail / Tooltip]\n    ChestUI --> Inventory",
        sources: [
          { label: "InventoryAndChestUIStructure.md", path: "../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md" },
          { label: "InventoryTransferResponsibilitySplit.md", path: "../RefactorBacklog/InventoryTransferResponsibilitySplit.md" }
        ]
      },
      {
        title: "Scene / Runtime Save",
        summary: "씬 전환, 포탈, 런타임 상태 capture/restore, run 진행 상태를 다룹니다.",
        purpose: "플레이어/런/맵 상태가 씬 이동과 저장 복원 사이에서 어디에 소유되는지 이해합니다.",
        responsibilities: [
          "PortalRouteManager와 active route plan 기반 이동",
          "플레이어 런타임 상태 capture와 restore",
          "run timer, pending transition, shortcut/map progression 관리",
          "scene-facing facade와 내부 service 책임 분리"
        ],
        connected: ["Bootstrap / Runtime Ownership", "Boss / Mob Encounter", "Inventory / Chest UI", "UI / Presentation"],
        caution: "씬 인스턴스 의미를 공유 prefab에 넣으면 다른 이동 맥락에 전파됩니다.",
        diagram: "flowchart TD\n    Portal[Scene Portal] --> Route[PortalRouteManager]\n    Route --> Transition[Scene Transition]\n    Transition --> Capture[Runtime Capture]\n    Transition --> Load[Scene Load]\n    Load --> Restore[Runtime Restore]\n    Restore --> Player[Player / Run State]",
        sources: [
          { label: "RuntimeSaveArchitecture.md", path: "../Architecture/RuntimeSaveArchitecture.md" },
          { label: "SceneRuntimeSaveStructure.md", path: "../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md" },
          { label: "SceneRunStateBoundarySplit.md", path: "../RefactorBacklog/SceneRunStateBoundarySplit.md" }
        ]
      },
      {
        title: "Boss / Mob Encounter",
        summary: "보스와 일반 몹의 FSM, 패턴 실행, 스폰, 사망 결과, battle-end 흐름입니다.",
        purpose: "인카운터가 전투와 보상으로 이어지는 경계를 이해하고, 보스 battle-end authoring 위험을 줄입니다.",
        responsibilities: [
          "보스 Encounter -> Battle -> BattleEnd 흐름 구분",
          "일반 몹 FSM, spawn population, room lock overlay 관리",
          "hazard/puddle 등 enemy-owned presentation cleanup 확인",
          "보스 chest/portal은 scene-authored activation object로 유지"
        ],
        connected: ["Combat / Weapon / Ability", "Loot / Reward", "Scene / Runtime Save", "UI / Presentation"],
        caution: "보스 battle-end의 고정 chest/portal 배치를 runtime spawn/anchor로 되돌리지 않습니다.",
        diagram: "flowchart TD\n    Spawn[스폰/인카운터 시작] --> FSM[FSM / Pattern]\n    FSM --> Battle[전투]\n    Battle --> Death[사망 처리]\n    Death --> Reward[보상]\n    Death --> BattleEnd[BossBattleEndHandler]\n    BattleEnd --> Portal[씬 작성 포탈 활성화]",
        sources: [
          { label: "BossEncounterArchitecture.md", path: "../Architecture/BossEncounterArchitecture.md" },
          { label: "BossAndMobEncounterStructure.md", path: "../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md" },
          { label: "MobCleanupContract.md", path: "../Contracts/MobCleanupContract.md" },
          { label: "BossDropResponsibilitySplit.md", path: "../RefactorBacklog/BossDropResponsibilitySplit.md" }
        ]
      },
      {
        title: "Dialogue / NPC Affection",
        summary: "Ink 대화, NPC feature, 호감도, merchant/upgrade, dialogue blocker 흐름입니다.",
        purpose: "대화 UI와 NPC 기능이 gameplay flow를 어떻게 막고 해제하는지 이해합니다.",
        responsibilities: [
          "DialogueService와 DialogueView/Controller 역할 구분",
          "NPC feature popup은 dialogue blocker 해제 후 열기",
          "호감도/업그레이드 보상은 런타임 modifier로 투영",
          "상인/업그레이드 정책과 저장 반영 경로 관리"
        ],
        connected: ["Loot / Reward", "UI / Presentation", "Runtime Save", "Run Modifier"],
        caution: "대화 blocker가 활성화된 상태에서 stack UI를 바로 열면 UIManager gate에 막힐 수 있습니다.",
        diagram: "sequenceDiagram\n    participant Player\n    participant Dialogue\n    participant Blocker\n    participant FeatureUI\n    Player->>Dialogue: 상호작용\n    Dialogue->>Blocker: 입력 차단 획득\n    Dialogue->>Blocker: 대화 종료 후 해제\n    Dialogue->>FeatureUI: 기능 UI 열기",
        sources: [
          { label: "DialogueArchitecture.md", path: "../Architecture/DialogueArchitecture.md" },
          { label: "DialogueNpcAffectionStructure.md", path: "../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md" },
          { label: "UIFlowInputBlocking.md", path: "../StructureMemory/UIFlowInputBlocking.md" },
          { label: "UpgradeRuntimeBoundarySplit.md", path: "../RefactorBacklog/UpgradeRuntimeBoundarySplit.md" }
        ]
      },
      {
        title: "UI / Presentation",
        summary: "GlobalUIRoot, HUD, popup, loading, cursor, camera/audio presentation과 display policy입니다.",
        purpose: "게임 상태를 직접 소유하지 않고 scene/prefab-authored presentation으로 보여주는 원칙을 이해합니다.",
        responsibilities: [
          "GlobalUIRoot 기반 전역 UI 레이어 구분",
          "runtime-created UI fallback은 prototype/debug/fallback으로만 취급",
          "loading/cursor/status/Boss HUD의 authored reference 검증",
          "display letterbox와 설정/input binding presentation 경계 확인"
        ],
        connected: ["Inventory / Chest UI", "Dialogue / NPC Affection", "Scene / Runtime Save", "Combat / Status"],
        caution: "production-facing UI를 코드에서 자동 생성하는 구조는 serialized reference와 레이아웃 검증을 숨깁니다.",
        diagram: "flowchart TD\n    RuntimeState[Runtime State] --> Projection[UI Projection]\n    Projection --> GlobalUIRoot[GlobalUIRoot]\n    GlobalUIRoot --> HUD[HUD]\n    GlobalUIRoot --> Popup[Popup]\n    GlobalUIRoot --> Loading[Loading]\n    GlobalUIRoot --> Cursor[Cursor]",
        sources: [
          { label: "PresentationAuthoringContract.md", path: "../Contracts/PresentationAuthoringContract.md" },
          { label: "LoadingPresentationStructure.md", path: "../StructureMemory/ScriptSystems/LoadingPresentationStructure.md" },
          { label: "display-presentation-rules.md", path: "../Contracts/display-presentation-rules.md" },
          { label: "RuntimePresentationFallbackAuthoringSplit.md", path: "../RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md" }
        ]
      },
      {
        title: "Bootstrap / Runtime Ownership",
        summary: "타이틀 씬, gameplay session boundary, app-scope service, scene domain bootstrap 경계입니다.",
        purpose: "앱 시작/타이틀 복귀/게임플레이 세션 경계를 구분해 runtime ownership drift를 막습니다.",
        responsibilities: [
          "TitleScene을 app entry와 gameplay session boundary로 취급",
          "app-scope service와 gameplay-scope presentation 구분",
          "return-to-title 시 run/session cleanup 보존",
          "scene-facing facade는 compatibility surface로 유지"
        ],
        connected: ["Scene / Runtime Save", "UI / Presentation", "Run Session", "Settings"],
        caution: "scene/prefab에서 이미 쓰는 public facade 이름은 migration 없이 바꾸지 않습니다.",
        diagram: "flowchart TD\n    Title[TitleScene] --> AppScope[App Scope Services]\n    Title --> StartRun[Run Start]\n    StartRun --> Gameplay[Gameplay Session]\n    Gameplay --> Return[Return To Title]\n    Return --> Cleanup[Run / Session Cleanup]\n    Cleanup --> Title",
        sources: [
          { label: "SceneDomainBootstrapArchitecture.md", path: "../Architecture/SceneDomainBootstrapArchitecture.md" },
          { label: "SceneDomainBootstrapBoundarySplit.md", path: "../RefactorBacklog/SceneDomainBootstrapBoundarySplit.md" },
          { label: "SceneRunStateLifecycleOwnershipSplit.md", path: "../RefactorBacklog/SceneRunStateLifecycleOwnershipSplit.md" }
        ]
      }
    ]
  },
  authoringGuide: {
    overviewDiagram: "flowchart LR\n    Intent[제작 목적] --> Assets[SO / Prefab / Ink / Audio / UI]\n    Assets --> Wiring[Database / Scene / Route / Inspector 연결]\n    Wiring --> Runtime[Runtime owner / Ability / FSM / Effect]\n    Runtime --> Presentation[Presentation / Cue / Audio / UI projection]\n    Presentation --> Balance[밸런싱 값 조정]\n    Balance --> Validation[정적 검증 + Unity 확인]",
    decisionCards: [
      {
        title: "이 페이지의 쓰임",
        summary: "새 콘텐츠를 만들 때 어떤 에셋을 만들고, 어떤 값을 조정하며, Unity에서 무엇을 확인할지 고르는 사람용 핸드북입니다.",
        status: "authoring"
      },
      {
        title: "원본 문서 우선",
        summary: "세부 계약과 실제 구현 규칙은 Architecture, Contracts, Guides, StructureMemory 원문이 기준입니다.",
        status: "Markdown-first"
      },
      {
        title: "수정 금지선",
        summary: "serialized field rename, SO schema 변경, scene/prefab reference 변경은 Unity migration risk를 먼저 봅니다.",
        status: "risk"
      }
    ],
    pipelines: [
      {
        title: "Weapon / Ability / Combo",
        summary: "새 무기, 새 weapon ability, 콤보, charge, delayed execution, runtime state, 쌍무기 상호작용을 제작합니다.",
        when: "무기 입력이 실행할 AD/AL을 바꾸거나, 무기별 누적 상태, 비활성 슬롯 감쇠, 반대 슬롯 참조, 긴 실행 구간이 필요할 때 사용합니다.",
        outputs: [
          "WeaponDefinition과 아이콘/프리팹/장착 스탯",
          "AbilityDefinition, AbilityLogic, 필요 시 WeaponAbilityLoadout",
          "WeaponAbilitySelectionStrategy와 WeaponSelectionContext 확장",
          "WeaponRuntimeData, WeaponRuntimeProcessor, thin WeaponAbilityRuntimeState",
          "긴 실행용 WeaponAbilityExecutor / WeaponExecutorRunner",
          "쌍무기면 WeaponInteractionLayer와 WeaponPairInteractionRule"
        ],
        steps: [
          "Attack, Skill1, Skill2의 입력별 역할과 persistent state 필요 여부를 먼저 고정합니다.",
          "무기 정의와 loadout의 명시적 AD 참조 소켓을 만들고 database/unlock 경로를 연결합니다.",
          "선택 규칙은 strategy에 두고, 실행 중 대기/취소/후속 입력은 executor/runner로 넘깁니다.",
          "슬롯이 오래 기억해야 하는 값은 RuntimeData에, 시간 경과는 RuntimeProcessor에 둡니다.",
          "반대 슬롯 상태를 읽는 경우 selection context를 쓰고, 소비/반영은 pair rule이 coordinator를 통해 처리합니다.",
          "hit, damage, element, VFX/SFX/HUD projection을 공용 전투/Presentation 경계에 맞춰 연결합니다."
        ],
        tuning: [
          "damage, cooldown, range, hit radius, projectile speed",
          "combo window, charge time, link wait, executor timeout",
          "runtime stack gain/decay, counter window, pair-rule consume amount",
          "element build-up, stagger, knockback",
          "icon/tooltip text, skill HUD cooldown 표시"
        ],
        unityChecks: [
          "WeaponDefinition ID 유일성, icon, prefab, equipped tag, loadout reference",
          "AbilityDefinition이 grant 대상에 포함되는지",
          "ItemDatabase all/default unlock 등록",
          "무기 프리팹의 serialized reference와 cleanup 경로",
          "교체, owner disable, scene transition 뒤 runtime state 유지 여부"
        ],
        checklist: [
          "GAS/ASC가 persistent state owner가 되지 않음",
          "strategy가 상태를 직접 mutation하지 않음",
          "다른 무기 state를 직접 cross-write하지 않음",
          "tooltip/HUD는 현재 상태 projection만 수행",
          "schema/serialized field 변경 시 asset migration risk 검토"
        ],
        pitfalls: [
          "WeaponRuntimeDataFactory/ProcessorFactory에 무기 예외가 계속 늘어나면 구조 부채 후보입니다.",
          "SwordCombo 같은 legacy/sample 구조를 새 표준으로 간주하지 않습니다.",
          "per-hit elemental tuning은 legacy payload field가 아니라 명시적 merge/override 정책으로 검토합니다."
        ],
        source: { label: "WeaponAuthoringPipeline.md", path: "../Guides/ContentAuthoring/WeaponAuthoringPipeline.md" },
        related: [
          { label: "GameplayAbilityWeaponArchitecture.md", path: "../Architecture/GameplayAbilityWeaponArchitecture.md" },
          { label: "DualWeaponPatternGuide.md", path: "../Guides/DualWeaponPatternGuide.md" },
          { label: "WeaponCleanupContract.md", path: "../Contracts/WeaponCleanupContract.md" }
        ],
        diagram: "flowchart TD\n    Definition[WeaponDefinition] --> Loadout[WeaponAbilityLoadout]\n    Loadout --> Selector[Selection Strategy]\n    RuntimeData[WeaponRuntimeData] --> Selector\n    RuntimeData --> Processor[RuntimeProcessor]\n    Selector --> Executor[Ability Executor]\n    Executor --> Bridge[WeaponAbilityBridge]\n    Bridge --> GAS[AbilitySystem]\n    Pair[PairInteractionRule] --> Coordinator[RuntimeCoordinator]\n    Coordinator --> RuntimeData"
      },
      {
        title: "Relic / Consumable Item",
        summary: "새 유물, 유물 proc, 소모품 사용 효과, 인벤토리 표시, loot 후보 등록을 제작합니다.",
        when: "새 획득형 아이템을 추가하거나, proc 조건/사용 효과/수량/툴팁/저장 필요 여부를 정의할 때 사용합니다.",
        outputs: [
          "Relic 또는 Consumable definition",
          "아이콘, 표시명, 설명, tooltip/detail 데이터",
          "proc/effect logic 또는 use effect",
          "ItemDatabase 등록",
          "loot table 또는 reward candidate 연결",
          "필요 시 save/restore 대상 runtime state"
        ],
        steps: [
          "아이템이 passively proc되는지, 직접 사용되는지, 보상 modifier인지 먼저 구분합니다.",
          "definition과 표시 데이터를 만들고 인벤토리/detail UI에 표시될 필드를 확인합니다.",
          "효과가 전투, 상태, 보상, 상점, 업그레이드 중 어디에 걸리는지 연결합니다.",
          "소모품은 사용 조건, 실패 조건, 소비 시점을 명확히 합니다.",
          "유물은 중복 획득, merge, level, proc cleanup 기준을 확인합니다.",
          "loot/database/reward source 등록 후 플레이어 획득과 UI 갱신을 확인합니다."
        ],
        tuning: [
          "proc chance, cooldown, duration, stack cap",
          "effect magnitude, tick interval, remove timing",
          "소모품 capacity, consume timing, 실패 시 소비 여부",
          "relic level scaling, merge behavior",
          "rarity, loot appearance chance"
        ],
        unityChecks: [
          "아이콘과 표시명/설명 누락 여부",
          "ItemDatabase 등록과 duplicate ID 여부",
          "inventory/detail/tooltip 표시",
          "loot table 후보 등록과 unlock-only 구분",
          "save/restore가 필요한 상태인지"
        ],
        checklist: [
          "inventory UI가 실제 gameplay state를 소유하지 않음",
          "사용 실패 규칙이 명확함",
          "중복/merge/level 처리와 tooltip 값이 일치함",
          "proc cleanup과 owner disable 위험 검토"
        ],
        pitfalls: [
          "SO asset 자체를 runtime state처럼 변형하지 않습니다.",
          "tooltip 값과 실제 effect magnitude가 따로 drift하지 않게 조정 지점을 명확히 둡니다."
        ],
        source: { label: "RelicAuthoringPipeline.md", path: "../Guides/ContentAuthoring/RelicAuthoringPipeline.md" },
        related: [
          { label: "ConsumableAuthoringPipeline.md", path: "../Guides/ContentAuthoring/ConsumableAuthoringPipeline.md" },
          { label: "InventoryAndChestUIStructure.md", path: "../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md" }
        ],
        diagram: "flowchart TD\n    ItemDef[Item Definition] --> Database[ItemDatabase]\n    ItemDef --> Detail[Tooltip / Detail UI]\n    ItemDef --> Effect[Proc / Use Effect]\n    Effect --> Runtime[Runtime Owner]\n    ItemDef --> Loot[Loot Candidate]\n    Loot --> Inventory[Inventory]"
      },
      {
        title: "Mob / FSM / Pattern",
        summary: "일반 몹 본체, FSM 상태, 공격 판단 source, ability runner, spawn population, death result를 제작합니다.",
        when: "새 일반 몹, attack state, runner, wave/spawn 구성, split/summon/transform/death result를 추가할 때 사용합니다.",
        outputs: [
          "Mob 본체 또는 기존 본체 확장",
          "prefab의 EnemyChaseIntent2D, MobAbilityCoordinator, AbilitySystem, TagSystem, AttributeSet",
          "IMobAttackDecisionSource 구현",
          "AbilityDefinition / AbilityLogic / IMobPatternRunner",
          "warning/hit/projectile Presentation references",
          "MonsterRoomSpawnProfileSO 또는 scene spawn container 연결"
        ],
        steps: [
          "몹 역할, 추적 범위, 공격 리듬, death result, split/summon 여부를 정의합니다.",
          "prefab에 공통 전투 컴포넌트와 target/chase 구성이 들어있는지 확인합니다.",
          "공격 선택은 IMobAttackDecisionSource에 두고, 긴 실행은 runner가 담당합니다.",
          "AL-owned warning/hit presentation과 state-owned 유지 연출을 구분합니다.",
          "spawn profile/container와 room lock overlay 영향을 연결합니다.",
          "사망 후 loot, split, summon, lock count 기준을 검증합니다."
        ],
        tuning: [
          "move speed, chase range, attack range, recover time",
          "warning duration, warning width/radius, hit delay",
          "damage, knockback, stagger, element build-up",
          "spawn count, wave interval, room lock 참여 여부",
          "death loot chance와 no-loot/gimmick enemy 정책"
        ],
        unityChecks: [
          "prefab component 구성과 serialized references",
          "AbilitySystem / MobAbilityCoordinator / TagSystem 존재",
          "spawn profile 또는 scene placement 연결",
          "runner Cancel/finally cleanup",
          "lock overlay 해제 경로와 death timing"
        ],
        checklist: [
          "공통 FSM 엔진에 몹별 특수 로직을 밀어 넣지 않음",
          "runner가 fixed presentation data owner가 되지 않음",
          "state-owned presentation은 state exit와 fail-safe cleanup을 가짐",
          "split/summon/transform은 lock count 설계가 먼저 필요함"
        ],
        pitfalls: [
          "일반 몹은 Encounter 단위가 아니라 population 후 battle-ready runtime으로 보는 편이 현재 구조에 맞습니다.",
          "delayed death presentation과 reward/lock release timing이 어긋나기 쉽습니다."
        ],
        source: { label: "MobAuthoringPipeline.md", path: "../Guides/ContentAuthoring/MobAuthoringPipeline.md" },
        related: [
          { label: "GeneralMobFSMAuthoringGuide.md", path: "../Guides/GeneralMobFSMAuthoringGuide.md" },
          { label: "MobCleanupContract.md", path: "../Contracts/MobCleanupContract.md" },
          { label: "BossAndMobEncounterStructure.md", path: "../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md" }
        ],
        diagram: "flowchart TD\n    SpawnProfile[Spawn Profile] --> MobPrefab[Mob Prefab]\n    MobPrefab --> FSM[Mob FSM]\n    Decision[Attack Decision Source] --> FSM\n    FSM --> Ability[Ability / Runner]\n    Ability --> Presentation[AL / State Presentation]\n    FSM --> Death[Death Result]\n    Death --> Loot[Monster Loot / Lock Release]"
      },
      {
        title: "Boss / Pattern / Battle-End",
        summary: "보스 encounter, boss controller, phase/pattern, death presentation, authored chest/portal battle-end를 제작합니다.",
        when: "새 보스, 새 보스 패턴, 보스 씬, 보스 보상 preset, battle-end chest/portal authoring을 만들 때 사용합니다.",
        outputs: [
          "BossControllerBase 기반 boss-specific controller",
          "BossPatternEntry, condition, AbilityLogic, pattern actor/executor",
          "BossEncounterDirector / BossDialogueRunner 연결",
          "BossDeathPresentation과 cinematic protection 확인",
          "RouteSet-linked BossSpecialRewardPresetSO",
          "scene-authored BossBattleEndHandler, inactive TreasureChest, inactive ScenePortal"
        ],
        steps: [
          "Encounter -> Battle -> BattleEnd 흐름과 phase/state 전이를 먼저 고정합니다.",
          "보스 고유 패턴은 boss-specific controller/actor/AL에 두고 공용 base로 새지 않게 합니다.",
          "warning/hit/projectile은 AL-owned, intro/death/cinematic rhythm은 state/director-owned로 나눕니다.",
          "RouteSet 특수 보상 preset과 StageLootTable boss base reward 경계를 확인합니다.",
          "보스 씬에 inactive chest/portal을 배치하고 BossBattleEndHandler에 명시적으로 연결합니다.",
          "final route에서는 chest activation이 suppression되는지 확인합니다."
        ],
        tuning: [
          "phase threshold, pattern weight/cooldown, groggy condition",
          "warning duration, hit radius, projectile count/speed",
          "death presentation duration, camera shake, player protection timing",
          "boss chest weapon/relic count, rarity weight, magic stone/field-heal count",
          "boss-specific special reward candidate"
        ],
        unityChecks: [
          "Boss scene의 BossBattleEndHandler boss/chest/portal references",
          "boss exit portal이 RunRouteCatalogSO를 들고 있지 않은지",
          "shared ScenePortal prefab이 semantic-neutral인지",
          "Boss battle-end validator 결과",
          "boss death 후 chest, portal, physical pickups, final-route chest suppression"
        ],
        checklist: [
          "runtime chest/portal instantiate나 anchor placement를 되살리지 않음",
          "보스 고유 예외를 BossControllerBase, 공용 HUD, reward policy에 새지 않게 함",
          "BossDrop legacy path를 재도입하지 않음",
          "player targeting은 player root 기준으로 검토"
        ],
        pitfalls: [
          "fixed chest/portal과 variable-count physical pickup을 같은 placement 문제로 다루지 않습니다.",
          "final boss route reward policy를 일반 boss reward flow로 무심코 되돌리지 않습니다."
        ],
        source: { label: "BossAuthoringPipeline.md", path: "../Guides/ContentAuthoring/BossAuthoringPipeline.md" },
        related: [
          { label: "BossEncounterArchitecture.md", path: "../Architecture/BossEncounterArchitecture.md" },
          { label: "BossDropResponsibilitySplit.md", path: "../RefactorBacklog/BossDropResponsibilitySplit.md" },
          { label: "LootRewardStructure.md", path: "../StructureMemory/ScriptSystems/LootRewardStructure.md" }
        ],
        diagram: "sequenceDiagram\n    participant Boss\n    participant Death\n    participant Handler\n    participant Reward\n    participant Portal\n    Boss->>Death: OnDeathStarted\n    Death->>Handler: Boss defeated\n    Handler->>Reward: Fill authored chest or suppress final chest\n    Handler->>Reward: Spawn variable pickups\n    Handler->>Portal: Activate authored exit portal"
      },
      {
        title: "Dialogue / NPC / Affection",
        summary: "Ink, NPCData, portrait/illustration, emotion, dialogue theme, speech bubble, feature UI handoff, affection reward를 제작합니다.",
        when: "새 NPC 대화, 보스 대화, 선택지, 일러스트 감정 표현, 상점/업그레이드/호감도 feature 연결이 필요할 때 사용합니다.",
        outputs: [
          ".ink 파일과 compiled JSON",
          "NPCData / NPCDatabase 등록",
          "DialogueTheme, DialogueView, PortraitController mapping",
          "speaker/emotion/theme/feature 관련 Ink tag",
          "SpeechBubbleComponent, PlayerSpeechData, BossSpeechData, BubbleTheme",
          "Affection reward/effect 또는 NPC feature 연결"
        ],
        steps: [
          "Ink knot/stitch 구조와 speaker, emotion, choice, feature trigger를 먼저 정의합니다.",
          "Ink Unity 자동 컴파일 또는 수동 Recompile Ink로 JSON 생성과 warning을 확인합니다.",
          "NPCData에 primary Ink, 기본 portrait/theme, feature/affection 정보를 연결합니다.",
          "DialogueTagHandler가 읽는 tag를 기준으로 portrait, emotion, theme, audio, feature handoff를 연결합니다.",
          "feature UI는 dialogue blocker release 이후 열리게 하고, 필요한 경우 owned UI open path를 사용합니다.",
          "보스 대화는 BossEncounterDirector/BossDialogueRunner/camera/player lock과 함께 검증합니다."
        ],
        tuning: [
          "typing speed, slide/fade duration, choice delay",
          "portrait position, scale, fade, shake/emphasis motion",
          "emotion fallback, theme colors, dialogue effect animator",
          "speech bubble duration, bubble theme, quick bark text",
          "affection gain amount, reward threshold, feature unlock condition"
        ],
        unityChecks: [
          "DialogueController view/director/portraitController/tagHandler references",
          "DialogueView textBoxGroup/nameText/dialogueText/choiceContainer/choiceButtonPrefab",
          "DialogueTheme, portrait sprites, effect animator 연결",
          "NPCDatabase 등록과 NPCData primary Ink reference",
          "Ink Player Window로 branch/variable/choice 확인"
        ],
        checklist: [
          "Ink는 상황/태그를 말하고 UI hierarchy나 gameplay state를 직접 소유하지 않음",
          "feature UI를 dialogue blocker가 살아있는 동안 바로 열지 않음",
          "일러스트 감정 fallback 기준이 있음",
          "보스 대화는 camera/player lock/combat start handoff와 같이 확인"
        ],
        pitfalls: [
          "대화 태그 문법은 반드시 실제 DialogueTagHandler 기준으로 문서화해야 합니다.",
          "말풍선은 짧은 전투 bark용입니다. 긴 대사는 Ink dialogue로 둡니다.",
          "호감도 보상은 base progression reward를 우회하지 않고 additive modifier로 투영합니다."
        ],
        source: { label: "DialogueArchitecture.md", path: "../Architecture/DialogueArchitecture.md" },
        related: [
          { label: "DialogueNpcAffectionStructure.md", path: "../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md" },
          { label: "UIFlowInputBlocking.md", path: "../StructureMemory/UIFlowInputBlocking.md" },
          { label: "ErrorLog.md", path: "../ErrorLog.md" }
        ],
        diagram: "flowchart TD\n    Ink[Ink knot / tags] --> Compile[Ink JSON]\n    Compile --> NPCData[NPCData]\n    NPCData --> Service[DialogueService]\n    Service --> Controller[DialogueController]\n    Controller --> View[DialogueView]\n    Controller --> Portrait[PortraitController]\n    Controller --> Tags[DialogueTagHandler]\n    Tags --> Feature[NPC Feature UI]\n    Tags --> Emotion[Portrait / Theme / Audio]\n    Feature --> Blocker[GameFlowInputBlocker release]"
      },
      {
        title: "Upgrade / Run Modifier",
        summary: "업그레이드 노드, cost/lock/unlock, UpgradeEffect, run-start effect, shop/chest/grave/boss reward modifier를 제작합니다.",
        when: "새 업그레이드 노드, 구매 조건, 런 시작 보상, 상점/보상 modifier, UI tooltip을 만들거나 조정할 때 사용합니다.",
        outputs: [
          "Upgrade node ScriptableObject와 Upgrade database 등록",
          "UpgradeEffect ScriptableObject",
          "unlock/lock/prerequisite/cost/display data",
          "run modifier delta 또는 run-start effect",
          "UpgradeTreeUI slot/tooltip/lake presentation",
          "warning popup failure mapping 확인"
        ],
        steps: [
          "노드의 목적이 즉시 적용, run-start 적용, shop modifier, reward modifier, unlock 중 무엇인지 구분합니다.",
          "node SO에 cost, prerequisite, unlock 조건, 표시명/설명을 작성합니다.",
          "효과는 UpgradeEffect 하위 SO로 만들고 runtime effect handoff 경로를 확인합니다.",
          "Reward/shop/boss reward 관련 값은 RunModifierService의 snapshot/aggregate 경로로 흘립니다.",
          "UI tooltip, locked visual, warning popup failure reason을 확인합니다.",
          "구매, 저장, 재진입, run start, 중복 적용을 검증합니다."
        ],
        tuning: [
          "magic stone cost, prerequisite depth, unlock order",
          "effect magnitude, stack cap, duration, target scope",
          "shop slot count, refresh count, discount",
          "chest/grave/boss reward modifier delta",
          "tooltip wording과 failure warning code"
        ],
        unityChecks: [
          "Upgrade database에 node 등록",
          "UpgradeTreeUI 노드/화살표/tooltip references",
          "locked node click이 warning popup으로 이어지는지",
          "UpgradeManager facade와 helper 경계 유지",
          "save/load 후 purchase state와 effect 재적용"
        ],
        checklist: [
          "UpgradeManager에 새 정책을 직접 뭉치지 않음",
          "RunModifierService를 upgrade-only service로 취급하지 않음",
          "SO asset을 runtime mutable state로 쓰지 않음",
          "locked disabled-looking 버튼도 설명 경로가 있음"
        ],
        pitfalls: [
          "대화에서 Upgrade UI를 열 때 dialogue blocker release handoff가 필요합니다.",
          "effect magnitude와 tooltip 설명이 따로 drift하기 쉽습니다."
        ],
        source: { label: "DialogueNpcAffectionStructure.md", path: "../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md" },
        related: [
          { label: "UpgradeRuntimeBoundarySplit.md", path: "../RefactorBacklog/UpgradeRuntimeBoundarySplit.md" },
          { label: "RunModifierAggregationBoundarySplit.md", path: "../RefactorBacklog/RunModifierAggregationBoundarySplit.md" },
          { label: "DecisionLog.md", path: "../DecisionLog.md" }
        ],
        diagram: "flowchart TD\n    Node[Upgrade Node SO] --> Database[Upgrade Database]\n    Node --> Effect[UpgradeEffect SO]\n    Effect --> Manager[UpgradeManager facade]\n    Manager --> Helpers[Purchase / Completion / Runtime Effect Helpers]\n    Effect --> Modifier[RunModifierService]\n    Modifier --> Snapshot[RunRewardModifierSnapshot]\n    Snapshot --> Reward[Shop / Chest / Grave / Boss Reward]"
      },
      {
        title: "Loot / Economy / Reward",
        summary: "아이템 DB, loot table, chest, monster/grave/boss reward, magic stone, field heal, reward modifier를 연결하고 밸런싱합니다.",
        when: "새 아이템을 보상 후보에 넣거나, reward source, rarity, count, boss special reward, field-heal/magic-stone 지급을 조정할 때 사용합니다.",
        outputs: [
          "ItemDatabase 등록",
          "StageLootTable / GraveLootTable 값",
          "BossSpecialRewardPresetSO",
          "Chest reward policy와 RunRewardModifierSnapshot 확인",
          "World pickup prefab/presentation",
          "MagicStonePickup / FieldHealPickup2D 설정"
        ],
        steps: [
          "reward source가 chest, monster death, boss battle-end, grave, merchant, upgrade, affection 중 어디인지 정의합니다.",
          "아이템 ID와 database 등록, unlock-only와 loot-pool item 구분을 확인합니다.",
          "StageLootTable에서 count, rarity, boss base reward, field-heal/magic-stone 값을 조정합니다.",
          "boss-specific special loot는 RouteSet-linked preset으로 연결합니다.",
          "runtime modifier는 additive aggregate/snapshot 경로로만 더합니다.",
          "world pickup delivery, inventory failure warning, pickup destruction timing을 확인합니다."
        ],
        tuning: [
          "chest count, relic rarity weights, weapon/relic/consumable appearance chance",
          "boss weapon/relic count, boss magic stone count, field heal count",
          "grave reward count, duplicate exclusion policy",
          "merchant stock weight와 reward source별 exclusion context",
          "pickup drop travel, landing offset, idle heartbeat/floating"
        ],
        unityChecks: [
          "StageLootTable boss/normal chest profile",
          "BossSpecialRewardPresetSO RouteSet reference",
          "LootManager/LootSpawnService prefab references",
          "WorldItemPickup2D detail/highlight/warning display",
          "field-heal optional consume particle와 drop movement"
        ],
        checklist: [
          "base reward와 additive modifier를 섞지 않음",
          "boss reward base value는 StageLootTable에서 확인",
          "final route boss chest fallback을 재도입하지 않음",
          "world pickup delivery는 inventory transfer policy와 같이 검토"
        ],
        pitfalls: [
          "고정 authored objects와 variable-count physical pickup을 같은 authoring 문제로 다루지 않습니다.",
          "Reward presentation과 reward generation은 별도 책임입니다."
        ],
        source: { label: "LootRewardIntegrationPipeline.md", path: "../Guides/ContentAuthoring/LootRewardIntegrationPipeline.md" },
        related: [
          { label: "LootRewardStructure.md", path: "../StructureMemory/ScriptSystems/LootRewardStructure.md" },
          { label: "LootRewardPolicyBoundarySplit.md", path: "../RefactorBacklog/LootRewardPolicyBoundarySplit.md" },
          { label: "DecisionLog.md", path: "../DecisionLog.md" }
        ],
        diagram: "flowchart TD\n    Source[Reward Source] --> Policy[Loot / Reward Policy]\n    Policy --> Table[StageLootTable]\n    Policy --> Modifier[RunRewardModifierSnapshot]\n    Table --> Roll[Loot Roll]\n    Modifier --> Roll\n    Roll --> Delivery[Chest / Pickup / Currency]\n    Delivery --> Inventory[Inventory / Runtime State]"
      },
      {
        title: "Shop / Merchant",
        summary: "ShopDefinitionSO, typed slot anchors, slot prefab, stock roll, refresh, discount, item detail presentation을 제작합니다.",
        when: "상점 레이아웃, 무기/유물/소모품 슬롯 분리, 가격/리롤/discount, merchant stock policy를 만들거나 조정할 때 사용합니다.",
        outputs: [
          "ShopDefinitionSO",
          "MerchantNPC slotPrefab reference",
          "authored slot anchors와 ShopSlotItemFilter",
          "ShopSlot prefab",
          "MerchantRunStateService stock state",
          "world item detail presenter와 purchase warning"
        ],
        steps: [
          "상점이 판매할 카테고리와 표시할 슬롯 유형을 먼저 정합니다.",
          "ShopDefinitionSO의 visible slot count, stock weights, max weapon/consumable caps를 작성합니다.",
          "scene에는 slot anchor transforms를 배치하고 각 anchor filter를 Any/Weapon/Relic/Consumable로 지정합니다.",
          "MerchantNPC에 ShopSlot prefab과 ordered anchors를 연결합니다.",
          "RunModifierService shop modifiers가 slot count, discount, refresh에 반영되는지 확인합니다.",
          "구매, refresh, stock preservation, item detail 표시를 검증합니다."
        ],
        tuning: [
          "visible slot count, typed slot count, max weapon/consumable caps",
          "item category weights, price, discount",
          "refresh count, refresh cost, stock preservation policy",
          "slot spacing/layout, hover/detail text",
          "upgrade/affection shop modifier magnitude"
        ],
        unityChecks: [
          "MerchantNPC slotPrefab assigned",
          "slotAnchors 순서와 filter 지정",
          "ShopSlot prefab visual/interaction references",
          "ShopDefinitionSO caps와 화면 slot layout 일치",
          "scene copied ShopSlot fallback이 의도치 않게 섞이지 않는지"
        ],
        checklist: [
          "scene layout은 anchor를 소유하고 live slot behavior는 prefab이 소유",
          "merchant stock은 run/session state로 보존",
          "UI는 stock state를 소유하지 않음",
          "typed slots와 ShopDefinitionSO caps가 서로 모순되지 않음"
        ],
        pitfalls: [
          "slot anchor 수만 늘리고 MaxWeaponSlots/MaxConsumableSlots를 조정하지 않으면 표시 의도와 roll cap이 어긋납니다.",
          "ShopSlot live object를 scene마다 복제하는 방식으로 되돌리지 않습니다."
        ],
        source: { label: "DialogueNpcAffectionStructure.md", path: "../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md" },
        related: [
          { label: "DecisionLog.md", path: "../DecisionLog.md" },
          { label: "ErrorLog.md", path: "../ErrorLog.md" }
        ],
        diagram: "flowchart TD\n    ShopDef[ShopDefinitionSO] --> Policy[MerchantShopPolicy]\n    Mod[Shop Modifiers] --> Policy\n    Anchors[Typed Slot Anchors] --> Merchant[MerchantNPC]\n    SlotPrefab[ShopSlot Prefab] --> Merchant\n    Policy --> Roll[ShopInventoryRoll]\n    Roll --> Slot[ShopSlot Instances]\n    Slot --> Detail[Item Detail / Purchase]"
      },
      {
        title: "Presentation / Cue / VFX / SFX / Animation",
        summary: "패턴별 Presentation, 재사용 Cue, particle prefab, animator, camera shake, cleanup ownership를 제작합니다.",
        when: "warning, hit, projectile, explosion, state overlay, reusable VFX/SFX preset, animation timing을 만들 때 사용합니다.",
        outputs: [
          "AL-owned Presentation references",
          "state-owned visual/mask/overlay references",
          "GameplayCue reusable preset",
          "particle/VFX prefab, animation clip, animator trigger/state",
          "sound/camera shake reference",
          "cleanup handle와 fail-safe cleanup path"
        ],
        steps: [
          "연출이 pattern execution인지, state rhythm인지, reusable preset인지 먼저 결정합니다.",
          "pattern-specific warning/hit/explosion은 AL 또는 pattern data에 둡니다.",
          "상태가 살아있는 동안 따라가야 하는 시각 요소는 FSM state/owner에 둡니다.",
          "여러 시스템이 공유할 완성 연출 묶음만 Cue로 승격합니다.",
          "runner/helper는 runtime handle과 cleanup만 맡고 fixed presentation data owner가 되지 않게 합니다.",
          "Cancel/finally, state exit, owner disable/death에서 연출이 사라지는지 확인합니다."
        ],
        tuning: [
          "warning duration, telegraph geometry, hit delay",
          "particle lifetime, fade, scale, sorting/order",
          "animation clip length, transition, animator parameter",
          "camera shake amplitude/duration",
          "sound volume, loop 여부, spatial 여부"
        ],
        unityChecks: [
          "prefab/particle/animator reference assigned",
          "runtime-created fallback이 build-facing 경로로 남지 않았는지",
          "sorting layer, canvas order, raycast conflict",
          "owner disable/death/cancel cleanup",
          "Scene Setup Validator 또는 관련 validator warning"
        ],
        checklist: [
          "Presentation은 local timing, Cue는 reusable finished preset",
          "state-owned presentation은 state exit cleanup을 가짐",
          "runner는 fixed data owner가 아님",
          "production-facing UI/presentation hierarchy는 prefab/scene authored 우선"
        ],
        pitfalls: [
          "Cue를 모든 local pattern tuning의 대체물로 쓰면 재사용 preset 관리가 오히려 어려워집니다.",
          "runtime fallback은 prototype/debug/emergency로만 취급합니다."
        ],
        source: { label: "PresentationAuthoringContract.md", path: "../Contracts/PresentationAuthoringContract.md" },
        related: [
          { label: "LoadingPresentationStructure.md", path: "../StructureMemory/ScriptSystems/LoadingPresentationStructure.md" },
          { label: "RuntimePresentationFallbackAuthoringSplit.md", path: "../RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md" }
        ],
        diagram: "flowchart TD\n    Need[연출 필요] --> Pattern{특정 패턴 타이밍?}\n    Pattern -->|예| AL[AL-owned Presentation]\n    Pattern -->|아니오| State{상태 유지 리듬?}\n    State -->|예| Owner[State / Owner Presentation]\n    State -->|아니오| Reuse{여러 시스템 재사용?}\n    Reuse -->|예| Cue[GameplayCue]\n    Reuse -->|아니오| Local[Local Presentation]\n    AL --> Cleanup[Runner / State cleanup]\n    Owner --> Cleanup\n    Cue --> Cleanup"
      },
      {
        title: "Audio Authoring",
        summary: "BGM, boss BGM, pattern SFX, dialogue SFX, UI sound, loop/one-shot, audio catalog 연결을 제작합니다.",
        when: "씬/route BGM, 보스 등장/패턴 사운드, 대화 효과음, UI open/close, pickup/reward sound를 추가하거나 조정할 때 사용합니다.",
        outputs: [
          "Audio clip asset과 import settings",
          "AudioCatalogSO 또는 관련 sound cue 등록",
          "BGM/ambience route 또는 scene context 연결",
          "Ability/Presentation/Cue에서 trigger할 SFX reference",
          "UI sound event 연결",
          "loop sound lifecycle와 cleanup 경로"
        ],
        steps: [
          "사운드가 BGM, ambience, one-shot SFX, loop SFX, UI sound, dialogue sound 중 무엇인지 구분합니다.",
          "재사용 sound는 catalog/cue로, pattern-local sound는 Ability/Presentation data로 둡니다.",
          "loop sound는 enter/exit lifecycle과 cleanup owner를 명확히 합니다.",
          "scene/route 전환 시 BGM 교체 기준과 fade timing을 확인합니다.",
          "UI/dialogue sound는 gameplay state를 바꾸지 않고 presentation event로만 처리합니다.",
          "실제 대화, 보스 등장, 패턴, UI, 씬 전환에서 재생/중복/누락을 확인합니다."
        ],
        tuning: [
          "volume, pitch, random pitch range",
          "loop 여부, fade in/out, stop timing",
          "mixer group, spatial blend, rolloff",
          "BGM transition/fade duration",
          "pattern timing과 SFX offset"
        ],
        unityChecks: [
          "AudioCatalogSO category와 clip reference",
          "Ability/Presentation/Cue에서 sound reference assigned",
          "BGM/route context 연결",
          "loop sound가 cancel/death/scene transition에서 멈추는지",
          "UI sound가 popup/stack open/close와 중복 재생되지 않는지"
        ],
        checklist: [
          "재사용 sound와 pattern-local sound를 구분",
          "loop lifecycle owner가 명확함",
          "audio service가 progression state를 소유하지 않음",
          "없는 clip fallback/warning을 확인"
        ],
        pitfalls: [
          "컴파일은 오디오 누락을 거의 잡아주지 못합니다. 실제 재생 확인이 필요합니다.",
          "loop sound는 cancel path를 놓치면 씬 전환 뒤에도 남을 수 있습니다."
        ],
        source: { label: "LoadingPresentationStructure.md", path: "../StructureMemory/ScriptSystems/LoadingPresentationStructure.md" },
        related: [
          { label: "PresentationAuthoringContract.md", path: "../Contracts/PresentationAuthoringContract.md" }
        ],
        diagram: "flowchart LR\n    Clip[Audio Clip] --> Catalog[AudioCatalogSO / Sound Cue]\n    Catalog --> Trigger[Ability / Presentation / UI / Dialogue]\n    Trigger --> Service[Audio Runtime Service]\n    Service --> Output[Mixer / Listener]\n    Trigger --> Cleanup[Loop stop / Scene transition cleanup]"
      },
      {
        title: "UI / HUD / Tooltip",
        summary: "HUD, item detail, tooltip, status HUD, boss HUD, warning popup, player-facing UI projection을 제작합니다.",
        when: "새 표시값, tooltip/detail section, HUD entry, status icon, boss HUD 특수 표시, warning popup을 추가할 때 사용합니다.",
        outputs: [
          "prefab/scene-authored UI references",
          "detail/tooltip provider 또는 formatter",
          "HUD presenter/source",
          "status/boss HUD entry",
          "warning popup code/message",
          "input glyph 또는 button binding"
        ],
        steps: [
          "UI가 보여줄 runtime state owner를 먼저 찾고 UI가 그 state를 소유하지 않게 합니다.",
          "base visual template은 prefab/scene/GlobalUIRoot에 두고 serialized references로 구동합니다.",
          "tooltip/detail은 current state projection과 formatter를 분리합니다.",
          "warning popup은 실패 reason과 user-facing text를 연결합니다.",
          "Boss HUD 특수성은 가능한 boss-local source/adapter로 격리합니다.",
          "mobile/desktop 크기에서 text overflow와 raycast/stack input을 확인합니다."
        ],
        tuning: [
          "tooltip text, glossary link, color palette",
          "HUD icon size, cooldown fill, warning duration",
          "boss HP/groggy/split-health display",
          "status group ordering, duration display",
          "popup text, fade/slide timing"
        ],
        unityChecks: [
          "GlobalUIRoot representative prefab references",
          "DialogueView/Inventory/Chest/HUD serialized references",
          "runtime-created fallback warning",
          "raycast gate, stack UI lock, input blocker",
          "Scene Setup Validator 결과"
        ],
        checklist: [
          "UI/HUD/tooltip은 gameplay state projection만 수행",
          "runtime UI hierarchy creation을 production structure로 고정하지 않음",
          "serialized UI field rename은 migration review 필요",
          "disabled-looking control도 필요한 경우 failure explanation path를 가짐"
        ],
        pitfalls: [
          "dynamic text/icon/row는 괜찮지만 base visual tree fallback을 코드에 숨기면 authoring risk가 커집니다.",
          "Boss HUD 특수 예외를 common HUD에 바로 밀어 넣지 않습니다."
        ],
        source: { label: "InventoryAndChestUIStructure.md", path: "../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md" },
        related: [
          { label: "LoadingPresentationStructure.md", path: "../StructureMemory/ScriptSystems/LoadingPresentationStructure.md" },
          { label: "PresentationAuthoringContract.md", path: "../Contracts/PresentationAuthoringContract.md" },
          { label: "RuntimePresentationFallbackAuthoringSplit.md", path: "../RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md" }
        ],
        diagram: "flowchart TD\n    RuntimeState[Gameplay Runtime State] --> Source[UI Source / Provider]\n    Source --> Formatter[Formatter / Tooltip Provider]\n    Formatter --> View[HUD / Tooltip / Detail View]\n    View --> Input[UI Input / Stack Policy]\n    Input --> UIManager[UIManager]\n    View -. no ownership .-> RuntimeState"
      },
      {
        title: "Scene / Route / Portal",
        summary: "RouteSet, scene portal, hub start portal, boss exit portal, loading context, player capture/restore를 제작합니다.",
        when: "새 corridor/boss scene, route set, hub start, boss exit, portal travel, loading manifest/context를 추가하거나 조정할 때 사용합니다.",
        outputs: [
          "CorridorBossRouteSetSO 또는 route catalog 연결",
          "ScenePortal scene instance",
          "hub start portal RunRouteCatalogSO reference",
          "boss exit portal authored inactive object",
          "loading context/manifest",
          "player spawn/restore references"
        ],
        steps: [
          "route가 HubToRunStart, CorridorToBoss, BossToCorridor, ReturnToHubAfterRun 중 어떤 의미인지 구분합니다.",
          "shared ScenePortal prefab은 semantic-neutral로 유지하고 scene instance에 필요한 의미만 둡니다.",
          "hub start portal은 RunRouteCatalogSO를 소유하고, boss exit portal은 active run plan에 맡깁니다.",
          "boss battle-end portal은 BossBattleEndHandler가 authored inactive object를 activate만 합니다.",
          "scene transition은 capture -> load -> restore 경로를 따르는지 확인합니다.",
          "loading/prewarm/manifest가 route context와 맞는지 확인합니다."
        ],
        tuning: [
          "scene name, route order, boss/corridor pairing",
          "loading display/fade duration, prewarm manifest",
          "portal interaction distance, prompt text",
          "spawn point, restore retry timing",
          "return-to-title cleanup timing"
        ],
        unityChecks: [
          "shared ScenePortal prefab에 RunRouteCatalogSO가 없는지",
          "hub start scene instance만 catalog를 들고 있는지",
          "boss exit portal TransitionType/scene reference",
          "BossBattleEndHandler exitPortal reference",
          "player spawn/restore point와 route loading manifest"
        ],
        checklist: [
          "scene-facing facade 이름/serialized reference를 migration 없이 바꾸지 않음",
          "ScenePortalTravelService.TryTravel은 compatibility wrapper로 유지",
          "GamePlayDataManager pending run data를 durable save처럼 취급하지 않음",
          "portal prefab에 scene-specific semantics를 넣지 않음"
        ],
        pitfalls: [
          "shared portal prefab에 start-run semantics를 넣으면 boss exit가 잘못 이동할 수 있습니다.",
          "runtime restore는 코드상 성공처럼 보여도 Unity scene reference 누락으로 깨질 수 있습니다."
        ],
        source: { label: "SceneRuntimeSaveStructure.md", path: "../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md" },
        related: [
          { label: "RuntimeSaveArchitecture.md", path: "../Architecture/RuntimeSaveArchitecture.md" },
          { label: "SceneDomainBootstrapArchitecture.md", path: "../Architecture/SceneDomainBootstrapArchitecture.md" },
          { label: "DecisionLog.md", path: "../DecisionLog.md" }
        ],
        diagram: "flowchart TD\n    Portal[ScenePortal instance] --> Travel[ScenePortalTravelService]\n    Travel --> Plan[PortalRouteManager active plan]\n    Plan --> Transition[SceneTransitionCoordinator]\n    Transition --> Capture[Player Runtime Capture]\n    Transition --> Load[Scene Load / Loading]\n    Load --> Restore[Player Runtime Restore]"
      },
      {
        title: "Balancing Checklist",
        summary: "콘텐츠 수치 조정 시 어느 값을 어디에서 조정하고 어떤 플레이 확인이 필요한지 정리합니다.",
        when: "데미지, 쿨다운, 거리, 보상 수량, 희귀도, 가격, 업그레이드 효과, 대화/연출 타이밍을 조정할 때 사용합니다.",
        outputs: [
          "조정 대상 값 목록",
          "source asset/SO와 runtime modifier 경계",
          "before/after 비교 기준",
          "manual playtest checklist",
          "문서 업데이트 필요 여부",
          "Presentation HTML stale 여부"
        ],
        steps: [
          "수치가 source asset 값인지 runtime modifier overlay인지 먼저 구분합니다.",
          "전투 값은 damage/cooldown/range만 보지 말고 warning/hit feedback/cleanup까지 함께 봅니다.",
          "보상 값은 base table과 additive modifier를 분리해서 조정합니다.",
          "UI/tooltip/대화 문구가 실제 수치와 일치하는지 확인합니다.",
          "자동 검증으로 잡히지 않는 체감값은 수동 play 확인 항목으로 남깁니다.",
          "값 변경이 여러 시스템 경계에 영향을 주면 관련 Markdown source를 먼저 갱신합니다."
        ],
        tuning: [
          "damage, stagger, element build-up, knockback",
          "cooldown, duration, tick interval, proc chance",
          "warning time, hit radius, projectile speed, animation length",
          "loot count, rarity, magic stone, field heal, shop price",
          "upgrade cost/effect magnitude, affection threshold"
        ],
        unityChecks: [
          "Inspector value와 runtime result 일치",
          "tooltip/detail/status/HUD 표시 값 일치",
          "scene/prefab override가 의도한 asset을 참조하는지",
          "boss/mob pattern feel, pickup feel, UI feedback feel",
          "manual playtest result와 remaining risk 기록"
        ],
        checklist: [
          "수치 조정만으로 compile success를 주장하지 않음",
          "SO schema 변경이 아니라 값 변경인지 구분",
          "runtime modifier가 source asset을 mutate하지 않음",
          "HTML은 요약만 갱신하고 Markdown 원본을 먼저 유지"
        ],
        pitfalls: [
          "밸런싱은 링크/문법 검증으로 성공을 증명할 수 없습니다.",
          "값이 여러 inspector에 흩어져 있으면 source-of-truth 위치를 먼저 정해야 합니다."
        ],
        source: { label: "ContentAuthoring README.md", path: "../Guides/ContentAuthoring/README.md" },
        related: [
          { label: "DecisionLog.md", path: "../DecisionLog.md" },
          { label: "ErrorLog.md", path: "../ErrorLog.md" }
        ],
        diagram: "flowchart LR\n    Value[조정 값] --> Source{Source asset?}\n    Source -->|예| SO[SO / Prefab / Scene Inspector]\n    Source -->|아니오| Modifier[Runtime Modifier]\n    SO --> UI[Tooltip / HUD / Presentation 확인]\n    Modifier --> UI\n    UI --> Play[Manual Play Check]\n    Play --> Docs[SessionLog / Source MD update]"
      }
    ]
  },
  refactorBoard: {
    overviewDiagram: "flowchart LR\n    Source[\"RefactorBacklog README\"] --> Board[\"Presentation 리팩터 보드\"]\n    Board --> P1[\"P1 기반 분리 완료\"]\n    Board --> P2[\"P2 트리거형 후속\"]\n    Board --> P3[\"P3 국소 감시\"]\n    Board --> Blocked[\"Blocked 설계 질문\"]\n    P1 --> Context[\"관련 작업 전 구조 기억으로 사용\"]\n    P2 --> Trigger[\"트리거 발생 시 focused slice\"]\n    P3 --> Watch[\"해당 영역 편집 때만 확인\"]",
    note: "이 페이지는 RefactorBacklog/README.md의 현재 우선순위 보드를 사람이 훑기 좋게 요약한 파생 보기입니다.",
    summaryCards: [
      {
        title: "P1",
        summary: "광범위한 재구성이나 반복 콘텐츠 확장 전에 읽을 기반 분리 항목입니다. 현재 보드는 모두 resolved 상태입니다.",
        status: "5 resolved"
      },
      {
        title: "P2",
        summary: "트리거가 생기면 focused implementation으로 좁혀 진행하는 구조 작업입니다.",
        status: "4 resolved / 1 partial"
      },
      {
        title: "P3 / Blocked",
        summary: "국소 감시 항목과 설계 규칙이 필요해 아직 실행할 수 없는 항목입니다.",
        status: "watch"
      }
    ],
    groups: [
      {
        title: "P1 - 기반 분리 완료",
        note: "이미 분리된 구조입니다. 관련 영역을 다시 크게 바꿀 때 원본 Backlog 문서와 StructureMemory를 먼저 읽습니다.",
        items: [
          {
            title: "Inventory Transfer Responsibility Split",
            priority: "P1",
            status: "resolved",
            summary: "quick-move, transfer execution, rollback, relic merge, adapter, warning mapping이 UI view body에서 분리되었습니다.",
            trigger: "새 inventory container/item category, chest/world/equipment transfer, relic merge UX, transfer contract 추출",
            source: { label: "InventoryTransferResponsibilitySplit.md", path: "../RefactorBacklog/InventoryTransferResponsibilitySplit.md" }
          },
          {
            title: "Loot Reward Policy Boundary Split",
            priority: "P1",
            status: "resolved",
            summary: "chest loot, reward policy, world pickup delivery, loot pool provider, monster/grave/boss reward 경계가 helper로 분리되었습니다.",
            trigger: "새 reward source, loot exclusion rule, chest modifier, world pickup UX, loot table 확장",
            source: { label: "LootRewardPolicyBoundarySplit.md", path: "../RefactorBacklog/LootRewardPolicyBoundarySplit.md" }
          },
          {
            title: "Run Modifier Aggregation Boundary Split",
            priority: "P1",
            status: "resolved",
            summary: "RunRewardModifierSnapshot 경계와 service/delta/snapshot/aggregation/provider/rebuild 파일이 progression layer로 정리되었습니다.",
            trigger: "새 modifier source, boss/chest/grave/shop reward modifier 확장, lifecycle/API 변경",
            source: { label: "RunModifierAggregationBoundarySplit.md", path: "../RefactorBacklog/RunModifierAggregationBoundarySplit.md" }
          },
          {
            title: "Scene Run State Boundary Split",
            priority: "P1",
            status: "resolved",
            summary: "RunProgressCoordinator, ScenePortalTravelService, GamePlayDataManager, restore bootstrap 흐름이 dedicated helper로 분리되었습니다.",
            trigger: "scene/save/run transition helper boundary regression",
            source: { label: "SceneRunStateBoundarySplit.md", path: "../RefactorBacklog/SceneRunStateBoundarySplit.md" }
          },
          {
            title: "Scene Domain Bootstrap Boundary Split",
            priority: "P1",
            status: "resolved",
            summary: "title launch, scene-domain scope, return-to-title execution, camera title guard 경계가 정리되었습니다.",
            trigger: "새 title entry mode, continue-run semantics, return-to-title behavior, service bootstrap 확장",
            source: { label: "SceneDomainBootstrapBoundarySplit.md", path: "../RefactorBacklog/SceneDomainBootstrapBoundarySplit.md" }
          }
        ]
      },
      {
        title: "P2 - 트리거형 후속",
        note: "이미 닫힌 항목도 있고, presentation fallback처럼 일부만 닫힌 항목도 있습니다. 시작 조건이 없으면 독립 작업으로 벌리지 않습니다.",
        items: [
          {
            title: "Upgrade Runtime Boundary Split",
            priority: "P2",
            status: "resolved",
            summary: "UpgradeManager는 compatibility facade로 남고 purchase/completion/effect/run-start/UI/save/lifetime helper가 세부 동작을 맡습니다.",
            trigger: "새 effect ownership, save semantics, planned scene/prefab API migration",
            source: { label: "UpgradeRuntimeBoundarySplit.md", path: "../RefactorBacklog/UpgradeRuntimeBoundarySplit.md" }
          },
          {
            title: "Runtime Presentation Fallback Authoring Split",
            priority: "P2",
            status: "partially-refactored",
            summary: "runtime-created fallback audit와 대표 GlobalUIRoot validation은 완료됐지만 display letterbox는 정책상 runtime-generated로 남아 있습니다.",
            trigger: "loading/cursor/status HUD/Boss HUD polish, Canvas/raycast/sorting conflict, 새 runtime-created UI fallback",
            source: { label: "RuntimePresentationFallbackAuthoringSplit.md", path: "../RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md" }
          },
          {
            title: "Scene Run State Lifecycle Ownership Split",
            priority: "P2",
            status: "resolved",
            summary: "ScenePortalTravelService와 GamePlayDataManager는 facade로 유지하고 lifecycle/progress/volatile helper가 세부 동작을 맡습니다.",
            trigger: "planned naming/static-entry migration, 새 transition type, scene/prefab reference pass",
            source: { label: "SceneRunStateLifecycleOwnershipSplit.md", path: "../RefactorBacklog/SceneRunStateLifecycleOwnershipSplit.md" }
          },
          {
            title: "Combat Element Build-Up Source Unification",
            priority: "P2",
            status: "resolved",
            summary: "element build-up source가 attacker ElementOffenseSource로 통일됐고 legacy producer/API는 제거되었습니다.",
            trigger: "새 elemental tuning policy, legacy weapon asset/schema migration, build-up regression",
            source: { label: "CombatElementBuildUpSourceUnification.md", path: "../RefactorBacklog/CombatElementBuildUpSourceUnification.md" }
          },
          {
            title: "BossDrop Responsibility Split",
            priority: "P2",
            status: "resolved",
            summary: "BossDrop adapter와 split reward/portal components가 제거되고, BossBattleEndHandler와 authored chest/portal 방식으로 정리되었습니다.",
            trigger: "boss battle-end validator 또는 Unity import에서 일반 Inspector authoring으로 해결 불가한 migration issue 발견",
            source: { label: "BossDropResponsibilitySplit.md", path: "../RefactorBacklog/BossDropResponsibilitySplit.md" }
          }
        ]
      },
      {
        title: "P3 - 감시 항목",
        note: "독립 refactor로 시작하지 않고, 해당 컴포넌트나 기능을 이미 편집할 때만 같이 확인합니다.",
        items: [
          {
            title: "Boss HUD Special-Case Source Split",
            priority: "P3",
            status: "proposed",
            summary: "Slime Queen split-health 예외가 common Boss HUD에 남아 있지만 현재는 국소적입니다.",
            trigger: "다중 body/shared-health boss 추가, Slime Queen phase-two HUD 수정, Boss HUD health-channel rework",
            source: { label: "BossHudSpecialCaseSourceSplit.md", path: "../RefactorBacklog/BossHudSpecialCaseSourceSplit.md" }
          }
        ]
      }
    ],
    blocked: [
      {
        title: "Room/chest lock overlay count semantics",
        priority: "Blocked",
        status: "design-needed",
        summary: "split mobs, summons, transforms, delayed death presentation, no-loot enemies의 count rule이 먼저 결정되어야 합니다.",
        trigger: "lock-tracking refactor를 scope하기 전 설계 규칙 결정",
        source: { label: "BossAndMobEncounterStructure.md", path: "../StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md" }
      }
    ],
    sources: [
      { label: "RefactorBacklog/README.md", path: "../RefactorBacklog/README.md" },
      { label: "document-inventory.md", path: "../Overview/document-inventory.md" }
    ]
  }
};
