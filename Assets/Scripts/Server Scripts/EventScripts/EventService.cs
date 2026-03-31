using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using ServerScripts.EventScripts;
using UnityEngine;


namespace ServerScripts.EventScripts
{
    public class EventService : MonoBehaviour
    {
        public static EventService Instance;

        private class PendingOptionalAction
        {
            public string PlayerName;
            public string ActionId;
            public string Message;
            public Action<bool> Callback;
        }

        private class PendingCardPickAction
        {
            public string PlayerName;
            public string RequestId;
            public List<Card> PresentedCards;
            public int RequiredCount;
            public Action<List<Card>> Callback;
        }

        private class PendingCardChoiceAction
        {
            public string PlayerName;
            public string RequestId;
            public ZoneType Zone;
            public string Filter;
            public List<Card> PresentedCards;
            public int RequiredCount;
            public Action<List<Card>> Callback;
        }

        private void Awake()
        {
            if (GameData.MyRole != "HOST")
            {
                this.enabled = false;
                return;
            }

            Instance = this;
        }

        // ==========================================
        //  게임 상태 및 변수들
        // ==========================================
        public firebase_network networkService;

        private GameContext context;
        private Player hostPlayer;
        private Player guestPlayer;

        private ServerGameManager _serverGameManager;

        private int currentTurnCount = 1;
        private bool isTurnEnded = false;
        private HashSet<string> revealedSetCards = new HashSet<string>();
        private readonly Dictionary<string, int> _lastKnownLifeByPlayer = new Dictionary<string, int>();
        private readonly Dictionary<string, PendingOptionalAction> _pendingOptionalActions = new Dictionary<string, PendingOptionalAction>();
        private readonly Dictionary<string, PendingCardPickAction> _pendingCardPickActions = new Dictionary<string, PendingCardPickAction>();
        private readonly Dictionary<string, PendingCardChoiceAction> _pendingCardChoiceActions = new Dictionary<string, PendingCardChoiceAction>();

        public void InitializeGame(GameContext gameContext, Player host, Player guest)
        {
            this.context = gameContext;
            this.hostPlayer = host;
            this.guestPlayer = guest;
            this.currentTurnCount = 1;
            this._serverGameManager = ServerGameManager.Instance;
            _lastKnownLifeByPlayer.Clear();
            _pendingOptionalActions.Clear();
            _pendingCardPickActions.Clear();
            _pendingCardChoiceActions.Clear();
            if (hostPlayer != null) _lastKnownLifeByPlayer[hostPlayer.Name] = hostPlayer.LifeTokens;
            if (guestPlayer != null) _lastKnownLifeByPlayer[guestPlayer.Name] = guestPlayer.LifeTokens;

            SubscribeToNotifications();
        }

