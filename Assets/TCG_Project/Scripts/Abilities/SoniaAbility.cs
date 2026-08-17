// SoniaAbility.cs — Phase 14: SONI-01 소니아 고유 능력
using System;
using System.Linq;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

namespace TCG_Project.Scripts.Abilities
{
    public class SoniaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "SONI-01";
        
        public override async void OnSetPhase(Player me, GameContext ctx, Action<bool> onComplete)
        {
            var validCards = me.Graveyard.Where(c =>
                c.Type != CardType.Resource &&
                (c.CharacterId == "SONI" || c.CharacterId == "SONIA" || (c.DataId != null && c.DataId.StartsWith("SONI")))
            ).ToList();

            if (validCards.Count == 0) { onComplete?.Invoke(false); return; }

            EventManager.OnLogMessage?.Invoke($"[{me.Name}] 소니아 능력: 폐기존에서 소니아 카드 1장 패로 가져올 수 있음");

            // 1. 발동 여부 묻기
            bool shouldActivate = false;
            if (me.Type == UserType.Bot)
            {
                // ★ 봇 자동 발동 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {me.Name}: 소니아 능력(세트 전 묘지 회수) 자동 발동 결정");
                shouldActivate = true;
            }
            else
            {
                shouldActivate = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                    cb => EventManager.OnRequireOptionalAction?.Invoke(me, "소니아 능력을 발동하시겠습니까?", ctx, cb),
                    () => false,
                    GameLogicHelpers.GetChooseTimeoutMs(me)
                );
            }

            if (!shouldActivate)
            {
                onComplete?.Invoke(false);
                return;
            }

            // 2. 묘지에서 카드 고르기
            Card chosenCard = null;
            if (me.Type == UserType.Bot)
            {
                chosenCard = validCards.First();
                // ★ 봇 카드 선택 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {me.Name}: 묘지에서 '{chosenCard.Name}' 자동 선택");
            }
            else
            {
                var selectedList = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<System.Collections.Generic.List<Card>>(
                    cb => EventManager.OnRequireCardPick?.Invoke(me, validCards, 1, cb),
                    () => new System.Collections.Generic.List<Card> { validCards.OrderBy(c => Guid.NewGuid()).First() },
                    GameLogicHelpers.GetChooseTimeoutMs(me)
                );
                chosenCard = selectedList?.FirstOrDefault();
            }

            // 3. 실행
            if (chosenCard != null && validCards.Contains(chosenCard))
            {
                me.ExtractCard(ZoneType.Graveyard, chosenCard);
                me.InsertCard(ZoneType.Hand, chosenCard);
                // OnCardMove를 빼먹으면 보드의 폐기존 스택에 카드 GO가 유령으로 남고,
                // 엔진 리스트(SSOT)를 읽는 폐기존 패널과 화면이 어긋난다
                EventManager.OnCardMove?.Invoke(chosenCard, me, ZoneType.Graveyard, me, ZoneType.Hand);
                EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 소니아 능력 발동! 폐기존 '{chosenCard.Name}' → 패");
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
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Abilities
{
    public class SoniaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "SONI-01";

        // 이전 버전 코드(백업용)
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
                    me.InsertCard(ZoneType.Hand, chosenCard);
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
*/