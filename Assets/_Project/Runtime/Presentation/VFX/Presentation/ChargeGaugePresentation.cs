using UnityEngine;

namespace CapstonePresentation
{
    [DisallowMultipleComponent]
    public sealed class ChargeGaugePresentation : MonoBehaviour, IChargeGaugePresentation
    {
        [SerializeField] private UnityEngine.UI.Image gaugeFill;
        [SerializeField] private UnityEngine.UI.Image gaugeHighlight;
        [SerializeField] private UnityEngine.UI.Image gaugeShadow;
        [SerializeField] private GameObject gaugeFullFrame;
        [SerializeField] private Vector3 gaugeOffset = new(0f, 1.3f, 0f);
        [SerializeField] private Color chargingColor = new Color32(221, 46, 70, 255);
        [SerializeField] private Color readyColor = new Color32(240, 120, 38, 255);
        [SerializeField] private Color fullColor = new Color32(255, 157, 65, 255);
        [SerializeField] private Color chargingHighlight = new Color32(255, 102, 91, 255);
        [SerializeField] private Color readyHighlight = new Color32(255, 171, 86, 255);
        [SerializeField] private Color fullHighlight = new Color32(255, 235, 171, 255);
        [SerializeField] private Color chargingShadow = new Color32(128, 23, 48, 255);
        [SerializeField] private Color readyShadow = new Color32(158, 60, 33, 255);
        [SerializeField] private Color fullShadow = new Color32(207, 70, 41, 255);

        public void ShowCharge(Vector3 ownerPosition, float ratio)
        {
            float progress = Mathf.Clamp01(ratio);
            transform.SetPositionAndRotation(ownerPosition + gaugeOffset, Quaternion.identity);
            gaugeFill.fillAmount = progress;
            gaugeHighlight.fillAmount = progress;
            gaugeShadow.fillAmount = progress;
            gaugeFill.color = progress >= 1f ? fullColor : progress >= 0.5f ? readyColor : chargingColor;
            gaugeHighlight.color = progress >= 1f ? fullHighlight : progress >= 0.5f ? readyHighlight : chargingHighlight;
            gaugeShadow.color = progress >= 1f ? fullShadow : progress >= 0.5f ? readyShadow : chargingShadow;
            gaugeFullFrame.SetActive(progress >= 1f);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnDisable()
        {
            // An owner disabled during a hold must not reveal a stale bar on re-enable.
            gaugeFill.fillAmount = gaugeHighlight.fillAmount = gaugeShadow.fillAmount = 0f;
            gaugeFullFrame.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
