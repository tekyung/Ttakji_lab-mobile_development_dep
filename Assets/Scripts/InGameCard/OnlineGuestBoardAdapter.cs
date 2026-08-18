// OnlineGuestBoardAdapter.cs — 게스트 화면을 board_state로 복원하는 어댑터
//
// ★ 왜 필요한가
//   호스트는 ServerGameManager라는 진짜 엔진이 돌고, 보드 UI는 그 엔진이 쏘는
//   EventManager 이벤트를 구독해 그려진다. 그런데 게스트에는 엔진도 Player 객체도 없다.
//   게스트가 받는 것은 호스트가 파이어베이스에 올리는 board_state 스냅샷뿐이라,
//   그대로는 손패 한 장도 그릴 수 없었다(조작 수단도 Test_* 디버그 메서드뿐이었다).
//
//   이 컴포넌트는 그 스냅샷을 읽어 게스트 쪽에 Player 두 개를 흉내 내어 만들고,
//   스냅샷이 바뀔 때마다 이전 상태와 비교해 EventManager 이벤트로 되쏜다.
//   그러면 기존 보드 UI(PlayerUIManager / EnemyVisualTester / CardBoardRegistry)가
//   로컬 플레이와 똑같이 동작한다. 보드 UI는 한 줄도 고치지 않았다.
//
// ★ 서버 스크립트를 건드리지 않는 방법
//   session_game_manage도 board_state를 구독하지만 그 필드는 private다.
//   파이어베이스 ValueChanged는 핸들러를 여러 개 붙일 수 있으므로
//   여기서 독자적으로 하나 더 붙인다(firebase_network.ListenForBoardState).
//   ⚠️ ListenForEventDTO는 부르면 안 된다 — 그쪽은 StopListeningEvents()로 기존 리스너를 떼어 낸다.
//
// ★ 한계 (2026-08-17 시점)
//   - 표시 전용이다. 게스트의 입력은 여전히 session_game_manage의 Test_* 경로다
//   - board_state에 없는 정보는 복원할 수 없다. 호스트가 Character1_ID를 채우지 않는
//     문제(섹션 11 F-1 #3)가 해결되면 용병 슬롯도 함께 살아난다
using System.Collections.Generic;
using ServerScripts.EventScripts;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnlineGuestBoardAdapter : MonoBehaviour
{
    private static OnlineGuestBoardAdapter _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("OnlineGuestBoardAdapter");
        _instance = go.AddComponent<OnlineGuestBoardAdapter>();
        DontDestroyOnLoad(go);
    }

    // ─── 상태 ───────────────────────────────────────────────────────────
    private Player _mine;   // 이 클라이언트(게스트)
    private Player _foe;    // 상대(호스트)
    private bool _listening;
    private bool _started;

    /// <summary>InstanceId → 복원한 카드. 스냅샷마다 새로 만들면 GO 매칭이 끊기므로 재사용한다.</summary>
    private readonly Dictionary<string, Card> _cards = new Dictionary<string, Card>();

    /// <summary>InstanceId → 현재 존. 다음 스냅샷과 비교해 이동을 찾아낸다.</summary>
    private readonly Dictionary<string, ZoneType> _zoneOf = new Dictionary<string, ZoneType>();

    /// <summary>InstanceId → 공개 여부. 존은 그대로인데 앞면/뒷면만 바뀌는 경우를 잡는다.</summary>
    private readonly Dictionary<string, bool> _revealedOf = new Dictionary<string, bool>();

    /// <summary>이번 스냅샷이 말하는 공개 여부 (재사용 버퍼)</summary>
    private readonly Dictionary<string, bool> _desiredReveal = new Dictionary<string, bool>();

    private readonly Queue<string> _pending = new Queue<string>();
    private readonly object _lock = new object();

    // ─── 이동 큐 ────────────────────────────────────────────────────────
    // ★ 스냅샷 하나에 한 페이즈치 이동(5~10건)이 통째로 들어 있다.
    //   이걸 한 프레임에 다 적용하면 이동 트윈과 덱·폐기존 재배치(DeckGraveyardStackUI.Sync)가
    //   동시에 터져 화면이 번쩍인다. 호스트는 엔진이 ActionDelay를 두고 진행해 그럴 일이 없다.
    //   그래서 프레임당 몇 건씩 나눠 적용한다.
    [Tooltip("한 프레임에 반영할 카드 이동 수")]
    public int movesPerFrame = 1;

    private readonly Queue<PendingMove> _moveQueue = new Queue<PendingMove>();

    private struct PendingMove
    {
        public string InstanceId;
        public Player Owner;
        public ZoneType From;
        public ZoneType To;
        public Card Card;
    }

    private int _lastTurn = -1;
    private string _lastPhase;
    private int _lastMyLife = int.MinValue;
    private int _lastFoeLife = int.MinValue;
    private int _lastMyResource = int.MinValue;
    private int _lastFoeResource = int.MinValue;
    private bool _gameOverHandled;

    private GameDataManager _data;

    // 입력 요청을 합성한 페이즈를 기억해 같은 페이즈에서 창이 두 번 뜨지 않게 한다
    private string _inputRequestedForPhase;
    private int _inputRequestedForTurn = -1;
    private session_game_manage _client;
    private readonly GameContext _ctx = new GameContext();

    // ─── 수명 주기 ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryAttach();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetMirror();
        TryAttach();
    }

    private void ResetMirror()
    {
        _mine = null;
        _foe = null;
        _started = false;
        _listening = false;
        _gameOverHandled = false;
        _cards.Clear();
        _zoneOf.Clear();
        _revealedOf.Clear();
        _desiredReveal.Clear();

        _lastTurn = -1;
        _lastPhase = null;
        _lastMyLife = int.MinValue;
        _lastFoeLife = int.MinValue;
        _lastMyResource = int.MinValue;
        _lastFoeResource = int.MinValue;
        _inputRequestedForPhase = null;
        _inputRequestedForTurn = -1;
        _client = null;
        _moveQueue.Clear();

        lock (_lock) { _pending.Clear(); }
    }

    /// <summary>게스트 온라인 매치일 때만 board_state 구독을 건다.</summary>
    private void TryAttach()
    {
        if (_listening) return;
        if (!OnlineMatchStarter.IsOnlineSessionActive) return;
        if (GameData.MyRole != "GUEST") return;   // 호스트는 진짜 엔진이 돌린다

        // 비활성 오브젝트까지 찾는다 — GameManage는 씬에 꺼진 채 저장돼 있고
        // OnlineMatchStarter가 켜 주는데, 그 순서를 보장할 수 없다
        _client = FindFirstObjectByType<session_game_manage>(FindObjectsInactive.Include);
        if (_client == null) return; // 대전 씬이 아니다

        // 아직 켜지지 않았으면 Start()가 안 돌아 파이어베이스 초기화 전이다. 다음 프레임에 다시 본다
        if (!_client.isActiveAndEnabled) return;

        var network = FindFirstObjectByType<firebase_network>(FindObjectsInactive.Include);
        if (network == null) return;

        // 구독은 firebase_network.Initialize() 이후에만 유효하다(내부 dbRef가 static).
        // session_game_manage.Start()가 그걸 await하므로 몇 프레임 늦을 수 있어 실패하면 조용히 재시도한다.
        try
        {
            // ⚠️ ListenForBoardState는 핸들러를 추가만 한다. session_game_manage 쪽 구독과 공존한다
            network.ListenForBoardState(GameData.SessionCode, EnqueueSnapshot);
        }
        catch (System.Exception e)
        {
            if (Time.frameCount % 120 == 0)
                Debug.Log($"[GuestBoard] board_state 구독 대기 중 ({e.GetType().Name}) — 파이어베이스 초기화 전일 수 있다.");
            return;
        }

        _listening = true;

        Debug.Log($"[GuestBoard] board_state 구독 시작 — 세션 {GameData.SessionCode}");
    }

    /// <summary>파이어베이스 콜백. 유니티 객체를 만지지 않고 큐에만 넣는다.</summary>
    private const int MaxPendingSnapshots = 30;

    private void EnqueueSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        lock (_lock)
        {
            // 순서를 지켜야 하므로 원칙적으로 버리지 않는다. 병적으로 밀릴 때만 오래된 것을 흘린다
            while (_pending.Count >= MaxPendingSnapshots) _pending.Dequeue();
            _pending.Enqueue(json);
        }
    }

    private void Update()
    {
        if (!_listening)
        {
            TryAttach();
            return;
        }

        // 밀린 이동부터 조금씩 흘려보낸다. 다 비우기 전에는 다음 스냅샷을 읽지 않는다(순서 보존)
        if (_moveQueue.Count > 0)
        {
            DrainMoveQueue();
            return;
        }

        string json = null;
        lock (_lock)
        {
            if (_pending.Count > 0) json = _pending.Dequeue();
        }

        // ★ 예전에는 "밀렸으면 최신 것만" 반영했는데, 그러면 중간 상태가 통째로 사라진다.
        //   특히 세트존에서 공개(앞면)된 뒤 스택존으로 넘어가는 카드는 그 '공개' 스냅샷을 놓치면
        //   앞면으로 뒤집힐 기회를 영영 잃는다(스택존은 ApplyZoneMove가 앞뒷면을 건드리지 않는다).
        //   그래서 순서대로, 프레임당 하나씩 반영한다. 한 프레임에 몰아 처리하지 않으니
        //   화면이 한꺼번에 다시 그려지며 번쩍이던 것도 줄어든다.
        if (json != null) Apply(json);
    }

    // ─── 스냅샷 반영 ────────────────────────────────────────────────────

    private void Apply(string json)
    {
        BoardState state;
        try
        {
            state = JsonUtility.FromJson<BoardState>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GuestBoard] board_state 파싱 실패: {e.Message}");
            return;
        }

        if (state == null || !EnsureData()) return;

        if (!_started) StartMirror(state);

        Reconcile(state);
    }

    private bool EnsureData()
    {
        if (_data != null) return true;

        // 씬의 BattleManager가 이미 카드 데이터를 들고 있다 (카드 확대 팝업도 이걸 쓴다)
        _data = BattleManager.Instance != null ? BattleManager.Instance.CardData : null;
        if (_data != null) return true;

        Debug.LogWarning("[GuestBoard] 카드 데이터를 얻지 못했다 — 씬에 BattleManager가 있어야 한다.");
        return false;
    }

    /// <summary>첫 스냅샷으로 미러 플레이어 2명을 만들고 카드 GO 풀을 띄운다.</summary>
    private void StartMirror(BoardState state)
    {
        PlayerState mineState = state.GuestState;
        PlayerState foeState = state.HostState;

        _mine = new Player
        {
            Name = "GUEST",
            Type = UserType.Human,
            CharacterCardId = mineState != null ? mineState.Character1_ID : null,
            SecondaryCharacterId = mineState != null ? mineState.Character2_ID : null
        };

        _foe = new Player
        {
            Name = "HOST",
            Type = UserType.Human,
            CharacterCardId = foeState != null ? foeState.Character1_ID : null,
            SecondaryCharacterId = foeState != null ? foeState.Character2_ID : null
        };

        // ★ 카드 GO 풀은 OnGameStart 시점의 Deck + ResourceDeck으로 만들어진다.
        //   중간에 들어왔을 수도 있으므로 모든 존의 카드를 일단 덱에 담아 풀을 넉넉히 만들고,
        //   바로 뒤따르는 Reconcile이 각자 제자리로 옮긴다.
        AssignAllCardsToDeck(_mine, mineState, state, "GUEST");
        AssignAllCardsToDeck(_foe, foeState, state, "HOST");

        _started = true;

        // 내가 p1이어야 하단 보드 주인이 된다 (CharacterFieldUI·CardZoomPopupUI의 owner == p1 규칙)
        EventManager.OnGameStart?.Invoke(_mine, _foe);
        EventManager.OnLogMessage?.Invoke("<color=cyan>[온라인] 상대 보드와 동기화되었습니다.</color>");

        Debug.Log($"[GuestBoard] 미러 생성 — 내 카드 {_mine.Deck.Count}장 / 상대 {_foe.Deck.Count}장");
    }

    /// <summary>스냅샷에 등장하는 이 플레이어의 카드를 전부 덱에 담는다 (풀 생성용).</summary>
    private void AssignAllCardsToDeck(Player player, PlayerState ps, BoardState state, string role)
    {
        var main = new List<Card>();
        var resource = new List<Card>();

        if (ps != null)
        {
            CollectInto(main, ps.DeckCardInstanceIds, ps.DeckCardDataIds);
            CollectInto(main, ps.HandCardInstanceIds, ps.HandCardDataIds);
            CollectInto(main, ps.GraveCardInstanceIds, ps.GraveCardDataIds);
            CollectInto(resource, ps.ResourceDeckCardInstanceIds, ps.ResourceDeckCardDataIds);
            CollectInto(resource, ps.ResourceZoneCardInstanceIds, ps.ResourceZoneCardDataIds);
        }

        if (state.FieldCards != null)
        {
            foreach (CardState cs in state.FieldCards)
            {
                if (cs == null || cs.OwnerRole != role) continue;

                Card card = ResolveCard(cs.InstanceId, cs.CardDataId);
                if (card != null) main.Add(card);
            }
        }

        player.ResetForNewGame(main, resource);

        // ★ 시작 위치를 기록해 둔다. 이게 없으면 첫 Reconcile이 "원래 어디 있었는지"를 몰라
        //   자원덱 카드를 다시 자원덱에 넣어 중복시킨다.
        foreach (Card c in main) _zoneOf[c.InstanceId] = ZoneType.Deck;
        foreach (Card c in resource) _zoneOf[c.InstanceId] = ZoneType.ResourceDeck;
    }

    private void CollectInto(List<Card> target, string[] instanceIds, string[] dataIds)
    {
        if (instanceIds == null || dataIds == null) return;

        int count = Mathf.Min(instanceIds.Length, dataIds.Length);
        for (int i = 0; i < count; i++)
        {
            Card card = ResolveCard(instanceIds[i], dataIds[i]);
            if (card != null) target.Add(card);
        }
    }

    /// <summary>InstanceId로 카드를 캐시에서 찾고, 없으면 카드 데이터에서 복제해 만든다.</summary>
    private Card ResolveCard(string instanceId, string dataId)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(dataId)) return null;
        if (_cards.TryGetValue(instanceId, out Card cached)) return cached;

        if (!_data.AllCards.TryGetValue(dataId, out Card template) || template == null)
        {
            Debug.LogWarning($"[GuestBoard] 알 수 없는 카드 데이터: {dataId}");
            return null;
        }

        Card card = template.Clone();
        card.InstanceId = instanceId; // ★ 호스트와 같은 식별자여야 카드 GO가 매칭된다
        _cards[instanceId] = card;
        return card;
    }

    // ─── 차이 반영 ──────────────────────────────────────────────────────

    private struct ZoneMembership
    {
        public Player Owner;
        public ZoneType Zone;
        public Card Card;
    }

    private void Reconcile(BoardState state)
    {
        var desired = new Dictionary<string, ZoneMembership>();

        _desiredReveal.Clear();
        CollectZones(desired, state.GuestState, _mine);
        CollectZones(desired, state.HostState, _foe);
        CollectFieldZones(desired, state);

        // 1) 존이 바뀐 카드를 옮긴다
        foreach (var pair in desired)
        {
            string instanceId = pair.Key;
            ZoneMembership want = pair.Value;

            if (want.Card == null || want.Owner == null) continue;
            if (_zoneOf.TryGetValue(instanceId, out ZoneType current) && current == want.Zone) continue;

            ZoneType from = ZoneType.Deck;
            if (_zoneOf.TryGetValue(instanceId, out ZoneType known))
            {
                from = known;
                want.Owner.ExtractCard(from, want.Card);
            }
            else
            {
                // 어디 있었는지 모르는 카드(스냅샷에 갑자기 등장). 어느 존에도 남지 않도록 훑어서 뺀다
                RemoveFromAnyZone(want.Owner, want.Card);
            }

            want.Owner.InsertCard(want.Zone, want.Card);
            _zoneOf[instanceId] = want.Zone;

            _moveQueue.Enqueue(new PendingMove
            {
                InstanceId = instanceId,
                Owner = want.Owner,
                From = from,
                To = want.Zone,
                Card = want.Card
            });
            continue; // 실제 화면 반영은 DrainMoveQueue가 프레임을 나눠 처리한다

        }

        // 2) 앞면/뒷면 변화 (존은 그대로인데 공개만 된 경우 — 오픈 페이즈의 세트 카드)
        ApplyRevealChanges(desired);

        // 3) 수치 변화
        SyncNumbers(state);

        // 4) 턴·페이즈
        if (state.CurrentTurn > 0 && state.CurrentTurn != _lastTurn)
        {
            _lastTurn = state.CurrentTurn;
            EventManager.OnTurnStart?.Invoke(state.CurrentTurn, state.ActivePlayer);
        }

        if (!string.IsNullOrEmpty(state.CurrentPhase) && state.CurrentPhase != _lastPhase)
        {
            _lastPhase = state.CurrentPhase;
            EventManager.OnLogMessage?.Invoke($"[ {PhaseLabel(state.CurrentPhase)} ]");
        }

        // 5) 종료
        if (state.IsGameOver && !_gameOverHandled)
        {
            _gameOverHandled = true;

            if (!string.IsNullOrEmpty(state.ResultMessage))
                EventManager.OnLogMessage?.Invoke(state.ResultMessage);

            Player winner = null;
            if (state.WinnerRole == "GUEST") winner = _mine;
            else if (state.WinnerRole == "HOST") winner = _foe;

            EventManager.OnGameSet?.Invoke(winner);
        }

        // 6) 내 차례의 입력 요청을 합성한다
        RequestInputIfNeeded(state);
    }

    /// <summary>큐에 쌓인 카드 이동을 프레임당 몇 건씩 화면에 반영한다.</summary>
    private void DrainMoveQueue()
    {
        int budget = Mathf.Max(1, movesPerFrame);

        while (budget-- > 0 && _moveQueue.Count > 0)
        {
            PendingMove move = _moveQueue.Dequeue();

            // ★ 앞면/뒷면은 '도착한 존'이 정한다.
            //   엔진 규칙(Player.cs)은 "세트존에 뒷면으로 올라간 카드만 뒷면, 나머지는 앞면"이고,
            //   AbandonSetCard는 폐기 직전에 앞면으로 되돌린다(L297).
            ApplyFaceForZone(move.Card, move.To, move.InstanceId);

            EventManager.OnCardMove?.Invoke(move.Card, move.Owner, move.From, move.Owner, move.To);

            // ApplyZoneMove는 존마다 앞뒷면 처리가 다르고 스택존은 아예 건드리지 않는다.
            // 그래서 이동 뒤에 카드 GO의 면을 한 번 더 맞춘다.
            RefreshCardFace(move.InstanceId, move.Card, move.Owner, move.To);

            EmitZoneArrivalEvents(move.Card, move.Owner, move.From, move.To);
            LogCardMove(move.Card, move.Owner, move.From, move.To);
        }
    }

    /// <summary>
    /// 도착한 존에 따라 <b>엔진이 쏘는 것과 같은 이벤트</b>를 함께 발행한다.
    ///
    /// ★ 이게 이번 버그의 핵심이었다. 어댑터가 <c>OnCardMove</c>만 흉내 냈는데,
    ///   보드 UI의 스택존 렌더링은 <c>OnCardStacked</c>에 걸려 있다
    ///   (PlayerUIManager.HandleCardStacked → StackZoneRowUI.SyncFromEngineStack →
    ///    거기서 줄 배치와 <c>SetFaceDown(false)</c>가 이뤄진다).
    ///   그 이벤트를 안 쏘니 내 스택 카드가 줄로 정렬되지도, 앞면으로 뒤집히지도 않았다.
    ///
    ///   교훈: <b>미러는 엔진 이벤트를 골라 쏘면 안 된다.</b> 같은 전이에는 같은 세트를 전부 쏜다.
    ///   (ServerGameManager L848·L857, BattleManager도 동일하게 쌍으로 발행한다)
    /// </summary>
    private void EmitZoneArrivalEvents(Card card, Player owner, ZoneType from, ZoneType to)
    {
        switch (to)
        {
            case ZoneType.SetZone:
                // PlayerUIManager가 이 이벤트로 "확정 전 잔상"을 지운다
                EventManager.OnCardSet?.Invoke(card, owner);
                break;

            case ZoneType.StackZone:
                EventManager.OnCardStacked?.Invoke(card, owner);
                break;

            case ZoneType.BattlefieldZone:
                EventManager.OnCardBattlefield?.Invoke(card, owner);
                break;

            case ZoneType.ResourceZone:
                EventManager.OnCardResourceAdded?.Invoke(card, owner);
                break;

            case ZoneType.Hand:
                if (from == ZoneType.Deck)
                    EventManager.OnCardDraw?.Invoke(card, owner, ZoneType.Deck);
                break;
        }
    }

    /// <summary>
    /// 카드 GO의 앞뒷면을 모델(<see cref="Card.IsFaceUp"/>)에 맞춘다.
    /// 단, 상대 손패는 언제나 뒷면이어야 하므로 예외로 둔다.
    /// </summary>
    private void RefreshCardFace(string instanceId, Card card, Player owner, ZoneType zone)
    {
        if (!CardBoardRegistry.TryGet(instanceId, out GameObject go) || go == null) return;

        var ui = go.GetComponent<CardUI>();
        if (ui == null) return;

        bool faceDown = !card.IsFaceUp;

        // 상대 손패·덱은 내용이 보이면 안 된다
        if (zone == ZoneType.Deck || zone == ZoneType.ResourceDeck) faceDown = true;
        else if (zone == ZoneType.Hand && !LocalPlayerContext.IsMine(owner)) faceDown = true;

        ui.SetFaceDown(faceDown);
    }

    /// <summary>
    /// 도착한 존에 맞춰 카드의 앞면/뒷면을 정한다.
    /// 세트존만 "공개했는가"를 따르고 나머지는 모두 앞면이다(덱은 보드 UI가 강제로 뒷면 처리한다).
    /// </summary>
    private void ApplyFaceForZone(Card card, ZoneType zone, string instanceId)
    {
        if (zone == ZoneType.SetZone)
        {
            card.IsFaceUp = _desiredReveal.TryGetValue(instanceId, out bool revealed) && revealed;
            return;
        }

        card.IsFaceUp = true;
    }

    /// <summary>
    /// 카드가 같은 존에 머문 채 앞면/뒷면만 바뀐 경우를 처리한다.
    /// ★ 존 이동이 없으면 OnCardMove가 나가지 않아 화면이 뒷면인 채로 남는다 —
    ///   오픈 페이즈에서 세트 카드가 공개돼도 게스트 화면에서는 안 뒤집히던 원인이다.
    /// </summary>
    private void ApplyRevealChanges(Dictionary<string, ZoneMembership> desired)
    {
        foreach (var pair in _desiredReveal)
        {
            string instanceId = pair.Key;
            bool revealed = pair.Value;

            if (_revealedOf.TryGetValue(instanceId, out bool known) && known == revealed) continue;
            _revealedOf[instanceId] = revealed;

            if (!desired.TryGetValue(instanceId, out ZoneMembership membership) || membership.Card == null) continue;

            membership.Card.IsFaceUp = revealed;

            // 계약대로 알린다 (PlayerUIManager·EnemyVisualTester 둘 다 이 이벤트를 구독한다)
            EventManager.OnCardStateChanged?.Invoke(membership.Card);

            if (revealed)
            {
                string who = LocalPlayerContext.IsMine(membership.Owner) ? "나" : "상대";
                EventManager.OnLogMessage?.Invoke($"{who}: '{membership.Card.Name}' 공개! (Speed: {membership.Card.Speed}, {membership.Card.Type})");
            }

            // 구독자가 놓치는 경우를 대비해 카드 GO도 직접 맞춰 준다
            RefreshCardFace(instanceId, membership.Card, membership.Owner, membership.Zone);
        }
    }

    private void CollectZones(Dictionary<string, ZoneMembership> map, PlayerState ps, Player owner)
    {
        if (ps == null || owner == null) return;

        Add(map, owner, ZoneType.Deck, ps.DeckCardInstanceIds, ps.DeckCardDataIds);
        Add(map, owner, ZoneType.Hand, ps.HandCardInstanceIds, ps.HandCardDataIds);
        Add(map, owner, ZoneType.Graveyard, ps.GraveCardInstanceIds, ps.GraveCardDataIds);
        Add(map, owner, ZoneType.ResourceDeck, ps.ResourceDeckCardInstanceIds, ps.ResourceDeckCardDataIds);
        Add(map, owner, ZoneType.ResourceZone, ps.ResourceZoneCardInstanceIds, ps.ResourceZoneCardDataIds);
    }

    private void Add(Dictionary<string, ZoneMembership> map, Player owner, ZoneType zone,
                     string[] instanceIds, string[] dataIds)
    {
        if (instanceIds == null || dataIds == null) return;

        int count = Mathf.Min(instanceIds.Length, dataIds.Length);
        for (int i = 0; i < count; i++)
        {
            Card card = ResolveCard(instanceIds[i], dataIds[i]);
            if (card == null) continue;

            map[instanceIds[i]] = new ZoneMembership { Owner = owner, Zone = zone, Card = card };
        }
    }

    private void CollectFieldZones(Dictionary<string, ZoneMembership> map, BoardState state)
    {
        if (state.FieldCards == null) return;

        foreach (CardState cs in state.FieldCards)
        {
            if (cs == null) continue;

            Card card = ResolveCard(cs.InstanceId, cs.CardDataId);
            if (card == null) continue;

            Player owner = cs.OwnerRole == "GUEST" ? _mine : _foe;
            if (owner == null) continue;

            if (!TryParseZone(cs.Zone, out ZoneType zone)) continue;

            _desiredReveal[cs.InstanceId] = cs.IsRevealed;
            map[cs.InstanceId] = new ZoneMembership { Owner = owner, Zone = zone, Card = card };
        }
    }

    // ─── 진행 로그 ──────────────────────────────────────────────────────
    //
    // 호스트는 엔진이 OnLogMessage를 직접 쏘지만 게스트에는 그 엔진이 없다.
    // 스냅샷 차이에서 읽어낼 수 있는 것만이라도 좌측 로그에 남겨 준다.

    private void LogCardMove(Card card, Player owner, ZoneType from, ZoneType to)
    {
        string who = LocalPlayerContext.IsMine(owner) ? "나" : "상대";

        if (to == ZoneType.SetZone)
            EventManager.OnLogMessage?.Invoke($"{who}: 카드를 세트했습니다.");
        else if (from == ZoneType.SetZone && to == ZoneType.Graveyard)
            EventManager.OnLogMessage?.Invoke($"{who}: '{card.Name}' 폐기");
        else if (to == ZoneType.StackZone)
            EventManager.OnLogMessage?.Invoke($"{who}: '{card.Name}' 스택존에 대기");
        else if (to == ZoneType.BattlefieldZone)
            EventManager.OnLogMessage?.Invoke($"{who}: '{card.Name}' 전장에 배치");
        else if (from == ZoneType.Deck && to == ZoneType.Hand)
            EventManager.OnLogMessage?.Invoke($"{who}: 카드를 뽑았습니다.");
    }

    private void LogLifeChange(Player owner, int before, int after)
    {
        if (before == int.MinValue || before == after) return;

        string who = LocalPlayerContext.IsMine(owner) ? "나" : "상대";
        int delta = after - before;

        EventManager.OnLogMessage?.Invoke(delta < 0
            ? $"<color=#ff6b6b>{who}: 라이프 {delta} (남은 {after})</color>"
            : $"<color=#7ee787>{who}: 라이프 +{delta} (현재 {after})</color>");
    }

    private void SyncNumbers(BoardState state)
    {
        if (_mine != null && state.GuestState != null)
        {
            if (state.GuestState.LifeToken != _lastMyLife)
            {
                int before = _lastMyLife;
                _lastMyLife = state.GuestState.LifeToken;
                EventManager.OnLifeChange?.Invoke(_mine, _lastMyLife);
                LogLifeChange(_mine, before, _lastMyLife);
            }

            if (state.GuestState.ResourceZoneCount != _lastMyResource)
            {
                _lastMyResource = state.GuestState.ResourceZoneCount;
                EventManager.OnResourceChange?.Invoke(_mine, _lastMyResource);
            }
        }

        if (_foe != null && state.HostState != null)
        {
            if (state.HostState.LifeToken != _lastFoeLife)
            {
                int before = _lastFoeLife;
                _lastFoeLife = state.HostState.LifeToken;
                EventManager.OnLifeChange?.Invoke(_foe, _lastFoeLife);
                LogLifeChange(_foe, before, _lastFoeLife);
            }

            if (state.HostState.ResourceZoneCount != _lastFoeResource)
            {
                _lastFoeResource = state.HostState.ResourceZoneCount;
                EventManager.OnResourceChange?.Invoke(_foe, _lastFoeResource);
            }
        }
    }

    // ─── 게스트 입력 ────────────────────────────────────────────────────
    //
    // ★ 로컬 사람이 쓰는 UI를 그대로 재사용한다.
    //   보드 UI는 EventManager의 OnRequireSetPhaseAction / OnRequireOpenPhaseAction을 구독해
    //   손패 선택과 [공개]/[폐기] 흐름을 띄운다. 게스트에는 그 이벤트를 쏠 엔진이 없으므로
    //   board_state의 페이즈 전환을 보고 여기서 **같은 이벤트를 합성**한다.
    //   콜백이 오면 엔진 대신 session_game_manage의 공개 전송 API로 호스트에 보낸다.
    //
    //   덕분에 세트 잔상 미리보기·버튼·확대까지 로컬과 완전히 같은 UI가 동작한다.

    private void RequestInputIfNeeded(BoardState state)
    {
        if (_client == null || _mine == null || state.IsGameOver) return;
        if (string.IsNullOrEmpty(state.CurrentPhase)) return;

        // 같은 턴·같은 페이즈에서는 한 번만 띄운다
        if (state.CurrentPhase == _inputRequestedForPhase && state.CurrentTurn == _inputRequestedForTurn) return;

        if (state.CurrentPhase == GamePhase.SetPhase.ToString())
        {
            _inputRequestedForPhase = state.CurrentPhase;
            _inputRequestedForTurn = state.CurrentTurn;
            RequestSetPhase();
        }
        else if (state.CurrentPhase == GamePhase.OpenPhase.ToString())
        {
            _inputRequestedForPhase = state.CurrentPhase;
            _inputRequestedForTurn = state.CurrentTurn;
            RequestOpenPhase();
        }
    }

    /// <summary>세트 페이즈: 손패에서 카드를 고르게 하고 선택을 호스트로 보낸다.</summary>
    private void RequestSetPhase()
    {
        if (_mine.Hand.Count == 0) return;

        EventManager.OnRequireSetPhaseAction?.Invoke(_mine, _ctx, chosen =>
        {
            if (chosen == null || string.IsNullOrEmpty(chosen.InstanceId))
            {
                Debug.LogWarning("[GuestBoard] 세트 선택이 비어 있다 — 전송하지 않는다.");
                return;
            }

            Debug.Log($"[GuestBoard] 세트 전송: {chosen.Name} ({chosen.InstanceId})");
            _client.SendSetPhaseChoice(chosen.InstanceId, true);
        });
    }

    /// <summary>오픈 페이즈: 세트한 카드를 공개할지 폐기할지 묻고 결과를 보낸다.</summary>
    private void RequestOpenPhase()
    {
        Card setCard = _mine.SetZoneCard;
        if (setCard == null)
        {
            // 아직 세트 결과가 스냅샷에 반영되지 않았을 수 있다. 다음 스냅샷에서 다시 시도한다
            _inputRequestedForPhase = null;
            return;
        }

        int cost = GameLogicHelpers.GetEffectiveCost(setCard, _mine);

        EventManager.OnRequireOpenPhaseAction?.Invoke(_mine, setCard, cost, _ctx, choice =>
        {
            string payload = choice == OpenPhaseChoice.Open ? "Open" : "Abandon";
            Debug.Log($"[GuestBoard] 오픈 전송: {setCard.Name} → {payload}");
            _client.SendOpenPhaseChoice(setCard.InstanceId, payload);
        });
    }

    private static readonly ZoneType[] AllZones =
    {
        ZoneType.Deck, ZoneType.Hand, ZoneType.Graveyard, ZoneType.ResourceDeck,
        ZoneType.ResourceZone, ZoneType.StackZone, ZoneType.SetZone, ZoneType.BattlefieldZone
    };

    /// <summary>카드를 어느 존에 있든 찾아서 뺀다. 같은 카드가 두 존에 겹쳐 남는 것을 막는다.</summary>
    private static void RemoveFromAnyZone(Player owner, Card card)
    {
        foreach (ZoneType zone in AllZones)
            owner.ExtractCard(zone, card);
    }

    private static readonly HashSet<string> UnknownZonesLogged = new HashSet<string>();

    /// <summary>
    /// board_state의 Zone 문자열을 엔진 ZoneType으로 바꾼다.
    ///
    /// ★ 서버(EventService)가 쓰는 값과 enum 이름이 다르다 — <b>"Stack"</b>, <b>"Battlefield"</b>로 쓴다
    ///   (`ZoneType.StackZone` / `BattlefieldZone`이 아니다). 둘 다 받는다.
    /// ★ 모르는 값을 폐기존으로 떨어뜨리면 안 된다. 실제로 그렇게 만들어 뒀다가
    ///   게스트 화면에서 스택 카드가 스택존에 머물지 못하고 곧장 폐기존으로 가 버렸다.
    ///   모르면 <c>false</c>를 돌려주고 그 카드는 이번 스냅샷에서 건드리지 않는다.
    /// </summary>
    private static bool TryParseZone(string zone, out ZoneType parsed)
    {
        switch (zone)
        {
            case "SetZone": parsed = ZoneType.SetZone; return true;

            case "Stack":
            case "StackZone": parsed = ZoneType.StackZone; return true;

            case "Battlefield":
            case "BattlefieldZone": parsed = ZoneType.BattlefieldZone; return true;

            case "Hand": parsed = ZoneType.Hand; return true;
            case "Deck": parsed = ZoneType.Deck; return true;
            case "Graveyard": parsed = ZoneType.Graveyard; return true;
            case "ResourceZone": parsed = ZoneType.ResourceZone; return true;
            case "ResourceDeck": parsed = ZoneType.ResourceDeck; return true;
        }

        parsed = default; // false를 돌려주므로 호출부는 이 값을 쓰지 않는다

        if (!string.IsNullOrEmpty(zone) && UnknownZonesLogged.Add(zone))
            Debug.LogWarning($"[GuestBoard] 모르는 존 문자열: '{zone}' — 이 카드는 건너뛴다.");

        return false;
    }

    private static string PhaseLabel(string phase)
    {
        switch (phase)
        {
            case "ResourcePhase": return "자원 페이즈";
            case "DrawPhase": return "드로우 페이즈";
            case "SetPhase": return "세트 페이즈";
            case "OpenPhase": return "오픈 페이즈";
            case "MainPhase": return "메인 페이즈";
            case "EndPhase": return "엔드 페이즈";
            default: return phase;
        }
    }
}
