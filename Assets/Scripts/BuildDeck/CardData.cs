using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CardData
{
    public string id;
    public string characterId;
    public string name;
    public string type;
    public int speed;
    public int cost;
    public int max_deck_count;
    public string imagePath;

    /// <summary>
    /// 카드 효과 설명. RulebookCards.json에 원래부터 들어 있었는데
    /// 여기서 주석 처리돼 있어 화면에서 읽을 방법이 없었다(2026-09-02 되살림).
    /// 덱 편집 화면의 미리보기 패널이 이 값을 그대로 보여 준다.
    /// </summary>
    public string description;

    [System.Serializable]
    public class CardDataWrapper
    {
        public List<CardData> Card;
    }
}

/// <summary>
/// 용병(캐릭터) 카드. Character.json에서 읽는다.
/// 덱 편집 화면의 용병 슬롯이 이름·그림을 보여 주려면 필요하다
/// (RulebookCards.json에는 효과 카드 40종만 있고 용병 카드는 없다).
/// </summary>
[System.Serializable]
public class CharacterData
{
    public string id;           // "ELLI-01"
    public string characterId;  // "ELLIE" — 효과 카드의 characterId와 이걸로 짝짓는다
    public string name;
    public string description;
    public string imagePath;

    [System.Serializable]
    public class CharacterDataWrapper
    {
        public List<CharacterData> Characters;
    }
}
