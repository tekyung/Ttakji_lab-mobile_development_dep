using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCG_Project.Scripts.Abilities;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Manager;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;
using ServerScripts.EventScripts;
using UnityEngine;

public class ServerGameManager : MonoBehaviour
{
    public static ServerGameManager Instance;
    public GameContext context;

    public int CurrentTurn = 1;

    // 유저의 입력을 기다리기 위한 대기 상태 보관소
    private Dictionary<string, Card> setPhaseChoices = new Dictionary<string, Card>();
    private int openPhaseResponseCount = 0;
    private bool _isFinalizingSetPhase = false;
    private bool _isFinalizingOpenPhase = false;

    // 오픈 페이즈에서 공개한 카드를 메인 페이즈까지 전달
    private Card _p1RevealedCard = null;
    private Card _p2RevealedCard = null;
    private string _winnerRole = null;
    private string _resultMessage = null;
    private bool _isWaitingForStackResponse = false;
    private string _pendingStackResponder = null;
    private string _pendingStackCardInstanceId = null;
    private string _pendingOpponentCardInstanceId = null;
    private bool _pendingStackChoiceReceived = false;
    private bool _pendingStackChoiceUse = false;
    private MatchManager _matchManager;
    private Player _hostPlayer;
    private Player _guestPlayer;
    private List<Card> _hostMainDeckTemplate = new List<Card>();
    private List<Card> _hostResourceDeckTemplate = new List<Card>();
    private List<Card> _guestMainDeckTemplate = new List<Card>();
    private List<Card> _guestResourceDeckTemplate = new List<Card>();
    private bool _isMatchOver = false;
    private bool _isResolvingGameOver = false;
    private bool _hasRecordedCurrentGameResult = false;
    private int _currentGameIndex = 0;

    public string WinnerRole => _winnerRole;
    public string ResultMessage => _resultMessage;

    private void Awake()
    {
        //if (GameData.MyRole != "HOST")
        //{
        //    Destroy(this.gameObject);
        //    return;
        //}
        Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnLogMessage += HandleLogMessage;
        EventManager.OnGameSet += HandleGameSet;
        EventManager.OnGameDraw += HandleGameDraw;
    }

    private void OnDisable()
    {
        EventManager.OnLogMessage -= HandleLogMessage;
        EventManager.OnGameSet -= HandleGameSet;
        EventManager.OnGameDraw -= HandleGameDraw;
    }

    private void HandleLogMessage(string msg)
    {
        Debug.Log(msg);
    }

    private void HandleGameSet(Player winner)
    {
        if (context == null || _isResolvingGameOver) return;

        context.IsGameOver = true;
        if (winner != null)
        {
            _winnerRole = winner.Name;
            _resultMessage = $"{winner.Name} 승리";
        }
        else if (string.IsNullOrEmpty(_resultMessage))
        {
            _resultMessage = "게임 종료";
        }

        StartCoroutine(HandleGameOverFlow(winner, false));
    }

    private void HandleGameDraw(Player p1, Player p2, int turn)
    {
        if (context == null || _isResolvingGameOver) return;

        context.IsGameOver = true;
        _winnerRole = "DRAW";
        if (string.IsNullOrEmpty(_resultMessage))
            _resultMessage = "무승부";

        StartCoroutine(HandleGameOverFlow(null, true));
    }

    // ==========================================================
    //  게임 시작 (세션 초기화 및 첫 턴 돌입)
    // ==========================================================
    public void StartMultiplayerGame(Player host, Player guest)
    {
        _hostPlayer = host;
        _guestPlayer = guest;
        _matchManager = new MatchManager(gamesToWin: 2, maxGames: 3);
        _isMatchOver = false;
        _currentGameIndex = 1;

        // 첫 게임 시작 전 덱 원본 스냅샷을 저장해 다음 게임 초기화에 재사용합니다.
        _hostMainDeckTemplate = CloneDeck(host != null ? host.Deck : null);
        _hostResourceDeckTemplate = CloneDeck(host != null ? host.ResourceDeck : null);
        _guestMainDeckTemplate = CloneDeck(guest != null ? guest.Deck : null);
        _guestResourceDeckTemplate = CloneDeck(guest != null ? guest.ResourceDeck : null);

        StartSingleGameRound();
    }

    private void StartSingleGameRound()
    {
        if (_hostPlayer == null || _guestPlayer == null)
        {
            Debug.LogError("[서버][매치] 플레이어 정보가 없어 게임을 시작할 수 없습니다.");
            return;
        }

        if (context == null) context = new GameContext();
        context.Players.Clear();
        context.Players.Add(_hostPlayer);
        context.Players.Add(_guestPlayer);
        context.ActivePlayer = null;
        context.TargetPlayer = null;
        context.CurrentTurn = 1;
        context.CurrentPhase = GamePhase.TurnStart;
        context.IsGameOver = false;
        context.LastEffectSucceeded = true;
        context.ClearOpenPhaseStates();
        context.PendingEffects.Clear();
        context.ClearVariables();

        CurrentTurn = 1; //  1턴 시작!
        _winnerRole = null;
        _resultMessage = null;
        _hasRecordedCurrentGameResult = false;
        setPhaseChoices.Clear();
        openPhaseResponseCount = 0;
        _isFinalizingSetPhase = false;
        _isFinalizingOpenPhase = false;
        _p1RevealedCard = null;
        _p2RevealedCard = null;
        ClearPendingStackResponse();

        _hostPlayer.ResetForNewGame(CloneDeck(_hostMainDeckTemplate), CloneDeck(_hostResourceDeckTemplate));
        _guestPlayer.ResetForNewGame(CloneDeck(_guestMainDeckTemplate), CloneDeck(_guestResourceDeckTemplate));

        // 시작 패 드로우
        GameLogicHelpers.DrawCards(_hostPlayer, GameRules.StartingHands, context);
        GameLogicHelpers.DrawCards(_guestPlayer, GameRules.StartingHands, context);

        EventManager.OnLogMessage?.Invoke("[서버] 게임이 시작되었습니다!");
        EventManager.OnLogMessage?.Invoke($"[서버][매치] 게임 {_currentGameIndex} 시작");
        EventManager.OnGameStart?.Invoke(_hostPlayer, _guestPlayer);

        // 첫 번째 턴, 자원 페이즈부터 시작!
        StartPhase(GamePhase.ResourcePhase);
    }

