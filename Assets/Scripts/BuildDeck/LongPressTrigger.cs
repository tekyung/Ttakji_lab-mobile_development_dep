using UnityEngine;
using UnityEngine.EventSystems; // [필수] 터치/클릭 감지용

// IPointerDownHandler: 눌렀을 때
// IPointerUpHandler: 뗐을 때
public class LongPressTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public string cardId; // 이 카드의 ID (매니저가 넣어줘야 함)

    private bool isPressed = false;
    private float pressTimer = 0f;
    private bool isZoomOpened = false; // 이미 열렸는지 체크

    void Start()
    {
        // 게임이 켜지거나 이 카드가 생길 때, JSON 파일이 로드되어 있는지 확실하게 확인!
        CommonConfigManager.LoadConfig();
    }

    void Update()
    {
        // 누르고 있는 동안 시간을 잽니다.
        if (isPressed)
        {
            pressTimer += Time.deltaTime;

            if (pressTimer >= CommonConfigManager.card_zoom_hold_duration_time && isZoomOpened == false)
            {
                isZoomOpened = true; // 중복 실행 방지

                // 매니저를 찾아서 팝업 열기 실행!
                DeckBuilderManager manager = FindAnyObjectByType<DeckBuilderManager>();
                if (manager != null)
                {
                    manager.OpenCardZoom(cardId);
                }
            }
        }
    }

    // 눌렀을 때 (타이머 시작)
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressTimer = 0f;
        isZoomOpened = false;
    }

    // 손을 뗐을 때 (타이머 초기화)
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        pressTimer = 0f;
    }

    // 누르다가 카드 밖으로 손 나가면 취소
    public void OnPointerExit(PointerEventData eventData)
    {
        isPressed = false;
        pressTimer = 0f;
    }
}