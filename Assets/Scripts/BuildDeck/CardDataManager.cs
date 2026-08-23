using System.Collections.Generic;
using UnityEngine;
using static CardData;

public class CardDataManager : MonoBehaviour
{
    public static CardDataManager Instance;

    public Dictionary<string, CardData> cardDic = new Dictionary<string, CardData>();
    public List<CardData> allCardList = new List<CardData>();

    /// <summary>용병 4종. Character.json의 순서(엘리·베로니카·다이나·소니아)를 그대로 따른다.</summary>
    public List<CharacterData> allCharacterList = new List<CharacterData>();

    void Awake()
    {
        Instance = this;
        LoadCardData();
        LoadCharacterData();
    }

    /// <summary>
    /// 카드가 <see cref="allCardList"/>에서 몇 번째인가. 모르는 카드는 맨 뒤로 보낸다.
    ///
    /// ★ RulebookCards.json의 순서가 이미 <b>엘리 → 베로니카 → 다이나 → 소니아</b>이고,
    ///   각 용병 안에서 <b>공격 → 방어 → 지원</b>이다.
    ///   따라서 이 인덱스로만 정렬해도 기획이 요구한 두 조건이 모두 충족된다.
    ///   정렬 규칙을 따로 만들면 JSON과 어긋나기 쉬우므로 이 함수 하나만 쓴다.
    /// </summary>
    public int GetSortOrder(string cardId)
    {
        for (int i = 0; i < allCardList.Count; i++)
            if (allCardList[i].id == cardId) return i;

        return int.MaxValue;
    }

    void LoadCharacterData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("GameData/Character");
        if (jsonFile == null)
        {
            Debug.LogError("Character.json을 찾을 수 없습니다! Assets/Resources/GameData/Character.json 확인 필요");
            return;
        }

        var wrapper = JsonUtility.FromJson<CharacterData.CharacterDataWrapper>(jsonFile.text);
        if (wrapper?.Characters == null)
        {
            Debug.LogError("Character.json 형식이 잘못되었습니다.");
            return;
        }

        allCharacterList = wrapper.Characters;
        Debug.Log($"용병 데이터 로드 완료! 총 {allCharacterList.Count}명");
    }

    /// <summary>characterId("ELLIE")로 용병을 찾는다.</summary>
    public CharacterData GetCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        return allCharacterList.Find(c => c != null && c.characterId == characterId);
    }

    void LoadCardData()
    {
        // 1. Resources 폴더에서 JSON 파일 읽어오기 (확장자 .json 제외)
        TextAsset jsonFile = Resources.Load<TextAsset>("GameData/RulebookCards");

        if (jsonFile == null)
        {
            Debug.LogError("JSON 파일을 찾을 수 없습니다! Assets/Resources/GameData/RulebookCards.json 확인 필요");
            return;
        }

        // 2. JSON 문자열을 객체로 변환 (역직렬화)
        // 껍데기(Wrapper) 클래스를 통해 리스트를 통째로 가져옵니다.
        CardDataWrapper wrapper = JsonUtility.FromJson<CardDataWrapper>(jsonFile.text);

        // 3. 딕셔너리와 리스트에 정리해 넣기
        if (wrapper != null && wrapper.Card != null)
        {
            allCardList = wrapper.Card;

            foreach (CardData card in allCardList)
            {
                if (!cardDic.ContainsKey(card.id))
                {
                    cardDic.Add(card.id, card);
                }
            }
            Debug.Log($"카드 데이터 로드 완료! 총 {allCardList.Count}장");
        }
        else
        {
            Debug.LogError("JSON 형식이 잘못되었습니다.");
        }
    }

    // ID로 카드 정보 가져오는 함수 (기존과 동일)
    public CardData GetCard(string id)
    {
        if (cardDic.ContainsKey(id))
            return cardDic[id];
        return null;
    }
}