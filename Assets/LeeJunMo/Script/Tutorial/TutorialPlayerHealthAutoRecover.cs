using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class TutorialPlayerHealthAutoRecover : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private AttributeSet attributeSet;
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;

    [Header("Recovery")]
    [SerializeField] private bool restoreToMaxOnEnable = true;
    [SerializeField] private bool keepFullHealthInUpdate = true;
    [SerializeField, Min(0f)] private float restoreEpsilon = 0.001f;

    [Header("Death Guard")]
    [SerializeField] private bool suspendPlayerDeathReturn = true;
    [SerializeField] private PlayerDeathReturnToHub2D deathReturnToHub;

    private AttributeSet subscribedAttributeSet;
    private bool isRestoring;
    private bool hasStoredDeathReturnState;
    private bool previousDeathReturnEnabled;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeAttributeSet();
        ApplyDeathGuard();

        if (restoreToMaxOnEnable)
            RestoreNow();
    }

    private void Update()
    {
        ResolveReferences();
        SubscribeAttributeSet();
        ApplyDeathGuard();

        if (keepFullHealthInUpdate)
            RestoreNow();
    }

    private void OnDisable()
    {
        UnsubscribeAttributeSet();
        RestoreDeathGuard();
    }

    public void RestoreNow()
    {
        if (attributeSet == null || healthAttribute == null || maxHealthAttribute == null)
            return;

        if (isRestoring)
            return;

        float maxHealth = attributeSet.GetAttributeValue(maxHealthAttribute);
        float currentHealth = attributeSet.GetAttributeValue(healthAttribute);
        if (currentHealth + restoreEpsilon >= maxHealth)
            return;

        isRestoring = true;
        try
        {
            if (!attributeSet.TrySetCurrentValue(healthAttribute, maxHealth, this))
            {
                float delta = maxHealth - currentHealth;
                if (delta > 0f)
                    attributeSet.TryModifyAttributeValue(healthAttribute, delta, this);
            }
        }
        finally
        {
            isRestoring = false;
        }
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute != healthAttribute && attribute != maxHealthAttribute)
            return;

        RestoreNow();
    }

    private void SubscribeAttributeSet()
    {
        if (subscribedAttributeSet == attributeSet)
            return;

        UnsubscribeAttributeSet();
        if (attributeSet == null)
            return;

        attributeSet.OnAttributeChanged += HandleAttributeChanged;
        subscribedAttributeSet = attributeSet;
    }

    private void UnsubscribeAttributeSet()
    {
        if (subscribedAttributeSet == null)
            return;

        subscribedAttributeSet.OnAttributeChanged -= HandleAttributeChanged;
        subscribedAttributeSet = null;
    }

    private void ApplyDeathGuard()
    {
        if (!suspendPlayerDeathReturn || deathReturnToHub == null || hasStoredDeathReturnState)
            return;

        previousDeathReturnEnabled = deathReturnToHub.enabled;
        hasStoredDeathReturnState = true;

        if (deathReturnToHub.enabled)
            deathReturnToHub.enabled = false;
    }

    private void RestoreDeathGuard()
    {
        if (!hasStoredDeathReturnState)
            return;

        if (deathReturnToHub != null)
            deathReturnToHub.enabled = previousDeathReturnEnabled;

        hasStoredDeathReturnState = false;
    }

    private void ResolveReferences()
    {
        if (playerTransform == null)
            playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();

        if (playerTransform == null)
            return;

        if (attributeSet == null)
            attributeSet = playerTransform.GetComponent<AttributeSet>();

        if (deathReturnToHub == null)
            deathReturnToHub = playerTransform.GetComponent<PlayerDeathReturnToHub2D>();
    }
}
