using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class MyHandManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform handArea;     // 카드가 놓일 곳 오브젝트 (HandArea)
    public GameObject cardPrefab;  // 생성할 임시 카드 프리팹

    public HorizontalLayoutGroup handLayoutGroup;

    [Header("Settings")]
    public int maxHandSize = 10;   // 패의 최대 개수
    public float cardWidth = 200f;

    private List<int> currentDrawPile = new List<int>();

    private void Start()
    {
        LoadDeckFromJSON(); // 게임 시작하자마자 덱을 불러옵니다!
    }

    private void LoadDeckFromJSON()
    {
        // 1. 메인 화면에서 저장했던 덱 이름 가져오기
        string deckName = PlayerPrefs.GetString("SelectedDeckName", "");

        if (string.IsNullOrEmpty(deckName) || deckName == "덱 없음")
        {
            Debug.LogError("🚨 선택된 덱이 없거나 오류가 발생했습니다!");
            return;
        }

        // 2. 파일 경로 찾기 (MainDeckSelector랑 똑같은 경로!)
        string filePath = Path.Combine(Application.dataPath, "MyDeck", deckName + ".json");

        if (File.Exists(filePath))
        {
            // 3. JSON 파일 읽기
            string jsonData = File.ReadAllText(filePath);

            // ⭐ 4. 잠기님의 형식(DeckSaveData)에 맞춰서 해독하기!
            DeckSaveData loadedData = JsonUtility.FromJson<DeckSaveData>(jsonData);

            if (loadedData != null && loadedData.cardIdList != null)
            {
                // 해독한 데이터의 cardIdList를 현재 뽑을 덱으로 복사합니다.
                currentDrawPile = new List<int>(loadedData.cardIdList);
                Debug.Log($"✨ [{deckName}] 덱을 성공적으로 불러왔습니다! 남은 카드: {currentDrawPile.Count}장");
            }
            else
            {
                Debug.LogError("🚨 JSON 데이터는 찾았으나, 형식이 맞지 않거나 비어있습니다.");
            }
        }
        else
        {
            Debug.LogError($"🚨 {deckName}.json 파일을 찾을 수 없습니다!");
        }
    }

    public void DrawCard()
    {
        if (handArea.childCount >= maxHandSize) return;
        if (currentDrawPile.Count <= 0) return;

        int randomIndex = Random.Range(0, currentDrawPile.Count);
        int drawnCardID = currentDrawPile[randomIndex];
        currentDrawPile.RemoveAt(randomIndex);

        GameObject newCard = Instantiate(cardPrefab);
        newCard.transform.SetParent(handArea, false);

        // ⭐ [자동 크기 조절 시작]
        RectTransform handRect = handArea.GetComponent<RectTransform>();
        RectTransform cardRect = newCard.GetComponent<RectTransform>();

        // 1. 패널(HandArea)의 현재 높이를 가져옵니다. 
        // (패널에 너무 꽉 차면 안 예쁘니 0.95f를 곱해 위아래로 5%의 여백을 줍니다!)
        float targetHeight = handRect.rect.height * 0.95f;

        // 2. 카드의 원래 가로/세로 크기를 확인합니다.
        float originalHeight = cardRect.rect.height;
        float originalWidth = cardRect.rect.width;

        // 3. 카드를 얼마나 줄여야 하는지 '비율'을 계산합니다.
        float scaleRatio = targetHeight / originalHeight;

        // 4. 계산된 비율만큼 카드의 가로/세로 크기를 덮어씌웁니다. (이미지가 찌그러지지 않음!)
        cardRect.sizeDelta = new Vector2(originalWidth * scaleRatio, originalHeight * scaleRatio);

        // 5. [매우 중요] 간격 계산기(UpdateSpacingRoutine)가 안 고장 나도록, cardWidth 변수도 동기화!
        cardWidth = originalWidth * scaleRatio;
        // ⭐ [자동 크기 조절 끝]

        CardUI cardUI = newCard.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.SetupForBattle(drawnCardID);
        }

        StartCoroutine(UpdateSpacingRoutine());
    }

    private IEnumerator UpdateSpacingRoutine()
    {
        // 유니티 시스템이 UI 크기 배치를 마칠 때까지 한 프레임 대기
        yield return null;

        int cardCount = handArea.childCount;
        if (cardCount < 2)
        {
            handLayoutGroup.spacing = 0;
            yield break; // 코루틴 종료
        }

        RectTransform handRect = handArea.GetComponent<RectTransform>();
        float panelWidth = handRect.rect.width;
        float totalCardWidth = cardWidth * cardCount;

        if (totalCardWidth > panelWidth)
        {
            float neededSpacing = (panelWidth - totalCardWidth) / (cardCount - 1);
            handLayoutGroup.spacing = neededSpacing;
        }
        else
        {
            handLayoutGroup.spacing = -20f;
        }
    }
}