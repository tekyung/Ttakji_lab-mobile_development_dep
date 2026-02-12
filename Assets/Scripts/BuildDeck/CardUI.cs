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

    public int myCardID;
    private DeckBuilderManager deckManager; // 매니저 참조 변수

    public Button removeAllButton;

    public GameObject deckCount; // 카드 개수, 카드 우측 하단
    public TextMeshProUGUI deckCountText;

    private bool isDeckMode = false; //덱 리스트인지 아닌지

    // 매니저가 이 함수를 호출해서 카드를 설정해줍니다.
    public void Setup(int id, int currentCount, int maxCount, DeckBuilderManager manager, bool isDeck)
    {
        myCardID = id;
        deckManager = manager;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);

        // 2. 텍스트 & 이미지 적용
        if (nameText) nameText.text = data.name;
        // if (descText) descText.text = data.description;

        // 이미지 로드 (Resources 폴더 기준)
        LoadCardImage(data.skin_res);

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
        // CSV 경로: "asset/m1_tmp/card_img/u_slime.png"
        // 목표 경로: "card_img/u_slime" (Resources 폴더 기준)

        string path = originalPath;

        // 1. 불필요한 앞부분 경로 삭제
        path = path.Replace("Assets/Resources/", ""); // 가장 중요한 부분
        //path = path.Replace("asset/", "");        // 혹시 몰라 추가

        // 2. 확장자 삭제
        path = path.Replace(".png", "").Replace(".jpg", "");

        // 3. 로드
        Sprite sp = Resources.Load<Sprite>(path);

        // 4. 적용
        if (sp != null && cardImage != null)
        {
            cardImage.sprite = sp;
        }
        else
        {
            // 디버깅용: 이미지가 하얗게 나오면 콘솔창을 확인하세요!
             Debug.LogWarning($"이미지 로드 실패! 최종 경로: {path} / 원본: {originalPath}");
        }
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

    public void SetupForZoom(int id)
    {
        myCardID = id;

        // 1. 데이터 불러오기
        CardData data = CardDataManager.Instance.GetCard(id);
        if (data == null) return;

        // 2. 텍스트 & 이미지 적용 (기존 로직과 동일)
        if (nameText) nameText.text = data.name;
        //if (descText) descText.text = data.description; // 설명도 있다면 표시

        // 이미지 로드
        string path = data.skin_res.Replace(".png", "").Replace("Assets/Resources/", "");
        path = path.Replace(".png", "").Replace(".jpg", "");
        Sprite sp = Resources.Load<Sprite>(path);
        if (sp && cardImage) cardImage.sprite = sp;

        // 3. [중요] 확대 화면에서는 필요 없는 것들 숨기기

        // 개수 텍스트 숨기기
        if (countText) countText.text = "";

        // 버튼들 비활성화 (눌러도 반응 안 하게)
        if (plusButton) plusButton.gameObject.SetActive(false);
        if (minusButton) minusButton.gameObject.SetActive(false);
    }
}