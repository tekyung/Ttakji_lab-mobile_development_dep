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
        public string SkinResource { get; set; } // 이미지 경로

        // 이 리스트가 있어야 GameDataManager에서 Effects.Add(...)를 할 수 있습니다.
        public List<ICardEffect> Effects { get; set; } = new List<ICardEffect>();

        // [룰북] 카드 스피드 (1=빠름, 2, 3=느림). 메인 페이즈 해결 순서를 결정.
        public CardSpeed Speed { get; set; } = CardSpeed.None;

        // [룰북] 카드 소속 캐릭터 ID (예: "ELLIE", "VERONICA", "DAINA", "SONIA")
        public string CharacterId { get; set; }

        // [룰북] 스택 카드 여부. true이면 효과 발동 후 폐기존 대신 스택존으로 이동.
        public bool IsStack { get; set; } = false;
        public bool IsBattlefield { get; set; } = false; // 전장 카드 여부 (전장 효과 처리용)

        // 불변 스탯(초기화용 원본 데이터)
        public int OriginalCost { get; set; } // 원래 코스트 기억

        // 원래 주인 (게임 시작 시 덱의 주인, 불변)
        public Player OriginalOwner { get; set; }

        // 현재 컨트롤러 (누구 필드/패에 있는가, 가변)
        public Player Controller { get; set; }

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
        }

        public void AddEffect(ICardEffect effect)
        {
            Effects.Add(effect);
        }

        // 카드를 사용할 때 호출 (Action 콜백 추가)
        public void Play(GameContext context, Action onPlayComplete = null, bool isStackTrigger = false)
        {
            // 새 카드를 발동할 때 컨텍스트 변수 초기화
            context.ClearVariables();
            // 스택 트리거 여부에 따라 로그 분리
            string triggerType = isStackTrigger ? "[스택 발동]" : "[오픈 즉발]"; // 로그 메시지에 트리거 유형 명시
            EventManager.OnLogMessage?.Invoke($"--- {triggerType} {Name} / Cost:{Cost} / {Description} ---");

            ExecuteEffectsSequentially(0, context, onPlayComplete, isStackTrigger);
        }

        // 카드를 사용할 때 호출 (Action 콜백 추가)
        public void Play(GameContext context, Action onPlayComplete = null)
        {
            bool isStackTrigger = false; // 스택 트리거 여부를 명시적으로 false로 설정
            // 새 카드를 발동할 때 컨텍스트 변수 초기화
            context.ClearVariables();
            // 스택 트리거 여부에 따라 로그 분리
            string triggerType = "[오픈 즉발]"; // 로그 메시지에 트리거 유형 명시
            EventManager.OnLogMessage?.Invoke($"--- {triggerType} {Name} / Cost:{Cost} / {Description} ---");

            ExecuteEffectsSequentially(0, context, onPlayComplete, isStackTrigger);
        }

        // ★ 효과를 1번부터 순서대로 끝날 때까지 기다리며 실행하는 릴레이 함수
        private void ExecuteEffectsSequentially(int index, GameContext context, Action onComplete, bool isStackTrigger)
        {
            // 1. 종료 조건 (리스트 범위를 벗어났는지) 검사를 ★가장 먼저★ 수행해야 합니다!
            if (index >= Effects.Count)
            {
                onComplete?.Invoke();
                return;
            }

            var currentEffect = Effects[index];

            // 2. "그 후" 시맨틱 검사 (이전 효과가 실패했는지 확인)
            if (index > 0 && currentEffect.RequirePreviousSuccess && !context.LastEffectSucceeded)
            {
                EventManager.OnLogMessage?.Invoke($"    🚫 [효과 중단] 이전 조건을 만족하지 못해 이후 효과가 취소됩니다.");
                onComplete?.Invoke(); // 취소 후 종료
                return;
            }

            // 3. 타이밍 필터링: 스택 카드의 경우 스택용 효과와 오픈 즉발용 효과를 구분
            if (this.IsStack)
            {
                if (currentEffect.IsStackAction != isStackTrigger)
                {
                    // 타이밍이 맞지 않으면 이 효과를 스킵하고 다음 인덱스로 즉시 넘어감
                    ExecuteEffectsSequentially(index + 1, context, onComplete, isStackTrigger);
                    return;
                }
            }

            // 4. 현재 이펙트 실행 및 콜백 체인 연결
            currentEffect.Execute(context, () =>
            {
                ExecuteEffectsSequentially(index + 1, context, onComplete, isStackTrigger);
            });
        }

        // [조건 판별] 이 카드를 지금 쓸 수 있는가?
        public bool IsPlayable(GameContext context)
        {
            // 자원존 기반 코스트 체크
            if (Controller != null && !Controller.CanAfford(this.Cost)) return false;

            // 커스텀 발동 조건 확인
            if (!string.IsNullOrEmpty(PlayCondition))
            {
                return Systems.ConditionEvaluator.Evaluate(PlayCondition, context);
            }

            return true;
        }

        // 깊은 복사 메서드
        public Card Clone()
        {
            Card newCard = new Card
            {
                InstanceId = System.Guid.NewGuid().ToString(), // 고유 ID 발급
                DataId = this.DataId,
                Name = this.Name,
                Cost = this.Cost,
                Description = this.Description,
                Id = this.Id,
                OriginalCost = this.OriginalCost,
                Type = this.Type,
                Speed = this.Speed,
                SkinResource = this.SkinResource,
                CharacterId = this.CharacterId,
                IsStack = this.IsStack,
                IsBattlefield = this.IsBattlefield
            };

            // ★ 효과 리스트 깊은 복사(Deep Copy) 적용
            foreach (var effect in this.Effects)
            {
                // 다형성을 활용하여 각각의 효과가 스스로를 복제하게 만듦
                newCard.AddEffect(effect.Clone());
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
        /// 카드의 코스트, 스피드를 변경하는 유일한 파이프라인 메서드입니다.
        /// </summary>
        /// <param name="amount">변화량 (+는 회복/버프, -는 데미지/디버프)</param>
        /// <param name="reason">변화 원인 (로그 및 디버깅 용도)</param>
        public void ModifyCost(int amount, string reason = "System")
        {
            if (amount == 0) return;
            this.Cost += amount;
            // 중앙 집중화된 이벤트 송출 (여기서 UI 업데이트 이벤트도 쏠 수 있음)
            // {amount:+#;-#;0} 는 양수일 때 +, 음수일 때 - 기호를 자동으로 붙여주는 C# 포맷팅입니다.
            EventManager.OnLogMessage?.Invoke($"    ✨ [스탯 변경] {this.Name}의 Cost {amount:+#;-#;0} 변동 -> {this.Cost}) - 원인: {reason}");
        }
        public void ModifySpeed(int amount, string reason = "System")
        {
            if (amount == 0) return;
            this.Speed -= amount;
            EventManager.OnLogMessage?.Invoke($"    ✨ [스탯 변경] {this.Name}의 Speed {amount:+#;-#;0} 변동 -> {this.Speed}) - 원인: {reason}");
        }
    }
}
