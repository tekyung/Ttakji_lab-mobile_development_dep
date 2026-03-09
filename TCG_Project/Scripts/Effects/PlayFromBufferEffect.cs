using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// PlayBuffer의 첫 번째 카드를 코스트 지불 후 즉시 발동하고,
    /// 발동 완료 후 OriginalCost를 복원한 뒤 폐기존으로 이동한다.
    ///
    /// CompositeEffect Step 순서: MoveEffect(→PlayBuffer) → TemporaryCostEffect → PlayFromBufferEffect
    ///
    /// 코스트 지불 불가 시 카드를 폐기존에만 이동하고 효과는 취소한다.
    /// </summary>
    public class PlayFromBufferEffect : ICardEffect
    {
        private Dictionary<string, object> _cachedParams;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();

            if (parameters.TryGetValue("requirePreviousSuccess", out var requirePrevObj))
            {
                RequirePreviousSuccess = Convert.ToBoolean(requirePrevObj.ToString());
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
                    "  [버퍼발동] PlayBuffer가 비어 있어 발동할 카드가 없습니다.");
                onComplete?.Invoke();
                return;
            }

            Card card = owner.PlayBuffer[0];
            owner.PlayBuffer.RemoveAt(0);

            // 코스트 지불
            if (card.Cost > 0 && !owner.PayCost(card.Cost))
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [버퍼발동] '{card.Name}' 코스트 부족 → 발동 취소, 폐기존으로 이동");
                card.Cost = card.OriginalCost;
                owner.Graveyard.Add(card);
                onComplete?.Invoke();
                return;
            }

            EventManager.OnLogMessage?.Invoke(
                $"  [버퍼발동] '{card.Name}' (코스트 {card.Cost}) 즉시 발동!");

            card.Play(context, () =>
            {
                // 코스트 복원 후 폐기존으로
                card.Cost = card.OriginalCost;
                owner.Graveyard.Add(card);
                EventManager.OnLogMessage?.Invoke(
                    $"  [버퍼발동] '{card.Name}' 발동 완료 → 폐기존");
                onComplete?.Invoke();
            });
        }

        public ICardEffect Clone()
        {
            var clone = new PlayFromBufferEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
