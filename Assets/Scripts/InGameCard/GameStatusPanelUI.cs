// GameStatusPanelUI.cs — 인게임 상태 표시 패널 (턴·페이즈 / 로그 / 결과)
//
// 엔진은 이미 필요한 이벤트를 모두 쏘고 있었는데 화면에 표시하는 곳이 없었다. 이 스크립트가 그 소비자다.
//
//   1) 턴 / 페이즈  — 화면 상단 중앙에 "Round 3 · 메인 페이즈"
//   2) 진행 로그    — 화면 좌측에 최근 N줄 (엔진이 보내는 <color> 리치 텍스트 그대로 표시)
//   3) 결과 오버레이 — 게임/매치가 끝나면 전체 화면 딤 + 결과 문구
//
// 씬 배선이 필요 없다. RuntimeInitializeOnLoadMethod로 자가 생성하고 UI도 코드로 만든다.
// 로그 패널 좌측 상단은 톱니바퀴 버튼(DeckInfoPanelUI) 자리를 피해 아래쪽에서 시작한다.
using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameStatusPanelUI : MonoBehaviour
{
    public static GameStatusPanelUI Instance { get; private set; }

    [Header("Log")]
    [Tooltip("화면에 유지할 최근 로그 줄 수")]
    public int maxLogLines = 10;
    public Vector2 logPanelSize = new Vector2(430f, 250f);
    [Tooltip("톱니바퀴 버튼 아래에서 시작하도록 띄우는 간격")]
    public float logPanelTopOffset = 74f;
    public Color logPanelColor = new Color(0.08f, 0.08f, 0.10f, 0.62f);

    [Header("Turn / Phase")]
    public Color headerColor = new Color(0.08f, 0.08f, 0.10f, 0.72f);
    [Tooltip("화면 세로 중앙 기준 오프셋. 0이면 정중앙")]
    public float headerCenterOffsetY = 0f;

    [Header("Result Overlay")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.78f);
    public Color winColor = new Color(0.35f, 0.85f, 1f, 1f);
    public Color loseColor = new Color(1f, 0.45f, 0.45f, 1f);
    public Color drawColor = new Color(0.9f, 0.9f, 0.6f, 1f);
    public Color retryColor = new Color(0.20f, 0.55f, 0.95f, 1f);
    public Color menuColor = new Color(0.36f, 0.36f, 0.42f, 1f);
    [Tooltip("[메인 메뉴로] 버튼이 로드할 씬 이름 (Build Settings에 포함되어 있어야 한다)")]
    public string mainMenuSceneName = "MainMenu";

    private Canvas _hostCanvas;
    private TMP_FontAsset _font;

    private TextMeshProUGUI _headerText;
    private TextMeshProUGUI _logText;
    private GameObject _overlay;
    private TextMeshProUGUI _overlayTitle;
    private TextMeshProUGUI _overlayDetail;

    private readonly Queue<string> _logLines = new Queue<string>();
    private int _currentTurn = 1;
    private string _currentPhase = "";

    // ─── 입력 제한 시간 표시 ────────────────────────────────────────────
    // 서버는 사람 입력을 choose_wait_time만큼만 기다리고 그 뒤에는 자동으로 결정한다.
    // 남은 시간을 안 보여 주면 갑자기 카드가 자동 선택돼 당황하게 된다.
    private float _inputDeadline = -1f;
    private string _inputLabel;

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("GameStatusPanelUI");
        Instance = go.AddComponent<GameStatusPanelUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnTurnStart += HandleTurnStart;

        EventManager.OnResourcePhase += HandleResourcePhase;
        EventManager.OnDrawPhase += HandleDrawPhase;
        EventManager.OnSetPhase += HandleSetPhase;
        EventManager.OnOpenPhase += HandleOpenPhase;
        EventManager.OnMainPhase += HandleMainPhase;
        EventManager.OnEndPhase += HandleEndPhase;

        EventManager.OnLogMessage += HandleLogMessage;

        EventManager.OnRequireSetPhaseAction += HandleRequireSetTimer;
        EventManager.OnRequireOpenPhaseAction += HandleRequireOpenTimer;
        EventManager.OnRequireCardPick += HandleRequireCardPickTimer;
        EventManager.OnRequireOptionalAction += HandleRequireOptionalTimer;
        EventManager.OnCardSet += HandleCardSetTimer;

        EventManager.OnGameSet += HandleGameSet;
        EventManager.OnGameDraw += HandleGameDraw;
        EventManager.OnMatchSet += HandleMatchSet;
        EventManager.OnMatchDraw += HandleMatchDraw;
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnTurnStart -= HandleTurnStart;

        EventManager.OnResourcePhase -= HandleResourcePhase;
        EventManager.OnDrawPhase -= HandleDrawPhase;
        EventManager.OnSetPhase -= HandleSetPhase;
        EventManager.OnOpenPhase -= HandleOpenPhase;
        EventManager.OnMainPhase -= HandleMainPhase;
        EventManager.OnEndPhase -= HandleEndPhase;

        EventManager.OnLogMessage -= HandleLogMessage;

        EventManager.OnRequireSetPhaseAction -= HandleRequireSetTimer;
        EventManager.OnRequireOpenPhaseAction -= HandleRequireOpenTimer;
        EventManager.OnRequireCardPick -= HandleRequireCardPickTimer;
        EventManager.OnRequireOptionalAction -= HandleRequireOptionalTimer;
        EventManager.OnCardSet -= HandleCardSetTimer;

        EventManager.OnGameSet -= HandleGameSet;
        EventManager.OnGameDraw -= HandleGameDraw;
        EventManager.OnMatchSet -= HandleMatchSet;
        EventManager.OnMatchDraw -= HandleMatchDraw;
    }

    // ─── 이벤트 핸들러 ──────────────────────────────────────────────────

    private Player _p1;
    private Player _p2;

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;

        EnsureUI();
        _logLines.Clear();
        if (_logText != null) _logText.text = "";
        HideOverlay();

        _currentTurn = 1;
        _currentPhase = "";
        RefreshHeader();
    }

    private void HandleTurnStart(int turn, string subject)
    {
        _currentTurn = turn;
        _currentPhase = "";
        RefreshHeader();
    }

    private void HandleResourcePhase(string s, int t) => SetPhase("자원 페이즈", t);
    private void HandleDrawPhase(string s, int t) => SetPhase("드로우 페이즈", t);
    private void HandleSetPhase(string s, int t) => SetPhase("세트 페이즈", t);
    private void HandleOpenPhase(string s, int t) => SetPhase("오픈 페이즈", t);
    private void HandleMainPhase(string s, int t) => SetPhase("메인 페이즈", t);
    private void HandleEndPhase(string s, int t) => SetPhase("엔드 페이즈", t);

    private void SetPhase(string phaseName, int turn)
    {
        EnsureUI();

        if (_currentPhase != phaseName) StopInputTimer(); // 페이즈가 넘어가면 대기도 끝난 것이다

        _currentTurn = turn;
        _currentPhase = phaseName;
        RefreshHeader();
    }

    // ─── 제한 시간 ──────────────────────────────────────────────────────

    private void StartInputTimer(Player player, string label)
    {
        if (!LocalPlayerContext.IsMine(player)) return;

        float limit = GameRules.ChooseWaitTime / 1000f;
        if (limit <= 0f) return;

        _inputLabel = label;
        _inputDeadline = Time.unscaledTime + limit;
        RefreshHeader();
    }

    private void StopInputTimer()
    {
        if (_inputDeadline < 0f) return;

        _inputDeadline = -1f;
        _inputLabel = null;
        RefreshHeader();
    }

    private void HandleRequireSetTimer(Player p, GameContext c, Action<Card> cb) => StartInputTimer(p, "세트");
    private void HandleRequireOpenTimer(Player p, Card card, int cost, GameContext c, Action<OpenPhaseChoice> cb) => StartInputTimer(p, "공개/폐기");
    private void HandleRequireCardPickTimer(Player p, List<Card> cards, int count, CardPickPrompt prompt, Action<List<Card>> cb) => StartInputTimer(p, "카드 선택");
    private void HandleRequireOptionalTimer(Player p, string msg, GameContext c, Action<bool> cb) => StartInputTimer(p, "선택");
    private void HandleCardSetTimer(Card card, Player owner)
    {
        if (LocalPlayerContext.IsMine(owner)) StopInputTimer();
    }

    private void Update()
    {
        if (_inputDeadline < 0f) return;

        if (Time.unscaledTime >= _inputDeadline)
        {
            StopInputTimer();
            return;
        }

        RefreshHeader();
    }

    /// <summary>
    /// 결과 오버레이를 임의 문구로 띄운다. 상대 이탈처럼 승패가 아닌 종료 상황에 쓴다.
    /// 오버레이에는 [다시 하기] / [메인 메뉴로]가 이미 붙어 있다.
    /// </summary>
    public void ShowNotice(string title, string detail)
    {
        EnsureUI();
        StopInputTimer();
        ShowOverlay(title, detail, drawColor);
    }

    private void RefreshHeader()
    {
        if (_headerText == null) return;

        string baseText = string.IsNullOrEmpty(_currentPhase)
            ? $"ROUND {_currentTurn}"
            : $"ROUND {_currentTurn}  ·  {_currentPhase}";

        if (_inputDeadline >= 0f)
        {
            int remain = Mathf.Max(0, Mathf.CeilToInt(_inputDeadline - Time.unscaledTime));
            string color = remain <= 5 ? "#ff6b6b" : "#ffd479";
            baseText += $"   <color={color}>{_inputLabel} {remain}초</color>";
        }

        _headerText.text = baseText;
    }

    private void HandleLogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        EnsureUI();
        if (_logText == null) return;

        // 엔진 로그에는 앞뒤 개행이 섞여 있다. 빈 줄이 화면을 잡아먹지 않도록 정리한다.
        foreach (var raw in message.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            _logLines.Enqueue(line);
            while (_logLines.Count > maxLogLines) _logLines.Dequeue();
        }

        _logText.text = string.Join("\n", _logLines);
    }

    // ─── 결과 오버레이 ──────────────────────────────────────────────────

    private void HandleGameSet(Player winner)
    {
        Player human = ResolveHuman();
        bool humanWon = human != null && ReferenceEquals(winner, human);

        string title = human == null
            ? "게임 종료"
            : (humanWon ? "승리" : "패배");
        Color color = human == null ? drawColor : (humanWon ? winColor : loseColor);

        ShowOverlay(title, winner != null ? $"{winner.Name} 승리" : "", color);
    }

    private void HandleGameDraw(Player p1, Player p2, int turn) =>
        ShowOverlay("무승부", $"{turn}턴 종료", drawColor);

    private void HandleMatchSet(Player winner)
    {
        Player human = ResolveHuman();
        bool humanWon = human != null && ReferenceEquals(winner, human);
        ShowOverlay(
            humanWon ? "매치 승리" : "매치 패배",
            winner != null ? $"{winner.Name} 세트 승리" : "",
            humanWon ? winColor : loseColor);
    }

    private void HandleMatchDraw(Player p1, Player p2) =>
        ShowOverlay("매치 무승부", "", drawColor);

    /// <summary>
    /// 이 화면의 주인(= 승패를 판정할 기준 플레이어).
    ///
    /// ★ 예전에는 <c>BattleManager.HumanPlayer</c>를 봤는데, 온라인에서는 BattleManager가
    ///   매치를 돌리지 않으므로 **항상 null**이었다. 그 결과 이긴 쪽에도 "매치 패배"가,
    ///   게스트에는 "게임 종료"가 떴다(승패 판정이 통째로 무너진 상태였다).
    ///   지금은 매치 시작 때 받은 두 플레이어에서 LocalPlayerContext로 고른다.
    /// </summary>
    private Player ResolveHuman()
    {
        Player mine = LocalPlayerContext.ResolveMine(_p1, _p2);
        if (mine != null) return mine;

        // 로컬 봇전 등 조작 주체가 없을 때의 폴백
        return BattleManager.Instance != null ? BattleManager.Instance.HumanPlayer : null;
    }

    private void ShowOverlay(string title, string detail, Color color)
    {
        EnsureUI();
        if (_overlay == null) return;

        // 페이즈 표시가 화면 중앙에 있어 결과 문구를 가린다. 결과가 뜨는 동안에는 숨긴다.
        SetHeaderVisible(false);

        _overlay.SetActive(true);
        _overlay.transform.SetAsLastSibling();

        _overlayTitle.text = title;
        _overlayTitle.color = color;
        _overlayDetail.text = detail;
    }

    private void HideOverlay()
    {
        if (_overlay != null) _overlay.SetActive(false);
        SetHeaderVisible(true);
    }

    private void SetHeaderVisible(bool visible)
    {
        if (_headerText != null && _headerText.transform.parent != null)
            _headerText.transform.parent.gameObject.SetActive(visible);
    }

    // ─── UI 생성 ────────────────────────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null) return;
        if (_headerText != null && _hostCanvas == canvas) return;

        if (_headerText != null) Destroy(_headerText.transform.parent.gameObject);
        if (_logText != null) Destroy(_logText.transform.parent.gameObject);
        if (_overlay != null) Destroy(_overlay);

        _hostCanvas = canvas;
        _font = UiFontResolver.Resolve();
        Build(canvas);
    }

    private Canvas ResolveCanvas()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myHandTransform != null)
        {
            var c = PlayerUIManager.Instance.myHandTransform.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }
        return FindFirstObjectByType<Canvas>();
    }

    private void Build(Canvas canvas)
    {
        // ── 상단 중앙: 턴 / 페이즈 ──
        var headerGo = new GameObject("StatusHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        // 화면 가로 중앙 · 세로도 중앙에 배치한다.
        // 게임이 끝나면 결과 문구를 가리므로 ShowOverlay에서 숨긴다.
        var headerRect = (RectTransform)headerGo.transform;
        headerRect.SetParent(canvas.transform, false);
        headerRect.anchorMin = new Vector2(0.5f, 0.5f);
        headerRect.anchorMax = new Vector2(0.5f, 0.5f);
        headerRect.pivot = new Vector2(0.5f, 0.5f);
        headerRect.sizeDelta = new Vector2(360f, 48f);
        headerRect.anchoredPosition = new Vector2(0f, headerCenterOffsetY);
        var headerBg = headerGo.GetComponent<Image>();
        headerBg.color = headerColor;
        headerBg.raycastTarget = false;

        _headerText = CreateText(headerRect, "ROUND 1", 24f, TextAlignmentOptions.Center);
        Stretch(_headerText.rectTransform);

        // ── 좌측: 진행 로그 ──
        var logGo = new GameObject("StatusLog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        var logRect = (RectTransform)logGo.transform;
        logRect.SetParent(canvas.transform, false);
        logRect.anchorMin = new Vector2(0f, 1f);
        logRect.anchorMax = new Vector2(0f, 1f);
        logRect.pivot = new Vector2(0f, 1f);
        logRect.sizeDelta = logPanelSize;
        logRect.anchoredPosition = new Vector2(12f, -logPanelTopOffset);
        var logBg = logGo.GetComponent<Image>();
        logBg.color = logPanelColor;
        logBg.raycastTarget = false;

        _logText = CreateText(logRect, "", 16f, TextAlignmentOptions.BottomLeft);
        var logTextRect = _logText.rectTransform;
        logTextRect.anchorMin = Vector2.zero;
        logTextRect.anchorMax = Vector2.one;
        logTextRect.offsetMin = new Vector2(10f, 8f);
        logTextRect.offsetMax = new Vector2(-10f, -8f);
        _logText.richText = true;   // 엔진이 보내는 <color> 태그를 그대로 살린다
        _logText.enableWordWrapping = true;

        // ── 결과 오버레이 ──
        _overlay = new GameObject("ResultOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        var ovRect = (RectTransform)_overlay.transform;
        ovRect.SetParent(canvas.transform, false);
        Stretch(ovRect);
        _overlay.GetComponent<Image>().color = overlayColor;

        var ovCanvas = _overlay.GetComponent<Canvas>();
        ovCanvas.overrideSorting = true;
        ovCanvas.sortingOrder = 900; // 선택 다이얼로그(500)보다 위

        _overlayTitle = CreateText(ovRect, "", 72f, TextAlignmentOptions.Center);
        var titleRect = _overlayTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(700f, 110f);
        titleRect.anchoredPosition = new Vector2(0f, 40f);

        _overlayDetail = CreateText(ovRect, "", 26f, TextAlignmentOptions.Center);
        var detailRect = _overlayDetail.rectTransform;
        detailRect.anchorMin = new Vector2(0.5f, 0.5f);
        detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.pivot = new Vector2(0.5f, 0.5f);
        detailRect.sizeDelta = new Vector2(700f, 44f);
        detailRect.anchoredPosition = new Vector2(0f, -40f);

        // ── 종료 후 이탈 버튼 ──
        // 이게 없으면 게임이 끝난 뒤 화면이 그대로 멈춰 에디터를 껐다 켜야 한다.
        TextMeshProUGUI retryLabel, menuLabel;

        Button retry = CreateButton(ovRect, "RetryButton", "다시 하기", retryColor, out retryLabel);
        var retryRect = retry.GetComponent<RectTransform>();
        retryRect.anchorMin = new Vector2(0.5f, 0.5f);
        retryRect.anchorMax = new Vector2(0.5f, 0.5f);
        retryRect.pivot = new Vector2(1f, 0.5f);
        retryRect.sizeDelta = new Vector2(210f, 60f);
        retryRect.anchoredPosition = new Vector2(-12f, -150f);
        retry.onClick.AddListener(RestartMatch);

        Button menu = CreateButton(ovRect, "MainMenuButton", "메인 메뉴로", menuColor, out menuLabel);
        var menuRect = menu.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0f, 0.5f);
        menuRect.sizeDelta = new Vector2(210f, 60f);
        menuRect.anchoredPosition = new Vector2(12f, -150f);
        menu.onClick.AddListener(GoToMainMenu);

        _overlay.SetActive(false);
    }

    // ─── 종료 후 이동 ───────────────────────────────────────────────────

    /// <summary>
    /// 같은 씬을 다시 로드해 매치를 처음부터 시작한다.
    /// BattleManager.StartMatch를 다시 부르는 방식은 카드 GO 풀·레지스트리·엔진 상태를
    /// 일일이 되돌려야 해서 위험하다. 씬 재로드가 가장 확실하다.
    /// </summary>
    private void RestartMatch()
    {
        HideOverlay();

        // 온라인 대전은 같은 세션으로 다시 들어가면 안 된다.
        // 씬을 재로드하면 덱을 다시 올리고 이벤트 리스너를 다시 붙여 과거 이벤트가 전부 재생된다.
        if (OnlineMatchStarter.IsOnlineSessionActive)
        {
            GoToMainMenu();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMainMenu()
    {
        HideOverlay();

        // 온라인 매치를 떠나므로 세션 흔적을 지운다.
        // 남겨 두면 같은 실행 안에서 로컬 봇전을 시작할 때 LocalMatchStarter가 계속 비켜선다.
        OnlineMatchStarter.ClearSession();

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private Button CreateButton(Transform parent, string name, string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        labelText = CreateText(go.transform, label, 24f, TextAlignmentOptions.Center);
        var r = labelText.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font; // 기본 폰트는 한글 글리프가 없어 ㅁ로 깨진다
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }
}
