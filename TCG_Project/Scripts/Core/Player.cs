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
        // ★ 플레이어의 타입과 두뇌
        public UserType Type { get; set; } = UserType.Bot; // 기본값은 Bot
        public IPlayerBrain Brain { get; set; } // 행동을 위임할 두뇌 인터페이스

        // 이 플레이어가 선택한 1번째(주) 캐릭터 카드 ID. 능력 발동에 사용.
        // 예: "ELLI-01"(엘리), "VERO-01"(베로니카)
        public string CharacterCardId { get; set; } = null;

        // 2번째(부) 캐릭터 카드 ID. 듀얼 덱 시에만 사용. null이면 단일 캐릭터.
        public string SecondaryCharacterId { get; set; } = null;

        // 듀얼 덱: 이 플레이어가 해당 캐릭터를 보유하는지 (주 또는 부).
        public bool HasCharacter(string characterCardId)
            => CharacterCardId == characterCardId || SecondaryCharacterId == characterCardId;

        // 듀얼 덱: 캐릭터별 능력 사용 여부. Phase 17 확장 — 각 캐릭터당 1회, 총 2회 가능.
        // ResetForNewGame() 호출 시 초기화.
        private HashSet<string> _usedCharacterAbility = new HashSet<string>();

        // 해당 캐릭터의 고유 능력을 이 게임에서 이미 사용했는지 여부를 반환한다.
        public bool HasUsedCharacterAbility(string characterCardId)
            => _usedCharacterAbility.Contains(characterCardId);

        // 해당 캐릭터의 고유 능력을 이 게임에서 사용했음을 표시한다 (듀얼: 캐릭터당 1회).
        public void MarkCharacterAbilityUsed(string characterCardId)
        {
            if (!string.IsNullOrEmpty(characterCardId))
                _usedCharacterAbility.Add(characterCardId);
        }

        // --- 기존 존 ---
        public List<Card> Deck { get; private set; } = new List<Card>();
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Card> Graveyard { get; private set; } = new List<Card>();

        // --- 룰북 신규 존 ---
        public List<Card> ResourceDeck { get; private set; } = new List<Card>();
        public List<Card> ResourceZone { get; private set; } = new List<Card>(); // 자원 존
        public Card SetZoneCard { get; private set; } = null; // 세트 존 (뒷면 카드 1장 보관) - 공개 시 RevealSetCard()로 반환 후 null로 초기화
        public List<Card> StackZone { get; private set; } = new List<Card>(); // 스택 효과 카드 보관
        public Card BattlefieldCard { get; private set; } = null; // 전장 효과 카드 보관

        // 임시 발동 대기 존 (ReplayCardEffect 등 복합 효과에서 카드 보관)
        public List<Card> PlayBuffer { get; private set; } = new List<Card>();

        // 라이프 토큰 (기본 5개, 0이 되면 패배)
        public int LifeTokens { get; private set; } = GameRules.LifeTokens;

        // 효과 처리 중인 카드를 잠시 보관하는 장소 (BattlefieldEffect 등에서 사용)
        public Card PlayingCard { get; set; } = null;

        // --- 전투 버프 (라운드 단위, EndPhase에서 ClearCombatBuffs로 초기화) ---
        // --- 이번 턴 오픈 카드 버프 (턴 종료 시 초기화) ---
        public int ArmorBonus { get; set; } = 0;  // 일반 데미지만 N 감소
        public int SuperArmorBonus { get; set; } = 0;  // 관통 포함 모든 데미지 N 감소
        public bool IsInvincible { get; set; } = false; // 키워드 데미지 전체 차단
        public int FirepowerBonus { get; set; } = 0;  // 자신의 데미지/관통 수치 +N
        public bool HasCounterAttack { get; set; } = false; // 받은 원본 데미지 반환

        // 반격 성공 시 1회성으로 실행될 보상 대기열 (Action 델리게이트 사용 / ex: "리벤지" 효과)
        public Queue<Action> PendingCounterRewards { get; private set; } = new Queue<Action>();

        // --- 스택형 버프 (1회성, 개별 방어구로 관리) ---
        public List<int> StackArmors { get; set; } = new List<int>();   // 1회용 일반 방어
        public List<int> StackSuperArmors { get; set; } = new List<int>();  // 1회용 관통 방어
        public List<Card> StackCounterAttacks { get; set; } = new List<Card>(); // 1회용 반격 카드
        //public Queue<Card> StackCounterAttacks { get; set; } = new Queue<Card>();
        public List<Card> StackInvincibilities { get; set; } = new List<Card>(); // 1회용 무적 카드
        public List<int> StackFirepowers { get; set; } = new List<int>(); // 1회용 화력 카드

        // --- 다음 턴 예약 버프 (NextTurnBuffEffect에서 설정, 다음 드로우 페이즈에서 적용) ---
        public int NextTurnFirepowerBonus { get; set; } = 0;
        public int NextTurnArmorBonus { get; set; } = 0;
        public int NextTurnSuperArmorBonus { get; set; } = 0;
        public bool NextTurnIsInvincible { get; set; } = false;
        public bool NextTurnCounterAttack {get; set;} = false;

        // [전장 전용 버프] 매 턴 드로우 페이즈에 리필되며, 첫 타격 발생 시 즉시 소진됨
        public int BattlefieldArmor { get; set; } = 0;
        public int BattlefieldFirepower { get; set; } = 0;

        /// <summary>
        /// 라운드 종료 시 전투 버프를 초기화한다 (ThisTurn 효과 만료).
        /// NextTurn 예약 버프는 여기서 초기화하지 않는다 (다음 드로우 페이즈에서 소비).
        /// </summary>
        public void ClearCombatBuffs()
        {
            ArmorBonus = 0;
            SuperArmorBonus = 0;
            IsInvincible = false;
            FirepowerBonus = 0;
            HasCounterAttack = false;
            BattlefieldArmor = 0;
            BattlefieldFirepower = 0;
            PendingCounterRewards.Clear(); // 턴 종료 시 대기열도 초기화
        }

        // 다음 턴 예약 버프를 이번 턴 버프로 적용하고 예약을 소비한다 (드로우 페이즈 시작 시 호출).
        public void ApplyNextTurnBuffs()
        {
            if (NextTurnFirepowerBonus > 0)
            {
                FirepowerBonus += NextTurnFirepowerBonus;
                EventManager.OnLogMessage?.Invoke(
                    $"  [다음 턴 버프] {Name} 화력 +{NextTurnFirepowerBonus} 적용 (이번 턴 총: {FirepowerBonus})");
                NextTurnFirepowerBonus = 0;
            }
            if (NextTurnArmorBonus > 0)
            {
                ArmorBonus += NextTurnArmorBonus;
                EventManager.OnLogMessage?.Invoke(
                    $"  [다음 턴 버프] {Name} 아머 +{NextTurnArmorBonus} 적용 (이번 턴 총: {ArmorBonus})");
                NextTurnArmorBonus = 0;
            }
            if (NextTurnSuperArmorBonus > 0)
            {
                SuperArmorBonus += NextTurnSuperArmorBonus;
                EventManager.OnLogMessage?.Invoke(
                    $"  [다음 턴 버프] {Name} 슈퍼아머 +{NextTurnSuperArmorBonus} 적용 (이번 턴 총: {SuperArmorBonus})");
                NextTurnSuperArmorBonus = 0;
            }
            if (NextTurnIsInvincible)
            {
                IsInvincible = true;
                EventManager.OnLogMessage?.Invoke(
                    $"  [다음 턴 버프] {Name} 무적 상태 적용");
                NextTurnIsInvincible = false;
            }
        }


        // 라이프 토큰을 회복한다 (캐릭터 능력 등에서 사용).
        public void GainLife(int amount)
        {
            int before = LifeTokens;
            LifeTokens = Math.Min(GameRules.LifeTokens, LifeTokens + amount);
            int gained = LifeTokens - before;
            if (gained > 0)
                EventManager.OnLogMessage?.Invoke(
                    $"💚 [{Name}] 라이프 +{gained} 회복 (현재: {LifeTokens}/{GameRules.LifeTokens})");
        }

        // 새 게임 시작 시 플레이어 상태를 완전 초기화한다 (MatchManager에서 호출).
        public void ResetForNewGame(List<Card> newDeck, List<Card> newResourceDeck)
        {
            SetDeck(newDeck);
            SetResourceDeck(newResourceDeck);
            InitializeLifeTokens();
            ClearCombatBuffs();

            _usedCharacterAbility.Clear();
            Hand.Clear();
            Graveyard.Clear();
            ResourceZone.Clear();
            SetZoneCard = null;
            StackZone.Clear();
            BattlefieldCard = null;
            PlayBuffer.Clear();
            PlayingCard = null;
            EnableCardList.Clear();

            NextTurnFirepowerBonus = 0;
            NextTurnArmorBonus = 0;
            NextTurnIsInvincible = false;
            NextTurnSuperArmorBonus = 0;

            StackArmors.Clear();
            StackSuperArmors.Clear();
            StackCounterAttacks.Clear();
            StackInvincibilities.Clear();
            StackFirepowers.Clear();
            BattlefieldArmor = 0;
            BattlefieldFirepower = 0;
        }

        // 현재 사용 가능한 카드 목록
        public List<Card> EnableCardList { get; private set; } = new List<Card>();

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

        // --- 라이프 토큰 초기화 (게임 시작 시 호출) ---
        public void InitializeLifeTokens()
        {
            LifeTokens = GameRules.LifeTokens;
        }

        // --- 자원덱 초기화 ---
        public void SetResourceDeck(List<Card> deck)
        {
            ResourceDeck = new List<Card>(deck);
        }

        // --- 자원 페이즈: 자원덱에서 자원존으로 1장 이동 ---
        public bool TakeResourceCard()
        {
            if (ResourceDeck.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke($"[{Name}] 자원덱이 비어있습니다.");
                return false;
            }
            Card resource = ResourceDeck[0];
            ExtractCard(ZoneType.ResourceDeck, ResourceDeck[0]); // 자원덱에서 제거
            InsertCard(ZoneType.ResourceZone, resource); // 자원존으로 이동
            EventManager.OnLogMessage?.Invoke($"[{Name}] 자원 획득 (자원존: {ResourceZone.Count}개 / 자원덱 잔여: {ResourceDeck.Count}장)");
            return true;
        }

        // --- 현재 사용 가능한 자원 수 (자원존 카드 수) ---
        public int GetResourceCount() => ResourceZone.Count;

        // --- 코스트 지불 가능 여부 ---
        public bool CanAfford(int cost) => ResourceZone.Count >= cost;

        // --- 코스트 지불: 자원존 → 폐기존 ---
        public bool PayCost(int cost)
        {
            if (!CanAfford(cost))
            {
                EventManager.OnLogMessage?.Invoke($"[{Name}] 코스트 부족 (필요: {cost}, 보유: {ResourceZone.Count})");
                return false;
            }
            for (int i = 0; i < cost; i++)
            {
                Card resource = ResourceZone[ResourceZone.Count - 1];
                ExtractCard(ZoneType.ResourceZone, ResourceZone[ResourceZone.Count - 1]); // 자원존에서 제거
                InsertCard(ZoneType.Graveyard, resource);
            }
            EventManager.OnLogMessage?.Invoke($"[{Name}] 코스트 {cost} 지불 (자원존 잔여: {ResourceZone.Count}개)");
            return true;
        }

        // --- 세트 페이즈: 패에서 세트존으로 카드 이동 (뒷면) ---
        public bool SetCard(Card card)
        {
            if (!Hand.Contains(card)) return false;
            if (SetZoneCard != null)
            {
                return false; // 세트존에 이미 카드가 있으면 실패 (룰북 기준)

                EventManager.OnLogMessage?.Invoke($"[{Name}] 세트존에 이미 카드('{SetZoneCard.Name}')가 있어 교체합니다.");
                Card oldSet = SetZoneCard;
                ExtractCard(ZoneType.SetZone, SetZoneCard);
                InsertCard(ZoneType.Hand, oldSet); // 기존 카드는 패로 복귀
            }
            ExtractCard(ZoneType.Hand, card); // 패에서 제거
            InsertCard(ZoneType.SetZone, card); // 세트존으로 이동
            EventManager.OnLogMessage?.Invoke($"[{Name}] 세트존에 카드('{SetZoneCard.Name}')를 뒷면으로 세트했습니다.");
            return true;
        }

        // --- 오픈 페이즈 - 폐기 선택: 세트 카드를 뒷면으로 폐기 + 드로우 1장 ---
        public void AbandonSetCard()
        {
            if (SetZoneCard == null) return;
            Card abandoned = SetZoneCard;
            ExtractCard(ZoneType.SetZone, SetZoneCard); // 세트존에서 제거
            InsertCard(ZoneType.Graveyard, abandoned); // 폐기존으로 이동
            EventManager.OnLogMessage?.Invoke($"[{Name}] 세트 카드('{abandoned.Name}')를 폐기했습니다. 메인덱에서 1장 드로우.");
            // 드로우는 호출자(GameRunner)에서 처리
        }

        // --- 오픈 페이즈 - 공개 선택: 세트 카드를 앞면으로 공개 (카드 객체 반환) ---
        public Card RevealSetCard()
        {
            if (SetZoneCard == null) return null;
            Card revealed = SetZoneCard;
            EventManager.OnLogMessage?.Invoke($"[{Name}] '{revealed.Name}' 공개! (Speed: {revealed.Speed}, Type: {revealed.Type})");
            return revealed;
        }

        // --- 스택존: 스택 카드 추가 ---
        public void AddToStackZone(Card card)
        {
            InsertCard(ZoneType.StackZone, card);
            EventManager.OnLogMessage?.Invoke($"[{Name}] '{card.Name}'을 스택존에 배치. (스택존: {StackZone.Count}장)");
        }

        // --- 스택존: 스택 카드 사용 후 폐기 ---
        public void UseAndDiscardStack(Card card)
        {
            if (!StackZone.Contains(card)) return;
            ExtractCard(ZoneType.StackZone, card); // 스택존에서 제거
            InsertCard(ZoneType.Graveyard, card); // 폐기존으로 이동
            EventManager.OnLogMessage?.Invoke($"[{Name}] 스택 카드 '{card.Name}' 효과 사용 → 폐기존.");
        }

        // --- 전장존: 전장 카드 배치 ---
        public bool PlaceBattlefield(Card card)
        {
            if (BattlefieldCard != null)
            {
                EventManager.OnLogMessage?.Invoke($"[{Name}] 기존 전장 '{BattlefieldCard.Name}' 폐기.");

                // 1. 기존 전장 카드를 묘지(폐기존)로 이동
                Card oldCard = BattlefieldCard;
                BattlefieldCard = null; // 안전을 위해 일단 비움
                InsertCard(ZoneType.Graveyard, oldCard);

                // 2. ★ 중요: 기존 전장이 부여했던 1회성 전장 버프를 완전히 날려버립니다.
                BattlefieldArmor = 0;
                BattlefieldFirepower = 0;
            }

            BattlefieldCard = card; // 새 카드 배치
            // InsertCard(ZoneType.BattlefieldZone, card); 
            EventManager.OnLogMessage?.Invoke($"[{Name}] '{card.Name}'을 전장존에 배치.");
            return true;
        }

        // --- 전장존: 전장 카드 파괴 ---
        public void DestroyBattlefield()
        {
            if (BattlefieldCard == null) return;
            EventManager.OnLogMessage?.Invoke($"[{Name}] 전장 카드 '{BattlefieldCard.Name}' 파괴 → 폐기존.");

            // 1. 기존 전장 카드를 묘지(폐기존)로 이동
            Card oldCard = BattlefieldCard;
            BattlefieldCard = null; // 안전을 위해 일단 비움
            InsertCard(ZoneType.Graveyard, oldCard);

            // 2. ★ 중요: 기존 전장이 부여했던 1회성 전장 버프를 완전히 날려버립니다.
            BattlefieldArmor = 0;
            BattlefieldFirepower = 0;
        }

        // --- 라이프 토큰 감소 (메인 승리 조건) ---
        public void LoseLife(int amount, GameContext context)
        {
            if (context.IsGameOver) return;
            LifeTokens = Math.Max(0, LifeTokens - amount);
            EventManager.OnLogMessage?.Invoke($"💔 [{Name}] 라이프 -{amount} (남은 라이프: {LifeTokens}/{GameRules.LifeTokens})");
            EventManager.OnLifeChange?.Invoke(this, LifeTokens);

            /* 승패 판정 로직을 시스템에게 이관
            if (LifeTokens <= 0)
            {
                context.IsGameOver = true;
                Player winner = context.GetOpponent(this);
                EventManager.OnGameSet?.Invoke(winner);
            }*/
        }

        // --- 폐기존 총 장수 (타이브레이커용) ---
        public int GetTotalDiscardCount() => Graveyard.Count;

        // --- 덱 초기화 및 카드 소유권 설정 ---
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

        // 카드 이동 로직
        // ---------------------------------------------------------
        // 1. 공간 확인 (이동 전 필수 체크)
        // ---------------------------------------------------------
        public bool HasSpaceInZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: return Hand.Count < GameRules.MaxHandSize;
                case ZoneType.SetZone: return SetZoneCard == null;
                case ZoneType.BattlefieldZone: return true; // 기존 전장 카드는 덮어씀
                default: return true;
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
                    Deck.Add(card);
                    break;
                case ZoneType.Hand:
                    Hand.Add(card);
                    break;
                case ZoneType.Graveyard:
                    Graveyard.Add(card);
                    break;
                case ZoneType.ResourceDeck:
                    ResourceDeck.Add(card);
                    break;
                case ZoneType.ResourceZone:
                    ResourceZone.Add(card);
                    break;
                case ZoneType.StackZone:
                    StackZone.Add(card);
                    break;
                case ZoneType.SetZone:
                    SetZoneCard = card;
                    break;
                case ZoneType.BattlefieldZone:
                    PlaceBattlefield(card);
                    break;
                case ZoneType.PlayBuffer:
                    PlayBuffer.Add(card);
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
                case ZoneType.ResourceDeck: return ResourceDeck.Remove(card);
                case ZoneType.ResourceZone: return ResourceZone.Remove(card);
                case ZoneType.StackZone: return StackZone.Remove(card);
                case ZoneType.SetZone:
                    if (SetZoneCard == card) { SetZoneCard = null; return true; }
                    return false;
                case ZoneType.BattlefieldZone:
                    if (BattlefieldCard == card) { BattlefieldCard = null; return true; }
                    return false;
                case ZoneType.PlayBuffer:
                    return PlayBuffer.Remove(card);
            }
            return false;
        }

        // (오버로딩) 특정 위치/조건으로 뺄 때 (Top, Bottom, Random 등)
        public Card ExtractCard(ZoneType zone, string strategy = "Top")
        {
            Card target = null;

            switch (zone)
            {
                case ZoneType.Deck:
                    // 메인덱은 항상 맨 위(인덱스 0)에서 드로우
                    if (Deck.Count > 0) target = Deck[0];
                    break;

                case ZoneType.ResourceDeck:
                    // 자원덱도 맨 위에서 1장씩 가져옴
                    if (ResourceDeck.Count > 0) target = ResourceDeck[0];
                    break;

                case ZoneType.Hand:
                    if (Hand.Count > 0)
                    {
                        if (strategy == "Random")
                        {
                            int idx = new Random().Next(Hand.Count);
                            target = Hand[idx];
                        }
                        else // "Top" = 맨 앞
                        {
                            target = Hand[0];
                        }
                    }
                    break;

                case ZoneType.ResourceZone:
                    // 자원존은 가장 나중에 쌓인 것(맨 뒤)에서 소비
                    if (ResourceZone.Count > 0) target = ResourceZone[ResourceZone.Count - 1];
                    break;

                case ZoneType.StackZone:
                    // 스택존은 맨 앞(가장 먼저 세팅된 것)부터 꺼냄
                    if (StackZone.Count > 0) target = StackZone[0];
                    break;

                case ZoneType.Graveyard:
                    // 묘지는 가장 최근에 들어간 것(맨 뒤)
                    if (Graveyard.Count > 0) target = Graveyard[Graveyard.Count - 1];
                    break;

                case ZoneType.SetZone:
                    target = SetZoneCard;
                    break;

                case ZoneType.BattlefieldZone:
                    target = BattlefieldCard;
                    break;

                case ZoneType.PlayBuffer:
                    if (PlayBuffer.Count > 0) target = PlayBuffer[0];
                    break;
            }

            if (target != null)
            {
                ExtractCard(zone, target);
            }

            return target;
        }

        // ZoneType에 따라 해당 영역의 카드 리스트를 반환하는 메서드
        public List<Card> GetZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: return Hand;
                case ZoneType.Deck: return Deck;
                case ZoneType.Graveyard: return Graveyard;
                case ZoneType.ResourceDeck: return ResourceDeck;
                case ZoneType.ResourceZone: return ResourceZone;
                case ZoneType.StackZone: return StackZone;
                case ZoneType.SetZone:
                    return SetZoneCard != null ? new List<Card> { SetZoneCard } : new List<Card>();
                case ZoneType.BattlefieldZone:
                    return BattlefieldCard != null ? new List<Card> { BattlefieldCard } : new List<Card>();
                case ZoneType.PlayBuffer:
                    return PlayBuffer;
                default:
                    return new List<Card>();
            }
        }
    }
}
