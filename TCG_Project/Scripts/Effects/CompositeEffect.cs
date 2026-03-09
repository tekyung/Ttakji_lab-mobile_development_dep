using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 복합 효과: 여러 ICardEffect 를 순서대로 순차 실행한다.
    /// 각 효과는 onComplete 콜백 체인으로 연결되므로 비동기 효과도 올바르게 처리된다.
    ///
    /// GameDataManager에서 Steps.Add() 로 구성한다.
    /// 예) SONI-10 신재생에너지 = MoveEffect(ResourceFromDiscard) + MoveEffect(ReturnFromDiscard)
    /// </summary>
    public class CompositeEffect : ICardEffect
    {
        public List<ICardEffect> Steps { get; } = new List<ICardEffect>();

        private Dictionary<string, object> _cachedParams;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();
            
            // 스택 효과 여부 지정 (JSON에서 "isStackAction": true/false 로 설정 가능, 기본값은 false)
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }
        }

        /// <summary>Steps에 효과를 추가한다. GameDataManager에서 빌드 시 사용.</summary>
        public void AddStep(ICardEffect effect)
        {
            if (effect != null) Steps.Add(effect);
        }

        public void Execute(GameContext context, Action onComplete)
            => ExecuteStep(0, context, onComplete);

        private void ExecuteStep(int idx, GameContext context, Action onComplete)
        {
            if (idx >= Steps.Count || context.IsGameOver)
            {
                onComplete?.Invoke();
                return;
            }
            // Phase 18: "그 후" 시맨틱 — 선행 Step 실패 시 후속 Step 스킵
            if (idx > 0 && !context.LastEffectSucceeded)
            {
                onComplete?.Invoke();
                return;
            }
            context.LastEffectSucceeded = true; // 이번 Step 실행 전 초기화
            Steps[idx].Execute(context, () => ExecuteStep(idx + 1, context, onComplete));
        }

        public ICardEffect Clone()
        {
            var clone = new CompositeEffect();
            clone.Initialize(_cachedParams);
            foreach (var step in Steps)
                clone.Steps.Add(step.Clone());
            return clone;
        }
    }
}
