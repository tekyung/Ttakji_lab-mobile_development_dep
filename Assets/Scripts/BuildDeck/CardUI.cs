using TCG_Project.Scripts.Core;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image cardImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI countText; // 1/2 표시용

    public Button plusButton;
    public Button minusButton;

    public string myCardID;
    public string myInstanceId;
    private DeckBuilderManager deckManager; // 매니저 참조 변수

    public Button removeAllButton;

    public GameObject deckCount; // 카드 개수, 카드 우측 하단
    public TextMeshProUGUI deckCountText;

    private bool isDeckMode = false; //덱 리스트인지 아닌지

    [Header("Card Back")]
    public GameObject cardBackObj;

    // 매니저가 이 함수를 호출해서 카드를 설정해줍니다.
    public void Setup(string id, int currentCount, int maxCount, DeckBuilderManager manager, bool isDeck)
    {
        myCardID = id;
        deckManager = manager;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);
        if (data == null)
        {
            Debug.LogError($"🚨 삐용삐용! 매니저가 카드를 못 찾았습니다! 찾으려던 ID: [{id}]");
            return;
        }

        // 2. 텍스트 & 이미지 적용
        if (nameText) nameText.text = data.name;
        // if (descText) descText.text = data.description;

        LoadCardImage(data.imagePath);

        isDeckMode = isDeck;

        // 3. 개수 표시
        UpdateCount(currentCount, maxCount);

        // 버튼 연결
        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners(); // 기존 연결 초기화
            plusButton.onClick.AddListener(() => {
                Debug.Log($"[클릭] + 버튼 눌림: {data.name} ({myCardID})"); // 로그로 확인
                deckManager.AddCard(myCardID);
            });
        }

        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => {
                Debug.Log($"[클릭] - 버튼 눌림: {data.name} ({myCardID})");
                deckManager.RemoveCard(myCardID);
            });
        }

        if (removeAllButton != null)
        {
            // 기존 연결 제거 (중복 방지)
            removeAllButton.onClick.RemoveAllListeners();

            // 새 기능 연결: 매니저의 RemoveAllCards 호출
            removeAllButton.onClick.AddListener(() => {
                deckManager.RemoveAllCards(myCardID);
            });
        }
    }

    private void LoadCardImage(string originalPath)
    {
        if (!CardImageLoader.ApplyToImage(cardImage, originalPath))
        {
            Debug.LogWarning(
                $"이미지 로드 실패! 경로: {CardImageLoader.NormalizeResourcesPath(originalPath)} / 원본: {originalPath}");
        }
    }

    private void ApplyDefaultCardBack()
    {
        if (cardBackObj != null)
            CardImageLoader.ApplyDefaultCardBack(cardBackObj);
    }

    // 개수만 따로 갱신하는 함수 (깜빡임 방지)
    public void UpdateCount(int current, int max)
    {
        if (isDeckMode)
        {
            if (countText) countText.gameObject.SetActive(false);

            if (deckCount) deckCount.SetActive(true);

            if (deckCountText) deckCountText.text = current.ToString();
        }
        else
        {
            if (deckCount) deckCount.SetActive(false);

            if (countText)
            {
                countText.gameObject.SetActive(true);
                if (current > 0) countText.text = $"{current}/{max}";
                else countText.text = ""; // 0장이면 숫자 숨김
            }
        }

        if (removeAllButton != null)
        {
            if (current > 0)
            {
                removeAllButton.gameObject.SetActive(true);
            }
            else
            {
                removeAllButton.gameObject.SetActive(false);
            }
        }
    }

    // + 버튼에 연결될 함수
    public void OnClickPlus()
    {
        deckManager.AddCard(myCardID);
    }

    // - 버튼에 연결될 함수
    public void OnClickMinus()
    {
        deckManager.RemoveCard(myCardID);
    }

    public void SetupForZoom(string id)
    {
        myCardID = id;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);
        if (data == null) return;

        // 2. 텍스트 & 이미지 적용 (기존 로직과 동일)
        if (nameText) nameText.text = data.name;
        //if (descText) descText.text = data.description; // 설명도 있다면 표시

        LoadCardImage(data.imagePath);

        // 3. [중요] 확대 화면에서는 필요 없는 것들 숨기기

        // 개수 텍스트 숨기기
        if (countText) countText.text = "";

        // 버튼들 비활성화 (눌러도 반응 안 하게)
        if (plusButton) plusButton.gameObject.SetActive(false);
        if (minusButton) minusButton.gameObject.SetActive(false);
    }

    public void SetupForBattle(string id)
    {
        myCardID = id;
        myInstanceId = null;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);
        if (data == null) return;

        // 2. 텍스트 설정
        if (nameText) nameText.text = data.name;
        //if (descText) descText.text = data.description; // 주석 해제하시면 설명도 뜹니다!

        LoadCardImage(data.imagePath);
        ApplyDefaultCardBack();

        // 4. 전투 씬에서는 필요 없는 '덱 편성용 UI' 전부 끄기
        if (countText) countText.gameObject.SetActive(false);
        if (deckCount) deckCount.SetActive(false);
        if (plusButton) plusButton.gameObject.SetActive(false);
        if (minusButton) minusButton.gameObject.SetActive(false);
        if (removeAllButton) removeAllButton.gameObject.SetActive(false);
    }

    public void BindEngineCard(Card card)
    {
        if (card == null)
            return;

        myCardID = card.Id;
        myInstanceId = card.InstanceId;
        gameObject.name = string.IsNullOrEmpty(card.InstanceId) ? card.Id : card.InstanceId;

        if (nameText) nameText.text = card.Name;

        string imagePath = card.ImagePath;
        if (string.IsNullOrEmpty(imagePath) && CardDataManager.Instance != null)
        {
            CardData data = CardDataManager.Instance.GetCard(card.Id);
            imagePath = data != null ? data.imagePath : null;
        }

        LoadCardImage(imagePath);
        ApplyDefaultCardBack();

        if (countText) countText.gameObject.SetActive(false);
        if (deckCount) deckCount.SetActive(false);
        if (plusButton) plusButton.gameObject.SetActive(false);
        if (minusButton) minusButton.gameObject.SetActive(false);
        if (removeAllButton) removeAllButton.gameObject.SetActive(false);
    }

    // 카드 뒤집는 함수
    public void SetFaceDown(bool isFaceDown)
    {
        if (cardBackObj != null)
        {
            // true면 뒷면 이불을 덮고, false면 이불을 치워서 앞면을 보여줍니다!
            cardBackObj.SetActive(isFaceDown);
        }
    }
}