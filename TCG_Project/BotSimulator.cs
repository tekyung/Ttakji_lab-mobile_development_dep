using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

namespace TCG_Project
{
    class Program
    {

        // 전역 변수 : 룰에 관련된 상수는 GameRules로 이동됨
        public static int turnCount = 1;

        static void Main(string[] args)
        {
            // 콘솔에서 UTF-8 인코딩 사용 설정
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.WriteLine("=== TCG 콘솔 시뮬레이터 시작 ===\n");

            // 0. 룰 데이터 로드 (가장 먼저 실행)
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
            string cardsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Cards.json");
            List<Card> cardDatabase = CardFactory.LoadCardsFromFile(cardsPath);

            if (cardDatabase.Count == 0)
            {
                Console.WriteLine("오류: 카드 데이터를 불러올 수 없습니다. Cards.json을 확인하세요.");
                return;
            }

            // 2. 플레이어 초기화 (GameRules 값 사용)
            // 시작 마나와 체력을 룰 파일에서 가져옴
            Player p1 = new Player
            {
                Name = "Player 1",
                Health = GameRules.StartingHealth,
                Mana = GameRules.StartingMana
            };

            Player p2 = new Player
            {
                Name = "Player 2",
                Health = GameRules.StartingHealth,
                Mana = GameRules.StartingMana
            };

            int maxSameCard = 3;

            // 3. 덱 생성 (Deep Copy 적용)
            // 주의: P1과 P2는 서로 다른 덱 인스턴스를 가져야 하므로 BuildDeck을 각각 호출
            p1.SetDeck(BuildDeck(cardDatabase, 20, maxSameCard));
            p2.SetDeck(BuildDeck(cardDatabase, 20, maxSameCard));

            // 4. 게임 시작 준비 (초기 핸드 드로우)
            int startDraw = GameRules.StartingDrawCount;
            Console.WriteLine($"--- 게임 준비: 초기 핸드 {startDraw}장 드로우 ---");
            p1.DrawCard(startDraw); 
            p2.DrawCard(startDraw);
            Console.WriteLine("--------------------------------------\n");

            // 5. 게임 루프 변수 설정
            int currentMaxMana = GameRules.StartingMana; // 룰에 정해진 시작 마나로 초기화

            while (true)
            {
                Console.WriteLine($"\n========== [ TURN {turnCount} ] 최대 마나: {currentMaxMana} ==========");

                // --- Player 1 턴 ---
                if (!ProcessTurn(p1, p2, currentMaxMana)) break;

                // 승패 체크
                if (CheckGameOver(p1, p2)) break;

                Console.WriteLine("--------------------------------------");

                // --- Player 2 턴 ---
                if (!ProcessTurn(p2, p1, currentMaxMana)) break;

                // 승패 체크
                if (CheckGameOver(p1, p2)) break;

                // 턴 종료 처리
                // 룰에 따라 최대 마나 증가
                if (currentMaxMana < GameRules.MaxMana)
                {
                    currentMaxMana += GameRules.ManaGainPerTurn;
                    // 최대치 보정
                    if (currentMaxMana > GameRules.MaxMana) currentMaxMana = GameRules.MaxMana;
                }

                turnCount++;

                // 엔터키로 진행
                Console.ReadLine();
            }

            Console.WriteLine("\n프로그램이 종료되었습니다. 아무 키나 눌러 종료하세요...");
            Console.ReadKey(true);
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

        // 한 플레이어의 턴을 진행하는 로직
        public static bool ProcessTurn(Player activePlayer, Player opponent, int currentTurnMaxMana)
        {
            // 1. 마나 충전 (현재 턴의 최대 마나로 리필)
            // * 룰 변경: 보통 TCG는 턴 시작 시 마나가 '회복'되므로 할당(=)이 일반적입니다.
            // * 기존 로직(+=)을 원하시면 activePlayer.Mana += GameRules.ManaGainPerTurn; 으로 변경 가능
            if (turnCount != 1) activePlayer.Mana += GameRules.ManaGainPerTurn;

            Console.WriteLine($"\n--- [{activePlayer.Name}] 의 턴 / 체력 : {activePlayer.Health} / 마나 : {activePlayer.Mana} ---");

            // 2. 드로우 페이즈 (룰에 따른 장수만큼 드로우)
            for (int i = 0; i < GameRules.DrawPerTurn; i++)
            {
                if (activePlayer.Hand.Count >= GameRules.MaxHandSize)
                {
                    Console.WriteLine("패가 가득 차서 드로우할 수 없습니다 (Burn).");
                    // 덱에서는 한 장 태워야 하는 룰이라면 DrawCard() 호출 후 핸드에 안 넣는 로직 필요
                    // 여기서는 단순 스킵
                    break;
                }

                bool canDraw = activePlayer.DrawCard(1);
                if (!canDraw)
                {
                    Console.WriteLine($"[{activePlayer.Name}] 의 덱이 바닥났습니다. 패배합니다.");
                    activePlayer.Health = 0;
                    return false;
                }
            }

            // 판별을 위해 임시 Context 생성
            GameContext checkContext = new GameContext
            {
                Players = new List<Player> { activePlayer, opponent },
                ActivePlayer = activePlayer
            };

            activePlayer.UpdatePlayableCards(checkContext);

            Console.WriteLine($"[{activePlayer.Name}] 의 패: {string.Join(", ", activePlayer.Hand.Select(c => c.Name))}");

            // 3. 메인 페이즈
            Card cardToPlay = activePlayer.EnableCardList.FirstOrDefault();

            if (cardToPlay != null)
            {
                // 실제 사용 Context
                GameContext playContext = new GameContext
                {
                    Players = new List<Player> { activePlayer, opponent },
                    ActivePlayer = activePlayer,
                    TargetPlayer = opponent
                };

                activePlayer.PlayCard(cardToPlay, playContext);
            }
            else
            {
                Console.WriteLine($"[{activePlayer.Name}] 사용할 수 있는 카드가 없어 턴을 넘깁니다.");
            }

            return true;
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
    }
}