    private System.Collections.IEnumerator HandleGameOverFlow(Player gameWinner, bool isDraw)
    {
        _isResolvingGameOver = true;
        yield return StartCoroutine(SyncBoardStateAndWait());

        // 단판 모드 호환: 매치 정보가 없다면 여기서 종료합니다.
        if (_matchManager == null || _hostPlayer == null || _guestPlayer == null)
        {
            _isResolvingGameOver = false;
            yield break;
        }

        if (!_hasRecordedCurrentGameResult)
        {
            _matchManager.RecordResult(isDraw ? null : gameWinner, _hostPlayer, _guestPlayer);
            _hasRecordedCurrentGameResult = true;
            EventManager.OnLogMessage?.Invoke($"  현재 전적: {_matchManager.GetStatusString(_hostPlayer, _guestPlayer)}");
        }

        if (_matchManager.IsMatchOver())
        {
            _isMatchOver = true;
            Player matchWinner = _matchManager.GetMatchWinner(_hostPlayer, _guestPlayer);
            if (matchWinner != null)
            {
                _winnerRole = matchWinner.Name;
                _resultMessage = $"{matchWinner.Name} 매치 승리";
                EventManager.OnMatchSet?.Invoke(matchWinner);
            }
            else
            {
                _winnerRole = "DRAW";
                _resultMessage = "매치 무승부";
                EventManager.OnMatchDraw?.Invoke(_hostPlayer, _guestPlayer);
            }

            yield return StartCoroutine(SyncBoardStateAndWait());
            _isResolvingGameOver = false;
            yield break;
        }

        // 매치 미종료: 다음 게임으로 넘어갑니다.
        _currentGameIndex++;
        yield return new WaitForSeconds(1.0f);
        StartSingleGameRound();
        yield return StartCoroutine(SyncBoardStateAndWait());
        _isResolvingGameOver = false;
    }

    // ==========================================================
    // 페이즈 컨트롤러 (상태 머신)
    // ==========================================================
    private void StartPhase(GamePhase phase)
    {
        context.CurrentPhase = phase;

        // 매 페이즈 진입 시 누군가 죽었는지 체크
        if (CheckAndHandleGameOver(context.Players[0], context.Players[1]))
        {
            StartCoroutine(SyncBoardStateAndWait());
            return;
        }

        switch (phase)
        {
            case GamePhase.ResourcePhase:
                ExecuteResourcePhase();
                // ResourcePhase 결과가 반영된 board_state를 먼저 동기화한 뒤 DrawPhase로 넘어갑니다.
                StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.DrawPhase));
                break;

