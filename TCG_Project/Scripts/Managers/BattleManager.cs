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
using TCG_Project.Scripts.Manager;
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
    }

    private void OnDisable()
    {
        // 구독 해제 (메모리 누수 및 중복 실행 완벽 방지)
        EventManager.OnGameSet -= HandleGameSet;
        EventManager.OnGameDraw -= HandleGameDraw;
        
        EventManager.OnLogMessage -= HandleLogMessage;
        EventManager.OnRequireCardPick -= HandleQA_CardPick;
        EventManager.OnRequireOptionalAction -= HandleQA_OptionalAction;
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

    // ─── 이벤트 핸들러 (익명 람다 대신 기명 메서드 사용) ───

    private void HandleLogMessage(string msg)
    {
        Debug.Log(msg);
    }

    private void HandleQA_CardPick(Player player, List<Card> validCards, int count, Action<List<Card>> callback)
    {
        // UI가 없을 때의 임시 카드 선택기 (앞에서부터 강제 선택)
        Debug.Log($"<color=orange>[임시 UI] {player.Name}에게 카드 선택 요청 ({validCards.Count}장 중 {count}장) -> 앞쪽부터 강제 자동 선택</color>");
        callback?.Invoke(validCards.Take(count).ToList());
    }

    private void HandleQA_OptionalAction(Player player, string message, GameContext ctx, Action<bool> callback)
    {
        // UI가 없을 때의 임시 Yes/No 선택기 (무조건 Yes 발동)
        Debug.Log($"<color=orange>[임시 UI] {player.Name}에게 질문: '{message}' -> 무조건 Yes 응답</color>");
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
        p1 = new Player { 
            Name = _p1Setup.PlayerName, 
            Type = _p1Setup.Type, 
            CharacterCardId = _p1Setup.MainCharacterId, 
            SecondaryCharacterId = _p1Setup.SubCharacterId 
        };
        p2 = new Player { 
            Name = _p2Setup.PlayerName, 
            Type = _p2Setup.Type, 
            CharacterCardId = _p2Setup.MainCharacterId, 
            SecondaryCharacterId = _p2Setup.SubCharacterId 
        };

        DeckValidator.DeckValidationResult validation1 = DeckValidator.ValidateFullDeckSet(p1.Deck, p1.ResourceDeck, p1.CharacterCardId, p1.SecondaryCharacterId);
        DeckValidator.DeckValidationResult validation2 = DeckValidator.ValidateFullDeckSet(p2.Deck, p2.ResourceDeck, p2.CharacterCardId, p2.SecondaryCharacterId);

        if (!validation1.IsValid)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[Deck Error] {_p1Setup.PlayerName}의 덱이 유효하지 않습니다: {validation1.ErrorMessage}</color>");
            // return; // 덱이 무효여도 일단 게임은 시작하도록 허용 (QA 테스트용)
        }

        if (!validation2.IsValid)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[Deck Error] {_p2Setup.PlayerName}의 덱이 유효하지 않습니다: {validation2.ErrorMessage}</color>");
            // return;
        }

        p1.InitializeBrain();
        p2.InitializeBrain();

        EventManager.OnLogMessage?.Invoke($"[게임 시작] {p1.Name} ({p1.Type}) VS {p2.Name} ({p2.Type})");

        // 2. 매치 루프 가동
        StartCoroutine(MatchLoop());
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

        GameLogicHelpers.DrawCards(p1, GameRules.StartingHands, context);
        GameLogicHelpers.DrawCards(p2, GameRules.StartingHands, context);

        // ==============================================================
        // ★ QA 인젝션 테스트 (유니티 환경)
        // ==============================================================
        // (예시) 봇 블루의 스택에 방어막 강제 장전
        // InjectTestCard(p2, "SONI-07", ZoneType.StackZone); // 마하 10
        // InjectTestCard(p2, "SONI-06", ZoneType.StackZone); // 엔진 예열

        // (예시) 플레이어 레드의 패에 무기 강제 쥐어주기
        // InjectTestCard(p1, "DAIN-02", ZoneType.Hand);      // 함포 준비, 발사!
        // ==============================================================

        EventManager.OnGameStart?.Invoke(p1, p2);
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

        context.ActivePlayer = p1; context.TargetPlayer = p2;
        p1.TakeResourceCard();
        if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p1, context)) yield break;

        context.ActivePlayer = p2; context.TargetPlayer = p1;
        p2.TakeResourceCard();
        if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p2, context)) yield break;

        yield return new WaitForSeconds(ActionDelay);
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
            // 휴먼 로직: UI 대기 + 타임아웃
            bool done = false;
            Card chosenCard = null;
            bool timeOutOccurred = false;

            EventManager.OnRequireSetPhaseAction?.Invoke(player, context, card =>
            {
                if (timeOutOccurred) return;
                chosenCard = card;
                done = true;
            });

            float waitLimit = GameRules.ChooseWaitTime / 1000f;
            float timer = 0f;

            while (!done && timer < waitLimit)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!done)
            {
                timeOutOccurred = true;
                EventManager.OnLogMessage?.Invoke($"<color=red>⏳ 제한 시간 초과! 시스템이 강제로 세트 카드를 무작위 선택합니다.</color>");
                // 타임아웃 시 봇과 동일한 전략으로 자동 선택
                var affordableCards = player.Hand.Where(c => GameLogicHelpers.GetEffectiveCost(c, player) <= player.ResourceZone.Count).ToList();
                var candidates = affordableCards.Count > 0 ? affordableCards : player.Hand;
                chosenCard = candidates.OrderBy(c => Guid.NewGuid()).FirstOrDefault();
            }

            onChosen(chosenCard);
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

        // 2. 수집된 결과를 일괄 적용 (여기서 실제 효과와 로그가 터짐)
        _p1RevealedCard = ApplyOpenChoice(p1, p1Choice);
        context.OpenPhaseStates[p1.Name] = new PlayerOpenPhaseState { HasOpened = (_p1RevealedCard != null), RevealedCard = _p1RevealedCard };

        _p2RevealedCard = ApplyOpenChoice(p2, p2Choice);
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
            // 휴먼 로직: UI 대기 + 타임아웃
            bool done = false;
            bool timeOutOccurred = false;
            OpenPhaseChoice chosen = OpenPhaseChoice.Abandon; // 기본값 타임아웃 시 폐기

            EventManager.OnRequireOpenPhaseAction?.Invoke(player, player.SetZoneCard, effectiveCost, context, c =>
            {
                if (timeOutOccurred) return;
                chosen = c;
                done = true;
            });

            float waitLimit = GameRules.ChooseWaitTime / 1000f;
            float timer = 0f;

            while (!done && timer < waitLimit)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!done)
            {
                timeOutOccurred = true;
                EventManager.OnLogMessage?.Invoke($"<color=red>⏳ 제한 시간 초과! 시스템이 강제로 세트 카드를 폐기합니다.</color>");
                chosen = OpenPhaseChoice.Abandon; // 안전하게 폐기 처리
            }

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
                if (ability.CanUse(player, context))
                {
                    // 즉발 효과라 대기 불필요
                    ability.OnOpenPhaseAbandon(player, context, _ => { });
                }
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
            // 휴먼은 UI를 통해 직접 발동 순서를 고름
            bool done = false;
            bool timeOutOccurred = false; // ★ 지각 응답 차단용 플래그

            EventManager.OnRequireCardPick?.Invoke(stackOwner, validStackCards, requiredCount, chosenCards =>
            {
                // 이미 시간이 지나서 시스템이 강제 선택했다면, 뒤늦게 들어온 UI 클릭은 무시!
                if (timeOutOccurred) return;
                selectedCards = chosenCards;
                done = true;
            });

            // ★ 무한 대기(WaitUntil)를 버리고, 타이머 루프를 돌립니다.
            // GameRules.ChooseWaitTime은 밀리초(기본 10000)이므로 초 단위(10f)로 변환
            float waitLimit = GameRules.ChooseWaitTime / 1000f;
            float timer = 0f;

            // 응답이 아직 안 왔고, 타이머가 제한 시간을 넘지 않았다면 계속 대기
            while (!done && timer < waitLimit)
            {
                timer += Time.deltaTime;
                yield return null; // 다음 프레임까지 대기
            }

            // ★ 루프를 빠져나왔는데 여전히 done이 false라면? = 타임아웃 발생!
            if (!done)
            {
                timeOutOccurred = true;
                EventManager.OnLogMessage?.Invoke($"<color=red>⏳ 제한 시간({waitLimit}초) 초과! 시스템이 강제로 방어 카드를 자동 선택합니다.</color>");

                // 봇과 동일하게 앞에서부터 필요한 만큼 강제 선택
                selectedCards = validStackCards.Take(requiredCount).ToList();
            }
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
        if (_dataManager == null) return;

        // 1. 데이터 매니저에서 카드 템플릿 검색
        if (!_dataManager.AllCards.TryGetValue(cardId, out Card template))
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] 주입 실패: ID '{cardId}'를 찾을 수 없습니다.</color>");
            return;
        }

        // 2. 실제 게임에 사용될 독립된 객체로 복제 (Deep Copy)
        Card injectedCard = template.Clone();

        // 3. 타겟 존의 성격에 맞춰 안전하게 밀어넣기
        switch (targetZone)
        {
            case ZoneType.Hand:
                player.InsertCard(ZoneType.Hand, injectedCard);
                break;
            case ZoneType.Deck:
                // 덱 조작: 다음 턴에 바로 뽑히도록 덱의 맨 위(0번 인덱스)에 강제 삽입
                player.Deck.Insert(0, injectedCard);
                break;
            case ZoneType.Graveyard:
                player.InsertCard(ZoneType.Graveyard, injectedCard);
                break;
            case ZoneType.ResourceZone:
                player.InsertCard(ZoneType.ResourceZone, injectedCard);
                break;
            case ZoneType.StackZone:
                player.AddToStackZone(injectedCard);
                break;
            case ZoneType.BattlefieldZone:
                player.PlaceBattlefield(injectedCard);
                break;
            default:
                EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] '{targetZone}'은(는) 주입이 지원되지 않는 존입니다.</color>");
                return;
        }

        EventManager.OnLogMessage?.Invoke($"<color=yellow>[QA Inject] {player.Name}의 {targetZone}에 '{injectedCard.Name}' 강제 장전 완료.</color>");
    }

    private void ResolveSimultaneousDeckout()
    {
        EventManager.OnTiebreaker.Invoke(p1, p2);
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
}
