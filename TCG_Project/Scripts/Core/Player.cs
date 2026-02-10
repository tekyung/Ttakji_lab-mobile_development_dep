using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Core
{
    public class Player
    {
        public string Name { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }
        public int PrizePoints { get; set; } = 0; // 승점

        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> Graveyard { get; private set; } = new List<Card>();

        // 효과 처리 중인 카드를 잠시 보관하는 장소 (스택)
        public Card PlayingCard { get; set; } = null;

        // 현재 사용 가능한 카드 목록
        public List<Card> EnableCardList { get; private set; } = new List<Card>();

        // 필드 존 (예: 5칸 고정). null이면 빈 공간.
        public Card[] Field { get; private set; } = new Card[5];

        // 필드의 빈 자리 찾기 (-1이면 꽉 참)
        public int GetEmptyFieldSlot()
        {
            for (int i = 0; i < Field.Length; i++)
            {
                if (Field[i] == null) return i;
            }
            return -1;
        }

        /* 턴 시작 시 호출할 메서드 (GameLoop에서 호출 필요)
        public void OnTurnStart()
        {
            // 마나 회복 등은 GameLoop에서 하더라도, 유닛 상태 초기화는 여기서
            foreach (var card in Field)
            {
                if (card != null) card.RefreshUnitState();
            }
        }*/

        public void SetDeck(List<Card> newDeck)
        {
            Deck = new List<Card>(newDeck);
            foreach (var card in Deck)
            {
                card.SetOwner(this); // "이 카드는 내 것이다!" 각인
            }
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
            Console.WriteLine($"(플레이 가능한 카드: {EnableCardList.Count}장)");
        }

        // 덱 셔플
        public void ShuffleDeck()
        {
            Random rng = new Random();
            Deck = Deck.OrderBy(x => rng.Next()).ToList();
        }

        // 1장만 드로우, 지금은 안 씀
        public void Draw()
        {
            DrawCard(1);
        }

        // 덱에서 카드 드로우(복수형), 안 씀
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

        // [핵심 수정] PlayCard
        public void PlayCard(Card card, GameContext context)
        {
            if (!Hand.Contains(card)) return;

            // 1. 자원 소모
            Mana -= card.Cost;

            if (card.Type == CardType.Unit)
            {
                Console.WriteLine($"\n>>> [{Name}] 이 '{card.Name}' 소환 (Cost: {card.Cost})");
            }
            else
            {
                Console.WriteLine($"\n>>> [{Name}] 이 '{card.Name}' 사용 (Cost: {card.Cost}) / 남은 마나: {Mana}");
            }

            // 2. 패에서 PlayingCard 존으로 이동
            Hand.Remove(card);
            PlayingCard = card;

            // 3. 효과 발동
            if (card.Type == CardType.Skill) { card.Play(context); }

            // 4. [종료 처리] PlayingCard -> Graveyard (원래 주인 묘지로!) / 소환 시 유닛 효과
            PlayingCard = null;

            // 유닛 소환시 효과 분기
            if (card.Type == CardType.Unit)
            {
                // [유닛] 필드로 이동
                if (InsertCard(ZoneType.Field, card))
                {
                    // 소환 후유증 (바로 공격 불가) 미적용
                    card.IsExhausted = false;
                    Console.WriteLine($"   ⚔️ [소환] {card.Name} (Power:{card.Power})가 필드에 배치되었습니다.");
                    card.Play(context);
                }
                else
                {
                    // 필드가 꽉 차서 소환 실패 시 -> 묘지로 가거나 핸드로 복귀 (룰에 따라 다름)
                    // 여기서는 묘지로 보내고 경고
                    Console.WriteLine($"   🚫 [소환 실패] 필드가 꽉 찼습니다! {card.Name} 돌아감.");
                    Hand.Add(card);
                }
            }
            else
            {
                // [수정된 부분] 내 묘지가 아니라 '카드의 원래 주인' 묘지로 보냄
                Player owner = card.OriginalOwner ?? this; // 안전장치
                owner.Graveyard.Add(card);
                // 상태 초기화 (묘지로 가니까)
                card.ResetState();
                Console.WriteLine($" ({owner.Name}의 묘지에 '{card.Name}' 카드가 쌓였습니다. / {owner.Name} 묘지 {owner.Graveyard.Count}장)");
            }
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

        // 카드 이동 로직
        // ---------------------------------------------------------
        // 1. 공간 확인 (이동 전 필수 체크)
        // ---------------------------------------------------------
        public bool HasSpaceInZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: return Hand.Count < GameRules.MaxHandSize;
                case ZoneType.Field: return GetEmptyFieldSlot() != -1;
                default: return true; // 덱/묘지는 무제한
            }
        }

        // ---------------------------------------------------------
        // 2. 카드 삽입 (Insert) - 외부에서 카드가 들어올 때
        // ---------------------------------------------------------
        public bool InsertCard(ZoneType zone, Card card)
        {
            if (card == null) return false;

            switch (zone)
            {
                case ZoneType.Deck:
                    Deck.Add(card); // 맨 뒤에 추가 (필요 시 Shuffle 별도 호출)
                    break;

                case ZoneType.Hand:
                    Hand.Add(card);
                    break;

                case ZoneType.Field:
                    int slot = GetEmptyFieldSlot();
                    if (slot != -1) Field[slot] = card;
                    break;

                case ZoneType.Graveyard:
                    Graveyard.Add(card);
                    break;
            }
            return true;
        }

        // ---------------------------------------------------------
        // 3. 카드 추출 (Extract) - 외부로 카드가 나갈 때
        // ---------------------------------------------------------
        // 특정 카드를 지정해서 뺄 때
        public bool ExtractCard(ZoneType zone, Card card)
        {
            if (card == null) return false;

            switch (zone)
            {
                case ZoneType.Deck: return Deck.Remove(card);
                case ZoneType.Hand: return Hand.Remove(card);
                case ZoneType.Graveyard: return Graveyard.Remove(card);
                case ZoneType.Field:
                    // 배열에서 해당 카드를 찾아 비움
                    for (int i = 0; i < Field.Length; i++)
                    {
                        if (Field[i] == card)
                        {
                            Field[i] = null;
                            return true;
                        }
                    }
                    return false;
            }
            return false;
        }

        // (오버로딩) 특정 위치/조건으로 뺄 때 (Top, Random 등)
        public Card ExtractCard(ZoneType zone, string strategy = "Top")
        {
            Card target = null;

            switch (zone)
            {
                case ZoneType.Deck:
                    if (Deck.Count > 0) target = Deck[0]; // 덱은 무조건 맨 위(Top)
                    break;

                case ZoneType.Hand:
                    if (Hand.Count > 0)
                    {
                        // 전략에 따라 선택 (여기선 임시로 Random)
                        // 추후 "Choice"(유저 선택) 등이 들어갈 자리
                        int idx = new Random().Next(Hand.Count);
                        target = Hand[idx];
                    }
                    break;

                case ZoneType.Field:
                    // 필드는 앞에서부터 있는 거 가져옴 (임시)
                    target = Field.FirstOrDefault(c => c != null);
                    break;

                case ZoneType.Graveyard:
                    if (Graveyard.Count > 0) target = Graveyard[Graveyard.Count - 1]; // 가장 최근 것
                    break;
            }

            // 찾았으면 추출 실행
            if (target != null)
            {
                ExtractCard(zone, target);
            }

            return target;
        }

        public void OnTurnStart()
        {
            // 필드에 있는 내 유닛들 상태 초기화
            foreach (var card in Field)
            {
                if (card != null)
                {
                    // 1. 행동력 회복 (공격 기회 리필)
                    card.RefreshUnitState();
                    
                    // 2. [신규] 체력 완전 회복 (줄어든 HP 초기화)
                    if (card.Health < card.MaxHealth)
                    {
                        int healAmount = card.MaxHealth - card.Health;
                        card.Health = card.MaxHealth;
                        Console.WriteLine($"   ✨ [회복] {card.Name}의 체력이 초기화되었습니다. (+{healAmount})");
                    }
                }
            }
        }

        // [신규] ZoneType에 따라 해당 영역의 카드 리스트를 반환하는 메서드
        public List<Card> GetZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand:
                    return Hand;

                case ZoneType.Deck:
                    return Deck;

                case ZoneType.Graveyard:
                    return Graveyard;

                case ZoneType.Field:
                    // Field는 배열(Card[])이므로, 비어있지 않은(null이 아닌) 유닛만 리스트로 변환하여 반환
                    // (TargetSelector가 null 체크를 하긴 하지만, 여기서 걸러주는 게 안전함)
                    return Field.Where(c => c != null).ToList();

                default:
                    return new List<Card>(); // 빈 리스트 반환
            }
        }

    }
}