            case GamePhase.DrawPhase:
                ExecuteDrawPhase();
                // DrawPhase 결과를 동기화한 뒤 SetPhase로 이동합니다.
                StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.SetPhase));
                break;

            case GamePhase.SetPhase:
                StartCoroutine(BeginSetPhaseRoutine());
                break;

            case GamePhase.OpenPhase:
                StartCoroutine(BeginOpenPhaseRoutine());
                break;

            case GamePhase.MainPhase:
                EventManager.OnLogMessage?.Invoke(
                    $"[서버][MainPhase] 진입 turn={CurrentTurn} | " +
                    $"p1Revealed={(_p1RevealedCard != null ? _p1RevealedCard.InstanceId : "null")}, " +
                    $"p2Revealed={(_p2RevealedCard != null ? _p2RevealedCard.InstanceId : "null")}");
                StartCoroutine(ExecuteMainPhaseRoutine());
                break;

            case GamePhase.EndPhase:
                StartCoroutine(ExecuteEndPhaseRoutine());
                break;
        }
    }

    // ==========================================================
    //  클라이언트 응답 수신부 (EventService에서 호출됨)
    // ==========================================================

    private System.Collections.IEnumerator BeginSetPhaseRoutine()
    {
        setPhaseChoices.Clear();
        _isFinalizingSetPhase = false;

        EventManager.OnSetPhase?.Invoke("양측", CurrentTurn);

        bool p1AbilityDone = false;
        bool p2AbilityDone = false;
        StartCoroutine(CheckPreSetAbilities(context.Players[0], () => p1AbilityDone = true));
        StartCoroutine(CheckPreSetAbilities(context.Players[1], () => p2AbilityDone = true));
        yield return new WaitUntil(() => (p1AbilityDone && p2AbilityDone) || context == null || context.IsGameOver);

        if (context == null || context.IsGameOver || context.CurrentPhase != GamePhase.SetPhase)
            yield break;

        EventManager.OnRequireSetPhaseAction?.Invoke(context.Players[0], context, null);
        EventManager.OnRequireSetPhaseAction?.Invoke(context.Players[1], context, null);
        RequestBoardStateSync();

        float waitLimit = GameRules.ChooseWaitTime / 100f;
        float timer = 0f;
        while (context != null &&
               !context.IsGameOver &&
               context.CurrentPhase == GamePhase.SetPhase &&
               !_isFinalizingSetPhase &&
               setPhaseChoices.Count < context.Players.Count &&
               timer < waitLimit)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (context == null || context.IsGameOver || context.CurrentPhase != GamePhase.SetPhase || _isFinalizingSetPhase)
            yield break;

        if (setPhaseChoices.Count < context.Players.Count)
        {
            foreach (Player player in context.Players)
            {
                if (setPhaseChoices.ContainsKey(player.Name)) continue;

                Card autoCard = ChooseDefaultSetCard(player);
                setPhaseChoices[player.Name] = autoCard;

                EventManager.OnLogMessage?.Invoke(
                    autoCard != null
                        ? $"[서버][SetPhase] 응답 시간 초과({waitLimit:0.##}s) player={player.Name} -> 자동 세트 {autoCard.Name}[instance={autoCard.InstanceId},data={autoCard.DataId}]"
                        : $"[서버][SetPhase] 응답 시간 초과({waitLimit:0.##}s) player={player.Name} -> 세트할 카드 없음");
            }

            TryFinalizeSetPhase();
        }
    }

    private System.Collections.IEnumerator CheckPreSetAbilities(Player player, Action onDone)
    {
        foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
        {
            if (!ability.CanUse(player, context)) continue;

            bool done = false;
            ability.OnSetPhase(player, context, _ => done = true);
            yield return new WaitUntil(() => done || context == null || context.IsGameOver);

            if (context == null || context.IsGameOver)
                yield break;
        }

        onDone?.Invoke();
    }

    private Card ChooseDefaultSetCard(Player player)
    {
        if (player == null || player.Hand.Count == 0) return null;

        var affordableCards = player.Hand
            .Where(c => GameLogicHelpers.GetEffectiveCost(c, player) <= player.ResourceZone.Count)
            .ToList();
        var candidates = affordableCards.Count > 0 ? affordableCards : player.Hand;
        return candidates.OrderBy(c => Guid.NewGuid()).FirstOrDefault();
    }

    private void TryFinalizeSetPhase()
    {
        if (context == null || context.CurrentPhase != GamePhase.SetPhase || _isFinalizingSetPhase) return;
        if (setPhaseChoices.Count < context.Players.Count) return;

        _isFinalizingSetPhase = true;

        foreach (Player player in context.Players)
        {
            if (!setPhaseChoices.TryGetValue(player.Name, out Card finalCard))
            {
                EventManager.OnLogMessage?.Invoke($"[서버] 세트 확정 실패: {player.Name}의 카드 정보가 없습니다.");
                _isFinalizingSetPhase = false;
                return;
            }

            if (finalCard == null)
            {
                EventManager.OnLogMessage?.Invoke($"[서버][SetPhase] {player.Name}은 세트할 카드가 없어 이번 턴 세트를 생략합니다.");
                continue;
            }

            bool setSucceeded = player.SetCard(finalCard);
            if (!setSucceeded)
            {
                _isFinalizingSetPhase = false;
                return;
            }
        }

        EventManager.OnLogMessage?.Invoke("[서버] 양측 세트 완료 → board_state 동기화 후 OpenPhase 진입");
        StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.OpenPhase));
    }

    private System.Collections.IEnumerator BeginOpenPhaseRoutine()
    {
        openPhaseResponseCount = 0;
        _isFinalizingOpenPhase = false;
        _p1RevealedCard = null;
        _p2RevealedCard = null;
        context.ClearOpenPhaseStates();

        EventManager.OnOpenPhase?.Invoke("양측", CurrentTurn);

        int p1Cost = context.Players[0].SetZoneCard != null ? GameLogicHelpers.GetEffectiveCost(context.Players[0].SetZoneCard, context.Players[0]) : 0;
        int p2Cost = context.Players[1].SetZoneCard != null ? GameLogicHelpers.GetEffectiveCost(context.Players[1].SetZoneCard, context.Players[1]) : 0;

        EventManager.OnLogMessage?.Invoke(
            $"[서버][OpenPhase] 진입 turn={CurrentTurn} | " +
            $"{context.Players[0].Name}(set={(context.Players[0].SetZoneCard != null ? context.Players[0].SetZoneCard.InstanceId : "null")}, cost={p1Cost}) / " +
            $"{context.Players[1].Name}(set={(context.Players[1].SetZoneCard != null ? context.Players[1].SetZoneCard.InstanceId : "null")}, cost={p2Cost})");

        RequestBoardStateSync();

        if (context.Players[0].SetZoneCard != null)
            EventManager.OnRequireOpenPhaseAction?.Invoke(context.Players[0], context.Players[0].SetZoneCard, p1Cost, context, null);
        else
            OnPlayerOpenActionReceived(context.Players[0].Name, "Abandon", 0);

        if (context.Players[1].SetZoneCard != null)
            EventManager.OnRequireOpenPhaseAction?.Invoke(context.Players[1], context.Players[1].SetZoneCard, p2Cost, context, null);
        else
            OnPlayerOpenActionReceived(context.Players[1].Name, "Abandon", 0);

        float waitLimit = GameRules.ChooseWaitTime / 100f;
        float timer = 0f;
        while (context != null &&
               !context.IsGameOver &&
               context.CurrentPhase == GamePhase.OpenPhase &&
               !_isFinalizingOpenPhase &&
               openPhaseResponseCount < context.Players.Count &&
               timer < waitLimit)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (context == null || context.IsGameOver || context.CurrentPhase != GamePhase.OpenPhase || _isFinalizingOpenPhase)
            yield break;

        if (openPhaseResponseCount < context.Players.Count)
        {
            foreach (Player player in context.Players)
            {
                if (context.OpenPhaseStates.ContainsKey(player.Name)) continue;

                int effectiveCost = player.SetZoneCard != null ? GameLogicHelpers.GetEffectiveCost(player.SetZoneCard, player) : 0;
                EventManager.OnLogMessage?.Invoke(
                    $"[서버][OpenPhase] 응답 시간 초과({waitLimit:0.##}s) player={player.Name} -> 자동 Abandon");
                OnPlayerOpenActionReceived(player.Name, "Abandon", effectiveCost);
            }
        }
    }

    private void TryFinalizeOpenPhase()
    {
        if (context == null || context.CurrentPhase != GamePhase.OpenPhase || _isFinalizingOpenPhase) return;
        if (openPhaseResponseCount < context.Players.Count) return;

        _isFinalizingOpenPhase = true;
        EventManager.OnLogMessage?.Invoke(
            $"[서버] 양측 오픈 완료 → board_state 동기화 후 MainPhase 진입 | " +
            $"p1Revealed={(_p1RevealedCard != null ? _p1RevealedCard.InstanceId : "null")}, " +
            $"p2Revealed={(_p2RevealedCard != null ? _p2RevealedCard.InstanceId : "null")}");
        StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.MainPhase));
    }

    // [세트 페이즈 응답 도착]
    public void OnPlayerSetActionReceived(string playerName, string cardInstanceId)
    {
        // EventManager.OnLogMessage?.Invoke(
        //     $"[서버][SetPhase] 요청 수신 player={playerName}, cardInstanceId={cardInstanceId}, " +
        //     $"phase={(context != null ? context.CurrentPhase.ToString() : "null")}, collected={setPhaseChoices.Count}/{(context != null ? context.Players.Count : 0)}");

        if (context == null || context.CurrentPhase != GamePhase.SetPhase)
        {
            EventManager.OnLogMessage?.Invoke($"[서버] SetPhase가 아닌 상태에서 세트 요청을 무시했습니다. ({playerName})");
            return;
        }

        Player p = GetPlayerByName(playerName);
        if (p == null)
        {
            // EventManager.OnLogMessage?.Invoke($"[서버] 세트 요청 실패: 플레이어를 찾을 수 없습니다. ({playerName})");
            return;
        }

        if (setPhaseChoices.ContainsKey(playerName))
        {
            EventManager.OnLogMessage?.Invoke($"[서버] {playerName}의 중복 세트 요청을 무시했습니다.");
            return;
        }

        Card pickedCard = null;
        if (!string.IsNullOrEmpty(cardInstanceId))
            pickedCard = p.Hand.Find(c => c.InstanceId == cardInstanceId);

        if (!string.IsNullOrEmpty(cardInstanceId) && pickedCard == null)
        {
            string handList = p.Hand.Count == 0
                ? "empty"
                : string.Join(", ", p.Hand.Select(c => $"{c.Name}[instance={c.InstanceId},data={c.DataId}]"));
            // EventManager.OnLogMessage?.Invoke(
            //     $"[서버] 세트 요청 실패: 손패에서 카드를 찾을 수 없습니다. ({playerName}, {cardInstanceId}) | hand={handList}");
            return;
        }

        setPhaseChoices[playerName] = pickedCard;
        // EventManager.OnLogMessage?.Invoke(
        //     $"[서버] {playerName} 세트 제출 저장 완료. picked={cardInstanceId}, cardName={pickedCard.Name}, " +
        //     $"handBeforeCommit={p.Hand.Count}, setCount={setPhaseChoices.Count}/{context.Players.Count}");

        TryFinalizeSetPhase();
    }
    // [오픈 페이즈 응답 도착]
    public void OnPlayerOpenActionReceived(string playerName, string choice, int effectiveCost)
    {
        if (context == null || context.CurrentPhase != GamePhase.OpenPhase)
        {
            EventManager.OnLogMessage?.Invoke($"[서버] OpenPhase가 아닌 상태에서 오픈 요청을 무시했습니다. ({playerName})");
            return;
        }

        EventManager.OnLogMessage?.Invoke(
            $"[서버][OpenPhase] 요청 수신 player={playerName}, choice={choice}, " +
            $"effectiveCost={effectiveCost}, phase={(context != null ? context.CurrentPhase.ToString() : "null")}, " +
            $"openCount={openPhaseResponseCount}/{(context != null ? context.Players.Count : 0)}");

        Player p = GetPlayerByName(playerName);
        if (p == null)
        {
            EventManager.OnLogMessage?.Invoke($"[서버][OpenPhase] 요청 실패: 플레이어를 찾을 수 없습니다. ({playerName})");
            return;
        }

        if (context.OpenPhaseStates.ContainsKey(p.Name))
        {
            EventManager.OnLogMessage?.Invoke($"[서버][OpenPhase] {playerName}의 중복 오픈 요청을 무시했습니다.");
            return;
        }

        EventManager.OnLogMessage?.Invoke(
            $"[서버][OpenPhase] 처리 직전 player={playerName}, " +
            $"setBefore={(p.SetZoneCard != null ? p.SetZoneCard.InstanceId : "null")}, " +
            $"handBefore={p.Hand.Count}, graveBefore={p.Graveyard.Count}, resourceBefore={p.ResourceZone.Count}");

        Card revealedCard = null;

        if (p.SetZoneCard == null)
        {
            EventManager.OnLogMessage?.Invoke($"[서버][OpenPhase] {playerName}은 세트 카드가 없어 OpenPhase를 생략합니다.");
        }
        else if (choice == "Open")
        {
            // OpenPhase에서는 공개 가능 여부만 판단하고,
            // 실제 코스트 지불은 MainPhase 카드 해결 시점에 수행합니다.

            revealedCard = p.RevealSetCard();
            EventManager.OnLogMessage?.Invoke(
                $"[서버][OpenPhase] Open 처리 완료 player={playerName}, " +
                $"revealed={(revealedCard != null ? $"{revealedCard.Name}[instance={revealedCard.InstanceId},data={revealedCard.DataId}]" : "null")}, " +
                $"resourceAfter={p.ResourceZone.Count}, setAfter={(p.SetZoneCard != null ? p.SetZoneCard.InstanceId : "null")}");
        }
        else // "Abandon" (폐기)
        {
            p.AbandonSetCard();
            GameLogicHelpers.DrawCards(p, 1, context);

            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p))
            {
                if (ability.CanUse(p, context)) ability.OnOpenPhaseAbandon(p, context, _ => { });
            }

            EventManager.OnLogMessage?.Invoke(
                $"[서버][OpenPhase] Abandon 처리 완료 player={playerName}, " +
                $"handAfter={p.Hand.Count}, graveAfter={p.Graveyard.Count}, setAfter={(p.SetZoneCard != null ? p.SetZoneCard.InstanceId : "null")}");
        }

        context.OpenPhaseStates[p.Name] = new PlayerOpenPhaseState { HasOpened = (revealedCard != null), RevealedCard = revealedCard };

        if (p == context.Players[0]) _p1RevealedCard = revealedCard;
        else _p2RevealedCard = revealedCard;

        EventManager.OnLogMessage?.Invoke(
            $"[서버] {playerName} 오픈 선택 완료: {choice} (cost={effectiveCost}) " +
            $"openCount={openPhaseResponseCount + 1}/2");

        openPhaseResponseCount++;
        if (openPhaseResponseCount < context.Players.Count)
        {
            EventManager.OnLogMessage?.Invoke(
                $"[서버][OpenPhase] 아직 상대 선택 대기 중입니다. openCount={openPhaseResponseCount}/{context.Players.Count}");
        }

        TryFinalizeOpenPhase();
    }

    // ==========================================================
    //  페이즈별 내부 실행 로직 (기존 룰북 이식)
    // ==========================================================

    // ResourcePhase 결과가 반영된 board_state sync가 끝나기 전에는 다음 페이즈로 넘어가지 않도록 합니다.
    private System.Collections.IEnumerator SyncBoardStateThenStartPhase(GamePhase nextPhase)
    {
        System.Threading.Tasks.Task task = null;
        try
        {
            if (EventService.Instance != null)
            {
                task = EventService.Instance.SyncGameStateToFirebase(GameData.SessionCode);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ServerGameManager] board_state sync 실패: {e.Message}");
        }

        while (task != null && !task.IsCompleted)
        {
            yield return null;
        }

        StartPhase(nextPhase);
    }

    private System.Collections.IEnumerator SyncBoardStateAndWait()
    {
        System.Threading.Tasks.Task task = null;
        try
        {
            if (EventService.Instance != null)
            {
                task = EventService.Instance.SyncGameStateToFirebase(GameData.SessionCode);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ServerGameManager] board_state sync 실패: {e.Message}");
        }

        while (task != null && !task.IsCompleted)
        {
            yield return null;
        }
    }

    private async void RequestBoardStateSync()
    {
        try
        {
            if (EventService.Instance != null)
                await EventService.Instance.SyncGameStateToFirebase(GameData.SessionCode);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ServerGameManager] 즉시 board_state sync 실패: {e.Message}");
        }
    }

    private void ExecuteResourcePhase()
    {
        Player p1 = context.Players[0]; 
        Player p2 = context.Players[1];
        EventManager.OnResourcePhase?.Invoke("양측", CurrentTurn); // CurrentTurn 적용
         if (p1.BattlefieldCard != null || p2.BattlefieldCard != null)
            {
                EventManager.OnLogMessage?.Invoke("[ 적용 중인 전장 카드 ]");
                EventManager.OnLogMessage?.Invoke($"  {p1.Name} 전장: {(p1.BattlefieldCard != null ? p1.BattlefieldCard.Name : "없음")}");
                EventManager.OnLogMessage?.Invoke($"  {p2.Name} 전장: {(p2.BattlefieldCard != null ? p2.BattlefieldCard.Name : "없음")}");
            }

        p1.ApplyNextTurnBuffs();
        p2.ApplyNextTurnBuffs();

        context.ActivePlayer = p1; 
        context.TargetPlayer = p2;
        p1.TakeResourceCard();
        
        bool p1PhaseCut = GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p1, context);
        if (p1PhaseCut || context.IsGameOver) return;

        context.ActivePlayer = p2; context.TargetPlayer = p1;
        p2.TakeResourceCard();
        GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p2, context);
    }

    private void ExecuteDrawPhase()
    {
        Player p1 = context.Players[0];
        Player p2 = context.Players[1];

        EventManager.OnDrawPhase?.Invoke("양측", CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 드로우 페이즈 ]");

            // 전장 주기 효과 적용 — 매 드로우 페이즈 (VERO-11 체크메이트, DAIN-11 조선소, SONI-11 노을지는 활주로)
            context.ActivePlayer = p1; context.TargetPlayer = p2;
            if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p1, context)) return;

            context.ActivePlayer = p2; context.TargetPlayer = p1;
            if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p2, context)) return;

            // 캐릭터 능력: VERONICA(VERO-01) — 드로우 대신 덱 탑 3장 보기 → 1장 패로
            // p1 드로우 처리
            bool p1DrawHandled = false;
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p1))
            {
                if (ability.CanUse(p1, context))
                {
                    ability.OnDrawPhase(p1, context, used => p1DrawHandled = used);
                    if (p1DrawHandled) break;
                }
            }
            if (!p1DrawHandled) GameLogicHelpers.DrawCards(p1, GameRules.DrawPerTurn, context); // 기본 드로우 처리

            // p2 드로우 처리
            bool p2DrawHandled = false;
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p2))
            {
                if (ability.CanUse(p2, context))
                {
                    ability.OnDrawPhase(p2, context, used => p2DrawHandled = used);
                    if (p2DrawHandled) break;
                }
            }
            if (!p2DrawHandled) GameLogicHelpers.DrawCards(p2, GameRules.DrawPerTurn, context);

            EventManager.OnLogMessage?.Invoke($"{p1.Name} 패: {p1.Hand.Count}장 | {string.Join(", ", p1.Hand.Select(c => c.Name))}");
            EventManager.OnLogMessage?.Invoke($"{p2.Name} 패: {p2.Hand.Count}장 | {string.Join(", ", p2.Hand.Select(c => c.Name))}");
    }

    private System.Collections.IEnumerator ExecuteMainPhaseRoutine()
    {
        Player p1 = context.Players[0]; Player p2 = context.Players[1];
        EventManager.OnMainPhase?.Invoke("양측", CurrentTurn); // CurrentTurn 적용

        // phase=MainPhase 상태를 먼저 확정해서 클라이언트가 현재 페이즈를 정확히 받게 합니다.
        yield return StartCoroutine(SyncBoardStateAndWait());

        var pendingQueue = new List<(Player player, Player enemy, Card card)>();
        if (_p1RevealedCard != null) pendingQueue.Add((p1, p2, _p1RevealedCard));
        if (_p2RevealedCard != null) pendingQueue.Add((p2, p1, _p2RevealedCard));

        if (pendingQueue.Count == 0)
        {
            EventManager.OnLogMessage?.Invoke("[서버][MainPhase] 공개된 카드가 없어 메인 페이즈를 생략합니다.");
            yield return StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.EndPhase));
            yield break;
        }

        while (pendingQueue.Count > 0)
        {
            var groups = SpeedResolver.GroupByResolutionOrder(pendingQueue);
            var currentGroup = groups[0];

            foreach (var item in currentGroup) pendingQueue.Remove(item);

            foreach (var (player, enemy, card) in currentGroup)
            {
                yield return StartCoroutine(ResolveMainPhaseCard(player, enemy, card, pendingQueue));
                yield return StartCoroutine(SyncBoardStateAndWait());

                if (CheckAndHandleGameOver(p1, p2))
                {
                    yield return StartCoroutine(SyncBoardStateAndWait());
                    yield break;
                }
            }
        }

        EventManager.OnLogMessage?.Invoke("[서버][MainPhase] 처리 완료. board_state 동기화 후 EndPhase로 이동합니다.");
        yield return StartCoroutine(SyncBoardStateThenStartPhase(GamePhase.EndPhase));
    }

    private System.Collections.IEnumerator ResolveMainPhaseCard(Player player, Player enemy, Card card, List<(Player player, Player enemy, Card card)> pendingQueue)
    {
        context.ActivePlayer = player;
        context.TargetPlayer = enemy;

        EventManager.OnLogMessage?.Invoke(
            $"[서버][MainPhase] 카드 처리 시작 player={player.Name}, enemy={enemy.Name}, " +
            $"card={card.Name}[instance={card.InstanceId},data={card.DataId}], " +
            $"enemyLife={enemy.LifeTokens}, enemyArmor={enemy.ArmorBonus}, enemySuperArmor={enemy.SuperArmorBonus}, " +
            $"enemyInvincible={enemy.IsInvincible}, enemyStackArmor={enemy.StackArmors.Count}, " +
            $"enemyStackSuperArmor={enemy.StackSuperArmors.Count}, enemyStackInvincible={enemy.StackInvincibilities.Count}");

        int effectiveCost = GameLogicHelpers.GetEffectiveCost(card, player);
        if (effectiveCost > 0 && !player.PayCost(effectiveCost))
        {
            EventManager.OnLogMessage?.Invoke(
                $"[서버][MainPhase] 코스트 지불 실패 player={player.Name}, " +
                $"card={card.Name}[instance={card.InstanceId},data={card.DataId}], cost={effectiveCost}");

            player.ExtractCard(ZoneType.SetZone, card);
            player.InsertCard(ZoneType.Graveyard, card);
            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);
            yield break;
        }
        else if (effectiveCost == 0 && card.Cost > 0)
        {
            EventManager.OnLogMessage?.Invoke(
                $"[서버][MainPhase] 무료 발동 player={player.Name}, " +
                $"card={card.Name}[instance={card.InstanceId},data={card.DataId}]");
        }

        yield return StartCoroutine(HandleStackActivation(enemy, player, card));

        EventManager.OnPlayCard?.Invoke(player, card);

        player.PlayingCard = card;
        context.LastEffectSucceeded = true;
        bool effectDone = false;

        card.Play(context, () => effectDone = true, isStackTrigger: false);
        yield return new WaitUntil(() => effectDone || context == null || context.IsGameOver);
        player.PlayingCard = null;

        EventManager.OnLogMessage?.Invoke(
            $"[서버][MainPhase] 카드 처리 직후 player={player.Name}, enemy={enemy.Name}, " +
            $"card={card.Name}[instance={card.InstanceId},data={card.DataId}], " +
            $"enemyLifeAfterPlay={enemy.LifeTokens}, enemyArmorAfter={enemy.ArmorBonus}, " +
            $"enemySuperArmorAfter={enemy.SuperArmorBonus}, enemyInvincibleAfter={enemy.IsInvincible}, " +
            $"enemyStackArmorAfter={enemy.StackArmors.Count}, enemyStackSuperArmorAfter={enemy.StackSuperArmors.Count}, " +
            $"enemyStackInvincibleAfter={enemy.StackInvincibilities.Count}");

        if (card.IsStack)
        {
            player.AddToStackZone(card);
            player.ExtractCard(ZoneType.SetZone, card);
            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.StackZone);
        }
        else if (card.IsBattlefield)
        {
            player.ExtractCard(ZoneType.SetZone, card);
            player.PlaceBattlefield(card);
            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.BattlefieldZone);

            // 전장 카드가 메인 페이즈에 놓이면 즉시 1회 적용되는 효과를 맞춥니다.
            GameLogicHelpers.ApplyBattlefieldTurnEffects(player, context);
        }
        else if (player.ResourceZone.Contains(card))
        {
            player.ExtractCard(ZoneType.SetZone, card);
            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.ResourceZone);
        }
        else
        {
            player.ExtractCard(ZoneType.SetZone, card);
            player.InsertCard(ZoneType.Graveyard, card);
            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);

            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
            {
                bool abilityDone = false;
                ability.OnMainPhaseAfterAttack(player, card, enemy, context, followUpCard =>
                {
                    if (followUpCard != null)
                    {
                        pendingQueue.Add((player, enemy, followUpCard));
                        EventManager.OnLogMessage?.Invoke(
                            $"[서버][MainPhase] 후속 카드 큐 추가 player={player.Name}, " +
                            $"card={followUpCard.Name}[instance={followUpCard.InstanceId},data={followUpCard.DataId}]");
                    }

                    abilityDone = true;
                });

                // 엘리 능력 등에서 Optional/카드 선택/타임아웃 처리가 끝날 때까지 대기
                yield return new WaitUntil(() => abilityDone || context == null || context.IsGameOver);
            }
        }

        EventManager.OnLogMessage?.Invoke(
            $"[서버][MainPhase] 카드 처리 완료 player={player.Name}, enemy={enemy.Name}, " +
            $"card={card.Name}[instance={card.InstanceId},data={card.DataId}], " +
            $"enemyLifeFinal={enemy.LifeTokens}, playerGrave={player.Graveyard.Count}, " +
            $"playerStack={player.StackZone.Count}, playerBattlefield={(player.BattlefieldCard != null ? player.BattlefieldCard.InstanceId : "null")}");
        yield break;
    }

    private System.Collections.IEnumerator HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard)
    {
        if (stackOwner == null || cardPlayer == null || playedCard == null) yield break;
        if (stackOwner.StackZone.Count == 0 || stackOwner.IsInvincible) yield break;

        int incomingHits = 0;
        bool isPiercingAttack = false;

        foreach (var effect in playedCard.Effects)
        {
            if (effect is DamageEffect dmgEffect && !dmgEffect.TargetSelf)
            {
                incomingHits += dmgEffect.Times;
                if (dmgEffect.isPiercing) isPiercingAttack = true;
            }
        }

        if (incomingHits == 0) yield break;

        Player originalActive = context.ActivePlayer;
        Player originalTarget = context.TargetPlayer;
        List<Card> validStackCards = new List<Card>();
        foreach (var stackCard in stackOwner.StackZone)
        {
            bool canBlock = false;
            if (stackCard.Type == CardType.Defense || stackCard.Type == CardType.Support)
            {
                foreach (var effect in stackCard.Effects)
                {
                    if (effect is BuffEffect buffEffect)
                    {
                        if (isPiercingAttack)
                        {
                            if (buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                canBlock = true;
                        }
                        else
                        {
                            if (buffEffect.TypeOfBuff == BuffType.Armor || buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                canBlock = true;
                        }
                    }
                }
            }

            if (canBlock) validStackCards.Add(stackCard);
        }

        if (validStackCards.Count == 0) yield break;

        int requiredCount = Mathf.Min(incomingHits, validStackCards.Count);
        List<Card> selectedCards = new List<Card>();

        if (stackOwner.Type == UserType.Bot)
        {
            selectedCards = validStackCards.Take(requiredCount).ToList();
        }
        else
        {
            bool done = false;
            bool timeOutOccurred = false;

            EventManager.OnLogMessage?.Invoke(
                $"[서버][Stack] 카드 선택 대기 시작 responder={stackOwner.Name}, required={requiredCount}, " +
                $"candidates={string.Join(", ", validStackCards.Select(card => $"{card.Name}[instance={card.InstanceId},data={card.DataId}]"))}");

            EventManager.OnRequireCardPick?.Invoke(stackOwner, validStackCards, requiredCount, chosenCards =>
            {
                if (timeOutOccurred) return;
                selectedCards = chosenCards ?? new List<Card>();
                done = true;
            });

            yield return StartCoroutine(SyncBoardStateAndWait());

            float waitLimit = GameRules.ChooseWaitTime / 1000f;
            float timer = 0f;
            while (!done && timer < waitLimit && context != null && !context.IsGameOver)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!done)
            {
                timeOutOccurred = true;
                selectedCards = validStackCards.Take(requiredCount).ToList();
                EventManager.OnLogMessage?.Invoke(
                    $"[서버][Stack] 카드 선택 시간 초과({waitLimit:0.##}s) responder={stackOwner.Name} -> 자동 선택 " +
                    $"{string.Join(", ", selectedCards.Select(card => card.InstanceId))}");
            }
        }

        foreach (var stackCard in selectedCards)
        {
            if (stackCard == null || !stackOwner.StackZone.Contains(stackCard)) continue;

            context.ActivePlayer = stackOwner;
            context.TargetPlayer = cardPlayer;

            stackOwner.PlayingCard = stackCard;
            context.LastEffectSucceeded = true;

            EventManager.OnLogMessage?.Invoke(
                $"[서버][Stack] {stackOwner.Name}: [{stackCard.Name}] 스택 발동! (<= [{playedCard.Name}])");

            bool done = false;
            stackCard.Play(context, () => done = true, isStackTrigger: true);
            yield return new WaitUntil(() => done || context == null || context.IsGameOver);
            stackOwner.PlayingCard = null;

            stackOwner.UseAndDiscardStack(stackCard);
            EventManager.OnCardMove?.Invoke(stackCard, stackOwner, ZoneType.StackZone, stackOwner, ZoneType.Graveyard);
            yield return StartCoroutine(SyncBoardStateAndWait());
        }

        context.ActivePlayer = originalActive;
        context.TargetPlayer = originalTarget;
    }

    public void OnPlayerStackResponseReceived(string playerName, string stackCardInstanceId, string opponentCardInstanceId, bool isUsingStack)
    {
        EventManager.OnLogMessage?.Invoke(
            $"[서버][Stack] 응답 수신 player={playerName}, stackCard={stackCardInstanceId}, " +
            $"opponent={opponentCardInstanceId}, use={isUsingStack}, waiting={_isWaitingForStackResponse}");

        if (!_isWaitingForStackResponse)
        {
            EventManager.OnLogMessage?.Invoke("[서버][Stack] 대기 중이 아닌 응답이라 무시합니다.");
            return;
        }

        if (!string.Equals(playerName, _pendingStackResponder, StringComparison.Ordinal))
        {
            EventManager.OnLogMessage?.Invoke(
                $"[서버][Stack] 응답자 불일치로 무시합니다. expected={_pendingStackResponder}, actual={playerName}");
            return;
        }

        if (!string.Equals(stackCardInstanceId, _pendingStackCardInstanceId, StringComparison.Ordinal) ||
            !string.Equals(opponentCardInstanceId, _pendingOpponentCardInstanceId, StringComparison.Ordinal))
        {
            EventManager.OnLogMessage?.Invoke(
                $"[서버][Stack] 응답 대상 불일치로 무시합니다. expectedStack={_pendingStackCardInstanceId}, " +
                $"actualStack={stackCardInstanceId}, expectedOpponent={_pendingOpponentCardInstanceId}, actualOpponent={opponentCardInstanceId}");
            return;
        }

        _pendingStackChoiceUse = isUsingStack;
        _pendingStackChoiceReceived = true;
    }

    private void ClearPendingStackResponse()
    {
        _isWaitingForStackResponse = false;
        _pendingStackResponder = null;
        _pendingStackCardInstanceId = null;
        _pendingOpponentCardInstanceId = null;
        _pendingStackChoiceReceived = false;
        _pendingStackChoiceUse = false;
    }

    private void ExecuteEndPhase()
    {
        Player p1 = context.Players[0]; Player p2 = context.Players[1];
        EventManager.OnEndPhase?.Invoke("양측", CurrentTurn); //  CurrentTurn 적용

        bool p1DeckOut = p1.Deck.Count == 0;
        bool p2DeckOut = p2.Deck.Count == 0;

        if (p1DeckOut && p2DeckOut)
        {
            context.IsGameOver = true;
            ResolveSimultaneousDeckout(p1, p2, CurrentTurn); // CurrentTurn 적용
        }
        else if (p1DeckOut)
        {
            context.IsGameOver = true;
            EventManager.OnGameSet?.Invoke(p2);
        }
        else if (p2DeckOut)
        {
            context.IsGameOver = true;
            EventManager.OnGameSet?.Invoke(p1);
        }

        p1.ClearCombatBuffs();
        p2.ClearCombatBuffs();
    }

    private System.Collections.IEnumerator ExecuteEndPhaseRoutine()
    {
        EventManager.OnLogMessage?.Invoke($"[서버][EndPhase] 진입 turn={CurrentTurn}");

        // phase=EndPhase 상태를 먼저 보여줍니다.
        yield return StartCoroutine(SyncBoardStateAndWait());

        ExecuteEndPhase();

        EventManager.OnLogMessage?.Invoke(
            $"[서버][EndPhase] 처리 완료 turn={CurrentTurn} | " +
            $"hostLife={context.Players[0].LifeTokens}, guestLife={context.Players[1].LifeTokens}, " +
            $"hostDeck={context.Players[0].Deck.Count}, guestDeck={context.Players[1].Deck.Count}, " +
            $"isGameOver={context.IsGameOver}, winner={_winnerRole ?? "null"}");

        // 엔드 페이즈 결과(버프 정리/덱 고갈 판정/승부 결과)를 저장합니다.
        yield return StartCoroutine(SyncBoardStateAndWait());

        if (context.IsGameOver)
            yield break;

        // 턴 끝! 다음 턴 자원 페이즈로 이동
        CurrentTurn++;
        EventManager.OnTurnEnd?.Invoke("양측");
        EventManager.OnTurnStart?.Invoke(CurrentTurn, "양측");
        StartPhase(GamePhase.ResourcePhase);
    }

    // ==========================================================
    // 유틸리티 도구들
    // ==========================================================
    private bool CheckAndHandleGameOver(Player p1, Player p2)
    {
        if (context.IsGameOver) return true;

        bool p1Dead = p1.LifeTokens <= 0;
        bool p2Dead = p2.LifeTokens <= 0;

        if (p1Dead && p2Dead)
        {
            context.IsGameOver = true;
            _winnerRole = "DRAW";
            _resultMessage = "양측 라이프가 동시에 0이 되어 무승부입니다.";
            EventManager.OnGameDraw?.Invoke(p1, p2, CurrentTurn); //  CurrentTurn 적용
            return true;
        }
        else if (p1Dead)
        {
            context.IsGameOver = true;
            _winnerRole = p2.Name;
            _resultMessage = $"{p2.Name} 승리";
            EventManager.OnGameSet?.Invoke(p2);
            return true;
        }
        else if (p2Dead)
        {
            context.IsGameOver = true;
            _winnerRole = p1.Name;
            _resultMessage = $"{p1.Name} 승리";
            EventManager.OnGameSet?.Invoke(p1);
            return true;
        }
        return false;
    }

    private void ResolveSimultaneousDeckout(Player p1, Player p2, int turn)
    {
        int result = DeckValidator.ResolveTiebreaker(p1, p2);
        if (result > 0)
        {
            _winnerRole = p1.Name;
            _resultMessage = $"{p1.Name} 승리";
            EventManager.OnGameSet?.Invoke(p1);
        }
        else if (result < 0)
        {
            _winnerRole = p2.Name;
            _resultMessage = $"{p2.Name} 승리";
            EventManager.OnGameSet?.Invoke(p2);
        }
        else
        {
            Player randomWinner = new System.Random().Next(0, 2) == 0 ? p1 : p2;
            _winnerRole = randomWinner.Name;
            _resultMessage = $"{randomWinner.Name} 승리";
            EventManager.OnGameSet?.Invoke(randomWinner);
        }
    }

    private Player GetPlayerByName(string name) { return context.Players.Find(p => p.Name == name); }

    private static List<Card> CloneDeck(List<Card> source)
    {
        if (source == null) return new List<Card>();
        return source
            .Where(card => card != null)
            .Select(card => card.Clone())
            .ToList();
    }
}