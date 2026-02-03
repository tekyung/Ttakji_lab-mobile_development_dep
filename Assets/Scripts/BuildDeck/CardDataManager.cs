using System.Collections.Generic;
using UnityEngine;

public class CardDataManager : MonoBehaviour
{
    public static CardDataManager Instance;

    public Dictionary<int, CardData> CardDict = new Dictionary<int, CardData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeData()
    {
        LoadFromLocalCSV();
    }

    // 일단 임시로 csv에서 불러옴
    void LoadFromLocalCSV()
    {
        var baseCsv = Resources.Load<TextAsset>("Card");
        if (baseCsv == null)
        {
            Debug.LogError("Card.csv 파일을 찾을 수 없습니다!");
            return;
        }

        // 윈도우 줄바꿈(\r) 제거 후 자르기
        string[] lines = baseCsv.text.Replace("\r", "").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = line.Split(',');

            // [핵심 수정] int.Parse 대신 int.TryParse 사용
            // 첫 번째 칸(cols[0])이 숫자가 아니면(헤더, 타입설명 등) 그냥 스킵합니다.
            if (!int.TryParse(cols[0], out int id))
            {
                continue;
            }

            // CSV 순서: 0:id, 1:name, 2:type, 3:max_deck_count, 4:skin_res
            string name = cols[1];
            string type = cols[2];

            // 숫자 파싱 (실패 시 기본값 0)
            int.TryParse(cols[3], out int maxCount);

            string skinRes = cols[4];

            // 데이터 생성 및 딕셔너리에 추가
            CardData newCard = new CardData(id, name, type, maxCount, skinRes);

            if (!CardDict.ContainsKey(id))
            {
                CardDict.Add(id, newCard);
            }
        }

        Debug.Log($"카드 데이터 로드 완료! 총 {CardDict.Count}개");
    }

    public CardData GetCard(int id)
    {
        if (CardDict.TryGetValue(id, out CardData data))
            return data;
        return null;
    }
}