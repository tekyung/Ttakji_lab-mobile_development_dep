using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Core
{
    public class Card
    {   // 모든 카드 공통 속성 및 메서드 정의
        // 게임 중 변할 수 있는 고유 ID (인스턴스 식별용)
        public string InstanceId { get; set; }

        // 원본 데이터 ID (어떤 종류의 카드인가?)
        public string DataId { get; set; }
        public string Name { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }
        public string PlayCondition { get; set; } // 카드의 발동 조건

        // ★ 카드의 "효과 발동" 조건 (CardEffect.json의 effect_function_type 참조)
        public string EffectCondition { get; set; }

        //  덱 최대 포함 가능 매수 (리미트 레귤레이션)
        public int MaxDeckCount { get; set; }

        // --- 유닛 스탯 ---
        public string Id { get; set; } // 여기가 대문자 'I'인지 확인
        public CardType Type { get; set; }
        public int Power { get; set; }      // 공격력이자 체력
        public int AttackCost { get; set; } // arts_cost (공격 시 필요한 코스트)
        public int Prize { get; set; }      // 처치 시 줄 보상
        public string SkinResource { get; set; } // 이미지 경로

        // 이 리스트가 있어야 GameDataManager에서 Effects.Add(...)를 할 수 있습니다.
        public List<ICardEffect> Effects { get; set; } = new List<ICardEffect>();

        // [전투용 상태 변수]
        public bool IsExhausted { get; set; } = false; // 행동 완료(피로) 상태

        // 턴 시작 시 상태 초기화 (Player.cs에서 호출)
        public void RefreshUnitState()
        {
            IsExhausted = false;
        }

        // 불변 스탯(초기화용 원본 데이터)
        public int OriginalCost { get; set; } // 원래 코스트 기억

        public int OriginalPower { get; set; } // 원래 유닛 파워
        
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
                Power = OriginalPower;
            }
        }

        // 게임 시작 시 덱 세팅할 때 호출
        public void SetOwner(Player owner)
        {
            OriginalOwner = owner;
            Controller = owner;
        }

        // 상태 초기화 (묘지행, 바운스 등)
        public void ResetState()
        {
            // 1. 코스트 복구
            this.Cost = this.OriginalCost;

            // 2. 소유권 복구 (원래 주인에게 돌아감)
            this.Controller = this.OriginalOwner;

            // 추후 공격력/체력/상태이상 초기화 로직이 여기에 추가됨
            this.Power = this.OriginalPower;

        }

        public void AddEffect(ICardEffect effect)
        {
            Effects.Add(effect);
        }

        // 카드를 사용할 때 호출
        // 카드를 사용할 때 호출 (Action 콜백 추가)
        public void Play(GameContext context, Action onPlayComplete = null)
        {
            // 새 카드를 발동할 때 컨텍스트 변수 초기화
            context.ClearVariables();

            if (this.Type == CardType.Skill)
            {
                DebugHelper.LogSpell($"'{Name}' 발동 (보유 효과: {Effects.Count}개)");
                EventManager.OnLogMessage?.Invoke($"--- {Name} / {Cost} / {Description} ---\n");

                // ★ foreach 대신 순차 실행기 호출
                ExecuteEffectsSequentially(0, context, onPlayComplete);
            }
            else // 유닛일 경우: 소환 시 효과 사용
            {
                EventManager.OnLogMessage?.Invoke($"--- {Name} / {Power} / 효과 {Effects.Count}개 ---\n");

                if (Effects.Count > 0)
                {
                    bool canUseEffect = true;
                    if (!string.IsNullOrEmpty(EffectCondition) && EffectCondition.Trim().ToLower() != "none")
                    {
                        canUseEffect = Systems.ConditionEvaluator.Evaluate(EffectCondition, context);
                    }

                    if (canUseEffect)
                    {
                        EventManager.OnLogMessage?.Invoke($"    {Name}의 소환 시 효과 발동");
                        EventManager.OnLogMessage?.Invoke($"    {Name} : {Description}");

                        // ★ foreach 대신 순차 실행기 호출
                        ExecuteEffectsSequentially(0, context, onPlayComplete);
                    }
                    else
                    {
                        EventManager.OnLogMessage?.Invoke($"    (조건 미달로 {Name}의 효과는 발동하지 않습니다.)");
                        onPlayComplete?.Invoke(); // 효과 발동 안 해도 완료 보고는 필수
                    }
                }
                else
                {
                    onPlayComplete?.Invoke(); // 효과가 아예 없는 유닛도 완료 보고 필수
                }
            }
        }

        // ★ 효과를 1번부터 순서대로 끝날 때까지 기다리며 실행하는 릴레이 함수
        private void ExecuteEffectsSequentially(int index, GameContext context, Action onComplete)
        {
            // 모든 효과를 다 실행했다면 최종 완료 콜백 호출
            if (index >= Effects.Count)
            {
                onComplete?.Invoke();
                return;
            }

            // 현재 순서의 효과를 실행하고, 그 효과가 "나 끝났어!"라고 알려주면 다음 인덱스(+1)를 실행
            Effects[index].Execute(context, () =>
            {
                ExecuteEffectsSequentially(index + 1, context, onComplete);
            });
        }

        // [조건 판별] 이 카드를 지금 쓸 수 있는가?
        public bool IsPlayable(GameContext context)
        {
            // 1. 마나 부족 체크
            if (Controller.Mana < this.Cost) return false;

            // 2. 유닛 소환 공간 체크
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
                Power = this.Power,
                PlayCondition = this.PlayCondition,
                EffectCondition = this.EffectCondition, // 소환 시 효과 조건도 복사 목록에 포함
                Description = this.Description,
                Id = this.Id, // Id도 복사 필요
                OriginalCost = this.OriginalCost, // OriginalCost도 복사
                Type = this.Type,          // 이게 없으면 유닛으로 인식을 못함
                AttackCost = this.AttackCost,
                Prize = this.Prize,
                SkinResource = this.SkinResource,
                MaxDeckCount = this.MaxDeckCount
            };

            // 효과 리스트도 새로 만들어서 독립성 보장
            // 주의: Effect 객체 자체도 상태를 가진다면 Effect.Clone()이 필요
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

        /// <summary>
        /// 카드의 파워(체력/공격력)를 변경하는 유일한 파이프라인 메서드입니다.
        /// 전투 데미지, 스펠 버프, 턴 시작 회복 등 모든 변화는 여기를 거쳐야 합니다.
        /// </summary>
        /// <param name="amount">변화량 (+는 회복/버프, -는 데미지/디버프)</param>
        /// <param name="reason">변화 원인 (로그 및 디버깅 용도)</param>
        public void ModifyPower(int amount, string reason = "System")
        {
            if (amount == 0) return;

            this.Power += amount;

            // 중앙 집중화된 이벤트 송출 (여기서 UI 업데이트 이벤트도 쏠 수 있음)
            // {amount:+#;-#;0} 는 양수일 때 +, 음수일 때 - 기호를 자동으로 붙여주는 C# 포맷팅입니다.
            EventManager.OnLogMessage?.Invoke($"    ✨ [스탯 변경] {this.Name}의 Power {amount:+#;-#;0} 변동 -> {this.Power}) - 원인: {reason}");

            EventManager.OnCardPowerChanged?.Invoke(this, amount);
        }
    }
}
