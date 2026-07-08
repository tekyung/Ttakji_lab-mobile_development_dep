using UnityEngine;
using UnityEngine.UI;
using TMPro; // 텍스트를 TextMeshPro로 쓴다면 주석 해제하세요

public class InGameUIManager : MonoBehaviour
{
    // 어디서든 BattleUIManager.Instance 로 접근할 수 있게 해주는 마법의 코드
    public static InGameUIManager Instance;

    [Header("Zoom Panel UI")]
    public GameObject popupPanel;     // 뒤에 깔리는 검은 반투명 배경 (아까 만든 Background_Dimmer)
    public GameObject cardZoomPanel;  // 실제 카드가 커지는 패널
     public Image zoomCardImage;    // 나중에 카드 이미지 바꿀 때 쓸 변수
     public TextMeshProUGUI zoomCardName; // 이름 변수
    public TextMeshProUGUI zoomCardDesc; // 설명 변수

    private CardInteraction currentZoomedCard; //원본 카드 기억

    private void Awake()
    {
        // 씬이 시작될 때 자기 자신을 Instance에 등록
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 카드를 꾹 눌렀을 때 호출될 함수
    public void ShowCardZoom(CardInteraction targetCard)
    {
        // 1. 어떤 카드가 나를 불렀는지 기억합니다.
        currentZoomedCard = targetCard;

        // 2. 타겟 카드의 일러스트를 줌 패널의 큰 이미지에 복사합니다.
        CardUI cardUI = targetCard.GetComponent<CardUI>();
        if (cardUI != null)
        {
            // 1. 이미지 복사
            if (zoomCardImage != null && cardUI.cardImage != null)
            {
                zoomCardImage.sprite = cardUI.cardImage.sprite;

                Debug.Log($"📸 줌 패널 사진 복사 완료! 가져온 사진 이름: {cardUI.cardImage.sprite.name}");
            }

            // ⭐ 2. 카드 이름 복사
            // (주의: cardUI.cardNameText 부분은 CardUI 스크립트에 있는 '이름 변수명'으로 맞춰주세요!)
            if (zoomCardName != null && cardUI.nameText != null)
            {
                zoomCardName.text = cardUI.nameText.text;
            }

            // ⭐ 3. 카드 설명 복사
            // (주의: cardUI.cardDescText 부분은 CardUI 스크립트에 있는 '설명 변수명'으로 맞춰주세요!)
            if (zoomCardDesc != null && cardUI.descText != null)
            {
                zoomCardDesc.text = cardUI.descText.text;
            }
        }

        if (popupPanel != null) popupPanel.SetActive(true);
        if (cardZoomPanel != null) cardZoomPanel.SetActive(true);

        Debug.Log("🔍 줌 패널 켜짐! 원본 카드 기억 완료.");
    }

    // 배경을 터치해서 닫을 때 호출될 함수
    public void CloseCardZoom()
    {
        currentZoomedCard = null;
        if (popupPanel != null) popupPanel.SetActive(false);
        if (cardZoomPanel != null) cardZoomPanel.SetActive(false);
    }

    public void OnClickZoomReveal()
    {
        if (currentZoomedCard != null)
        {
            // 원본 카드의 '공개' 함수를 대신 실행해 주고, 나는 퇴장합니다!
            currentZoomedCard.OnClickReveal();
            CloseCardZoom();
        }
    }

    public void OnClickZoomDiscard()
    {
        if (currentZoomedCard != null)
        {
            // 원본 카드의 '폐기' 함수를 대신 실행해 주고, 나는 퇴장합니다!
            currentZoomedCard.OnClickDiscard();
            CloseCardZoom();
        }
    }
}