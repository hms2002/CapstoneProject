using UnityEngine;
using System.Collections;
using Unity.Cinemachine;
using DG.Tweening;

public class BossTalkManager : MonoBehaviour
{
    [Header("데이터 설정")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private NPCData npcData;

    [Header("카메라 설정")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;

    [Header("보스 등장 연출 설정")]
    [SerializeField] private DialogueManager.Direction bossEntryDir = DialogueManager.Direction.Left;

    private CinemachineBrain brain;

    void Awake()
    {
        if (Camera.main != null)
            brain = Camera.main.GetComponent<CinemachineBrain>();
    }

    void Start()
    {
        if (bossCam == null || playerCam == null || brain == null) return;
        StartCoroutine(EncounterSequence());
    }

    IEnumerator EncounterSequence()
    {
        // 1. 플레이어 정지
        if (TempPlayer.Instance != null)
            TempPlayer.Instance.SetInteractState(InteractState.Talking);

        // 2. 카메라 이동
        bossCam.Priority = 20;
        yield return new WaitForSeconds(0.1f);
        yield return new WaitUntil(() => !brain.IsBlending);

        // 3. 카메라 도착 후 UI 연출 및 대화 시작
        // EnterDialogueMode 내부에서 연출 시퀀스가 실행됩니다.
        DialogueManager.GetInstance().EnterDialogueMode(inkJSON, npcData, bossEntryDir);

        // 4. 대화 종료 대기
        yield return new WaitUntil(() => !DialogueManager.GetInstance().dialogueIsPlaying);

        // 5. 카메라 복구
        bossCam.Priority = 5;
        yield return new WaitForSeconds(0.1f);
        yield return new WaitUntil(() => !brain.IsBlending);

        if (TempPlayer.Instance != null)
            TempPlayer.Instance.SetInteractState(InteractState.Idle);

        GetComponent<BossDrop>().OnBossDead();
    }
}