using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class MagicStonePickup : MonoBehaviour
{
    [Header("Settings")]
    public int amount = 1;

    [Header("Magnet Effect")]
    public float magnetSpeed = 10f;       // 날아가는 속도
    public float delayBeforeMagnet = 0.5f;// 드롭 후 대기 시간

    private Transform targetPlayer;

    private void Awake()
    {
        // 획득 판정을 위해 트리거 설정
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnEnable()
    {
        // 활성화되면 자석 로직 시작
        StartCoroutine(MagnetRoutine());
    }

    private IEnumerator MagnetRoutine()
    {
        // 1. 드롭 연출을 위해 잠시 대기
        yield return new WaitForSeconds(delayBeforeMagnet);

        // 2. 플레이어 찾기 (전역 참조 사용)
        if (SampleTopDownPlayer.Instance != null)
        {
            targetPlayer = SampleTopDownPlayer.Instance.transform;
        }

        // 플레이어가 없으면 로직 종료 (안전장치)
        if (targetPlayer == null) yield break;

        // 3. 거리 상관없이 플레이어에게 무조건 이동
        while (targetPlayer != null)
        {
            // 점차 빨라지는 연출을 원하면 magnetSpeed에 Time.deltaTime을 더해줄 수도 있음
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPlayer.position,
                magnetSpeed * Time.deltaTime
            );

            // 한 프레임 대기
            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 몸에 닿으면 획득 처리
        if (collision.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.AddMagicStone(amount);
        }

        // TODO: 획득 효과음(Sound)이나 파티클(VFX) 추가 가능

        Destroy(gameObject);
    }
}