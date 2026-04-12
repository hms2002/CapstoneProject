using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CandlestickSeal : MonoBehaviour
{
    // 이 클래스의 책임:
    // 촛대 봉인 상태와 남은 해제 타격 수를 관리하고, 봉인 상태에 맞춰 빛 연출/판정 루트를 켜고 끈다.

    private const int SealHitCount = 3;

    private static Sprite markSprite;

    private readonly List<SpriteRenderer> marks = new();

    [SerializeField] private GameObject lightVisualRoot;
    [SerializeField] private SpriteMask sightMask;
    [SerializeField] private CandlestickLightZone lightZone;
    private SpriteRenderer ownerSprite;
    private int hitsLeft;
    private bool isSealed;

    public bool IsSealed => isSealed;
    public event Action<bool> SealChanged;

    private void Awake()
    {
        ownerSprite = GetComponent<SpriteRenderer>();
        if (lightVisualRoot == null)
            lightVisualRoot = FindLightVisualRoot();

        if (sightMask == null)
            sightMask = GetComponentInChildren<SpriteMask>(true);

        if (lightZone == null)
            lightZone = GetComponentInChildren<CandlestickLightZone>(true);

        BuildMarks();
        HideMarks();
    }

    /// <summary>촛대를 봉인 상태로 바꿉니다.</summary>
    public void Seal()
    {
        if (isSealed) return;

        isSealed = true;
        hitsLeft = SealHitCount;
        ToggleLight(false);
        ShowMarks();
        SealChanged?.Invoke(true);
    }

    /// <summary>봉인 해제 타격을 처리합니다.</summary>
    public bool UseHit()
    {
        if (!isSealed) return false;

        hitsLeft = Mathf.Max(0, hitsLeft - 1);
        UpdateMarks();

        if (hitsLeft == 0)
            BreakSeal();

        return true;
    }

    /// <summary>봉인을 해제합니다.</summary>
    private void BreakSeal()
    {
        isSealed = false;
        ToggleLight(true);
        HideMarks();
        SealChanged?.Invoke(false);
    }

    /// <summary>빛 연출 루트와 빛 판정 범위를 켜고 끕니다.</summary>
    private void ToggleLight(bool isOn)
    {
        if (lightVisualRoot != null)
            lightVisualRoot.SetActive(isOn);

        if (sightMask != null && sightMask.gameObject != lightVisualRoot)
            sightMask.gameObject.SetActive(isOn);

        if (lightZone != null)
            lightZone.gameObject.SetActive(isOn);
    }

    /// <summary>봉인 표식을 만듭니다.</summary>
    private void BuildMarks()
    {
        if (marks.Count > 0) return;

        Sprite sprite = GetMarkSprite();
        int sortingLayerId = ownerSprite != null ? ownerSprite.sortingLayerID : 0;
        int sortingOrder = ownerSprite != null ? ownerSprite.sortingOrder + 1 : 1;

        for (int i = 0; i < SealHitCount; i++)
        {
            GameObject markObject = new GameObject($"SealMark_{i + 1}");
            markObject.transform.SetParent(transform, false);
            markObject.transform.localPosition = GetMarkPos(i);
            markObject.transform.localScale = new Vector3(0.14f, 0.14f, 1f);

            SpriteRenderer markRenderer = markObject.AddComponent<SpriteRenderer>();
            markRenderer.sprite = sprite;
            markRenderer.color = new Color(0.88f, 0.1f, 1f, 1f);
            markRenderer.sortingLayerID = sortingLayerId;
            markRenderer.sortingOrder = sortingOrder;

            marks.Add(markRenderer);
        }
    }

    /// <summary>남은 봉인 수만큼 표식을 보여줍니다.</summary>
    private void UpdateMarks()
    {
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] == null) continue;

            marks[i].gameObject.SetActive(i < hitsLeft);
        }
    }

    /// <summary>봉인 표식을 모두 켭니다.</summary>
    private void ShowMarks()
    {
        UpdateMarks();
    }

    /// <summary>봉인 표식을 모두 끕니다.</summary>
    private void HideMarks()
    {
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] == null) continue;

            marks[i].gameObject.SetActive(false);
        }
    }

    /// <summary>표식 위치를 구합니다.</summary>
    private Vector3 GetMarkPos(int index)
    {
        return index switch
        {
            0 => new Vector3(-0.18f, 0.7f, 0f),
            1 => new Vector3(0f, 0.8f, 0f),
            _ => new Vector3(0.18f, 0.7f, 0f)
        };
    }

    /// <summary>표식에 쓸 스프라이트를 구합니다.</summary>
    private Sprite GetMarkSprite()
    {
        if (markSprite != null) return markSprite;

        Rect rect = new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height);
        markSprite = Sprite.Create(Texture2D.whiteTexture, rect, new Vector2(0.5f, 0.5f), 100f);
        return markSprite;
    }

    /// <summary>촛대 빛 연출 루트 후보를 찾아 반환합니다.</summary>
    private GameObject FindLightVisualRoot()
    {
        if (sightMask != null)
            return sightMask.gameObject;

        SpriteMask foundMask = GetComponentInChildren<SpriteMask>(true);
        if (foundMask != null)
            return foundMask.gameObject;

        return null;
    }
}
