// BattleManager.cs — 최신 엔진 코어 동기화 (SBA + 동적 큐 + 스마트 스택 AI + QA 난수 봇)
// [팀원 공유용] ConsoleRunner의 최신 아키텍처(Phase 18+)를 100% 반영한 유니티 매니저입니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using TCG_Project.Scripts.Abilities;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;
using UnityEngine;
using TCG_Project.Scripts.Interfaces;

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

    /// <summary>응답을 기다리는 '폐기 시 발동' 용병 능력 수 (다이나).</summary>
    private int _pendingAbandonAbilities = 0;

    /// <summary>응답을 기다리는 자원페이즈 전장 기동 효과 수 (ELLI-11).</summary>
    private int _pendingBattlefieldEffects = 0;
    private PlayerSetupData _p1Setup;
    private PlayerSetupData _p2Setup;

    // 게임 상태
    private Player _currentGameWinner = null;

    // 오픈 페이즈에서 공개된 카드 (→ 메인 페이즈로 전달)
    private Card _p1RevealedCard = null;
    private Card _p2RevealedCard = null;

    // ─── Unity 라이프사이클 ───────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        // 1. 게임 상태 이벤트 구독
        EventManager.OnGameSet += HandleGameSet;
        EventManager.OnGameDraw += HandleGameDraw;

        // 2. 로깅 및 QA 임시 자동 응답기 구독
        EventManager.OnLogMessage += HandleLogMessage;
        EventManager.OnRequireCardPick += HandleQA_CardPick;
        EventManager.OnRequireOptionalAction += HandleQA_OptionalAction; // Yes/No 자동 응답기
        EventManager.OnCharacterAbilityUsed += HandleCharacterAbilityUsed;
    }

    private void OnDisable()
    {
        // 구독 해제 (메모리 누수 및 중복 실행 완벽 방지)
        EventManager.OnGameSet -= HandleGameSet;
        EventManager.OnGameDraw -= HandleGameDraw;

        EventManager.OnLogMessage -= HandleLogMessage;
        EventManager.OnRequireCardPick -= HandleQA_CardPick;
        EventManager.OnRequireOptionalAction -= HandleQA_OptionalAction;
        EventManager.OnCharacterAbilityUsed -= HandleCharacterAbilityUsed;
    }

    private void HandleCharacterAbilityUsed(Player owner, string characterCardId)
    {
        CharacterFieldBroadcast.EmitSlotUpdate(owner, characterCardId);
    }

    // ★ context는 로컬 매치를 시작해야(StartMatch) 만들어진다. Start()에서는 만들지 않는다.
    //   온라인 대전 씬에는 이 매니저가 카드 데이터 제공자(CardData)로만 함께 올라가는데,
    //   그때 매치를 끝내는 쪽은 ServerGameManager다. 가드가 없으면 그 OnGameSet에 얹혀
    //   null 컨텍스트를 건드려 예외가 난다.

    private void HandleGameSet(Player winner)
    {
        if (context == null) return; // 이 매니저가 돌리는 매치가 아니다

        _currentGameWinner = winner;
        context.IsGameOver = true;
    }

    private void HandleGameDraw(Player p1, Player p2, int turn)
    {
        if (context == null) return; // 이 매니저가 돌리는 매치가 아니다

        context.IsGameOver = true;
    }

    // ─── 이벤트 핸들러 (익명 람다 대신 기명 메서드 사용) ───

    private void HandleLogMessage(string msg)
    {
        Debug.Log(msg);
    }

    // ─── QA 자동 응답기 ───────────────────────────────────────────────
    // ★ 봇 전용. 사람(Human)의 요청은 HumanChoiceDialogUI가 처리하므로 여기서 가로채면 안 된다.
    //   사람 요청을 여기서 즉시 응답해 버리면 플레이어의 선택권이 사라진다.
    //   (현재 OnRequireCardPick / OnRequireOptionalAction의 모든 발행처가 이미 Human 분기에서만
    //    이벤트를 쏘므로, 아래 가드가 걸리면 이 응답기는 사실상 동작하지 않는다.
    //    그래도 봇 경로가 추가될 때를 대비해 응답기 자체는 남겨 둔다.)

    private void HandleQA_CardPick(Player player, List<Card> validCards, int count, CardPickPrompt prompt, Action<List<Card>> callback)
    {
        if (player == null || player.Type != UserType.Bot) return; // 사람은 UI가 응답한다

        Debug.Log($"<color=orange>[봇 자동응답] {player.Name} 카드 선택 ({validCards.Count}장 중 {count}장) -> 앞쪽부터 자동 선택</color>");
        callback?.Invoke(validCards.Take(count).ToList());
    }

    private void HandleQA_OptionalAction(Player player, string message, GameContext ctx, Action<bool> callback)
    {
        if (player == null || player.Type != UserType.Bot) return; // 사람은 UI가 응답한다

        Debug.Log($"<color=orange>[봇 자동응답] {player.Name} 질문: '{message}' -> Yes</color>");
        callback?.Invoke(true);
    }

    // ─── Start 로직 ───────────────────────────────────────────────────

    private void Start()
    {
        InitializeSystem();
        EventManager.OnLogMessage?.Invoke("배틀 매니저 준비 완료. 매치 시작을 대기합니다...");
    }

    // ─── 퍼블릭 API: 외부(로비/게임매니저)에서 호출하는 게임 시작 트리거 ───

    public void StartMatch(PlayerSetupData p1Data, PlayerSetupData p2Data)
    {
        _p1Setup = p1Data;
        _p2Setup = p2Data;

        // 1. 전달받은 데이터를 바탕으로 Player 객체 뼈대 생성
        p1 = new Player
        {
            Name = _p1Setup.PlayerName,
            Type = _p1Setup.Type,
            CharacterCardId = _p1Setup.MainCharacterId,
            SecondaryCharacterId = _p1Setup.SubCharacterId
        };
        p2 = new Player
        {
            Name = _p2Setup.PlayerName,
            Type = _p2Setup.Type,
            CharacterCardId = _p2Setup.MainCharacterId,
            SecondaryCharacterId = _p2Setup.SubCharacterId
        };

        p1.InitializeBrain();
        p2.InitializeBrain();

        EventManager.OnLogMessage?.Invoke($"[게임 시작] {p1.Name} ({p1.Type}) VS {p2.Name} ({p2.Type})");

        // 2. 매치 루프 가동
        StartCoroutine(MatchLoop());
    }

    // ─── 퍼블릭 API: 항복 ──────────────────────────────────────────────

    /// <summary>
    /// 로드된 카드 데이터 조회용 (읽기 전용 용도).
    /// UI가 카드 ID로 이름·설명·이미지 경로를 찾을 때 쓴다. 상태를 바꾸지 말 것.
    /// </summary>
    public GameDataManager CardData => _dataManager;

    /// <summary>현재 매치에 참여 중인 사람 플레이어. 없으면 null (봇 vs 봇).</summary>
    public Player HumanPlayer
    {
        get
        {
            if (p1 != null && p1.Type == UserType.Human) return p1;
            if (p2 != null && p2.Type == UserType.Human) return p2;
            return null;
        }
    }

    /// <summary>
    /// UI의 [항복] 버튼용. 항복한 플레이어의 상대를 승자로 확정하고 게임을 끝낸다.
    ///
    /// OnGameSet을 발행하면 HandleGameSet이 context.IsGameOver를 세우고,
    /// RunSingleGame의 페이즈 간 CheckAndHandleGameOver()가 루프를 빠져나온다.
    /// 입력 대기 중이더라도 각 대기 지점이 IsGameOver를 함께 감시하므로 즉시 풀린다.
    /// </summary>
    public void SurrenderBy(Player quitter)
    {
        if (quitter == null || context == null || context.IsGameOver) return;

        Player winner = ReferenceEquals(quitter, p1) ? p2 : p1;
        if (winner == null) return;

        EventManager.OnLogMessage?.Invoke(
            $"<color=red>[항복] {quitter.Name}이(가) 항복했습니다. {winner.Name} 승리.</color>");
        EventManager.OnGameSet?.Invoke(winner);
    }

    // ─── 시스템 초기화 ───────────────────────────────────────────────

    private void InitializeSystem()
    {

        // 유니티 Resources 폴더 안의 "GameData" 폴더를 바라보는 로더 생성
        IJsonLoader loader = new UnityResourceLoader("GameData");

        _dataManager = new GameDataManager(loader);
        GameRules.LoadRules(loader, "CommonConfig"); // 룰북 로드 (경로 대신 로더 전달)

        // 경로 전달 없이 깔끔하게 메서드만 호출
        _dataManager.LoadRulebookCards();
        _dataManager.LoadCharacterCards();
        _dataManager.LoadResourceCards();
        EventManager.OnLogMessage?.Invoke($"<color=cyan>[System] 데이터 로드 완료 (UnityResourceLoader 사용)</color>");
        /* 과거 파일 시스템 접근 방식 (동기화용으로 남겨둠)
        string primaryPath = Path.Combine(Application.dataPath, "Resources", "GameData");
        // string dataPath = Directory.Exists(primaryPath) ? primaryPath : "./Data";
        string CardDataPath = Directory.Exists(primaryPath) ? primaryPath : "../Resources/GameData";

        try { GameRules.LoadRules(Path.Combine(primaryPath, "CommonConfig.json")); }
        catch (Exception e)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[Rules Error] {e.Message}</color>");
        }

        // ★ 동기화: JSON에서 파싱된 룰 데이터(0.5f)를 매니저의 ActionDelay에 덮어씌웁니다.
        ActionDelay = GameRules.BotDelayTime;

        _dataManager = new GameDataManager();
        _dataManager.LoadRulebookCards(primaryPath); // M1 용 수정 경로
        // _dataManager.LoadRulebookCards(dataPath); <- 예비 경로
        _dataManager.LoadCharacterCards(primaryPath);
        _dataManager.LoadResourceCards(primaryPath);

        EventManager.OnLogMessage?.Invoke($"<color=cyan>[System] 데이터 로드 완료 (경로: {primaryPath})</color>");*/
    }

    // ─── 매치 루프 (3판 2선승) ───────────────────────────────────────

    private IEnumerator MatchLoop()
    {
        int MaxGame = 3;
        int PlayToWin = 2;

        if (GameRules.BotSingleGame == 1) // 단판제일 경우
        {
            MaxGame = 1;
            PlayToWin = 1;
        }

        _matchManager = new MatchManager(gamesToWin: PlayToWin, maxGames: MaxGame);

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
            ResolveSimultaneousDeckout(); // 타이브레이커 판정으로 승자 결정
            // EventManager.OnGameDraw?.Invoke(p1, p2, context.CurrentTurn);
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
        context.CurrentTurn = 1;

        while (!context.IsGameOver && context.CurrentTurn <= 20)
        {
            EventManager.OnTurnStart?.Invoke(context.CurrentTurn, "양측");
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
            context.CurrentTurn++;
        }

        if (!context.IsGameOver && context.CurrentTurn > 20)
        {
            context.IsGameOver = true;
            EventManager.OnGameDraw?.Invoke(p1, p2, 20);
        }
    }

    private void InitializeSingleGame()
    {
        /* ==============================================================
        // [팀원 공유용] ★ 임시 테스트 덱 하드코딩 (ConsoleRunner 동기화)
        // 무작위 덱이 아닌 특정 카드들의 충돌을 테스트하기 위해 ID를 고정합니다.
        // 배열에 적어둔 ID 1개당 자동으로 2장씩 덱에 들어갑니다.
        // 10개를 적으면 정상적인 20장 덱이 되고, 적게 적으면 미니 덱이 됩니다.
        // ==============================================================
        string[] p1TestIds = 
            // BotRed : 엘리 + 다이나
            {
                "ELLI-02", // 퀵 드로우
                "ELLI-03", // 수류탄 투척
                "ELLI-04", // 미니건 난사
                "ELLI-05", // 준비된 방어선
                "ELLI-06", // 격추 시스템
                "ELLI-07", // 카모플라쥬

                "DAIN-02", // 함포 준비, 발사
                "DAIN-03", // 미사일 발사
                "DAIN-07", // 강도 테스트
                "DAIN-09", // 리벤지
                "DAIN-11", // 조선소
                        // 필요시 여기에 ID를 더 추가하세요.
            };

        string[] p2TestIds = // BorBlue : 베로니카 + 소니아
            {
                "VERO-02", // 숙청
                "VERO-03", // 계획대로
                "VERO-05", // 요새화
                "VERO-07", // 시위 해산
                "VERO-11", // 체크메이트

                "SONI-02", // 빵야!
                "SONI-03", // 내 선물이야 ♬
                "SONI-05", // 곡예 비행
                "SONI-06", // 엔진 예열
                "SONI-07", // 마하 10
                "SONI-11", // 노을지는 활주로
                        // 필요시 여기에 ID를 더 추가하세요.
            };*/

        // 전달받은 설정 데이터(_p1Setup, _p2Setup)의 DeckCardIds 리스트를 사용하여 덱을 생성합니다.
        // CreateDeckFromIds 메서드의 매개변수가 배열(string[])이었다면 IEnumerable<string>이나 List<string>을 받도록 살짝 수정해야 합니다.
        var deck1 = CreateDeckFromIds(_p1Setup.DeckCardIds);
        var resDeck1 = CreateResourceDeck();
        var deck2 = CreateDeckFromIds(_p2Setup.DeckCardIds);
        var resDeck2 = CreateResourceDeck();

        context = new GameContext();
        context.CurrentTurn = 1;

        context.Players.Add(p1);
        context.Players.Add(p2);

        p1.ResetForNewGame(deck1, resDeck1);
        p2.ResetForNewGame(deck2, resDeck2);

        LogDeckValidation(p1);
        LogDeckValidation(p2);

        EventManager.OnGameStart?.Invoke(p1, p2);
        CharacterFieldBroadcast.Register(p1, _dataManager);
        CharacterFieldBroadcast.SyncAll(p1, p2, _dataManager);

        GameLogicHelpers.DrawCards(p1, GameRules.StartingHands, context);
        GameLogicHelpers.DrawCards(p2, GameRules.StartingHands, context);

        // QaInjection.ApplyDefaultScenario(_dataManager, p1, p2);

        EventManager.OnLogMessage?.Invoke($"\n[초기] {p1.Name} — 라이프:{p1.LifeTokens} / 덱:{p1.Deck.Count} / 자원덱:{p1.ResourceDeck.Count} / 패:{p1.Hand.Count}");
        EventManager.OnLogMessage?.Invoke($"[초기] {p2.Name} — 라이프:{p2.LifeTokens} / 덱:{p2.Deck.Count} / 자원덱:{p2.ResourceDeck.Count} / 패:{p2.Hand.Count}");
    }

    // ─── 페이즈 1: 자원 페이즈 ──────────────────────────────────────

    private IEnumerator ExecuteResourcePhaseRoutine()
    {
        context.CurrentPhase = GamePhase.ResourcePhase;
        EventManager.OnResourcePhase?.Invoke("양측", context.CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 자원 페이즈 ]");

        if (p1.BattlefieldCard != null || p2.BattlefieldCard != null)
        {
            EventManager.OnLogMessage?.Invoke("[ 적용 중인 전장 카드 ]");
            EventManager.OnLogMessage?.Invoke($"  {p1.Name} 전장: {(p1.BattlefieldCard != null ? p1.BattlefieldCard.Name : "없음")}");
            EventManager.OnLogMessage?.Invoke($"  {p2.Name} 전장: {(p2.BattlefieldCard != null ? p2.BattlefieldCard.Name : "없음")}");
        }

        p1.ApplyNextTurnBuffs();
        p2.ApplyNextTurnBuffs();

        _pendingBattlefieldEffects = 0;

        context.ActivePlayer = p1; context.TargetPlayer = p2;
        p1.TakeResourceCard();
        // 전장 기동 효과(ELLI-11)는 사람에게 예/아니오를 묻는다.
        // 기다리지 않으면 회수 카드가 드로우 페이즈 도중에 들어온다.
        bool p1Cut = GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(
            p1, context, out int p1Started, () => _pendingBattlefieldEffects--);
        _pendingBattlefieldEffects += p1Started;
        yield return WaitForBattlefieldEffects();
        if (p1Cut) yield break;

        context.ActivePlayer = p2; context.TargetPlayer = p1;
        p2.TakeResourceCard();
        bool p2Cut = GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(
            p2, context, out int p2Started, () => _pendingBattlefieldEffects--);
        _pendingBattlefieldEffects += p2Started;
        yield return WaitForBattlefieldEffects();
        if (p2Cut) yield break;

        yield return new WaitForSeconds(ActionDelay);
    }

    /// <summary>
    /// 전장 기동 효과의 응답을 자원 페이즈 안에서 받는다.
    /// 응답이 안 와도 게임이 멈추지 않도록 상한을 둔다.
    /// </summary>
    private IEnumerator WaitForBattlefieldEffects()
    {
        float waited = 0f;
        float limit = GameRules.ChooseWaitTime / 1000f;
        while (_pendingBattlefieldEffects > 0 && waited < limit
               && context != null && !context.IsGameOver)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        _pendingBattlefieldEffects = 0;
    }

    // ─── 페이즈 2: 드로우 페이즈 (병렬 처리 적용) ─────────────────────────────────────

    private IEnumerator ExecuteDrawPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.DrawPhase;
        EventManager.OnDrawPhase?.Invoke("양측", context.CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 드로우 페이즈 ]");

        // 1. 전장 효과(자동)는 순차 처리해도 무방함 (체크메이트, 조선소 등)
        context.ActivePlayer = p1; context.TargetPlayer = p2;
        if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p1, context)) yield break;

        context.ActivePlayer = p2; context.TargetPlayer = p1;
        if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p2, context)) yield break;

        // 2. 캐릭터 능력(베로니카의 3픽 1 선택 등)과 드로우를 "동시에" 수집 및 처리
        bool p1DrawDone = false;
        bool p2DrawDone = false;

        StartCoroutine(ExecuteDrawForPlayerParallel(p1, () => p1DrawDone = true));
        StartCoroutine(ExecuteDrawForPlayerParallel(p2, () => p2DrawDone = true));

        // 양측이 드로우(또는 선택)를 모두 마칠 때까지 대기
        yield return new WaitUntil(() => p1DrawDone && p2DrawDone);

        EventManager.OnLogMessage?.Invoke($"{p1.Name} 패: {p1.Hand.Count}장");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} 패: {p2.Hand.Count}장");

        yield return new WaitForSeconds(ActionDelay);
    }

    // 헬퍼: 각 플레이어의 드로우/선택을 독립적인 흐름으로 실행
    private IEnumerator ExecuteDrawForPlayerParallel(Player player, Action onDone)
    {
        bool drawHandledByAbility = false;

        foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
        {
            if (ability.CanUse(player, context))
            {
                bool abilityDone = false;
                // 베로니카 능력 등이 UI 입력을 기다림 (타임아웃 로직은 능력 내부나 EventManager 호출부에 구현)
                ability.OnDrawPhase(player, context, used =>
                {
                    drawHandledByAbility = used;
                    abilityDone = true;
                });
                yield return new WaitUntil(() => abilityDone);
                if (drawHandledByAbility) break;
            }
        }

        // 능력을 안 썼다면 일반 드로우 진행
        if (!drawHandledByAbility)
        {
            GameLogicHelpers.DrawCards(player, GameRules.DrawPerTurn, context);
        }

        onDone?.Invoke();
    }

    /* ─── 페이즈 2: 드로우 페이즈 ─────────────────────────────────────

    private IEnumerator ExecuteDrawPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.DrawPhase;
        EventManager.OnDrawPhase?.Invoke("양측", context.CurrentTurn);
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

    private IEnumerator ExecuteDrawForPlayer(Player player) // 베로니카 효과 사용 시
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
    }*/

    // ─── 페이즈 3: 세트 페이즈 (동시 처리) ───────────────────────────────────────

    private IEnumerator ExecuteSetPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.SetPhase;
        EventManager.OnSetPhase?.Invoke("양측", context.CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 세트 페이즈 ]");

        // 1. 세트 전 능력(소니아 등) 동시 처리
        bool p1AbilityDone = false; bool p2AbilityDone = false;
        StartCoroutine(CheckPreSetAbilities(p1, () => p1AbilityDone = true));
        StartCoroutine(CheckPreSetAbilities(p2, () => p2AbilityDone = true));
        yield return new WaitUntil(() => p1AbilityDone && p2AbilityDone);

        // 2. 양측의 '세트할 카드 선택'을 동시에 수집
        Card p1SetCard = null; Card p2SetCard = null;
        bool p1SetDone = false; bool p2SetDone = false;

        StartCoroutine(GetSetCardChoice(p1, card => { p1SetCard = card; p1SetDone = true; }));
        StartCoroutine(GetSetCardChoice(p2, card => { p2SetCard = card; p2SetDone = true; }));

        // 양측이 카드를 고를 때까지 대기
        yield return new WaitUntil(() => p1SetDone && p2SetDone);

        // 3. 일괄 적용 (동시에 세트존에 배치)
        yield return new WaitForSeconds(ActionDelay);
        if (p1SetCard != null) p1.SetCard(p1SetCard);
        if (p2SetCard != null) p2.SetCard(p2SetCard);

        EventManager.OnLogMessage?.Invoke($"{p1.Name} 세트존: {(p1.SetZoneCard != null ? "세트됨" : "없음")}");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} 세트존: {(p2.SetZoneCard != null ? "세트됨" : "없음")}");

        yield return new WaitForSeconds(ActionDelay);
    }

    private IEnumerator CheckPreSetAbilities(Player player, Action onDone)
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
        onDone?.Invoke();
    }

    // ★ 헬퍼 변경: 실제로 카드를 세트하지 않고, '무엇을 세트할지' 선택만 수집합니다.
    private IEnumerator GetSetCardChoice(Player player, Action<Card> onChosen)
    {
        if (player.Hand.Count == 0)
        {
            onChosen(null);
            yield break;
        }

        if (player.Type == UserType.Bot)
        {
            // 봇 로직: 지불 가능한 카드 중 랜덤 선택 (즉시 완료)
            var affordableCards = player.Hand.Where(c => GameLogicHelpers.GetEffectiveCost(c, player) <= player.ResourceZone.Count).ToList();
            var candidates = affordableCards.Count > 0 ? affordableCards : player.Hand;
            Card cardToSet = candidates.OrderBy(c => Guid.NewGuid()).FirstOrDefault();

            onChosen(cardToSet);
        }
        else
        {
            // 휴먼 로직: UI 응답을 무제한 대기
            // ★ 사람은 제한 시간 없이 생각할 수 있어야 한다 (사람 vs 봇 기준).
            //   온라인(사람 vs 사람)에서는 상대를 무한정 기다리게 할 수 없으므로
            //   ServerGameManager 쪽 제한 시간은 그대로 유지한다.
            bool done = false;
            Card chosenCard = null;

            EventManager.OnRequireSetPhaseAction?.Invoke(player, context, card =>
            {
                chosenCard = card;
                done = true;
            });

            // IsGameOver도 함께 감시한다. 항복 등으로 게임이 끝나면 입력을 기다리지 않고 즉시 빠져나온다.
            yield return new WaitUntil(() => done || context.IsGameOver);

            onChosen(done ? chosenCard : null);
        }
    }

    // ─── 페이즈 4: 오픈 페이즈 (동시 처리) ───────────────────────────────────────

    private IEnumerator ExecuteOpenPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.OpenPhase;
        EventManager.OnOpenPhase?.Invoke("양측", context.CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 오픈 페이즈 ]");

        context.ClearOpenPhaseStates();

        // 1. 양측의 '오픈/폐기 선택'을 동시에 수집
        OpenPhaseChoice p1Choice = OpenPhaseChoice.Abandon;
        OpenPhaseChoice p2Choice = OpenPhaseChoice.Abandon;
        bool p1Done = false; bool p2Done = false;

        StartCoroutine(GetOpenChoiceParallel(p1, choice => { p1Choice = choice; p1Done = true; }));
        StartCoroutine(GetOpenChoiceParallel(p2, choice => { p2Choice = choice; p2Done = true; }));

        // 양측이 선택을 마칠 때까지 대기
        yield return new WaitUntil(() => p1Done && p2Done);

        // 2. 수집된 결과를 동시 적용 (여기서 실제 효과와 로그가 터짐)
        _p1RevealedCard = ApplyOpenChoice(p1, p1Choice);
        context.OpenPhaseStates[p1.Name] = new PlayerOpenPhaseState { HasOpened = (_p1RevealedCard != null), RevealedCard = _p1RevealedCard };

        _p2RevealedCard = ApplyOpenChoice(p2, p2Choice);

        // 폐기 시 발동 능력(다이나)의 응답을 이 페이즈 안에서 받는다 (상한을 둬 멈추지 않게)
        float abandonWait = 0f;
        float abandonLimit = GameRules.ChooseWaitTime / 1000f;
        while (_pendingAbandonAbilities > 0 && abandonWait < abandonLimit
               && context != null && !context.IsGameOver)
        {
            abandonWait += Time.deltaTime;
            yield return null;
        }
        _pendingAbandonAbilities = 0;
        context.OpenPhaseStates[p2.Name] = new PlayerOpenPhaseState { HasOpened = (_p2RevealedCard != null), RevealedCard = _p2RevealedCard };

        yield return new WaitForSeconds(ActionDelay);
    }

    // ★ 헬퍼 변경: 선택만 수집 (결과 적용 X)
    private IEnumerator GetOpenChoiceParallel(Player player, Action<OpenPhaseChoice> onChosen)
    {
        if (player.SetZoneCard == null)
        {
            onChosen(OpenPhaseChoice.Abandon);
            yield break;
        }

        int effectiveCost = GameLogicHelpers.GetEffectiveCost(player.SetZoneCard, player);

        if (player.Type == UserType.Bot)
        {
            // 봇 로직: 코스트 가능하면 오픈, 아니면 폐기
            OpenPhaseChoice choice = (effectiveCost == 0 || player.CanAfford(effectiveCost)) ? OpenPhaseChoice.Open : OpenPhaseChoice.Abandon;
            onChosen(choice);
        }
        else
        {
            // 휴먼 로직: UI 응답을 무제한 대기 (사람 vs 봇 기준 — 제한 시간 없음)
            bool done = false;
            OpenPhaseChoice chosen = OpenPhaseChoice.Abandon; // 응답 전 기본값

            EventManager.OnRequireOpenPhaseAction?.Invoke(player, player.SetZoneCard, effectiveCost, context, c =>
            {
                chosen = c;
                done = true;
            });

            // 항복 등으로 게임이 끝나면 입력을 기다리지 않는다 (기본값 폐기로 진행)
            yield return new WaitUntil(() => done || context.IsGameOver);

            onChosen(chosen);
        }
    }

    // ★ 신규 헬퍼: 수집된 선택을 실제로 집행
    private Card ApplyOpenChoice(Player player, OpenPhaseChoice choice)
    {
        if (player.SetZoneCard == null) return null;

        if (choice == OpenPhaseChoice.Abandon)
        {
            int effectiveCost = GameLogicHelpers.GetEffectiveCost(player.SetZoneCard, player);

            EventManager.OnLogMessage?.Invoke($"{player.Name}: 세트 카드 [{player.SetZoneCard.Name}] 폐기 선택 (코스트 필요: {effectiveCost} / 자원존: {player.GetResourceCount()})");

            player.AbandonSetCard();
            GameLogicHelpers.DrawCards(player, 1, context);

            // 폐기 시 능력(DAIN 다이나 등) 발동
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
            {
    // ★ 다이나 능력(폐기 시 라이프 +1)은 사람에게 예/아니오를 묻는 **비동기** 훅이다.
    //   예전에는 "즉발 효과라 대기 불필요"라며 던져 놓고 바로 다음으로 넘어갔는데,
    //   그건 묻지 않고 즉시 회복하던 구버전 기준의 주석이었다.
    //   그대로 두면 오픈 페이즈가 먼저 끝나 버려 라이프 회복이 메인 페이즈 도중에 적용된다
    //   (그 사이에 데미지를 맞으면 회복 전에 죽을 수 있다).
    //   그래서 발동한 능력 수를 세고, 오픈 페이즈가 그 응답을 기다린다.
                if (!ability.CanUse(player, context)) continue;

                _pendingAbandonAbilities++;
                ability.OnOpenPhaseAbandon(player, context, _ => _pendingAbandonAbilities--);
            }
            return null;
        }
        else
        {
            return player.RevealSetCard();
        }
    }

    /* ─── 페이즈 4: 오픈 페이즈 ───────────────────────────────────────

    private IEnumerator ExecuteOpenPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.OpenPhase;
        EventManager.OnOpenPhase?.Invoke("양측", context.CurrentTurn);
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

        OpenPhaseChoice choice = OpenPhaseChoice.Abandon;

        // [팀원 공유용] 봇 로직 동기화: 코스트를 낼 수 있으면 무조건 오픈, 아니면 폐기
        if (player.Type == UserType.Bot)
        {
            choice = (effectiveCost == 0 || player.CanAfford(effectiveCost)) ? OpenPhaseChoice.Open : OpenPhaseChoice.Abandon;
        }
        else
        {
            bool done = false;
            EventManager.OnRequireOpenPhaseAction?.Invoke(player, setCard, effectiveCost, context, c =>
            {
                choice = c;
                done = true;
            });
            yield return new WaitUntil(() => done);
        }

        if (choice == OpenPhaseChoice.Abandon)
        {
            EventManager.OnLogMessage?.Invoke($"{player.Name}: 코스트 부족 (필요: {effectiveCost} / 자원존: {player.GetResourceCount()}) → 폐기 선택");
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
    }*/

    // ─── 페이즈 5: 메인 페이즈 (동적 큐 + 완벽한 SBA 대응) ────────────────────

    private IEnumerator ExecuteMainPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.MainPhase;
        EventManager.OnMainPhase?.Invoke("양측", context.CurrentTurn);
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

                // [팀원 공유용] 2중 코스트 할인 연산 동기화 (GetEffectiveCost 사용)
                if (card.Cost > 0)
                {
                    int effectiveCost = GameLogicHelpers.GetEffectiveCost(card, player);
                    if (effectiveCost > 0 && !player.PayCost(effectiveCost))
                    {
                        EventManager.OnLogMessage?.Invoke($"{player.Name}: [{card.Name}] 코스트 지불 실패 → 효과 취소");
                        EventManager.OnPlayFailed?.Invoke(card);
                        player.AbandonSetCard();
                        continue;
                    }
                    else if (effectiveCost == 0 && card.Cost > 0)
                    {
                        EventManager.OnLogMessage?.Invoke($"  ✨ [{card.Name}] 전장 효과로 코스트 무료 발동!");
                    }
                }

                // 스택 반응 (최신 AI 최적화 로직 적용)
                yield return StartCoroutine(HandleStackActivation(enemy, player, card));

                // ★ UI 팀 참고: 이 이벤트가 터지면 카드가 전장 중앙으로 팝업되는 연출을 재생하세요!
                EventManager.OnPlayCard?.Invoke(player, card);

                player.PlayingCard = card;
                context.LastEffectSucceeded = true;
                bool effectDone = false;

                // 오픈 즉발 효과만 실행
                card.Play(context, () => effectDone = true, isStackTrigger: false);
                yield return new WaitUntil(() => effectDone);

                player.PlayingCard = null;

                // [팀원 공유용] 카드 라우팅 및 이동 이벤트 방송 동기화
                yield return new WaitForSeconds(ActionDelay);
                if (card.IsStack)
                {
                    player.AddToStackZone(card);
                    player.ExtractCard(ZoneType.SetZone, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.StackZone);
                    EventManager.OnCardStacked?.Invoke(card, player);
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 스택존에 대기 상태로 전환.");
                }
                else if (card.IsBattlefield)
                {
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 전장 카드 발동!");
                    player.ExtractCard(ZoneType.SetZone, card);
                    player.PlaceBattlefield(card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.BattlefieldZone);
                    EventManager.OnCardBattlefield?.Invoke(card, player);
                    // 메인 페이즈 발동 즉시 전장 효과 1회 장전 (On-Play Wake)
                    GameLogicHelpers.ApplyBattlefieldTurnEffects(player, context);
                }
                else if (player.ResourceZone.Contains(card))
                {
                    // SelfAsResource 효과로 처리된 경우 이동 이벤트만 쏴줌
                    player.ExtractCard(ZoneType.SetZone, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.ResourceZone);
                    EventManager.OnCardResourceAdded?.Invoke(card, player);
                    EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 자원존에 배치됨.");
                    EventManager.OnResourceChange?.Invoke(player, player.GetResourceCount());
                }
                else
                {
                    player.ExtractCard(ZoneType.SetZone, card);
                    player.InsertCard(ZoneType.Graveyard, card);
                    EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);

                    foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player)) // 엘리 효과
                    {
                        bool abilityDone = false;
                        ability.OnMainPhaseAfterAttack(player, card, enemy, context, followUpCard =>
                        {
                            if (followUpCard != null)
                            {
                                pendingQueue.Add((player, enemy, followUpCard));
                            }
                            abilityDone = true;
                        });

                        // 유저가 결정을 내리거나 타임아웃이 끝날 때까지 엔진을 잠시 대기시킴
                        yield return new WaitUntil(() => abilityDone);

                        /* 기존 동기화 방식 (콜백 이전 자동 선택 로직)
                        Card followUpCard = ability.OnMainPhaseAfterAttack(player, card, enemy, context);
                        if (followUpCard != null)
                        {
                            pendingQueue.Add((player, enemy, followUpCard));
                        }*/
                    }
                }
                yield return new WaitForSeconds(ActionDelay);
            }

            if (CheckAndHandleGameOver())
            {
                EventManager.OnLogMessage?.Invoke("최후의 일격으로 교전이 중단되었습니다!");
                break;
            }
        }
    }

    /// <summary>
    /// 스마트 스택 AI 및 발동 코루틴 (다단히트, 관통 완벽 대응 + 타임아웃 자동 선택 기능)
    /// </summary>
    private IEnumerator HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard)
    {
        if (stackOwner.StackZone.Count == 0 || stackOwner.IsInvincible) yield break;

        int incomingHits = 0;
        bool isPiercingAttack = false;

        // 1. 공격 카드의 타격 횟수 스캔
        foreach (var effect in playedCard.Effects)
        {
            if (effect is DamageEffect dmgEffect)
            {
                // ★ 피아식별: 자해(Self) 데미지는 방어할 필요가 없으므로 타격 횟수에서 제외
                if (!dmgEffect.TargetSelf)
                {
                    incomingHits += dmgEffect.Times;
                    if (dmgEffect.isPiercing) isPiercingAttack = true;
                }
            }
        }

        if (incomingHits == 0) yield break;

        // ★ 불발될 카드에는 스택을 소진하지 않는다.
        //   "그 후," 선행 조건을 못 채우면 카드 전체가 불발인데(Card.Play),
        //   스택 발동은 그보다 먼저 일어나 상대 방어 카드만 태워 버렸다.
        //
        //   ⚠️ 이 시점엔 cardPlayer.PlayingCard가 아직 설정되기 전이라,
        //   excludeSelf를 쓰는 효과는 후보를 한 장 더 세게 된다.
        //   즉 관대한 쪽으로만 틀린다 — "멀짱한 공격인데 스택을 안 태우는" 일은 없다.
        if (!playedCard.WillResolve(context))
        {
            EventManager.OnLogMessage?.Invoke(
                $"  [스택 보류] '{playedCard.Name}'은(는) 불발될 카드라 스택을 발동하지 않습니다.");
            yield break;
        }

        // 2. 발동 "가능한" 방어 카드 모두 추리기 (수집)
        List<Card> validStackCards = new List<Card>();
        foreach (var stackCard in stackOwner.StackZone)
        {
            bool canBlock = false;
            // 봇이 서포트 방어카드도 인식하도록 수정됨
            if (stackCard.Type == CardType.Defense || stackCard.Type == CardType.Support || stackCard.Type == CardType.Attack)
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

        // 막을 수 있는 카드가 하나도 없다면 종료
        if (validStackCards.Count == 0) yield break;

        // 3. 강제 발동해야 할 횟수 계산 (유효한 카드 수와 타격 횟수 중 작은 값)
        int requiredCount = Mathf.Min(incomingHits, validStackCards.Count);
        List<Card> selectedCards = new List<Card>();

        // 4. 순서 및 대상 선택 (AI vs Human + Timeout)
        if (stackOwner.Type == UserType.Bot)
        {
            // 봇은 단순하게 먼저 깔린 순서대로 필요한 만큼 선택
            selectedCards = validStackCards.Take(requiredCount).ToList();
        }
        else
        {
            // 휴먼은 UI(HumanChoiceDialogUI)를 통해 직접 발동 순서를 고른다.
            // ★ 제한 시간 없음 — 사람은 얼마든지 생각할 수 있어야 한다 (사람 vs 봇 기준).
            bool done = false;

            EventManager.OnRequireCardPick?.Invoke(stackOwner, validStackCards, requiredCount, default, chosenCards =>
            {
                selectedCards = chosenCards ?? new List<Card>();
                done = true;
            });

            // 항복 등으로 게임이 끝나면 입력을 기다리지 않는다 (스택 미발동으로 진행)
            yield return new WaitUntil(() => done || context.IsGameOver);
            if (!done) yield break;
        }

        // 5. 선택된 카드들을 순서대로 발동 (실행)
        Player originalActive = context.ActivePlayer;
        Player originalTarget = context.TargetPlayer;

        foreach (var stackCard in selectedCards)
        {
            context.ActivePlayer = stackOwner;
            context.TargetPlayer = cardPlayer;

            // 스택 카드를 발동하기 전에 시스템에 "현재 사용 중인 카드"를 명시적으로 주입
            stackOwner.PlayingCard = stackCard;
            EventManager.OnLogMessage?.Invoke($"{stackOwner.Name}: [{stackCard.Name}] 스택 발동! (← 상대: [{playedCard.Name}])");

            bool done = false;
            context.LastEffectSucceeded = true;

            stackCard.Play(context, () => done = true, isStackTrigger: true);
            yield return new WaitUntil(() => done);

            // 복구: 발동이 끝났으니 다시 null로 비워줍니다.
            stackOwner.PlayingCard = null;
            yield return new WaitForSeconds(ActionDelay);
            stackOwner.UseAndDiscardStack(stackCard);

            yield return new WaitForSeconds(ActionDelay);
        }

        // ★ 상태 복구 (State Restore)
        context.ActivePlayer = originalActive;
        context.TargetPlayer = originalTarget;
    }

    // ─── 페이즈 6: 엔드 페이즈 ──────────────────────────────────────

    private IEnumerator ExecuteEndPhaseRoutine()
    {
        context.CurrentPhase = GamePhase.EndPhase;
        EventManager.OnEndPhase?.Invoke("양측", context.CurrentTurn);
        EventManager.OnLogMessage?.Invoke("[ 엔드 페이즈 ]");

        EventManager.OnLogMessage?.Invoke($"{p1.Name} — 라이프:{p1.LifeTokens} / 자원:{p1.ResourceZone.Count} / 패:{p1.Hand.Count} / 덱:{p1.Deck.Count}");
        EventManager.OnLogMessage?.Invoke($"{p2.Name} — 라이프:{p2.LifeTokens} / 자원:{p2.ResourceZone.Count} / 패:{p2.Hand.Count} / 덱:{p2.Deck.Count}\n");

        if (!context.IsGameOver) // 덱 아웃 체크는 게임 오버가 아닐 때만 (이미 승패가 갈린 상황에서 덱 아웃이 겹쳐서 터지는 걸 방지)
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

    /// <summary>
    /// [QA 전용] 특정 플레이어의 원하는 위치(Zone)에 특정 카드를 강제로 생성하여 주입합니다.
    /// 복잡한 엣지 케이스를 1턴 만에 재현하기 위한 유니티 디버깅용 툴입니다.
    /// </summary>
    private void InjectTestCard(Player player, string cardId, ZoneType targetZone)
    {
        QaInjection.Inject(_dataManager, player, cardId, targetZone);
    }

    private void ResolveSimultaneousDeckout()
    {
        EventManager.OnTiebreaker?.Invoke(p1, p2);
        int result = TiebreakerResolver.ResolveTiebreaker(p1, p2);

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
            bool p1WinsToss = UnityEngine.Random.Range(0, 2) == 0; // 50% 확률로 동전 던지기
            Player tossWinner = p1WinsToss ? p1 : p2;
            EventManager.OnLogMessage?.Invoke($"5단계 모두 동일 → 코인토스! {tossWinner.Name} 승리");
            _currentGameWinner = tossWinner;
            EventManager.OnGameSet?.Invoke(tossWinner);
        }
    }

    private List<Card> CreateDeckFromIds(List<string> ids)
    {
        var deck = new List<Card>();
        foreach (string id in ids)
        {
            if (_dataManager.AllCards.TryGetValue(id, out Card c))
                deck.Add(c.Clone()); // 기존에는 for문으로 2장씩 넣었지만, 이제 로비에서 정확히 20장 리스트를 넘겨줄 것이므로 1장씩 넣도록 수정!
        }
        return deck;
    }

    // 기존 랜덤 방식 덱 생성 헬퍼
    private List<Card> CreateDualCharacterDeck(string charId1, string charId2, int count1, int count2)
    {
        var ids1 = _dataManager.GetEffectCardIdsForCharacter(charId1);
        var ids2 = _dataManager.GetEffectCardIdsForCharacter(charId2);
        var ids = ids1.Take(count1).Concat(ids2.Take(count2)).ToArray();
        return CreateDeckFromIds(ids.ToList());
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

    private void LogDeckValidation(Player player)
    {
        DeckValidator.DeckValidationResult validation = DeckValidator.ValidateFullDeckSet(
            player.Deck,
            player.ResourceDeck,
            player.CharacterCardId,
            player.SecondaryCharacterId);

        if (!validation.IsValid)
        {
            EventManager.OnLogMessage?.Invoke(
                $"<color=red>[Deck Error] {player.Name}의 덱이 유효하지 않습니다: {validation.ErrorMessage}</color>");
        }
    }
}