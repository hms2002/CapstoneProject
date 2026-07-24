using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EndingOutroSlide
{
    [SerializeField] private Sprite image;
    [SerializeField, TextArea(2, 6)] private string text;

    public Sprite Image => image;
    public string Text => text ?? string.Empty;
}

[CreateAssetMenu(menuName = "Ending/Ending Outro Sequence", fileName = "EndingOutroSequence")]
public sealed class EndingOutroSequenceSO : ScriptableObject
{
    [Header("Slides")]
    [SerializeField] private List<EndingOutroSlide> slides = new();

    [Header("Typing")]
    [SerializeField, Min(0f)] private float secondsPerCharacter = 0.05f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float outroStartFadeDuration = 1.2f;
    [SerializeField, Min(0f)] private float oneLineWaitSeconds = 1.5f;
    [SerializeField, Min(0f)] private float multiLineWaitSeconds = 2f;
    [SerializeField, Min(0f)] private float imageFadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float initialImageFadeDuration = 1.2f;

    [Header("Skip")]
    [SerializeField, Min(0f)] private float skipHoldSeconds = 2.5f;
    [SerializeField] private Color skipFillColor = new Color32(0xF3, 0x3F, 0x48, 0xFF);

    public IReadOnlyList<EndingOutroSlide> Slides => slides;
    public int SlideCount => slides != null ? slides.Count : 0;
    public float SecondsPerCharacter => Mathf.Max(0f, secondsPerCharacter);
    public float OutroStartFadeDuration => Mathf.Max(0f, outroStartFadeDuration);
    public float ImageFadeDuration => Mathf.Max(0f, imageFadeDuration);
    public float InitialImageFadeDuration => Mathf.Max(0f, initialImageFadeDuration);
    public float SkipHoldSeconds => Mathf.Max(0f, skipHoldSeconds);
    public Color SkipFillColor => skipFillColor;

    public EndingOutroSlide GetSlide(int index)
    {
        if (slides == null || index < 0 || index >= slides.Count)
            return null;

        return slides[index];
    }

    public float GetPostTextWaitSeconds(string text)
    {
        return CountLines(text) <= 1
            ? Mathf.Max(0f, oneLineWaitSeconds)
            : Mathf.Max(0f, multiLineWaitSeconds);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                count++;
        }

        return Mathf.Max(1, count);
    }
}
