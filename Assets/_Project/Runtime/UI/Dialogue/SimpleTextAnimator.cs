using UnityEngine;
using TMPro;
using System;
using Random = UnityEngine.Random;

[RequireComponent(typeof(TMP_Text))]
public class SimpleTextAnimator : MonoBehaviour
{
    private TMP_Text textComponent;
    private bool effectLinksDirty = true;
    private bool hasAnimatedLinks;
    private string observedText;
    private TMP_Style observedStyle;
    private bool observedRichText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int DiagnosticForceMeshRefreshCount { get; private set; }
    public int DiagnosticGeometryUploadCount { get; private set; }
#endif

    [Header("효과 설정")]
    public float waveSpeed = 10f;   // 물결 속도
    public float waveHeight = 5f;   // 물결 높이
    public float shakeAmount = 3f;  // 흔들림 강도

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        textComponent ??= GetComponent<TMP_Text>();
        effectLinksDirty = true;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        effectLinksDirty = true;
    }

    private void HandleTextChanged(UnityEngine.Object changed)
    {
        if (changed == textComponent) effectLinksDirty = true;
    }

    private void Update()
    {
        // 텍스트가 없으면 실행 안 함
        if (textComponent.textInfo.characterCount == 0) return;

        if (observedText != textComponent.text || observedStyle != textComponent.textStyle ||
            observedRichText != textComponent.richText)
            effectLinksDirty = true;

        bool refreshed = false;
        if ((effectLinksDirty || textComponent.havePropertiesChanged) &&
            (hasAnimatedLinks || MayContainLinks()))
        {
            RefreshMesh();
            refreshed = true;
            effectLinksDirty = true;
        }

        if (effectLinksDirty)
            RefreshEffectLinks();

        // Ordinary text is rendered by TMP's normal lifecycle. Do not overwrite
        // geometry owned by another text effect when there is no wave/shake link.
        if (!hasAnimatedLinks) return;

        // Retain the original regenerate-then-offset path for real effects. A
        // base-vertex cache could accidentally absorb another animator's offsets.
        if (!refreshed)
        {
            RefreshMesh();
            RefreshEffectLinks();
            if (!hasAnimatedLinks) return;
        }
        TMP_TextInfo textInfo = textComponent.textInfo;

        // 텍스트 안의 모든 <link> 태그를 순회
        for (int i = 0; i < textInfo.linkCount; i++)
        {
            TMP_LinkInfo linkInfo = textInfo.linkInfo[i];
            string linkId = linkInfo.GetLinkID(); // "wave" 또는 "shake"

            // 해당 링크 태그 안에 있는 글자들을 하나씩 꺼냄
            for (int j = linkInfo.linkTextfirstCharacterIndex; j < linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength; j++)
            {
                // 안 보이는 글자(공백 등)는 스킵
                if (!textInfo.characterInfo[j].isVisible) continue;

                // 글자의 정점(Vertex) 데이터 가져오기
                int materialIndex = textInfo.characterInfo[j].materialReferenceIndex;
                int vertexIndex = textInfo.characterInfo[j].vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 offset = Vector3.zero;

                // 1. Wave (둥실둥실) 효과
                if (linkId == "wave")
                {
                    // Sin 그래프를 이용해 위아래로 움직임 (j를 더해서 글자마다 파동이 다르게 함)
                    offset = new Vector3(0, Mathf.Sin(Time.time * waveSpeed + j) * waveHeight, 0);
                }
                // 2. Shake (덜덜덜) 효과
                else if (linkId == "shake")
                {
                    // 랜덤한 위치로 사방으로 흔들림
                    offset = new Vector3(Random.Range(-shakeAmount, shakeAmount), Random.Range(-shakeAmount, shakeAmount), 0);
                }

                // 글자를 구성하는 4개의 꼭짓점에 오프셋 적용
                vertices[vertexIndex + 0] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }
        }

        // 변경된 정점 데이터를 실제 메시에 업데이트하여 화면에 반영
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticGeometryUploadCount++;
#endif
        }
    }

    private bool MayContainLinks()
    {
        if (!textComponent.richText) return false;
        string content = textComponent.text ?? string.Empty;
        // Styles may expand into link tags, so let TMP parse changed rich input
        // rather than recognizing only one authored spelling of a link tag.
        return content.IndexOf("<link", StringComparison.OrdinalIgnoreCase) >= 0 ||
               content.IndexOf("<style", StringComparison.OrdinalIgnoreCase) >= 0 ||
               textComponent.textStyle.hashCode != TMP_Style.NormalStyle.hashCode;
    }

    private void RefreshMesh()
    {
        textComponent.ForceMeshUpdate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticForceMeshRefreshCount++;
#endif
    }

    private void RefreshEffectLinks()
    {
        hasAnimatedLinks = false;
        TMP_TextInfo info = textComponent.textInfo;
        for (int i = 0; i < info.linkCount; i++)
        {
            string id = info.linkInfo[i].GetLinkID();
            if (id == "wave" || id == "shake") { hasAnimatedLinks = true; break; }
        }
        observedText = textComponent.text;
        observedStyle = textComponent.textStyle;
        observedRichText = textComponent.richText;
        effectLinksDirty = false;
    }
}
