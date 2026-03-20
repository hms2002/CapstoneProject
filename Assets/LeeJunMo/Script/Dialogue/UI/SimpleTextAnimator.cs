using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class SimpleTextAnimator : MonoBehaviour
{
    private TMP_Text textComponent;

    [Header("효과 설정")]
    public float waveSpeed = 10f;   // 물결 속도
    public float waveHeight = 5f;   // 물결 높이
    public float shakeAmount = 3f;  // 흔들림 강도

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        // 텍스트가 없으면 실행 안 함
        if (textComponent.textInfo.characterCount == 0) return;

        // 텍스트 메시 정보를 강제로 최신화해서 가져옴
        textComponent.ForceMeshUpdate();
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
        }
    }
}