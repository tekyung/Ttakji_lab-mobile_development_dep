using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

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

        /// <summary>
        /// async void라 여기서 예외가 나면 조용히 삼켜지고 onComplete가 영영 불리지 않는다.
        /// 그러면 호출한 쪽의 대기가 풀리지 않아 게임이 통째로 멈춘다. 반드시 감싼다.
        /// </summary>
        public async void Execute(GameContext context, Action onComplete)
        {
            try
            {
                await ExecuteInternal(context, onComplete);
            }
            catch (Exception e)
            {
                EventManager.OnLogMessage?.Invoke($"  ⚠️ [버퍼발동] 처리 중 오류: {e.Message}");
                onComplete?.Invoke();
            }
        }

        private async Task ExecuteInternal(GameContext context, Action onComplete)
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

            // ★ 상대의 스택 응답을 먼저 받는다.
            //   보통 경로(오픈 페이즈)에서는 대전 진행 코드가 카드 발동 직전에 이 단계를 넣는다.
            //   간접 발동은 그 코드를 거치지 않으므로 여기서 훅으로 위임한다.
            //   이게 없으면 상대가 깔아 둔 방어 스택이 무시된 채 데미지가 들어간다.
            // ★ 코스트를 실제로 낸다.
            //   예전에는 내지 않고 발동만 했다 — 후보 필터도 없어서 자원이 모자라도
            //   아무 카드나 골라 공짜로 쓸 수 있었다. 보통 경로(오픈 페이즈)와 같은 API를 쓴다.
            //   여기까지 왔는데 못 내면 발동을 취소하고 폐기존으로만 보낸다.
            int cost = GameLogicHelpers.GetEffectiveCost(bufferedCard, owner);
            if (cost > 0 && !owner.PayCost(cost))
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [버퍼발동] '{bufferedCard.Name}' 코스트 부족 (필요 {cost} / 자원 {owner.GetResourceCount()}) → 발동 취소");

                owner.ExtractCard(ZoneType.PlayBuffer, bufferedCard);
                owner.InsertCard(ZoneType.Graveyard, bufferedCard);
                EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.Graveyard);

                owner.PlayingCard = originalPlayingCard;
                onComplete?.Invoke();
                return;
            }

            await RequestStackResponse(context, owner, bufferedCard);

            // 2. 카드 발동
            bufferedCard.Play(context, () =>
            {
                // 3. 카드 이동 분기 처리 (ConsoleRunner 교통정리와 100% 동일한 라우팅)
                owner.ExtractCard(ZoneType.PlayBuffer, bufferedCard);

                // ★ 이동 이벤트만 쏘면 안 된다.
                //   화면은 "어디로 옮겨졌다"(OnCardMove)와 "무엇이 되었다"(OnCardStacked 등)를
                //   따로 받는다. 뒤엣것이 정렬·뒤집기·개수 표시를 맡는다.
                //   예전에는 여기서 OnCardMove만 쏴서, 기뢰로 발동한 스택 카드가
                //   엔진에는 스택으로 들어갔는데 화면 스택존에는 나타나지 않았다.
                //   BattleManager·ServerGameManager의 라우팅과 같은 짝을 맞춘다.
                if (bufferedCard.IsStack)
                {
                    owner.AddToStackZone(bufferedCard);
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.StackZone);
                    EventManager.OnCardStacked?.Invoke(bufferedCard, owner);
                }
                else if (bufferedCard.IsBattlefield)
                {
                    owner.PlaceBattlefield(bufferedCard);
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.BattlefieldZone);
                    EventManager.OnCardBattlefield?.Invoke(bufferedCard, owner);

                    // ★ 전장 즉시 발동(Wake) 처리
                    GameLogicHelpers.ApplyBattlefieldTurnEffects(owner, context);
                }
                else if (owner.ResourceZone.Contains(bufferedCard))
                {
                    // SelfAsResource 효과 (보급 전달 등)
                    EventManager.OnCardMove?.Invoke(bufferedCard, owner, ZoneType.PlayBuffer, owner, ZoneType.ResourceZone);
                    EventManager.OnCardResourceAdded?.Invoke(bufferedCard, owner);
                    EventManager.OnResourceChange?.Invoke(owner, owner.GetResourceCount());
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

        /// <summary>
        /// 간접 발동한 카드에 대해 상대의 스택 응답 단계를 돌려 달라고 요청하고 기다린다.
        ///
        /// 구독자가 없으면(단위 테스트 등) 그냥 지나간다 —
        /// 응답 단계가 없다고 카드 발동 자체가 막히면 안 된다.
        /// 응답이 오지 않아도 게임이 멈추지 않도록 제한 시간을 둔다.
        /// </summary>
        private static async Task RequestStackResponse(GameContext context, Player owner, Card played)
        {
            var hook = EventManager.OnRequireIndirectStackResponse;
            if (hook == null) return;

            Player stackOwner = null;
            foreach (Player p in context.Players)
            {
                if (p != null && p != owner) { stackOwner = p; break; }
            }

            if (stackOwner == null) return;
            if (stackOwner.StackZone == null || stackOwner.StackZone.Count == 0) return;

            await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                done => hook.Invoke(stackOwner, owner, played, done),
                () => false,
                GameLogicHelpers.GetChooseTimeoutMs(stackOwner));
        }

        public ICardEffect Clone()
        {
            var clone = new PlayFromBufferEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}