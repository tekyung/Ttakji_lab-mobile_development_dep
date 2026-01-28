using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

// 아래 네임스페이스들이 정확해야 에러가 사라집니다.
namespace TCG_Project
{
    class Program
    {
        static void Main(string[] args)
        {
            // Main 시작부에 추가 : 콘솔에서 UTF-8 인코딩 사용 설정
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.WriteLine("=== TCG 콘솔 시뮬레이터 시작 ===\n");

            // 1. 데이터 로드 (카드 DB 불러오기)
            string jsonPath = Path.Combine("Data", "Cards.json");
            List<Card> cardDatabase = CardFactory.LoadCardsFromFile(jsonPath);

            if (cardDatabase.Count == 0)
            {
                Console.WriteLine("오류: 카드 데이터를 불러올 수 없습니다. Cards.json을 확인하세요.");
                return;
            }

            // 2. 플레이어 초기화
            Player p1 = new Player { Name = "Player 1", Health = 20 };
            Player p2 = new Player { Name = "Player 2", Health = 20 };

            // 3. 덱 생성 (같은 덱 구성: DB에 있는 카드를 복제해서 20장 채움)
            p1.SetDeck(BuildDeck(cardDatabase, 20));
            p2.SetDeck(BuildDeck(cardDatabase, 20));

            // 4. 게임 시작 준비 (첫 패 4장 드로우)
            Console.WriteLine("--- 게임 준비: 초기 핸드 2장 드로우 ---");
            for (int i = 0; i < 2; i++) { p1.DrawCard(); p2.DrawCard(); }
            Console.WriteLine("--------------------------------------\n");

            // 5. 게임 루프 시작
            int turnCount = 1;
            int currentMaxMana = 7; // 시작 코스트 7

            while (true)
            {   
                Console.WriteLine($"\n========== [ TURN {turnCount} ] 최대 마나: {currentMaxMana} ==========");

                // --- Player 1 턴 ---
                if (!ProcessTurn(p1, p2, currentMaxMana)) break; // 게임 종료 조건 발생 시 루프 탈출

                // 승패 체크 (P1 공격 후 상황)
                if (CheckGameOver(p1, p2)) break;

                currentMaxMana += 2; // 매 턴 코스트 2씩 증가
                turnCount++;

                Console.WriteLine($"\n========== [ TURN {turnCount} ] MaxMana: {currentMaxMana} ==========");
                // --- Player 2 턴 ---
                if (!ProcessTurn(p2, p1, currentMaxMana)) break;

                // 승패 체크 (P2 공격 후 상황)
                if (CheckGameOver(p1, p2)) break;

                // 턴 종료 처리
                currentMaxMana += 2; // 매 턴 코스트 2씩 증가
                turnCount++;

                // 엔터키로 진행 (너무 빨리 지나가는 것 방지, 원치 않으면 주석 처리)
                // Console.ReadLine(); 
            }
        }

        // 덱 생성 헬퍼 함수
        public static List<Card> BuildDeck(List<Card> database, int deckSize)
        {
            List<Card> newDeck = new List<Card>();
            Random rng = new Random();

            for (int i = 0; i < deckSize; i++)
            {
                // DB에서 랜덤하게 카드를 뽑아 복제본을 넣음 (데이터 오염 방지를 위해 복제 필요하지만 여기선 간략히 참조만 사용)
                // *주의: 실제 게임에선 Card.Clone()을 구현해서 깊은 복사를 해야 각 카드의 상태가 꼬이지 않음.
                // 현재 구조는 카드가 상태(변수)를 가지지 않고 데이터만 가지므로 참조로도 괜찮음.
                Card randomCard = database[rng.Next(database.Count)];

                // 리스트에 넣을 때는 같은 객체라도 별개로 취급되지만, 
                // 만약 Card 내부에 '강화 수치' 같은 게 생긴다면 반드시 Clone() 해야 함.
                newDeck.Add(randomCard);
            }
            return newDeck;
        }

        // 한 플레이어의 턴을 진행하는 로직
        public static bool ProcessTurn(Player activePlayer, Player opponent, int mana)
        {
            // 1. 마나 충전
            activePlayer.Mana = mana;
            Console.WriteLine($"\n--- [{activePlayer.Name}] 의 턴 / 체력 : {activePlayer.Health} / 마나 : {activePlayer.Mana} ---");
            
            // 2. 드로우 페이즈
            bool canDraw = activePlayer.DrawCard();
            if (!canDraw)
            {
                Console.WriteLine($"[{activePlayer.Name}] 의 덱이 바닥났습니다. 패배합니다.");
                activePlayer.Health = 0; // 패배 처리
                return false; // 게임 종료 신호
            }

            Console.WriteLine($"\n[{activePlayer.Name}] 의 패: {string.Join(", ", activePlayer.Hand.Select(c => c.Name))}");

            // 3. 메인 페이즈 (카드 1장 사용)
            // 간단한 AI: 현재 마나로 쓸 수 있는 카드 중 첫 번째를 사용
            // (실제 게임이면 여기서 입력을 받거나 복잡한 AI가 들어감)
            Card cardToPlay = activePlayer.Hand.FirstOrDefault(c => c.Cost <= activePlayer.Mana);

            if (cardToPlay != null)
            {
                // GameContext 생성 (현재 상황 전달)
                GameContext context = new GameContext
                {
                    Player = activePlayer,
                    Opponent = opponent
                };

                activePlayer.PlayCard(cardToPlay, context);
            }
            else
            {
                Console.WriteLine($"[{activePlayer.Name}] 마나가 부족하거나 낼 카드가 없어 턴을 넘깁니다.");
            }

            return true; // 게임 계속 진행
        }

        // 승패 조건 체크 (무승부 포함)
        public static bool CheckGameOver(Player p1, Player p2)
        {
            if (p1.Health <= 0 && p2.Health <= 0)
            {
                Console.WriteLine("\n========== [ 무승부! ] ==========");
                Console.WriteLine("두 플레이어의 체력이 동시에 0이 되었습니다.");
                return true;
            }
            else if (p1.Health <= 0)
            {
                Console.WriteLine($"\n========== [ {p2.Name} 승리! ] ==========");
                Console.WriteLine($"{p1.Name}의 체력이 0이 되었습니다.");
                return true;
            }
            else if (p2.Health <= 0)
            {
                Console.WriteLine($"\n========== [ {p1.Name} 승리! ] ==========");
                Console.WriteLine($"{p2.Name}의 체력이 0이 되었습니다.");
                return true;
            }

            return false; // 아직 안 끝남
        }
    }
}