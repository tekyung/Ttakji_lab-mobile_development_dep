// VeronicaAbility.cs — Phase 14: VERO-01 베로니카 고유 능력
using System;
using System.Collections.Generic;
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
                    GameLogicHelpers.GetChooseTimeoutMs(me)
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
                var selectedList = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<List<Card>>(
                    cb => EventManager.OnRequireCardPick?.Invoke(
                        me, peeked, 1, new CardPickPrompt("패로 가져올 카드 1장을 고르세요."), cb),
                    () => new List<Card> { peeked.OrderBy(c => Guid.NewGuid()).First() }, // 타임아웃 무작위
                    GameLogicHelpers.GetChooseTimeoutMs(me)
                );
                chosenCard = selectedList?.FirstOrDefault();
            }

            // 3. 고른 카드를 패로
            if (chosenCard == null || !peeked.Contains(chosenCard))
            {
                onComplete?.Invoke(false);
                return;
            }

            me.ExtractCard(ZoneType.Deck, chosenCard);
            me.InsertCard(ZoneType.Hand, chosenCard);
            EventManager.OnCardMove?.Invoke(chosenCard, me, ZoneType.Deck, me, ZoneType.Hand);
            EventManager.OnCardDraw?.Invoke(chosenCard, me, ZoneType.Deck);
            EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 베로니카 능력 발동! '{chosenCard.Name}' 패로 추가.");

            // ★ 되돌리기보다 **먼저** 사용 기록을 남긴다.
            //   뒤 단계에서 무슨 일이 생겨도 능력이 다시 발동되면 안 된다.
            //   (예전에 능력이 조용히 실패해 이 호출을 건너뛰는 바람에 매 턴 재발동한 적이 있다)
            me.MarkCharacterAbilityUsed(CharacterCardId);

            // 4. 남은 카드를 덱 맨 위/아래 중 원하는 쪽으로, 원하는 순서로 되돌린다.
            await ReturnRemainingCards(me, ctx, peeked.Where(c => c != chosenCard).ToList());

            onComplete?.Invoke(true);
        }

        /// <summary>
        /// 남은 공개 카드를 덱 맨 위/맨 아래 중 한쪽으로, 플레이어가 정한 순서대로 되돌린다.
        ///
        /// 순서의 뜻은 하나뿐이다 — <b>먼저 고른 카드를 덱에서 먼저 만난다.</b>
        /// 맨 위로 보내면 첫 카드가 덱 맨 위(다음 드로우)이고,
        /// 맨 아래로 보내면 첫 카드가 둘 중 위쪽(= 둘 중 먼저 뽑히는 쪽)이다.
        /// </summary>
        private static async Task ReturnRemainingCards(Player me, GameContext ctx, List<Card> remaining)
        {
            if (remaining == null || remaining.Count == 0) return;

            // 기본값은 지금까지의 동작 그대로 — 덱 맨 위, 원래 순서.
            // 봇도, 응답이 없을 때도 이 값을 쓴다. (카드는 이미 패로 갔으니 절대 중단하지 않는다)
            bool toTop = true;
            List<Card> ordered = remaining;

            if (me.Type == UserType.Bot)
            {
                EventManager.OnLogMessage?.Invoke(
                    $" 🤖 [Bot AI] {me.Name}: 나머지 {remaining.Count}장을 덱 맨 위에 원래 순서로 되돌림");
            }
            else
            {
                // 4-1. 위인가 아래인가
                toTop = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                    cb => EventManager.OnRequireOptionalAction?.Invoke(
                        me, $"나머지 {remaining.Count}장을 덱 맨 위로 되돌릴까요? (아니오 = 맨 아래)", ctx, cb),
                    () => true,
                    GameLogicHelpers.GetChooseTimeoutMs(me)
                );

                // 4-2. 순서 — 2장 이상일 때만 물어볼 의미가 있다
                if (remaining.Count > 1)
                {
                    string where = toTop ? "맨 위" : "맨 아래";
                    var picked = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<List<Card>>(
                        cb => EventManager.OnRequireCardPick?.Invoke(
                            me, remaining, remaining.Count,
                            // ★ Ordered: true — 고르는 순서가 결과를 바꾸므로 UI가 1·2 번호를 띄운다.
                            new CardPickPrompt(
                                $"덱 {where}로 되돌립니다. 먼저 뽑고 싶은 순서대로 고르세요.", ordered: true), cb),
                        () => remaining,
                        GameLogicHelpers.GetChooseTimeoutMs(me)
                    );

                    ordered = NormalizeOrder(picked, remaining);
                }
            }

            // 덱에서 빼낸 뒤 원하는 위치에 다시 넣는다.
            foreach (var card in ordered) me.ExtractCard(ZoneType.Deck, card);

            for (int i = 0; i < ordered.Count; i++)
            {
                // 맨 위:   0, 1, ... 순으로 꽂으면 첫 카드가 다음 드로우가 된다.
                // 맨 아래: 순서대로 뒤에 붙이면 첫 카드가 둘 중 위쪽 = 먼저 뽑힌다.
                int index = toTop ? i : me.Deck.Count;
                me.InsertCard(ZoneType.Deck, ordered[i], index);
            }

            // ★ 카드 이름은 남기지 않는다. 이 로그는 상대에게도 보이므로
            //   "덱 맨 위에 무엇이 있는지"가 새어 나가면 능력의 의미가 사라진다.
            EventManager.OnLogMessage?.Invoke(
                $"  ▶ [{me.Name}] 나머지 {ordered.Count}장을 덱 {(toTop ? "맨 위" : "맨 아래")}로 되돌림.");
        }

        /// <summary>
        /// 응답이 원래 후보와 같은 구성인지 확인하고, 어긋나면 원래 순서로 되돌린다.
        /// 서버 검증(<c>ValidateOnRequireCardPick</c>)은 **개수 상한만** 보므로 여기서 한 번 더 본다.
        /// </summary>
        private static List<Card> NormalizeOrder(List<Card> picked, List<Card> original)
        {
            if (picked == null) return original;

            var ordered = picked.Where(c => c != null && original.Contains(c)).Distinct().ToList();
            if (ordered.Count != original.Count)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  ⚠ 되돌릴 순서 응답이 온전하지 않습니다 ({ordered.Count}/{original.Count}). 원래 순서로 되돌립니다.");
                return original;
            }
            return ordered;
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