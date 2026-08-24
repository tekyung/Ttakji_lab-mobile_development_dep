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
// 배치는 프리팹이 소유한다 — Resources/Build/HumanChoiceDialog
//   프리팹 소유 : 위치·크기·앵커·계층·폰트·기본 색
//   코드   소유 : 텍스트 내용·활성 여부·상태 색·목록 개수·애니메이션
//
// 로직 싱글턴(이 스크립트)은 RuntimeInitializeOnLoadMethod로 스스로 만들어져 씬을 넘어 살아남고,
// 화면(프리팹)은 씬 캔버스 아래에 찍는다. 씬이 바뀌면 화면만 다시 찍는다.
//
// ⚠️ 이 UI는 임계 경로다. 엔진이 WaitUntil로 응답을 기다리므로 화면이 안 뜨면 게임이 멈춘다.
//    그래서 프리팹을 못 찾거나 참조가 비면 **기본값으로 자동 응답**해 게임은 굴러가게 한다.
//    (코드로 UI를 다시 짓는 폴백은 두지 않는다 — 경로가 둘이면 한쪽이 조용히 썩는다)
using System;
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HumanChoiceDialogUI : MonoBehaviour
{
    public static HumanChoiceDialogUI Instance { get; private set; }

    /// <summary>
    /// 선택 패널이 화면에 떠 있는지. 이 패널은 화면 하단(손패 위)을 덮으므로
    /// CardZoomPopupUI의 손패 호버 미리보기가 이때는 멈춘다.
    /// </summary>
    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    // ─────────────────────────────────────────────────────────────
    // 패널의 크기·색·간격은 이제 **프리팹이 소유**한다.
    // 여기 남은 것은 카드 항목(다음 라운드에서 프리팹으로 옮긴다)과 애니메이션뿐이다.
    // ─────────────────────────────────────────────────────────────

    [Header("애니메이션")]
    [Tooltip("패널이 오르내리는 데 걸리는 시간(초).")]
    public float slideDuration = 0.18f;

    [Header("선택 상태 색")]
    // 칸 크기·테두리 두께·배지 위치는 ChoiceCardItem 프리팹이 소유한다.
    // 여기 남은 것은 **상태**에 따라 코드가 바꾸는 색뿐이다.
    [Tooltip("선택되지 않은 카드의 테두리.")]
    public Color cardFrameNormal = new Color(1f, 1f, 1f, 0f);

    [Tooltip("선택된 카드의 테두리 · 순서 배지 바탕.")]
    public Color cardFrameSelected = new Color(1f, 0.82f, 0.25f, 1f);

    // ─── 요청 큐 ────────────────────────────────────────────────────────
    private class Request
    {
        public Player Player;
        public string Message;
        public List<Card> Candidates;       // 카드 선택 요청일 때만
        public int RequiredCount;           // 카드 선택 요청일 때만
        public bool Ordered;                // true면 고른 순서대로 1·2·… 번호를 보여 준다
        public Action<List<Card>> CardCallback;
        public Action<bool> BoolCallback;   // 예/아니오 요청일 때만
        public bool IsYesNo => BoolCallback != null;
    }

    private readonly Queue<Request> _queue = new Queue<Request>();
    private Request _current;

    // ─── 런타임 UI 참조 ─────────────────────────────────────────────────
    //
    // 개별 참조는 프리팹에 붙은 HumanChoiceDialogView가 들고 있다.
    // 여기서는 편의를 위한 지름길만 둔다.

    /// <summary>Resources 아래에서 다이얼로그 프리팹을 찾을 경로.</summary>
    private const string DialogResourcePath = "Build/HumanChoiceDialog";

    private Canvas _hostCanvas;
    private TMP_FontAsset _font;
    private GameObject _dialogRoot;
    private HumanChoiceDialogView _view;

    /// <summary>
    /// 이 화면을 우리가 찍었는가.
    /// 씬에 미리 놓인 것을 빌려 쓴 경우에는 <b>절대 파괴하면 안 된다</b> — 기획자의 오브젝트다.
    /// </summary>
    private bool _ownsDialogRoot;

    /// <summary>패널이 프리팹에서 놓여 있던 자리. 슬라이드의 기준점이다.</summary>
    private Vector2 _panelShownPosition;

    private RectTransform _panel => _view != null ? _view.panel : null;
    private RectTransform _content => _view != null ? _view.content : null;
    private ScrollRect _scroll => _view != null ? _view.scroll : null;
    private Image _askerImage => _view != null ? _view.askerImage : null;
    private TextMeshProUGUI _messageText => _view != null ? _view.messageText : null;
    private Button _confirmButton => _view != null ? _view.confirmButton : null;
    private TextMeshProUGUI _confirmLabel => _view != null ? _view.confirmLabel : null;
    private Button _declineButton => _view != null ? _view.declineButton : null;
    private Button _expandButton => _view != null ? _view.expandButton : null;

    private readonly List<Card> _selected = new List<Card>();
    /// <summary>키 → 화면에 올라간 카드 칸. 테두리 색과 순서 배지를 여기로 되짚는다.</summary>
    private readonly Dictionary<string, ChoiceCardItemView> _itemByKey = new Dictionary<string, ChoiceCardItemView>();
    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    private bool _collapsed;
    private Coroutine _slideRoutine;

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

        // ★ 씬이 로드되면 **요청을 기다리지 않고** 곧바로 화면을 정리한다.
        //   씬에 놓인 다이얼로그는 편집하기 좋도록 켜진 채 저장되므로,
        //   여기서 꺼 주지 않으면 게임 시작부터 화면을 가로막는다.
        //   (예전에는 EnsureUI를 '요청이 올 때'만 불러서 정확히 그 증상이 났다)
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(AdoptSceneDialogSoon());
    }

    private void OnDisable()
    {
        EventManager.OnRequireCardPick -= HandleRequireCardPick;
        EventManager.OnRequireCardChoice -= HandleRequireCardChoice;
        EventManager.OnRequireOptionalAction -= HandleRequireOptionalAction;
        EventManager.OnGameSet -= HandleGameSet;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AdoptSceneDialogSoon());

    /// <summary>
    /// 한 프레임 기다렸다가 씬의 다이얼로그를 거둔다.
    /// 씬 로드 직후에는 캔버스·레이아웃이 아직 자리를 잡지 않았다.
    /// </summary>
    private IEnumerator AdoptSceneDialogSoon()
    {
        yield return null;

        // 씬에 없으면 아무 일도 하지 않는다 — 로비처럼 다이얼로그가 없는 화면도 있다.
        // 실제로 필요한 순간(EnsureUI)에 프리팹을 찍으면 된다.
        AdoptSceneDialogIfPresent();
    }

    // ─── 이벤트 수신 ────────────────────────────────────────────────────

    private void HandleRequireCardPick(Player player, List<Card> candidates, int count, CardPickPrompt prompt, Action<List<Card>> callback)
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
            // 문구를 불러준 쪽이 있으면 그걸 쓴다. 순서가 중요한 요청(베로니카)은
            // "몇 장 고르세요"만으로는 뭐를 해야 할지 알 수 없다.
            Message = string.IsNullOrWhiteSpace(prompt.Message) ? $"카드를 {required}장 선택하세요." : prompt.Message,
            Ordered = prompt.Ordered,
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

    /// <summary>
    /// 이 요청에 내가 답해야 하는가.
    /// ★ 온라인은 호스트·게스트 둘 다 <c>UserType.Human</c>이라 사람 여부만 보면
    ///   호스트 화면에 게스트에게 물어야 할 창이 뜨고 호스트가 대신 답해 버린다.
    ///   판정은 <see cref="LocalPlayerContext"/>에 맡긴다.
    /// </summary>
    private static bool IsHuman(Player p) => LocalPlayerContext.IsMine(p);

    private void Enqueue(Request request)
    {
        // ★ 같은 질문이 두 번 오면 무시한다.
        //   게스트에서 "확정을 두 번 해야 창이 닫히는" 증상이 바로 이것이었다 —
        //   첫 확정이 요청 A를 처리하고 창을 닫자마자 ShowNext()가 똑같은 요청 B를 띄우니
        //   창이 안 닫힌 것처럼 보이고, B의 응답은 호스트가 이미 지운 뒤라 무시된다.
        //   근본 원인(알림 중복 방송)은 따로 고치지만, 여기서도 한 번 더 막는다.
        if (IsSameRequest(_current, request))
        {
            Debug.LogWarning("[HumanChoiceDialogUI] 지금 띄운 것과 같은 요청이 또 왔다 — 무시한다.");
            return;
        }

        foreach (Request queued in _queue)
        {
            if (!IsSameRequest(queued, request)) continue;

            Debug.LogWarning("[HumanChoiceDialogUI] 이미 대기 중인 것과 같은 요청이 또 왔다 — 무시한다.");
            return;
        }

        _queue.Enqueue(request);
        if (_current == null) ShowNext();
    }

    /// <summary>
    /// 두 요청이 사실상 같은 질문인가. <b>아직 응답하지 않은 요청끼리만</b> 비교하므로,
    /// 다음 턴에 같은 질문이 다시 오는 정상적인 경우는 막지 않는다.
    /// </summary>
    private static bool IsSameRequest(Request a, Request b)
    {
        if (a == null || b == null) return false;
        if (!ReferenceEquals(a.Player, b.Player)) return false;
        if (a.IsYesNo != b.IsYesNo) return false;
        if (a.Message != b.Message) return false;
        if (a.IsYesNo) return true;

        if (a.RequiredCount != b.RequiredCount) return false;
        if (a.Candidates == null || b.Candidates == null) return false;
        if (a.Candidates.Count != b.Candidates.Count) return false;

        for (int i = 0; i < a.Candidates.Count; i++)
        {
            Card x = a.Candidates[i];
            Card y = b.Candidates[i];
            if (x == null || y == null) return false;

            // 게스트는 스냅샷마다 카드 객체를 새로 만들 수 있어 참조 비교로는 부족하다.
            if (x.InstanceId != y.InstanceId) return false;
        }

        return true;
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
            // 화면을 띄우지 못했다면(캔버스 없음 · 프리팡 없음 · 참조 누락)
            // 게임이 멈추지 않도록 안전한 기본값으로 응답한다.
            // ★ 코드로 UI를 다시 지는 폴백은 일부러 두지 않았다 —
            //   조용히 다른 모양이 뜨는 것보다 명확히 실패하고 진행하는 편이 낫다.
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
            if (_declineButton != null) _declineButton.gameObject.SetActive(true);
            if (_confirmLabel != null) _confirmLabel.text = "예";
            SetButtonInteractable(_confirmButton, true);
            ShowAskingCharacter(_current.Player, _current.Message);
            return;
        }

        HideAskingCharacter();
        _scroll.gameObject.SetActive(true);
        if (_declineButton != null) _declineButton.gameObject.SetActive(false);

        for (int i = 0; i < _current.Candidates.Count; i++)
            CreateCardItem(_current.Candidates[i], i);

        RefreshOrderBadges();
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
        if (_askerImage == null) return;   // 프리팡에 용병 그림 자리가 없으면 그냥 안 보여 준다

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

    /// <summary>Resources 아래에서 카드 칸 템플릿을 찾을 경로.</summary>
    private const string CardItemResourcePath = "Build/ChoiceCardItem";

    private GameObject _cardItemTemplate;

    /// <summary>
    /// 카드 한 장을 목록에 올린다.
    ///
    /// 칸의 모양(크기·테두리 두께·배지 자리)은 <b>프리팹이 정한다.</b>
    /// 코드는 어떤 카드를 넣을지와 선택 상태만 다룬다.
    /// </summary>
    private void CreateCardItem(Card card, int index)
    {
        if (card == null) return;

        GameObject template = ResolveCardItemTemplate();
        if (template == null) return;

        GameObject itemGo = Instantiate(template, _content);
        itemGo.name = $"Item_{index}_{card.Id}";
        itemGo.SetActive(true);

        var view = itemGo.GetComponent<ChoiceCardItemView>();
        if (view == null)
        {
            Debug.LogError("[HumanChoiceDialogUI] 카드 칸 템플릿에 ChoiceCardItemView가 없습니다.");
            Destroy(itemGo);
            return;
        }

        if (!view.Validate(out string reason))
        {
            Debug.LogError($"[HumanChoiceDialogUI] 카드 칸 템플릿이 온전하지 않습니다 — {reason}");
            Destroy(itemGo);
            return;
        }

        view.frameImage.color = cardFrameNormal;

        // 카드 그림은 프리팹이 마련해 둔 자리에 넣고 꽉 채운다.
        // (예전에는 테두리 두께를 코드가 빼서 크기를 계산했다 — 이제 그 몫은 프리팹에 있다)
        GameObject cardGo = InstantiateCardVisual(card, view.cardHost);
        if (cardGo != null)
        {
            var cardRect = cardGo.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                // 프리팹 원본 피벗이 (0.5, 0)이라 그대로 두면 위로 삐져나온다. 중앙으로 정규화한다.
                cardRect.anchorMin = Vector2.zero;
                cardRect.anchorMax = Vector2.one;
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;
                cardRect.localScale = Vector3.one;
            }

            // 카드 내부 그래픽이 클릭을 가로채지 않도록 막는다 (칸의 Button이 받아야 한다)
            foreach (var graphic in cardGo.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        string key = CardKey(card, index);
        _itemByKey[key] = view;
        _spawnedItems.Add(itemGo);

        // 순서가 무의미한 요청('패 3장 버리기' 등)에 번호를 띄우면 오해를 준다.
        if (view.badgeRoot != null) view.badgeRoot.SetActive(false);

        Card captured = card;
        string capturedKey = key;

        view.button.onClick.RemoveAllListeners();
        view.button.onClick.AddListener(() => ToggleSelection(captured, capturedKey));
    }

    /// <summary>카드 칸 템플릿을 한 번만 불러 둔다.</summary>
    private GameObject ResolveCardItemTemplate()
    {
        if (_cardItemTemplate != null) return _cardItemTemplate;

        _cardItemTemplate = Resources.Load<GameObject>(CardItemResourcePath);

        if (_cardItemTemplate == null)
        {
            Debug.LogError(
                $"[HumanChoiceDialogUI] 카드 칸 템플릿을 찾지 못했습니다: Resources/{CardItemResourcePath}. " +
                "카드 목록을 그릴 수 없습니다.");
        }

        return _cardItemTemplate;
    }

    /// <summary>
    /// 선택 순서를 배지에 다시 찍는다.
    /// <c>_selected</c>가 클릭 순서를 담은 리스트라 번호는 곧 <c>index + 1</c>이다.
    /// 1번을 해제하면 2번이 1번이 되는 동작도 여기서 자연히 따라온다.
    /// </summary>
    private void RefreshOrderBadges()
    {
        if (_itemByKey.Count == 0) return;

        foreach (var pair in _itemByKey)
            if (pair.Value != null && pair.Value.badgeRoot != null) pair.Value.badgeRoot.SetActive(false);

        if (_current == null || !_current.Ordered || _current.Candidates == null) return;

        for (int order = 0; order < _selected.Count; order++)
        {
            string key = KeyForCandidate(_selected[order]);
            if (key == null) continue;
            if (!_itemByKey.TryGetValue(key, out ChoiceCardItemView view)) continue;
            if (view == null || view.badgeRoot == null || view.badgeLabel == null) continue;

            view.badgeLabel.text = (order + 1).ToString();
            view.badgeRoot.SetActive(true);
        }
    }

    /// <summary>후보 목록에서 카드를 찾아 <see cref="CardKey"/>와 같은 키를 만든다.</summary>
    private string KeyForCandidate(Card card)
    {
        if (_current?.Candidates == null || card == null) return null;

        for (int i = 0; i < _current.Candidates.Count; i++)
            if (ReferenceEquals(_current.Candidates[i], card)) return CardKey(card, i);

        return null;
    }

    private GameObject InstantiateCardVisual(Card card, Transform parent)
    {
        GameObject prefab = ResolveCardPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[HumanChoiceDialogUI] 카드 프리팹을 찾지 못했습니다. 이름 텍스트로 대체합니다.");
            return CreateNameOnlyCard(parent, card);
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

    /// <summary>
    /// 카드 프리팹조차 없을 때 이름만 보여 주는 최소 대체물.
    /// 카드 항목은 다음 라운드에 프리팹으로 옮기므로 그때 함께 정리된다.
    /// </summary>
    private GameObject CreateNameOnlyCard(Transform parent, Card card)
    {
        var go = new GameObject("CardNameFallback", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = card.Name;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

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

        if (_itemByKey.TryGetValue(key, out ChoiceCardItemView view) && view?.frameImage != null)
            view.frameImage.color = _selected.Contains(card) ? cardFrameSelected : cardFrameNormal;

        RefreshOrderBadges();
        RefreshConfirmState();
    }

    /// <summary>후보 목록에서 해당 카드의 인덱스를 찾아 테두리 색을 갱신한다.</summary>
    private void ApplyFrameColor(Card card, bool selected)
    {
        if (_current?.Candidates == null) return;

        for (int i = 0; i < _current.Candidates.Count; i++)
        {
            if (!ReferenceEquals(_current.Candidates[i], card)) continue;

            if (_itemByKey.TryGetValue(CardKey(card, i), out ChoiceCardItemView view) && view?.frameImage != null)
                view.frameImage.color = selected ? cardFrameSelected : cardFrameNormal;
            return;
        }
    }

    private void RefreshConfirmState()
    {
        if (_current == null || _current.IsYesNo) return;

        int need = _current.RequiredCount;
        bool ready = _selected.Count == need;

        if (_confirmLabel != null)
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

        // ★ 화면 정리가 실패해도 응답은 반드시 돌려준다.
        //   엔진은 WaitUntil로 이 콜백을 기다리므로, 여기서 예외가 나면 게임이 그대로 멈췄다.
        try
        {
            ClearItems();
            HidePanel();
        }
        catch (Exception e)
        {
            Debug.LogError($"[HumanChoiceDialogUI] 패널 정리 중 예외 — 응답은 그대로 보낸다: {e}");
        }

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

        try
        {
            ClearItems();
            HidePanel();
        }
        catch (Exception e)
        {
            Debug.LogError($"[HumanChoiceDialogUI] 패널 정리 중 예외 — 응답은 그대로 보낸다: {e}");
        }

        finished.BoolCallback?.Invoke(false);
        ShowNext();
    }

    // ─── 접기 / 펼치기 ──────────────────────────────────────────────────

    private void SetCollapsed(bool collapsed, bool instant = false)
    {
        _collapsed = collapsed;

        if (_expandButton != null)
            _expandButton.gameObject.SetActive(collapsed);

        if (_panel == null) return;

        Vector2 target = collapsed ? CollapsedPosition : _panelShownPosition;

        if (_slideRoutine != null) StopCoroutine(_slideRoutine);

        if (instant || !gameObject.activeInHierarchy)
        {
            _panel.anchoredPosition = target;
            return;
        }

        _slideRoutine = StartCoroutine(SlidePanelTo(target));
    }

    /// <summary>
    /// 접었을 때의 자리 — 프리팹에 놓인 자리에서 패널 높이만큼 아래.
    ///
    /// ★ x는 건드리지 않는다. 예전에는 0으로 덮어써서, 프리팹에서 패널을
    ///   가운데가 아닌 곳에 두면 접었다 펼 때 옆으로 튀었다.
    /// </summary>
    private Vector2 CollapsedPosition
        => _panelShownPosition - new Vector2(0f, _panel.rect.height);

    private IEnumerator SlidePanelTo(Vector2 target)
    {
        Vector2 start = _panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            _panel.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        _panel.anchoredPosition = target;
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
        _itemByKey.Clear();   // 배지·테두리는 칸의 일부라 위에서 함께 파괴된다
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

        if (_view != null && _hostCanvas == canvas) return;

        // 씬이 바뀌어 캔버스가 교체되면 화면을 다시 마련한다 (로직 싱글턴은 그대로 산다).
        // ★ 우리가 찍은 것만 파괴한다. 씬에 놓인 것을 지우면 기획자의 작업이 사라진다.
        if (_dialogRoot != null && _ownsDialogRoot) Destroy(_dialogRoot);
        _dialogRoot = null;
        _view = null;
        _ownsDialogRoot = false;

        _hostCanvas = canvas;

        // ① 씬에 이미 놓여 있으면 그것을 쓴다.
        //    이 검사가 없으면 씬에 배치한 것 위에 하나를 더 찍어 **둘이 겹친다.**
        //    (CharacterSlotBar·CharacterPicker에서 똑같이 겪은 문제다)
        if (AdoptSceneDialogIfPresent()) return;

        // ② 없으면 프리팹을 찍는다
        BuildFromPrefab(canvas);
    }

    /// <summary>
    /// 씬에 미리 놓인 다이얼로그를 찾아 연결한다. 위치·크기는 손대지 않는다.
    ///
    /// 씬 로드 직후에도 부르고(화면을 곧바로 정리하기 위해),
    /// 요청이 왔을 때도 부른다(그때까지 없었을 수도 있으므로).
    /// 이미 같은 것을 쓰고 있으면 아무 일도 하지 않는다.
    /// </summary>
    private bool AdoptSceneDialogIfPresent()
    {
        // 이미 우리가 찍은 것을 쓰고 있으면 그대로 둔다.
        // (씬이 바뀌면 그것은 씬과 함께 사라져 _view가 null이 되므로 자연스럽게 다시 찾는다)
        if (_ownsDialogRoot && _view != null) return true;

        foreach (HumanChoiceDialogView candidate in FindObjectsByType<HumanChoiceDialogView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate == _view) return true;   // 이미 쓰고 있다

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning(
                    $"[HumanChoiceDialogUI] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뜁니다 — {reason}");
                continue;
            }

            // ★ 꺼진 부모 아래에 있으면 무슨 짓을 해도 화면에 나오지 않는다.
            //   이 화면은 엔진이 응답을 기다리는 임계 경로라, 조용히 안 보이면 게임이 멈춘다.
            if (FindInactiveAncestor(candidate.transform) is Transform blocker)
            {
                Debug.LogError(
                    $"[HumanChoiceDialogUI] 씬의 '{candidate.name}'은 꺼져 있는 '{blocker.name}' 아래에 있어 화면에 뜰 수 없습니다. " +
                    "캔버스 바로 아래로 옮기거나 씬에서 지우세요. 지금은 프리팹을 새로 찍어 씁니다.");
                continue;
            }

            Canvas canvas = candidate.GetComponentInParent<Canvas>();

            _dialogRoot = candidate.gameObject;
            _view = candidate;
            _ownsDialogRoot = false;   // 빌려 쓰는 것이다
            _hostCanvas = canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;

            PrepareView();

            Debug.Log($"[HumanChoiceDialogUI] 씬에 배치된 '{candidate.name}'을 사용합니다.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 자기 자신을 뺀 조상 중에 꺼져 있는 것이 있으면 돌려준다. 없으면 null.
    /// (자기 자신은 코드가 켜고 끄므로 검사에서 뺀다)
    /// </summary>
    private static Transform FindInactiveAncestor(Transform t)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf) return p;
        }

        return null;
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

    /// <summary>
    /// 프리팹을 씬 캔버스 아래에 찍고 참조를 연결한다.
    ///
    /// ★ 씬 캔버스의 자식으로 두는 것이 중요하다 — CanvasScaler를 물려받아야
    ///   카드 크기가 보드와 같은 비율로 보인다.
    /// </summary>
    private void BuildFromPrefab(Canvas canvas)
    {
        GameObject prefab = Resources.Load<GameObject>(DialogResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[HumanChoiceDialogUI] 프리팹을 찾지 못했습니다: Resources/{DialogResourcePath}. " +
                "선택 요청은 기본값으로 자동 응답됩니다.");
            return;
        }

        // 텍스트를 만지기 전에 폰트를 정한다 (기본 폰트로는 한글이 ㅁ로 깨진다)
        _font = UiFontResolver.Resolve();

        _dialogRoot = Instantiate(prefab, canvas.transform);
        _dialogRoot.name = prefab.name;   // (Clone) 꼬리표 제거

        _view = _dialogRoot.GetComponent<HumanChoiceDialogView>()
                ?? _dialogRoot.GetComponentInChildren<HumanChoiceDialogView>(true);

        if (_view == null)
        {
            Debug.LogError(
                $"[HumanChoiceDialogUI] '{prefab.name}'에 HumanChoiceDialogView가 없습니다. " +
                "프리팹 루트에 컴포넌트를 붙여 주세요.");
            Destroy(_dialogRoot);
            _dialogRoot = null;
            return;
        }

        if (!_view.Validate(out string reason))
        {
            Debug.LogError(
                $"[HumanChoiceDialogUI] 프리팹 참조가 온전하지 않습니다 — {reason}. " +
                "선택 요청은 기본값으로 자동 응답됩니다.");
            Destroy(_dialogRoot);
            _dialogRoot = null;
            _view = null;
            return;
        }

        _ownsDialogRoot = true;
        PrepareView();
    }

    /// <summary>
    /// 찍었든 빌려 왔든, 쓰기 전에 똑같이 해 두어야 하는 것들.
    ///
    /// ★ 씬에 놓인 것은 편집하기 좋도록 **패널이 켜진 채 저장돼 있다.**
    ///   여기서 꺼 주지 않으면 화면 진입부터 계속 떠 있게 된다.
    /// </summary>
    private void PrepareView()
    {
        // ★ 놓여 있던 자리를 '보이는 위치'로 기억한다.
        //   예전에는 y=0으로 못박아서, 패널을 어디에 두든 화면 아래로 튀었다.
        _panelShownPosition = _view.panel.anchoredPosition;

        WireButtons();

        _view.panel.gameObject.SetActive(false);
        if (_view.expandButton != null) _view.expandButton.gameObject.SetActive(false);
        if (_view.declineButton != null) _view.declineButton.gameObject.SetActive(false);
        if (_view.askerImage != null) _view.askerImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 프리팹 버튼에 동작을 건다.
    /// 인스펙터에 남아 있을지 모를 배선과 겹치지 않도록 먼저 비운다.
    /// </summary>
    private void WireButtons()
    {
        if (_view.confirmButton != null)
        {
            _view.confirmButton.onClick.RemoveAllListeners();
            _view.confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (_view.declineButton != null)
        {
            _view.declineButton.onClick.RemoveAllListeners();
            _view.declineButton.onClick.AddListener(OnDeclineClicked);
        }

        if (_view.collapseButton != null)
        {
            _view.collapseButton.onClick.RemoveAllListeners();
            _view.collapseButton.onClick.AddListener(() => SetCollapsed(true));
        }

        if (_view.expandButton != null)
        {
            _view.expandButton.onClick.RemoveAllListeners();
            _view.expandButton.onClick.AddListener(() => SetCollapsed(false));
        }
    }

}
