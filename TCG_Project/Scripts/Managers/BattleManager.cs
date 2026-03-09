// BattleManager.cs — 최신 엔진 코어 동기화 (SBA + 동적 큐 + 스마트 스택 AI)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCG_Project.Scripts.Abilities;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Manager;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("타이밍 설정")]
    [Tooltip("카드 효과 사이 대기 시간 (초)")]
    public float ActionDelay = 0.5f;
    [Tooltip("페이즈 전환 대기 시간 (초)")]
    public float PhaseDelay = 1.0f;

    // 시스템 객체
    private GameDataManager _dataManager;
    private MatchManager _matchManager;

    // 플레이어
    private Player p1;
    private Player p2;
    private GameContext context;

    // 게임 상태
    private int _globalTurn = 1;
    private Player _currentGameWinner = null;

    // 오픈 페이즈에서 공개된 카드 (→ 메인 페이즈로 전달)
    private Card _p1RevealedCard = null;
    private Card _p2RevealedCard = null;

    // 듀얼 캐릭터: P1(ELLI+VERO 5:5), P2(DAIN+SONI 5:5)
    private const string P1_CHAR1 = "ELLI-01";
    private const string P1_CHAR2 = "VERO-01";
    private const string P2_CHAR1 = "DAIN-01";
    private const string P2_CHAR2 = "SONI-01";

    // ─── Unity 라이프사이클 ───────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        EventManager.OnGameSet += HandleGameSet;
        EventManager.OnGameDraw += HandleGameDraw;
    }

    private void OnDisable()
    {
        EventManager.OnGameSet -= HandleGameSet;
        EventManager.OnGameDraw -= HandleGameDraw;
    }

    private void HandleGameSet(Player winner)
    {
        _currentGameWinner = winner;
        context.IsGameOver = true;
    }

    private void HandleGameDraw(Player p1, Player p2, int turn)
    {
        context.IsGameOver = true;
    }

    private void Start()
    {
        EventManager.OnLogMessage += msg => Debug.Log(msg);
        InitializeSystem();

        // Player 객체 생성 (매치 전체 재사용)
        p1 = new Player { Name = "Player1", Type = UserType.Human, CharacterCardId = P1_CHAR1, SecondaryCharacterId = P1_CHAR2 };
        p2 = new Player { Name = "Bot_AI", Type = UserType.Bot, CharacterCardId = P2_CHAR1, SecondaryCharacterId = P2_CHAR2 };
        p1.InitializeBrain();
        p2.InitializeBrain();

        StartCoroutine(MatchLoop());
    }

    // ─── 시스템 초기화 ───────────────────────────────────────────────

    private void InitializeSystem()
    {
        string primaryPath = Path.Combine(Application.dataPath, "Resources", "GameData");
        string dataPath = Directory.Exists(primaryPath) ? primaryPath : "./Data";

        try { GameRules.LoadRules(Path.Combine(dataPath, "CommonConfig.json")); }
        catch (Exception e)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[Rules Error] {e.Message}</color>");
        }

        _dataManager = new GameDataManager();
        _dataManager.LoadAllData(dataPath);
        _dataManager.LoadRulebookCards(dataPath);
        _dataManager.LoadCharacterCards(dataPath);
        _dataManager.LoadResourceCards(dataPath);

        EventManager.OnLogMessage?.Invoke($"<color=cyan>[System] 데이터 로드 완료 (경로: {dataPath})</color>");
    }

    // ─── 매치 루프 (3판 2선승) ───────────────────────────────────────

    private IEnumerator MatchLoop()
    {
        _matchManager = new MatchManager(gamesToWin: 2, maxGames: 3);

        while (!_matchManager.IsMatchOver())
        {
            _currentGameWinner = null;
            yield return StartCoroutine(RunSingleGame());

            _matchManager.RecordResult(_currentGameWinner, p1, p2);
            EventManager.OnLogMessage?.Invoke($"  현재 전적: {_matchManager.GetStatusString(p1, p2)}");
            yield return new WaitForSeconds(PhaseDelay * 2);
        }

        Player matchWinner = _matchManager.GetMatchWinner(p1, p2);
        if (matchWinner != null)
            EventManager.OnMatchSet?.Invoke(matchWinner);
        else
            EventManager.OnMatchDraw?.Invoke(p1, p2);
    }

    // ─── 단일 게임 및 SBA 글로벌 심판 ───────────────────────────────────────

    /// <summary>
    /// 글로벌 심판: 현재 상태를 검사하여 누군가의 HP가 0이라면 게임 오버를 선언합니다.
    /// 어느 시점에서든 호출할 수 있는 전천후 상태 기반 행동(SBA) 체크포인트입니다.
    /// </summary>
    private bool CheckAndHandleGameOver()
    {
        if (context.IsGameOver) return true;

        bool p1Dead = p1.LifeTokens <= 0;
        bool p2Dead = p2.LifeTokens <= 0;

        if (p1Dead && p2Dead)
        {
            context.IsGameOver = true;
            EventManager.OnLogMessage?.Invoke("\n⚔️ 양측 플레이어의 라이프가 동시에 0이 되었습니다! (무승부)");
            EventManager.OnGameDraw?.Invoke(p1, p2, _globalTurn);
            return true;
        }
        else if (p1Dead)
        {
            context.IsGameOver = true;
            EventManager.OnGameSet?.Invoke(p2); // p2 승리
            return true;
        }
        else if (p2Dead)
        {
            context.IsGameOver = true;
            EventManager.OnGameSet?.Invoke(p1); // p1 승리
            return true;
        }

        return false;
    }

    private IEnumerator RunSingleGame()
    {
        InitializeSingleGame();
        _globalTurn = 1;

        while (!context.IsGameOver && _globalTurn <= 20)
        {
            EventManager.OnTurnStart?.Invoke(_globalTurn, "양측");
            _p1RevealedCard = null;
            _p2RevealedCard = null;

            yield return StartCoroutine(ExecuteResourcePhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            yield return StartCoroutine(ExecuteDrawPhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            yield return StartCoroutine(ExecuteSetPhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            yield return StartCoroutine(ExecuteOpenPhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            yield return StartCoroutine(ExecuteMainPhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            yield return StartCoroutine(ExecuteEndPhaseRoutine());
            if (CheckAndHandleGameOver()) break;

            EventManager.OnTurnEnd?.Invoke("양측");
            _globalTurn++;
        }

        if (!context.IsGameOver && _globalTurn > 20)
        {
            context.IsGameOver = true;
            EventManager.OnGameDraw?.Invoke(p1, p2, 20);
        }
    }

    private void InitializeSingleGame()
    {
        var deck1 = CreateDualCharacterDeck(P1_CHAR1, P1_CHAR2, 5, 5);
        var resDeck1 = CreateResourceDeck();
        var deck2 = CreateDualCharacterDeck(P2_CHAR1, P2_CHAR2, 5, 5);
        var resDeck2 = CreateResourceDeck();

        var result1 = DeckValidator.ValidateFullDeckSet(deck1, resDeck1, p1.CharacterCardId, p1.SecondaryCharacterId);
        if (!result1.IsValid)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[덱 오류] {p1.Name}: {result1.ErrorMessage}</color>");
            context = new GameContext { IsGameOver = true };
            return;
        }

        var result2 = DeckValidator.ValidateFullDeckSet(deck2, resDeck2, p2.CharacterCardId, p2.SecondaryCharacterId);
        if (!result2.IsValid)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[덱 오류] {p2.Name}: {result2.ErrorMessage}</color>");
            context = new GameContext { IsGameOver = true };
            return;
        }

        context = new GameContext();
        context.Players.Add(p1);
        context.Players.Add(p2);

        p1.ResetForNewGame(deck1, resDeck1);
        p2.ResetForNewGame(deck2, resDeck2);

        GameLogicHelpers.DrawCards(p1, GameRules.StartingHands, context);
        GameLogicHelpers.DrawCards(p2, GameRules.StartingHands, context);

        EventManager.OnGameStart?.Invoke(p1, p2);
        EventManager.OnLogMessage?.Invoke($"\n[초기] {p1.Name} — 라이프:{p1.LifeTokens} / 덱:{p1.Deck.Count} / 자원덱:{p1.ResourceDeck.Count} / 패:{p1.Hand.Count}");
        EventManager.OnLogMessage?.Invoke($"[초기] {p2.Name} — 라이프:{p2.LifeTokens} / 덱:{p2.Deck.Count} / 자원덱:{p2.ResourceDeck.Count} / 패:{p2.Hand.Count}");
    }

    // ─── 페이즈 1: 자원 페이즈 ──────────────────────────────────────

    private IEnumerator ExecuteResourcePhaseRoutine()
    {
        context.CurrentPhase = GamePhase.ResourcePhase;
        EventManager.OnResourcePhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 자원 페이즈 ]");

        p1.ApplyNextTurnBuffs();
        p2.ApplyNextTurnBuffs();

        context.ActivePlayer = p1; context.TargetPlayer = p2;
        p1.TakeResourceCard();
        if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p1, context)) yield break;

        context.ActivePlayer = p2; context.TargetPlayer = p1;
        p2.TakeResourceCard();
        if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p2, context)) yield break;

        yield return new WaitForSeconds(ActionDelay);
    }

    // ─── 페이즈 2: 드로우 페이즈 ─────────────────────────────────────

    private IEnumerator ExecuteDrawPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.DrawPhase;
        EventManager.OnDrawPhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 드로우 페이즈 ]");

        context.ActivePlayer = p1; context.TargetPlayer = p2;
        if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p1, context)) yield break;
        context.ActivePlayer = p2; context.TargetPlayer = p1;
        if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p2, context)) yield break;

        yield return StartCoroutine(ExecuteDrawForPlayer(p1));
        yield return StartCoroutine(ExecuteDrawForPlayer(p2));

        EventManager.OnLogMessage?.Invoke($"{p1.Name} 패: {p1.Hand.Count}장 | {string.Join(", ", p1.Hand.Select(c => c.Name))}");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} 패: {p2.Hand.Count}장 | {string.Join(", ", p2.Hand.Select(c => c.Name))}");

        yield return new WaitForSeconds(ActionDelay);
    }

    private IEnumerator ExecuteDrawForPlayer(Player player)
    {
        bool drawHandledByAbility = false;

        foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
        {
            if (ability.CanUse(player, context))
            {
                bool done = false;
                ability.OnDrawPhase(player, context, used =>
                {
                    drawHandledByAbility = used;
                    done = true;
                });
                yield return new WaitUntil(() => done);
                if (drawHandledByAbility) break;
            }
        }

        if (!drawHandledByAbility)
        {
            GameLogicHelpers.DrawCards(player, GameRules.DrawPerTurn, context);
        }
    }

    // ─── 페이즈 3: 세트 페이즈 ───────────────────────────────────────

    private IEnumerator ExecuteSetPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.SetPhase;
        EventManager.OnSetPhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 세트 페이즈 ]");

        yield return StartCoroutine(CheckPreSetAbilities(p1));
        yield return StartCoroutine(CheckPreSetAbilities(p2));

        yield return StartCoroutine(PerformSetCard(p1));
        yield return StartCoroutine(PerformSetCard(p2));

        EventManager.OnLogMessage?.Invoke($"{p1.Name} 세트존: {(p1.SetZoneCard != null ? "세트됨" : "없음")}");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} 세트존: {(p2.SetZoneCard != null ? "세트됨" : "없음")}");

        yield return new WaitForSeconds(ActionDelay);
    }

    private IEnumerator CheckPreSetAbilities(Player player)
    {
        foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
        {
            if (ability.CanUse(player, context))
            {
                bool done = false;
                ability.OnSetPhase(player, context, _ => done = true);
                yield return new WaitUntil(() => done);
            }
        }
    }

    private IEnumerator PerformSetCard(Player player)
    {
        if (player.Hand.Count == 0) yield break;

        Card cardToSet;
        if (player.Type == UserType.Bot)
        {
            cardToSet = player.Brain.ChooseSetCard(player, context);
        }
        else
        {
            bool done = false;
            Card chosen = null;
            EventManager.OnRequireSetPhaseAction?.Invoke(player, context, card =>
            {
                chosen = card;
                done = true;
            });
            yield return new WaitUntil(() => done);
            cardToSet = chosen;
        }

        if (cardToSet != null)
            player.SetCard(cardToSet);
    }

    // ─── 페이즈 4: 오픈 페이즈 ───────────────────────────────────────

    private IEnumerator ExecuteOpenPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.OpenPhase;
        EventManager.OnOpenPhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 오픈 페이즈 ]");

        context.ClearOpenPhaseStates();

        yield return StartCoroutine(PerformOpenOrAbandon(p1, result =>
        {
            _p1RevealedCard = result;
            context.OpenPhaseStates[p1.Name] = new PlayerOpenPhaseState { HasOpened = (result != null), RevealedCard = result };
        }));

        yield return StartCoroutine(PerformOpenOrAbandon(p2, result =>
        {
            _p2RevealedCard = result;
            context.OpenPhaseStates[p2.Name] = new PlayerOpenPhaseState { HasOpened = (result != null), RevealedCard = result };
        }));
    }

    private IEnumerator PerformOpenOrAbandon(Player player, Action<Card> onResult)
    {
        if (player.SetZoneCard == null) { onResult(null); yield break; }

        Card setCard = player.SetZoneCard;
        int effectiveCost = GameLogicHelpers.GetEffectiveCost(setCard, player);

        OpenPhaseChoice choice;
        if (player.Type == UserType.Bot)
        {
            choice = player.Brain.ChooseOpenOrAbandon(player, setCard, effectiveCost, context);
        }
        else
        {
            bool done = false;
            OpenPhaseChoice chosen = OpenPhaseChoice.Abandon;
            EventManager.OnRequireOpenPhaseAction?.Invoke(player, setCard, effectiveCost, context, c =>
            {
                chosen = c;
                done = true;
            });
            yield return new WaitUntil(() => done);
            choice = chosen;
        }

        if (choice == OpenPhaseChoice.Abandon)
        {
            player.AbandonSetCard();
            GameLogicHelpers.DrawCards(player, 1, context);

            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
            {
                if (ability.CanUse(player, context))
                {
                    bool done = false;
                    ability.OnOpenPhaseAbandon(player, context, _ => done = true);
                    yield return new WaitUntil(() => done);
                }
            }
            onResult(null);
        }
        else
        {
            onResult(player.RevealSetCard());
        }
    }

    // ─── 페이즈 5: 메인 페이즈 (동적 큐 + 완벽한 SBA 대응) ────────────────────

    private IEnumerator ExecuteMainPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.MainPhase;
        EventManager.OnMainPhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 메인 페이즈 ]");

        var pendingQueue = new List<(Player player, Player enemy, Card card)>();
        if (_p1RevealedCard != null) pendingQueue.Add((p1, p2, _p1RevealedCard));
        if (_p2RevealedCard != null) pendingQueue.Add((p2, p1, _p2RevealedCard));

        if (pendingQueue.Count == 0)
        {
            EventManager.OnLogMessage?.Invoke("공개된 카드가 없습니다. 메인 페이즈 생략.");
            yield break;
        }

        while (pendingQueue.Count > 0)
        {
            var groups = SpeedResolver.GroupByResolutionOrder(pendingQueue);
            var currentGroup = groups[0];

            foreach (var item in currentGroup)
                pendingQueue.Remove(item);

            foreach (var (player, enemy, card) in currentGroup)
            {
                context.ActivePlayer = player;
                context.TargetPlayer = enemy;

                if (card.Cost > 0 && !player.PayCost(GameLogicHelpers.GetEffectiveCost(card, player)))
                {
                    EventManager.OnLogMessage?.Invoke($"{player.Name}: [{card.Name}] 코스트 지불 실패 → 효과 취소");
                    player.ExtractCard(ZoneType.SetZone, card);
                    player.InsertCard(ZoneType.Graveyard, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);
                    continue;
                }

                // 스택 반응 (AI 최적화 로직 적용)
                yield return StartCoroutine(HandleStackActivation(enemy, player, card));

                // 카드 사용 방송 (UI 연출)
                EventManager.OnPlayCard?.Invoke(player, card);
                EventManager.OnLogMessage?.Invoke($"{player.Name}: [{card.Name}] 발동 (Speed: {card.Speed}, Type: {card.Type})");

                player.PlayingCard = card;
                context.LastEffectSucceeded = true;
                bool effectDone = false;
                
                // 오픈 즉발 효과만 실행 (IsStackAction = false)
                card.Play(context, () => effectDone = true, isStackTrigger: false);
                yield return new WaitUntil(() => effectDone);
                
                player.PlayingCard = null;

                // 카드 이동 처리
                if (card.IsStack)
                {
                    player.AddToStackZone(card);
                    player.ExtractCard(ZoneType.SetZone, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.StackZone);
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 스택존에 대기 상태로 전환.");
                }
                else if (player.BattlefieldCard == card)
                {
                    player.InsertCard(ZoneType.BattlefieldZone, card);
                    player.ExtractCard(ZoneType.SetZone, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.BattlefieldZone);
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 전장존에 배치됨.");
                }
                else if (player.ResourceZone.Contains(card))
                {
                    player.InsertCard(ZoneType.ResourceZone, card);
                    player.ExtractCard(ZoneType.SetZone, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.ResourceZone);
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 자원존에 배치됨.");
                }
                else
                {
                    player.ExtractCard(ZoneType.SetZone, card);
                    player.InsertCard(ZoneType.Graveyard, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);

                    // 캐릭터 후속 능력 검사 (일반 공격 카드 사용 시 패에서 다음 공격 카드 탐색 등)
                    foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
                    {
                        Card followUpCard = ability.OnMainPhaseAfterAttack(player, card, enemy, context);
                        if (followUpCard != null)
                        {
                            pendingQueue.Add((player, enemy, followUpCard));
                        }
                    }
                }
                
                yield return new WaitForSeconds(ActionDelay);
            } // 동시 그룹 1회 완료

            // 그룹 단위로 처리 후 글로벌 심판 (SBA)
            if (CheckAndHandleGameOver())
            {
                EventManager.OnLogMessage?.Invoke("최후의 일격으로 교전이 중단되었습니다!");
                break;
            }
        }
    }

    /// <summary>
    /// 스마트 스택 AI 및 발동 코루틴
    /// </summary>
    private IEnumerator HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard)
    {
        if (stackOwner.StackZone.Count == 0 || stackOwner.IsInvincible) yield break;

        int incomingHits = 0;
        bool isPiercingAttack = false;

        if (playedCard.Type == CardType.Attack)
        {
            foreach (var effect in playedCard.Effects)
            {
                if (effect is DamageEffect dmgEffect)
                {
                    incomingHits += dmgEffect.Times;
                    if (dmgEffect.isPiercing) isPiercingAttack = true;
                }
            }
        }

        if (incomingHits == 0) yield break;

        Player originalActive = context.ActivePlayer;
        Player originalTarget = context.TargetPlayer;
        int activatedCount = 0;

        foreach (var stackCard in new List<Card>(stackOwner.StackZone))
        {
            if (activatedCount >= incomingHits) break;

            bool canBlock = false;
            if (stackCard.Type == CardType.Defense)
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

            bool activate = false;

            if (stackOwner.Type == UserType.Bot)
            {
                if (canBlock) activate = true; 
            }
            else if (canBlock)
            {
                // 플레이어가 방어할 수 있다면 의사를 물어봄 (UI 구현 시 사용)
                bool done = false;
                bool chosen = false;
                EventManager.OnRequireStackResponse?.Invoke(stackOwner, stackCard, playedCard, result =>
                {
                    chosen = result;
                    done = true;
                });
                yield return new WaitUntil(() => done);
                activate = chosen;
            }

            if (activate)
            {
                context.ActivePlayer = stackOwner;
                context.TargetPlayer = cardPlayer;

                EventManager.OnLogMessage?.Invoke($"{stackOwner.Name}: [{stackCard.Name}] 스택 발동! (← 상대: [{playedCard.Name}])");

                bool done = false;
                context.LastEffectSucceeded = true;
                
                // 스택 반응이므로 isStackTrigger = true 로 실행
                stackCard.Play(context, () => done = true, isStackTrigger: true);
                yield return new WaitUntil(() => done);

                stackOwner.UseAndDiscardStack(stackCard);
                activatedCount++;
                yield return new WaitForSeconds(ActionDelay);
            }
        }

        context.ActivePlayer = originalActive;
        context.TargetPlayer = originalTarget;
    }

    // ─── 페이즈 6: 엔드 페이즈 ──────────────────────────────────────

    private IEnumerator ExecuteEndPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.EndPhase;
        EventManager.OnEndPhase?.Invoke("양측", _globalTurn);
        EventManager.OnLogMessage?.Invoke("[ 엔드 페이즈 ]");

        EventManager.OnLogMessage?.Invoke($"{p1.Name} — 라이프:{p1.LifeTokens} / 자원:{p1.ResourceZone.Count} / 패:{p1.Hand.Count} / 덱:{p1.Deck.Count}");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} — 라이프:{p2.LifeTokens} / 자원:{p2.ResourceZone.Count} / 패:{p2.Hand.Count} / 덱:{p2.Deck.Count}\n");

        if (!context.IsGameOver)
        {
            bool p1DeckOut = p1.Deck.Count == 0;
            bool p2DeckOut = p2.Deck.Count == 0;

            if (p1DeckOut && p2DeckOut)
            {
                EventManager.OnLogMessage?.Invoke("양측 덱 동시 고갈 → 타이브레이커 판정!");
                context.IsGameOver = true;
                ResolveSimultaneousDeckout();
            }
            else if (p1DeckOut)
            {
                EventManager.OnLogMessage?.Invoke($"{p1.Name}: 덱 고갈 → 패배");
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p2);
            }
            else if (p2DeckOut)
            {
                EventManager.OnLogMessage?.Invoke($"{p2.Name}: 덱 고갈 → 패배");
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p1);
            }
        }

        p1.ClearCombatBuffs();
        p2.ClearCombatBuffs();

        yield return new WaitForSeconds(PhaseDelay);
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────

    private void ResolveSimultaneousDeckout()
    {
        int result = DeckValidator.ResolveTiebreaker(p1, p2);

        if (result > 0)
        {
            EventManager.OnLogMessage?.Invoke($"타이브레이커: {p1.Name} 승리");
            _currentGameWinner = p1;
            EventManager.OnGameSet?.Invoke(p1);
        }
        else if (result < 0)
        {
            EventManager.OnLogMessage?.Invoke($"타이브레이커: {p2.Name} 승리");
            _currentGameWinner = p2;
            EventManager.OnGameSet?.Invoke(p2);
        }
        else
        {
            bool p1WinsToss = UnityEngine.Random.Range(0, 2) == 0;
            Player tossWinner = p1WinsToss ? p1 : p2;
            EventManager.OnLogMessage?.Invoke($"6단계 모두 동일 → 코인토스! {tossWinner.Name} 승리");
            _currentGameWinner = tossWinner;
            EventManager.OnGameSet?.Invoke(tossWinner);
        }
    }

    private List<Card> CreateDeckFromIds(string[] ids)
    {
        var deck = new List<Card>();
        foreach (string id in ids)
        {
            if (_dataManager.AllCards.TryGetValue(id, out Card c))
                for (int i = 0; i < 2; i++) deck.Add(c.Clone());
        }
        return deck;
    }

    private List<Card> CreateDualCharacterDeck(string charId1, string charId2, int count1, int count2)
    {
        var ids1 = _dataManager.GetEffectCardIdsForCharacter(charId1);
        var ids2 = _dataManager.GetEffectCardIdsForCharacter(charId2);
        var ids = ids1.Take(count1).Concat(ids2.Take(count2)).ToArray();
        return CreateDeckFromIds(ids);
    }

    private List<Card> CreateResourceDeck()
    {
        var deck = new List<Card>();
        if (!_dataManager.AllCards.TryGetValue("RES-01", out Card template))
        {
            for (int i = 0; i < GameRules.ResourceDeckCount; i++)
                deck.Add(new Card { Id = $"res_{i:000}", DataId = "RES-01", Name = "자원 카드", Type = CardType.Resource, Cost = 0, Speed = CardSpeed.None });
            return deck;
        }
        for (int i = 0; i < GameRules.ResourceDeckCount; i++)
            deck.Add(template.Clone());
        return deck;
    }
}