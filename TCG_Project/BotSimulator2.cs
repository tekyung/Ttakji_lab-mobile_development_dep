using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Effects; // namespace 확인 필요

public class BotSimulator2
{
    private static GameDataManager _dataManager;
    private static BattleSystem _battleSystem;

    public static void Run()
    {
        Console.WriteLine("=== 🤖 봇 대전 시뮬레이터 (Fixed Logic) ===");
        InitializeSystem(); // 시스템 초기화 필수

        // 데이터 로드 확인
        if (_dataManager.AllCards.Count == 0)
        {
            Console.WriteLine("[Error] 카드 데이터가 로드되지 않았습니다!");
            return;
        }

        Player p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11003, 11007, 21001, 21002, 21003, 21004);
        Player p2 = CreateBotPlayer("Bot_Blue", 11004, 11005, 11006, 21001, 21002, 21003, 21004);

        GameContext context = new GameContext();
        context.Players.Add(p1);
        context.Players.Add(p2);

        DrawCards(p1, 3);
        DrawCards(p2, 3);

        int globalTurn = 1;
        bool isGameRunning = true;

        while (isGameRunning && globalTurn <= 20)
        {
            Player activePlayer = (globalTurn % 2 != 0) ? p1 : p2;
            Player targetPlayer = (globalTurn % 2 != 0) ? p2 : p1;

            context.ActivePlayer = activePlayer;
            context.TargetPlayer = targetPlayer;

            Console.WriteLine($"\n========== [ TURN {globalTurn} : {activePlayer.Name} (Deck: {activePlayer.Deck.Count}) ] ==========");

            if (!RunBotTurn(activePlayer, targetPlayer, globalTurn, context))
            {
                isGameRunning = GameSet(p1, p2);
                break;
            }
            globalTurn++;
        }

