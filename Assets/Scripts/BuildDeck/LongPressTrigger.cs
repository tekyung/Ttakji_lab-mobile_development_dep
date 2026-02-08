using UnityEngine;
using UnityEngine.EventSystems; // [필수] 터치/클릭 감지용

// IPointerDownHandler: 눌렀을 때
// IPointerUpHandler: 뗐을 때
public class LongPressTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int cardId; // 이 카드의 ID (매니저가 넣어줘야 함)

    private bool isPressed = false;
    private float pressTimer = 0f;
    private float holdDuration = 1f; // 1.5초 동안 눌러야 함
    private bool isZoomOpened = false; // 이미 열렸는지 체크

    void Update()
    {
        // 누르고 있는 동안 시간을 잽니다.
        if (isPressed)
        {
            pressTimer += Time.deltaTime;

            // 1.5초가 넘었고, 아직 팝업이 안 열렸다면?
            if (pressTimer >= holdDuration && isZoomOpened == false)
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