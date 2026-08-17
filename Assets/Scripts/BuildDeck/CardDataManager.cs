using System.Collections.Generic;
using UnityEngine;
using static CardData;

public class CardDataManager : MonoBehaviour
{
    public static CardDataManager Instance;

    public Dictionary<string, CardData> cardDic = new Dictionary<string, CardData>();
    public List<CardData> allCardList = new List<CardData>();

    void Awake()
    {
        Instance = this;
        LoadCardData();
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