        Console.WriteLine("\n=== 게임 종료 ===");
        PrintGameResult(p1, p2);
    }

    private static bool RunBotTurn(Player me, Player enemy, int globalTurn, GameContext context)
    {
        // 1턴(선공): 0, 2턴(후공): 1, 3턴 이후: 3
        int setMana = 3;
        if (globalTurn == 1) setMana = 0;
        else if (globalTurn == 2) setMana = 1;

        // "현재 코스트가 3보다 높으면 낮추진 않음" (기존 보유 마나가 더 많을 경우 유지)
        if (me.Mana > 3) setMana = me.Mana;

        me.Mana = setMana;
        me.OnTurnStart();
        DrawCards(me, 1);

        Console.WriteLine($"--- HP: {me.Health} | Prize: {me.PrizePoints} | Mana: {me.Mana}/{setMana} | Hand: {me.Hand.Count} | Field: {me.Field.Count(c => c != null)} | Graveyard: {me.Graveyard.Count(c => c != null)} ---");

        Console.WriteLine($"{me.Name} 의 패: {string.Join(", ", me.Hand.Select(c => c.Name))}");
        
        // [Phase 1] 메인 페이즈
        bool actionTaken = true;
        int safetyCount = 0; // 무한 루프 방지용

        while (actionTaken && safetyCount < 20)
        {
            //me.UpdatePlayableCards(context);
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
                // Console.WriteLine($"   [Action] {unitToPlay.Name} 소환 시도");
                me.PlayCard(unitToPlay, context);
                actionTaken = true;
                continue; // 유닛 냈으면 다시 처음부터
            }
            else
            {
                Console.WriteLine($"{me.Name} : 소환할 유닛이 없습니다.");
            }

            // 2. 마나 보존 정책
            int unitCount = me.Field.Count(c => c != null);
            int manaThreshold = (unitCount <= 1) ? 1 : 2;

            if (me.Mana <= manaThreshold)
            {
                // Console.WriteLine("   [Skip] 마나 보존을 위해 스펠 사용 중단");
                break; // 배틀로 이동
            }

            // 3. 스펠 사용
            var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
            foreach (var skill in skills)
            {
                if (IsSkillUseful(skill, me, enemy))
                {
                    // Console.WriteLine($"   [Action] {skill.Name} 사용 시도");
                    me.PlayCard(skill, context);
                    actionTaken = true;
                    break;
                }
            }
        }

        if (me.Field.Count(c => c != null) > 0 && me.Mana >=1)
        {
            // [Phase 2] 배틀 페이즈
            ExecuteBattlePhase(me, enemy, context);
        }
        else
        {
            Console.WriteLine($"{me.Name} : 컨트롤하는 유닛이 없거나 배틀할 마나가 없습니다.");
        }

        return GameSet(me, enemy);
    }

    private static void ExecuteBattlePhase(Player me, Player enemy, GameContext context)
    {
        Console.WriteLine("   ⚔️ [배틀 페이즈 시작]");
        int loopSafety = 0;

        while (loopSafety < 10) // 최대 10번까지만 재탐색 (무한 루프 방지)
        {
            loopSafety++;
            bool attackOccurred = false;

            // 공격 가능한 내 유닛 찾기 (Power 높은 순)
            var attackers = me.Field
                .Where(c => c != null && !c.IsExhausted)
                .OrderByDescending(c => c.Power)
                .ToList();

            if (attackers.Count == 0) break; // 공격할 유닛 없음

            var enemyUnits = enemy.Field.Where(c => c != null).ToList();

            foreach (var attacker in attackers)
            {
                // 마나 체크 (중요: 마나 없으면 아예 스킵)
                if (me.Mana < attacker.AttackCost)
                {
                    // Console.WriteLine($"      (Skip) {attacker.Name} 마나 부족");
                    continue;
                }

                object finalTarget = null;

                if (enemyUnits.Count > 0)
                {
                    // 이길 수 있는 적 중 가장 센 놈
                    var validTargets = enemyUnits
                        .Where(e => e.Power <= attacker.Power)
                        .OrderByDescending(e => e.Power)
                        .ToList();

                    if (validTargets.Count > 0)
                    {
                        finalTarget = validTargets[0];
                    }
                }
                else
                {
                    finalTarget = enemy; // 명치
                }

                if (finalTarget != null)
                {
                    _battleSystem.Attack(attacker, finalTarget, context);
                    attackOccurred = true;
                    if (GameSet(me, enemy)) break; // 공격 발생! 다시 처음부터 탐색 (재귀 효과)
                }
            }

            // 한 바퀴 다 돌았는데 아무도 공격을 안/못 했다면 종료
            if (!attackOccurred) break;
        }

        Console.WriteLine("   ⚔️ [배틀 페이즈 종료]");
    }

    // --- Helpers ---
    private static void InitializeSystem()
    {
        _dataManager = new GameDataManager();
        _dataManager.LoadAllData("./Data"); // 경로 주의!
        _battleSystem = new BattleSystem();
    }

    private static Player CreateBotPlayer(string name, params int[] ids)
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

    private static void DrawCards(Player p, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var c = p.ExtractCard(ZoneType.Deck, "Top");
            if (c != null) p.InsertCard(ZoneType.Hand, c);
        }
    }

    public static bool GameSet(Player me, Player emermy)
    {
        if (me.PrizePoints >= 7 || emermy.PrizePoints >= 7)
        { return false; }
        else
        { return true; }
    }

    private static void PrintGameResult(Player p1, Player p2)
    {
        Console.WriteLine("\n========== [ Result ] ==========");
        Console.WriteLine($"{p1.Name}: {p1.PrizePoints} Prize");
        Console.WriteLine($"{p2.Name}: {p2.PrizePoints} Prize");
        if (p1.PrizePoints >= 7 && p2.PrizePoints >= 7) Console.WriteLine("🤝 무승부!");
        else if (p1.PrizePoints >= 7) Console.WriteLine($"🏆 승리: {p2.Name}");
        else if (p2.PrizePoints >= 7) Console.WriteLine($"🏆 승리: {p1.Name}");
        else Console.WriteLine("🤝 무승부 (턴 오버)");
    }

    private static bool IsSkillUseful(Card skill, Player me, Player enemy)
    {
        // 간단 체크 로직
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