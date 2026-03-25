using UnityEngine;

public class LeverShortcut : PermanentShortcut
{
    [Header("프롬프트")]
    [SerializeField] private string interactPromptText = "작동하기";

    [Header("비주얼")]
    [SerializeField] private SpriteRenderer leverRenderer;
    [SerializeField] private Sprite activatedSprite;

    private Sprite defaultSprite;

    protected override void Awake()
    {
        base.Awake();

        if (leverRenderer != null)
            defaultSprite = leverRenderer.sprite;
    }

    protected override bool CheckCondition(IPlayerInteractor player) => true;

    protected override void SetActivatedVisual()
    {
        if (leverRenderer == null)
            return;

        if (activatedSprite != null)
            leverRenderer.sprite = activatedSprite;
    }

    public void SetDeactivatedVisual()
    {
        if (leverRenderer == null)
            return;

        if (defaultSprite != null)
            leverRenderer.sprite = defaultSprite;
    }

    public override string GetInteractDescription() => interactPromptText;
}