using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Interfaces;

public class BotSimulator2
{
    private static GameDataManager _dataManager;
    private static BattleSystem _battleSystem;

    public static void Run()
    {
        Console.WriteLine("=== 🤖 봇 대전 시뮬레이터 (Advanced AI) ===");
        InitializeSystem();

        Player p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11004, 21001, 21003);
        Player p2 = CreateBotPlayer("Bot_Blue", 11003, 11005, 11006, 21002, 21004);

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
                isGameRunning = false;
                break;
            }
            globalTurn++;
        }

        Console.WriteLine("\n=== 게임 종료 ===");
        PrintGameResult(p1, p2);
    }

    private static bool RunBotTurn(Player me, Player enemy, int globalTurn, GameContext context)
    {
        if (me.Health <= 0 || enemy.Health <= 0) return false;

        int maxMana = (globalTurn <= 2) ? 1 : 3;
        me.Mana = maxMana;
        me.OnTurnStart();
        DrawCards(me, 1);

        Console.WriteLine($"--- HP: {me.Health} | Mana: {me.Mana}/{maxMana} | Hand: {me.Hand.Count} | Field: {me.Field.Count(c => c != null)} ---");

        // [Phase 1] 메인 페이즈 (소환 및 스펠)
        bool actionTaken = true;
        while (actionTaken)
        {
            // [요청 1] 마나 보존 정책 (마나가 적으면 배틀로 직행)
            int unitCount = me.Field.Count(c => c != null);
            int manaThreshold = (unitCount <= 1) ? 1 : 2;

            if (me.Mana <= manaThreshold)
            {
                // 단, 소환 가능한 유닛이 있다면 마나를 다 써서라도 필드를 채우는 게 유리할 수 있으므로
                // "스킬"만 제한하거나, 아예 행동을 멈출지 결정해야 합니다.
                // 요청하신 대로 "스펠 사용 중단"의 의미로 해석하여 루프를 탈출합니다.
                // (만약 유닛 소환도 멈추길 원하시면 이대로 두시면 됩니다.)
                break;
            }

            actionTaken = false;
            var handClone = new List<Card>(me.Hand);

            // 1. 유닛 소환 (최우선 - 마나 제한 무시 or 별도 체크 가능)
            var unitToPlay = handClone.FirstOrDefault(c => c.Type == CardType.Unit && c.IsPlayable(context));
            if (unitToPlay != null)
            {
                me.PlayCard(unitToPlay, context);
                actionTaken = true;
                continue;
            }

            // 2. 스킬 사용 (AI 판단)
            var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
            foreach (var skill in skills)
            {
                if (IsSkillUseful(skill, me, enemy))
                {
                    me.PlayCard(skill, context);
                    actionTaken = true;
                    break;
                }
            }
        }

        // [Phase 2] 배틀 페이즈 (개선됨)
        ExecuteBattlePhase(me, enemy, context);

        return enemy.Health > 0 && me.Health > 0;
    }

    private static void ExecuteBattlePhase(Player me, Player enemy, GameContext context)
    {
        // [요청 3] 재탐색을 위한 루프
        // 한 번이라도 공격이 발생하면(전황이 바뀌면) 처음부터 다시 최적의 공격을 찾습니다.
        while (true)
        {
            bool attackOccurred = false;

            // [요청 2] Power가 높은 유닛부터 탐색
            var attackers = me.Field
                .Where(c => c != null && !c.IsExhausted) // 행동 안 한 유닛만
                .OrderByDescending(c => c.Power)       // 센 놈 먼저!
                .ToList();

            if (attackers.Count == 0) break; // 공격할 유닛 없으면 종료

            var enemyUnits = enemy.Field.Where(c => c != null).ToList();

            foreach (var attacker in attackers)
            {
                if (me.Mana < attacker.AttackCost) continue;

                object finalTarget = null;

                if (enemyUnits.Count > 0)
                {
                    // 도발 룰: 유닛이 있으면 유닛 먼저
                    // 내 공격력 이하인 적 중 가장 센 놈(위협적인 놈)을 잡는다? (혹은 약한 놈?)
                    // 여기선 기존 로직(약한 순) 유지하되, 필요하면 OrderByDescending으로 변경 가능
                    var validTargets = enemyUnits
                        .Where(e => e.Power <= attacker.Power)
                        .OrderBy(e => e.Power)
                        .ToList();

                    if (validTargets.Count > 0)
                    {
                        finalTarget = validTargets[0];
                    }
                    else
                    {
                        // 공격 포기 (대기)
                        // 주의: 여기서 로그를 계속 찍으면 루프 돌 때마다 도배될 수 있음
                    }
                }
                else
                {
                    // 적 유닛 없음 -> 명치
                    finalTarget = enemy;
                }

                if (finalTarget != null)
                {
                    _battleSystem.Attack(attacker, finalTarget, context);

                    // 공격 성공! 루프 재시작 (요청 3)
                    // 전황이 바뀌었으므로(적 사망 등), 남은 유닛들의 최적 타겟이 바뀔 수 있음
                    attackOccurred = true;
                    break;
                }
            }

            // 모든 유닛을 다 훑었는데 아무도 공격을 안/못 했다면 배틀 종료
            if (!attackOccurred) break;
        }
    }

    // --- (이하 Helper 메서드들은 기존과 동일) ---
    private static void InitializeSystem() { _dataManager = new GameDataManager(); _dataManager.LoadAllData("./Data"); _battleSystem = new BattleSystem(); }
    private static Player CreateBotPlayer(string name, params int[] ids)
    {
        /* 기존과 동일 */
        Player p = new Player { Name = name, Health = 20, Mana = 0 };
        List<Card> deck = new List<Card>();
        foreach (int id in ids) { if (_dataManager.AllCards.TryGetValue(id.ToString(), out Card c)) for (int i = 0; i < 3; i++) deck.Add(c.Clone()); }
        p.SetDeck(deck); return p;
    }
    private static void DrawCards(Player p, int count) { for (int i = 0; i < count; i++) { var c = p.ExtractCard(ZoneType.Deck, "Top"); if (c != null) p.InsertCard(ZoneType.Hand, c); } }
    private static void PrintGameResult(Player p1, Player p2)
    {
        Console.WriteLine("\n========== [ Result ] ==========");
        Console.WriteLine($"{p1.Name}: {p1.Health} HP");
        Console.WriteLine($"{p2.Name}: {p2.Health} HP");
        if (p1.Health <= 0 && p2.Health <= 0) Console.WriteLine("🤝 무승부!");
        else if (p1.Health <= 0) Console.WriteLine($"🏆 승리: {p2.Name}");
        else if (p2.Health <= 0) Console.WriteLine($"🏆 승리: {p1.Name}");
        else Console.WriteLine("🤝 무승부 (턴 오버)");
    }
    private static bool IsSkillUseful(Card skill, Player me, Player enemy)
    {
        // ... (기존 로직 유지) ...
        if (skill.Id == "21001" || skill.Effects.Any(e => e is ModifyStatEffect))
        {
            if (enemy.Field.All(c => c == null)) return false;
        }
        if (skill.Id == "21003") { if (me.Field.All(c => c == null)) return false; }
        if (skill.Id == "21002") { if (me.Deck.Count == 0) return false; }
        return true;
    }
}