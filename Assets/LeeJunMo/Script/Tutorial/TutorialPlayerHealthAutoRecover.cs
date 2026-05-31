using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
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
    [FormerlySerializedAs("keepFullHealthInUpdate")]
    [SerializeField] private bool recoverAfterDamage = true;
    [SerializeField, Min(0f)] private float recoveryDelaySeconds = 2f;
    [SerializeField, Min(0f)] private float minimumSurvivalHealth = 1f;
    [SerializeField, Min(0f)] private float restoreEpsilon = 0.001f;

    [Header("Death Guard")]
    [SerializeField] private bool suspendPlayerDeathReturn = true;
    [SerializeField] private PlayerDeathReturnToHub2D deathReturnToHub;

    private AttributeSet subscribedAttributeSet;
    private Coroutine recoveryRoutine;
    private bool isRestoring;
    private bool hasStoredDeathReturnState;
    private bool previousDeathReturnEnabled;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

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
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;

        StopPendingRecovery();
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
        if (isRestoring)
            return;

        if (attribute == healthAttribute)
        {
            if (newValue <= 0f)
            {
                EnsureSurvivalHealth();
                ScheduleRecovery();
                return;
            }

            if (newValue + restoreEpsilon < oldValue)
                ScheduleRecovery();

            return;
        }

        if (attribute == maxHealthAttribute && recoverAfterDamage && IsBelowMaxHealth())
            ScheduleRecovery();
    }

    private void ScheduleRecovery()
    {
        if (!recoverAfterDamage || !isActiveAndEnabled)
            return;

        StopPendingRecovery();
        recoveryRoutine = StartCoroutine(RecoveryRoutine());
    }

    private IEnumerator RecoveryRoutine()
    {
        if (recoveryDelaySeconds > 0f)
            yield return new WaitForSeconds(recoveryDelaySeconds);

        recoveryRoutine = null;
        RestoreNow();
    }

    private void StopPendingRecovery()
    {
        if (recoveryRoutine == null)
            return;

        StopCoroutine(recoveryRoutine);
        recoveryRoutine = null;
    }

    private void EnsureSurvivalHealth()
    {
        if (attributeSet == null || healthAttribute == null || maxHealthAttribute == null)
            return;

        float maxHealth = attributeSet.GetAttributeValue(maxHealthAttribute);
        if (maxHealth <= restoreEpsilon)
            return;

        float survivalHealth = Mathf.Clamp(minimumSurvivalHealth, restoreEpsilon, maxHealth);
        float currentHealth = attributeSet.GetAttributeValue(healthAttribute);
        if (currentHealth + restoreEpsilon >= survivalHealth)
            return;

        isRestoring = true;
        try
        {
            if (!attributeSet.TrySetCurrentValue(healthAttribute, survivalHealth, this))
            {
                float delta = survivalHealth - currentHealth;
                if (delta > 0f)
                    attributeSet.TryModifyAttributeValue(healthAttribute, delta, this);
            }
        }
        finally
        {
            isRestoring = false;
        }
    }

    private bool IsBelowMaxHealth()
    {
        if (attributeSet == null || healthAttribute == null || maxHealthAttribute == null)
            return false;

        float maxHealth = attributeSet.GetAttributeValue(maxHealthAttribute);
        float currentHealth = attributeSet.GetAttributeValue(healthAttribute);
        return currentHealth + restoreEpsilon < maxHealth;
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

    private void HandlePlayerRegistered(PlayerInteractor2D registeredPlayer)
    {
        if (registeredPlayer == null)
            return;

        BindPlayer(registeredPlayer.transform);

        if (restoreToMaxOnEnable)
            RestoreNow();
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D unregisteredPlayer)
    {
        if (unregisteredPlayer == null || unregisteredPlayer.transform != playerTransform)
            return;

        BindPlayer(null);
    }

    private void ResolveReferences()
    {
        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null && registeredPlayer != playerTransform)
            BindPlayer(registeredPlayer);

        if (playerTransform == null)
            return;

        if (attributeSet == null)
            attributeSet = playerTransform.GetComponent<AttributeSet>();

        if (deathReturnToHub == null)
            deathReturnToHub = playerTransform.GetComponent<PlayerDeathReturnToHub2D>();
    }

    private void BindPlayer(Transform newPlayerTransform)
    {
        if (playerTransform == newPlayerTransform)
            return;

        StopPendingRecovery();
        UnsubscribeAttributeSet();
        RestoreDeathGuard();

        playerTransform = newPlayerTransform;
        attributeSet = null;
        deathReturnToHub = null;

        ResolveReferences();
        SubscribeAttributeSet();
        ApplyDeathGuard();
    }
}
