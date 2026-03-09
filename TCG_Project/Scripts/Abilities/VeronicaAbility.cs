// VeronicaAbility.cs — Phase 14: VERO-01 베로니카 고유 능력
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
