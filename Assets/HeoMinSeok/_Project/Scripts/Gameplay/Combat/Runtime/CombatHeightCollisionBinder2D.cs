using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - CombatHeightState2D의 지상/공중 상태를 EntityCollisionProfile2D의 body 충돌 모드에 연결한다.
    /// - 높이 상태와 collider 제어 책임을 분리한 채, 공중일 때 actor 통과 같은 정책만 얇게 적용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHeightCollisionBinder2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CombatHeightState2D heightState;
        [SerializeField] private EntityCollisionProfile2D collisionProfile;

        [Header("Modes")]
        [SerializeField] private EntityCollisionProfile2D.BodyCollisionMode groundedMode = EntityCollisionProfile2D.BodyCollisionMode.Normal;
        [SerializeField] private EntityCollisionProfile2D.BodyCollisionMode airborneMode = EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors;
        [SerializeField] private bool restoreProfileDefaultWhenGrounded = true;

        private void Awake()
        {
            CacheReferences();
            ApplyCurrentMode();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (heightState != null)
                heightState.Changed += HandleHeightChanged;

            ApplyCurrentMode();
        }

        private void OnDisable()
        {
            if (heightState != null)
                heightState.Changed -= HandleHeightChanged;
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void CacheReferences()
        {
            if (heightState == null)
                heightState = GetComponent<CombatHeightState2D>();

            if (collisionProfile == null)
                collisionProfile = GetComponent<EntityCollisionProfile2D>();
        }

        private void HandleHeightChanged(CombatHeightState2D _)
        {
            ApplyCurrentMode();
        }

        /// <summary>현재 높이 상태를 읽어 body collision profile을 즉시 동기화한다.</summary>
        private void ApplyCurrentMode()
        {
            if (heightState == null || collisionProfile == null)
                return;

            if (heightState.IsAirborne)
            {
                collisionProfile.SetBodyCollisionMode(airborneMode);
                return;
            }

            if (restoreProfileDefaultWhenGrounded)
                collisionProfile.RestoreDefaultMode();
            else
                collisionProfile.SetBodyCollisionMode(groundedMode);
        }
    }
}
