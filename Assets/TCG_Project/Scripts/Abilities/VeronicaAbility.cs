// VeronicaAbility.cs — Phase 14: VERO-01 베로니카 고유 능력
using System;
using System.Linq;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

namespace TCG_Project.Scripts.Abilities
{
    public class VeronicaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "VERO-01";

        public override bool ShouldReplaceDraw(Player me)
            => me.HasCharacter("VERO-01") && me.Deck.Count > 0;

        // async void (또는 async Task)로 변경하여 내부에서 비동기 대기 지원
        public override async void OnDrawPhase(Player me, GameContext ctx, Action<bool> onComplete)
        {
            int peekCount = Math.Min(3, me.Deck.Count);
            if (peekCount == 0) { onComplete?.Invoke(false); return; }

            var peeked = me.Deck.Take(peekCount).ToList();
            EventManager.OnLogMessage?.Invoke($"[{me.Name}] 베로니카 능력: 덱 탑 {peekCount}장 공개 → 1장 선택");
            EventManager.OnLogMessage?.Invoke($"  공개 카드: {string.Join(", ", peeked.Select(c => c.Name))}");

            // 1. 발동 여부 묻기 (타임아웃 시 false)
            bool shouldActivate = false;
            if (me.Type == UserType.Bot)
            {
                // ★ 봇 자동 발동 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {me.Name}: 베로니카 능력(드로우 대체) 자동 발동 결정");
                shouldActivate = true; // 봇은 무조건 발동
            }
            else
            {
                shouldActivate = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                    cb => EventManager.OnRequireOptionalAction?.Invoke(me, "베로니카 능력을 발동하시겠습니까?", ctx, cb),
                    () => false,
                    GameRules.ChooseWaitTime
                );
            }

            if (!shouldActivate)
            {
                onComplete?.Invoke(false);
                return;
            }

            // 2. 카드 선택 묻기 (타임아웃 시 무작위 선택)
            Card chosenCard = null;
            if (me.Type == UserType.Bot)
            {
                chosenCard = peeked.First();
                // ★ 봇 카드 선택 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {me.Name}: 확인한 3장 중 '{chosenCard.Name}' 자동 선택");
            }
            else
            {
                var selectedList = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<System.Collections.Generic.List<Card>>(
                    cb => EventManager.OnRequireCardPick?.Invoke(me, peeked, 1, cb),
                    () => new System.Collections.Generic.List<Card> { peeked.OrderBy(c => Guid.NewGuid()).First() }, // 타임아웃 무작위
                    GameRules.ChooseWaitTime
                );
                chosenCard = selectedList?.FirstOrDefault();
            }

            // 3. 실제 적용
            if (chosenCard != null && peeked.Contains(chosenCard))
            {
                me.Deck.Remove(chosenCard);
                me.Hand.Add(chosenCard);
                EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 베로니카 능력 발동! '{chosenCard.Name}' 패로 추가.");
                me.MarkCharacterAbilityUsed(CharacterCardId);
                onComplete?.Invoke(true); 
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }
    }
}
/*
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Abilities
{
    public class VeronicaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "VERO-01";

        public override bool ShouldReplaceDraw(Player me)
            => me.HasCharacter("VERO-01") && me.Deck.Count > 0;

        public override void ExecuteDrawPhase(Player me, GameContext ctx, Card chosenCard)
        {
            if (chosenCard == null || !me.Deck.Contains(chosenCard)) return;

            me.Deck.Remove(chosenCard);
            me.Hand.Add(chosenCard);
            EventManager.OnLogMessage?.Invoke(
                $"  [{me.Name}] '{chosenCard.Name}' → 패. 나머지 덱 위 유지.");
        }

        // Gemini 버전
        public override void OnDrawPhase(Player me, GameContext ctx, Action<bool> onComplete)
        {
            int peekCount = Math.Min(3, me.Deck.Count);
            if (peekCount == 0) { onComplete?.Invoke(false); return; }

            var peeked = me.Deck.Take(peekCount).ToList();
            EventManager.OnLogMessage?.Invoke($"[{me.Name}] 베로니카 능력: 덱 탑 {peekCount}장 공개 → 1장 선택");
            EventManager.OnLogMessage?.Invoke($"  공개 카드: {string.Join(", ", peeked.Select(c => c.Name))}");

            Action<Card> doDraw = chosenCard =>
            {
                if (chosenCard != null && peeked.Contains(chosenCard))
                {
                    me.Deck.Remove(chosenCard);
                    me.Hand.Add(chosenCard);
                    EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 베로니카 능력 발동! '{chosenCard.Name}' 패로 추가. (나머지 덱 위 유지)");

                    me.MarkCharacterAbilityUsed(CharacterCardId); // 1회 사용 마킹
                    onComplete?.Invoke(true); // 능력이 기본 드로우를 대체함
                }
                else
                {
                    onComplete?.Invoke(false); // 취소됨 (기본 드로우를 수행해야 함)
                }
            };

            if (me.Type == UserType.Bot)
            {
                doDraw(peeked.First());
            }
            else
            {
                EventManager.OnRequireCardPick?.Invoke(me, peeked, 1, cards =>
                {
                    doDraw(cards?.FirstOrDefault());
                });
            }
        }
    }
}
*/