using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Effects; // 데이터 로더 등 필요 시
using TCG_Project.Scripts.Interfaces;

public class BotSimulator2
{
	private static GameDataManager _dataManager;
	private static BattleSystem _battleSystem;

	public static void Run()
	{
		Console.WriteLine("=== 🤖 봇 대전 시뮬레이터 (Rule Updated) ===");

		InitializeSystem();

		// 플레이어 생성 (Bot_Red vs Bot_Blue)
		Player p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11004, 21001, 21003);
		Player p2 = CreateBotPlayer("Bot_Blue", 11003, 11005, 11006, 21002, 21004);

		GameContext context = new GameContext();
		context.Players.Add(p1);
		context.Players.Add(p2);

		// 첫 패 드로우 (3장)
		DrawCards(p1, 3);
		DrawCards(p2, 3);

		// [수정 1] 글로벌 턴 카운트 (행동 마칠 때마다 증가)
		int globalTurn = 1;
		bool isGameRunning = true;

		// 최대 20턴(각자 10번씩 행동)까지 진행
		while (isGameRunning && globalTurn <= 20)
		{
			// 홀수 턴: P1, 짝수 턴: P2
			Player activePlayer = (globalTurn % 2 != 0) ? p1 : p2;
			Player targetPlayer = (globalTurn % 2 != 0) ? p2 : p1;

			context.ActivePlayer = activePlayer;
			context.TargetPlayer = targetPlayer;

			Console.WriteLine($"\n========== [ TURN {globalTurn} : {activePlayer.Name} : 남은 덱 {activePlayer.Deck.Count}장 ] ==========");

			if (!RunBotTurn(activePlayer, targetPlayer, globalTurn, context))
			{
				isGameRunning = false;
				break;
			}

			globalTurn++; // [수정 1] 턴 종료 시 증가
		}

