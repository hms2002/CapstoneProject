using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class PlayerHealthHeartHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject player;
    [SerializeField] private AttributeDefinition hpDef;
    [SerializeField] private AttributeDefinition maxHpDef;

    [Header("Heart Setup")]
    [SerializeField] private HeartTokenUI heartTokenPrefab;
    [SerializeField] private Transform heartContainer;
    [SerializeField] private Sprite filledHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;
    [SerializeField] private float healthPerHeart = 1f;

    private readonly List<HeartTokenUI> heartTokens = new();

    private AttributeSet attrs;
    private int lastDisplayedMaxHearts = -1;
    private int lastDisplayedFilledHearts = -1;

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

    private void HandlePlayerRegistered(SampleTopDownPlayer registeredPlayer)
    {
        UnbindAttributeEvents();

        player = registeredPlayer != null ? registeredPlayer.gameObject : null;
        attrs = registeredPlayer != null ? registeredPlayer.GetComponent<AttributeSet>() : null;

        BindAttributeEvents();
        RefreshHearts(forceRebuild: true);
    }

    private void HandlePlayerUnregistered(SampleTopDownPlayer unregisteredPlayer)
    {
        if (unregisteredPlayer == null || player != unregisteredPlayer.gameObject)
            return;

        UnbindAttributeEvents();
        player = null;
        attrs = null;
        RefreshHearts(forceRebuild: true);
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute == hpDef || attribute == maxHpDef)
            RefreshHearts(forceRebuild: attribute == maxHpDef);
    }

    private void TryResolvePlayerAttributes()
    {
        if (player == null)
        {
            var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer.gameObject
                : SampleTopDownPlayer.Instance != null ? SampleTopDownPlayer.Instance.gameObject : null;

            player = currentPlayer;
        }

        if (player != null)
            attrs = player.GetComponent<AttributeSet>();
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

        if (forceRebuild || maxHearts != lastDisplayedMaxHearts)
            EnsureHeartTokenCount(maxHearts);

        if (forceRebuild || filledHearts != lastDisplayedFilledHearts || maxHearts != lastDisplayedMaxHearts)
            ApplyHeartStates(filledHearts, maxHearts);

        lastDisplayedMaxHearts = maxHearts;
        lastDisplayedFilledHearts = filledHearts;
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
        token.SetFilled(false);
        return token;
    }

    private void ApplyHeartStates(int filledHearts, int maxHearts)
    {
        for (int i = 0; i < heartTokens.Count; i++)
        {
            HeartTokenUI token = heartTokens[i];
            if (token == null)
                continue;

            bool isVisible = i < maxHearts;
            token.gameObject.SetActive(isVisible);

            if (!isVisible)
                continue;

            token.SetSprites(filledHeartSprite, emptyHeartSprite);
            token.SetFilled(i < filledHearts);
        }
    }
}
