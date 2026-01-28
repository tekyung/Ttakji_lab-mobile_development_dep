using unity_Exercise;
using System;
using System.Linq;

class TCG_Card_Program
{
    private class Card
    {
        public int cardNumber;
        public String cardName;
        public int cost;
        public int damage;
    }
    private class Player
    {
        public String playerName;
        public int health;
        public int mana;
        public int ATK;
        public int DEF;
        public Card[] hand;
        public Card[] deck;
        public Card[] graveyard;
    }
    private Card MakeCards(int number, String name, int cost, int damage)
    {
        Card card = new Card();
        card.cardNumber = number;
        card.cardName = name;
        card.cost = cost;
        card.damage = damage;
        return card;
    }
    private Player MakePlayer(String name, int health, int mana, int ATK, int DEF)
    {
        Player player = new Player();
        player.playerName = name;
        player.health = health;
        player.mana = mana;
        player.ATK = ATK;
        player.DEF = DEF;
        player.hand = new Card[] { };
        player.deck = new Card[] { };
        player.graveyard = new Card[] { };
        return player;
    }
    static void Main(string[] args)
    {
        TCG_Card_Program program = new TCG_Card_Program();
        Card[] AllCards = new Card[]
        {
            program.MakeCards(1, "Fireball", 5, 5),
            program.MakeCards(2, "Ice Lance", 2, 3),
            program.MakeCards(3, "Healing Touch", 2, -4),
            program.MakeCards(4, "Lightning Bolt", 3, 4),
            program.MakeCards(5, "Earth Shock", 1, 2)
        };

        Player[] players = new Player[]
        {
            program.MakePlayer("Player1", 15, 8, 5, 5),
            program.MakePlayer("Player2", 15, 8, 5, 5)
        };

        Player player1 = program.SearchPlayerByName("Player1", players);
        Player player2 = program.SearchPlayerByName("Player2", players);

        program.AddCardToDeck(player1, new int[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, AllCards);
        program.AddCardToDeck(player2, new int[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, AllCards);
        program.shuffleDeck(player1);
        program.shuffleDeck(player2);

        program.startGame(player1, player2);
        return;
    }

    private void startGame(Player player, Player opponent)
    {
        Console.WriteLine("Game Started!");
        Random rand = new Random();
        int firstPlayer = rand.Next(0, 2); // 0 또는 1 랜덤 선택 (수정)
        Player currentPlayer = (firstPlayer == 0) ? player : opponent; // 현재 플레이어 설정
        Player currentOpponent = (firstPlayer == 0) ? opponent : player; // 상대 플레이어 설정

        for (int i = 0; i < 2; i++) // 초기 핸드 2장씩 뽑기
        {
            DrawCard(currentPlayer);
            DrawCard(currentOpponent);
        }

        while (player.health > 0 && opponent.health > 0)
        {
            DrawCard(currentPlayer);
            Console.WriteLine("\n" + currentPlayer.playerName + "'s turn:");
            currentPlayer.mana += 2; // 매 턴마다 마나 2 증가
            Console.WriteLine("체력: " + currentPlayer.health + ", 마나: " + currentPlayer.mana);
            Console.WriteLine("손패:");
            for (int i = 0; i < currentPlayer.hand.Length; i++)
            {
                Card card = currentPlayer.hand[i];
                Console.WriteLine($"[{i + 1}] {card.cardName} (코스트: {card.cost}, 데미지: {card.damage})");
            }
            Console.Write("\n사용할 카드 번호를 입력하세요 (0은 턴 종료) : ");
            string inputStr = Console.ReadLine();
            int input;
            if (!int.TryParse(inputStr, out input))
            {
                Console.WriteLine("숫자를 입력하세요.");
                continue;
            }
            if (input != 0)
            {
                if (input > 0 && input <= currentPlayer.hand.Length)
                {
                    Card playedCard = currentPlayer.hand[input - 1]; // 선택한 카드 한 장
                    Console.WriteLine(currentPlayer.playerName + " 가 " + playedCard.cardName + " 카드를 사용했습니다.");
                    if (currentPlayer.mana >= playedCard.cost)
                    {
                        currentPlayer.mana -= playedCard.cost;

                        if (playedCard.damage > 0) // 공격 카드
                        {
                            currentOpponent.health -= playedCard.damage;
                            Console.WriteLine(currentPlayer.playerName + " 가 " + playedCard.cardName + " 카드를 사용하여 " + currentOpponent.playerName + "에게 " + playedCard.damage + "의 데미지를 입혔습니다.");
                        }
                        else if (playedCard.damage < 0) // 치유 카드
                        {
                            currentPlayer.health += Math.Abs(playedCard.damage); // 가독성 개선
                            Console.WriteLine(currentPlayer.playerName + " 가 " + playedCard.cardName + " 카드를 사용하여 체력을 " + Math.Abs(playedCard.damage) + " 회복했습니다.");
                        }
                        // 선택한 카드 한 장만 손패에서 제거
                        currentPlayer.hand = currentPlayer.hand.Where((c, idx) => idx != (input - 1)).ToArray();
                        currentPlayer.graveyard = currentPlayer.graveyard.Append(playedCard).ToArray(); // 무덤에 카드 추가
                    }
                    else
                    {
                        Console.WriteLine("사용할 마나가 부족합니다."); // 마나 부족
                    }
                }
                else
                {
                    Console.WriteLine("손패에 해당 번호의 카드가 없습니다.");
                }
            }
            else
            {
                Console.WriteLine(currentPlayer.playerName + " 가 턴을 종료합니다.");
            }

            // 턴 종료 후 플레이어 교체
            Player temp = currentPlayer;
            currentPlayer = currentOpponent;
            currentOpponent = temp;
        }
        if (player.health <= 0)
        {
            Console.WriteLine(opponent.playerName + " wins!");
        }
        else
        {
            Console.WriteLine(player.playerName + " wins!");
        }
    }
    private Player SearchPlayerByName(String name, Player[] players) // 플레이어 이름으로 검색
    {
        foreach (Player player in players)
        {
            if (player.playerName == name)
            {
                return player;
            }
        }
        return null;
    }
    private Card SearchCardByNumber(int number, Card[] cards) // 카드 번호로 검색
    {
        foreach (Card card in cards)
        {
            if (card.cardNumber == number)
            {
                return card;
            }
        }
        return null;
    }
    private void AddCardToDeck(Player player, int[] cardNumbers, Card[] cards)
    {
        foreach (int number in cardNumbers)
        {
            Card cardToAdd = SearchCardByNumber(number, cards);
            if (cardToAdd != null)
            {
                player.deck = player.deck.Append(cardToAdd).ToArray();
            }
        }
    }
    private void AddCardToHand(Player player, Card card)
    {
        player.hand = player.hand.Append(card).ToArray();
    }
    private void DrawCard(Player player)
    {
        if (player.deck.Length > 0)
        {
            Card drawnCard = player.deck[0];
            AddCardToHand(player, drawnCard);
            player.deck = player.deck.Skip(1).ToArray();
        }
        else
        {
            Console.WriteLine(player.playerName + ": 덱이 비었습니다. 묘지를 섞어 덱으로 만듭니다.");
            player.deck = player.graveyard; // 무덤을 덱으로 이동
            player.graveyard = new Card[] { }; // 무덤 초기화
            shuffleDeck(player); // 덱 섞기
            DrawCard(player); // 카드 다시 뽑기
        }
    }

    private void shuffleDeck(Player player)
    {
        Random rand = new Random();
        for (int i = player.deck.Length - 1; i > 0; i--)
        {
            int j = rand.Next(0, i + 1);
            Card temp = player.deck[i];
            player.deck[i] = player.deck[j];
            player.deck[j] = temp;
        }
    }
    private void searchCardInDeck(Player player, String cardName)
    {
        foreach (Card card in player.deck)
        {
            if (card.cardName == cardName)
            {
                Console.WriteLine(player.playerName + ": 카드를 찾았습니다: " + card.cardName);
                AddCardToHand(player, card);
                return;
            }
        }
        Console.WriteLine("카드가 덱에 없습니다.");
    }
}
