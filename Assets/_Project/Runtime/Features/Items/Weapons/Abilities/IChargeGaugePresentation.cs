using UnityEngine;

// Gameplay owns charge progress; an authored view only projects it above the owner.
public interface IChargeGaugePresentation
{
    void ShowCharge(Vector3 ownerPosition, float ratio);
    void Hide();
}
