using System;
using UnityEditor;
using UnityEngine;
using UnityGAS;

// Run in an isolated Unity Editor project with the production attribute sources.
public static class AttributeDepletionRegression
{
    public static void Run()
    {
        try
        {
            CheckDepletion(0.0005f, 10f);
            CheckDepletion(0.001f, 10f);
            CheckDepletion(180f, 1.2f);
            CheckDepletion(180f, 2.4f);
            CheckDepletion(180f, 7.2f);
            CheckDepletion(180f, 180f);
            CheckSmallNonzeroChange();
            Debug.Log("ATTRIBUTE_DEPLETION_PASS: tiny residual, threshold boundary, fractional damage, ordinary lethal damage, no duplicate depletion, small nonzero change.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static AttributeDefinition CreateDefinition(float initial)
    {
        var definition = ScriptableObject.CreateInstance<AttributeDefinition>();
        definition.defaultBaseValue = initial;
        definition.minValue = 0f;
        definition.maxValue = 180f;
        definition.hasRegeneration = false;
        return definition;
    }

    private static void CheckDepletion(float initial, float damage)
    {
        var definition = CreateDefinition(initial);
        try
        {
            var value = new AttributeValue(definition);
            value.SetClampNormalizationPolicy(true);
            int depletedEvents = 0;
            int allEvents = 0;
            value.OnValueChanged += (before, after) =>
            {
                allEvents++;
                if (before > 0f && after <= 0f) depletedEvents++;
            };
            for (int i = 0; i < 1000 && value.CurrentValue > 0f; i++)
            {
                value.AddBaseValue(-damage);
                value.ForceRecalculate();
            }
            if (value.CurrentValue != 0f || depletedEvents != 1)
                throw new Exception($"Lost depletion: initial={initial}, damage={damage}, hp={value.CurrentValue}, events={depletedEvents}");

            int eventsAtDeath = allEvents;
            for (int i = 0; i < 3; i++)
            {
                value.AddBaseValue(-damage);
                value.ForceRecalculate();
            }
            if (allEvents != eventsAtDeath)
                throw new Exception("Repeated damage at zero emitted another event.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    private static void CheckSmallNonzeroChange()
    {
        var definition = CreateDefinition(1f);
        try
        {
            var value = new AttributeValue(definition);
            int events = 0;
            value.OnValueChanged += (_, __) => events++;
            value.AddBaseValue(-0.0005f);
            value.ForceRecalculate();
            if (value.CurrentValue <= 0f || value.CurrentValue >= 1f || events != 0)
                throw new Exception("Small nonzero change no longer preserves the existing notification threshold.");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }
}
