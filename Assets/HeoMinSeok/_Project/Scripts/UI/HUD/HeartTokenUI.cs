using UnityEngine;
using UnityEngine.UI;

public class HeartTokenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image heartImage;

    [Header("Sprites")]
    [SerializeField] private Sprite filledHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

    private void Awake()
    {
        if (heartImage == null)
            heartImage = GetComponent<Image>();
    }

    public void SetSprites(Sprite filledSprite, Sprite emptySprite)
    {
        filledHeartSprite = filledSprite;
        emptyHeartSprite = emptySprite;
    }

    public void SetFilled(bool isFilled)
    {
        if (heartImage == null)
            heartImage = GetComponent<Image>();

        if (heartImage == null)
            return;

        heartImage.sprite = isFilled ? filledHeartSprite : emptyHeartSprite;
        heartImage.enabled = heartImage.sprite != null;
    }
}
