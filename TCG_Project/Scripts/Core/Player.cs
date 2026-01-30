using System;
using System.Collections.Generic;
using System.Linq;

namespace TCG_Project.Scripts.Core
{
    public class Player
    {
        public string Name { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }

        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> Graveyard { get; private set; } = new List<Card>();

        // [신규] 효과 처리 중인 카드를 잠시 보관하는 장소 (스택)
        public Card PlayingCard { get; set; } = null;

        // 현재 사용 가능한 카드 목록
        public List<Card> EnableCardList { get; private set; } = new List<Card>();

        public void SetDeck(List<Card> newDeck)
        {
            Deck = new List<Card>(newDeck);
            ShuffleDeck();
        }

        // 패에 있는 모든 카드의 사용 가능 여부를 검사하고 리스트 갱신
        public void UpdatePlayableCards(GameContext context)
        {
            EnableCardList.Clear();

            foreach (Card card in Hand)
            {
                if (card.IsPlayable(context))
                {
                    EnableCardList.Add(card);
                }
            }

            // 디버깅용 로그 (너무 시끄러우면 주석 처리)
            // Console.WriteLine($"   (플레이 가능한 카드: {EnableCardList.Count}장)");
        }

        // 덱 셔플
        public void ShuffleDeck()
        {
            Random rng = new Random();
            Deck = Deck.OrderBy(x => rng.Next()).ToList();
        }

        // 1장만 드로우
        public void Draw()
        {
            DrawCard(1);
        }

        // 덱에서 카드 드로우(복수)
        public bool DrawCard(int n)
        {
            if (Deck.Count - n < 0) return false;
            for (int i = 0; i < n; i++)
            {
                Card card = Deck[0];
                Deck.RemoveAt(0);
                Hand.Add(card);
            }
            Console.WriteLine($"🎴 [{Name}] 가 카드를 {n}장 드로우했습니다. (남은 덱: {Deck.Count}장)");
            return true;
        }

        public void PlayCard(Card card, GameContext context)
        {
            if (!Hand.Contains(card)) return;
            // 1. [자원 소모] 코스트 지불
            Mana -= card.Cost;

            // 2. [물리적 이동] Hand -> PlayingCard (패에서 안전하게 대피)
            Hand.Remove(card);
            PlayingCard = card;
            Console.WriteLine($"\n>>> [{Name}] 이 '{card.Name}' 발동 (Cost: {card.Cost})");

            // [중요] 효과 발동 전에 손패에서 먼저 제거합니다!
            // 이렇게 해야 '패를 버리는 효과'가 자기 자신을 버리지 않습니다.
            // 3. 효과 발동
            card.Play(context);

            // 4. [종료 처리] PlayingCard -> Graveyard
            PlayingCard = null; // 존 비우기
            Graveyard.Add(card);
            Console.WriteLine($"   (묘지에 '{card.Name}' 카드가 쌓였습니다. / 현재 묘지 {Graveyard.Count}장)");
        }

        // DamageEffect에서 호출할 메서드
        public void TakeDamage(int amount)
        {
            Health -= amount;
            Console.WriteLine($"🔻 [{Name}] 가 {amount}의 피해를 입었습니다! (남은 체력: {Health})");
        }

        // HealEffect에서 호출할 메서드
        public void Heal(int amount)
        {
            Health += amount;
            Console.WriteLine($"💚 [{Name}] 가 {amount}의 체력을 회복했습니다. (현재 체력: {Health})");
        }

        // ManaGainEffect에서 호출할 메서드
        public void ManaGain(int amount)
        {
            Mana += amount;
            Console.WriteLine($"+ [{Name}] 가 {amount}의 마나를 회복했습니다. (현재 마나: {Mana})");
        }

        // HandDropEffect에서 호출할 메서드
        public void DropHand(int amount)
        {
            List<Card> droppedCards = new List<Card>();
            Random rng = new Random();
            for (int i = 0; i < amount && Hand.Count > 0; i++)
            {
                int index = rng.Next(Hand.Count);
                Card card = Hand[index];
                Hand.RemoveAt(index);
                Graveyard.Add(card);
                droppedCards.Add(card);
            }
            Console.WriteLine($"🗑️ [{Name}] 가 {droppedCards.Count}장의 카드를 버렸습니다: {string.Join(", ", droppedCards.Select(c => c.Name))}");
            droppedCards.Clear();
        }
    }
}
