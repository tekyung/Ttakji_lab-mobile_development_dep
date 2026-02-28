using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects
{
    public class ConditionalEffect : ICardEffect
    {
        private ICardCondition condition;
        private ICardEffect successEffect;
        private ICardEffect failEffect; // 실패 시 실행할 효과
        
        // 복제를 위해 초기화 파라미터를 기억해 둡니다.
        private Dictionary<string, object> _cachedParams;

        // ★ JSON 파싱이 아니라, 이미 완성된 객체를 딕셔너리에서 바로 꺼내 옵니다.
        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters;

            if (parameters.TryGetValue("condition", out object condObj))
                condition = condObj as ICardCondition;

            if (parameters.TryGetValue("successEffect", out object successObj))
                successEffect = successObj as ICardEffect;

            if (parameters.TryGetValue("failEffect", out object failObj))
                failEffect = failObj as ICardEffect;
        }

        // ★ 깊은 복사 구현체
        public ICardEffect Clone()
        {
            var clone = new ConditionalEffect();
            // 내부 효과들도 각각 깊은 복사가 필요합니다.
            var clonedParams = new Dictionary<string, object>(_cachedParams);

            if (successEffect != null) clonedParams["successEffect"] = successEffect.Clone();
            if (failEffect != null) clonedParams["failEffect"] = failEffect.Clone();
            if (condition != null) clonedParams["condition"] = condition; // Condition도 필요하다면 Clone 구현 권장

            clone.Initialize(clonedParams);
            return clone;
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 1. 조건식이 아예 없다면 무조건 성공(True)으로 간주
            bool isMet = (condition == null) || condition.IsMet(context);

            // 성공 효과가 등록되어 있으면 실행하고, 끝나면 onComplete를 넘겨서 호출하게 함
            if (isMet)
            {
                if (successEffect != null) successEffect.Execute(context, onComplete);
                else onComplete?.Invoke(); // 등록된 효과가 없으면 즉시 콜백 반환 (흐름이 멈추지 않도록 방어)
            }
            // 실패 효과가 등록되어 있으면 실행하고, 끝나면 onComplete를 넘겨서 호출하게 함
            else
            {
                if (failEffect != null) failEffect.Execute(context, onComplete);
                else onComplete?.Invoke();
            }
        }
    }
}