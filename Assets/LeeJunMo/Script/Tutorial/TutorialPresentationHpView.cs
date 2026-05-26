using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialPresentationHpView : MonoBehaviour
{
    [Serializable]
    public sealed class IntEvent : UnityEvent<int>
    {
    }

    [Header("HP")]
    [SerializeField, Min(1)] private int maxHp = 5;
    [SerializeField, Min(0)] private int currentHp = 5;
    [SerializeField] private bool resetToMaxOnEnable;

    [Header("Text")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpFormat = "{0}/{1}";

    [Header("Authored Slots")]
    [SerializeField] private GameObject[] filledSlotRoots;
    [SerializeField] private GameObject[] emptySlotRoots;

    [Header("Events")]
    [SerializeField] private IntEvent onCurrentHpChanged = new();
    [SerializeField] private UnityEvent onDepleted = new();

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public IntEvent OnCurrentHpChanged => onCurrentHpChanged;
    public UnityEvent OnDepleted => onDepleted;

    private void OnEnable()
    {
        if (resetToMaxOnEnable)
            currentHp = maxHp;

        Refresh();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        if (isActiveAndEnabled)
            Refresh();
    }

    public void SetMaxHp(int value)
    {
        int previousHp = currentHp;
        maxHp = Mathf.Max(1, value);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        RefreshAndNotify(previousHp);
    }

    public void SetCurrentHp(int value)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(value, 0, maxHp);
        RefreshAndNotify(previousHp);
    }

    public void ResetToMax()
    {
        SetCurrentHp(maxHp);
    }

    public void ReduceOne()
    {
        Reduce(1);
    }

    public void Reduce(int amount)
    {
        if (amount <= 0)
            return;

        SetCurrentHp(currentHp - amount);
    }

    public void Refresh()
    {
        ApplyText();
        ApplySlots();
    }

    private void RefreshAndNotify(int previousHp)
    {
        Refresh();

        if (previousHp == currentHp)
            return;

        onCurrentHpChanged?.Invoke(currentHp);

        if (previousHp > 0 && currentHp <= 0)
            onDepleted?.Invoke();
    }

    private void ApplyText()
    {
        if (hpText == null)
            return;

        if (string.IsNullOrWhiteSpace(hpFormat))
        {
            hpText.text = $"{currentHp}/{maxHp}";
            return;
        }

        try
        {
            hpText.text = string.Format(hpFormat, currentHp, maxHp);
        }
        catch (FormatException)
        {
            hpText.text = $"{currentHp}/{maxHp}";
        }
    }

    private void ApplySlots()
    {
        ApplySlotRoots(filledSlotRoots, isFilledLayer: true);
        ApplySlotRoots(emptySlotRoots, isFilledLayer: false);
    }

    private void ApplySlotRoots(GameObject[] roots, bool isFilledLayer)
    {
        if (roots == null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            bool insideMax = i < maxHp;
            bool shouldShow = insideMax && (isFilledLayer ? i < currentHp : i >= currentHp);
            if (root.activeSelf != shouldShow)
                root.SetActive(shouldShow);
        }
    }
}
