// HumanChoiceDialogUI.cs — 사람 플레이어의 카드 선택 / 예·아니오 응답 UI
//
// 담당 이벤트 (사람 요청만 처리. 봇 요청은 BattleManager의 자동 응답기가 담당):
//   - EventManager.OnRequireCardPick       : 제시된 목록에서 N장 선택
//   - EventManager.OnRequireCardChoice     : 특정 존에서 N장 선택 (현재 발행처 없음, 대비용)
//   - EventManager.OnRequireOptionalAction : 예/아니오 (스톱갭 — 사양 미정, 하단 주석 참조)
//
// 화면 구성
//   화면 하단에 카드 세로 높이의 1.4배 높이인 반투명 회색 패널이 올라온다.
//   패널 상단 영역: 선택 가능한 카드들이 가로 스크롤로 나열된다. 카드를 누르면 테두리 색이 바뀌며 선택/해제된다.
//   패널 하단 영역: 안내 문구와 확정 버튼. 필요한 장수를 채우면 확정 버튼이 활성화된다.
//   패널 우측 상단: ▼ 버튼. 누르면 패널이 화면 아래로 내려가고 화면 우측 하단에 ▲ 버튼만 남는다.
//                   ▲ 버튼을 누르면 ▲가 사라지고 패널이 다시 올라온다. (선택 전 보드를 확인하는 용도)
//
// 씬 배선이 필요 없다. RuntimeInitializeOnLoadMethod로 스스로 생성되며 UI도 코드로 만든다.
// 색상·크기는 인스펙터에서 조정할 수 있도록 필드로 노출해 두었다.
using System;
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HumanChoiceDialogUI : MonoBehaviour
{
    public static HumanChoiceDialogUI Instance { get; private set; }

    /// <summary>
    /// 선택 패널이 화면에 떠 있는지. 이 패널은 화면 하단(손패 위)을 덮으므로
    /// CardZoomPopupUI의 손패 호버 미리보기가 이때는 멈춘다.
    /// </summary>
    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    [Header("Layout")]
    [Tooltip("카드 한 장의 표시 크기. CardSlotInGame 프리팹 기본값과 맞춘다.")]
    public Vector2 cardSize = new Vector2(200f, 280f);

    [Tooltip("패널 높이 = 카드 세로 높이 × 이 배수")]
    public float panelHeightMultiplier = 1.4f;

    public float cardSpacing = 16f;
    public float slideDuration = 0.18f;

    [Header("Colors")]
    public Color panelColor = new Color(0.22f, 0.22f, 0.24f, 0.88f);
    public Color cardFrameNormal = new Color(1f, 1f, 1f, 0f);
    public Color cardFrameSelected = new Color(1f, 0.82f, 0.25f, 1f);
    public float cardFrameThickness = 6f;

    // ─── 요청 큐 ────────────────────────────────────────────────────────
    private class Request
    {
        public Player Player;
        public string Message;
        public List<Card> Candidates;       // 카드 선택 요청일 때만
        public int RequiredCount;           // 카드 선택 요청일 때만
        public Action<List<Card>> CardCallback;
        public Action<bool> BoolCallback;   // 예/아니오 요청일 때만
        public bool IsYesNo => BoolCallback != null;
    }

    private readonly Queue<Request> _queue = new Queue<Request>();
    private Request _current;

    // ─── 런타임 UI 참조 ─────────────────────────────────────────────────
    private Canvas _hostCanvas;
    private TMP_FontAsset _font;
    private RectTransform _panel;
    private RectTransform _content;
    private ScrollRect _scroll;
    private Image _askerImage;   // 예/아니오 요청 시 물어본 용병 이미지
    private TextMeshProUGUI _messageText;
    private Button _confirmButton;
    private TextMeshProUGUI _confirmLabel;
    private Button _declineButton;
    private Button _collapseButton;
    private Button _expandButton;

    private readonly List<Card> _selected = new List<Card>();
    private readonly Dictionary<string, Image> _frameByKey = new Dictionary<string, Image>();
    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    private bool _collapsed;
    private Coroutine _slideRoutine;

    private float PanelHeight => cardSize.y * panelHeightMultiplier;
    private float FooterHeight => Mathf.Max(56f, PanelHeight - cardSize.y);

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("HumanChoiceDialogUI");
        Instance = go.AddComponent<HumanChoiceDialogUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnRequireCardPick += HandleRequireCardPick;
        EventManager.OnRequireCardChoice += HandleRequireCardChoice;
        EventManager.OnRequireOptionalAction += HandleRequireOptionalAction;
        EventManager.OnGameSet += HandleGameSet;
    }

    private void OnDisable()
    {
        EventManager.OnRequireCardPick -= HandleRequireCardPick;
        EventManager.OnRequireCardChoice -= HandleRequireCardChoice;
        EventManager.OnRequireOptionalAction -= HandleRequireOptionalAction;
        EventManager.OnGameSet -= HandleGameSet;
    }

    // ─── 이벤트 수신 ────────────────────────────────────────────────────

    private void HandleRequireCardPick(Player player, List<Card> candidates, int count, Action<List<Card>> callback)
    {
        if (!IsHuman(player) || callback == null) return;

        // 고를 것이 없거나 0장을 요구하면 UI를 띄우지 않고 즉시 빈 응답을 돌려준다.
        // (콜백을 호출하지 않으면 엔진이 영원히 대기한다)
        if (candidates == null || candidates.Count == 0 || count <= 0)
        {
            callback.Invoke(new List<Card>());
            return;
        }

        int required = Mathf.Min(count, candidates.Count);

        Enqueue(new Request
        {
            Player = player,
            Message = $"카드를 {required}장 선택하세요.",
            Candidates = new List<Card>(candidates),
            RequiredCount = required,
            CardCallback = callback
        });
    }

    private void HandleRequireCardChoice(Player player, ZoneType zone, int count, string filter, Action<List<Card>> callback)
    {
        if (!IsHuman(player) || callback == null) return;

        // 존에서 후보를 직접 추린다. (이 이벤트는 현재 발행처가 없지만 계약상 존재하므로 대응해 둔다)
        List<Card> candidates = player.GetZone(zone) ?? new List<Card>();

        if (candidates.Count == 0 || count <= 0)
        {
            callback.Invoke(new List<Card>());
            return;
        }

        Enqueue(new Request
        {
            Player = player,
            Message = $"{zone}에서 카드를 {Mathf.Min(count, candidates.Count)}장 선택하세요.",
            Candidates = new List<Card>(candidates),
            RequiredCount = Mathf.Min(count, candidates.Count),
            CardCallback = callback
        });
    }

    // ※ 스톱갭: 예/아니오 UI는 별도 사양이 정해지지 않았다.
    //   그러나 QA 자동 응답기를 봇 전용으로 제한한 이상, 이 이벤트에 응답할 주체가 없으면
    //   용병 능력(다이나·엘리·소니아·베로니카)과 OptionalActionEffect에서 게임이 영구 정지한다.
    //   그래서 같은 패널을 카드 없이 재사용해 [예]/[아니오] 두 버튼만 띄운다. 디자인 확정 시 교체할 것.
    private void HandleRequireOptionalAction(Player player, string message, GameContext ctx, Action<bool> callback)
    {
        if (!IsHuman(player) || callback == null) return;

        Enqueue(new Request
        {
            Player = player,
            Message = string.IsNullOrEmpty(message) ? "실행하시겠습니까?" : message,
            BoolCallback = callback
        });
    }

    private void HandleGameSet(Player winner)
    {
        // 게임이 끝나면 남은 요청을 정리한다 (응답을 기다리는 쪽이 이미 사라졌을 수 있다)
        _queue.Clear();
        _current = null;
        HidePanelImmediate();
    }

    private static bool IsHuman(Player p) => p != null && p.Type == UserType.Human;

    private void Enqueue(Request request)
    {
        _queue.Enqueue(request);
        if (_current == null) ShowNext();
    }

    private void ShowNext()
    {
        // 콜백이 동기적으로 다음 요청을 띄운 경우(용병 능력: 발동 여부 → 카드 선택),
        // 뒤늦게 돌아온 ShowNext가 방금 띄운 창을 덮어쓰지 않도록 막는다.
        if (_current != null) return;

        if (_queue.Count == 0)
        {
            _current = null;
            HidePanel();
            return;
        }

        _current = _queue.Dequeue();
        EnsureUI();

        if (_panel == null)
        {
            // UI를 만들지 못했다면(캔버스 없음) 게임이 멈추지 않도록 안전한 기본값으로 응답한다
            Debug.LogError("[HumanChoiceDialogUI] UI 생성 실패 — 기본값으로 자동 응답합니다.");
            Request aborted = _current;
            _current = null;

            if (aborted.IsYesNo) aborted.BoolCallback?.Invoke(false);
            else aborted.CardCallback?.Invoke(new List<Card>());

            ShowNext();
            return;
        }

        BuildForCurrentRequest();
        SetCollapsed(false, instant: true);
        ShowPanel();
    }

    // ─── 요청별 화면 구성 ───────────────────────────────────────────────

    private void BuildForCurrentRequest()
    {
        ClearItems();
        _selected.Clear();

        _messageText.text = _current.Message;

        if (_current.IsYesNo)
        {
            _scroll.gameObject.SetActive(false);
            _declineButton.gameObject.SetActive(true);
            _confirmLabel.text = "예";
            SetButtonInteractable(_confirmButton, true);
            ShowAskingCharacter(_current.Player, _current.Message);
            return;
        }

        HideAskingCharacter();
        _scroll.gameObject.SetActive(true);
        _declineButton.gameObject.SetActive(false);

        for (int i = 0; i < _current.Candidates.Count; i++)
            CreateCardItem(_current.Candidates[i], i);

        RefreshConfirmState();
    }

    // ─── 예/아니오 요청에 물어본 용병 이미지 표시 ───────────────────────
    //
    // OnRequireOptionalAction에는 "어느 용병이 묻는지"가 파라미터로 오지 않는다(메시지 문자열뿐).
    // 이벤트 시그니처는 UI팀과의 계약이라 함부로 바꾸지 않는다(원칙 7).
    // 대신 요청자의 용병 2종 이름을 데이터에서 가져와 메시지와 대조한다.
    // 용병 능력 메시지는 모두 "엘리 능력을...", "다이나 능력을..." 처럼 이름으로 시작한다.
    // (카드 효과의 OptionalActionEffect 메시지는 어느 이름과도 안 맞아 자연히 이미지가 숨겨진다)
    private void ShowAskingCharacter(Player asker, string message)
    {
        Card character = ResolveAskingCharacter(asker, message);

        if (character == null || !CardImageLoader.ApplyToImage(_askerImage, character.ImagePath))
        {
            HideAskingCharacter();
            return;
        }

        _askerImage.preserveAspect = true;
        _askerImage.gameObject.SetActive(true);
    }

    private void HideAskingCharacter()
    {
        if (_askerImage != null) _askerImage.gameObject.SetActive(false);
    }

    private static Card ResolveAskingCharacter(Player asker, string message)
    {
        if (asker == null || string.IsNullOrEmpty(message)) return null;

        var dm = BattleManager.Instance != null ? BattleManager.Instance.CardData : null;
        if (dm == null) return null;

        foreach (string id in new[] { asker.CharacterCardId, asker.SecondaryCharacterId })
        {
            if (string.IsNullOrEmpty(id)) continue;

            Card view = dm.GetCharacterCardView(id);
            if (view == null || string.IsNullOrEmpty(view.Name)) continue;

            if (message.Contains(view.Name)) return view;
        }

        return null;
    }

    private void CreateCardItem(Card card, int index)
    {
        if (card == null) return;

        // 테두리 프레임(선택 표시) → 그 안에 실제 카드 프리팹.
        // 프레임 바깥 크기를 cardSize로 고정하고 카드를 두께만큼 안쪽으로 넣는다.
        // (프레임을 카드보다 크게 만들면 스크롤 뷰포트 높이를 넘어 잘린다)
        var frameGo = new GameObject($"Item_{index}_{card.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
        var frameRect = (RectTransform)frameGo.transform;
        frameRect.SetParent(_content, false);
        frameRect.sizeDelta = cardSize;

        var frameImage = frameGo.GetComponent<Image>();
        frameImage.color = cardFrameNormal;

        var layout = frameGo.AddComponent<LayoutElement>();
        layout.preferredWidth = cardSize.x;
        layout.preferredHeight = cardSize.y;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        Vector2 innerSize = cardSize - Vector2.one * (cardFrameThickness * 2f);

        GameObject cardGo = InstantiateCardVisual(card, frameRect);
        if (cardGo != null)
        {
            var cardRect = cardGo.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                // 프리팹 원본 피벗이 (0.5, 0)이라 그대로 두면 프레임 위쪽으로 삐져나온다. 중앙으로 정규화한다.
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = innerSize;
                cardRect.localScale = Vector3.one;
            }

            // 카드 내부 그래픽이 클릭을 가로채지 않도록 막는다 (프레임의 Button이 받아야 한다)
            foreach (var graphic in cardGo.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        string key = CardKey(card, index);
        _frameByKey[key] = frameImage;
        _spawnedItems.Add(frameGo);

        Card captured = card;
        string capturedKey = key;
        frameGo.GetComponent<Button>().onClick.AddListener(() => ToggleSelection(captured, capturedKey));
    }

    private GameObject InstantiateCardVisual(Card card, Transform parent)
    {
        GameObject prefab = ResolveCardPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[HumanChoiceDialogUI] 카드 프리팹을 찾지 못했습니다. 이름 텍스트로 대체합니다.");
            var fallback = CreateText(parent, card.Name, 22, TextAlignmentOptions.Center);
            var rect = fallback.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return fallback.gameObject;
        }

        GameObject go = Instantiate(prefab, parent);
        var ui = go.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.BindEngineCard(card);
            ui.SetFaceDown(false); // 선택 대상은 항상 앞면으로 보여준다
        }

        // 보드용 컴포넌트가 붙어 있으면 다이얼로그 안에서는 꺼 둔다 (드래그·드롭 방지)
        var interaction = go.GetComponent<CardInteraction>();
        if (interaction != null) interaction.enabled = false;

        return go;
    }

    private GameObject ResolveCardPrefab()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myCardPrefab != null)
            return PlayerUIManager.Instance.myCardPrefab;

        return Resources.Load<GameObject>("Build/CardSlotInGame");
    }

    private static string CardKey(Card card, int index)
        => string.IsNullOrEmpty(card.InstanceId) ? $"{card.Id}#{index}" : card.InstanceId;

    private void ToggleSelection(Card card, string key)
    {
        if (_current == null || _current.IsYesNo) return;

        if (_selected.Contains(card))
        {
            _selected.Remove(card);
        }
        else
        {
            // 필요한 장수를 이미 채웠다면 가장 먼저 고른 카드를 밀어낸다 (다시 누르지 않아도 되도록)
            if (_selected.Count >= _current.RequiredCount && _current.RequiredCount > 0)
            {
                Card dropped = _selected[0];
                _selected.RemoveAt(0);
                ApplyFrameColor(dropped, false);
            }
            _selected.Add(card);
        }

        if (_frameByKey.TryGetValue(key, out Image frame) && frame != null)
            frame.color = _selected.Contains(card) ? cardFrameSelected : cardFrameNormal;

        RefreshConfirmState();
    }

    /// <summary>후보 목록에서 해당 카드의 인덱스를 찾아 테두리 색을 갱신한다.</summary>
    private void ApplyFrameColor(Card card, bool selected)
    {
        if (_current?.Candidates == null) return;

        for (int i = 0; i < _current.Candidates.Count; i++)
        {
            if (!ReferenceEquals(_current.Candidates[i], card)) continue;

            if (_frameByKey.TryGetValue(CardKey(card, i), out Image frame) && frame != null)
                frame.color = selected ? cardFrameSelected : cardFrameNormal;
            return;
        }
    }

    private void RefreshConfirmState()
    {
        if (_current == null || _current.IsYesNo) return;

        int need = _current.RequiredCount;
        bool ready = _selected.Count == need;

        _confirmLabel.text = ready ? "확정" : $"확정 ({_selected.Count}/{need})";
        SetButtonInteractable(_confirmButton, ready);
    }

    private void SetButtonInteractable(Button button, bool value)
    {
        if (button == null) return;
        button.interactable = value;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            Color c = image.color;
            c.a = value ? 1f : 0.45f;
            image.color = c;
        }
    }

    // ─── 확정 / 취소 ────────────────────────────────────────────────────

    private void OnConfirmClicked()
    {
        if (_current == null) return;

        Request finished = _current;
        _current = null;

        // 콜백이 동기적으로 다음 요청을 띄울 수 있으므로, 콜백 호출 전에 상태를 모두 정리한다
        var answer = new List<Card>(_selected);
        _selected.Clear();
        ClearItems();
        HidePanel();

        if (finished.IsYesNo)
            finished.BoolCallback?.Invoke(true);
        else
            finished.CardCallback?.Invoke(answer);

        ShowNext();
    }

    private void OnDeclineClicked()
    {
        if (_current == null || !_current.IsYesNo) return;

        Request finished = _current;
        _current = null;

        _selected.Clear();
        ClearItems();
        HidePanel();

        finished.BoolCallback?.Invoke(false);
        ShowNext();
    }

    // ─── 접기 / 펼치기 ──────────────────────────────────────────────────

    private void SetCollapsed(bool collapsed, bool instant = false)
    {
        _collapsed = collapsed;

        if (_expandButton != null)
            _expandButton.gameObject.SetActive(collapsed);

        float targetY = collapsed ? -PanelHeight : 0f;

        if (_slideRoutine != null) StopCoroutine(_slideRoutine);

        if (instant || !gameObject.activeInHierarchy)
        {
            _panel.anchoredPosition = new Vector2(0f, targetY);
            return;
        }

        _slideRoutine = StartCoroutine(SlidePanelTo(targetY));
    }

    private IEnumerator SlidePanelTo(float targetY)
    {
        float startY = _panel.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            _panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, targetY, t));
            yield return null;
        }

        _panel.anchoredPosition = new Vector2(0f, targetY);
        _slideRoutine = null;
    }

    // ─── 표시 상태 ──────────────────────────────────────────────────────

    private void ShowPanel()
    {
        if (_panel != null) _panel.gameObject.SetActive(true);
    }

    private void HidePanel()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
        if (_expandButton != null) _expandButton.gameObject.SetActive(false);
    }

    private void HidePanelImmediate()
    {
        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }
        ClearItems();
        _selected.Clear();
        HidePanel();
    }

    private void ClearItems()
    {
        foreach (var go in _spawnedItems)
            if (go != null) Destroy(go);

        _spawnedItems.Clear();
        _frameByKey.Clear();
    }

    // ─── UI 생성 (코드로 직접 구축) ─────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogError("[HumanChoiceDialogUI] Canvas를 찾지 못해 선택 UI를 만들 수 없습니다.");
            return;
        }

        if (_panel != null && _hostCanvas == canvas) return;

        // 씬이 바뀌어 캔버스가 교체되면 다시 만든다
        if (_panel != null) Destroy(_panel.gameObject);
        if (_expandButton != null) Destroy(_expandButton.gameObject);

        _hostCanvas = canvas;
        BuildPanel(canvas);
    }

    private Canvas ResolveCanvas()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myHandTransform != null)
        {
            var fromHand = PlayerUIManager.Instance.myHandTransform.GetComponentInParent<Canvas>();
            if (fromHand != null) return fromHand.rootCanvas != null ? fromHand.rootCanvas : fromHand;
        }

        return FindFirstObjectByType<Canvas>();
    }

    private void BuildPanel(Canvas canvas)
    {
        float panelH = PanelHeight;
        float footerH = FooterHeight;

        // 텍스트를 만들기 전에 폰트를 먼저 정한다 (기본 폰트로 만들면 한글이 ㅁ로 깨진다)
        _font = UiFontResolver.Resolve();

        // ── 루트 패널: 하단 가로 스트레치 ──
        var panelGo = new GameObject("CardChoicePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _panel = (RectTransform)panelGo.transform;
        _panel.SetParent(canvas.transform, false);
        _panel.anchorMin = new Vector2(0f, 0f);
        _panel.anchorMax = new Vector2(1f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.sizeDelta = new Vector2(0f, panelH);
        _panel.anchoredPosition = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = true; // 패널 뒤 보드 클릭 차단

        // 보드 위에 확실히 그려지도록 별도 정렬 순서를 준다
        var panelCanvas = panelGo.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 500;
        panelGo.AddComponent<GraphicRaycaster>();

        // ── 카드 스크롤 영역 ──
        var scrollGo = new GameObject("CardScroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.SetParent(_panel, false);
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(12f, footerH);
        scrollRect.offsetMax = new Vector2(-12f, 0f);

        _scroll = scrollGo.GetComponent<ScrollRect>();
        _scroll.horizontal = true;
        _scroll.vertical = false;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.scrollSensitivity = 25f;

        // ★ Mask(스텐실)가 아니라 RectMask2D를 쓴다.
        //   Mask는 그래픽의 '알파'를 잘라내기 모양으로 사용하므로, 투명한 Image를 마스크로 두면
        //   자식(카드)이 통째로 잘려서 아무것도 보이지 않는다.
        //   RectMask2D는 사각 영역만으로 자르므로 그래픽 알파와 무관하다.
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var viewportRect = (RectTransform)viewportGo.transform;
        viewportRect.SetParent(scrollRect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // 드래그 스크롤을 받으려면 레이캐스트 대상이 필요하다. 보이지는 않게 거의 투명하게 둔다.
        var viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewportImage.raycastTarget = true;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        _content = (RectTransform)contentGo.transform;
        _content.SetParent(viewportRect, false);
        _content.anchorMin = new Vector2(0f, 0.5f);
        _content.anchorMax = new Vector2(0f, 0.5f);
        _content.pivot = new Vector2(0f, 0.5f);
        _content.anchoredPosition = Vector2.zero;

        var hlg = contentGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = cardSpacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(8, 8, 0, 0);

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll.viewport = viewportRect;
        _scroll.content = _content;

        // ── 예/아니오 요청 시 표시할 용병 이미지 (카드 영역 자리에 중앙 배치) ──
        var askerGo = new GameObject("AskingCharacter", typeof(RectTransform), typeof(Image));
        var askerRect = (RectTransform)askerGo.transform;
        askerRect.SetParent(_panel, false);
        askerRect.anchorMin = new Vector2(0.5f, 0f);
        askerRect.anchorMax = new Vector2(0.5f, 0f);
        askerRect.pivot = new Vector2(0.5f, 0f);
        askerRect.sizeDelta = new Vector2(cardSize.x, cardSize.y);
        askerRect.anchoredPosition = new Vector2(0f, footerH);
        _askerImage = askerGo.GetComponent<Image>();
        _askerImage.preserveAspect = true;
        _askerImage.raycastTarget = false;
        askerGo.SetActive(false);

        // ── 하단 안내 문구 ──
        _messageText = CreateText(_panel, "", 24, TextAlignmentOptions.MidlineLeft);
        var msgRect = _messageText.rectTransform;
        msgRect.anchorMin = new Vector2(0f, 0f);
        msgRect.anchorMax = new Vector2(0.6f, 0f);
        msgRect.pivot = new Vector2(0f, 0f);
        msgRect.offsetMin = new Vector2(20f, 8f);
        msgRect.offsetMax = new Vector2(0f, footerH - 8f);

        // ── 확정 버튼 (패널 하단) ──
        _confirmButton = CreateButton(_panel, "ConfirmButton", "확정", new Color(0.20f, 0.55f, 0.95f, 1f), out _confirmLabel);
        var confirmRect = _confirmButton.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(1f, 0f);
        confirmRect.anchorMax = new Vector2(1f, 0f);
        confirmRect.pivot = new Vector2(1f, 0f);
        confirmRect.sizeDelta = new Vector2(180f, footerH - 16f);
        confirmRect.anchoredPosition = new Vector2(-20f, 8f);
        _confirmButton.onClick.AddListener(OnConfirmClicked);

        // ── 아니오 버튼 (예/아니오 요청에서만 표시) ──
        TextMeshProUGUI declineLabel;
        _declineButton = CreateButton(_panel, "DeclineButton", "아니오", new Color(0.45f, 0.45f, 0.48f, 1f), out declineLabel);
        var declineRect = _declineButton.GetComponent<RectTransform>();
        declineRect.anchorMin = new Vector2(1f, 0f);
        declineRect.anchorMax = new Vector2(1f, 0f);
        declineRect.pivot = new Vector2(1f, 0f);
        declineRect.sizeDelta = new Vector2(180f, footerH - 16f);
        declineRect.anchoredPosition = new Vector2(-212f, 8f);
        _declineButton.onClick.AddListener(OnDeclineClicked);
        _declineButton.gameObject.SetActive(false);

        // ── 접기 버튼 ▼ (패널 우측 상단) ──
        _collapseButton = CreateArrowButton(_panel, "CollapseButton", pointDown: true, new Color(0f, 0f, 0f, 0.55f));
        var collapseRect = _collapseButton.GetComponent<RectTransform>();
        collapseRect.anchorMin = new Vector2(1f, 1f);
        collapseRect.anchorMax = new Vector2(1f, 1f);
        collapseRect.pivot = new Vector2(1f, 1f);
        collapseRect.sizeDelta = new Vector2(64f, 48f);
        collapseRect.anchoredPosition = new Vector2(-12f, -12f);
        _collapseButton.onClick.AddListener(() => SetCollapsed(true));

        // ── 펼치기 버튼 ▲ (화면 우측 하단. 접기 버튼과 가로 위치를 맞추고 높이만 다르다) ──
        var expandHost = new GameObject("ExpandButtonRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var expandHostRect = (RectTransform)expandHost.transform;
        expandHostRect.SetParent(canvas.transform, false);
        expandHostRect.anchorMin = Vector2.zero;
        expandHostRect.anchorMax = Vector2.one;
        expandHostRect.offsetMin = Vector2.zero;
        expandHostRect.offsetMax = Vector2.zero;
        var expandCanvas = expandHost.GetComponent<Canvas>();
        expandCanvas.overrideSorting = true;
        expandCanvas.sortingOrder = 501;

        _expandButton = CreateArrowButton(expandHostRect, "ExpandButton", pointDown: false, new Color(0f, 0f, 0f, 0.55f));
        var expandRect = _expandButton.GetComponent<RectTransform>();
        expandRect.anchorMin = new Vector2(1f, 0f);
        expandRect.anchorMax = new Vector2(1f, 0f);
        expandRect.pivot = new Vector2(1f, 0f);
        expandRect.sizeDelta = new Vector2(64f, 48f);
        expandRect.anchoredPosition = new Vector2(-12f, 12f); // 접기 버튼과 동일한 우측 여백(-12)
        _expandButton.onClick.AddListener(() => SetCollapsed(false));
        _expandButton.gameObject.SetActive(false);

        _panel.gameObject.SetActive(false);
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        labelText = CreateText(go.transform, label, 26, TextAlignmentOptions.Center);
        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    /// <summary>
    /// 삼각형 아이콘 버튼. ▼/▲ 문자는 프로젝트 한글 폰트(정적 아틀라스)에 글리프가 없어 ㅁ로 깨지므로,
    /// 문자 대신 코드로 만든 삼각형 스프라이트를 쓴다. pointDown=false면 180° 돌려 ▲로 쓴다.
    /// </summary>
    private Button CreateArrowButton(Transform parent, string name, bool pointDown, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);

        var iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(24f, 16f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.localRotation = Quaternion.Euler(0f, 0f, pointDown ? 0f : 180f);

        var iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = GetTriangleSprite();
        iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        return go.GetComponent<Button>();
    }

    private static Sprite _triangleSprite;

    /// <summary>아래를 가리키는 삼각형(▼) 스프라이트를 코드로 생성한다.</summary>
    private static Sprite GetTriangleSprite()
    {
        if (_triangleSprite != null) return _triangleSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color opaque = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);
        float centerX = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            // 텍스처는 y=0이 아래쪽. 아래로 갈수록 폭이 좁아져야 아래를 가리키는 삼각형이 된다.
            float halfWidth = y * 0.5f;
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Mathf.Abs(x - centerX) <= halfWidth ? opaque : clear);
        }

        tex.Apply();
        _triangleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return _triangleSprite;
    }
}
