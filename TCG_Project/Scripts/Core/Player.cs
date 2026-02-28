using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    public class Player
    {
        public string Name { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }
        private int _prizePoints;
        public int PrizePoints
        {
            get => _prizePoints;
            set
            {
                _prizePoints = value;
                // 값 변경 시 UI 갱신 이벤트만 쏜다 (게임 오버 판별 삭제)
                EventManager.OnPrizeChange?.Invoke(this, _prizePoints);

                /* ★ 점수가 오르는 즉시 게임 셋을 외친다!
                if (_prizePoints >= GameRules.WinPrizePoints)
                {
                    EventManager.OnGameSet?.Invoke(this);
                }
                */
            }
        }
        // ★ 플레이어의 타입과 두뇌
        public UserType Type { get; set; } = UserType.Bot; // 기본값은 Bot
        public IPlayerBrain Brain { get; set; } // 행동을 위임할 두뇌 인터페이스


        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> Graveyard { get; private set; } = new List<Card>();

        // 효과 처리 중인 카드를 잠시 보관하는 장소 (스택)
        public Card PlayingCard { get; set; } = null;

        // 현재 사용 가능한 카드 목록
        public List<Card> EnableCardList { get; private set; } = new List<Card>();

        // (주의: 객체 생성 시점에 GameRules가 로드되어 있어야 함)
        public Card[] Field { get; private set; }

        // 플레이어 생성 후, Type에 맞는 두뇌를 셋팅해주는 초기화 메서드
        public void InitializeBrain()
        {
            if (this.Type == UserType.Bot)
            {
                this.Brain = new BotBrain(this); // 기존 자동화 로직
            }
            else if (this.Type == UserType.Human)
            {
                this.Brain = new HumanBrain(this); // 입력을 기다리는 로직
            }
        }
        public Player()
        {
            // 필드 최대 유닛 수(3칸) 적용
            Field = new Card[GameRules.MaxFieldUnitCount];
        }

        // 필드의 빈 자리 찾기 (-1이면 꽉 참)
        public int GetEmptyFieldSlot()
        {
            for (int i = 0; i < Field.Length; i++)
            {
                if (Field[i] == null) return i;
            }
            return -1;
        }

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
            EventManager.OnLogMessage?.Invoke($"(플레이 가능한 카드: {EnableCardList.Count}장)");
        }

        // 덱 셔플
        public void ShuffleDeck()
        {
            Random rng = new Random();
            Deck = Deck.OrderBy(x => rng.Next()).ToList();
        }

        /// <summary>
        /// 카드를 플레이(소환/발동)합니다. 
        /// 카드의 모든 효과(타겟팅 대기 등)가 끝나면 onCardPlayed 콜백이 호출됩니다.
        /// </summary>
        // 카드 사용 로직 (비동기 콜백 지원)
        public void PlayCard(Card card, GameContext context, Action onCardPlayed = null)
        {
            if (!Hand.Contains(card))
            {
                // [안전장치] 패에 없는 카드면 무시하되, 대기 중인 엔진이 멈추지 않도록 콜백은 쏴줍니다.
                onCardPlayed?.Invoke();
                return;
            }

            // 1. 자원 소모
            Mana -= card.Cost;
            EventManager.OnManaChange?.Invoke(this, Mana); // 마나 썼으니 갱신

            // 카드 사용 알림 (UI: 패에서 카드가 날아가는 연출)
            EventManager.OnPlayCard?.Invoke(this, card);

            if (card.Type == CardType.Unit)
            {
                EventManager.OnLogMessage?.Invoke($"\n>>> [{Name}] 이 '{card.Name}' 소환 (Cost: {card.Cost})");
            }
            else
            {
                EventManager.OnLogMessage?.Invoke($"\n>>> [{Name}] 이 '{card.Name}' 사용 (Cost: {card.Cost}) / 남은 마나: {Mana}");
            }

            // 2. 패에서 PlayingCard 존으로 이동
            Hand.Remove(card);
            PlayingCard = card;

            // 3. 카드 타입별 비동기 효과 발동
            if (card.Type == CardType.Skill)
            {
                // [스펠] 효과를 실행하고, 유저의 타겟팅이나 처리가 모두 끝나면 람다식 안쪽이 실행됩니다.
                card.Play(context, () =>
                {
                    // 효과가 끝난 후 PlayingCard를 비우고 묘지로 보냄
                    PlayingCard = null;

                    Player owner = card.OriginalOwner ?? this; // 안전장치
                    owner.Graveyard.Add(card);
                    card.ResetState(); // 상태 초기화

                    EventManager.OnCardMove?.Invoke(card, this, ZoneType.Hand, owner, ZoneType.Graveyard);
                    EventManager.OnLogMessage?.Invoke($"  ({owner.Name}의 묘지에 '{card.Name}' 카드가 쌓였습니다. / {owner.Name} 묘지 {owner.Graveyard.Count}장)");

                    // ★ 모든 물리적 처리가 끝났음을 엔진에 보고
                    onCardPlayed?.Invoke();
                });
            }
            else if (card.Type == CardType.Unit)
            {
                // [유닛] PlayingCard를 비우고 필드로 이동 시도
                PlayingCard = null;

                if (InsertCard(ZoneType.Field, card))
                {
                    // 소환 성공
                    card.IsExhausted = false;
                    EventManager.OnUnitSummoned?.Invoke(card);
                    EventManager.OnLogMessage?.Invoke($"   ⚔️ [소환] {card.Name} (Power:{card.Power})가 필드에 배치되었습니다.");

                    // 소환 시 효과 비동기 실행
                    card.Play(context, () =>
                    {
                        // 유저가 효과 대상을 다 고르거나, 자동으로 처리가 끝나면 엔진에 보고
                        onCardPlayed?.Invoke();
                    });
                }
                else
                {
                    // 필드가 꽉 차서 소환 실패 시
                    EventManager.OnLogMessage?.Invoke($"   🚫 [소환 실패] 필드가 꽉 찼습니다! {card.Name} 패로 돌아감.");
                    Hand.Add(card);

                    // ★ 실패했어도 턴 진행이 멈추지 않도록 엔진에 보고
                    onCardPlayed?.Invoke();
                }
            }
        }

        // 프라이즈(승점) 획득 및 종료 판별 함수(기존의 TakeDamage 대체)
        public void GetPrize(int amount, GameContext context)
        {
            // ★ [상태 가드] 이미 게임이 끝났다면 추가 점수 획득 무시
            if (context.IsGameOver) return;

            // 1. 점수 증가 (이때 setter가 호출되어 OnPrizeChange UI 갱신이 일어남)
            PrizePoints += amount; 
            
            // 2. 점수 획득 로그를 "먼저" 출력!
            EventManager.OnLogMessage?.Invoke($"🏆 [{Name}] 승점 {amount} 획득! (현재 승점: {PrizePoints}/{GameRules.WinPrizePoints})");

            // 3. 점수가 다 찼다면 게임 종료 선언을 "마지막"에 출력! / GameRules.WinPrizePoints 사용
            if (PrizePoints >= GameRules.WinPrizePoints)
            {
                context.IsGameOver = true; // 문을 잠가서 추가 연쇄 작용 차단
                EventManager.OnGameSet?.Invoke(this); // "내가 이겼다!" 방송 송출
            }
        }

        // ManaGainEffect에서 호출할 메서드
        public void ManaGain(int amount)
        {
            Mana += amount;
            EventManager.OnLogMessage?.Invoke($"+ [{Name}] 가 {amount}의 마나를 회복했습니다. (현재 마나: {Mana})");
            EventManager.OnManaChange?.Invoke(this, Mana);
        }

        /// <summary>
        /// 내 패(Hand)에 있는 카드 중, 당장 소환이 가능하며 "소환 시 효과"의 발동 조건까지 만족하는 
        /// 유닛 카드의 인덱스(Index) 리스트를 반환합니다.
        /// (UI 하이라이팅 또는 AI 판단용)
        /// </summary>
        public List<int> GetUsableEffectCardIndices(GameContext context)
        {
            List<int> validIndices = new List<int>();

            for (int i = 0; i < Hand.Count; i++)
            {
                Card card = Hand[i];

                // 1차 필터: 유닛 카드이며, 효과가 하나 이상 있고, 당장 소환(코스트/자리/소환조건)이 가능한가?
                if (card.Type == CardType.Unit && card.Effects.Count > 0 && card.IsPlayable(context))
                {
                    // 2차 필터: 소환 시 효과 발동 조건(EffectCondition)을 만족하는가?
                    bool conditionMet = true;
                    if (!string.IsNullOrEmpty(card.EffectCondition) && card.EffectCondition.Trim().ToLower() != "none")
                    {
                        conditionMet = Systems.ConditionEvaluator.Evaluate(card.EffectCondition, context);
                    }

                    if (conditionMet)
                    {
                        validIndices.Add(i); // 조건을 모두 만족하면 인덱스 저장
                    }
                }
            }
            return validIndices;
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

                    // 2. Power 완전 회복 (줄어든 Power 초기화)
                    if (card.Power < card.OriginalPower)
                    {
                        int healAmount = card.OriginalPower - card.Power;

                        // 연산자로 직접 조작하지 않고, 전담 파이프라인을 통해 안전하게 회복 (아직은 미사용)
                        // card.ModifyPower(healAmount, "턴 시작 회복");
                    }
                }
            }
        }

        // ZoneType에 따라 해당 영역의 카드 리스트를 반환하는 메서드
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
