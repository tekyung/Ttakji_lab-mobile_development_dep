// CardZoomPopupUI.cs — 공개 정보 카드 확대 보기 + 폐기존 목록 패널
//
//   · 클릭하면 확대되는 대상: 양측 용병 카드 / 스택 카드 / 세트존 카드(앞면 한정) / 폐기존 목록의 카드
//   · 폐기존을 클릭하면 화면 오른쪽에 세로로 긴 패널이 열리고, 버려진 순서대로 세로 스크롤로 볼 수 있다
//     (왼쪽은 GameStatusPanelUI의 진행 로그가 쓰므로 겹치지 않도록 오른쪽에 둔다)
//   · 확대 이미지는 preserveAspect로 원본 비율을 유지한 채 크기만 키운다
//
// 손패 카드는 호버·클릭 시 서브 팝업(왼쪽)이 뜨고(CardInteraction), 위로 드래그하면 InGameUIManager의 중앙 줌이 뜬다.
//
// 클릭 감지는 EventSystem 레이캐스트로 직접 처리한다.
// 카드 GO에 컴포넌트를 붙이거나 CardBoardRegistry를 고치지 않아도 되므로 기존 코드를 건드리지 않는다.
//
// ── 화면은 프리팹이 갖는다 ────────────────────────────────────────────
//   프리팹 — 위치·크기·앵커·글꼴·바탕색·정렬 순서
//   코드   — 어떤 카드를 보여 줄지 · 열고 닫기 · 목록 채우기 · 제목 문구
//
//   Resources/Build/CardZoomPopupRoot     서브 팝업(780) · 확대 팝업(850) · 폐기존 패널(800)
//   Resources/Build/GraveyardEntryItem    폐기존 목록 한 줄
//
// ★ 예전에는 서브 팝업과 확대 상자의 크기를 실행 중에 캔버스 높이로 계산했다.
//   이제 프리팹에 적힌 크기를 그대로 쓴다 — 해상도는 CanvasScaler가 맡는다.
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CardZoomPopupUI : MonoBehaviour
{
    public static CardZoomPopupUI Instance { get; private set; }

    /// <summary>Resources 아래에서 세 화면을 찾을 경로.</summary>
    private const string PopupResourcePath = "Build/CardZoomPopupRoot";

    /// <summary>Resources 아래에서 폐기존 목록 한 줄 템플릿을 찾을 경로.</summary>
    private const string GraveEntryResourcePath = "Build/GraveyardEntryItem";

    // 크기·색·자리는 전부 프리팹이 갖는다. 인스펙터에 남길 값이 없다.

    // ─── 상태 ───────────────────────────────────────────────────────────
    private Canvas _hostCanvas;

    private GameObject _root;
    private CardZoomPopupView _view;

    /// <summary>내가 찍은 것인가. 씬에서 빌려 온 것은 절대 파괴하지 않는다.</summary>
    private bool _ownsRoot;

    private GameObject _graveEntryTemplate;

    private Player _p1;
    private Player _p2;
    private readonly Dictionary<string, string> _characterSlotIds = new Dictionary<string, string>();

    // 확대 팝업 (중앙, 모달)
    private GameObject _zoomRoot;
    private Image _zoomImage;
    private TextMeshProUGUI _zoomName;
    private TextMeshProUGUI _zoomDesc;

    // 서브 팝업 (화면 왼쪽 중앙, 비모달)
    private GameObject _subRoot;
    private Image _subImage;
    private Card _subCard;
    private bool _subFromHand; // 손패 호버로 띄운 것인지 (커서가 손패를 벗어나면 이때만 닫는다)

    // 폐기존 패널
    private GameObject _graveRoot;
    private RectTransform _graveContent;
    private TextMeshProUGUI _graveTitle;
    private readonly List<GameObject> _graveEntries = new List<GameObject>();
    private Player _graveOwner;

    // 씬의 용병 슬롯 이미지 이름 → 어느 플레이어의 어느 슬롯인지
    private static readonly string[] CharacterSlotNames =
        { "character1", "character2", "EnemyCharacter1", "EnemyCharacter2" };

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("CardZoomPopupUI");
        Instance = go.AddComponent<CardZoomPopupUI>();
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
        EventManager.OnCharacterFieldSync += HandleFieldSync;
        EventManager.OnCharacterSlotUpdated += HandleSlotUpdated;

        // ★ 씬이 로드되면 게임 시작을 기다리지 않고 곧바로 화면을 정리한다.
        //   씬에 놓인 것은 편집하기 좋도록 켜진 채 저장될 수 있어, 여기서 꺼 주지 않으면
        //   진입하자마자 딤이나 폐기존 패널이 화면을 덮는다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(AdoptSceneRootSoon());
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;
        EventManager.OnCharacterFieldSync -= HandleFieldSync;
        EventManager.OnCharacterSlotUpdated -= HandleSlotUpdated;

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
        AdoptSceneRootIfPresent();
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
        _hoveredHandInstanceId = null;
        _focusWarned = false;
        EnsureUI();
        CloseAll();
    }

    private void HandleGameEnd(Player winner) => CloseAll();

    private void HandleFieldSync(IReadOnlyList<CharacterSlotSnapshot> snapshots)
    {
        if (snapshots == null) return;
        foreach (var s in snapshots) CacheSlot(s);
    }

    private void HandleSlotUpdated(CharacterSlotSnapshot snapshot) => CacheSlot(snapshot);

    private void CacheSlot(CharacterSlotSnapshot s)
    {
        if (s.Owner == null || string.IsNullOrEmpty(s.CharacterCardId)) return;

        // CharacterFieldUI와 동일한 규칙: 하단(사람 기준 P1) = character1/2, 상단 = EnemyCharacter1/2
        bool isBottom = ReferenceEquals(s.Owner, _p1);
        string slotName = s.Slot == CharacterSlotType.Main
            ? (isBottom ? "character1" : "EnemyCharacter1")
            : (isBottom ? "character2" : "EnemyCharacter2");

        _characterSlotIds[slotName] = s.CharacterCardId;
    }

    // ─── 클릭 감지 ──────────────────────────────────────────────────────

    // ★ EventSystem 레이캐스트를 쓰지 않고 Rect 포함 판정으로 직접 찾는다.
    //   CardBoardRegistry가 상호작용이 필요 없는 존(스택·폐기·전장·덱)의 레이캐스트를
    //   의도적으로 꺼 두기 때문에(SetRaycast(..., false)), 레이캐스트로는 그 카드들을 집을 수 없다.
    //   레이캐스트 설정을 되살리면 드래그·클릭 동작이 엉키므로, 감지 쪽을 좌표 판정으로 바꾼다.
    private void Update()
    {
        UpdateHandHover();

        if (!Input.GetMouseButtonDown(0)) return;

        // 확대 팝업이 열려 있으면 그쪽 배경 버튼이 닫기를 처리한다
        if (_zoomRoot != null && _zoomRoot.activeSelf) return;

        EnsureUI();

        Vector2 pos = Input.mousePosition;
        Camera cam = ResolveEventCamera();

        // 서브 팝업 위 클릭은 그쪽 버튼이 중앙 팝업으로 승격시킨다
        if (_subRoot != null && _subRoot.activeSelf && ContainsPoint(_subRoot.transform, pos, cam))
            return;

        // 손패 미리보기는 여기서 닫지 않는다 — 커서가 손패를 벗어날 때 닫는 것이 맞고,
        // 클릭(마우스 다운) 때 닫으면 방금 누른 카드의 미리보기가 사라진다.
        // 스택·폐기존을 눌러서 띄운 미리보기만 "아무 데나 누르면 닫힘"을 유지한다
        if (!_subFromHand) CloseSub();

        // 내가 만든 폐기존 패널 위 클릭은 그 안의 버튼이 처리하고,
        // 패널 바깥을 클릭하면 [닫기] 버튼 없이도 닫힌다 (닫은 뒤에도 그 클릭은 아래 존 판정으로 이어진다)
        if (_graveRoot != null && _graveRoot.activeSelf)
        {
            if (ContainsPoint(_graveRoot.transform, pos, cam)) return;
            CloseGraveyard();
        }

        // ★ 진짜 UI(버튼)와 손패 카드가 커서 아래 있으면 Rect 판정을 양보한다.
        //   Rect 판정은 레이캐스트를 거치지 않으므로, 양보하지 않으면 손패 카드의
        //   [공개]/[폐기] 버튼을 눌렀을 때 그 뒤에 겹친 용병 슬롯까지 함께 반응한다.
        if (_hoveredHandInstanceId != null || FindHandCardUnder(pos, cam) != null) return;
        if (IsPointerOverInteractiveUI(pos)) return;

        EnemyVisualTester enemy = ResolveEnemyVisual();
        PlayerUIManager mine = PlayerUIManager.Instance;

        // 1) 폐기존 — 카드가 없어도 영역만 누르면 목록을 연다
        if (mine != null && ContainsPoint(mine.myGraveyardTransform, pos, cam))
        {
            OpenGraveyard(ResolveOwnerForMyBoard());
            return;
        }
        if (enemy != null && ContainsPoint(enemy.enemyGraveyardTransform, pos, cam))
        {
            OpenGraveyard(ResolveOwnerForEnemyBoard());
            return;
        }

        // 2) 카드가 놓이는 공개 존들
        if (mine != null)
        {
            if (TryZone(mine.myStackZoneRoot, pos, cam)) return;
            if (TryZone(mine.mySetZoneTransform, pos, cam)) return;
            if (TryZone(mine.myBattlefieldTransform, pos, cam)) return;
        }
        if (enemy != null)
        {
            if (TryZone(enemy.enemyStackZoneRoot, pos, cam)) return;
            if (TryZone(enemy.enemySetZoneTransform, pos, cam)) return;
            if (TryZone(enemy.enemyBattlefieldTransform, pos, cam)) return;
        }

        // 3) 용병 슬롯 — 클릭으로 중앙 팝업을 여는 유일한 대상
        //    (손패는 호버·클릭·드래그 시작에 서브 팝업이 뜬다 — CardInteraction)
        TryCharacterSlots(pos, cam);
    }

    private Camera ResolveEventCamera()
    {
        if (_hostCanvas == null) return null;
        return _hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _hostCanvas.worldCamera;
    }

    // ─── 손패 호버 미리보기 ─────────────────────────────────────────────
    //
    // ★ 커서를 올리기만 해도 왼쪽 서브 팝업에 카드가 뜬다. 클릭이 필요 없다.
    //   띄우는 경로가 둘이다 — 여기(매 프레임 Rect 포함 판정)와 CardInteraction의 IPointerEnterHandler.
    //   이 보드는 카드마다 중첩 Canvas + GraphicRaycaster가 붙고(CardInteraction.Awake)
    //   존마다 레이캐스트를 껐다 켜므로 포인터 이동 이벤트가 카드까지 오지 않는 경우가 있고,
    //   반대로 좌표 판정은 카드 GO를 어디서 찾느냐에 달려 있다. 어느 하나가 죽어도 호버가 살아 있도록 둘 다 둔다.
    //   표시(ShowSubByInstanceId)는 멱등이라 중복 호출해도 안전하다.

    private string _hoveredHandInstanceId;

    private void UpdateHandHover()
    {
        if (_subRoot == null)
        {
            // 매치 전(메뉴 씬)에는 만들지 않는다. 씬 재로드로 날아간 경우에만 다시 만든다
            if (_p1 == null) return;   // 매치 시작 전
            EnsureUI();
            if (_subRoot == null) return;   // 캔버스를 못 찾아 화면을 못 만든 상태
        }

        // 드래그 중엔 집은 카드를 계속 보여 준다
        if (CardInteraction.IsDraggingAny) return;   // 드래그 중엔 집은 카드를 계속 보여 준다

        // 모달이나 손패를 덮는 패널이 열려 있으면 그 아래 카드를 집지 않는다
        if (_zoomRoot != null && _zoomRoot.activeSelf)
        {
            _hoveredHandInstanceId = null;
            return;
        }
        if (HumanChoiceDialogUI.Instance != null && HumanChoiceDialogUI.Instance.IsOpen)
        {
            _hoveredHandInstanceId = null;
            return;
        }

        Vector2 pos = Input.mousePosition;
        Camera cam = ResolveEventCamera();

        if (_graveRoot != null && _graveRoot.activeSelf && ContainsPoint(_graveRoot.transform, pos, cam))
        {
            _hoveredHandInstanceId = null;
            return;
        }

        // ★ 여기가 4번 헤맨 자리다.
        //   StandaloneInputModule은 UpdateModule()/Process() 첫 줄에서
        //   `!eventSystem.isFocused` 이면 통째로 돌아간다(uGUI 2.0.0 기준).
        //   그러면 포인터 이벤트도, 커서 좌표 갱신도 멈춘다 —
        //   Update()는 runInBackground=1 덕에 계속 돌지만 판정할 좌표가 낡은 것이다.
        //   마우스를 누르면 포커스가 돌아와 그때만 호버가 살아난 것이 이 증상의 정체다.
        if (EventSystem.current != null && !EventSystem.current.isFocused)
        {
            if (!_focusWarned)
            {
                _focusWarned = true;
                Debug.Log("[CardZoomPopupUI] 게임 화면에 포커스가 없어 커서 좌표가 멈춰 있습니다. 화면을 한 번 클릭하세요.");
            }

            return;
        }

        _focusWarned = false;

        string hovered = FindHandCardUnder(pos, cam);
        if (hovered == _hoveredHandInstanceId) return; // 같은 카드 위 = 할 일 없음

        _hoveredHandInstanceId = hovered;

        if (hovered != null) ShowSubByInstanceId(hovered);
        else if (_subFromHand) CloseSub(); // 손패에서 띄운 것만 닫는다 (스택·폐기 열람은 유지)
    }

    /// <summary>
    /// 포커스가 없다고 이미 알렸는가. 매 프레임 찍지 않으려고 둔다.
    ///
    /// ★ 이 안내만 남긴 이유: 에디터에서 Game 뷰에 포커스가 없으면 커서 좌표가 갱신되지 않아
    ///   호버가 통째로 죽는다. 원인을 찾는 데 다섯 번을 헤맸으므로, 다음 사람은
    ///   침묵 대신 이 한 줄을 보게 한다. (빌드에서는 창이 포커스를 가지므로 뜨지 않는다)
    /// </summary>
    private bool _focusWarned;

    /// <summary>
    /// 커서 아래에 있는 손패 카드의 InstanceId. 없으면 null.
    /// 감지 수단이 둘이다 — ① EventSystem 레이캐스트 ② 좌표(Rect) 판정.
    /// ①이 우선인 이유: <b>클릭이 먹힌다는 것은 그 좌표에서 레이캐스트가 카드를 잡는다는 뜻</b>이므로
    /// 커서 위치 판정으로 가장 믿을 만하다. ②는 레이캐스트가 꺼진 경우를 위한 보루다.
    /// </summary>
    private string FindHandCardUnder(Vector2 pos, Camera cam)
    {
        Player human = ResolveOwnerForMyBoard();
        if (human == null) return null;

        string byRaycast = FindHandCardByRaycast(pos, human);
        if (byRaycast != null) return byRaycast;

        return FindHandCardByRect(pos, cam, human);
    }

    /// <summary>커서 아래 맨 위 카드가 내 손패 카드면 그 InstanceId.</summary>
    private string FindHandCardByRaycast(Vector2 pos, Player human)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return null;

        var data = new PointerEventData(es) { position = pos };
        RaycastBuffer.Clear();
        es.RaycastAll(data, RaycastBuffer);

        for (int i = 0; i < RaycastBuffer.Count; i++)
        {
            var ui = RaycastBuffer[i].gameObject.GetComponentInParent<CardUI>();
            if (ui == null || string.IsNullOrEmpty(ui.myInstanceId)) continue;

            // 맨 위 카드 하나만 본다. 그 아래 카드는 가려져 있다
            return IsInHand(human, ui.myInstanceId) ? ui.myInstanceId : null;
        }

        return null;
    }

    /// <summary>
    /// 엔진 손패(SSOT) → CardBoardRegistry GO 순으로 훑어 좌표로 판정한다.
    /// 씬의 손패 부모가 무엇인지 몰라도 동작한다.
    /// </summary>
    private string FindHandCardByRect(Vector2 pos, Camera cam, Player human)
    {
        string topmost = null;
        int topSibling = int.MinValue;

        foreach (var card in human.Hand)
        {
            if (card == null || string.IsNullOrEmpty(card.InstanceId)) continue;
            if (!CardBoardRegistry.TryGet(card.InstanceId, out GameObject go) || go == null) continue;
            if (!ContainsPoint(go.transform, pos, cam)) continue;

            // 선택된 카드는 확대 + 맨 앞 렌더라 형제 순서와 무관하게 우선한다
            var interaction = go.GetComponent<CardInteraction>();
            if (interaction != null && interaction.IsSelected) return card.InstanceId;

            // 겹쳐 있으면 뒤 형제가 위에 그려진다
            int sibling = go.transform.GetSiblingIndex();
            if (sibling > topSibling)
            {
                topSibling = sibling;
                topmost = card.InstanceId;
            }
        }

        return topmost;
    }

    private static bool IsInHand(Player human, string instanceId)
    {
        foreach (var card in human.Hand)
            if (Matches(card, instanceId)) return true;

        return false;
    }

    private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();

    /// <summary>커서 아래 맨 위에 실제로 눌리는 UI 버튼이 있는가.</summary>
    private static bool IsPointerOverInteractiveUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        var data = new PointerEventData(es) { position = screenPos };
        RaycastBuffer.Clear();
        es.RaycastAll(data, RaycastBuffer);
        if (RaycastBuffer.Count == 0) return false;

        // 맨 위 히트만 본다. 그 아래는 가려져 있으므로 클릭을 받지 않는다
        Transform t = RaycastBuffer[0].gameObject.transform;
        while (t != null)
        {
            var selectable = t.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable()) return true;

            // 카드 본체까지 올라왔으면 버튼이 아니다 — 존 Rect 판정에 맡긴다
            if (t.GetComponent<CardUI>() != null) return false;

            t = t.parent;
        }

        return false;
    }

    private static bool ContainsPoint(Transform t, Vector2 screenPos, Camera cam)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;

        var rect = t as RectTransform;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }

    /// <summary>존 안에서 클릭 지점에 걸린 카드를 찾아 공개 규칙에 맞게 처리한다.</summary>
    private bool TryZone(Transform zoneRoot, Vector2 pos, Camera cam)
    {
        if (zoneRoot == null || !zoneRoot.gameObject.activeInHierarchy) return false;

        // 나중에 그려진(= 위에 있는) 카드부터 검사한다
        for (int i = zoneRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = zoneRoot.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            var ui = child.GetComponent<CardUI>();
            if (ui == null || string.IsNullOrEmpty(ui.myInstanceId)) continue;
            if (!ContainsPoint(child, pos, cam)) continue;

            return TryShowByInstanceId(ui.myInstanceId);
        }

        return false;
    }

    /// <summary>InstanceId로 엔진 상태를 조회해, 공개된 카드일 때만 확대한다.</summary>
    private bool TryShowByInstanceId(string instanceId)
    {
        foreach (var owner in new[] { _p1, _p2 })
        {
            if (owner == null) continue;

            // 스택존 — 항상 공개 정보. 중앙 모달이 아니라 서브 팝업으로 보여 준다
            foreach (var c in owner.StackZone)
                if (Matches(c, instanceId)) { ShowSub(c); return true; }

            // 세트존 — 앞면일 때만 공개
            if (Matches(owner.SetZoneCard, instanceId))
            {
                if (owner.SetZoneCard.IsFaceUp) { ShowSub(owner.SetZoneCard); return true; }
                return true; // 뒷면이면 아무것도 열지 않되, 뒤쪽 존까지 훑지는 않는다
            }

            // 전장존 — 공개 정보
            if (Matches(owner.BattlefieldCard, instanceId)) { ShowSub(owner.BattlefieldCard); return true; }

            // 폐기존 — 카드 하나가 아니라 목록 패널을 연다
            foreach (var c in owner.Graveyard)
                if (Matches(c, instanceId)) { OpenGraveyard(owner); return true; }
        }

        return false; // 손패·덱 등은 무시 (손패는 기존 드래그 확대를 유지)
    }

    private readonly Dictionary<string, Transform> _characterSlotObjects = new Dictionary<string, Transform>();

    private void TryCharacterSlots(Vector2 pos, Camera cam)
    {
        foreach (var slotName in CharacterSlotNames)
        {
            Transform slot = ResolveCharacterSlot(slotName);
            if (slot == null || !ContainsPoint(slot, pos, cam)) continue;

            if (!_characterSlotIds.TryGetValue(slotName, out string cardId)) return;

            Card card = LookupCard(cardId);
            if (card != null) ShowZoom(card);
            return;
        }
    }

    private Transform ResolveCharacterSlot(string slotName)
    {
        if (_characterSlotObjects.TryGetValue(slotName, out Transform cached) && cached != null)
            return cached;

        GameObject go = GameObject.Find(slotName);
        Transform t = go != null ? go.transform : null;
        _characterSlotObjects[slotName] = t;
        return t;
    }

    private EnemyVisualTester _enemyVisual;

    private EnemyVisualTester ResolveEnemyVisual()
    {
        if (_enemyVisual == null) _enemyVisual = FindFirstObjectByType<EnemyVisualTester>();
        return _enemyVisual;
    }

    /// <summary>내 보드(PlayerUIManager)가 표시 중인 플레이어.</summary>
    private Player ResolveOwnerForMyBoard()
    {
        Player human = PlayerUIManager.ResolveHumanPlayer(_p1, _p2);
        return human != null ? human : _p1; // 봇 vs 봇 관전에서는 P1이 하단
    }

    /// <summary>상대 보드(EnemyVisualTester)가 표시 중인 플레이어.</summary>
    private Player ResolveOwnerForEnemyBoard()
    {
        Player mineOwner = ResolveOwnerForMyBoard();
        return ReferenceEquals(mineOwner, _p1) ? _p2 : _p1;
    }

    private static bool Matches(Card card, string instanceId)
        => card != null && !string.IsNullOrEmpty(card.InstanceId) && card.InstanceId == instanceId;

    private static Card LookupCard(string cardId)
    {
        var dm = BattleManager.Instance != null ? BattleManager.Instance.CardData : null;
        if (dm == null || string.IsNullOrEmpty(cardId)) return null;

        if (dm.AllCards.TryGetValue(cardId, out Card c)) return c;

        // 용병 카드는 AllCards에 없다. 전용 접근자로 표시용 사본을 받는다.
        return dm.GetCharacterCardView(cardId);
    }

    // ─── 확대 팝업 ──────────────────────────────────────────────────────

    public void ShowZoom(Card card)
    {
        if (card == null) return;

        EnsureUI();
        if (_zoomRoot == null) return;

        if (!CardImageLoader.ApplyToImage(_zoomImage, card.ImagePath))
            _zoomImage.sprite = null;

        _zoomImage.preserveAspect = true; // 원본 비율 유지, 크기만 확대
        _zoomImage.color = _zoomImage.sprite != null ? Color.white : new Color(0.2f, 0.2f, 0.24f, 1f);

        _zoomName.text = card.Name;
        _zoomDesc.text = string.IsNullOrWhiteSpace(card.Description) ? "" : card.Description;

        _zoomRoot.SetActive(true);
        _zoomRoot.transform.SetAsLastSibling();
    }

    public void CloseZoom()
    {
        if (_zoomRoot != null) _zoomRoot.SetActive(false);
    }

    private void CloseAll()
    {
        CloseZoom();
        CloseSub();
        CloseGraveyard();
    }

    // ─── 서브 팝업 (화면 왼쪽 중앙) ─────────────────────────────────────
    //
    // 읽기 전용 미리보기다. 화면을 덮지 않으므로 드래그 중에도 보드를 계속 볼 수 있다.
    // 중앙 팝업(모달)은 용병 클릭과 손패 세트 흐름에만 쓴다.

    public void ShowSub(Card card)
    {
        if (card == null) return;

        EnsureUI();
        if (_subRoot == null) return;

        if (!CardImageLoader.ApplyToImage(_subImage, card.ImagePath))
        {
            CloseSub();
            return;
        }

        _subImage.preserveAspect = true;
        _subCard = card;
        _subFromHand = false; // 스택·폐기존 등에서 띄운 것. 손패 호버가 해제해도 닫히지 않는다
        _subRoot.SetActive(true);
        _subRoot.transform.SetAsLastSibling();
    }

    /// <summary>InstanceId로 카드를 찾아 서브 팝업을 띄운다. CardInteraction의 호버/클릭/드래그 시작에서 부른다.</summary>
    public void ShowSubByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        if (_subCard != null && _subCard.InstanceId == instanceId && _subRoot != null && _subRoot.activeSelf)
        {
            _subFromHand = true; // 이미 같은 카드를 띄운 상태 — 이미지를 다시 만들지 않는다
            return;
        }

        foreach (var owner in new[] { _p1, _p2 })
        {
            if (owner == null) continue;
            foreach (var c in owner.Hand)
                if (Matches(c, instanceId)) { ShowSub(c); _subFromHand = true; return; }
        }
    }

    public void CloseSub()
    {
        if (_subRoot != null) _subRoot.SetActive(false);
        _subCard = null;
        _subFromHand = false;
    }

    /// <summary>
    /// 커서가 손패 카드를 벗어났을 때(CardInteraction.OnPointerExit) 호출된다.
    /// Rect 호버 판정이 현재 어떤 카드를 잡고 있으면 그쪽이 전환·닫기를 책임지므로 여기서는 아무것도 하지 않는다.
    /// 판정이 카드를 못 잡는 상황(포인터 이벤트만 살아 있는 경우)에서만 닫는다.
    /// </summary>
    public void CloseSubOnPointerExit(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        if (_hoveredHandInstanceId != null) return;              // 호버 판정이 담당 중
        if (!_subFromHand || _subCard == null) return;           // 스택·폐기존 열람은 유지
        if (_subCard.InstanceId != instanceId) return;           // 다른 카드를 보는 중

        CloseSub();
    }

    /// <summary>서브 팝업 이미지를 한 번 더 누르면 중앙 팝업으로 승격한다.</summary>
    private void OnSubClicked()
    {
        Card card = _subCard;
        CloseSub();
        ShowZoom(card);
    }

    // ─── 폐기존 패널 ────────────────────────────────────────────────────

    public void OpenGraveyard(Player owner)
    {
        if (owner == null) return;

        EnsureUI();
        if (_graveRoot == null) return;

        _graveOwner = owner;
        RebuildGraveyardList();

        _graveRoot.SetActive(true);
        _graveRoot.transform.SetAsLastSibling();
    }

    public void CloseGraveyard()
    {
        if (_graveRoot != null) _graveRoot.SetActive(false);
        _graveOwner = null;
    }

    private void RebuildGraveyardList()
    {
        foreach (var go in _graveEntries) if (go != null) Destroy(go);
        _graveEntries.Clear();

        if (_graveOwner == null) return;

        _graveTitle.text = $"{_graveOwner.Name} 폐기존 ({_graveOwner.Graveyard.Count}장)";

        // ★ 최신순 — 가장 최근에 버린 카드가 맨 위.
        //   엔진 Graveyard는 추가 순서(오래된 것이 앞)라 역순으로 순회한다.
        //   최신 카드를 보려고 스크롤을 끝까지 내리지 않아도 되게 한다.
        for (int i = _graveOwner.Graveyard.Count - 1; i >= 0; i--)
        {
            Card card = _graveOwner.Graveyard[i];
            if (card == null) continue;
            GameObject entry = CreateGraveyardEntry(card);
            if (entry != null) _graveEntries.Add(entry);
        }
    }

    /// <summary>
    /// 폐기존 목록에 카드 한 줄을 올린다.
    /// 줄 크기·비율은 <b>프리팹이 정한다.</b> 코드는 어떤 그림을 넣을지와 누를 수 있는지만 다룬다.
    /// </summary>
    private GameObject CreateGraveyardEntry(Card card)
    {
        GameObject template = ResolveGraveEntryTemplate();
        if (template == null) return null;

        GameObject entryGo = Instantiate(template, _graveContent);
        entryGo.name = $"Grave_{card.Id}";
        entryGo.SetActive(true);

        var view = entryGo.GetComponent<GraveyardEntryItemView>();
        if (view == null)
        {
            Debug.LogError("[CardZoomPopupUI] 폐기존 줄 템플릿에 GraveyardEntryItemView가 없습니다.");
            Destroy(entryGo);
            return null;
        }

        if (!view.Validate(out string reason))
        {
            Debug.LogError($"[CardZoomPopupUI] 폐기존 줄 템플릿이 온전하지 않습니다 — {reason}");
            Destroy(entryGo);
            return null;
        }

        // 그림을 못 읽으면 프리팹의 바탕색이 그대로 남는다 (빈 칸이 되지 않게)
        if (CardImageLoader.ApplyToImage(view.image, card.ImagePath))
            view.image.color = Color.white;

        // 자원 카드는 확대해 볼 내용이 없으므로 누를 수 없게 한다.
        bool clickable = card.Type != CardType.Resource;
        view.button.interactable = clickable;
        view.image.raycastTarget = clickable;

        view.button.onClick.RemoveAllListeners();
        if (clickable)
        {
            Card captured = card;
            view.button.onClick.AddListener(() => ShowSub(captured)); // 중앙 모달이 아니라 서브 팝업
        }

        return entryGo;
    }

    /// <summary>폐기존 줄 템플릿을 한 번만 불러 둔다.</summary>
    private GameObject ResolveGraveEntryTemplate()
    {
        if (_graveEntryTemplate != null) return _graveEntryTemplate;

        _graveEntryTemplate = Resources.Load<GameObject>(GraveEntryResourcePath);

        if (_graveEntryTemplate == null)
        {
            Debug.LogError(
                $"[CardZoomPopupUI] 폐기존 줄 템플릿을 찾지 못했습니다: Resources/{GraveEntryResourcePath}. " +
                "폐기존 목록을 그릴 수 없습니다.");
        }

        return _graveEntryTemplate;
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
        ClearViewFields();

        _hostCanvas = canvas;

        // ① 씬에 이미 놓여 있으면 그것을 쓴다 (중복 방지)
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

    /// <summary>씬에 미리 놓인 화면을 찾아 연결한다. 위치·크기는 손대지 않는다.</summary>
    private bool AdoptSceneRootIfPresent()
    {
        if (_ownsRoot && _view != null) return true;

        foreach (CardZoomPopupView candidate in FindObjectsByType<CardZoomPopupView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate == _view) return true;

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning(
                    $"[CardZoomPopupUI] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뜁니다 — {reason}");
                continue;
            }

            // ★ 꺼진 부모 아래에 있으면 무슨 짓을 해도 화면에 나오지 않는다.
            if (FindInactiveAncestor(candidate.transform) is Transform blocker)
            {
                Debug.LogError(
                    $"[CardZoomPopupUI] 씬의 '{candidate.name}'은 꺼져 있는 '{blocker.name}' 아래에 있어 화면에 뜰 수 없습니다. " +
                    "캔버스 바로 아래로 옮기거나 씬에서 지우세요. 지금은 프리팹을 새로 찍어 씁니다.");
                continue;
            }

            Canvas canvas = candidate.GetComponentInParent<Canvas>();

            _root = candidate.gameObject;
            _view = candidate;
            _ownsRoot = false;   // 빌려 쓰는 것이다
            _hostCanvas = canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;

            PrepareView();

            Debug.Log($"[CardZoomPopupUI] 씬에 배치된 '{candidate.name}'을 사용합니다.");
            return true;
        }

        return false;
    }

    /// <summary>자기 자신을 뺀 조상 중에 꺼져 있는 것이 있으면 돌려준다. 없으면 null.</summary>
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
    /// ★ 씬 캔버스의 자식이어야 CanvasScaler를 물려받아 크기가 보드와 맞는다.
    /// </summary>
    private void BuildFromPrefab(Canvas canvas)
    {
        GameObject prefab = Resources.Load<GameObject>(PopupResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[CardZoomPopupUI] 프리팹을 찾지 못했습니다: Resources/{PopupResourcePath}. " +
                "카드 확대와 폐기존 목록을 쓸 수 없습니다.");
            return;
        }

        _root = Instantiate(prefab, canvas.transform);
        _root.name = prefab.name;   // (Clone) 꼬리표 제거

        _view = _root.GetComponent<CardZoomPopupView>()
                ?? _root.GetComponentInChildren<CardZoomPopupView>(true);

        if (_view == null)
        {
            Debug.LogError($"[CardZoomPopupUI] '{prefab.name}'에 CardZoomPopupView가 없습니다.");
            Destroy(_root);
            _root = null;
            return;
        }

        if (!_view.Validate(out string reason))
        {
            Debug.LogError($"[CardZoomPopupUI] 프리팹 참조가 온전하지 않습니다 — {reason}");
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
    /// 참조를 예전 필드에 그대로 옮겨 담는다 — 쓰는 곳이 서른 곳이 넘어
    /// 한꺼번에 갈아 끼우기보다 여기서 한 번 이어 주는 편이 안전하다.
    /// </summary>
    private void PrepareView()
    {
        _zoomRoot = _view.zoomRoot;
        _zoomImage = _view.zoomImage;
        _zoomName = _view.zoomName;
        _zoomDesc = _view.zoomDesc;

        _subRoot = _view.subRoot;
        _subImage = _view.subImage;

        _graveRoot = _view.graveRoot;
        _graveTitle = _view.graveTitle;
        _graveContent = _view.graveContent;

        WireButtons();

        // ★ 씬에 놓인 것은 켜진 채 저장돼 있을 수 있다. 셋 다 확실히 닫는다.
        if (_zoomRoot != null) _zoomRoot.SetActive(false);
        if (_subRoot != null) _subRoot.SetActive(false);
        if (_graveRoot != null) _graveRoot.SetActive(false);
    }

    private void ClearViewFields()
    {
        _zoomRoot = null; _zoomImage = null; _zoomName = null; _zoomDesc = null;
        _subRoot = null; _subImage = null;
        _graveRoot = null; _graveTitle = null; _graveContent = null;
    }

    /// <summary>
    /// 프리팹 버튼에 동작을 건다.
    /// 인스펙터에 남아 있을지 모를 배선과 겹치지 않도록 먼저 비운다.
    /// </summary>
    private void WireButtons()
    {
        if (_view.subButton != null)
        {
            _view.subButton.onClick.RemoveAllListeners();
            _view.subButton.onClick.AddListener(OnSubClicked);
        }

        if (_view.zoomBackground != null)
        {
            _view.zoomBackground.onClick.RemoveAllListeners();
            _view.zoomBackground.onClick.AddListener(CloseZoom);   // 배경 클릭 = 닫기
        }

        if (_view.graveCloseButton != null)
        {
            _view.graveCloseButton.onClick.RemoveAllListeners();
            _view.graveCloseButton.onClick.AddListener(CloseGraveyard);
        }
    }
}
