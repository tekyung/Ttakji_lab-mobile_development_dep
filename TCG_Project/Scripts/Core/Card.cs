using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    public class Card
    {   // 스펠 카드 종류
        // 게임 중 변할 수 있는 고유 ID (인스턴스 식별용)
        public string InstanceId { get; set; }
        // 원본 데이터 ID (어떤 종류의 카드인가?)
        public string DataId { get; set; }
        public string Name { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }
        public string PlayCondition { get; set; } // 카드의 발동 조건

        // --- 신규 유닛 스탯 ---
        public string Id { get; set; } // 여기가 대문자 'I'인지 확인
        public CardType Type { get; set; }
        public int Power { get; set; }      // 공격력
        public int MaxHealth { get; set; }  // 최대 체력 (Power로 초기화)
        public int Health { get; set; }     // 현재 체력
        public int AttackCost { get; set; } // arts_cost (공격 시 필요한 코스트)
        public int Prize { get; set; }      // 처치 시 줄 보상
        public string SkinResource { get; set; } // 이미지 경로

        // 이 리스트가 있어야 GameDataManager에서 Effects.Add(...)를 할 수 있습니다.
        public List<ICardEffect> Effects { get; set; } = new List<ICardEffect>();

        // [전투용 상태 변수]
        public bool IsExhausted { get; set; } = false; // 행동 완료(피로) 상태

        // 턴 시작 시 상태 초기화 (Player.cs에서 호출 예정)
        public void RefreshUnitState()
        {
            IsExhausted = false;
        }

        // 카드는 여러 개의 효과를 가질 수 있습니다.(구형)
        // private List<ICardEffect> effects = new List<ICardEffect>();

        // 불변 스탯(초기화용 원본 데이터)
        public int OriginalCost { get; set; } // 원래 코스트 기억
        
        // 원래 주인 (게임 시작 시 덱의 주인, 불변)
        public Player OriginalOwner { get; set; }

        // 현재 컨트롤러 (누구 필드/패에 있는가, 가변)
        public Player Controller { get; set; }


        // 팩토리에서 카드 생성 시 호출 (최초 1회)
        public void InitializeData(int baseCost)
        {
            this.OriginalCost = baseCost;
            this.Cost = baseCost;
        }

        public void InitializeUnitStats()
        {
            if (Type == CardType.Unit)
            {
                // 유닛 배치 시 초기화 로직
                Health = MaxHealth;
            }
        }

        // 게임 시작 시 덱 세팅할 때 호출
        public void SetOwner(Player owner)
        {
            OriginalOwner = owner;
            Controller = owner;
        }

        // [핵심] 상태 초기화 (묘지행, 바운스 등)
        public void ResetState()
        {
            // 1. 코스트 복구
            this.Cost = this.OriginalCost;

            // 2. 소유권 복구 (원래 주인에게 돌아감)
            this.Controller = this.OriginalOwner;

            // 추후 공격력/체력/상태이상 초기화 로직이 여기에 추가됨
            // 예: this.Attack = this.OriginalAttack;

            // 주의: Effects 리스트는 건드리지 않음 (카드 고유 능력이므로)
        }

        public void AddEffect(ICardEffect effect)
        {
            Effects.Add(effect);
        }

        // 카드를 사용할 때 호출
        public void Play(GameContext context)
        {
            // [중요] 새 카드를 발동할 때 컨텍스트 변수 초기화
            context.ClearVariables();

            // [디버깅] 스펠 사용 시작 로그
            if (this.Type == CardType.Skill)
            {
                DebugHelper.LogSpell($"'{Name}' 발동 시작! (보유 효과: {Effects.Count}개)");
            }

            Console.WriteLine($"--- {Name} / {Cost} / {Description} ---\n");
            foreach (var effect in Effects)
            {
                effect.Execute(context);
            }
            Console.WriteLine("---------------------------------------------\n");
        }

        // [조건 판별] 이 카드를 지금 쓸 수 있는가?
        public bool IsPlayable(GameContext context)
        {
            // 1. 마나 부족 체크
            if (Controller.Mana < this.Cost) return false;

            // 2. [신규] 유닛 소환 공간 체크
            if (this.Type == CardType.Unit)
            {
                // [나중에 구현] 제물 소환(Tribute Summon) 여부 확인
                // 예: Level 5 이상이라 제물이 필요하다면, 필드가 꽉 차 있어도 
                // 제물을 바치고 그 자리에 들어갈 수 있으므로 공간 체크를 건너뜀(true).
                bool isTributeSummon = false; // (임시 플래그)

                if (!isTributeSummon)
                {
                    // 일반 소환이면 빈 자리가 있어야 함
                    if (!Controller.HasSpaceInZone(ZoneType.Field)) return false;
                }
            }

            // 2. 커스텀 발동 조건 확인
            if (!string.IsNullOrEmpty(PlayCondition))
            {
                return Systems.ConditionEvaluator.Evaluate(PlayCondition, context);
            }

            return true; // 조건이 없으면 마나만 되면 OK
        }

        // 깊은 복사 메서드
        public Card Clone()
        {
            Card newCard = new Card
            {
                InstanceId = System.Guid.NewGuid().ToString(), // 고유한 주민등록번호 발급
                DataId = this.DataId, // 원본 카드의 종류 유지
                Name = this.Name,
                Cost = this.Cost,
                PlayCondition = this.PlayCondition,
                Description = this.Description
            };

            // 효과 리스트도 새로 만들어서 독립성 보장
            // (주의: Effect 객체 자체도 상태를 가진다면 Effect.Clone()이 필요하지만, 
            // 현재 단계에서는 리스트만 새로 파도 충분합니다.)
            foreach (var effect in this.Effects)
            {
                newCard.AddEffect(effect);
            }

            return newCard;
        }

        // Card 클래스 내부에 추가
        public bool HasEffectType(string typeName)
        {
            // 효과 리스트를 순회하며 타입 이름이 포함되는지 검사
            foreach (var effect in Effects)
            {
                // 예: DamageEffect -> "Damage" 포함됨
                if (effect.GetType().Name.Contains(typeName)) return true;

                // 원자적 효과의 경우 JSON의 "type" 필드를 별도로 저장하고 있다면 그것을 비교하는 것이 더 정확함
                // 현재는 클래스 이름(DamageEffect) 기반으로 약식 구현
            }
            return false;
        }
    }
}
