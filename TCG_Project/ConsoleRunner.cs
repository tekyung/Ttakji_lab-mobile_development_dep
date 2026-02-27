using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project
{
    // 유니티 에디터 없이 VS 콘솔에서 빛의 속도로 테스트하기 위한 실행기
    public class ConsoleRunner
    {
        private static GameDataManager _dataManager;
        private static BattleSystem _battleSystem;
        private static GameContext context;
        public static int globalTurn = 1;

        public static void Run()
        {
            // 1. 이벤트 구독
            EventManager.OnLogMessage += CustomColoredConsoleLogger; // 커스텀 로거로 구독
            EventManager.OnTurnStart += (turn, name) => Console.WriteLine($"\n========== [ TURN {turn} : {name} ] ==========");
            EventManager.OnGameSet += (winner) => Console.WriteLine($"\n🎉 [GAME SET] {winner.Name} 승리!");
            EventManager.OnGameDraw += (p1, p2, turn) => Console.WriteLine($"\n🤝 [GAME DRAW] {p1.Name} vs {p2.Name} 무승부! (턴 {turn})");

            EventManager.OnLogMessage?.Invoke("=== 🚀 콘솔 고속 시뮬레이터 (페이즈 체인 구조) 시작 ===");

            // 2. 초기화, 여기서는 안전하게 작업 경로에서 파일 불러옴
            GameRules.LoadRules("./Data/CommonConfig.json");
            _dataManager = new GameDataManager();
            _dataManager.LoadAllData("./Data");
            _battleSystem = new BattleSystem();

            Player p1 = CreateBotPlayer("Bot_Red", 11001, 11002, 11003, 11007, 21001, 21002, 21003, 21004);
            Player p2 = CreateBotPlayer("Bot_Blue", 11004, 11005, 11006, 21001, 21002, 21003, 21004);

            context = new GameContext();
            context.Players.Add(p1);
            context.Players.Add(p2);

            // 초기 드로우 (StartingHands 설정값만큼 드로우)
            DrawCards(p1, GameRules.StartingHands);
            DrawCards(p2, GameRules.StartingHands);

            // 3. 고속 루프 (코루틴 대기 없음)
            while (!context.IsGameOver && globalTurn <= 20)
            {
                Player activePlayer = (globalTurn % 2 != 0) ? p1 : p2;
                Player targetPlayer = (globalTurn % 2 != 0) ? p2 : p1;

                context.ActivePlayer = activePlayer;
                context.TargetPlayer = targetPlayer;

                EventManager.OnTurnStart?.Invoke(globalTurn, activePlayer.Name);

                // 턴 진행 시작 (드로우 페이즈부터 체인 시작)
                ExecuteDrawPhase(activePlayer, targetPlayer, globalTurn);

                globalTurn++;
            }

            // ★ 루프가 끝났는데도 승자가 결정되지 않았다면 (턴 초과) 무승부 처리
            if (!context.IsGameOver && globalTurn > 20)
            {
                context.IsGameOver = true; // 게임 상태를 종료로 잠금
                EventManager.OnGameDraw?.Invoke(p1, p2, 20); // 무승부 이벤트 발동
            }
            // 구독 해지 (정리)
            EventManager.OnLogMessage -= Console.WriteLine;
        }

        #region Phase Logic (페이즈 체인)

        // 1. 드로우 페이즈 (마나 계산 -> 상태 이상 회복 -> 드로우)
        private static void ExecuteDrawPhase(Player me, Player enemy, int currentTurn)
        {
            EventManager.OnDrawPhase?.Invoke(me.Name, currentTurn);
            if (context.IsGameOver) return;
            EventManager.OnLogMessage?.Invoke($"[ 🃏 드로우 페이즈 ]");

            // 마나 룰 적용
            int setMana = GameRules.BasicEnergy;
            if (currentTurn == 1) setMana = GameRules.FirstPlayerFirstTurnEnergy;
            else if (currentTurn == 2) setMana = GameRules.SecondPlayerFirstTurnEnergy;

            if (me.Mana > GameRules.BasicEnergy) setMana = me.Mana;
            me.Mana = setMana;

            // 유닛 상태 및 디버프 회복 (Player.cs 내부 로직 / 아직 미적용)
            me.OnTurnStart();

            // 턴 드로우
            DrawCards(me, GameRules.DrawPerTurn);
            EventManager.OnLogMessage?.Invoke($"{me.Name} 의 패: {me.Hand.Count}장 / {string.Join(", ", me.Hand.Select(c => c.Name))}");

            // 다음 페이즈 호출
            ExecuteMainPhase(me, enemy, currentTurn);
        }

        // 2. 메인 페이즈 (유닛 소환, 스펠 사용 루프)
        private static void ExecuteMainPhase(Player me, Player enemy, int currentTurn)
        {
            EventManager.OnMainPhase?.Invoke(me.Name, currentTurn);
            if (context.IsGameOver) return;
            EventManager.OnLogMessage?.Invoke($"[ ⚙️ 메인 페이즈 ]");

            bool actionTaken = true;
            int safetyCount = 0; // 무한 루프 방지

            while (actionTaken && safetyCount < 20)
            {
                safetyCount++;
                actionTaken = false;
                if (context.IsGameOver) return;

                var handClone = new List<Card>(me.Hand);

                // --- 행동 1: 유닛 소환 ---
                var unitToPlay = handClone.Where(c => c.Type == CardType.Unit && c.IsPlayable(context)).OrderByDescending(c => c.Power).FirstOrDefault();
                if (unitToPlay != null)
                {
                    // ★ PlayCard의 3번째 파라미터로 콜백(Action)을 넘겨줍니다.
                    // 봇 로직이므로 PlayCard가 호출되자마자 즉시 이 안쪽 로직이 실행됩니다.
                    me.PlayCard(unitToPlay, context, () =>
                    {
                        actionTaken = true; // 카드를 정상적으로 냈음을 루프에 알림
                    });

                    continue; // 필드 상황이 변했으므로 처음부터 재평가
                }

                // AI 마나 보존 전략: 필드 유닛이 적으면 마나를 아낌
                int unitCount = me.Field.Count(c => c != null);
                int manaThreshold = (unitCount <= 1) ? 1 : 2;
                if (me.Mana <= manaThreshold) break; // 스펠 안 쓰고 배틀로 넘김

                // --- 행동 2: 스펠/스킬 사용 ---
                var skills = handClone.Where(c => c.Type == CardType.Skill && c.IsPlayable(context)).ToList();
                foreach (var skill in skills)
                {
                    if (IsSkillUseful(skill, me, enemy))
                    {
                        // ★ 스킬도 마찬가지로 콜백을 넘겨줍니다.
                        me.PlayCard(skill, context, () =>
                        {
                            actionTaken = true;
                        });
                        break; // 스펠 발동 후 필드 재평가
                    }
                }
            }

            if (context.IsGameOver) return;

            // --- 1. 메인 페이즈 종료 시점의 상태 로깅 ---
            int aliveUnitsCount = me.Field.Count(c => c != null);
            EventManager.OnLogMessage?.Invoke($"\n[ ⚙️ 메인 페이즈 종료 ]");
            EventManager.OnLogMessage?.Invoke($"--- 잔여 마나: {me.Mana} | 필드 유닛: {aliveUnitsCount}/{GameRules.MaxFieldUnitCount} ---");
            // 3칸 고정 배열의 특성을 살려, null일 경우 "[빈칸]"이라는 명시적인 텍스트로 치환하여 출력합니다.
            EventManager.OnLogMessage?.Invoke(
                $"{me.Name}의 필드 : {string.Join(" / ", me.Field.Select(c => c != null ? c.Name : "[빈칸]"))}"
            );

            // --- 2. 배틀 페이즈 진입 조건 정밀 검사 ---
            if (aliveUnitsCount == 0)
            {
                EventManager.OnLogMessage?.Invoke($"{me.Name} : 필드에 유닛이 없어 배틀 페이즈를 건너뜁니다.");
                ExecuteEndPhase(me, enemy, currentTurn);
                return;
            }

            // 필드에 유닛은 있으나, 행동 가능한(피로 상태가 아닌) 유닛 추출
            var readyUnits = me.Field.Where(c => c != null && !c.IsExhausted).ToList();

            if (readyUnits.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke($"{me.Name} : 공격 가능한 유닛이 없어 배틀 페이즈를 건너뜁니다.");
                ExecuteEndPhase(me, enemy, currentTurn);
                return;
            }

            // 행동 가능한 유닛은 있으나, 마나가 충분한지 검사
            // (가장 저렴한 공격 코스트를 가진 유닛 기준)
            int minAttackCost = readyUnits.Min(c => c.AttackCost);
            if (me.Mana < minAttackCost)
            {
                EventManager.OnLogMessage?.Invoke($"{me.Name} : 마나가 부족해 배틀 페이즈를 건너뜁니다. (최소 필요: {minAttackCost}, 보유 마나: {me.Mana})");
                ExecuteEndPhase(me, enemy, currentTurn);
                return;
            }

            // 모든 조건을 통과했다면 배틀 페이즈 진입
            ExecuteBattlePhase(me, enemy, currentTurn);
        }

        // 3. 배틀 페이즈 (전투 실행)
        private static void ExecuteBattlePhase(Player me, Player enemy, int currentTurn)
        {
            EventManager.OnBattlePhase?.Invoke(me.Name, currentTurn);
            if (context.IsGameOver) return;
            EventManager.OnLogMessage?.Invoke($"[ ⚔️ 배틀 페이즈 ]");

            int loopSafety = 0;

            while (loopSafety < 10)
            {
                loopSafety++;
                bool attackOccurred = false;

                // 공격 가능 유닛 탐색
                var attackers = me.Field.Where(c => c != null && !c.IsExhausted).OrderByDescending(c => c.Power).ToList();
                if (attackers.Count == 0) break;

                var enemyUnits = enemy.Field.Where(c => c != null).ToList();

                foreach (var attacker in attackers)
                {
                    if (context.IsGameOver) return;
                    if (me.Mana < attacker.AttackCost) continue;

                    // 타겟팅 전략: 이길 수 있는 적 중 가장 강한 놈
                    object finalTarget = null;

                    // 1. 적 필드에 유닛이 존재하는가?
                    if (enemyUnits.Count > 0)
                    {
                        // 이길 수 있는 적 중 가장 강한 놈을 찾는다.
                        finalTarget = enemyUnits.Where(e => e.Power <= attacker.Power).OrderByDescending(e => e.Power).FirstOrDefault();

                        // 만약 이길 수 있는 적이 없다면? 
                        if (finalTarget == null)
                        {
                            // 공격을 포기한다.
                            continue;
                        }
                    }
                    // 2. 적 필드가 완전히 비어있을 때만 본체를 타격한다.
                    else
                    {
                        finalTarget = enemy;
                    }

                    _battleSystem.Attack(attacker, finalTarget, context);
                    attackOccurred = true;

                    // 한 번 공격이 발생하면 적 유닛이 죽었을 수 있으므로 다시 타겟팅 (break 후 while 재진입)
                    break;
                }

                if (!attackOccurred) break;
            }

            ExecuteEndPhase(me, enemy, currentTurn);
        }

        // 4. 엔드 페이즈 (턴 종료 처리)
        private static void ExecuteEndPhase(Player me, Player enemy, int currentTurn)
        {
            EventManager.OnEndPhase?.Invoke(me.Name, currentTurn);
            if (context.IsGameOver) return;
            EventManager.OnLogMessage?.Invoke($"[ 🛑 엔드 페이즈 ]\n");

            // TODO: 향후 '턴 종료 시 발동'하는 디버프 데미지나 버프 해제 로직을 여기에 추가합니다.
        }

        #endregion

        #region Helpers

        private static Player CreateBotPlayer(string name, params int[] ids)
        {
            Player p = new Player { Name = name, PrizePoints = 0, Mana = 0 };
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
            {   // 덱에 카드가 부족할 때 남은 카드 다 드로우
                int drawnCount = p.Deck.Count;

                while (p.Deck.Count > 0)
                {
                    var c = p.ExtractCard(ZoneType.Deck, "Top");
                    if (c != null) p.InsertCard(ZoneType.Hand, c);
                }
                EventManager.OnLogMessage?.Invoke($"{p.Name} 드로우 시도: {count}장 중 {drawnCount}장 성공 (덱 고갈)");
            }
        }

        private static bool IsSkillUseful(Card skill, Player me, Player enemy)
        {
            // 의미 없는 스펠 낭비를 막는 하드코딩된 AI 방어 로직
            if (skill.Id == "21001") { return enemy.Field.Any(c => c != null && c.Power <= 300); } // 적 필드에 파워 300 이하 유닛이 없는데 파이어 볼 사용 방지
            if (skill.Id == "21003") { if (me.Field.All(c => c == null)) return false; } // 내 유닛이 없는데 버프 스펠 사용 방지
            if (skill.Id == "21002") { if (me.Deck.Count == 0) return false; } // 덱이 없는데 드로우 방지
            if (skill.Id == "21004") { if (me.Hand.Count <= 1) return false; } // 패가 1장도 없는데 마나 회복 방지
            return true;
        }

        // --- 새로 추가할 커스텀 로거 메서드 ---
        /// <summary>
        /// Rich Text 태그(<color=...>)를 감지하여 VS 콘솔의 색상으로 변환해 출력합니다.
        /// </summary>
        private static void CustomColoredConsoleLogger(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                Console.WriteLine();
                return;
            }

            // 1. Cyan (스펠)
            if (message.StartsWith("<color=cyan>"))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(message.Replace("<color=cyan>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            // 2. Yellow (이펙트)
            else if (message.StartsWith("<color=yellow>"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message.Replace("<color=yellow>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            // 3. Red (경고)
            else if (message.StartsWith("<color=red>"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message.Replace("<color=red>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            // 태그가 없는 일반 메시지
            else
            {
                Console.WriteLine(message);
            }
        }
        #endregion
    }
}