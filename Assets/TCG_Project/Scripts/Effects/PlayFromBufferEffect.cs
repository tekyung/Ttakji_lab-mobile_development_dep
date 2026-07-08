using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

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
            if (owner.PlayBuffer.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            Card bufferedCard = owner.PlayBuffer[0];

            // 1. 자아(Identity) 교체: 이펙트들이 자기 자신을 올바르게 참조하도록 변경
            Card originalPlayingCard = owner.PlayingCard;
            owner.PlayingCard = bufferedCard;

            EventManager.OnLogMessage?.Invoke($"  [버퍼발동] '{bufferedCard.Name}' (코스트 {bufferedCard.Cost}) 즉시 발동!");

            // 2. 카드 발동
            bufferedCard.Play(context, () =>
            {
                // 3. 카드 이동 분기 처리 (ConsoleRunner 교통정리와 100% 동일한 라우팅)
                owner.ExtractCard(ZoneType.PlayBuffer, bufferedCard);

                if (bufferedCard.IsStack)
                {
                    owner.AddToStackZone(bufferedCard);
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.StackZone);
                }
                else if (bufferedCard.IsBattlefield)
                {
                    owner.PlaceBattlefield(bufferedCard);
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.BattlefieldZone);

                    // ★ 전장 즉시 발동(Wake) 처리
                    GameLogicHelpers.ApplyBattlefieldTurnEffects(owner, context);
                }
                else if (owner.ResourceZone.Contains(bufferedCard))
                {
                    // SelfAsResource 효과 (보급 전달 등)
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.ResourceZone);
                }
                else
                {
                    owner.InsertCard(ZoneType.Graveyard, bufferedCard);
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.Graveyard);
                    EventManager.OnLogMessage?.Invoke($"  [버퍼발동] '{bufferedCard.Name}' 발동 완료 → 폐기존");
                }

                // 4. 자아(Identity) 복구: 다시 원래대로 돌아옴("기뢰" 등)
                owner.PlayingCard = originalPlayingCard;

                onComplete?.Invoke();
            }, isStackTrigger: false);
        }

        public ICardEffect Clone()
        {
            var clone = new PlayFromBufferEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}