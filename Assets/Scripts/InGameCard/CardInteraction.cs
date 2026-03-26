using System;
using UnityEngine;
using UnityEngine.EventSystems; // 마우스/터치 이벤트를 처리하기 위해 꼭 필요합니다!

// IPointerDownHandler(누를 때), IPointerUpHandler(뗄 때), IPointerExitHandler(영역을 벗어날 때) 인터페이스를 상속받습니다.
public class CardInteraction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Zoom Settings")]
    public float holdTime = 0.5f; // 0.5초 동안 누르고 있으면 확대됨

    [Header("Drag Settings")]
    [Tooltip("화면 높이의 몇 % 이상 드래그해야 카드를 낸 것으로 판정할 것인가? (0.0 ~ 1.0)")]
    public float playZoneThreshold = 0.4f;

    [Header("Select Settings")]
    public float focusScaleFactor = 1.2f;
    public GameObject actionButtonPanel; // 공개, 폐기 버튼
    private bool isSelected = false; // 현재 내가 선택되었는가
    private static CardInteraction currentlySelectedCard; // 현재 카드 기억하기
    public bool isInSetZone = false; // 네트존에 있는가

    [Header("Drag to Center Settings")]
    public GameObject actionButtonPanelBottom; // 하단 공개, 폐기 버튼
    public float dragUpThreshold = 100f; // ⭐ 이 픽셀 이상 위로 드래그하면 중앙으로 인식합니다.

    private Vector3 startDragPosition;    // 드래그를 시작한 위치 기억용

    private bool isPointerDown = false;
    private float pointerDownTimer = 0f;
    private bool isDragging = false;

    private bool isPlayed = false;

    private Transform originalParent;
    private int originalSiblingIndex;

    private Canvas myCanvas;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        // CanvasGroup 컴포넌트가 없으면 자동으로 추가합니다.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // ⭐ 카드가 개별적인 렌더링 순서를 가질 수 있도록 Canvas를 달아줍니다.
        myCanvas = GetComponent<Canvas>();
        if (myCanvas == null) myCanvas = gameObject.AddComponent<Canvas>();

        // Canvas를 추가하면 클릭이 먹통이 될 수 있어 GraphicRaycaster도 짝꿍으로 달아줍니다.
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    void Update()
    {
        if (isInSetZone) return; // 세트존에 있을 때 누르기 금지

        if (isPointerDown && !isDragging)
        {
            pointerDownTimer += Time.deltaTime;

            if (pointerDownTimer >= holdTime)
            {
                isPointerDown = false; // 계속 실행되는 것 방지
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isInSetZone || isPlayed || isDragging) return; // 필드에 나갔거나 드래그 중이면 무시

        if (isSelected)
        {
            // 이미 튀어나와 있는 상태에서 또 누르면 -> 원상복구
            DeselectCard();
        }
        else
        {
            // 안 튀어나와 있다면 -> 앞으로 꺼내기
            SelectCard();
        }
    }

    private void SelectCard()
    {
        // 1. 만약 내 패의 "다른 카드"가 이미 튀어나와 있다면, 그 녀석을 먼저 집어넣습니다.
        if (currentlySelectedCard != null && currentlySelectedCard != this)
        {
            currentlySelectedCard.DeselectCard();
        }

        isSelected = true;
        currentlySelectedCard = this;

        // 2. 카드를 살짝 키움
        transform.localScale = new Vector3(focusScaleFactor, focusScaleFactor, 1f);

        // 3. 레이아웃 그룹의 순서를 무시하고 '무조건 화면 맨 앞'에 그리도록 덮어씌웁니다!
        myCanvas.overrideSorting = true;
        myCanvas.sortingOrder = 10;

        if (actionButtonPanel != null) actionButtonPanel.SetActive(true);
    }

    public void DeselectCard()
    {
        if (!isSelected) return;

        isSelected = false;
        if (currentlySelectedCard == this) currentlySelectedCard = null;

        // 1. 키웠던 카드를 다시 원래 높이로 빼줍니다.
        transform.localScale = Vector3.one;

        // 2. 맨 앞 그리기 취소 (다시 패 사이에 얌전히 들어감)
        myCanvas.overrideSorting = false;

        if (actionButtonPanel != null) actionButtonPanel.SetActive(false);
    }

    // 회수 버튼
    public void OnClickReturn()
    {
        Debug.Log("🔄 회수 버튼 클릭! 카드를 다시 패로 가져옵니다.");

        isInSetZone = false;

        // 1. 씬에서 패(HandArea)를 찾습니다. 
        GameObject handArea = GameObject.Find("MyHand");

        if (handArea != null)
        {
            // 2. 카드의 부모를 다시 패(HandArea)로 바꿉니다.
            // Horizontal Layout Group이 알아서 카드를 패의 오른쪽 끝에 예쁘게 정렬해 줍니다.
            transform.SetParent(handArea.transform);

            // 원래 인덱스 위치로 이동
            transform.SetSiblingIndex(originalSiblingIndex);

            // 3. 필드에 나갔다는 상태를 해제! (이제 다시 드래그/확대가 가능해집니다)
            isPlayed = false;

            // 4. 뒷면 이불을 치우고 다시 앞면을 보여줍니다.
            CardUI cardUI = GetComponent<CardUI>();
            if (cardUI != null) cardUI.SetFaceDown(false);

            // 5. 손패에 카드가 다시 늘어났으니, 간격(Spacing)을 다시 예쁘게 맞춰줍니다.
            // (이전에 작성하신 HandManager의 코루틴을 원격으로 실행합니다)
            MyHandManager handManager = FindAnyObjectByType<MyHandManager>();

            if (handManager != null)
            {
                // 카드 간격 맞추는 김에 버튼 끄기도 같이 시킵니다!
                handManager.StartCoroutine("UpdateSpacingRoutine");
                handManager.SetReadyButtonState(false);
            }

            DeselectCard();

            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.CancelSet();
            }
        }
        else
        {
            Debug.LogError("🚨 HandArea(손패 패널)를 찾을 수 없습니다! 이름을 확인해주세요.");
        }
    }

    //-----------------------------
    //마우스 드래그
    //-----------------------------
    // 1. 드래그를 시작할 때
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlayed || isInSetZone) return;
        if (isSelected) DeselectCard();

        isDragging = true;
        isPointerDown = false; // 드래그를 시작하면 줌 기능 취소

        // 원래 있던 패 영역(HandArea)과 순서를 기억해 둡니다.
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        // 드래그 시작 위치 기억
        startDragPosition = transform.position;

        // 카드를 화면 맨 앞(Canvas 직속)으로 빼내어 LayoutGroup의 속박에서 벗어납니다.
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        transform.SetParent(transform.root);

        transform.localScale = Vector3.one;
        // 드래그 중에는 마우스 레이캐스트를 무시하게 해서, 카드 뒤에 있는 필드나 UI를 인식할 수 있게 합니다.
        canvasGroup.blocksRaycasts = false;
    }

    // 2. 드래그 중일 때 (마우스 따라다니기)
    public void OnDrag(PointerEventData eventData)
    {
        if (isPlayed || isInSetZone) return;

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
        if (isPlayed || isInSetZone) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        if (transform.position.y > startDragPosition.y + dragUpThreshold)
        {
            ShowZoomPanel();
        }
            ReturnToHand();
    }

    private void ShowZoomPanel()
    {
        Debug.Log("🌟 위로 드래그 성공! 전용 줌 패널 띄우기");

        // ⭐ InGameUIManager에게 '나 자신(this)'을 넘겨주며 줌 패널을 띄워달라고 요청합니다.
        if (InGameUIManager.Instance != null)
        {
            InGameUIManager.Instance.ShowCardZoom(this);
        }
    }

    // 카드를 패로 다시 돌려보내는 함수
    private void ReturnToHand()
    {

        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);

        RectTransform rect = GetComponent<RectTransform>();

        // ⭐ [추가됨] 패로 돌아왔으니, 다른 카드들과 밑선이 맞도록 피벗을 다시 발바닥(Y: 0)으로 돌려놓습니다!
        rect.pivot = new Vector2(0.5f, 0.5f);

        transform.localScale = Vector3.one;
        myCanvas.overrideSorting = false;

        MyHandManager handManager = FindAnyObjectByType<MyHandManager>();
        if (handManager != null) handManager.StartCoroutine("UpdateSpacingRoutine");
    }

    // 카드를 사용(필드에 냄)하는 함수
    private void PlayThisCard(Transform fieldTransform)
    {
        Debug.Log("⚔️ 카드 사용! 필드로 쏙 들어갑니다!");

        // 카드 인덱스 기억하기
        originalSiblingIndex = transform.GetSiblingIndex();

        // 1. 카드의 부모를 필드(FieldArea)로 완전히 바꿔줍니다.
        transform.SetParent(fieldTransform);

        // 2. 카드의 위치를 부모(필드 칸)의 정중앙(0, 0)으로 강제 이동시킵니다! (자석 효과)
        RectTransform rect = GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = Vector2.zero;

        isPlayed = true;

        CardUI cardUI = GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.SetFaceDown(true); // true = 뒷면으로 덮기!
        }

        MyHandManager handManager = FindAnyObjectByType<MyHandManager>();
        if (handManager != null)
        {
            handManager.SetReadyButtonState(true);
        }
    }

    //--------------------------------------
    // 공개 , 폐기 버튼
    //--------------------------------------

    // '공개' 버튼을 눌렀을 때 실행됩니다.
    public void OnClickReveal()
    {
        Debug.Log("👁️ 공개 버튼 클릭! 세트 필드로 이동합니다.");
        SendToSetField(true);
    }

    // '폐기' 버튼을 눌렀을 때 실행됩니다.
    public void OnClickDiscard()
    {
        Debug.Log("🗑️ 폐기 버튼 클릭! 세트 필드로 이동합니다.");
        SendToSetField(false);
    }

    // 카드를 찾아내서 세트 필드로 쏘아 보내는 공통 함수
    private void SendToSetField(bool isReveal)
    {
        // 씬(Scene)에서 '세트 필드' 역할을 하는 오브젝트를 이름으로 찾습니다.
        GameObject setField = GameObject.Find("set");

        if (setField != null)
        {
            if (setField.transform.childCount >= 2) // 세트존 이라는 글자 때문에 2로 둠
            {
                Debug.LogWarning("⚠️ 이미 세트 존에 카드가 있습니다! 더 이상 놓을 수 없습니다.");

                DeselectCard();
                return;
            }
            // 아까 만들어둔 완벽한 이동 함수를 불러서 필드 중앙에 꽂아버립니다!
            DeselectCard();
            //PlayThisCard(setField.transform);
            // ⭐ 1. 내 카드에 적힌 ID를 가져옵니다.
            string myId = GetComponent<CardUI>().myCardID;

            // ⭐ 2. 내 UI 매니저에게 "나 이 카드 낼 거니까 심판한테 알려줘!" 라고 넘깁니다.
            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.ConfirmSetCard(myId, this.gameObject, isReveal);
            }
            else
            {
                Debug.LogError("🚨 PlayerUIManager.Instance를 찾을 수 없습니다! 하이어라키에 PlayerUI가 있나요?");
            }
        }
        else
        {
            Debug.LogError("🚨 세트 필드를 찾을 수 없습니다! 하이어라키 창의 오브젝트 이름을 확인해주세요.");
        }
    }
}