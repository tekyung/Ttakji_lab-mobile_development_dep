// LongPressTrigger.cs — 덱 편집 화면 카드 슬롯의 포인터 입력 담당.
//
//   짧게 클릭   → 좌측 하단 미리보기를 이 카드로 바꾼다
//   길게 누르기 → 전체화면 확대 팝업을 연다 (예전부터 있던 동작)
//
// ★ 이름은 "LongPress"지만 두 조작을 함께 맡는다.
//   카드 슬롯에서 포인터를 받는 곳이 이미 여기라, 짧은 클릭만 얹는 편이
//   프리팹에 컴포넌트를 하나 더 붙이는 것보다 낫다고 보았다.
//
// ★ 드래그 스크롤과 부딪히지 않는다.
//   카드를 잡고 리스트를 끌면 uGUI가 드래그 대상(ScrollRect)이 누른 대상(카드)과 다른 것을 보고
//   eligibleForClick을 꺼 버린다. 그래서 OnPointerClick 자체가 오지 않는다 — 따로 막을 것이 없다.
//
// ★ +/− /[전부 빼기] 버튼은 이 오브젝트의 자식이라 그쪽이 먼저 클릭을 먹는다.
//   그래서 카드 몸통을 눌렀을 때만 미리보기가 바뀐다.
using UnityEngine;
using UnityEngine.EventSystems; // [필수] 터치/클릭 감지용

// IPointerDownHandler: 눌렀을 때
// IPointerUpHandler: 뗐을 때
// IPointerClickHandler: 짧게 클릭했을 때 (드래그였다면 오지 않는다)
public class LongPressTrigger : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    public string cardId; // 이 카드의 ID (매니저가 넣어줘야 함)

    private bool isPressed = false;
    private float pressTimer = 0f;
    private bool isZoomOpened = false; // 이미 열렸는지 체크

    /// <summary>매 프레임 찾지 않도록 한 번만 잡아 둔다. 슬롯이 수십 개라 값이 크다.</summary>
    private DeckBuilderManager _manager;

    void Start()
    {
        // 게임이 켜지거나 이 카드가 생길 때, JSON 파일이 로드되어 있는지 확실하게 확인!
        CommonConfigManager.LoadConfig();

        _manager = FindAnyObjectByType<DeckBuilderManager>();
    }

    /// <summary>씬을 오가며 참조가 끊어졌을 수 있다. 필요할 때 다시 찾는다.</summary>
    private DeckBuilderManager ResolveManager()
    {
        if (_manager == null) _manager = FindAnyObjectByType<DeckBuilderManager>();
        return _manager;
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
                DeckBuilderManager manager = ResolveManager();
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

    /// <summary>짧은 클릭 — 좌측 하단 미리보기를 이 카드로 바꾼다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 길게 눌러 확대 팝업이 이미 떴다면, 손을 뗄 때 미리보기까지 바꾸지는 않는다.
        if (isZoomOpened) return;

        if (string.IsNullOrEmpty(cardId)) return;

        DeckBuilderManager manager = ResolveManager();
        if (manager != null) manager.ShowCardPreview(cardId);
    }
}
