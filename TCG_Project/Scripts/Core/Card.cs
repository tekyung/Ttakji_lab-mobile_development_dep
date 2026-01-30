using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    public class Card
    {
        // 게임 중 변할 수 있는 고유 ID (인스턴스 식별용)
        public string InstanceId { get; private set; }

        // 원본 데이터 ID (어떤 종류의 카드인가?)
        public string DataId { get; set; }

        public string Name { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }
        public string PlayCondition { get; set; } // 카드의 발동 조건

        // 카드는 여러 개의 효과를 가질 수 있습니다.
        private List<ICardEffect> effects = new List<ICardEffect>();

        public void AddEffect(ICardEffect effect)
        {
            effects.Add(effect);
        }

        // 카드를 사용할 때 호출
        public void Play(GameContext context)
        {
            Console.WriteLine($"--- {Name} / {Cost} / {Description} ---\n");
            foreach (var effect in effects)
            {
                effect.Execute(context);
            }
            Console.WriteLine("---------------------------------------------\n");
        }

        // [조건 판별] 이 카드를 지금 쓸 수 있는가?
        public bool IsPlayable(GameContext context)
        {
            // 1. 마나 코스트 확인 (기본 조건)
            if (context.ActivePlayer.Mana < this.Cost)
                return false;

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
            foreach (var effect in this.effects)
            {
                newCard.AddEffect(effect);
            }

            return newCard;
        }

        // Card 클래스 내부에 추가
        public bool HasEffectType(string typeName)
        {
            // 효과 리스트를 순회하며 타입 이름이 포함되는지 검사
            foreach (var effect in effects)
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
