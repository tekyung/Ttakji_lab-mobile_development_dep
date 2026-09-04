using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// PlayBuffer에 있는 첫 번째 카드의 코스트를 임시로 N만큼 감소시킨다.
    /// 원본 코스트는 카드의 OriginalCost 필드를 유지하므로 복원은 PlayFromBufferEffect가 담당한다.
    ///
    /// CompositeEffect Step 순서: MoveEffect(→PlayBuffer) → TemporaryCostEffect → PlayFromBufferEffect
    ///
    /// 파라미터:
    ///   reduction : int — 코스트 감소량 (기본 1)
    /// </summary>
    public class TemporaryCostEffect : ICardEffect
    {
        private int _reduction;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();
            _reduction = _cachedParams.ContainsKey("reduction")
                ? Convert.ToInt32(_cachedParams["reduction"])
                : 1;
            if (parameters.ContainsKey("requirePreviousSuccess"))
            {
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);
            }
            
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            Player owner = context.ActivePlayer;
            if (owner == null || owner.PlayBuffer.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke(
                    "  [임시코스트] PlayBuffer가 비어 있어 효과를 적용할 수 없습니다.");
                onComplete?.Invoke();
                return;
            }

            Card card = owner.PlayBuffer[0];
            int before = card.Cost;

            // ★ 전장 할인(GetEffectiveCost)을 여기서 반영하면 안 된다.
            //   지불 시점(PlayFromBufferEffect)에서 GetEffectiveCost를 다시 부르므로
            //   전장 할인이 **두 번** 적용된다. 여기서는 이 효과의 몫만 깎는다.
            card.Cost = Math.Max(0, card.Cost - _reduction);

            EventManager.OnLogMessage?.Invoke(
                $"  [임시코스트] '{card.Name}' 코스트 {before} -{_reduction} → {card.Cost} " +
                $"(전장 할인은 지불할 때 따로 적용된다)");

            onComplete?.Invoke();
        }

        public ICardEffect Clone()
        {
            var clone = new TemporaryCostEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
