using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - Feather Orbit 유물의 런타임 컨트롤러다.
/// - 깃털 생성/제거, 공전 위치 갱신, 이속 기반 회전속도/재타격 쿨다운/피해량 계산을 담당한다.
/// - Feather가 실제 적중할 때 사용할 공통 CombatHitPayload를 만들어 제공한다.
/// </summary>
public class FeatherOrbitController : MonoBehaviour
{
    [System.Serializable]
    public struct Config
    {
        public GameObject owner;
        public Object token;

        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public StatId attackStatId;
        public float damageCoef;
        public float knockbackImpulse;
        public GameplayTag hitConfirmedTag;

        public FeatherOrbitFeather featherPrefab;
        public int featherCount;
        public float radius;
        public Vector2 orbitCenterLocalOffset;
        public float baseAngularSpeedDegPerSec;
        public float basePerTargetHitCooldown;

        public StatId moveSpeedFinalStatId;
    }

    private Config _cfg;
    private bool _active;
    private Object _activeToken;

    private readonly List<FeatherOrbitFeather> _feathers = new();
    private float _angleDeg;

    private AbilitySystem _system;
    private StatTypeBindings _bindings;
    private AttributeStatProvider _statProvider;

    public void Setup(Config cfg)
    {
        _cfg = cfg;
        _system = GetComponent<AbilitySystem>();

        if (_system != null && _system.DamageProfile != null)
            _bindings = _system.DamageProfile.GetStatBindings();

        _statProvider = (_system != null && _bindings != null)
            ? new AttributeStatProvider(_system.AttributeSet, _bindings)
            : null;
    }

    public void EnableForToken(Object token)
    {
        DisableInternal();

        _active = true;
        _activeToken = token;
        SpawnFeathers();
    }

    public void DisableForToken(Object token)
    {
        if (_activeToken != null && token != null && _activeToken != token)
            return;

        DisableInternal();
    }

    private void DisableInternal()
    {
        _active = false;
        _activeToken = null;

        for (int i = 0; i < _feathers.Count; i++)
        {
            if (_feathers[i] != null)
                Destroy(_feathers[i].gameObject);
        }

        _feathers.Clear();
    }

    private void SpawnFeathers()
    {
        if (_cfg.featherPrefab == null)
            return;

        for (int i = 0; i < _cfg.featherCount; i++)
        {
            var feather = Instantiate(_cfg.featherPrefab, transform);
            feather.name = $"FeatherOrbit_{i}";
            feather.Bind(this, index: i);
            _feathers.Add(feather);
        }
    }

    private void Update()
    {
        if (!_active) return;
        if (_feathers.Count == 0) return;

        float moveMult = GetMoveSpeedMultX1();
        float angular = _cfg.baseAngularSpeedDegPerSec * moveMult;

        _angleDeg += angular * Time.deltaTime;
        if (_angleDeg >= 360f)
            _angleDeg -= 360f;

        int count = _feathers.Count;
        Vector3 orbitCenter = ResolveOrbitCenter();
        for (int i = 0; i < count; i++)
        {
            var feather = _feathers[i];
            if (feather == null) continue;

            float angle = _angleDeg + (360f / count) * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _cfg.radius;
            feather.transform.position = orbitCenter + offset;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 플레이어 루트 피벗이 발밑에 있어도 깃털이 의도한 몸 중앙을 기준으로 공전하도록 중심점을 계산한다.
    /// - 유물 데이터의 local offset을 owner/컨트롤러 Transform 기준 월드 좌표로 변환한다.
    /// </summary>
    private Vector3 ResolveOrbitCenter()
    {
        Transform ownerTransform = _cfg.owner != null ? _cfg.owner.transform : transform;
        Vector3 localOffset = new Vector3(_cfg.orbitCenterLocalOffset.x, _cfg.orbitCenterLocalOffset.y, 0f);
        return ownerTransform.TransformPoint(localOffset);
    }

    public AbilitySystem System => _system;
    public GameplayEffect DamageEffect => _cfg.damageEffect;
    public GE_Knockback_Spec KnockbackEffect => _cfg.knockbackEffect;
    public GameplayTag HitConfirmedTag => _cfg.hitConfirmedTag;

    public float GetMoveSpeedMultX1()
    {
        if (_statProvider != null)
        {
            float value = _statProvider.Get(_cfg.moveSpeedFinalStatId);
            return value > 0f ? value : 1f;
        }

        return 1f;
    }

    public float GetPerTargetHitCooldown()
    {
        float moveSpeed = GetMoveSpeedMultX1();
        return _cfg.basePerTargetHitCooldown / Mathf.Max(0.01f, moveSpeed);
    }

    public float ComputeHpDamage()
    {
        if (_statProvider != null)
        {
            float atk = _statProvider.Get(_cfg.attackStatId);
            return Mathf.Max(0f, atk) * _cfg.damageCoef;
        }

        return 0f;
    }

    public float KnockbackImpulse => Mathf.Max(0f, _cfg.knockbackImpulse);

    /// <summary>
    /// 책임 :
    /// - 현재 Feather Orbit 상태를 바탕으로 실제 타격에 사용할 payload를 구성한다.
    /// - 유물은 특정 AbilitySpec이 없으므로 sourceSpec은 null로 유지한다.
    /// </summary>
    public bool TryBuildHitPayload(GameObject causer, out CombatHitPayload payload)
    {
        payload = null;

        if (_system == null || _cfg.damageEffect == null)
            return false;

        float hpDamage = ComputeHpDamage();
        if (hpDamage <= 0f)
            return false;

        payload = new CombatHitPayload
        {
            sourceSystem = _system,
            sourceSpec = null,
            damageEffect = _cfg.damageEffect,
            knockbackEffect = _cfg.knockbackEffect,
            finalHpDamage = hpDamage,
            finalStaggerBuildUp = 0f,
            elementBuildUps = null,
            finalKnockbackImpulse = KnockbackImpulse,
            hitConfirmedTag = _cfg.hitConfirmedTag,
            causer = causer != null ? causer : gameObject
        };

        return true;
    }

    private void OnDestroy()
    {
        DisableInternal();
    }
}
