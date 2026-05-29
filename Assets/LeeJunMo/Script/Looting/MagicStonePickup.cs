using UnityEngine;
using System.Collections;

/// <summary>
/// 책임:
/// - 드롭된 마정석을 플레이어 쪽으로 끌어당기고, 공식 PickupCollector2D와 접촉했을 때 재화로 적립한다.
/// - 플레이어 root/tag 구조 변화에 영향받지 않도록 수집 판정은 수집 전용 콜라이더 컴포넌트에 위임한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MagicStonePickup : MonoBehaviour
{
    [Header("Settings")]
    public int amount = 1;

    [Header("Magnet Effect")]
    public float magnetSpeed = 10f;       // 날아가는 속도
    public float delayBeforeMagnet = 0.5f;// 드롭 후 대기 시간

    private Transform targetPlayer;
    private bool collected;

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

    private void OnDisable()
    {
        StopAllCoroutines();
        targetPlayer = null;
    }

    private IEnumerator MagnetRoutine()
    {
        // 1. 드롭 연출을 위해 잠시 대기
        yield return new WaitForSeconds(delayBeforeMagnet);

        // 2. 플레이어 찾기 (스폰 완료될 때까지 대기)
        while (targetPlayer == null)
        {
            targetPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
            if (targetPlayer != null)
                break;

            yield return null;
        }

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
        TryCollect(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void TryCollect(Collider2D collision)
    {
        if (collected || collision == null)
            return;

        if (collision.GetComponent<PickupCollector2D>() == null)
            return;

        Collect();
    }

    private void Collect()
    {
        if (collected)
            return;

        collected = true;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMagicStone(amount);
        }

        // TODO: 획득 효과음(Sound)이나 파티클(VFX) 추가 가능

        Destroy(gameObject);
    }
}
