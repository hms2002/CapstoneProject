using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

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
        // 이미 켜져 있으면 먼저 정리
        DisableInternal();

        _active = true;
        _activeToken = token;

        SpawnFeathers();
    }

    public void DisableForToken(Object token)
    {
        // 다른 토큰이면 무시(혹시나 여러 유물/토큰 상황 대비)
        if (_activeToken != null && token != null && _activeToken != token) return;
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
        if (_cfg.featherPrefab == null) return;

        for (int i = 0; i < _cfg.featherCount; i++)
        {
            var f = Instantiate(_cfg.featherPrefab, transform);
            f.name = $"FeatherOrbit_{i}";
            f.Bind(this, index: i);
            _feathers.Add(f);
        }
    }

    private void Update()
    {
        if (!_active) return;
        if (_feathers.Count == 0) return;

        float moveMult = GetMoveSpeedMultX1();
        float angular = _cfg.baseAngularSpeedDegPerSec * moveMult;

        _angleDeg += angular * Time.deltaTime;
        if (_angleDeg >= 360f) _angleDeg -= 360f;

        int n = _feathers.Count;
        for (int i = 0; i < n; i++)
        {
            var f = _feathers[i];
            if (f == null) continue;

            float a = _angleDeg + (360f / n) * i;
            var rad = a * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad),0f) * _cfg.radius;
            f.transform.position = transform.position + offset;

            // (선택) 바라보는 방향 연출
            //f.transform.rotation = Quaternion.LookRotation(offset.normalized, -Vector3.forward);
        }
    }

    // ---- Feather가 호출하는 API ----

    public AbilitySystem System => _system;
    public GameplayEffect DamageEffect => _cfg.damageEffect;
    public GE_Knockback_Spec KnockbackEffect => _cfg.knockbackEffect;
    public GameplayTag HitConfirmedTag => _cfg.hitConfirmedTag;

    public float GetMoveSpeedMultX1()
    {
        // StatBindings 기반(권장)
        if (_statProvider != null)
        {
            float v = _statProvider.Get(_cfg.moveSpeedFinalStatId);
            return (v > 0f) ? v : 1f;
        }
        return 1f;
    }

    public float GetPerTargetHitCooldown()
    {
        // “깃털 공격속도 = 이속 비례” 구현
        // 실제 쿨다운 = base / MoveSpeedFinal
        float ms = GetMoveSpeedMultX1();
        return _cfg.basePerTargetHitCooldown / Mathf.Max(0.01f, ms);
    }

    public float ComputeHpDamage()
    {
        // ATK * coef
        if (_statProvider != null)
        {
            float atk = _statProvider.Get(_cfg.attackStatId);
            return Mathf.Max(0f, atk) * _cfg.damageCoef;
        }
        return 0f;
    }

    public float KnockbackImpulse => Mathf.Max(0f, _cfg.knockbackImpulse);

    private void OnDestroy()
    {
        DisableInternal();
    }
}
