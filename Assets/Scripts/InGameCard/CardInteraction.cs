using System;
using UnityEngine;
using UnityEngine.EventSystems; // 마우스/터치 이벤트를 처리하기 위해 꼭 필요합니다!

// IPointerDownHandler(누를 때), IPointerUpHandler(뗄 때), IPointerExitHandler(영역을 벗어날 때) 인터페이스를 상속받습니다.
public class CardInteraction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Zoom Settings")]
    public float holdTime = 0.5f; // 0.5초 동안 누르고 있으면 확대됨

    [Header("Drag Settings")]
    [Tooltip("화면 높이의 몇 % 이상 드래그해야 카드를 낸 것으로 판정할 것인가? (0.0 ~ 1.0)")]
    public float playZoneThreshold = 0.4f;

    private bool isPointerDown = false;
    private float pointerDownTimer = 0f;
    private bool isDragging = false;

    private bool isPlayed = false;

    private Transform originalParent;
    private int originalSiblingIndex;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        // CanvasGroup 컴포넌트가 없으면 자동으로 추가합니다.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        // 1. 마우스를 누르고 있는 동안 시간 체크
        if (isPointerDown)
        {
            pointerDownTimer += Time.deltaTime;

            // 2. 설정한 시간이 다 지나면 줌 기능 실행
            if (pointerDownTimer >= holdTime)
            {
                isPointerDown = false; // 계속 실행되는 것 방지
                ShowZoomPanel();
            }
        }
    }

    // 마우스(터치)를 누르기 시작했을 때
    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pointerDownTimer = 0f;
    }
    //------------------------------------
    // 마우스(터치)를 뗐을 때
    //------------------------------------
    public void OnPointerUp(PointerEventData eventData)
    {
        ResetPress();
        // 💡 나중에 여기에 "카드를 위로 드래그해서 뗐을 때 발동(Play)"하는 로직을 추가할 수 있습니다.
    }

    // 누른 상태로 카드 밖으로 마우스가 빠져나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        ResetPress(); // 의도치 않은 확대 방지
    }

    private void ResetPress()
    {
        isPointerDown = false;
        pointerDownTimer = 0f;
    }

    //-----------------------------
    //마우스 드래그
    //-----------------------------
    // 1. 드래그를 시작할 때
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlayed) return;

        Debug.Log("드래그 시작");
        isDragging = true;
        isPointerDown = false; // 드래그를 시작하면 줌 기능 취소

        // 원래 있던 패 영역(HandArea)과 순서를 기억해 둡니다.
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        // 카드를 화면 맨 앞(Canvas 직속)으로 빼내어 LayoutGroup의 속박에서 벗어납니다.
        transform.SetParent(transform.root);

        // 드래그 중에는 마우스 레이캐스트를 무시하게 해서, 카드 뒤에 있는 필드나 UI를 인식할 수 있게 합니다.
        canvasGroup.blocksRaycasts = false;


    }

    // 2. 드래그 중일 때 (마우스 따라다니기)
    public void OnDrag(PointerEventData eventData)
    {
        if (isPlayed) return;

        Debug.Log("드래그 이동중");
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)transform.parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 globalMousePos);

        // 카드의 위치를 마우스(터치) 위치로 이동시킵니다.
        transform.position = globalMousePos;
    }

    // 3. 드래그를 끝냈을 때 (마우스에서 손을 뗐을 때)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlayed) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        // 마우스 포인터가 가리키고 있는 UI 오브젝트를 가져옵니다.
        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;

        // 마우스를 놓은 곳이 허공이 아니라면?
        if (dropTarget != null)
        {
            // 놓은 곳이나, 그 부모 오브젝트 중에 'DropZone' 명찰이 있는지 찾습니다.
            // (카드 위에 겹쳐서 놓더라도 부모인 FieldArea를 찾아냅니다!)
            DropZone zone = dropTarget.GetComponentInParent<DropZone>();

            if (zone != null)
            {
                // 명찰을 찾았다면, 해당 구역(zone.transform)으로 카드를 냅니다!
                PlayThisCard(zone.transform);
                return; // 여기서 함수 종료
            }
        }

        // DropZone을 못 찾았다면 무조건 패로 돌아갑니다.
        ReturnToHand();
    }

    private void ShowZoomPanel()
    {
        Debug.Log("🔍 카드 꾹 누르기 성공! 줌 패널 띄우기");
         InGameUIManager.Instance.ShowCardZoom();
    }

    // 카드를 패로 다시 돌려보내는 함수
    private void ReturnToHand()
    {
        Debug.Log("↩️ 카드 사용 취소. 패로 돌아갑니다.");
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex); // 원래 있던 순서(위치) 그대로 쏙 들어갑니다!
    }

    // 카드를 사용(필드에 냄)하는 함수
    private void PlayThisCard()
    {
        Debug.Log("⚔️ 카드 사용! 필드로 출격!");

        // 일단 테스트용으로 카드를 투명하게 만들거나 파괴해봅시다.
        // Destroy(gameObject); 

        // 💡 나중에 여기에 "BattleManager.Instance.PlayCard(this.cardData);" 같은 로직을 연결하면 됩니다.
    }

    private void PlayThisCard(Transform fieldTransform)
    {
        Debug.Log("⚔️ 카드 사용! 필드로 쏙 들어갑니다!");

        // 1. 카드의 부모를 필드(FieldArea)로 완전히 바꿔줍니다.
        transform.SetParent(fieldTransform);

        // 2. 카드의 위치를 부모(필드 칸)의 정중앙(0, 0)으로 강제 이동시킵니다! (자석 효과)
        RectTransform rect = GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = Vector2.zero;

        isPlayed = true;

        // (선택) 혹시 카드가 회전해 있다면 똑바로 세워줍니다.
        rect.localRotation = Quaternion.identity;

        // (선택) 필드에 놓인 카드는 더 이상 조작 못하게 막기
        // Destroy(this); // 이 코드를 주석 해제하면 필드에 나간 카드는 더 이상 드래그되지 않습니다.
    }
}