        // ==========================================
        // 📮 파이어베이스 이벤트 중앙 처리소 (라우터)
        // ==========================================
        public async Task ProcessEvent(string sessionRoom, string action, string senderRole, string jsonData)
        {
            if (GameData.MyRole != "HOST" || context == null) return;

            Player senderPlayer = (senderRole == "HOST") ? hostPlayer : guestPlayer;
            Player targetPlayer = (senderRole == "HOST") ? guestPlayer : hostPlayer;

            try
            {
                // 16개의 모든 DTO 요청을 분류하고 검증
                switch (action)
                {
                    // ─── 1. 턴 / 페이즈 진행 ───
                    case "TurnEndRequest":
                        // 멀티플레이에서는 ServerGameManager가 페이즈 진행을 담당하므로,
                        // 여기서는 상태 변이를 하지 않고 동기화만 수행합니다.
                        await SyncGameStateToFirebase(sessionRoom);
                        break;

                    case "PhaseChangeRequest":
                        var phaseReq = JsonUtility.FromJson<PhaseChangeRequest>(jsonData);
                        Debug.Log($"[서버] {phaseReq.PlayerName}님의 페이즈 변경 요청: {phaseReq.TargetPhase}");

                        // ServerGameManager가 OpenPhase(양쪽 응답) 완료 후 자동으로 MainPhase를 시작합니다.
                        // 여기서는 상태 변이를 하지 않습니다.
                        if (phaseReq.TargetPhase == "MainPhase")
                            Debug.Log("[서버] PhaseChangeRequest(MainPhase)은 자동 진행이라 무시합니다.");
                        break;
                    // ─── 2. 기본 전투 및 카드 행동 ───
                    case "ResourceActionRequest":
                        var resReq = JsonUtility.FromJson<ResourceActionRequest>(jsonData);

                        if (!ActionValidator.ValidateOnResourcePhase(senderPlayer.Name, currentTurnCount, context, out string resError))
                        {
                            Debug.LogWarning($"[검증 실패] 자원 획득 거부: {resError}");
                            return;
                        }

                        // 통과했다면 실제 실행 함수로 넘깁니다.
                        await HandleResourceAction(sessionRoom, senderPlayer);
                        break;
                    case "PlayCardRequest":
                        var playReq = JsonUtility.FromJson<PlayCardRequest>(jsonData);
                        await HandlePlayCard(sessionRoom, senderPlayer, playReq.CardInstanceId);
                        break;

                    // ─── 3. Human Input (상세 UI 선택 응답) ───
                    case "SetPhaseActionRequest":
                        var setReq = JsonUtility.FromJson<SetPhaseActionRequest>(jsonData);
                        Debug.Log(
                            $"[서버][EventService] Set 요청 수신 sender={senderPlayer.Name}, " +
                            $"instanceId={setReq.PickedCardInstanceId}, confirmed={setReq.IsConfirmed}, " +
                            $"phase={(context != null ? context.CurrentPhase.ToString() : "null")}");

                        if (!setReq.IsConfirmed)
                        {
                            Debug.Log($"[서버] 세트 선택 중간 상태는 무시합니다. ({senderPlayer.Name})");
                            break;
                        }

                        Card pickedCard = GetCardById(senderPlayer, setReq.PickedCardInstanceId);
                        Debug.Log(
                            $"[서버][EventService] Set 검증 직전 sender={senderPlayer.Name}, " +
                            $"pickedCard={(pickedCard != null ? $"{pickedCard.Name}[instance={pickedCard.InstanceId},data={pickedCard.DataId}]" : "null")}, " +
                            $"handCount={(senderPlayer != null ? senderPlayer.Hand.Count : -1)}");

                        if (!ActionValidator.ValidateOnRequireSetPhaseAction(senderPlayer, context, GetCardById(senderPlayer, setReq.PickedCardInstanceId), out string setError))
                        {
                            Debug.LogWarning($"[검증 실패] 세트 거부: {setError}"); return;
                        }

                        Debug.Log($"[서버][EventService] Set 검증 통과 sender={senderPlayer.Name}, instanceId={setReq.PickedCardInstanceId}");
                        await HandleSetPhaseAction(sessionRoom, senderPlayer, pickedCard);
                        break;


                    case "OpenPhaseActionRequest":
                        var openReq = JsonUtility.FromJson<OpenPhaseActionRequest>(jsonData);
                        OpenPhaseChoice choiceEnum = (openReq.Choice == "Open") ? OpenPhaseChoice.Open : OpenPhaseChoice.Abandon;
                        int effectiveCost = GameLogicHelpers.GetEffectiveCost(senderPlayer.SetZoneCard, senderPlayer);

                        Debug.Log(
                            $"[서버][EventService] Open 요청 수신 sender={senderPlayer.Name}, " +
                            $"setCardId={openReq.SetCardInstanceId}, choice={openReq.Choice}, " +
                            $"effectiveCost={effectiveCost}, phase={(context != null ? context.CurrentPhase.ToString() : "null")}, " +
                            $"currentSet={(senderPlayer.SetZoneCard != null ? senderPlayer.SetZoneCard.InstanceId : "null")}");

                        if (!ActionValidator.ValidateOnRequireOpenPhaseAction(senderPlayer, senderPlayer.SetZoneCard, effectiveCost, context, choiceEnum, out string openError))
                        {
                            Debug.LogWarning($"[검증 실패] 오픈 거부: {openError}"); return;
                        }

                        Debug.Log(
                            $"[서버][EventService] Open 검증 통과 sender={senderPlayer.Name}, " +
                            $"choice={openReq.Choice}, setCardId={openReq.SetCardInstanceId}");
                        await HandleOpenPhaseAction(sessionRoom, senderPlayer, choiceEnum, effectiveCost);
                        break;

                    case "StackResponseRequest":
                        var stackReq = JsonUtility.FromJson<StackResponseRequest>(jsonData);
                        Card stackCard = GetCardFromAnywhere(stackReq.StackCardInstanceId);
                        Card oppCard = GetCardFromAnywhere(stackReq.OpponentCardInstanceId);

                        if (!ActionValidator.ValidateOnRequireStackResponse(senderPlayer, stackCard, oppCard, stackReq.IsUsingStack, context, out string stackError))
                        {
                            Debug.LogWarning($"[검증 실패] 스택 응답 거부: {stackError}"); return;
                        }

                        Debug.Log(
                            $"[서버][EventService] 스택 응답 수신 sender={senderPlayer.Name}, " +
                            $"stackCard={(stackCard != null ? $"{stackCard.Name}[instance={stackCard.InstanceId},data={stackCard.DataId}]" : stackReq.StackCardInstanceId)}, " +
                            $"opponentCard={(oppCard != null ? $"{oppCard.Name}[instance={oppCard.InstanceId},data={oppCard.DataId}]" : stackReq.OpponentCardInstanceId)}, " +
                            $"use={stackReq.IsUsingStack}");

                        _serverGameManager?.OnPlayerStackResponseReceived(
                            senderPlayer.Name,
                            stackReq.StackCardInstanceId,
                            stackReq.OpponentCardInstanceId,
                            stackReq.IsUsingStack);
                        break;

                    case "CardPickRequest":
                        var pickReq = JsonUtility.FromJson<CardPickRequest>(jsonData);
                        if (!_pendingCardPickActions.TryGetValue(pickReq.RequestId, out PendingCardPickAction pendingCardPick))
                        {
                            Debug.LogWarning($"[서버][CardPick] 대기 중인 요청을 찾지 못했습니다. requestId={pickReq.RequestId}");
                            return;
                        }

                        if (pendingCardPick.PlayerName != senderPlayer.Name)
                        {
                            Debug.LogWarning(
                                $"[서버][CardPick] 응답 플레이어 불일치. expected={pendingCardPick.PlayerName}, actual={senderPlayer.Name}, requestId={pickReq.RequestId}");
                            return;
                        }

                        List<Card> pickedCards = pickReq.PickedCardInstanceIds
                            .Select(id => GetCardFromAnywhere(id))
                            .Where(card => card != null)
                            .ToList();

                        if (!ActionValidator.ValidateOnRequireCardPick(
                                senderPlayer,
                                pickedCards,
                                pendingCardPick.RequiredCount,
                                pendingCardPick.PresentedCards,
                                context,
                                out string pickError))
                        {
                            Debug.LogWarning($"[검증 실패] 카드 픽 거부: {pickError}"); return;
                        }

                        _pendingCardPickActions.Remove(pickReq.RequestId);
                        Debug.Log(
                            $"[서버][CardPick] 응답 수신 sender={senderPlayer.Name}, requestId={pickReq.RequestId}, " +
                            $"picked={string.Join(", ", pickedCards.Select(c => c != null ? c.InstanceId : "null"))}");
                        pendingCardPick.Callback?.Invoke(pickedCards);
                        break;

                    case "CardChoiceRequest":
                        var choiceReq = JsonUtility.FromJson<CardChoiceRequest>(jsonData);
                        if (!_pendingCardChoiceActions.TryGetValue(choiceReq.RequestId, out PendingCardChoiceAction pendingCardChoice))
                        {
                            Debug.LogWarning($"[서버][CardChoice] 대기 중인 요청을 찾지 못했습니다. requestId={choiceReq.RequestId}");
                            return;
                        }

                        if (pendingCardChoice.PlayerName != senderPlayer.Name)
                        {
                            Debug.LogWarning(
                                $"[서버][CardChoice] 응답 플레이어 불일치. expected={pendingCardChoice.PlayerName}, actual={senderPlayer.Name}, requestId={choiceReq.RequestId}");
                            return;
                        }

                        List<Card> choicedCards = choiceReq.PickedCardInstanceIds
                            .Select(id => GetCardFromAnywhere(id))
                            .Where(card => card != null)
                            .ToList();
                        ZoneType parsedZone = (ZoneType)Enum.Parse(typeof(ZoneType), choiceReq.Zone);

                        if (parsedZone != pendingCardChoice.Zone)
                        {
                            Debug.LogWarning(
                                $"[서버][CardChoice] 응답 존 불일치. expected={pendingCardChoice.Zone}, actual={parsedZone}, requestId={choiceReq.RequestId}");
                            return;
                        }

                        foreach (var choiceCard in choicedCards)
                        {
                            if (!pendingCardChoice.PresentedCards.Contains(choiceCard))
                            {
                                Debug.LogWarning(
                                    $"[서버][CardChoice] 제시되지 않은 카드가 선택되었습니다. requestId={choiceReq.RequestId}, card={choiceCard.InstanceId}");
                                return;
                            }
                        }

                        if (!ActionValidator.ValidateOnRequireCardChoice(
                                senderPlayer,
                                parsedZone,
                                pendingCardChoice.RequiredCount,
                                pendingCardChoice.Filter,
                                choicedCards,
                                context,
                                out string choiceError))
                        {
                            Debug.LogWarning($"[검증 실패] 카드 서치 거부: {choiceError}"); return;
                        }

                        _pendingCardChoiceActions.Remove(choiceReq.RequestId);
                        Debug.Log(
                            $"[서버][CardChoice] 응답 수신 sender={senderPlayer.Name}, requestId={choiceReq.RequestId}, zone={parsedZone}, " +
                            $"picked={string.Join(", ", choicedCards.Select(c => c != null ? c.InstanceId : "null"))}");
                        pendingCardChoice.Callback?.Invoke(choicedCards);
                        break;

                    case "OptionalActionRequest":
                        var optReq = JsonUtility.FromJson<OptionalActionRequest>(jsonData);
                        if (!ActionValidator.ValidateOnRequireOptionalAction(senderPlayer, optReq.Choice, optReq.ActionId, context, out string optError))
                        {
                            Debug.LogWarning($"[검증 실패] 선택적 행동 응답 거부: {optError}"); return;
                        }

                        if (!_pendingOptionalActions.TryGetValue(optReq.ActionId, out PendingOptionalAction pendingOptional))
                        {
                            Debug.LogWarning($"[서버][Optional] 대기 중인 요청을 찾지 못했습니다. actionId={optReq.ActionId}");
                            return;
                        }

                        if (pendingOptional.PlayerName != senderPlayer.Name)
                        {
                            Debug.LogWarning(
                                $"[서버][Optional] 응답 플레이어 불일치. expected={pendingOptional.PlayerName}, actual={senderPlayer.Name}, actionId={optReq.ActionId}");
                            return;
                        }

                        _pendingOptionalActions.Remove(optReq.ActionId);
                        Debug.Log(
                            $"[서버][Optional] 응답 수신 sender={senderPlayer.Name}, actionId={optReq.ActionId}, choice={optReq.Choice}, message={pendingOptional.Message}");
                        pendingOptional.Callback?.Invoke(optReq.Choice);
                        break;

                    // ─── 4. 특수 요청 ───
                    case "CardDrawRequest":
                        var drawReq = JsonUtility.FromJson<CardDrawRequest>(jsonData);

                        // 🌟 대망의 드로우 페이즈 유효성 검사 연결!
                        if (!ActionValidator.ValidateOnDrawPhase(senderPlayer.Name, currentTurnCount, context, out string drawError))
                        {
                            Debug.LogWarning($"[검증 실패] 드로우 거부: {drawError}");
                            return; // 불법이면 컷!
                        }

                        // 통과했다면 실제 드로우 실행 함수로 넘깁니다.
                        await HandleDrawPhaseAction(sessionRoom, senderPlayer, drawReq.DrawCount);
                        break;

                    case "GameSetRequest":
                        var setGameReq = JsonUtility.FromJson<GameSetRequest>(jsonData);
                        Player winner = context.Players.Find(p => p.Name == setGameReq.WinnerName);
                        if (!ActionValidator.ValidateOnGameSet(winner, context, context.IsGameOver, out string gameSetError))
                        {
                            Debug.LogWarning($"[검증 실패] 게임 강제 종료 거부: {gameSetError}"); return;
                        }
                        context.IsGameOver = true;
                        EventManager.OnGameSet?.Invoke(winner);
                        break;

                    // ─── 5. 직접 수치/상태 제어 요청 ───
                    case "LifeChangeRequest":
                        {
                            var lifeReq = JsonUtility.FromJson<LifeChangeRequest>(jsonData);
                            Player lifeTarget = context.Players.Find(p => p.Name == lifeReq.TargetPlayerName);

                            if (!ActionValidator.ValidateOnLifeChange(lifeTarget, lifeReq.NewLife, context, out string lifeError))
                            {
                                Debug.LogWarning($"[검증 실패] 라이프 변경 거부: {lifeError}"); return;
                            }

                            // 여기서 현재 체력과 목표 체력의 차이를 계산합니다!
                            int difference = lifeTarget.LifeTokens - lifeReq.NewLife;

                            if (difference > 0)
                            {
                                // 현재 체력이 더 높다면 -> 데미지를 입은 것
                                lifeTarget.LoseLife(difference, context);
                            }
                            else if (difference < 0)
                            {
                                // 목표 체력이 더 높다면 -> 회복한 것
                                lifeTarget.GainLife(-difference); // 음수를 양수로 바꿔서 회복
                            }
                            // difference == 0 이면 아무것도 안 함

                            // 이하 라이프 0 체크 로직은 동일
                            if (lifeTarget.LifeTokens <= 0)
                            {
                                Player gameWinner = (lifeTarget == hostPlayer) ? guestPlayer : hostPlayer;
                                context.IsGameOver = true;
                                EventManager.OnGameSet?.Invoke(gameWinner);
                            }

                            await SyncGameStateToFirebase(sessionRoom);
                            break;
                        }

                    case "UnitStatusChangeRequest":
                        var statusReq = JsonUtility.FromJson<UnitStatusChangeRequest>(jsonData);
                        Card statusTargetCard = GetCardFromAnywhere(statusReq.TargetInstanceId);

                        if (statusTargetCard == null)
                        {
                            Debug.LogWarning($"[검증 실패] 상태이상 부여 거부: 카드를 찾을 수 없습니다. ({statusReq.TargetInstanceId})"); return;
                        }

                        // 카드 객체에 상태이상을 저장하는 속성/리스트가 있다면 업데이트
                        // 예: statusTargetCard.Status = statusReq.Status;
                        Debug.Log($"[서버] {statusTargetCard.Name}에 '{statusReq.Status}' 상태 부여 완료.");

                        await SyncGameStateToFirebase(sessionRoom);
                        break;
                    default:
                        //Debug.Log($"[서버 수신] 처리되지 않은 DTO 액션: {action}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventService 오류] {action} 처리 중 예외 발생: {e.Message}");
            }
        }

        // ==========================================
        //  개별 행동 처리 세부 로직들 (이전과 동일)
        // ==========================================

        private async Task HandleTurnEnd(string sessionRoom, string senderRole, Player sender)
        {
            // 1. 엔드 페이즈 자격 검증! (게임 오버 상태는 아닌지, 턴 플레이어가 맞는지)
            if (!ActionValidator.ValidateOnEndPhase(sender.Name, currentTurnCount, context, out string endPhaseError))
            {
                Debug.LogWarning($"[검증 실패] 엔드 페이즈 진입 불가: {endPhaseError}");
                return;
            }

            // 2. 턴 종료 자격 검증! (이미 턴이 넘어갔는데 또 누른 건 아닌지 중복 클릭 방지)
            if (!ActionValidator.ValidateOnTurnEnd(sender.Name, context.ActivePlayer, isTurnEnded, context.IsGameOver, out string turnEndError))
            {
                Debug.LogWarning($"[검증 실패] 턴 종료 거부: {turnEndError}");
                return;
            }

            await ExecuteTurnEndCleanup(sessionRoom, senderRole, sender);
        }

        private async Task HandlePlayCard(string sessionRoom, Player sender, string cardId)
        {
            Card card = GetCardById(sender, cardId);
            if (!ActionValidator.ValidateOnPlayCard(sender, card, context, out string error))
            {
                Debug.LogError($"[검증 실패] 카드 사용 거부: {error}"); 
                return;
            }
            sender.PayCost(card.Cost);
            sender.ExtractCard(ZoneType.Hand, card);
            if (card.IsBattlefield)
                sender.InsertCard(ZoneType.BattlefieldZone, card);
            else if (card.IsStack)
                sender.InsertCard(ZoneType.StackZone, card);
            else
                sender.InsertCard(ZoneType.Graveyard, card);

            EventManager.OnPlayCard?.Invoke(sender, card);
            await SyncGameStateToFirebase(sessionRoom);
        }
        private async Task HandleResourceAction(string sessionRoom, Player sender)
        {
            // 1. 자원 덱에서 맨 위 카드 1장을 뽑습니다.
            Card resourceCard = sender.ResourceDeck[0];

            // 2. 자원 덱에서 제거하고, 자원존(ResourceZone)으로 옮깁니다! (Player.cs의 이동 로직 활용)
            sender.ExtractCard(ZoneType.ResourceDeck, resourceCard);
            sender.InsertCard(ZoneType.ResourceZone, resourceCard);

            // 3. 자원 획득이 끝났으니, 다음 페이즈(드로우 페이즈 등)로 자동으로 넘깁니다.
            context.CurrentPhase = GamePhase.DrawPhase;

            await SyncGameStateToFirebase(sessionRoom);
        }
        private async Task HandleDrawPhaseAction(string sessionRoom, Player sender, int drawCount)
        {
            // 1. 지정된 장수만큼 카드를 뽑습니다.
            for (int i = 0; i < drawCount; i++)
            {
                if (sender.Deck.Count > 0)
                {
                    Card drawnCard = sender.Deck[0];

                    // 덱에서 제거하고 패(Hand)로 이동!
                    sender.ExtractCard(ZoneType.Deck, drawnCard);
                    sender.InsertCard(ZoneType.Hand, drawnCard);
                }
            }

            // 2. 드로우가 끝났으니, 다음 페이즈인 '세트 페이즈(Set Phase)'로 자동 이동합니다.
            context.CurrentPhase = GamePhase.SetPhase;

            await SyncGameStateToFirebase(sessionRoom);
        }

        private async Task HandleSetPhaseAction(string sessionRoom, Player sender, Card pickedCard)
        {
            // 상태 변경/페이즈 진행은 ServerGameManager가 담당합니다.
            _serverGameManager?.OnPlayerSetActionReceived(sender.Name, pickedCard.InstanceId);
            await Task.CompletedTask;
        }

        private async Task HandleOpenPhaseAction(string sessionRoom, Player sender, OpenPhaseChoice choice, int cost)
        {
            // 상태 변경/페이즈 진행은 ServerGameManager가 담당합니다.
            string choiceStr = (choice == OpenPhaseChoice.Open) ? "Open" : "Abandon";
            _serverGameManager?.OnPlayerOpenActionReceived(sender.Name, choiceStr, cost);
            await SyncGameStateToFirebase(sessionRoom);
        }

        private async Task HandleMainPhaseClash(string sessionRoom, bool hostHasCard, bool guestHasCard)
        {
            Card hostCard = hostHasCard ? hostPlayer.SetZoneCard : null;
            Card guestCard = guestHasCard ? guestPlayer.SetZoneCard : null;

            // =========================================================
            //  2. 필드 청소 (다 쓴 카드를 무덤으로 이동)
            // =========================================================
            if (hostHasCard)
            {
                hostPlayer.ExtractCard(ZoneType.SetZone, hostCard);
                hostPlayer.InsertCard(ZoneType.Graveyard, hostCard);
            }
            if (guestHasCard)
            {
                guestPlayer.ExtractCard(ZoneType.SetZone, guestCard);
                guestPlayer.InsertCard(ZoneType.Graveyard, guestCard);
            }

            // =========================================================
            //  3. 다음 페이즈로 이동 및 전광판 방송!
            // =========================================================
            context.CurrentPhase = GamePhase.EndPhase; // 전투가 끝났으니 엔드 페이즈로!
            await SyncGameStateToFirebase(sessionRoom);
        }

        private async Task ExecuteTurnEndCleanup(string sessionRoom, string senderRole, Player sender)
        {
            isTurnEnded = true;

            // 1. 엔드 페이즈 청소 
            sender.ClearCombatBuffs();
            revealedSetCards.Clear();
            // 2. 턴 넘김 처리
            string nextTurn = (senderRole == "HOST") ? "GUEST" : "HOST";
            context.ActivePlayer = (nextTurn == "HOST") ? hostPlayer : guestPlayer;

            // 턴 카운트를 올리고, 페이즈를 다시 처음(자원 페이즈)으로 되돌립니다!
            currentTurnCount++;
            context.CurrentPhase = GamePhase.ResourcePhase;

            isTurnEnded = false;

            // 3. 파이어베이스 턴 정보 갱신 및 전광판 방송!
            await networkService.ChangeTurn(sessionRoom, nextTurn);
            await SyncGameStateToFirebase(sessionRoom);
        }
        // ==========================================
        //  헬퍼 유틸리티 함수들
        // ==========================================

        private Card GetCardById(Player p, string id, bool fromField = false)
        {
            //if (p == null || string.IsNullOrEmpty(id)) return null;

            if (fromField)
            {
                // 1. 전장(Battlefield)에 있는 카드인지 확인
                if (p.BattlefieldCard != null && p.BattlefieldCard.InstanceId == id)
                    return p.BattlefieldCard;

                // 2. 스택존(StackZone)에 쌓여있는 카드들 중 하나인지 확인
                var stackCard = p.StackZone.FirstOrDefault(c => c != null && c.InstanceId == id);
                if (stackCard != null) return stackCard;

                // 3. 세트존(SetZone)에 엎어둔 카드인지 확인
                if (p.SetZoneCard != null && p.SetZoneCard.InstanceId == id)
                    return p.SetZoneCard;

                return null;
            }

            // 기본적으로는 패(Hand)에서 찾습니다
            return p.Hand.FirstOrDefault(c => c != null && c.InstanceId == id);
        }

        private Card GetCardFromAnywhere(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // 양측 플레이어의 모든 활성 구역을 합친 리스트 생성
            List<Card> allCards = new List<Card>();

            Action<Player> collect = (p) => {
                if (p == null) return;
                allCards.AddRange(p.Hand);          // 패
                allCards.AddRange(p.StackZone);     // 스택존
                if (p.BattlefieldCard != null) allCards.Add(p.BattlefieldCard); // 전장
                if (p.SetZoneCard != null) allCards.Add(p.SetZoneCard);         // 세트존
            };

            collect(hostPlayer);
            collect(guestPlayer);

            return allCards.FirstOrDefault(c => c != null && c.InstanceId == id);
        }


        public async Task SyncGameStateToFirebase(string sessionRoom)
        {
            BoardState boardData = new BoardState
            {
                CurrentTurn = _serverGameManager != null ? _serverGameManager.CurrentTurn : currentTurnCount,
                CurrentPhase = context.CurrentPhase.ToString(),
                ActivePlayer = context.ActivePlayer?.Name,
                IsGameOver = context != null && context.IsGameOver,
                WinnerRole = _serverGameManager != null ? _serverGameManager.WinnerRole : null,
                ResultMessage = _serverGameManager != null ? _serverGameManager.ResultMessage : null,
                HostState = ExtractPlayerState(hostPlayer),
                GuestState = ExtractPlayerState(guestPlayer)
            };

            // 양측 필드 및 세트존 카드를 모두 모아서 배열로 만듭니다.
            List<CardState> allFieldCards = new List<CardState>();
            allFieldCards.AddRange(ExtractCardStates(hostPlayer, "HOST"));
            allFieldCards.AddRange(ExtractCardStates(guestPlayer, "GUEST"));
            boardData.FieldCards = allFieldCards.ToArray();

            Debug.Log(
                $"[SyncGameStateToFirebase] turn={boardData.CurrentTurn}, phase={boardData.CurrentPhase}, active={boardData.ActivePlayer}, hostSet={(hostPlayer?.SetZoneCard != null ? hostPlayer.SetZoneCard.InstanceId : "null")}, guestSet={(guestPlayer?.SetZoneCard != null ? guestPlayer.SetZoneCard.InstanceId : "null")}");

            string json = JsonUtility.ToJson(boardData);
            await networkService.SyncBoardState(sessionRoom, json);
        }

        private PlayerState ExtractPlayerState(Player p)
        {
            if (p == null) return new PlayerState();
            return new PlayerState
            {
                LifeToken = p.LifeTokens,
                DeckCount = p.Deck.Count,
                HandCount = p.Hand.Count,
                GraveCount = p.Graveyard.Count,
                
                ResourceDeckCount = p.ResourceDeck.Count, 
                ResourceZoneCount = p.ResourceZone.Count,

                CurrentArmor = p.ArmorBonus,           
                CurrentSuperArmor = p.SuperArmorBonus, 
                CurrentFirepower = p.FirepowerBonus,   
                IsInvincible = p.IsInvincible,
                IsCounterActive = p.HasCounterAttack,

                // 디버깅/관찰용 카드 목록
                HandCardInstanceIds = p.Hand.Select(c => c.InstanceId).ToArray(),
                HandCardDataIds = p.Hand.Select(c => c.DataId).ToArray(),
                DeckCardInstanceIds = p.Deck.Select(c => c.InstanceId).ToArray(),
                DeckCardDataIds = p.Deck.Select(c => c.DataId).ToArray(),
                ResourceZoneCardInstanceIds = p.ResourceZone.Select(c => c.InstanceId).ToArray(),
                ResourceZoneCardDataIds = p.ResourceZone.Select(c => c.DataId).ToArray(),
                GraveCardInstanceIds = p.Graveyard.Select(c => c.InstanceId).ToArray(),
                GraveCardDataIds = p.Graveyard.Select(c => c.DataId).ToArray(),
                ResourceDeckCardInstanceIds = p.ResourceDeck.Select(c => c.InstanceId).ToArray(),
                ResourceDeckCardDataIds = p.ResourceDeck.Select(c => c.DataId).ToArray()
            };
        }

        private List<CardState> ExtractCardStates(Player p, string role)
        {
            List<CardState> list = new List<CardState>();
            if (p == null) return list;

            // 1. 전장 카드 (BattlefieldCard)
            if (p.BattlefieldCard != null)
            {
                list.Add(new CardState
                {
                    InstanceId = p.BattlefieldCard.InstanceId,
                    CardDataId = p.BattlefieldCard.DataId,
                    OwnerRole = role,
                    Zone = "Battlefield",
                    Status = "Active"
                });
            }

            // 2. 스택 카드들 (StackZone)
            foreach (var card in p.StackZone)
            {
                list.Add(new CardState
                {
                    InstanceId = card.InstanceId,
                    CardDataId = card.DataId,
                    OwnerRole = role,
                    Zone = "Stack",
                    Status = "Waiting"
                });
            }

            // 3. 세트존 카드 (SetZone)
            if (p.SetZoneCard != null)
            {
                bool isCardRevealed = false;
                if (context != null &&
                    context.OpenPhaseStates != null &&
                    context.OpenPhaseStates.TryGetValue(p.Name, out PlayerOpenPhaseState st))
                {
                    isCardRevealed =
                        st.HasOpened &&
                        st.RevealedCard != null &&
                        st.RevealedCard.InstanceId == p.SetZoneCard.InstanceId;
                }

                list.Add(new CardState
                {
                    InstanceId = p.SetZoneCard.InstanceId,
                    CardDataId = p.SetZoneCard.DataId, 
                    OwnerRole = role,
                    Zone = "SetZone",
                    Status = isCardRevealed ? "Revealed" : "Hidden",
                    IsRevealed = isCardRevealed // 장부 검사 결과 대입!
                });
            }
            return list;
        }
        
        private void SubscribeToNotifications()
        {
            // 1. 세트 페이즈 요구 알림 방송
            EventManager.OnRequireSetPhaseAction += async (player, ctx, callback) =>
            {
                var noti = new RequireSetPhaseNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    Message = "패에서 세트할 카드를 선택해 주세요."
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireSetPhaseNotification", noti);
            };

            // 2. 오픈 페이즈 요구 알림 방송
            EventManager.OnRequireOpenPhaseAction += async (player, setCard, cost, ctx, callback) =>
            {
                var noti = new RequireOpenPhaseNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    SetCardInstanceId = setCard?.InstanceId,
                    EffectiveCost = cost,
                    Message = $"비용({cost})을 지불하고 카드를 공개하시겠습니까?"
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireOpenPhaseNotification", noti);
            };

            // 3. 스택 방어 요구 알림 방송
            EventManager.OnRequireStackResponse += async (player, stackCard, oppCard, callback) =>
            {
                var noti = new RequireStackNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    StackCardInstanceId = stackCard?.InstanceId,
                    StackCardDataId = stackCard?.DataId,
                    OpponentCardInstanceId = oppCard?.InstanceId,
                    Message = $"상대가 공격했습니다! 스택 방어 카드를 발동하시겠습니까?"
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireStackNotification", noti);
            };

            // 4. 애니메이션/시각 연출 방송 (카드 이동)
            EventManager.OnCardMove += async (card, fromPlayer, fromZone, toPlayer, toZone) =>
            {
                var noti = new VisualEventNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = "ALL", // 연출은 호스트/게스트 양쪽 화면 모두에서 재생되어야 함
                    EventType = "CardMove",
                    CardInstanceId = card.InstanceId,
                    CardDataId = card.DataId,
                    OwnerRole = fromPlayer != null ? fromPlayer.Name : null,
                    FromZone = fromZone.ToString(),
                    ToZone = toZone.ToString()
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "VisualEventNotification", noti);
            };

            // 5. 애니메이션 연출 방송 (카드 발동 이펙트)
            EventManager.OnPlayCard += async (player, card) =>
            {
                var noti = new VisualEventNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = "ALL",
                    EventType = "PlayCard",
                    CardInstanceId = card.InstanceId,
                    CardDataId = card.DataId,
                    OwnerRole = player != null ? player.Name : null
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "VisualEventNotification", noti);
            };

