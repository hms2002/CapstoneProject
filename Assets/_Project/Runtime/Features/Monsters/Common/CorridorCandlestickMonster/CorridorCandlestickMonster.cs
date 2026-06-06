using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임:
/// 취룡 복도용 촛대 몬스터의 공통 Mob FSM 본체 역할을 담당하고,
/// 살아있는 동안 술 장판과 접촉하면 불 장판으로 전환시키는 고유 기믹을 제공한다.
/// 추적/공격 판단은 같은 오브젝트의 helper/AD 구성에 위임한다.
/// </summary>
public class CorridorCandlestickMonster : Mob
{
    private static readonly SoundRef DieSound = SoundRef.FromKey("sound_candleMonsger_Die");

    [Header("Ignition")]
    [Tooltip("켜두면 살아있는 촛대 몬스터가 술 장판에 닿을 때 점화를 요청합니다.")]
    [SerializeField] private bool igniteAlcoholPuddlesOnContact = true;

    [Tooltip("OnTriggerStay/OnCollisionStay가 과도하게 점화 요청을 반복하지 않도록 제한하는 최소 간격입니다.")]
    [SerializeField, Min(0f)] private float contactIgnitionInterval = 0.1f;

    /// <summary>
    /// 책임:
    /// 촛대 몬스터가 술 장판 trigger에 진입하면 즉시 점화를 요청한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryIgniteAlcoholPuddle(other);
    }

    /// <summary>
    /// 책임:
    /// 이미 겹친 상태로 생성되거나 collider enter를 놓친 경우에도 접촉 점화를 보완한다.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        TryIgniteAlcoholPuddle(other);
    }

    /// <summary>
    /// 책임:
    /// 술 장판이 trigger가 아닌 collider로 구성되어도 촛대 접촉 점화가 동작하게 한다.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgniteAlcoholPuddle(collision != null ? collision.collider : null);
    }

    /// <summary>
    /// 책임:
    /// 술 장판이 trigger가 아닌 collider로 구성되어도 지속 접촉 점화가 동작하게 한다.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        TryIgniteAlcoholPuddle(collision != null ? collision.collider : null);
    }

    /// <summary>
    /// 책임:
    /// 접촉한 collider가 지면 상태 술 장판이면 중복 요청을 줄이면서 점화를 요청한다.
    /// </summary>
    private void TryIgniteAlcoholPuddle(Collider2D other)
    {
        if (!igniteAlcoholPuddlesOnContact || IsDead || other == null)
            return;

        AlcoholPuddleArea alcohol = other.GetComponentInParent<AlcoholPuddleArea>();
        if (alcohol == null || alcohol.Mode != PuddleAreaMode.Ground)
            return;

        float interval = Mathf.Max(0f, contactIgnitionInterval);
        if (interval > 0f && Time.time < nextContactIgnitionTime)
            return;

        nextContactIgnitionTime = Time.time + interval;
        alcohol.RequestIgnite();
    }

    private float nextContactIgnitionTime;

    /// <summary>촛대 몬스터 사망 시 전용 파괴 사운드를 재생합니다.</summary>
    protected override void OnDeathStarted()
    {
        SoundPlaybackUtility.Play(DieSound, causer: gameObject, position: transform.position, sourceObject: this);
        base.OnDeathStarted();
    }
}
