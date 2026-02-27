using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("Simulation Settings")]
    [Tooltip("각 행동 사이의 대기 시간 (초)")]
    public float ActionDelay = 1.0f;
    [Tooltip("턴 시작/종료 대기 시간 (초)")]
    public float TurnDelay = 1.5f;

    // 시스템 객체
    private GameDataManager _dataManager;
    private BattleSystem _battleSystem;

    // 플레이어 및 컨텍스트
    private Player p1;
    private Player p2;
    private GameContext context;

    // 게임 상태 제어
    private bool isGameRunning = false;
    private int globalTurn = 1;

    // ★ 1. 이벤트 구독/해지
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

    // ★ 2. 게임 종료 이벤트 수신부
    private void HandleGameSet(Player winner)
    {
        EventManager.OnLogMessage?.Invoke($"\n🎉 [GAME SET] {winner.Name} 승리!");
        isGameRunning = false;
        StopAllCoroutines(); // 유니티 코루틴 강제 종료로 오버킬 방지
    }

    private void HandleGameDraw(Player p1, Player p2, int turn)
    {
        EventManager.OnLogMessage?.Invoke($"\n🤝 [GAME DRAW] {p1.Name} vs {p2.Name} 무승부! (턴 {turn})");
        isGameRunning = false;
        StopAllCoroutines();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void StartBot()
    {
        EventManager.OnLogMessage?.Invoke("=== 🤖 봇 대전 시뮬레이터 (Unity Ver) ===");

        // 유니티 콘솔에서 Rich Text 색상을 보려면 BattleManager에서도 구독 필요
        EventManager.OnLogMessage += msg => UnityEngine.Debug.Log(msg);

        StartCoroutine(GameLoop());
    }

    // 1. 초기화 및 게임 루프
    private IEnumerator GameLoop()
    {
        InitializeSystem();

        if (_dataManager.AllCards.Count == 0)
        {
            EventManager.OnLogMessage?.Invoke("<color=red>[Error] 카드 데이터가 로드되지 않았습니다!</color>");
            yield break;
        }

        p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11003, 11007, 21001, 21002, 21003, 21004);
        p2 = CreateBotPlayer("Bot_Blue", 11004, 11005, 11006, 21001, 21002, 21003, 21004);

        context = new GameContext();
        context.Players.Add(p1);
        context.Players.Add(p2);

        EventManager.OnGameStart?.Invoke(p1, p2);

        // 초기 드로우
        DrawCards(p1, GameRules.StartingHands);
        DrawCards(p2, GameRules.StartingHands);

        isGameRunning = true;
        globalTurn = 1;

        // 메인 게임 루프
        while (isGameRunning && globalTurn <= 20)
        {
            Player activePlayer = (globalTurn % 2 != 0) ? p1 : p2;
            Player targetPlayer = (globalTurn % 2 != 0) ? p2 : p1;

            context.ActivePlayer = activePlayer;
            context.TargetPlayer = targetPlayer;

            EventManager.OnLogMessage?.Invoke($"\n========== [ TURN {globalTurn} : {activePlayer.Name} (Deck: {activePlayer.Deck.Count}) ] ==========");
            EventManager.OnTurnStart?.Invoke(globalTurn, activePlayer.Name);

            yield return new WaitForSeconds(TurnDelay);

            // 턴 진행 코루틴 체인 시작 (드로우 페이즈부터)
            yield return StartCoroutine(ExecuteDrawPhaseRoutine(activePlayer, targetPlayer, globalTurn));

            globalTurn++;
        }

        if (!context.IsGameOver && globalTurn > 20)
        {
            context.IsGameOver = true;
            EventManager.OnGameDraw?.Invoke(p1, p2, 20);
        }

        EventManager.OnLogMessage?.Invoke("\n=== 게임 종료 ===");
        PrintGameResult(p1, p2);
    }

    #region Phase Logic (코루틴 기반 페이즈 체인)

    // 1. 드로우 페이즈 코루틴
    private IEnumerator ExecuteDrawPhaseRoutine(Player me, Player enemy, int currentTurn)
    {
        if (context.IsGameOver) yield break;

        EventManager.OnDrawPhase?.Invoke(me.Name, currentTurn);
        EventManager.OnLogMessage?.Invoke($"[ 🃏 드로우 페이즈 ]");

        int setMana = GameRules.BasicEnergy;
        if (currentTurn == 1) setMana = GameRules.FirstPlayerFirstTurnEnergy;
        else if (currentTurn == 2) setMana = GameRules.SecondPlayerFirstTurnEnergy;

        if (me.Mana > GameRules.BasicEnergy) setMana = me.Mana;
        me.Mana = setMana;
        EventManager.OnManaChange?.Invoke(me, me.Mana);

        me.OnTurnStart(); // 턴 시작 피로도 회복 등
        DrawCards(me, GameRules.DrawPerTurn); // 턴 당 1장 드로우 (GameRules 확인 필요, 기본 1)

        yield return new WaitForSeconds(ActionDelay);

        yield return StartCoroutine(ExecuteMainPhaseRoutine(me, enemy, currentTurn));
    }

    // 2. 메인 페이즈 코루틴
    private IEnumerator ExecuteMainPhaseRoutine(Player me, Player enemy, int currentTurn)
    {
        if (context.IsGameOver) yield break;

        EventManager.OnMainPhase?.Invoke(me.Name, currentTurn);
        EventManager.OnLogMessage?.Invoke($"[ ⚙️ 메인 페이즈 ]");

        bool actionTaken = true;
        int safetyCount = 0;

        while (actionTaken && safetyCount < 20)
        {
            safetyCount++;
            actionTaken = false;
            if (context.IsGameOver) yield break;

            var handClone = new List<Card>(me.Hand);

            // --- 행동 1: 유닛 소환 ---
            var unitToPlay = handClone.Where(c => c.Type == CardType.Unit && c.IsPlayable(context)).OrderByDescending(c => c.Power).FirstOrDefault();
            if (unitToPlay != null)
            {
                bool isEffectRunning = true; // ★ 대기 플래그 활성화

                // 카드를 내고, 콜백으로 플래그를 끄도록 지시함
                me.PlayCard(unitToPlay, context, () => { isEffectRunning = false; });

                // ★ 카드의 모든 효과(소환 시 효과, 타겟팅 등)가 끝날 때까지 이 코루틴을 일시 정지시킴
                yield return new WaitUntil(() => !isEffectRunning);

                actionTaken = true;
                yield return new WaitForSeconds(ActionDelay); // 카드를 낸 직후 연출을 감상할 약간의 딜레이
                continue;
            }

            // 마나 보존 정책 검사
            int unitCount = me.Field.Count(c => c != null);
            int manaThreshold = (unitCount <= 1) ? 1 : 2;
            if (me.Mana <= manaThreshold) break;

            // --- 행동 2: 스킬 사용 ---
            var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
            foreach (var skill in skills)
            {
                if (IsSkillUseful(skill, me, enemy))
                {
                    bool isEffectRunning = true; // ★ 대기 플래그 활성화

                    // 스킬 발동 (만약 타겟팅 모드가 HumanChoice라면 여기서 무한정 대기하게 됨)
                    me.PlayCard(skill, context, () => { isEffectRunning = false; });

                    // ★ 스킬 효과 연산(데미지 계산, 파괴 처리 등)이 다 끝날 때까지 대기
                    yield return new WaitUntil(() => !isEffectRunning);

                    actionTaken = true;
                    yield return new WaitForSeconds(ActionDelay);
                    break;
                }
            }
        }

        if (context.IsGameOver) yield break;

        // 메인 페이즈 결산 및 배틀 페이즈 진입 검사
        int aliveUnitsCount = me.Field.Count(c => c != null);
        EventManager.OnLogMessage?.Invoke($"\n[ ⚙️ 메인 페이즈 종료 ]");
        EventManager.OnLogMessage?.Invoke($"--- 잔여 마나: {me.Mana} | 필드 유닛: {aliveUnitsCount}/{GameRules.MaxFieldUnitCount} ---");
        EventManager.OnLogMessage?.Invoke($"{me.Name}의 필드 : {string.Join(" / ", me.Field.Select(c => c != null ? c.Name : "[빈칸]"))}");

        if (aliveUnitsCount == 0)
        {
            EventManager.OnLogMessage?.Invoke($"{me.Name} : 필드에 유닛이 없어 배틀 페이즈를 건너뜁니다.");
        }
        else
        {
            var readyUnits = me.Field.Where(c => c != null && !c.IsExhausted).ToList();
            if (readyUnits.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke($"{me.Name} : 공격 가능한 상태의 유닛이 없어 배틀 페이즈를 건너뜁니다.");
            }
            else
            {
                int minAttackCost = readyUnits.Min(c => c.AttackCost);
                if (me.Mana < minAttackCost)
                {
                    EventManager.OnLogMessage?.Invoke($"{me.Name} : 마나가 부족하여 배틀 페이즈를 건너뜁니다. (최소 필요: {minAttackCost}, 보유 마나: {me.Mana})");
                }
                else
                {
                    yield return StartCoroutine(ExecuteBattlePhaseRoutine(me, enemy, currentTurn));
                }
            }
        }

        yield return StartCoroutine(ExecuteEndPhaseRoutine(me, enemy, currentTurn));
    }

    // 3. 배틀 페이즈 코루틴
    private IEnumerator ExecuteBattlePhaseRoutine(Player me, Player enemy, int currentTurn)
    {
        if (context.IsGameOver) yield break;

        EventManager.OnBattlePhase?.Invoke(me.Name, currentTurn);
        EventManager.OnLogMessage?.Invoke($"[ ⚔️ 배틀 페이즈 ]");

        int loopSafety = 0;

        while (loopSafety < 10)
        {
            loopSafety++;
            bool attackOccurred = false;

            var attackers = me.Field.Where(c => c != null && !c.IsExhausted).OrderByDescending(c => c.Power).ToList();
            if (attackers.Count == 0) break;

            var enemyUnits = enemy.Field.Where(c => c != null).ToList();

            foreach (var attacker in attackers)
            {
                if (context.IsGameOver) yield break;
                if (me.Mana < attacker.AttackCost) continue;

                object finalTarget = null;

                if (enemyUnits.Count > 0)
                {
                    finalTarget = enemyUnits.Where(e => e.Power <= attacker.Power).OrderByDescending(e => e.Power).FirstOrDefault();
                    if (finalTarget == null) continue; // 이길 적이 없으면 공격 포기 (자살 방지)
                }
                else
                {
                    finalTarget = enemy; // 명치
                }

                if (finalTarget != null)
                {
                    _battleSystem.Attack(attacker, finalTarget, context);
                    attackOccurred = true;
                    yield return new WaitForSeconds(ActionDelay);
                    break;
                }
            }

            if (!attackOccurred) break;
        }
    }

    // 4. 엔드 페이즈 코루틴
    private IEnumerator ExecuteEndPhaseRoutine(Player me, Player enemy, int currentTurn)
    {
        if (context.IsGameOver) yield break;

        EventManager.OnEndPhase?.Invoke(me.Name, currentTurn);
        EventManager.OnLogMessage?.Invoke($"[ 🛑 엔드 페이즈 ]\n");

        yield return new WaitForSeconds(TurnDelay);
    }

    #endregion

    // --- Helpers ---
    private void InitializeSystem()
    {
        // 1차 시도 경로: 유니티 환경 (Assets/Resources/GameData)
        string primaryPath = Path.Combine(Application.dataPath, "Resources", "GameData");

        // 2차 시도 경로: 콘솔 환경 및 기본 폴백 (./Data)
        string fallbackPath = "./Data";

        // 최종 결정된 경로
        string targetPath = primaryPath;

        // 1. 폴백(Fallback) 로직: 1차 경로가 없으면 2차 경로로 전환
        if (!Directory.Exists(primaryPath))
        {
            EventManager.OnLogMessage?.Invoke($"<color=yellow>[System] 1차 경로({primaryPath})를 찾을 수 없어 2차 경로({fallbackPath})를 시도합니다.</color>");
            targetPath = fallbackPath;
        }
        else
        {
            EventManager.OnLogMessage?.Invoke($"<color=cyan>[System] 게임 데이터를 '{targetPath}'에서 로드합니다.</color>");
        }

        // 2. 룰 데이터 로드
        try
        {
            string rulesPath = Path.Combine(targetPath, "CommonConfig.json");
            GameRules.LoadRules(rulesPath);
        }
        catch (System.Exception e)
        {
            EventManager.OnLogMessage?.Invoke($"<color=red>[Rules Load Error] {e.Message}</color>");
        }

        // 3. 시스템 및 매니저 초기화
        _dataManager = new GameDataManager();
        _dataManager.LoadAllData(targetPath);
        _battleSystem = new BattleSystem();
    }

    private Player CreateBotPlayer(string name, params int[] ids)
    {
        Player p = new Player { Name = name, PrizePoints = 0, Mana = 0 }; // Health 삭제, Prize 도입 반영
        List<Card> deck = new List<Card>();
        foreach (int id in ids)
        {
            if (_dataManager.AllCards.TryGetValue(id.ToString(), out Card c))
                for (int i = 0; i < 2; i++) deck.Add(c.Clone());
        }
        p.SetDeck(deck);
        return p;
    }

    private void DrawCards(Player p, int count)
    {
        if (p.Deck.Count >= count)
        {
            for (int i = 0; i < count; i++)
            {
                var c = p.ExtractCard(ZoneType.Deck, "Top");
                if (c != null) p.InsertCard(ZoneType.Hand, c);
            }
            EventManager.OnLogMessage?.Invoke($"{p.Name} 드로우: {count}장 (남은 덱: {p.Deck.Count}장)");
        }
        else
        {
            int drawnCount = p.Deck.Count;
            while (p.Deck.Count > 0)
            {
                var c = p.ExtractCard(ZoneType.Deck, "Top");
                if (c != null) p.InsertCard(ZoneType.Hand, c);
            }
            EventManager.OnLogMessage?.Invoke($"{p.Name} 드로우 시도: {count}장 중 {drawnCount}장 성공 (덱 고갈)");
        }
    }

    private void PrintGameResult(Player p1, Player p2)
    {
        EventManager.OnLogMessage?.Invoke("\n========== [ Result ] ==========");
        EventManager.OnLogMessage?.Invoke($"{p1.Name}: {p1.PrizePoints} Prize");
        EventManager.OnLogMessage?.Invoke($"{p2.Name}: {p2.PrizePoints} Prize");

        // 여기서 승리 로그를 중복해서 출력하지 않음 (HandleGameSet에서 이미 처리됨)
    }

    private bool IsSkillUseful(Card skill, Player me, Player enemy)
    {
        if (skill.Id == "21001") { return enemy.Field.Any(c => c != null && c.Power <= 300); }
        if (skill.Id == "21003") { if (me.Field.All(c => c == null)) return false; }
        if (skill.Id == "21002") { if (me.Deck.Count == 0) return false; }
        if (skill.Id == "21004") { if (me.Hand.Count <= 1) return false; }
        return true;
    }
}