            // 6. 드로우 연출 방송
            EventManager.OnCardDraw += async (card, player, fromZone) =>
            {
                var noti = new VisualEventNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = "ALL",
                    EventType = "CardDraw",
                    CardInstanceId = card.InstanceId,
                    CardDataId = card.DataId,
                    OwnerRole = player != null ? player.Name : null,
                    FromZone = fromZone.ToString(),
                    ToZone = ZoneType.Hand.ToString()
                };
                await networkService.SendRequestDTO(GameData.SessionCode, "VisualEventNotification", noti);
            };

            // 7. 선택적 행동 질문 알림 방송
            EventManager.OnRequireOptionalAction += async (player, message, ctx, callback) =>
            {
                if (player == null || callback == null) return;

                string actionId = Guid.NewGuid().ToString();
                _pendingOptionalActions[actionId] = new PendingOptionalAction
                {
                    PlayerName = player.Name,
                    ActionId = actionId,
                    Message = message,
                    Callback = callback
                };

                var noti = new RequireOptionalNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    ActionId = actionId,
                    Message = message
                };

                Debug.Log(
                    $"[서버][Optional] 질문 전송 target={player.Name}, actionId={actionId}, message={message}");
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireOptionalNotification", noti);
            };

            // 8. 카드 픽 질문 알림 방송
            EventManager.OnRequireCardPick += async (player, candidates, requiredCount, callback) =>
            {
                if (player == null || callback == null) return;

                string requestId = Guid.NewGuid().ToString();
                List<Card> presentedCards = candidates != null
                    ? candidates.Where(card => card != null).ToList()
                    : new List<Card>();

                _pendingCardPickActions[requestId] = new PendingCardPickAction
                {
                    PlayerName = player.Name,
                    RequestId = requestId,
                    PresentedCards = presentedCards,
                    RequiredCount = requiredCount,
                    Callback = callback
                };

                var noti = new RequireCardPickNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    RequestId = requestId,
                    PresentedCardInstanceIds = presentedCards.Select(card => card.InstanceId).ToArray(),
                    PresentedCardDataIds = presentedCards.Select(card => card.DataId).ToArray(),
                    RequiredCount = requiredCount,
                    Message = $"카드 {requiredCount}장을 선택하세요."
                };

                Debug.Log(
                    $"[서버][CardPick] 질문 전송 target={player.Name}, requestId={requestId}, " +
                    $"required={requiredCount}, candidates={string.Join(", ", presentedCards.Select(card => card.InstanceId))}");
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireCardPickNotification", noti);
            };

            // 9. 카드 초이스 질문 알림 방송
            EventManager.OnRequireCardChoice += async (player, zone, requiredCount, filter, callback) =>
            {
                if (player == null || callback == null) return;

                string requestId = Guid.NewGuid().ToString();
                List<Card> presentedCards = player.GetZone(zone) ?? new List<Card>();
                if (!string.IsNullOrEmpty(filter))
                {
                    presentedCards = presentedCards.Where(card => MatchesFilter(card, filter)).ToList();
                }

                _pendingCardChoiceActions[requestId] = new PendingCardChoiceAction
                {
                    PlayerName = player.Name,
                    RequestId = requestId,
                    Zone = zone,
                    Filter = filter,
                    PresentedCards = presentedCards,
                    RequiredCount = requiredCount,
                    Callback = callback
                };

                var noti = new RequireCardChoiceNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = player.Name,
                    RequestId = requestId,
                    Zone = zone.ToString(),
                    PresentedCardInstanceIds = presentedCards.Select(card => card.InstanceId).ToArray(),
                    PresentedCardDataIds = presentedCards.Select(card => card.DataId).ToArray(),
                    RequiredCount = requiredCount,
                    Filter = filter,
                    Message = $"{zone}에서 {requiredCount}장 선택"
                };

                Debug.Log(
                    $"[서버][CardChoice] 질문 전송 target={player.Name}, requestId={requestId}, zone={zone}, required={requiredCount}, " +
                    $"filter={filter}, candidates={string.Join(", ", presentedCards.Select(card => card.InstanceId))}");
                await networkService.SendRequestDTO(GameData.SessionCode, "RequireCardChoiceNotification", noti);
            };

            // 10. 라이프 변화 알림 방송
            EventManager.OnLifeChange += async (player, newLife) =>
            {
                if (player == null) return;

                int previousLife = _lastKnownLifeByPlayer.TryGetValue(player.Name, out int cachedLife)
                    ? cachedLife
                    : newLife;
                int delta = newLife - previousLife;
                _lastKnownLifeByPlayer[player.Name] = newLife;

                var noti = new LifeChangeNotification
                {
                    MatchId = GameData.SessionCode,
                    PlayerName = "ALL",
                    TargetPlayerName = player.Name,
                    NewLife = newLife,
                    Delta = delta,
                    Reason = delta < 0 ? "Damage" : delta > 0 ? "Heal" : "Sync"
                };

                await networkService.SendRequestDTO(GameData.SessionCode, "LifeChangeNotification", noti);
            };
        }

        private static bool MatchesFilter(Card card, string filter)
        {
            if (card == null || string.IsNullOrEmpty(filter))
                return true;

            foreach (var cond in filter.Split(','))
            {
                var parts = cond.Trim().Split(':');
                if (parts.Length != 2) continue;

                switch (parts[0].Trim())
                {
                    case "type":
                        if (Enum.TryParse(parts[1].Trim(), true, out CardType cardType) &&
                            card.Type != cardType)
                            return false;
                        break;
                    case "character":
                        if (card.CharacterId != parts[1].Trim())
                            return false;
                        break;
                }
            }

            return true;
        }
    }
}