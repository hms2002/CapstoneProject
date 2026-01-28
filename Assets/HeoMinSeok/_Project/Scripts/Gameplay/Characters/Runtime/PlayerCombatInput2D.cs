using UnityEngine;

namespace UnityGAS.Sample
{
    public class PlayerCombatInput2D : MonoBehaviour
    {
        [SerializeField] private AbilitySystem abilitySystem;
        [SerializeField] private AbilityDefinition swordCombo;

        public Vector2 AimDirection { get; private set; }

        private Camera cam;

        private void Awake()
        {
            if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
            cam = Camera.main;
        }

        private void Update()
        {
            UpdateAim();

            bool holding = Input.GetMouseButton(0);

            // “지금 비어있을 때만” 호출 → 버퍼 스팸 방지 + 홀드 연속 발동
            if (holding && abilitySystem != null && !abilitySystem.IsBusy)
            {
                abilitySystem.TryActivateAbility(swordCombo, null);
            }
        }

        private void UpdateAim()
        {
            if (cam == null) return;
            var world = cam.ScreenToWorldPoint(Input.mousePosition);
            var dir = (Vector2)(world - transform.position);
            AimDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }
    }
}
