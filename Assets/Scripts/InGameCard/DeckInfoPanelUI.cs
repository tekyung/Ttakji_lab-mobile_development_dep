// DeckInfoPanelUI.cs — 좌측 상단 톱니바퀴 → 덱 리스트 + 항복 패널
//
//   · 화면 좌측 상단에 톱니바퀴 버튼. 누르면 패널이 열린다 (다시 누르면 닫힘)
//   · 패널 안에 내 덱 20장을 5 × 4 격자로 표시. 덱에 남아 있지 않은 카드는 흐리게 처리한다
//   · 격자 아래, 패널 우측에 [항복] 버튼. 오조작 방지를 위해 한 번 더 확인을 받는다
//
// 덱 구성은 OnGameStart 시점에 스냅샷한다. 이 이벤트는 엔진의 시작 드로우보다 먼저 발행되므로
// (InitializeSingleGame: OnGameStart → CharacterFieldBroadcast → DrawCards 순서)
// 그 시점의 player.Deck이 곧 온전한 20장이다. 엔진에 별도 API를 뚫지 않아도 된다.
//
// ── 화면은 프리팹이 갖는다 ────────────────────────────────────────────
//   프리팹 — 위치·크기·앵커·격자 배치(칸 크기·간격·열 수)·글꼴·기본 색·톱니 스프라이트
//   코드   — 제목 문구 · 열고 닫기 · 격자에 채울 카드 · 항복 확인 단계의 문구와 색
//
//   Resources/Build/DeckInfoPanelRoot — 톱니바퀴 + 패널
//   Resources/Build/DeckCellItem      — 격자에 늘어놓을 카드 한 칸
//
//   씬에 미리 놓여 있으면 그것을 쓰고, 없으면 프리팹을 찍는다.
//   로직 싱글턴은 씬을 넘어 살아남지만 화면은 씬 캔버스에 붙으므로 수명이 다르다.
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeckInfoPanelUI : MonoBehaviour
{
    public static DeckInfoPanelUI Instance { get; private set; }

    /// <summary>Resources 아래에서 톱니바퀴 + 패널을 찾을 경로.</summary>
    private const string PanelResourcePath = "Build/DeckInfoPanelRoot";

    /// <summary>Resources 아래에서 카드 한 칸 템플릿을 찾을 경로.</summary>
    private const string CellResourcePath = "Build/DeckCellItem";

    [Header("상태에 따라 코드가 바꾸는 값")]
    // 칸 크기·간격·열 수·축소 배율·패널 색은 프리팹이 갖는다.
    // 여기 남은 것은 **상황**에 따라 달라지는 것뿐이다.
    [Range(0f, 1f)]
    [Tooltip("덱에 남아 있지 않은 카드를 얼마나 흐리게 보여 줄지.")]
    public float usedCardAlpha = 0.28f;

    [Tooltip("[항복] 버튼의 평소 색.")]
    public Color surrenderColor = new Color(0.80f, 0.25f, 0.25f, 1f);

    [Tooltip("[관전 종료]로 나갈 씬 이름. Build Settings에 있어야 한다.")]
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("한 번 눌러 '정말 항복?'이 된 상태의 색.")]
    public Color surrenderConfirmColor = new Color(0.95f, 0.35f, 0.20f, 1f);

    private Canvas _hostCanvas;

    private GameObject _root;
    private DeckInfoPanelView _view;

    /// <summary>내가 찍은 것인가. 씬에서 빌려 온 것은 절대 파괴하지 않는다.</summary>
    private bool _ownsRoot;

    private GameObject _cellTemplate;

    private Player _human;
    private readonly List<Card> _deckSnapshot = new List<Card>();
    private readonly List<GameObject> _cells = new List<GameObject>();
    private bool _surrenderArmed;

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("DeckInfoPanelUI");
        Instance = go.AddComponent<DeckInfoPanelUI>();
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
        EventManager.OnGameSet += HandleGameEnd;

        // ★ 씬이 로드되면 **게임 시작을 기다리지 않고** 곧바로 화면을 정리한다.
        //   씬에 놓인 UI는 편집하기 좋도록 켜진 채 저장되므로,
        //   여기서 꺼 주지 않으면 진입하자마자 패널이 화면을 가로막는다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(AdoptSceneRootSoon());
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AdoptSceneRootSoon());

    /// <summary>
    /// 한 프레임 기다렸다가 씬에 놓인 패널을 거둔다.
    /// 씬 로드 직후에는 캔버스·레이아웃이 아직 자리를 잡지 않았다.
    /// </summary>
    private IEnumerator AdoptSceneRootSoon()
    {
        yield return null;

        // 씬에 없으면 아무 일도 하지 않는다 — 로비처럼 이 패널이 없는 화면도 있다.
        // 실제로 필요한 순간(EnsureUI)에 프리팹을 찍으면 된다.
        AdoptSceneRootIfPresent();
    }

    // ─── 게임 흐름 ──────────────────────────────────────────────────────

    /// <summary>
    /// 관전 중인가 — <b>봇 vs 봇</b>이다.
    ///
    /// ★ 관전에는 조작 주체가 없어 <c>_human</c>이 null이고, 그래서 예전에는
    ///   톱니바퀴가 아예 뜨지 않았다. 패널을 열 수 없으니 <b>중간에 나갈 방법도 없었다.</b>
    ///   온라인은 항상 조작 주체가 있으므로 여기 해당하지 않는다.
    /// </summary>
    private bool _spectating;

    /// <summary>관전 중 덱 격자에 보여 줄 플레이어(아래 보드 주인).</summary>
    private Player _spectateSubject;

    /// <summary>격자·제목이 다룰 플레이어. 대전이면 나, 관전이면 아래 보드.</summary>
    private Player DeckSubject => _human ?? _spectateSubject;

    private void HandleGameStart(Player p1, Player p2)
    {
        _human = PlayerUIManager.ResolveHumanPlayer(p1, p2);

        _spectating = _human == null && !OnlineMatchStarter.IsOnlineSessionActive;
        _spectateSubject = _spectating ? p1 : null;

        // 시작 드로우 이전이라 이 시점의 Deck이 온전한 덱 구성이다
        _deckSnapshot.Clear();
        if (DeckSubject != null) _deckSnapshot.AddRange(DeckSubject.Deck);

        EnsureUI();
        SetGearVisible(DeckSubject != null);
        ClosePanel();
        WireLegacySurrenderButton();
    }

    /// <summary>
    /// 씬에 남아 있던 기존 [Surrender] 버튼을 이 패널에 연결한다.
    /// 코드 참조가 없어 눌러도 아무 일도 일어나지 않던 죽은 버튼이었다.
    /// 항복 확인 절차를 한 곳으로 모으기 위해, 바로 항복시키지 않고 이 패널을 연다.
    /// </summary>
    private void WireLegacySurrenderButton()
    {
        Button button = FindSceneButtonByName("Surrender");
        if (button == null || button == _legacySurrenderButton) return;

        // 씬을 다시 로드하면 버튼 인스턴스가 새로 생기므로, 참조가 바뀔 때마다 다시 연결한다.
        // (이 컴포넌트는 DontDestroyOnLoad라 bool 플래그로 막으면 재로드 후 연결되지 않는다)
        button.onClick.RemoveListener(OpenPanel);
        button.onClick.AddListener(OpenPanel);
        _legacySurrenderButton = button;
    }

    /// <summary>
    /// 이름으로 씬의 버튼을 찾는다. <b>꺼져 있는 것까지 본다.</b>
    ///
    /// ★ 예전에는 GameObject.Find를 썼는데, 그 함수는 계층에서 <b>켜져 있는</b> 오브젝트만 찾는다.
    ///   문제의 [항복] 버튼은 평소 닫혀 있는 '설정' 패널 안에 들어 있어서 한 번도 찾히지 않았고,
    ///   그래서 눌러도 아무 일이 없었다. (버튼 자신은 켜져 있지만 부모가 꺼져 있다)
    /// </summary>
    private static Button FindSceneButtonByName(string targetName)
    {
        foreach (Button candidate in FindObjectsByType<Button>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.name == targetName) return candidate;
        }

        return null;
    }

    private Button _legacySurrenderButton;

    private void HandleGameEnd(Player winner)
    {
        ClosePanel();
        SetGearVisible(false);
    }

    private void SetGearVisible(bool visible)
    {
        if (_view != null && _view.gearButton != null)
            _view.gearButton.gameObject.SetActive(visible);
    }

    // ─── 패널 열기 / 닫기 ───────────────────────────────────────────────

    private void TogglePanel()
    {
        if (_view == null || _view.panel == null) return;

        if (_view.panel.activeSelf) ClosePanel();
        else OpenPanel();
    }

    private void OpenPanel()
    {
        EnsureUI();
        if (_view == null || _view.panel == null) return;

        DisarmSurrender();
        RebuildGrid();
        _view.panel.SetActive(true);

        // 패널은 자기 Canvas(정렬 700)를 갖고 있어 형제 순서와 무관하게 앞에 뜨지만,
        // 정렬을 끄고 쓰는 경우까지 생각해 앞으로 끌어 둔다.
        if (_root != null) _root.transform.SetAsLastSibling();
    }

    private void ClosePanel()
    {
        DisarmSurrender();

        // 칸을 눌러 띄운 좌측 확대는 손패 호버가 닫아 주지 않는다(_subFromHand가 아니다).
        // 목록을 닫는데 확대만 남아 있으면 어색하므로 여기서 함께 거둔다.
        if (CardZoomPopupUI.Instance != null) CardZoomPopupUI.Instance.CloseSub();

        if (_view != null && _view.panel != null) _view.panel.SetActive(false);
    }

    // ─── 덱 격자 ────────────────────────────────────────────────────────

    private void RebuildGrid()
    {
        foreach (var go in _cells) if (go != null) Destroy(go);
        _cells.Clear();

        Player subject = DeckSubject;
        if (subject == null || _view == null || _view.grid == null) return;

        // 덱에 아직 남아 있는 수를 카드 ID별로 센다 (같은 카드가 2장이므로 개수 기반으로 판정)
        var remaining = new Dictionary<string, int>();
        foreach (var card in subject.Deck)
        {
            if (card == null) continue;
            remaining.TryGetValue(card.Id, out int n);
            remaining[card.Id] = n + 1;
        }

        int shown = 0;
        foreach (var card in _deckSnapshot)
        {
            if (card == null) continue;

            bool stillInDeck = false;
            if (remaining.TryGetValue(card.Id, out int left) && left > 0)
            {
                remaining[card.Id] = left - 1;
                stillInDeck = true;
            }

            GameObject cell = CreateCell(card, stillInDeck);
            if (cell != null) _cells.Add(cell);
            shown++;
        }

        if (_view.titleText != null)
            _view.titleText.text = _spectating
                ? $"아래 보드 덱  ({subject.Deck.Count} / {shown}장 남음)"
                : $"내 덱  ({subject.Deck.Count} / {shown}장 남음)";
    }

    /// <summary>
    /// 카드 한 장을 격자에 올린다.
    ///
    /// 칸의 모양(크기·카드 축소 배율)은 <b>프리팹이 정한다.</b>
    /// 코드는 어떤 카드를 넣을지와, 덱에 남아 있는지만 다룬다.
    /// </summary>
    private GameObject CreateCell(Card card, bool stillInDeck)
    {
        GameObject template = ResolveCellTemplate();
        if (template == null) return null;

        GameObject cellGo = Instantiate(template, _view.grid);
        cellGo.name = $"Cell_{card.Id}";
        cellGo.SetActive(true);

        var cellView = cellGo.GetComponent<DeckCellItemView>();
        if (cellView == null)
        {
            Debug.LogError("[DeckInfoPanel] 카드 칸 템플릿에 DeckCellItemView가 없습니다.");
            Destroy(cellGo);
            return null;
        }

        if (!cellView.Validate(out string reason))
        {
            Debug.LogError($"[DeckInfoPanel] 카드 칸 템플릿이 온전하지 않습니다 — {reason}");
            Destroy(cellGo);
            return null;
        }

        cellView.canvasGroup.alpha = stillInDeck ? 1f : usedCardAlpha;   // 덱에 없는 카드는 흐리게

        GameObject prefab = ResolveCardPrefab();
        if (prefab == null) return cellGo;

        GameObject cardGo = Instantiate(prefab, cellView.cardHost);
        var ui = cardGo.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.BindEngineCard(card);
            ui.SetFaceDown(false);
        }

        var interaction = cardGo.GetComponent<CardInteraction>();
        if (interaction != null) interaction.enabled = false;

        // 자리에 딱 맞춰 놓기만 한다. 축소는 CardHost의 localScale이 이미 걸고 있다.
        var cardRect = cardGo.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one;
        }

        // 카드 안쪽 그래픽은 클릭을 가로채지 않는다 — 칸의 Button이 받아야 한다.
        foreach (var g in cardGo.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        // 칸을 누르면 좌측에 크게 보여 준다. 폐기존 목록이 쓰는 것과 같은 서브 팝업이다.
        if (cellView.button != null)
        {
            Card captured = card;
            cellView.button.onClick.RemoveAllListeners();
            cellView.button.onClick.AddListener(() =>
            {
                if (CardZoomPopupUI.Instance != null) CardZoomPopupUI.Instance.ShowSub(captured);
            });
        }

        return cellGo;
    }

    /// <summary>카드 칸 템플릿을 한 번만 불러 둔다.</summary>
    private GameObject ResolveCellTemplate()
    {
        if (_cellTemplate != null) return _cellTemplate;

        _cellTemplate = Resources.Load<GameObject>(CellResourcePath);

        if (_cellTemplate == null)
        {
            Debug.LogError(
                $"[DeckInfoPanel] 카드 칸 템플릿을 찾지 못했습니다: Resources/{CellResourcePath}. " +
                "덱 리스트를 그릴 수 없습니다.");
        }

        return _cellTemplate;
    }

    private static GameObject ResolveCardPrefab()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myCardPrefab != null)
            return PlayerUIManager.Instance.myCardPrefab;

        return Resources.Load<GameObject>("Build/CardSlotInGame");
    }

    // ─── 항복 ───────────────────────────────────────────────────────────

    private void OnSurrenderClicked()
    {
        if (_view == null) return;

        if (!_surrenderArmed)
        {
            // 1차 클릭: 확인 요청 (오조작 방지)
            _surrenderArmed = true;
            if (_view.surrenderLabel != null)
                _view.surrenderLabel.text = _spectating ? "정말 나갈까요?" : "정말 항복?";
            if (_view.surrenderImage != null) _view.surrenderImage.color = surrenderConfirmColor;
            return;
        }

        // 2차 클릭
        ClosePanel();

        if (_spectating)
        {
            LeaveSpectating();
            return;
        }

        Surrender();
    }

    /// <summary>
    /// 항복 처리. 로컬과 온라인은 게임을 끝내는 주체가 다르다.
    ///
    /// ★ 예전엔 무조건 <c>BattleManager.SurrenderBy</c>를 불렀는데,
    ///   온라인에서는 BattleManager가 매치를 돌리지 않아 context가 null이라
    ///   그 메서드가 첫 줄에서 그냥 돌아왔다. 항복 버튼 둘이 모두 먹통이었던 이유다.
    /// </summary>
    private void Surrender()
    {
        if (_human == null)
        {
            Debug.LogWarning("[DeckInfoPanel] 항복할 플레이어를 찾지 못했다.");
            return;
        }

        if (!OnlineMatchStarter.IsOnlineSessionActive)
        {
            if (BattleManager.Instance != null) BattleManager.Instance.SurrenderBy(_human);
            return;
        }

        // 온라인: 호스트는 자기 엔진을 직접 끝내고, 게스트는 호스트에게 알린다.
        if (GameData.MyRole == "HOST")
        {
            if (ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.SurrenderBy(_human);
                return;
            }

            Debug.LogWarning("[DeckInfoPanel] ServerGameManager를 찾지 못해 항복하지 못했다.");
            return;
        }

        var client = FindFirstObjectByType<session_game_manage>(FindObjectsInactive.Include);
        if (client != null)
        {
            client.SendSurrender();
            return;
        }

        Debug.LogWarning("[DeckInfoPanel] 세션 매니저를 찾지 못해 항복을 보내지 못했다.");
    }

    /// <summary>
    /// 관전을 끝내고 메인 메뉴로 돌아간다.
    ///
    /// 봇 vs 봇에는 항복할 주체가 없으므로 <b>게임을 끝내는 것이 아니라 화면을 떠난다.</b>
    /// 씬을 새로 불러오면 보드도 엔진도 함께 정리된다.
    /// </summary>
    private void LeaveSpectating()
    {
        string scene = string.IsNullOrWhiteSpace(mainMenuSceneName) ? "MainMenu" : mainMenuSceneName;

        Debug.Log($"[DeckInfoPanel] 관전을 끝내고 '{scene}' 으로 돌아갑니다.");
        SceneManager.LoadScene(scene);
    }

    private void DisarmSurrender()
    {
        _surrenderArmed = false;
        if (_view == null) return;

        if (_view.surrenderLabel != null)
            _view.surrenderLabel.text = _spectating ? "관전 종료" : "항복";
        if (_view.surrenderImage != null) _view.surrenderImage.color = surrenderColor;
    }

    // ─── 화면 마련 ──────────────────────────────────────────────────────

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
        //    이 검사가 없으면 씬에 배치한 것 위에 하나를 더 찍어 **둘이 겹친다.**
        //    (CharacterSlotBar·CharacterPicker·HumanChoiceDialog에서 똑같이 겪은 문제다)
        if (AdoptSceneRootIfPresent()) return;

        // ② 없으면 프리팹을 찍는다
        BuildFromPrefab(canvas);
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
            var c = PlayerUIManager.Instance.myHandTransform.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }
        return FindFirstObjectByType<Canvas>();
    }

    /// <summary>
    /// 씬에 미리 놓인 패널을 찾아 연결한다. 위치·크기는 손대지 않는다.
    ///
    /// 씬 로드 직후에도 부르고(화면을 곧바로 정리하기 위해),
    /// 필요해졌을 때도 부른다(그때까지 없었을 수도 있으므로).
    /// </summary>
    private bool AdoptSceneRootIfPresent()
    {
        // 이미 우리가 찍은 것을 쓰고 있으면 그대로 둔다.
        // (씬이 바뀌면 그것은 씬과 함께 사라져 _view가 null이 되므로 자연스럽게 다시 찾는다)
        if (_ownsRoot && _view != null) return true;

        foreach (DeckInfoPanelView candidate in FindObjectsByType<DeckInfoPanelView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate == _view) return true;   // 이미 쓰고 있다

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning(
                    $"[DeckInfoPanel] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뜁니다 — {reason}");
                continue;
            }

            // ★ 꺼진 부모 아래에 있으면 무슨 짓을 해도 화면에 나오지 않는다.
            //   실제로 이 프리팹이 선택 다이얼로그의 패널(평소 꺼져 있다) 안으로 들어간 적이 있고,
            //   그때 톱니바퀴가 끝내 뜨지 않아 항복조차 할 수 없었다.
            //   조용히 안 보이는 것보다 시끄럽게 알리고 우리 것을 찍는 편이 낫다.
            if (FindInactiveAncestor(candidate.transform) is Transform blocker)
            {
                Debug.LogError(
                    $"[DeckInfoPanel] 씬의 '{candidate.name}'은 꺼져 있는 '{blocker.name}' 아래에 있어 화면에 뜰 수 없습니다. " +
                    "캔버스 바로 아래로 옮기거나 씬에서 지우세요. 지금은 프리팹을 새로 찍어 씁니다.");
                continue;
            }

            Canvas canvas = candidate.GetComponentInParent<Canvas>();

            _root = candidate.gameObject;
            _view = candidate;
            _ownsRoot = false;   // 빌려 쓰는 것이다
            _hostCanvas = canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;

            PrepareView();

            Debug.Log($"[DeckInfoPanel] 씬에 배치된 '{candidate.name}'을 사용합니다.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 프리팹을 씬 캔버스 아래에 찍고 참조를 연결한다.
    ///
    /// ★ 씬 캔버스의 자식으로 두는 것이 중요하다 — CanvasScaler를 물려받아야
    ///   카드 크기가 보드와 같은 비율로 보인다.
    /// </summary>
    private void BuildFromPrefab(Canvas canvas)
    {
        GameObject prefab = Resources.Load<GameObject>(PanelResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[DeckInfoPanel] 프리팹을 찾지 못했습니다: Resources/{PanelResourcePath}. " +
                "덱 리스트와 항복 버튼을 쓸 수 없습니다.");
            return;
        }

        _root = Instantiate(prefab, canvas.transform);
        _root.name = prefab.name;   // (Clone) 꼬리표 제거

        _view = _root.GetComponent<DeckInfoPanelView>()
                ?? _root.GetComponentInChildren<DeckInfoPanelView>(true);

        if (_view == null)
        {
            Debug.LogError(
                $"[DeckInfoPanel] '{prefab.name}'에 DeckInfoPanelView가 없습니다. " +
                "프리팹 루트에 컴포넌트를 붙여 주세요.");
            Destroy(_root);
            _root = null;
            return;
        }

        if (!_view.Validate(out string reason))
        {
            Debug.LogError($"[DeckInfoPanel] 프리팹 참조가 온전하지 않습니다 — {reason}");
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
    /// ★ 씬에 놓인 것은 편집하기 좋도록 **켜진 채 저장돼 있다.**
    ///   여기서 꺼 주지 않으면 화면 진입부터 계속 떠 있게 된다.
    /// </summary>
    private void PrepareView()
    {
        WireButtons();
        DisarmSurrender();

        if (_view.panel != null) _view.panel.SetActive(false);

        // 톱니바퀴는 사람이 조작하는 판에서만 쓴다. HandleGameStart가 다시 켠다.
        if (_view.gearButton != null) _view.gearButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 프리팹 버튼에 동작을 건다.
    /// 인스펙터에 남아 있을지 모를 배선과 겹치지 않도록 먼저 비운다.
    /// </summary>
    private void WireButtons()
    {
        if (_view.gearButton != null)
        {
            _view.gearButton.onClick.RemoveAllListeners();
            _view.gearButton.onClick.AddListener(TogglePanel);
        }

        if (_view.closeButton != null)
        {
            _view.closeButton.onClick.RemoveAllListeners();
            _view.closeButton.onClick.AddListener(ClosePanel);
        }

        if (_view.surrenderButton != null)
        {
            _view.surrenderButton.onClick.RemoveAllListeners();
            _view.surrenderButton.onClick.AddListener(OnSurrenderClicked);
        }
    }
}
