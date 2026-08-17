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
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardZoomPopupUI : MonoBehaviour
{
    public static CardZoomPopupUI Instance { get; private set; }

    [Header("중앙 팝업 (용병 클릭 / 서브 팝업에서 한 번 더 클릭)")]
    [Tooltip("확대 이미지가 차지할 최대 높이 (화면 높이 대비 비율)")]
    [Range(0.3f, 0.95f)] public float zoomHeightRatio = 0.62f;
    public Color dimColor = new Color(0f, 0f, 0f, 0.80f);

    [Header("서브 팝업 (화면 왼쪽 중앙, 읽기 전용 미리보기)")]
    [Tooltip("원본 카드 넓이의 몇 배로 볼지. 비율은 유지된다 (화면 높이를 넘으면 자동으로 줄인다)")]
    public float subZoomWidthScale = 3f;
    public Vector2 subZoomBaseSize = new Vector2(200f, 280f);
    public float subZoomLeftMargin = 16f;
    public Color subZoomBackColor = new Color(0.08f, 0.08f, 0.11f, 0.86f);

    [Header("Graveyard Panel")]
    public float graveyardPanelWidth = 260f;
    public Vector2 graveyardEntrySize = new Vector2(200f, 280f);
    public float graveyardEntryScale = 0.85f;
    public float graveyardEntrySpacing = 10f;
    public Color graveyardPanelColor = new Color(0.09f, 0.09f, 0.12f, 0.94f);

    // ─── 상태 ───────────────────────────────────────────────────────────
    private Canvas _hostCanvas;
    private TMP_FontAsset _font;

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
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;
        EventManager.OnCharacterFieldSync -= HandleFieldSync;
        EventManager.OnCharacterSlotUpdated -= HandleSlotUpdated;
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
        _hoveredHandInstanceId = null;
        _hoverReadinessLogged = false;
        _hoverHitMethod = null;
        _hoverState = null;
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
            if (_p1 == null) { LogHoverStateOnce("매치 시작 전"); return; }
            EnsureUI();
            if (_subRoot == null) { LogHoverStateOnce("서브 팝업 생성 실패(캔버스 없음)"); return; }
        }

        // 드래그 중엔 집은 카드를 계속 보여 준다
        if (CardInteraction.IsDraggingAny) { LogHoverStateOnce("드래그 중 — 호버 갱신 정지"); return; }

        // 모달이나 손패를 덮는 패널이 열려 있으면 그 아래 카드를 집지 않는다
        if (_zoomRoot != null && _zoomRoot.activeSelf)
        {
            _hoveredHandInstanceId = null;
            LogHoverStateOnce("중앙 확대 팝업이 열려 있음");
            return;
        }
        if (HumanChoiceDialogUI.Instance != null && HumanChoiceDialogUI.Instance.IsOpen)
        {
            _hoveredHandInstanceId = null;
            LogHoverStateOnce("선택 다이얼로그가 열려 있음");
            return;
        }

        Vector2 pos = Input.mousePosition;
        Camera cam = ResolveEventCamera();

        if (_graveRoot != null && _graveRoot.activeSelf && ContainsPoint(_graveRoot.transform, pos, cam))
        {
            _hoveredHandInstanceId = null;
            LogHoverStateOnce("폐기존 패널 위");
            return;
        }

        LogHoverStateOnce("판정 동작 중");

        string hovered = FindHandCardUnder(pos, cam);
        if (hovered == _hoveredHandInstanceId) return; // 같은 카드 위 = 할 일 없음

        _hoveredHandInstanceId = hovered;

        if (hovered != null) ShowSubByInstanceId(hovered);
        else if (_subFromHand) CloseSub(); // 손패에서 띄운 것만 닫는다 (스택·폐기 열람은 유지)
    }

    private string _hoverState;

    /// <summary>
    /// 호버 판정의 현재 상태를 <b>바뀔 때만</b> 한 줄 남긴다.
    /// 호버가 안 먹을 때 어느 관문에서 멈췄는지 콘솔에서 바로 보인다.
    /// </summary>
    private void LogHoverStateOnce(string state)
    {
        if (_hoverState == state) return;
        _hoverState = state;
        Debug.Log($"[CardZoomPopupUI] 손패 호버 상태: {state}");
    }

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

        LogHoverReadinessOnce(human);

        string byRaycast = FindHandCardByRaycast(pos, human);
        if (byRaycast != null)
        {
            LogHoverHitOnce("레이캐스트");
            return byRaycast;
        }

        string byRect = FindHandCardByRect(pos, cam, human);
        if (byRect != null) LogHoverHitOnce("Rect 판정");

        return byRect;
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

    private string _hoverHitMethod;

    /// <summary>어느 수단으로 카드를 잡았는지 처음 한 번만 남긴다.</summary>
    private void LogHoverHitOnce(string method)
    {
        if (_hoverHitMethod == method) return;
        _hoverHitMethod = method;
        Debug.Log($"[CardZoomPopupUI] 손패 호버 감지 성공 — {method}");
    }

    private bool _hoverReadinessLogged;

    /// <summary>
    /// 호버 미리보기가 카드 GO를 찾을 수 있는 상태인지 매치당 한 번만 기록한다.
    /// 호버가 안 먹을 때 "누구 손패를 보고 있는지 / GO 연결이 됐는지"를 바로 알 수 있다.
    /// </summary>
    private void LogHoverReadinessOnce(Player human)
    {
        if (_hoverReadinessLogged || human.Hand.Count == 0) return;
        _hoverReadinessLogged = true;

        int linked = 0;
        foreach (var c in human.Hand)
            if (c != null && CardBoardRegistry.TryGet(c.InstanceId, out GameObject go) && go != null) linked++;

        Debug.Log($"[CardZoomPopupUI] 손패 호버 준비 — 대상 '{human.Name}' 손패 {human.Hand.Count}장 / 카드 GO 연결 {linked}장");
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
            _graveEntries.Add(CreateGraveyardEntry(card));
        }
    }

    private GameObject CreateGraveyardEntry(Card card)
    {
        var entryGo = new GameObject($"Grave_{card.Id}", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)entryGo.transform;
        rect.SetParent(_graveContent, false);
        rect.sizeDelta = graveyardEntrySize * graveyardEntryScale;

        var img = entryGo.GetComponent<Image>();
        if (!CardImageLoader.ApplyToImage(img, card.ImagePath))
            img.color = new Color(0.22f, 0.22f, 0.26f, 1f);
        img.preserveAspect = true;

        var layout = entryGo.AddComponent<LayoutElement>();
        layout.preferredWidth = rect.sizeDelta.x;
        layout.preferredHeight = rect.sizeDelta.y;

        // 자원 카드는 확대해 볼 내용이 없으므로 클릭 대상에서 제외한다
        if (card.Type != CardType.Resource)
        {
            var button = entryGo.AddComponent<Button>();
            Card captured = card;
            button.onClick.AddListener(() => ShowSub(captured)); // 중앙 모달이 아니라 서브 팝업
        }
        else
        {
            img.raycastTarget = false;
        }

        return entryGo;
    }

    // ─── UI 생성 ────────────────────────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null) return;
        if (_zoomRoot != null && _hostCanvas == canvas) return;

        if (_zoomRoot != null) Destroy(_zoomRoot);
        if (_graveRoot != null) Destroy(_graveRoot);

        _hostCanvas = canvas;
        _font = UiFontResolver.Resolve();
        BuildZoom(canvas);
        BuildSub(canvas);
        BuildGraveyard(canvas);
    }

    private void BuildSub(Canvas canvas)
    {
        Vector2 size = new Vector2(
            subZoomBaseSize.x * subZoomWidthScale,
            subZoomBaseSize.y * subZoomWidthScale); // 비율 유지 — 넓이 배수를 높이에도 동일 적용

        // 배수를 키우면 세로가 화면을 넘칠 수 있다. 넘치면 비율을 지킨 채 함께 줄인다
        var canvasRect = canvas.GetComponent<RectTransform>();
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : Screen.height;
        float maxHeight = canvasHeight * 0.92f;
        if (size.y > maxHeight)
        {
            size *= maxHeight / size.y;
        }

        _subRoot = new GameObject("CardSubPopup",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
            typeof(Canvas), typeof(GraphicRaycaster));
        var rect = (RectTransform)_subRoot.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(subZoomLeftMargin, 0f);

        _subRoot.GetComponent<Image>().color = subZoomBackColor;
        _subRoot.GetComponent<Button>().onClick.AddListener(OnSubClicked);

        var sc = _subRoot.GetComponent<Canvas>();
        sc.overrideSorting = true;
        sc.sortingOrder = 780; // 폐기존 패널(800)보다 아래, 보드보다는 위

        var imgGo = new GameObject("SubImage", typeof(RectTransform), typeof(Image));
        var imgRect = (RectTransform)imgGo.transform;
        imgRect.SetParent(rect, false);
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.one * 6f;
        imgRect.offsetMax = Vector2.one * -6f;

        _subImage = imgGo.GetComponent<Image>();
        _subImage.preserveAspect = true;
        _subImage.raycastTarget = false; // 클릭은 루트 Button이 받는다

        _subRoot.SetActive(false);
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

    private void BuildZoom(Canvas canvas)
    {
        _zoomRoot = new GameObject("CardZoomPopup",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
            typeof(Canvas), typeof(GraphicRaycaster));
        var rootRect = (RectTransform)_zoomRoot.transform;
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);
        _zoomRoot.GetComponent<Image>().color = dimColor;
        _zoomRoot.GetComponent<Button>().onClick.AddListener(CloseZoom); // 배경 클릭 = 닫기

        var zc = _zoomRoot.GetComponent<Canvas>();
        zc.overrideSorting = true;
        zc.sortingOrder = 850; // 덱 패널(700)보다 위, 결과 오버레이(900)보다 아래

        // 확대 이미지
        var imgGo = new GameObject("ZoomImage", typeof(RectTransform), typeof(Image));
        var imgRect = (RectTransform)imgGo.transform;
        imgRect.SetParent(rootRect, false);
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.pivot = new Vector2(0.5f, 0.5f);
        imgRect.anchoredPosition = new Vector2(0f, 60f);
        imgRect.sizeDelta = ComputeZoomBox(canvas);
        _zoomImage = imgGo.GetComponent<Image>();
        _zoomImage.preserveAspect = true;
        _zoomImage.raycastTarget = false;

        // 이름
        _zoomName = CreateText(rootRect, "", 34f, TextAlignmentOptions.Center);
        var nameRect = _zoomName.rectTransform;
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.sizeDelta = new Vector2(760f, 46f);
        nameRect.anchoredPosition = new Vector2(0f, 60f - imgRect.sizeDelta.y * 0.5f - 12f);

        // 설명
        _zoomDesc = CreateText(rootRect, "", 22f, TextAlignmentOptions.Top);
        var descRect = _zoomDesc.rectTransform;
        descRect.anchorMin = new Vector2(0.5f, 0.5f);
        descRect.anchorMax = new Vector2(0.5f, 0.5f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.sizeDelta = new Vector2(760f, 120f);
        descRect.anchoredPosition = nameRect.anchoredPosition + new Vector2(0f, -50f);
        _zoomDesc.enableWordWrapping = true;

        _zoomRoot.SetActive(false);
    }

    private Vector2 ComputeZoomBox(Canvas canvas)
    {
        var canvasRect = canvas.GetComponent<RectTransform>();
        float h = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : Screen.height;

        float boxH = h * zoomHeightRatio;
        // preserveAspect가 실제 비율을 맞추므로 박스는 넉넉히 잡아 두면 된다 (카드 원본 200x280 기준)
        return new Vector2(boxH * (200f / 280f), boxH);
    }

    private void BuildGraveyard(Canvas canvas)
    {
        _graveRoot = new GameObject("GraveyardPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Canvas), typeof(GraphicRaycaster));
        // 화면 오른쪽 벽에 붙인다 (왼쪽은 진행 로그 패널 자리)
        var rect = (RectTransform)_graveRoot.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(graveyardPanelWidth, 0f);
        rect.anchoredPosition = Vector2.zero;
        _graveRoot.GetComponent<Image>().color = graveyardPanelColor;

        var gc = _graveRoot.GetComponent<Canvas>();
        gc.overrideSorting = true;
        gc.sortingOrder = 800; // 확대 팝업(850)보다 아래

        // 제목
        _graveTitle = CreateText(rect, "폐기존", 20f, TextAlignmentOptions.Center);
        var titleRect = _graveTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-16f, 40f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);

        // 닫기
        TextMeshProUGUI closeLabel;
        Button close = CreateButton(rect, "GraveClose", "닫기", new Color(0.32f, 0.32f, 0.38f, 1f), out closeLabel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(graveyardPanelWidth - 40f, 42f);
        closeRect.anchoredPosition = new Vector2(0f, 12f);
        close.onClick.AddListener(CloseGraveyard);

        // 세로 스크롤
        var scrollGo = new GameObject("GraveScroll", typeof(RectTransform), typeof(ScrollRect));
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.SetParent(rect, false);
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(8f, 62f);   // 닫기 버튼 위
        scrollRect.offsetMax = new Vector2(-8f, -52f); // 제목 아래

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var viewportRect = (RectTransform)viewportGo.transform;
        viewportRect.SetParent(scrollRect, false);
        Stretch(viewportRect);
        var vpImage = viewportGo.GetComponent<Image>();
        vpImage.color = new Color(0f, 0f, 0f, 0.001f);
        vpImage.raycastTarget = true;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _graveContent = (RectTransform)contentGo.transform;
        _graveContent.SetParent(viewportRect, false);
        _graveContent.anchorMin = new Vector2(0.5f, 1f);
        _graveContent.anchorMax = new Vector2(0.5f, 1f);
        _graveContent.pivot = new Vector2(0.5f, 1f);
        _graveContent.anchoredPosition = Vector2.zero;

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = graveyardEntrySpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 6, 6);

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = _graveContent;

        _graveRoot.SetActive(false);
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

        labelText = CreateText(go.transform, label, 20f, TextAlignmentOptions.Center);
        var r = labelText.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }
}
