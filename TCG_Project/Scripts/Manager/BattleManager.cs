using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO; // 파일 경로 처리를 위해 필요
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

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

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        Debug.Log("=== 🤖 봇 대전 시뮬레이터 (Unity Ver) ===");
        StartCoroutine(GameLoop());
    }

    // 1. 초기화 및 게임 루프
    private IEnumerator GameLoop()
    {
        // 시스템 초기화
        InitializeSystem();

        // 데이터 로드 확인
        if (_dataManager.AllCards.Count == 0)
        {
            Debug.LogError("[Error] 카드 데이터가 로드되지 않았습니다!");
            yield break;
        }

        // 봇 생성 및 설정 (BotSimulator2와 동일한 덱)
        p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11003, 11007, 21001, 21002, 21003, 21004);
        p2 = CreateBotPlayer("Bot_Blue", 11004, 11005, 11006, 21001, 21002, 21003, 21004);

        context = new GameContext();
        context.Players.Add(p1);
        context.Players.Add(p2);

        // 초기 드로우
        DrawCards(p1, 3);
        DrawCards(p2, 3);

        isGameRunning = true;
        globalTurn = 1;

        // 메인 게임 루프
        while (isGameRunning && globalTurn <= 20)
        {
            Player activePlayer = (globalTurn % 2 != 0) ? p1 : p2;
            Player targetPlayer = (globalTurn % 2 != 0) ? p2 : p1;

            context.ActivePlayer = activePlayer;
            context.TargetPlayer = targetPlayer;

            Debug.Log($"\n========== [ TURN {globalTurn} : {activePlayer.Name} (Deck: {activePlayer.Deck.Count}) ] ==========");

            // 턴 시작 연출 대기
            yield return new WaitForSeconds(TurnDelay);

            // 봇 턴 실행 (코루틴 대기)
            yield return StartCoroutine(RunBotTurnRoutine(activePlayer, targetPlayer, globalTurn));

            // 게임 종료 조건 체크 (승점이 7점 이상이면 종료)
            if (GameSet(p1, p2))
            {
                isGameRunning = false;
                break;
            }

            globalTurn++;
        }

        Debug.Log("\n=== 게임 종료 ===");
        PrintGameResult(p1, p2);
    }

    // 2. 봇 행동 로직 (코루틴)
    private IEnumerator RunBotTurnRoutine(Player me, Player enemy, int currentTurn)
    {
        if (me.Health <= 0 || enemy.Health <= 0) yield break;

        // 마나 규칙: 1턴=0, 2턴=1, 3턴+=3 (기존 보유량이 더 많으면 유지)
        int setMana = 3;
        if (currentTurn == 1) setMana = 0;
        else if (currentTurn == 2) setMana = 1;

        if (me.Mana > 3) setMana = me.Mana;
        me.Mana = setMana;

        me.OnTurnStart();
        DrawCards(me, 1);

        Debug.Log($"--- HP: {me.Health} | Prize: {me.PrizePoints} | Mana: {me.Mana}/{setMana} | Hand: {me.Hand.Count} | Field: {me.Field.Count(c => c != null)} ---");
        Debug.Log($"{me.Name} 의 패: {string.Join(", ", me.Hand.Select(c => c.Name))}");

        yield return new WaitForSeconds(ActionDelay); // 드로우 후 대기

        // [Phase 1] 메인 페이즈
        bool actionTaken = true;
        int safetyCount = 0;

        while (actionTaken && safetyCount < 20)
        {
            safetyCount++;
            actionTaken = false;
            var handClone = new List<Card>(me.Hand);

            // 1. 유닛 소환 (Power 높은 순)
            var unitToPlay = handClone
                .Where(c => c.Type == CardType.Unit && c.IsPlayable(context))
                .OrderByDescending(c => c.Power)
                .FirstOrDefault();

            if (unitToPlay != null)
            {
                me.PlayCard(unitToPlay, context);
                actionTaken = true;
                yield return new WaitForSeconds(ActionDelay); // 소환 후 대기
                continue;
            }
            else
            {
                // 소환할 유닛이 없음 (로그 생략 가능)
            }

            // 2. 마나 보존 정책
            int unitCount = me.Field.Count(c => c != null);
            int manaThreshold = (unitCount <= 1) ? 1 : 2;

            if (me.Mana <= manaThreshold)
            {
                break; // 스펠 사용 중단, 배틀 페이즈로 이동
            }

            // 3. 스킬 사용
            var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
            foreach (var skill in skills)
            {
                if (IsSkillUseful(skill, me, enemy))
                {
                    me.PlayCard(skill, context);
                    actionTaken = true;
                    yield return new WaitForSeconds(ActionDelay); // 스킬 사용 후 대기
                    break;
                }
            }
        }

        // [Phase 2] 배틀 페이즈
        if (me.Field.Count(c => c != null) > 0 && me.Mana >= 1)
        {
            yield return StartCoroutine(ExecuteBattlePhaseRoutine(me, enemy));
        }
        else
        {
            Debug.Log($"{me.Name} : 컨트롤하는 유닛이 없거나 배틀할 마나가 없습니다.");
        }
    }

    private IEnumerator ExecuteBattlePhaseRoutine(Player me, Player enemy)
    {
        Debug.Log("   ⚔️ [배틀 페이즈 시작]");
        int loopSafety = 0;

        while (loopSafety < 10)
        {
            loopSafety++;
            bool attackOccurred = false;

            // 공격 가능한 유닛 탐색 (Power 높은 순)
            var attackers = me.Field
                .Where(c => c != null && !c.IsExhausted)
                .OrderByDescending(c => c.Power)
                .ToList();

            if (attackers.Count == 0) break;

            var enemyUnits = enemy.Field.Where(c => c != null).ToList();

            foreach (var attacker in attackers)
            {
                // 마나 체크
                if (me.Mana < attacker.AttackCost) continue;

                object finalTarget = null;

                if (enemyUnits.Count > 0)
                {
                    // 적 유닛 중 가장 강한 놈 타겟팅 (단, 이길 수 있는 상대)
                    var validTargets = enemyUnits
                        .Where(e => e.Power <= attacker.Power)
                        .OrderByDescending(e => e.Power)
                        .ToList();

                    if (validTargets.Count > 0) finalTarget = validTargets[0];
                }
                else
                {
                    finalTarget = enemy; // 명치
                }

                if (finalTarget != null)
                {
                    _battleSystem.Attack(attacker, finalTarget, context);
                    attackOccurred = true;
                    yield return new WaitForSeconds(ActionDelay); // 공격 연출 대기

                    // 승패 판정으로 조기 종료
                    if (GameSet(me, enemy)) yield break;

                    break; // 공격 발생 시 루프 재시작
                }
            }

            if (!attackOccurred) break;
        }
        Debug.Log("   ⚔️ [배틀 페이즈 종료]");
    }

    // --- Helpers ---

    private void InitializeSystem()
    {
        // [중요] 유니티 에디터 경로 설정
        // Assets/Resources/GameData 경로를 가리킵니다.
        string resourcePath = Path.Combine(Application.dataPath, "Resources", "GameData");

        // 1. 룰 로드
        try
        {
            string rulesPath = Path.Combine(resourcePath, "Rules.json");
            GameRules.LoadRules(rulesPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Rules Load Error] {e.Message}");
        }

        // 2. 데이터 매니저 초기화
        _dataManager = new GameDataManager();
        _dataManager.LoadAllData(resourcePath); // 경로 전달

        _battleSystem = new BattleSystem();
    }

    private Player CreateBotPlayer(string name, params int[] ids)
    {
        Player p = new Player { Name = name, Health = 7, Mana = 0 };
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
        for (int i = 0; i < count; i++)
        {
            var c = p.ExtractCard(ZoneType.Deck, "Top");
            if (c != null) p.InsertCard(ZoneType.Hand, c);
        }
    }

    private bool GameSet(Player me, Player enemy)
    {
        // 승점이 7점 이상이면 게임 종료
        return me.PrizePoints >= 7 || enemy.PrizePoints >= 7;
    }

    private void PrintGameResult(Player p1, Player p2)
    {
        Debug.Log("\n========== [ Result ] ==========");
        Debug.Log($"{p1.Name}: {p1.PrizePoints} Prize");
        Debug.Log($"{p2.Name}: {p2.PrizePoints} Prize");
        if (p1.PrizePoints >= 7 && p2.PrizePoints >= 7) Debug.Log("🤝 무승부!");
        else if (p1.PrizePoints >= 7) Debug.Log($"🏆 승리: {p1.Name}");
        else if (p2.PrizePoints >= 7) Debug.Log($"🏆 승리: {p2.Name}");
        else Debug.Log("🤝 무승부 (턴 오버)");
    }

    private bool IsSkillUseful(Card skill, Player me, Player enemy)
    {
        if (skill.Id == "21001" || skill.Effects.Any(e => e.GetType().Name.Contains("ModifyStat")))
        {
            if (enemy.Field.All(c => c == null)) return false;
        }
        if (skill.Id == "21003") { if (me.Field.All(c => c == null)) return false; }
        if (skill.Id == "21002") { if (me.Deck.Count == 0) return false; }
        if (skill.Id == "21004") { if (me.Hand.Count <= 1) return false; }
        return true;
    }
}