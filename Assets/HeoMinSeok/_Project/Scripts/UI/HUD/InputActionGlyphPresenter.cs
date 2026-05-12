using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 클래스의 책임:
/// 지정한 InputActionId의 현재 바인딩을 읽어 키 아이콘 또는 텍스트 라벨로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputActionGlyphPresenter : MonoBehaviour
{
    [Header("Action")]
    [SerializeField] private InputActionId action = InputActionId.InventoryToggle;
    [SerializeField] private bool useSecondaryBinding;

    [Header("View")]
    [SerializeField] private Image keyIconImage;
    [SerializeField] private TMP_Text fallbackLabel;
    [SerializeField] private Sprite fallbackIcon;

    private KeyCode lastRenderedKey = KeyCode.None;

    private void Awake()
    {
        ResolveViewReferences();
    }

    private void OnEnable()
    {
        Refresh(force: true);
    }

    private void LateUpdate()
    {
        Refresh(force: false);
    }

    public void Refresh()
    {
        Refresh(force: true);
    }

    private void Refresh(bool force)
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        KeyCode currentKey = input.GetKey(action, useSecondaryBinding);
        if (!force && currentKey == lastRenderedKey)
            return;

        lastRenderedKey = currentKey;
        InputGlyphPresentation glyph = input.GetKeyGlyph(currentKey);
        InputGlyphVisualUtility.Apply(fallbackLabel, keyIconImage, glyph, input.GetKeyDisplayLabel(currentKey), fallbackIcon);
    }

    private void ResolveViewReferences()
    {
        if (keyIconImage == null)
            keyIconImage = GetComponentInChildren<Image>(true);

        if (fallbackLabel == null)
            fallbackLabel = GetComponentInChildren<TMP_Text>(true);
    }
}
