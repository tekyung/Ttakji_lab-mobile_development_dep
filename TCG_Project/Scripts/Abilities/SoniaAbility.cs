// SoniaAbility.cs — Phase 14: SONI-01 소니아 고유 능력
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Abilities
{
    public class SoniaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "SONI-01";
        
        // Gemini 버전
        public override void OnSetPhase(Player me, GameContext ctx, Action<bool> onComplete)
        {
            //var validCards = me.Graveyard.Where(c => c.CharacterId == "SONIA" && c.Type != CardType.Resource).ToList();
            var validCards = me.Graveyard.Where(c =>
                c.Type != CardType.Resource &&
                (
                    c.CharacterId == "SONI" ||
                    c.CharacterId == "SONIA" ||
                    (c.DataId != null && c.DataId.StartsWith("SONI"))
                )
            ).ToList();
            if (validCards.Count == 0) { onComplete?.Invoke(false); return; }

            EventManager.OnLogMessage?.Invoke($"[{me.Name}] 소니아 능력: 폐기존에서 소니아 카드 1장 패로 가져올 수 있음");

            Action<Card> doRecover = chosenCard =>
            {
                if (chosenCard != null && validCards.Contains(chosenCard))
                {
                    me.Graveyard.Remove(chosenCard);
                    me.Hand.Add(chosenCard);
                    EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 소니아 능력 발동! 폐기존 '{chosenCard.Name}' → 패");

                    me.MarkCharacterAbilityUsed(CharacterCardId); // 1회 사용 마킹
                    onComplete?.Invoke(true);
                }
                else
                {
                    onComplete?.Invoke(false);
                }
            };

            if (me.Type == UserType.Bot)
            {
                doRecover(validCards.First());
            }
            else
            {
                EventManager.OnRequireCardPick?.Invoke(me, validCards, 1, cards =>
                {
                    doRecover(cards?.FirstOrDefault());
                });
            }
        }
    }
}
