using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Heart Layout")]
    [Tooltip("위치와 크기가 고정되는 하트 배치 영역입니다. 런타임에는 이 RectTransform을 변경하지 않습니다.")]
    [SerializeField] private RectTransform heartLayoutArea;
    [SerializeField] private GridLayoutGroup heartGrid;
    [SerializeField, Min(1)] private int singleRowLimit = 6;
    [SerializeField, Min(1)] private int designedMaxHeartCount = 12;
    [SerializeField, Min(1f)] private float minimumHeartSize = 48f;
    [SerializeField, Min(1f)] private float maximumHeartSize = 72f;
    [SerializeField] private Vector2 heartSpacing = new Vector2(10f, 8f);

    private readonly List<HeartTokenUI> heartTokens = new();

    private AttributeSet attrs;
    private AttributeDefinition resolvedSoulHeartDef;
    private int lastDisplayedMaxHearts = -1;
    private int lastDisplayedFilledHearts = -1;
    private int lastDisplayedSoulHearts = -1;
    private int lastDisplayedTotalHearts = -1;
    private Vector2 lastLayoutAreaSize = new Vector2(float.NaN, float.NaN);

    private void Awake()
    {
        if (heartContainer == null)
            heartContainer = transform;

        ResolveLayoutReferences();

        TryResolvePlayerAttributes();
        RefreshHearts(forceRebuild: true);
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        TryResolvePlayerAttributes();
        ResolveLayoutReferences();
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

        singleRowLimit = Mathf.Max(1, singleRowLimit);
        designedMaxHeartCount = Mathf.Max(singleRowLimit, designedMaxHeartCount);
        minimumHeartSize = Mathf.Max(1f, minimumHeartSize);
        maximumHeartSize = Mathf.Max(minimumHeartSize, maximumHeartSize);
        heartSpacing.x = Mathf.Max(0f, heartSpacing.x);
        heartSpacing.y = Mathf.Max(0f, heartSpacing.y);

        ResolveLayoutReferences();
        RefreshHeartLayout(lastDisplayedTotalHearts, force: true);
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshHeartLayout(lastDisplayedTotalHearts, force: false);
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

        RefreshHeartLayout(totalHearts, forceRebuild || totalHearts != lastDisplayedTotalHearts);

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

    private void ResolveLayoutReferences()
    {
        if (heartLayoutArea == null)
            heartLayoutArea = heartContainer as RectTransform ?? transform as RectTransform;

        if (heartGrid == null && heartContainer != null)
            heartGrid = heartContainer.GetComponent<GridLayoutGroup>();
    }

    private void RefreshHeartLayout(int totalHearts, bool force)
    {
        if (totalHearts < 0)
            return;

        ResolveLayoutReferences();
        if (heartLayoutArea == null || heartGrid == null)
            return;

        Vector2 areaSize = heartLayoutArea.rect.size;
        if (!force && areaSize == lastLayoutAreaSize)
            return;

        lastLayoutAreaSize = areaSize;

        int safeHeartCount = Mathf.Max(1, totalHearts);
        int rowCount = safeHeartCount <= singleRowLimit ? 1 : 2;
        int columnCount = Mathf.Min(safeHeartCount, singleRowLimit);
        if (safeHeartCount > designedMaxHeartCount)
            columnCount = Mathf.Max(columnCount, Mathf.CeilToInt(safeHeartCount / 2f));

        RectOffset padding = heartGrid.padding ?? new RectOffset();
        float availableWidth = Mathf.Max(0f, areaSize.x - padding.horizontal);
        float availableHeight = Mathf.Max(0f, areaSize.y - padding.vertical);
        // Six hearts define the minimum size and the row width. Fewer hearts fill
        // that width with larger cells; adding a second row must not shrink them.
        float rowWidth = minimumHeartSize * singleRowLimit + heartSpacing.x * (singleRowLimit - 1);
        float cellSize = Mathf.Min(maximumHeartSize,
            (rowWidth - heartSpacing.x * (columnCount - 1)) / columnCount);
        cellSize = Mathf.Max(1f, cellSize);
        float requiredWidth = cellSize * columnCount + heartSpacing.x * (columnCount - 1);
        // Reserve both minimum-size rows even while only the first row is visible.
        float requiredHeight = Mathf.Max(cellSize * rowCount + heartSpacing.y * (rowCount - 1),
            minimumHeartSize * 2f + heartSpacing.y);
        float fitScale = Mathf.Min(1f, availableWidth / requiredWidth, availableHeight / requiredHeight);
        cellSize *= fitScale;

        heartGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        heartGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        heartGrid.childAlignment = TextAnchor.MiddleLeft;
        heartGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        heartGrid.constraintCount = columnCount;
        heartGrid.spacing = heartSpacing * fitScale;
        heartGrid.cellSize = new Vector2(cellSize, cellSize);
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
