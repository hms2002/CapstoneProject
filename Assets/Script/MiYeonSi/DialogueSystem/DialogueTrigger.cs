using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Header("데이터 설정")]
    [SerializeField] private NPCData npcData;
    [SerializeField] private TextAsset inkJSON;

    [Header("시각적 가이드")]
    [SerializeField] private GameObject visualCue; // 기존 머리 위 F 아이콘

    [SerializeField] private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propBlock;

    // 친구가 준 셰이더의 프로퍼티 이름에 맞춤
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");

    // [추가] 이 NPC가 가진 기능 컨트롤러 (없을 수도 있음)
    private NPCFeatureController featureController;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        visualCue.SetActive(false);

        // [추가] 같은 오브젝트에 있는 FeatureController 가져오기
        featureController = GetComponent<NPCFeatureController>();

        // 초기 상태: 테두리 끄기
        OnUnHighlight();
    }

    // [Nearby/Leave] 트리거 범위 내 진입 시 visualCue 제어
    public void OnPlayerNearby() => visualCue?.SetActive(true);
    public void OnPlayerLeave() => visualCue?.SetActive(false);

    // [Highlight] 최단 거리 타겟 선정 시 셰이더 테두리 켜기
    public void OnHighlight()
    {
        // Debug.Log($"{gameObject.name}: 하이라이트 실행됨!");
        if (spriteRenderer == null) return;

        spriteRenderer.GetPropertyBlock(propBlock);
        // _OutlineEnabled를 1.0으로 설정하여 테두리 활성화
        propBlock.SetFloat(OutlineEnabledID, 1f);
        // 필요하다면 여기서 색상을 코드로 변경할 수도 있습니다.
        // propBlock.SetColor(OutlineColorID, Color.white); 
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    // 타겟 해제 시 테두리 끄기
    public void OnUnHighlight()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, 0f); // 0.0으로 설정하여 비활성화
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    public void OnPlayerInteract(TempPlayer player)
    {
        if (CanInteract(player))
        {
            // [수정] 대화 시작 시 featureController도 함께 전달
            DialogueManager.GetInstance().EnterDialogueMode(inkJSON, npcData, DialogueManager.Direction.Left, featureController);
        }
    }

    public bool CanInteract(TempPlayer player)
    {
        return !DialogueManager.GetInstance().dialogueIsPlaying;
    }

    public void GetInteract(string text) { }
    public InteractState GetInteractType() => InteractState.Talking;
    public string GetInteractDescription() => "대화하기";
}