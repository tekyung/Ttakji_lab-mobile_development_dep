using System;
using System.Collections.Generic;
using System.Linq;

namespace TCG_Project.Scripts.Core
{
    public class Player
    {
        public string Name { get; set; }
        public int Health { get; set; } = 20;
        public int Mana { get; set; }

        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> Graveyard { get; private set; } = new List<Card>();

        public void SetDeck(List<Card> newDeck)
        {
            Deck = new List<Card>(newDeck);
            ShuffleDeck();
        }

        public void ShuffleDeck()
        {
            Random rng = new Random();
            Deck = Deck.OrderBy(x => rng.Next()).ToList();
        }

        // DrawCardEffect 호환을 위해 Draw() 메서드 추가 (또는 이름을 통일)
        public void Draw()
        {
            DrawCard();
        }

        // 덱에서 카드 드로우(1장)
        public bool DrawCard()
        {
            if (Deck.Count == 0) return false;

            Card card = Deck[0];
            Deck.RemoveAt(0);
            Hand.Add(card);
            Console.WriteLine($"🎴 [{Name}] 가 카드를 드로우했습니다. (남은 덱: {Deck.Count}장)");
            return true;
        }

        public void PlayCard(Card card, GameContext context)
        {
            if (!Hand.Contains(card)) return;

            Mana -= card.Cost;
            Console.WriteLine($"\n>>> [{Name}] 이 '{card.Name}' 발동 (Cost: {card.Cost})");

            card.Play(context); // 효과 실행

            Hand.Remove(card);
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
    }
}
