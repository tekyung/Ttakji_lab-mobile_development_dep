// GameStatusPanelUI.cs — 인게임 상태 표시 패널 (턴·페이즈 / 로그 / 결과)
//
// 엔진은 이미 필요한 이벤트를 모두 쏘고 있었는데 화면에 표시하는 곳이 없었다. 이 스크립트가 그 소비자다.
//
//   1) 턴 / 페이즈  — 화면 상단 중앙에 "Round 3 · 메인 페이즈"
//   2) 진행 로그    — 화면 좌측에 최근 N줄 (엔진이 보내는 <color> 리치 텍스트 그대로 표시)
//   3) 결과 오버레이 — 게임/매치가 끝나면 전체 화면 딤 + 결과 문구
//
// ── 화면은 프리팹이 갖는다 ────────────────────────────────────────────
//   프리팹 — 위치·크기·앵커·글꼴·바탕색·버튼 색·정렬 순서
//   코드   — 표시할 문구 · 로그 줄 쌓기 · 오버레이를 열고 닫기 · 승패에 따른 글자색
//
//   Resources/Build/GameStatusPanelRoot
//
//   씬에 미리 놓여 있으면 그것을 쓰고, 없으면 프리팹을 찍는다.
//   로직 싱글턴은 씬을 넘어 살아남지만 화면은 씬 캔버스에 붙으므로 수명이 다르다.
//
// ★ 가운데 띠와 로그는 UiSortingLayer(100)로 자리를 명시한다.
//   예전에는 정렬 순서가 없어, '실행 중에 캔버스 맨 뒤에 붙는다'는 사정만으로 맨 앞에 그려졌다.
//   그 바람에 씬에 놓인 설정 패널을 덮어 버렸다.
using System;
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStatusPanelUI : MonoBehaviour
{
    public static GameStatusPanelUI Instance { get; private set; }

    /// <summary>Resources 아래에서 이 화면을 찾을 경로.</summary>
    private const string PanelResourcePath = "Build/GameStatusPanelRoot";

    [Header("로그")]
    [Tooltip("화면에 유지할 최근 로그 줄 수. 패널 크기·색·자리는 프리팹이 갖는다.")]
    public int maxLogLines = 10;

    [Header("승패에 따라 코드가 바꾸는 글자색")]
    public Color winColor = new Color(0.35f, 0.85f, 1f, 1f);
    public Color loseColor = new Color(1f, 0.45f, 0.45f, 1f);
    public Color drawColor = new Color(0.9f, 0.9f, 0.6f, 1f);

    [Header("이동")]
    [Tooltip("[메인 메뉴로] 버튼이 로드할 씬 이름 (Build Settings에 포함되어 있어야 한다)")]
    public string mainMenuSceneName = "MainMenu";

    private Canvas _hostCanvas;

    private GameObject _root;
    private GameStatusPanelView _view;

    /// <summary>내가 찍은 것인가. 씬에서 빌려 온 것은 절대 파괴하지 않는다.</summary>
    private bool _ownsRoot;

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

        // ★ 씬이 로드되면 **게임 시작을 기다리지 않고** 곧바로 화면을 정리한다.
        //   씬에 놓인 UI는 편집하기 좋도록 켜진 채 저장되므로,
        //   여기서 결과 오버레이를 꺼 주지 않으면 진입하자마자 화면을 가로막는다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(AdoptSceneRootSoon());
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

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AdoptSceneRootSoon());

    /// <summary>
    /// 한 프레임 기다렸다가 씬에 놓인 화면을 거둔다.
    /// 씬 로드 직후에는 캔버스·레이아웃이 아직 자리를 잡지 않았다.
    /// </summary>
    private IEnumerator AdoptSceneRootSoon()
    {
        yield return null;

        // 씬에 없으면 아무 일도 하지 않는다 — 로비처럼 이 화면이 없는 곳도 있다.
        AdoptSceneRootIfPresent();
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
        if (_view != null && _view.logText != null) _view.logText.text = "";
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
        if (_view == null || _view.headerText == null) return;

        string baseText = string.IsNullOrEmpty(_currentPhase)
            ? $"ROUND {_currentTurn}"
            : $"ROUND {_currentTurn}  ·  {_currentPhase}";

        if (_inputDeadline >= 0f)
        {
            int remain = Mathf.Max(0, Mathf.CeilToInt(_inputDeadline - Time.unscaledTime));
            string color = remain <= 5 ? "#ff6b6b" : "#ffd479";
            baseText += $"   <color={color}>{_inputLabel} {remain}초</color>";
        }

        _view.headerText.text = baseText;
    }

    private void HandleLogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        EnsureUI();
        if (_view == null || _view.logText == null) return;

        // 엔진 로그에는 앞뒤 개행이 섞여 있다. 빈 줄이 화면을 잡아먹지 않도록 정리한다.
        foreach (var raw in message.Split('\n'))
        {
            string line = StripUnrenderable(raw).Trim();
            if (line.Length == 0) continue;

            _logLines.Enqueue(line);
            while (_logLines.Count > maxLogLines) _logLines.Dequeue();
        }

        _view.logText.text = string.Join("\n", _logLines);
    }

    /// <summary>
    /// 화면 폰트가 그릴 수 없는 글자를 걷어낸다.
    ///
    /// 엔진 로그에는 이모지가 섞여 있다. 콘솔에서는 잘 보이지만, 이 화면이 쓰는 한글 TMP 폰트는
    /// <b>정적 아틀라스</b>라 해당 글리프가 없어 네모나 엉뚱한 글자로 찍힌다.
    /// 이모지는 BMP 밖에 있어 C#에서 서로게이트 쌍으로 표현되므로, 그 쌍과 뒤따르는
    /// 변형 선택자·결합 문자를 함께 지운다.
    ///
    /// ★ 엔진 쪽 문자열은 건드리지 않는다. 콘솔 실행에서는 그대로 쓸모가 있고,
    ///   로직 레이어는 화면 사정을 몰라야 한다.
    /// </summary>
    private static string StripUnrenderable(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        bool needsWork = false;
        foreach (char c in source)
        {
            if (IsUnrenderable(c)) { needsWork = true; break; }
        }

        if (!needsWork) return source;

        var sb = new System.Text.StringBuilder(source.Length);
        foreach (char c in source)
        {
            if (!IsUnrenderable(c)) sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool IsUnrenderable(char c)
    {
        if (char.IsSurrogate(c)) return true;   // 이모지 본체
        if (c == '️' || c == '︎') return true;   // 변형 선택자
        if (c == '‍') return true;                     // 결합용 폭 없는 문자
        return false;
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
        if (_view == null || _view.overlay == null) return;

        // 페이즈 표시가 화면 중앙에 있어 결과 문구를 가린다. 결과가 뜨는 동안에는 숨긴다.
        SetHeaderVisible(false);

        _view.overlay.SetActive(true);
        if (_root != null) _root.transform.SetAsLastSibling();

        if (_view.overlayTitle != null)
        {
            _view.overlayTitle.text = title;
            _view.overlayTitle.color = color;
        }

        if (_view.overlayDetail != null) _view.overlayDetail.text = detail;
    }

    private void HideOverlay()
    {
        if (_view != null && _view.overlay != null) _view.overlay.SetActive(false);
        SetHeaderVisible(true);
    }

    private void SetHeaderVisible(bool visible)
    {
        if (_view != null && _view.header != null) _view.header.SetActive(visible);
    }

    // ─── UI 생성 ────────────────────────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null) return;

        if (_view != null && _hostCanvas == canvas) return;

        // 씬이 바뀌어 캔버스가 교체되면 화면을 다시 마련한다 (로직 싱글턴은 그대로 산다).
        // ★ 우리가 찍은 것만 파괴한다. 씬에 놓인 것을 지우면 기획자의 작업이 사라진다.
        if (_root != null && _ownsRoot) Destroy(_root);
        _root = null;
        _view = null;
        _ownsRoot = false;

        _hostCanvas = canvas;

        // ① 씬에 이미 놓여 있으면 그것을 쓴다.
        //    이 검사가 없으면 씬에 배치한 것 위에 하나를 더 찍어 둘이 겹친다.
        if (AdoptSceneRootIfPresent()) return;

        // ② 없으면 프리팹을 찍는다
        BuildFromPrefab(canvas);
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

    /// <summary>
    /// 씬에 미리 놓인 화면을 찾아 연결한다. 위치·크기는 손대지 않는다.
    /// </summary>
    private bool AdoptSceneRootIfPresent()
    {
        // 이미 우리가 찍은 것을 쓰고 있으면 그대로 둔다.
        // (씬이 바뀌면 그것은 씬과 함께 사라져 _view가 null이 되므로 자연스럽게 다시 찾는다)
        if (_ownsRoot && _view != null) return true;

        foreach (GameStatusPanelView candidate in FindObjectsByType<GameStatusPanelView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate == _view) return true;   // 이미 쓰고 있다

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning(
                    $"[GameStatusPanel] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뜁니다 — {reason}");
                continue;
            }

            // ★ 꺼진 부모 아래에 있으면 무슨 짓을 해도 화면에 나오지 않는다.
            //   여기서는 결과 오버레이가 안 뜬다는 뜻이고, 그러면 게임이 끝난 뒤
            //   화면에서 빠져나갈 방법이 사라진다.
            if (FindInactiveAncestor(candidate.transform) is Transform blocker)
            {
                Debug.LogError(
                    $"[GameStatusPanel] 씬의 '{candidate.name}'은 꺼져 있는 '{blocker.name}' 아래에 있어 화면에 뜰 수 없습니다. " +
                    "캔버스 바로 아래로 옮기거나 씬에서 지우세요. 지금은 프리팹을 새로 찍어 씁니다.");
                continue;
            }

            Canvas canvas = candidate.GetComponentInParent<Canvas>();

            _root = candidate.gameObject;
            _view = candidate;
            _ownsRoot = false;   // 빌려 쓰는 것이다
            _hostCanvas = canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;

            PrepareView();

            Debug.Log($"[GameStatusPanel] 씬에 배치된 '{candidate.name}'을 사용합니다.");
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

    /// <summary>
    /// 프리팹을 씬 캔버스 아래에 찍고 참조를 연결한다.
    ///
    /// ★ 씬 캔버스의 자식으로 두어야 CanvasScaler를 물려받아 글자 크기가 보드와 맞는다.
    /// </summary>
    private void BuildFromPrefab(Canvas canvas)
    {
        GameObject prefab = Resources.Load<GameObject>(PanelResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[GameStatusPanel] 프리팹을 찾지 못했습니다: Resources/{PanelResourcePath}. " +
                "턴 표시·진행 로그·결과 화면이 모두 나오지 않습니다.");
            return;
        }

        _root = Instantiate(prefab, canvas.transform);
        _root.name = prefab.name;   // (Clone) 꼬리표 제거

        _view = _root.GetComponent<GameStatusPanelView>()
                ?? _root.GetComponentInChildren<GameStatusPanelView>(true);

        if (_view == null)
        {
            Debug.LogError(
                $"[GameStatusPanel] '{prefab.name}'에 GameStatusPanelView가 없습니다. " +
                "프리팹 루트에 컴포넌트를 붙여 주세요.");
            Destroy(_root);
            _root = null;
            return;
        }

        if (!_view.Validate(out string reason))
        {
            Debug.LogError($"[GameStatusPanel] 프리팹 참조가 온전하지 않습니다 — {reason}");
            Destroy(_root);
            _root = null;
            _view = null;
            return;
        }

        _ownsRoot = true;
        PrepareView();
    }

    /// <summary>
    /// 찍었든 빌려 왔든, 쓰기 전에 똑같이 해 두어야 하는 것들.
    ///
    /// ★ 씬에 놓인 것은 편집하기 좋도록 결과 오버레이가 켜진 채 저장돼 있을 수 있다.
    ///   여기서 꺼 주지 않으면 화면 진입부터 딤이 덮인다.
    /// </summary>
    private void PrepareView()
    {
        WireButtons();

        if (_view.overlay != null) _view.overlay.SetActive(false);
        if (_view.header != null) _view.header.SetActive(true);

        // 씬을 다시 들어왔을 때 지난 판의 로그가 남아 있지 않도록 비운다.
        if (_view.logText != null) _view.logText.text = string.Empty;
    }

    /// <summary>
    /// 프리팹 버튼에 동작을 건다.
    /// 인스펙터에 남아 있을지 모를 배선과 겹치지 않도록 먼저 비운다.
    /// </summary>
    private void WireButtons()
    {
        if (_view.retryButton != null)
        {
            _view.retryButton.onClick.RemoveAllListeners();
            _view.retryButton.onClick.AddListener(RestartMatch);
        }

        if (_view.mainMenuButton != null)
        {
            _view.mainMenuButton.onClick.RemoveAllListeners();
            _view.mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
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
}
