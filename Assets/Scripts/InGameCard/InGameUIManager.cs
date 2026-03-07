using UnityEngine;
using UnityEngine.UI;
// using TMPro; // 텍스트를 TextMeshPro로 쓴다면 주석 해제하세요

public class InGameUIManager : MonoBehaviour
{
    // 어디서든 BattleUIManager.Instance 로 접근할 수 있게 해주는 마법의 코드
    public static InGameUIManager Instance;

    [Header("Zoom Panel UI")]
    public GameObject popupPanel;     // 뒤에 깔리는 검은 반투명 배경 (아까 만든 Background_Dimmer)
    public GameObject cardZoomPanel;  // 실제 카드가 커지는 패널
    // public Image zoomCardImage;    // 나중에 카드 이미지 바꿀 때 쓸 변수
    // public TextMeshProUGUI zoomCardName; // 나중에 이름 바꿀 때 쓸 변수

    private void Awake()
    {
        // 씬이 시작될 때 자기 자신을 Instance에 등록
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 카드를 꾹 눌렀을 때 호출될 함수
    public void ShowCardZoom() // 나중에 여기에 (CardData data) 를 넘겨받아서 이미지와 텍스트를 세팅하면 됩니다.
    {
        if (popupPanel != null) popupPanel.SetActive(true);
        if (cardZoomPanel != null) cardZoomPanel.SetActive(true);

        Debug.Log("🔍 줌 패널 켜짐!");
    }

    // 배경을 터치해서 닫을 때 호출될 함수
    public void CloseCardZoom()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
        if (cardZoomPanel != null) cardZoomPanel.SetActive(false);
    }
}