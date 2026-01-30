using UnityEngine;
using UnityEngine.U2D.Animation; // SpriteLibraryAsset 사용을 위해 필수
using System.Collections.Generic;

// [기존 유지] 사용자 정의 보상 구조체
[System.Serializable]
public struct AffectionReward
{
    public int targetLevel;      // 보상을 줄 특정 레벨
    public AffectionEffect effect; // 실행할 효과 SO
}

[CreateAssetMenu(menuName = "NPC/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;             // 고유 ID (Ink 태그 연동용: 1001, 1002...)
    public string npcName;     // 이름
    public bool isBoss;        // 보스 여부

    [Header("호감도 보상 시스템")]
    // 기존 기능 유지
    public List<AffectionReward> affectionRewards;

    [Header("시각적 데이터")]
    // 이 NPC가 사용할 표정 스프라이트 모음집
    public SpriteLibraryAsset spriteLibraryAsset;

    [Header("초상화 연출 설정")]
    // [New] 이모티콘이 뜰 머리 위 위치 (캐릭터 키에 맞춰서 Y값 조절)
    public Vector2 emoteOffset = new Vector2(300f, 300f);
}