		Console.WriteLine("\n=== 게임 종료 ===");
		PrintGameResult(p1, p2);
	}

	private static bool RunBotTurn(Player me, Player enemy, int globalTurn, GameContext context)
	{
		if (me.Health <= 0 || enemy.Health <= 0) return false;

		// [수정 2] 마나 규칙 (1~2턴: 1, 3턴부터: 3)
		int maxMana = (globalTurn <= 2) ? 1 : 3;
		me.Mana = maxMana;

		// [수정 4] 턴 시작 시 상태 및 HP 초기화
		me.OnTurnStart();
		DrawCards(me, 1);
		Console.WriteLine($"--- Status: HP {me.Health} / Mana {me.Mana} (Max {maxMana}) / Hand {me.Hand.Count} ---");

		// [수정 3] 메인 페이즈: 가능한 모든 행동 반복
        bool actionTaken = true;
        while (actionTaken)
        {
            actionTaken = false;
            var handClone = new List<Card>(me.Hand); 

            // 1. 유닛 소환 (최우선)
            var unitToPlay = handClone.FirstOrDefault(c => c.Type == CardType.Unit && c.IsPlayable(context));
            if (unitToPlay != null)
            {
                me.PlayCard(unitToPlay, context);
                actionTaken = true;
                continue; 
            }

            // 2. 스킬 사용 (조건부)
            var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
            
			foreach (var skill in skills)
			{
				if (IsSkillUseful(skill, me, enemy))
				{
					// [디버깅] 사용 전 상태 스냅샷
					int prevEnemyCount = enemy.Field.Count(c => c != null);
					int prevMyMana = me.Mana;
					int prevHandCount = me.Hand.Count;

					// --- 스펠 사용 ---
					me.PlayCard(skill, context); 
					// ----------------

					// [디버깅] 사용 후 검증
					Console.WriteLine("   🔍 [검증 리포트]");
        
					// 1. 마나 변화 체크 (스펠 코스트 + 효과)
					Console.WriteLine($"      - 마나: {prevMyMana} -> {me.Mana}");

					// 2. 파이어볼 등 (적 유닛 수 변화)
					int currEnemyCount = enemy.Field.Count(c => c != null);
					if (currEnemyCount < prevEnemyCount)
						Console.WriteLine($"      - 적 처치 확인: {prevEnemyCount - currEnemyCount}마리 감소 💀");

					// 3. 드로우 카드 (패 수 변화)
					// (카드 1장 냈으니 -1, 드로우 1장 했으면 +1 => 변화 없어야 함. 마나물약 등은 -1)
					Console.WriteLine($"      - 패 장수: {prevHandCount} -> {me.Hand.Count}");

					actionTaken = true;
					break; 
				}
			}

            
        }

		// [수정 5] 배틀 페이즈 (매 턴 유닛별 1회 공격)
		ExecuteBattlePhase(me, enemy, context);

		return enemy.Health > 0 && me.Health > 0;
	}

	private static void ExecuteBattlePhase(Player me, Player enemy, GameContext context)
	{
		// 공격 가능한 유닛 검색
		var attackers = me.Field.Where(c => c != null && !c.IsExhausted).ToList();
		var enemyUnits = enemy.Field.Where(c => c != null).ToList();

		foreach (var attacker in attackers)
		{
			if (me.Mana < attacker.AttackCost) continue;

			object finalTarget = null;

			if (enemyUnits.Count > 0)
			{
				// 상대 유닛이 있으면 유닛부터 공격 (도발)
				// 내 공격력 이하인 적만 공격
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
					Console.WriteLine($"   (대기) {attacker.Name}: 이길 수 있는 적이 없습니다.");
				}
			}
			else
			{
				// 적 유닛 없으면 명치 (1데미지)
				finalTarget = enemy;
			}

			if (finalTarget != null)
			{
				_battleSystem.Attack(attacker, finalTarget, context);

				// 전황 업데이트 (적이 죽었을 수 있음)
				enemyUnits = enemy.Field.Where(c => c != null).ToList();
				if (enemy.Health <= 0) break;
			}
		}
	}

	private static void InitializeSystem()
	{
		_dataManager = new GameDataManager();
		_dataManager.LoadAllData("./Data"); // 데이터 경로 확인 필요
		_battleSystem = new BattleSystem();
	}

	// 헬퍼: 봇 플레이어 생성
	private static Player CreateBotPlayer(string name, params int[] cardIds)
	{
		Player p = new Player { Name = name, Health = 20, Mana = 0 };
		List<Card> deck = new List<Card>();

		// ID 기반으로 덱 생성 (각 3장씩 꽉 채움)
		foreach (int id in cardIds)
		{
			if (_dataManager.AllCards.TryGetValue(id.ToString(), out Card original))
			{
				for (int i = 0; i < 3; i++) deck.Add(CloneCard(original));
			}
		}
		p.SetDeck(deck);
		return p;
	}

	// 헬퍼: 카드 복제 (Deep Copy)
	private static Card CloneCard(Card org)
	{
		return new Card
		{
			Id = org.Id,
			Name = org.Name,
			Type = org.Type,
			Cost = org.Cost,
			OriginalCost = org.OriginalCost,
			Power = org.Power,
			MaxHealth = org.MaxHealth,
			Health = org.MaxHealth,
			AttackCost = org.AttackCost,
			Prize = org.Prize,
			Effects = new List<ICardEffect>(org.Effects),
			SkinResource = org.SkinResource
		};
	}

	private static void DrawCards(Player p, int count)
	{
		for (int i = 0; i < count; i++)
		{
			var c = p.ExtractCard(ZoneType.Deck, "Top");
			if (c != null) p.InsertCard(ZoneType.Hand, c);
		}
	}

	private static void PrintGameResult(Player p1, Player p2)
	{
		Console.WriteLine("\n========== [ Result ] ==========");
		Console.WriteLine($"{p1.Name}: {p1.Health} HP");
		Console.WriteLine($"{p2.Name}: {p2.Health} HP");
		if (p1.Health <= 0) Console.WriteLine($"🏆 승리: {p2.Name}");
		else if (p2.Health <= 0) Console.WriteLine($"🏆 승리: {p1.Name}");
		else Console.WriteLine("🤝 무승부 (턴 오버)");
	}

	// [신규] 스킬 사용 가치 판단 (AI Heuristics)
    private static bool IsSkillUseful(Card skill, Player me, Player enemy)
    {
        // 1. 파이어볼 (21001) / 데미지 스킬: 적 유닛이 있어야 함
        if (skill.Id == "21001" || skill.Effects.Any(e => e is ModifyStatEffect)) // 간단한 체크
        {
            // 적 필드에 유닛이 없으면 안 씀
            if (enemy.Field.All(c => c == null)) return false;
        }

        // 2. 강화물약 (21003): 내 유닛이 있어야 함
        if (skill.Id == "21003")
        {
            if (me.Field.All(c => c == null)) return false;
        }

        // 3. 학습 (21002): 덱이 있어야 함
        if (skill.Id == "21002")
        {
            if (me.Deck.Count == 0) return false;
        }

        // 마나물약(21004)은 언제나 이득이므로 통과
        return true;
    }
}