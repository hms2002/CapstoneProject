using System;
using CapstoneAudio;
using UnityEngine;

[DisallowMultipleComponent]
public class CandlestickSeal : MonoBehaviour
{
    // 이 클래스의 책임:
    // 촛대 봉인 상태와 남은 해제 타격 수를 관리하고, 봉인 상태 변화에 맞춰 빛 루트를 제어하며 연출 구현체에 상태만 통지한다.

    private const int SealHitCount = 3;
    private static readonly SoundRef SealSound = SoundRef.FromKey("sound_candlestick_Seal");
    private static readonly SoundRef Unlock1Sound = SoundRef.FromKey("sound_candlestick_Unlock1");
    private static readonly SoundRef Unlock2Sound = SoundRef.FromKey("sound_candlestick_Unlock2");
    private static readonly SoundRef Unlock3Sound = SoundRef.FromKey("sound_candlestick_Unlock3");
    private static readonly SoundRef UnsealSound = SoundRef.FromKey("sound_candlestick_Unseal");

    [SerializeField] private GameObject lightVisualRoot;
    [SerializeField] private SpriteMask sightMask;
    [SerializeField] private CandlestickLightZone lightZone;
    [SerializeField] private MonoBehaviour[] presentationBehaviours;
    private ICandlestickSealPresentation[] presentations = Array.Empty<ICandlestickSealPresentation>();
    private int hitsLeft;
    private bool isSealed;

    public bool IsSealed => isSealed;
    public int CurrentHitsLeft => hitsLeft;
    public int MaxSealHits => SealHitCount;
    public event Action<bool> SealChanged;
    public event Action<int, int> SealStacksChanged;

    private void Awake()
    {
        if (lightVisualRoot == null)
            lightVisualRoot = FindLightVisualRoot();

        if (sightMask == null)
            sightMask = GetComponentInChildren<SpriteMask>(true);

        if (lightZone == null)
            lightZone = GetComponentInChildren<CandlestickLightZone>(true);

        ResolvePresentations();
        HideSealPresentations();
    }

    /// <summary>촛대를 봉인 상태로 바꿉니다.</summary>
    public void Seal()
    {
        if (isSealed) return;

        isSealed = true;
        hitsLeft = SealHitCount;
        ToggleLight(false);
        PlaySound(SealSound);
        ShowSealPresentations();
        SealChanged?.Invoke(true);
        SealStacksChanged?.Invoke(hitsLeft, SealHitCount);
    }

    /// <summary>봉인 해제 타격을 처리합니다.</summary>
    public bool UseHit()
    {
        if (!isSealed) return false;

        PlayUnlockSound(hitsLeft);
        hitsLeft = Mathf.Max(0, hitsLeft - 1);
        UpdateSealPresentations();
        SealStacksChanged?.Invoke(hitsLeft, SealHitCount);

        if (hitsLeft == 0)
            BreakSeal();

        return true;
    }

    /// <summary>봉인을 해제합니다.</summary>
    private void BreakSeal()
    {
        isSealed = false;
        ToggleLight(true);
        PlaySound(UnsealSound);
        PlaySealBrokenPresentations();
        SealChanged?.Invoke(false);
    }

    /// <summary>봉인 타격 전 남은 횟수에 대응하는 해제 사운드를 재생합니다.</summary>
    private void PlayUnlockSound(int remainingBeforeHit)
    {
        SoundRef sound = remainingBeforeHit switch
        {
            3 => Unlock3Sound,
            2 => Unlock2Sound,
            _ => Unlock1Sound
        };

        PlaySound(sound);
    }

    /// <summary>촛대 봉인 위치를 기준으로 단발 사운드를 재생합니다.</summary>
    private void PlaySound(SoundRef sound)
    {
        SoundPlaybackUtility.Play(sound, causer: gameObject, position: transform.position, sourceObject: this);
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

    private void ResolvePresentations()
    {
        if (presentationBehaviours == null || presentationBehaviours.Length == 0)
        {
            var defaultPresentation = GetComponent<CandlestickSealPipPresentation>();
            if (defaultPresentation == null)
                defaultPresentation = gameObject.AddComponent<CandlestickSealPipPresentation>();

            presentationBehaviours = new MonoBehaviour[] { defaultPresentation };
        }

        int validCount = 0;
        for (int i = 0; i < presentationBehaviours.Length; i++)
        {
            if (presentationBehaviours[i] is ICandlestickSealPresentation)
                validCount++;
        }

        presentations = new ICandlestickSealPresentation[validCount];
        int writeIndex = 0;
        for (int i = 0; i < presentationBehaviours.Length; i++)
        {
            if (presentationBehaviours[i] is ICandlestickSealPresentation presentation)
                presentations[writeIndex++] = presentation;
        }
    }

    private void ShowSealPresentations()
    {
        for (int i = 0; i < presentations.Length; i++)
        {
            presentations[i]?.ShowSeal(hitsLeft, SealHitCount);
        }
    }

    private void UpdateSealPresentations()
    {
        for (int i = 0; i < presentations.Length; i++)
        {
            presentations[i]?.UpdateSealStacks(hitsLeft, SealHitCount);
        }
    }

    private void PlaySealBrokenPresentations()
    {
        for (int i = 0; i < presentations.Length; i++)
        {
            presentations[i]?.PlaySealBroken();
        }
    }

    private void HideSealPresentations()
    {
        for (int i = 0; i < presentations.Length; i++)
        {
            presentations[i]?.HideSeal();
        }
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
