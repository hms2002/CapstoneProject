using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 보스 패턴 없이 술/불 장판 생성, 점화, 흡수 이동 느낌을 수동 테스트할 수 있게 한다.
    /// - 프로덕션 전투 규칙이 아니라 장판 시스템 authoring 검증용 도구다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleDebugSpawner : MonoBehaviour
    {
        private const float DefaultAlcoholProjectileSpeed = 1f;
        private const float DefaultFireProjectileSpeed = 1f;

        [Header("Prefabs")]
        [SerializeField] private AlcoholPuddleArea alcoholPuddlePrefab;
        [SerializeField] private FirePuddleArea firePuddlePrefab;

        [Header("Input")]
        [SerializeField] private KeyCode spawnAlcoholKey = KeyCode.Alpha7;
        [SerializeField] private KeyCode spawnFireKey = KeyCode.Alpha8;
        [SerializeField] private KeyCode igniteNearestAlcoholKey = KeyCode.Alpha9;
        [SerializeField] private KeyCode absorbNearestPuddleKey = KeyCode.Alpha0;
        [SerializeField] private KeyCode absorbNearestAlcoholKey = KeyCode.Minus;
        [SerializeField] private KeyCode absorbNearestFireKey = KeyCode.Equals;

        [Header("Absorb Test")]
        [SerializeField] private Transform absorbAnchor;
        [SerializeField, Min(0.01f)] private float alcoholAbsorbSpeed = 7f;
        [SerializeField, Min(0.01f)] private float fireAbsorbSpeed = 4f;
        [SerializeField] private bool logDebugMessages = true;

        private Camera cachedCamera;

        private void Update()
        {
            if (Input.GetKeyDown(spawnAlcoholKey))
                SpawnAlcohol();

            if (Input.GetKeyDown(spawnFireKey))
                SpawnFire();

            if (Input.GetKeyDown(igniteNearestAlcoholKey))
            {
                LogDebug($"입력 감지: {igniteNearestAlcoholKey} -> 가장 가까운 술 장판 점화");
                IgniteNearestAlcohol();
            }

            if (Input.GetKeyDown(absorbNearestPuddleKey))
            {
                LogDebug($"입력 감지: {absorbNearestPuddleKey} -> 가장 가까운 장판 흡수");
                AbsorbNearestPuddle();
            }

            if (Input.GetKeyDown(absorbNearestAlcoholKey))
            {
                LogDebug($"입력 감지: {absorbNearestAlcoholKey} -> 가장 가까운 술 장판 흡수");
                AbsorbNearestAlcohol();
            }

            if (Input.GetKeyDown(absorbNearestFireKey))
            {
                LogDebug($"입력 감지: {absorbNearestFireKey} -> 가장 가까운 불 장판 흡수");
                AbsorbNearestFire();
            }
        }

        private void SpawnAlcohol()
        {
            if (alcoholPuddlePrefab == null)
            {
                LogDebug("술 장판 생성 실패: alcoholPuddlePrefab이 연결되어 있지 않습니다.");
                return;
            }

            Vector3 spawnPosition = ResolvePointerWorldPosition();
            AlcoholPuddleArea puddle = Instantiate(alcoholPuddlePrefab, spawnPosition, Quaternion.identity);
            LogDebug($"술 장판 생성: {puddle.name}, position={spawnPosition}");
        }

        private void SpawnFire()
        {
            if (firePuddlePrefab == null)
            {
                LogDebug("불 장판 생성 실패: firePuddlePrefab이 연결되어 있지 않습니다.");
                return;
            }

            Vector3 spawnPosition = ResolvePointerWorldPosition();
            FirePuddleArea puddle = Instantiate(firePuddlePrefab, spawnPosition, Quaternion.identity);
            LogDebug($"불 장판 생성: {puddle.name}, position={spawnPosition}");
        }

        private void IgniteNearestAlcohol()
        {
            AlcoholPuddleArea alcohol = FindNearestPuddle<AlcoholPuddleArea>();
            if (alcohol == null)
            {
                LogDebug("점화 테스트 실패: 가장 가까운 술 장판을 찾지 못했습니다.");
                return;
            }

            LogDebug($"점화 테스트 시작: {alcohol.name}");
            alcohol?.RequestIgnite();
        }

        private void AbsorbNearestPuddle()
        {
            PuddleAreaBase puddle = FindNearestPuddle<PuddleAreaBase>();
            AbsorbPuddle(puddle);
        }

        private void AbsorbNearestAlcohol()
        {
            AlcoholPuddleArea puddle = FindNearestPuddle<AlcoholPuddleArea>();
            AbsorbPuddle(puddle);
        }

        private void AbsorbNearestFire()
        {
            FirePuddleArea puddle = FindNearestPuddle<FirePuddleArea>();
            AbsorbPuddle(puddle);
        }

        private void AbsorbPuddle(PuddleAreaBase puddle)
        {
            if (absorbAnchor == null)
            {
                LogDebug("흡수 테스트 실패: absorbAnchor가 연결되어 있지 않습니다.");
                return;
            }

            if (puddle == null)
            {
                LogDebug("흡수 테스트 실패: 마우스 위치 근처에서 장판을 찾지 못했습니다.");
                return;
            }

            float speed = ResolveAbsorbSpeed(puddle.ElementType);

            LogDebug($"흡수 테스트 시작: {puddle.name}, element={puddle.ElementType}, speed={speed}");
            puddle.EnterAbsorbProjectile(absorbAnchor, speed, arrived =>
            {
                LogDebug($"흡수 테스트 도착: {arrived.name}");
                arrived.MarkConsumed();
                arrived.gameObject.SetActive(false);
            });
        }

        private T FindNearestPuddle<T>() where T : PuddleAreaBase
        {
            PuddleManager manager = PuddleManager.ResolveForScene();
            if (manager == null)
            {
                LogDebug("장판 검색 실패: PuddleManager를 찾거나 생성하지 못했습니다.");
                return null;
            }

            Vector3 pointerPosition = ResolvePointerWorldPosition();
            T nearest = null;
            float nearestDistanceSqr = float.PositiveInfinity;

            var puddles = manager.Puddles;
            int candidateCount = 0;
            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i] is not T candidate || candidate == null)
                    continue;

                candidateCount++;
                float distanceSqr = (candidate.transform.position - pointerPosition).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
                nearest = candidate;
            }

            string typeName = typeof(T).Name;
            string nearestName = nearest != null ? nearest.name : "none";
            LogDebug($"장판 검색: type={typeName}, registered={puddles.Count}, candidates={candidateCount}, pointer={pointerPosition}, nearest={nearestName}");
            return nearest;
        }

        private float ResolveAbsorbSpeed(PuddleElementType elementType)
        {
            if (elementType == PuddleElementType.Alcohol)
                return alcoholAbsorbSpeed > 0f ? alcoholAbsorbSpeed : DefaultAlcoholProjectileSpeed;

            return fireAbsorbSpeed > 0f ? fireAbsorbSpeed : DefaultFireProjectileSpeed;
        }

        private void LogDebug(string message)
        {
            if (!logDebugMessages)
                return;

            Debug.Log($"[PuddleDebugSpawner] {message}", this);
        }

        private Vector3 ResolvePointerWorldPosition()
        {
            cachedCamera ??= Camera.main;
            if (cachedCamera == null)
                return transform.position;

            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(cachedCamera.transform.position.z - transform.position.z);
            Vector3 world = cachedCamera.ScreenToWorldPoint(screen);
            world.z = transform.position.z;
            return world;
        }
    }
}
