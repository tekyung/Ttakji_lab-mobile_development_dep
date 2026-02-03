using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image cardImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI countText; // 1/2 표시용

    public Button plusButton;
    public Button minusButton;

    public int myCardID;
    private DeckBuilderManager deckManager; // 매니저 참조 변수

    // 매니저가 이 함수를 호출해서 카드를 설정해줍니다.
    public void Setup(int id, int currentCount, int maxCount, DeckBuilderManager manager)
    {
        myCardID = id;
        deckManager = manager;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);

        // 2. 텍스트 & 이미지 적용
        if (nameText) nameText.text = data.name;
        // if (descText) descText.text = data.description;

        // 이미지 로드 (Resources 폴더 기준)
        string path = data.skinPath.Replace(".png", "").Replace("asset/m1_tmp/", "");
        Sprite sp = Resources.Load<Sprite>(path);
        if (sp && cardImage) cardImage.sprite = sp;

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
    }

    // 개수만 따로 갱신하는 함수 (깜빡임 방지)
    public void UpdateCount(int current, int max)
    {
        if (countText)
        {
            if (current > 0) countText.text = $"{current}/{max}";
            else countText.text = ""; // 0장이면 숫자 숨김
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

}