using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 태양의 파편 시각 오브젝트를 플레이어 주변에 생성/회전시키고 접촉 피해를 처리하는 책임을 가진다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SunFragmentOrbitController : MonoBehaviour
{
    /// <summary>
    /// 태양의 파편 궤도, 시각 프리팹, 피해 판정에 필요한 런타임 설정을 전달하는 값 묶음이다.
    /// </summary>
    public struct Config
    {
        public AbilitySystem system;
        public GameplayEffect damageEffect;
        public LayerMask targetLayers;
        public int maxFragments;
        public int burnStacks;
        public float spawnInterval;
        public float orbitRadius;
        public Vector2 orbitCenterLocalOffset;
        public float angularSpeedDegPerSec;
        public GameObject fragmentPrefab;
        public float fragmentSize;
        public float contactRadius;
    }

    private static readonly ElementDamageResult[] NoElementBuildUp = Array.Empty<ElementDamageResult>();
    private static readonly Collider2D[] HitBuffer = new Collider2D[32];
    private static Sprite squareSprite;
    private readonly List<GameObject> fragments = new();
    private Config config;
    private UnityEngine.Object activeToken;
    private float spawnElapsed;
    private float angleDeg;

    public void EnableForToken(UnityEngine.Object token, Config nextConfig)
    {
        DisableInternal();
        activeToken = token;
        config = nextConfig;
        spawnElapsed = 0f;
    }

    public void DisableForToken(UnityEngine.Object token)
    {
        if (activeToken != null && token != null && activeToken != token)
            return;
        DisableInternal();
    }

    private void Update()
    {
        if (activeToken == null || config.system == null)
            return;

        spawnElapsed += Time.deltaTime;
        float interval = Mathf.Max(0.05f, config.spawnInterval);
        while (fragments.Count < Mathf.Max(1, config.maxFragments) && spawnElapsed >= interval)
        {
            spawnElapsed -= interval;
            SpawnFragment();
        }

        angleDeg = Mathf.Repeat(angleDeg + config.angularSpeedDegPerSec * Time.deltaTime, 360f);
        Vector3 center = transform.TransformPoint(config.orbitCenterLocalOffset);
        for (int i = fragments.Count - 1; i >= 0; i--)
        {
            GameObject fragment = fragments[i];
            if (fragment == null)
            {
                fragments.RemoveAt(i);
                continue;
            }

            float angle = angleDeg + 360f / fragments.Count * i;
            float radians = angle * Mathf.Deg2Rad;
            fragment.transform.position = center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * config.orbitRadius;
            if (TryHit(fragment))
            {
                fragments.RemoveAt(i);
                Destroy(fragment);
                spawnElapsed = 0f;
            }
        }
    }

    private void SpawnFragment()
    {
        GameObject fragment = CreateFragmentVisual(out Vector3 authoredScale);
        fragment.transform.SetParent(transform, false);
        fragment.transform.localPosition = Vector3.zero;
        fragment.transform.localRotation = Quaternion.identity;
        fragment.transform.localScale = authoredScale * Mathf.Max(0.05f, config.fragmentSize);
        fragments.Add(fragment);
    }

    private GameObject CreateFragmentVisual(out Vector3 authoredScale)
    {
        if (config.fragmentPrefab != null)
        {
            GameObject instance = Instantiate(config.fragmentPrefab);
            instance.name = config.fragmentPrefab.name;
            authoredScale = instance.transform.localScale;
            return instance;
        }

        GameObject fragment = new("SunFragment_Square");
        SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = new Color(1f, 0.48f, 0.05f, 1f);
        renderer.sortingLayerName = "Projectile";
        renderer.sortingOrder = 4;
        authoredScale = Vector3.one;
        return fragment;
    }

    private bool TryHit(GameObject fragment)
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            fragment.transform.position,
            Mathf.Max(0.01f, config.contactRadius),
            HitBuffer,
            config.targetLayers);

        for (int i = 0; i < count; i++)
        {
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(HitBuffer[i]);
            if (target == null || target == gameObject)
                continue;

            IStatProvider provider = AbilityStatProviderFactory.Create(config.system);
            float fire = provider != null ? Mathf.Max(0f, provider.Get(StatId.FireFinal)) : 0f;
            DamageResult result = DamageFormulaUtil.PostProcess(provider, fire, 0f);
            CombatDamageAction.ApplyDamageAndEmitHit(
                system: config.system,
                spec: null,
                damageEffect: config.damageEffect,
                knockbackEffect: null,
                target: target,
                finalHpDamage: Mathf.Round(result.hpDamage),
                finalStaggerBuildUp: 0f,
                finalKnockbackImpulse: 0f,
                hitConfirmedTag: null,
                hitWorldPosition: fragment.transform.position,
                causer: fragment,
                isCriticalHit: result.isCrit,
                elementBuildUps: NoElementBuildUp,
                hasResolvedElementBuildUps: true);
            BurnStatus2D.Apply(target, config.system, config.damageEffect, gameObject, Mathf.Max(1, config.burnStacks));
            return true;
        }

        return false;
    }

    private void DisableInternal()
    {
        activeToken = null;
        for (int i = 0; i < fragments.Count; i++)
            if (fragments[i] != null)
                Destroy(fragments[i]);
        fragments.Clear();
        spawnElapsed = 0f;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            squareSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            squareSprite.name = "Runtime_SunFragmentSquare";
        }
        return squareSprite;
    }

    private void OnDestroy() => DisableInternal();
}
