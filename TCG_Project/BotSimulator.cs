using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Effects;
using System.ComponentModel;

namespace TCG_Project
{
    class Program
    {
        // 전역 변수 : 룰에 관련된 상수는 GameRules로 이동됨
        // public static int turnCount = 1;

        static void Main(string[] args)
        {   
            // 콘솔에서 UTF-8 인코딩 사용 설정
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            ConsoleRunner.Run();

            Console.WriteLine("\n엔터 키를 누르면 종료합니다.");
            Console.ReadLine();
            
            /* 0. 룰 데이터 로드 (가장 먼저 실행)
            try
            {
                string rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Rules.json");
                GameRules.LoadRules(rulesPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] 룰 파일을 불러오는데 실패했습니다: {e.Message}");
                return;
            }
            */

            //BotSimulator2.Run();
            //RunBot1();
        }
        /*
         * BotSimulator1 : 기존 방식 (룰이 코드에 하드코딩되어 있고, 드로우/턴 진행 로직이 분산되어 있음)
         * BotSimulator2 : 개선된 방식 (룰을 외부 JSON으로 분리, 드로우/턴 진행 로직 통합, Context 활용 강화)
         * 
         * 현재는 BotSimulator2가 완성된 상태이며, BotSimulator1은 참고용으로 남겨둔 상태입니다.
         * 필요에 따라 BotSimulator1의 코드를 BotSimulator2로 점진적으로 리팩토링하는 것도 가능합니다.
         
        public static void RunBot1()
        {
            
            TestLoader.RunTest(); // 테스트
            // 테스트가 끝나면 콘솔이 바로 꺼지지 않게 입력 대기
            Console.WriteLine("\n엔터 키를 누르면 종료합니다...");

            Console.WriteLine("=== TCG 콘솔 시뮬레이터 시작 ===\n");

            

            // 0.5 효과 데이터 로드 (신규 추가)
            try
            {
                string effectsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Effects.json");
                EffectFactory.LoadEffects(effectsPath);
            }
            catch (Exception e)
            {                 Console.WriteLine($"[Error] 효과 파일을 불러오는데 실패했습니다: {e.Message}");
                return;
            }

            // 1. 카드 데이터 로드
            string cardsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SpellCards.json");
            List<Card> cardDatabase = CardFactory.LoadCardsFromFile(cardsPath);
            
            //List<Card> cardDatabase = CardFactoryver2.LoadAllData(cardsPath);

            if (cardDatabase.Count == 0)
            {
                Console.WriteLine("오류: 카드 데이터를 불러올 수 없습니다. Cards.json을 확인하세요.");
                return;
            }

            // 2. 플레이어 초기화 (GameRules 값 사용)
            // 시작 마나와 체력을 룰 파일에서 가져옴
            Player p1 = new Player
            {
                Name = "Player 1 Bot",
                Health = GameRules.StartingHealth,
                Mana = GameRules.StartingMana
            };

            Player p2 = new Player
            {
                Name = "Player 2 Bot",
                Health = GameRules.StartingHealth,
                Mana = GameRules.StartingMana
            };

            // 2. [핵심] 게임 전체를 관통하는 Context 생성!
            GameContext globalContext = new GameContext();
            globalContext.Players.Add(p1);
            globalContext.Players.Add(p2);

            // 3. 덱 생성 (Deep Copy 적용)
            // 주의: P1과 P2는 서로 다른 덱 인스턴스를 가져야 하므로 BuildDeck을 각각 호출
            p1.SetDeck(BuildDeck(cardDatabase, 20, GameRules.MaxSameCardInDeck));
            p2.SetDeck(BuildDeck(cardDatabase, 20, GameRules.MaxSameCardInDeck));

            // 4. 게임 시작 준비 (초기 핸드 드로우)
            int startDraw = GameRules.StartingDrawCount;
            Console.WriteLine($"--- 게임 준비: 초기 핸드 {startDraw}장 드로우 ---");

            SetDebugHand(p1, "급성장", "화염구", "기적의 드로우");
            SetDebugHand(p2, "잠시만 빌릴게", "축제", "마나 재활용");
            
            // 첫 패 드로우 (MoveCardEffect 사용)
            // 턴 진행 중이 아니므로, 각 플레이어별로 Context를 임시로 만들어 실행합니다.
            var startingDraw = new MoveCardEffect();
            startingDraw.Initialize(new Dictionary<string, object>
            {
                { "src", "Deck" },
                { "dest", "Hand" },
                { "count", startDraw - 1 }, // 초기 핸드 2장 (룰에 따라 변경 가능)
                { "srcTarget", "ActivePlayer" }, // Context에서 주입된 플레이어를 대상으로 함
                { "destTarget", "ActivePlayer" }
            });

            // Player 1 드로우
            startingDraw.Execute(new GameContext { ActivePlayer = p1, TargetPlayer = p2 });

            // Player 2 드로우
            startingDraw.Execute(new GameContext { ActivePlayer = p2, TargetPlayer = p1 });
            

            Console.WriteLine("--------------------------------------\n");

            // 5. 게임 루프 변수 설정
            int currentMaxMana = GameRules.StartingMana; // 룰에 정해진 시작 마나로 초기화

            while (true)
            {
                // --- Player 1 턴 ---
                globalContext.ActivePlayer = p1;
                globalContext.TargetPlayer = p2;
                if (!ProcessTurn(p1, p2, GameRules.StartingMana, globalContext)) break;
                
                // 승패 체크
                if (CheckGameOver(p1, p2)) break;

                Console.WriteLine("-----------------------------------------------------\n");

                // --- Player 2 턴 ---
                globalContext.ActivePlayer = p2;
                globalContext.TargetPlayer = p1;
                if (!ProcessTurn(p2, p1, GameRules.StartingMana, globalContext)) break;
                
                // 승패 체크
                if (CheckGameOver(p1, p2)) break;

                // 턴 종료 처리
                // 룰에 따라 최대 마나 증가 (당장은 보류로 활용 안 됨)
                if (currentMaxMana < GameRules.MaxMana)
                {
                    currentMaxMana += GameRules.ManaGainPerTurn;
                    // 최대치 보정
                    if (currentMaxMana > GameRules.MaxMana) currentMaxMana = GameRules.MaxMana;
                }

                // turnCount++;

                // 엔터키로 진행
                Console.ReadLine();
            }

            Console.WriteLine("\n프로그램이 종료되었습니다. 아무 키나 눌러 종료하세요...");
            Console.ReadKey(true);
        }

        // 한 플레이어의 턴을 진행하는 로직
        public static bool ProcessTurn(Player activePlayer, Player opponent, int currentTurnMaxMana, GameContext context)
        {
            Console.WriteLine($"\n========== [ TURN {turnCount} ] 최대 마나: {GameRules.StartingMana} ==========");

            // 0. [턴 시작] 예약된 효과 처리 (예: "다음 턴 시작 시까지" 였던 효과들 만료)
            //ProcessPendingEffects(GamePhase.TurnStart, activePlayer, context);

            // 1. 마나 충전 (현재 턴의 최대 마나로 리필)
            // 보통 TCG는 턴 시작 시 마나가 '회복'되므로 할당(=)이 일반적입니다.
            if (turnCount != 1) activePlayer.Mana = GameRules.StartingMana;

            Console.WriteLine($"\n--- [{activePlayer.Name}] 의 턴 / 체력 : {activePlayer.Health} / 마나 : {activePlayer.Mana} ---");

            // 2. [통합된 드로우 로직]
            // 룰 드로우도 'MoveCardEffect'를 사용합니다.
            Console.WriteLine($"\n--- {activePlayer.Name} 드로우 페이즈 ---");

            if (activePlayer.Deck.Count == 0)
            {
                Console.WriteLine($"{activePlayer.Name} 이 드로우하지 못해 패배합니다.");
                return false;
            }

            var turnDrawEffect = new MoveCardEffect();
            turnDrawEffect.Initialize(new Dictionary<string, object>
            {
                { "src", "Deck" },
                { "dest", "Hand" },
                { "count", GameRules.DrawPerTurn }, // 룰에서 정한 장수만큼
                { "srcTarget", "ActivePlayer" },
                { "destTarget", "ActivePlayer" }
            });

            // 시스템이 발동하므로 Context만 넘김
            turnDrawEffect.Execute(new GameContext
            {
                ActivePlayer = activePlayer,
                TargetPlayer = activePlayer
            });

            // 판별을 위해 임시 Context 생성
            GameContext checkContext = new GameContext
            {
                Players = new List<Player> { activePlayer, opponent },
                ActivePlayer = activePlayer
            };

            activePlayer.UpdatePlayableCards(checkContext);

            Console.WriteLine($"[{activePlayer.Name}] 의 패: {string.Join(", ", activePlayer.Hand.Select(c => c.Name))}");

            // 3. 메인 페이즈

            int playCount = 0;

            // [조건 변경] 마나가 있고 && 낼 카드가 있고 && 3번 미만으로 행동했으면 반복
            while (activePlayer.Mana > 0 && playCount < GameRules.MaxPlaysPerTurn)
            {
                // 1. 낼 수 있는 카드 목록 갱신
                activePlayer.UpdatePlayableCards(checkContext);
                if (activePlayer.EnableCardList.Count == 0) break; // 낼 카드가 없으면 턴 종료

                Card cardToPlay = activePlayer.EnableCardList.FirstOrDefault();

                // 3. 카드 발동
                activePlayer.PlayCard(cardToPlay, new GameContext
                {
                    Players = new List<Player> { activePlayer, opponent },
                    ActivePlayer = activePlayer,
                    TargetPlayer = opponent
                });

                // 4. 행동 횟수 증가
                playCount++;

                // (선택 사항: 너무 빨리 지나가면 보기 힘드니 딜레이)
                System.Threading.Thread.Sleep(500);
            }

            Console.WriteLine($"\n--- {activePlayer.Name} 턴 종료 (사용 카드: {playCount}장 / LP: {activePlayer.Health} / 패: {activePlayer.Hand.Count} / 덱: {activePlayer.Deck.Count}장 / 묘지: {activePlayer.Graveyard.Count}장) ---");
            ProcessPendingEffects(GamePhase.TurnEnd, activePlayer, context);
            turnCount++;

            return true;
        }

        public static void ProcessPendingEffects(GamePhase phase, Player currentTurnPlayer, GameContext context)
        {
            // [수정] 필터링 로직 단순화 및 디버깅
            // OwnerPlayer가 null이면 공용 효과로 취급, 아니면 현재 턴 플레이어와 일치해야 함
            var effectsToRun = context.PendingEffects
                .Where(e => e.TriggerPhase == phase)
                .Where(e => e.OwnerPlayer == null || e.OwnerPlayer == currentTurnPlayer)
                .ToList();

            // (디버깅용: 만약 예약된 건 있는데 실행이 안 되는지 확인)
            if (context.PendingEffects.Count > 0 && effectsToRun.Count == 0)
                 Console.WriteLine($"   (Debug: 대기 중인 효과 {context.PendingEffects.Count}개 중 조건 만족 0개)");

            foreach (var pe in effectsToRun)
            {
                // 효과 실행
                pe.Effect.Execute(context);

                // 리스트에서 제거
                context.PendingEffects.Remove(pe);
            }
        }
        /*
        // 카드 트리거 효과 체크
        public static void ProcessPendingEffects(GamePhase phase, Player currentTurnPlayer, GameContext context)
        {
            // 리스트를 순회하며 조건에 맞는 효과 실행
            // (실행 중 리스트가 변경될 수 있으므로 역순이나 복사본 사용 권장)
            var effectsToRun = context.PendingEffects
                .Where(e => e.TriggerPhase == phase && (e.OwnerPlayer == null || e.OwnerPlayer == currentTurnPlayer))
                .ToList();

            if (effectsToRun.Count > 0)
            {
                Console.WriteLine($"\n⏰ [효과 만료] {phase} 단계의 임시 효과들이 해제됩니다.");
            }

            foreach (var pe in effectsToRun)
            {
                Console.WriteLine($"⏰ [만료] 예약된 효과가 발동합니다.");
                pe.Effect.Execute(pe.Context); // 저장해둔 컨텍스트로 실행

                // 실행 후 제거 (일회성)
                context.PendingEffects.Remove(pe);
            }
        }
        
        // 덱 생성 헬퍼 함수
        public static List<Card> BuildDeck(List<Card> database, int deckSize, int MaxSameCards)
        {
            List<Card> newDeck = new List<Card>();
            List<string> cardCountTracker = new List<string>();
            Random rng = new Random();

            for (int i = 0; i < deckSize; i++)
            {
                // DB에서 랜덤 카드 선택
                Card randomCard = database[rng.Next(database.Count)];

                // 중복 체크
                int sameCardCount = cardCountTracker.Count(c => c == randomCard.Name);
                if (sameCardCount >= MaxSameCards) // 등호 조건 수정 (>=)
                {
                    i--;
                    continue;
                }

                cardCountTracker.Add(randomCard.Name);

                // [핵심 변경] 원본 참조가 아닌, 복제본(Clone)을 덱에 추가
                newDeck.Add(randomCard.Clone());
            }
            return newDeck;
        }

        // [디버그용] 특정 플레이어의 패를 원하는 카드로 세팅하는 함수
        public static void SetDebugHand(Player player, params string[] cardNames)
        {
            Console.WriteLine($"\n🕵️‍♂️ [Debug] {player.Name}의 패를 조작합니다...");

            // 기존에 혹시 들어간 카드가 있다면 덱으로 돌려보내거나 초기화 (선택 사항)
            // player.Hand.Clear(); // 필요하면 주석 해제

            foreach (string name in cardNames)
            {
                // 1. 덱에서 해당 이름의 카드를 찾음 (첫 번째 발견된 것)
                Card targetCard = player.Deck.FirstOrDefault(c => c.Name == name);

                if (targetCard != null)
                {
                    // 2. 덱에서 빼고 패로 이동
                    player.Deck.Remove(targetCard);
                    player.Hand.Add(targetCard);
                    Console.WriteLine($"   -> '{name}' 추가됨.");
                }
                else
                {
                    Console.WriteLine($"   [Warning] 덱에 '{name}' 카드가 없습니다! (철자 확인 필요)");
                }
            }
            Console.WriteLine($"   (현재 패: {player.Hand.Count}장)");
        }

        public static bool CheckGameOver(Player p1, Player p2)
        {
            if (p1.Health <= 0 && p2.Health <= 0)
            {
                Console.WriteLine("\n========== [ 무승부! ] ==========");
                return true;
            }
            else if (p1.Health <= 0)
            {
                Console.WriteLine($"\n========== [ {p2.Name} 승리! ] ==========");
                return true;
            }
            else if (p2.Health <= 0)
            {
                Console.WriteLine($"\n========== [ {p1.Name} 승리! ] ==========");
                return true;
            }
            return false;
        }
        */
    }
}