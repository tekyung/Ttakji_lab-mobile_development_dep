using System;
using UnityEngine;
using UnityEngine.EventSystems; // 마우스/터치 이벤트를 처리하기 위해 꼭 필요합니다!

// IPointerDownHandler(누를 때), IPointerUpHandler(뗄 때), IPointerExitHandler(영역을 벗어날 때) 인터페이스를 상속받습니다.
public class CardInteraction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    /// <summary>선택(확대·액션 버튼 표시) 상태인지. CardZoomPopupUI가 두 번째 클릭을 구분하는 데 쓴다.</summary>
    public bool IsSelected => isSelected;

    private static CardInteraction _currentDragging;

    /// <summary>
    /// 지금 드래그 중인 손패 카드. 없으면 null.
    /// ⚠️ 단순 static bool로 두면 안 된다 — 드래그 도중 플레이를 멈추거나 카드 GO가 풀로 비활성화되면
    /// 플래그가 true로 굳어 호버 미리보기가 영영 죽는다(에디터에서 도메인 리로드를 끄면 세션을 넘어 살아남는다).
    /// 그래서 참조를 들고 있다가 읽을 때마다 실제 드래그 중인지 확인해 스스로 정리한다.
    /// </summary>
    public static CardInteraction CurrentDragging
    {
        get
        {
            if (_currentDragging != null && !_currentDragging.isDragging) _currentDragging = null;
            return _currentDragging;
        }
    }

    /// <summary>드래그 중에는 CardZoomPopupUI의 호버 미리보기가 대상을 바꾸지 않고 집은 카드를 계속 보여 준다.</summary>
    public static bool IsDraggingAny => CurrentDragging != null;
    private static CardInteraction currentlySelectedCard; // 현재 카드 기억하기

    [Header("Recall Settings")]
    [Tooltip("세트한 카드를 손패로 되돌리는 [회수] 버튼. 비우면 OnClickReturn이 걸린 버튼을 자식에서 찾는다.")]
    public GameObject recallButton;

    private bool _isInSetZone;
    private bool _recallAvailable;

    /// <summary>
    /// 이 카드가 내 세트존에 있는가.
    ///
    /// ★ 값이 바뀌면 [회수]는 무조건 닫는다.
    ///   존이 바뀌었다는 것은 엔진이 이미 카드를 옮겼다는 뜻이고, 그 시점의 되돌리기는
    ///   엔진 상태와 어긋난다. 되돌릴 수 있는 시점은 <b>[레디]를 누르기 전</b>뿐이며,
    ///   그건 <see cref="SetRecallAvailable"/>로 PlayerUIManager가 직접 알려 준다.
    ///
    ///   필드가 아니라 프로퍼티로 둔 것은, 이 값을 넣는 곳이 여덟 군데라
    ///   한 곳이라도 빠뜨리면 회수 버튼이 엉뚱한 카드에 남기 때문이다.
    /// </summary>
    public bool isInSetZone
    {
        get => _isInSetZone;
        set
        {
            _isInSetZone = value;
            SetRecallAvailable(false);
        }
    }

    /// <summary>
    /// [회수] 버튼을 보여 줄지 정한다. PlayerUIManager가 세트 선택의 시작·끝에서 부른다.
    /// </summary>
    public void SetRecallAvailable(bool available)
    {
        _recallAvailable = available;
        RefreshRecallButton();
    }

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

        // 세트 선택 시 카드 본체는 흐려지지만(PlayerUIManager가 루트 CanvasGroup.alpha를 낮춤)
        // 그 위에 뜨는 [공개]/[폐기] 버튼까지 같이 흐려지면 안 된다.
        // 버튼 패널에 자체 CanvasGroup을 두고 부모의 alpha를 무시하게 만든다.
        DetachFromParentAlpha(actionButtonPanel);
        DetachFromParentAlpha(actionButtonPanelBottom);

        ResolveRecallButton();

        // 세트 선택 중에는 카드 본체가 흐려진다(PlayerUIManager가 루트 CanvasGroup.alpha를 낮춤).
        // [회수]까지 같이 흐려지면 안 되므로 [공개]/[폐기]와 같은 처리를 해 둔다.
        DetachFromParentAlpha(recallButton);

        RefreshRecallButton();   // 기본은 숨김. 되돌릴 수 있는 동안에만 켠다.
    }

    /// <summary>
    /// [회수] 버튼을 자식에서 찾는다. 인스펙터에 연결돼 있으면 그것을 쓴다.
    ///
    /// 이름이 아니라 <b>인스펙터에 걸린 onClick 대상</b>으로 찾는다.
    /// 이름은 바뀌기 쉽지만 "OnClickReturn을 부르는 버튼"이라는 사실은 바뀌지 않는다.
    /// </summary>
    private void ResolveRecallButton()
    {
        if (recallButton != null) return;

        foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (button.onClick.GetPersistentMethodName(i) != nameof(OnClickReturn)) continue;

                recallButton = button.gameObject;
                return;
            }
        }
    }

    /// <summary>되돌릴 수 있는 동안에만 [회수]를 보여 준다.</summary>
    private void RefreshRecallButton()
    {
        if (recallButton == null) return;
        if (recallButton.activeSelf != _recallAvailable) recallButton.SetActive(_recallAvailable);
    }

    /// <summary>부모 CanvasGroup의 alpha 영향을 받지 않도록 자체 CanvasGroup을 붙인다.</summary>
    private static void DetachFromParentAlpha(GameObject panel)
    {
        if (panel == null) return;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();

        group.alpha = 1f;
        group.ignoreParentGroups = true;
    }

    void OnDisable()
    {
        // 드래그 도중 카드 GO가 풀로 비활성화되면 OnEndDrag가 오지 않는다.
        // 상태가 고착되면 호버 미리보기가 영영 멈추므로 여기서 푼다.
        if (isDragging)
        {
            isDragging = false;
            if (_currentDragging == this) _currentDragging = null;
        }
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

    // 커서를 올리기만 해도 왼쪽 서브 팝업에 카드를 크게 보여 준다 (클릭 불필요)
    //
    // ★ 미리보기를 띄우는 경로는 둘이다: 이 포인터 이벤트와 CardZoomPopupUI의 Rect 호버 판정.
    //   이 보드에서는 포인터 이동 이벤트가 카드까지 오지 않는 경우가 있어(중첩 Canvas +
    //   존별 레이캐스트 토글) 어느 한쪽만으로는 호버가 죽는다. 둘 다 두고, 표시는 멱등하게 만든다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isInSetZone || isPlayed) return;

        ShowSubPopup();
    }

    // 누른 상태로 카드 밖으로 마우스가 빠져나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        ResetPress(); // 의도치 않은 확대 방지

        // 드래그를 시작하면 blocksRaycasts가 꺼져 Exit가 오지만, 그때는 계속 보여 줘야 한다
        if (isDragging || IsDraggingAny) return;

        CloseSubPopup();
    }

    /// <summary>이 카드를 왼쪽 서브 팝업(읽기 전용 미리보기)에 띄운다.</summary>
    private void ShowSubPopup()
    {
        if (CardZoomPopupUI.Instance == null) return;

        CardUI ui = GetComponent<CardUI>();
        if (ui != null) CardZoomPopupUI.Instance.ShowSubByInstanceId(ui.myInstanceId);
    }

    /// <summary>커서가 이 카드를 벗어났음을 알린다. 실제로 닫을지는 CardZoomPopupUI가 판단한다.</summary>
    private void CloseSubPopup()
    {
        if (CardZoomPopupUI.Instance == null) return;

        CardUI ui = GetComponent<CardUI>();
        if (ui != null) CardZoomPopupUI.Instance.CloseSubOnPointerExit(ui.myInstanceId);
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

        // 클릭으로도 미리보기가 뜬다 (기존 동작 유지).
        // 마우스 다운 시점에 CardZoomPopupUI가 서브 팝업을 닫을 수 있으므로 클릭 처리 끝에서 다시 띄운다
        ShowSubPopup();
    }

    /// <summary>
    /// 손패에서 튀어나와 있는 카드를 집어넣는다.
    /// 중앙 팝업처럼 화면을 덮는 것을 열기 전에 부른다 — 안 그러면 그 카드가 위를 덮는다.
    /// </summary>
    public static void ClearHandSelection()
    {
        if (currentlySelectedCard != null) currentlySelectedCard.DeselectCard();
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
    /// <summary>
    /// 세트 선택을 되돌린다.
    ///
    /// ★ 지금 룰 흐름에서 되돌릴 수 있는 시점은 <b>[레디]를 누르기 전</b>뿐이다.
    ///   [공개]/[폐기]를 골라도 카드는 아직 손패에 흐리게 남아 있고,
    ///   세트존에는 잔상만 떠 있다. 실제 이동은 양쪽이 준비된 뒤 엔진이 한다.
    ///   [레디] 이후에 되돌리면 이미 보낸 선택과 어긋난다.
    ///
    ///   예전 코드는 카드를 '세트 필드'에서 손패로 <b>옮겨</b> 왔는데,
    ///   지금은 애초에 옮겨 가지 않으므로 그 일이 전부 헛일이었다.
    ///   되돌리기는 PlayerUIManager가 쥐고 있는 보류 선택을 지우는 것으로 끝난다.
    /// </summary>
    public void OnClickReturn()
    {

        SetRecallAvailable(false);
        DeselectCard();

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.CancelSet();
        }
        else
        {
            Debug.LogWarning("[CardInteraction] PlayerUIManager를 찾지 못해 세트 선택을 되돌리지 못했습니다.");
        }

        MyHandManager handManager = FindAnyObjectByType<MyHandManager>();
        if (handManager != null) handManager.SetReadyButtonState(false);
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
        _currentDragging = this;
        isPointerDown = false; // 드래그를 시작하면 줌 기능 취소

        // 카드가 손패에서 들리는 순간, 화면 왼쪽 서브 팝업에 큰 이미지를 띄운다.
        // 중앙 모달과 달리 보드를 가리지 않으므로 드래그 목표 지점이 계속 보인다.
        ShowSubPopup();

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
        if (_currentDragging == this) _currentDragging = null;
        canvasGroup.blocksRaycasts = true;

        if (transform.position.y > startDragPosition.y + dragUpThreshold)
        {
            ShowZoomPanel();
        }

        // 서브 팝업 정리는 CardZoomPopupUI의 호버 판정이 맡는다.
        // (커서가 손패를 벗어나 있으면 닫히고, 아직 카드 위면 그대로 유지된다)
        ReturnToHand();
    }

    private void ShowZoomPanel()
    {

        // ⭐ InGameUIManager에게 '나 자신(this)'을 넘겨주며 줌 패널을 띄워달라고 요청합니다.
        // 세트 선택용 중앙 팝업(공개/폐기 버튼 포함)을 연다.
        // 읽기용 서브 팝업과 겹치지 않도록 먼저 닫는다.
        if (CardZoomPopupUI.Instance != null) CardZoomPopupUI.Instance.CloseSub();

        // ★ 손패에서 튀어나와 있는 카드도 집어넣는다.
        //   SelectCard가 그 카드에 overrideSorting(order 10)을 걸어 두기 때문에,
        //   그대로 두면 <b>중앙 팝업보다 앞에 그려져 팝업을 가린다.</b>
        ClearHandSelection();

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
        SendToSetField(true);
    }

    // '폐기' 버튼을 눌렀을 때 실행됩니다.
    public void OnClickDiscard()
    {
        SendToSetField(false);
    }

    // 카드를 찾아내서 세트 필드로 쏘아 보내는 공통 함수
    private void SendToSetField(bool isReveal)
    {
        // 씬(Scene)에서 '세트 필드' 역할을 하는 오브젝트를 이름으로 찾습니다.
        GameObject setField = GameObject.Find("set");

        if (setField != null)
        {
            if (PlayerUIManager.Instance != null && !PlayerUIManager.Instance.CanPlaceCardInSetZone())
            {
                Debug.LogWarning("⚠️ 이미 세트 존에 카드가 있습니다! 더 이상 놓을 수 없습니다.");
                DeselectCard();
                return;
            }

            DeselectCard();
            CardUI cardUI = GetComponent<CardUI>();
            string instanceId = cardUI != null ? cardUI.myInstanceId : null;

            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.ConfirmSetCard(instanceId, this.gameObject, isReveal);
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