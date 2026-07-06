using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어 체력/소울하트 값을 하트 토큰 HUD로 표시한다.
/// - 기본 HUD 숨김 흐름에서 플레이어 체력 HUD 루트로 식별된다.
/// </summary>
public class PlayerHealthHeartHUD : MonoBehaviour, IDefaultHudVisibilityTarget
{
    private const string SoulHeartAttributeName = "SoulHeart";

    [Header("Refs")]
    [SerializeField] private GameObject player;
    [SerializeField] private AttributeDefinition hpDef;
    [SerializeField] private AttributeDefinition maxHpDef;
    [SerializeField] private AttributeDefinition soulHeartDef;

    [Header("Heart Setup")]
    [SerializeField] private HeartTokenUI heartTokenPrefab;
    [SerializeField] private Transform heartContainer;
    [SerializeField] private Sprite filledHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;
    [SerializeField] private Sprite soulHeartSprite;
    [SerializeField] private Color normalHeartColor = Color.white;
    [SerializeField] private Color soulHeartColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private float healthPerHeart = 1f;

    private readonly List<HeartTokenUI> heartTokens = new();

    private AttributeSet attrs;
    private AttributeDefinition resolvedSoulHeartDef;
    private int lastDisplayedMaxHearts = -1;
    private int lastDisplayedFilledHearts = -1;
    private int lastDisplayedSoulHearts = -1;
    private int lastDisplayedTotalHearts = -1;

    private void Awake()
    {
        if (heartContainer == null)
            heartContainer = transform;

        TryResolvePlayerAttributes();
        RefreshHearts(forceRebuild: true);
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        TryResolvePlayerAttributes();
        BindAttributeEvents();
        RefreshHearts(forceRebuild: true);
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        UnbindAttributeEvents();
    }

    private void OnValidate()
    {
        if (healthPerHeart <= 0f)
            healthPerHeart = 1f;
    }

    private void HandlePlayerRegistered(PlayerInteractor2D registeredPlayer)
    {
        UnbindAttributeEvents();

        player = registeredPlayer != null ? registeredPlayer.gameObject : null;
        attrs = registeredPlayer != null ? registeredPlayer.GetComponent<AttributeSet>() : null;
        resolvedSoulHeartDef = null;

        BindAttributeEvents();
        RefreshHearts(forceRebuild: true);
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D unregisteredPlayer)
    {
        if (unregisteredPlayer == null || player != unregisteredPlayer.gameObject)
            return;

        UnbindAttributeEvents();
        player = null;
        attrs = null;
        resolvedSoulHeartDef = null;
        RefreshHearts(forceRebuild: true);
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        AttributeDefinition activeSoulHeartDef = ResolveSoulHeartDefinition();
        if (attribute == hpDef || attribute == maxHpDef || attribute == activeSoulHeartDef)
            RefreshHearts(forceRebuild: attribute == maxHpDef);
    }

    private void TryResolvePlayerAttributes()
    {
        if (player == null)
        {
            var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer.gameObject
            : PlayerInteractor2D.Instance != null ? PlayerInteractor2D.Instance.gameObject : null;

            player = currentPlayer;
        }

        if (player != null)
        {
            attrs = player.GetComponent<AttributeSet>();
            resolvedSoulHeartDef = null;
        }
    }

    private void BindAttributeEvents()
    {
        if (attrs == null)
            return;

        attrs.OnAttributeChanged -= HandleAttributeChanged;
        attrs.OnAttributeChanged += HandleAttributeChanged;
    }

    private void UnbindAttributeEvents()
    {
        if (attrs == null)
            return;

        attrs.OnAttributeChanged -= HandleAttributeChanged;
    }

    private void RefreshHearts(bool forceRebuild)
    {
        if (attrs == null)
            TryResolvePlayerAttributes();

        int maxHearts = GetHeartCount(maxHpDef);
        int filledHearts = Mathf.Clamp(GetHeartCount(hpDef), 0, maxHearts);
        int soulHearts = GetHeartCount(ResolveSoulHeartDefinition());
        int totalHearts = maxHearts + soulHearts;

        if (forceRebuild || totalHearts != lastDisplayedTotalHearts)
            EnsureHeartTokenCount(totalHearts);

        if (forceRebuild
            || filledHearts != lastDisplayedFilledHearts
            || maxHearts != lastDisplayedMaxHearts
            || soulHearts != lastDisplayedSoulHearts
            || totalHearts != lastDisplayedTotalHearts)
        {
            ApplyHeartStates(filledHearts, maxHearts, soulHearts);
        }

        lastDisplayedMaxHearts = maxHearts;
        lastDisplayedFilledHearts = filledHearts;
        lastDisplayedSoulHearts = soulHearts;
        lastDisplayedTotalHearts = totalHearts;
    }

    private int GetHeartCount(AttributeDefinition attribute)
    {
        if (attrs == null || attribute == null)
            return 0;

        float value = attrs.GetAttributeValue(attribute);
        if (value <= 0f)
            return 0;

        return Mathf.CeilToInt(value / healthPerHeart);
    }

    private AttributeDefinition ResolveSoulHeartDefinition()
    {
        if (soulHeartDef != null)
            return soulHeartDef;

        if (resolvedSoulHeartDef != null)
            return resolvedSoulHeartDef;

        if (attrs == null)
            return null;

        foreach (AttributeDefinition definition in attrs.EnumerateDefinitions())
        {
            if (definition != null && definition.attributeName == SoulHeartAttributeName)
            {
                resolvedSoulHeartDef = definition;
                return resolvedSoulHeartDef;
            }
        }

        return null;
    }

    private void EnsureHeartTokenCount(int targetCount)
    {
        for (int i = heartTokens.Count; i < targetCount; i++)
        {
            HeartTokenUI newToken = CreateHeartToken();
            if (newToken == null)
                return;

            heartTokens.Add(newToken);
        }

        for (int i = 0; i < heartTokens.Count; i++)
        {
            bool shouldShow = i < targetCount;
            if (heartTokens[i] != null)
                heartTokens[i].gameObject.SetActive(shouldShow);
        }
    }

    private HeartTokenUI CreateHeartToken()
    {
        if (heartTokenPrefab == null || heartContainer == null)
            return null;

        HeartTokenUI token = Instantiate(heartTokenPrefab, heartContainer);
        token.SetSprites(filledHeartSprite, emptyHeartSprite);
        token.SetTint(normalHeartColor);
        token.SetFilled(false);
        return token;
    }

    private void ApplyHeartStates(int filledHearts, int maxHearts, int soulHearts)
    {
        int totalHearts = maxHearts + soulHearts;
        for (int i = 0; i < heartTokens.Count; i++)
        {
            HeartTokenUI token = heartTokens[i];
            if (token == null)
                continue;

            bool isVisible = i < totalHearts;
            token.gameObject.SetActive(isVisible);

            if (!isVisible)
                continue;

            bool isSoulHeart = i >= maxHearts;
            if (isSoulHeart)
            {
                Sprite sprite = soulHeartSprite != null ? soulHeartSprite : filledHeartSprite;
                token.SetSprites(sprite, sprite);
                token.SetTint(soulHeartColor);
                token.SetFilled(true);
                continue;
            }

            token.SetSprites(filledHeartSprite, emptyHeartSprite);
            token.SetTint(normalHeartColor);
            token.SetFilled(i < filledHearts);
        }
    }
}
