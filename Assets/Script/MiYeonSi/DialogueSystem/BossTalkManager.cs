using UnityEngine;
using System.Collections;
using System.Collections.Generic; // [추가] List 사용!
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
        if (TempPlayer.Instance != null)
            TempPlayer.Instance.SetInteractState(InteractState.Talking);

        bossCam.Priority = 20;
        yield return new WaitForSeconds(0.1f);
        yield return new WaitUntil(() => !brain.IsBlending);

        if (DialogueController.Instance != null)
        {
            // [에러 해결!] 보스 데이터 1명을 List라는 봉투에 담아서 제출!
            List<NPCData> participants = new List<NPCData>() { npcData };
            DialogueController.Instance.EnterDialogueMode(inkJSON, participants);
        }
        else
        {
            Debug.LogError("[BossTalkManager] DialogueController 인스턴스를 찾을 수 없습니다!");
        }

        yield return new WaitUntil(() => DialogueController.Instance == null || !DialogueController.Instance.isPlaying);

        bossCam.Priority = 5;
        yield return new WaitForSeconds(0.1f);
        yield return new WaitUntil(() => !brain.IsBlending);

        if (TempPlayer.Instance != null)
            TempPlayer.Instance.SetInteractState(InteractState.Idle);

        var bossDrop = GetComponent<BossDrop>();
        if (bossDrop != null)
        {
            bossDrop.OnBossDead();
        }
    }
}