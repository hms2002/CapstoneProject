using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects the run-owned first weapon-swap hint into an authored overhead prompt.</summary>
[DisallowMultipleComponent]
public sealed class WeaponSwapHintPresenter : MonoBehaviour
{
    [SerializeField] private RectTransform prompt;
    [SerializeField] private Image keyIcon;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject levelUpPrompt;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 uiOffset = new Vector2(-40f, 0f);

    private Canvas promptCanvas;
    private Transform boundPlayer;
    private WeaponInventory2D inventory;
    private KeyCode renderedKey;
    private bool hasRenderedKey;

    private void Awake()
    {
        promptCanvas = prompt != null ? prompt.GetComponentInParent<Canvas>() : null;
        SetVisible(false);
    }

    private void OnEnable() => hasRenderedKey = false;
    private void OnDisable() => SetVisible(false);

    private void LateUpdate()
    {
        Transform player = PlayerRuntimeRegistry.GetPlayerTransform();
        if (boundPlayer != player)
        {
            boundPlayer = player;
            inventory = player != null ? player.GetComponent<WeaponInventory2D>() : null;
        }

        GamePlayData run = RunSessionStore.Data;
        bool ready = run != null && run.isRunActive && run.weaponSwapHintUnlocked &&
                     !run.weaponSwapHintCompleted && inventory != null &&
                     inventory.HasWeapon(0) && inventory.HasWeapon(1) &&
                     inventory.IsSlotAccessible(0) && inventory.IsSlotAccessible(1);
        if (!ready || player == null || !player.gameObject.activeInHierarchy || !TryPosition(player))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        RefreshBinding();
        float blend = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / 2.4f);
        if (promptText != null)
            promptText.color = Color.HSVToRGB(Mathf.Lerp(0.23f, 0.38f, blend), 0.62f, 1f);
    }

    private bool TryPosition(Transform player)
    {
        Camera camera = Camera.main;
        if (prompt == null || promptCanvas == null || camera == null ||
            prompt.parent is not RectTransform parent)
            return false;
        Vector3 screen = camera.WorldToScreenPoint(player.position + worldOffset);
        if (screen.z <= 0f || !camera.pixelRect.Contains((Vector2)screen))
            return false;
        Canvas canvas = promptCanvas.rootCanvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null)
            return false;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, uiCamera, out Vector3 position))
            return false;
        float hover = 4f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 2.4f);
        prompt.position = position + parent.TransformVector((Vector3)uiOffset + Vector3.up * hover);
        return true;
    }

    private void RefreshBinding()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        KeyCode key = input.GetKey(InputActionId.SwapWeapon);
        if (hasRenderedKey && key == renderedKey)
            return;
        hasRenderedKey = true;
        renderedKey = key;
        Sprite icon = input.GetKeyGlyph(key).Icon;
        if (keyIcon != null)
        {
            keyIcon.sprite = icon;
            keyIcon.gameObject.SetActive(icon != null);
        }
        if (promptText != null)
            promptText.text = icon != null ? "무기 교체" : $"[{input.GetKeyDisplayLabel(key)}] 무기 교체";
    }

    private void SetVisible(bool visible)
    {
        if (prompt != null && prompt.gameObject.activeSelf != visible)
            prompt.gameObject.SetActive(visible);
        if (visible && prompt != null)
            PlayerOverheadPromptLayout.Place(this, prompt, promptCanvas, PlayerOverheadPromptLayout.WeaponSwap);
        else
            PlayerOverheadPromptLayout.Remove(this);
    }
}
