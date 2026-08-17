// EllieAbility.cs — Phase 14: ELLI-01 엘리 고유 능력
using System;
using System.Linq;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

namespace TCG_Project.Scripts.Abilities
{
    public class EllieAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "ELLI-01";

        // ★ 변경점: return Card 대신 콜백(Action<Card>)을 사용하여 비동기 대기를 지원합니다.
        public override async void OnMainPhaseAfterAttack(Player owner, Card playedCard, Player enemy, GameContext context, Action<Card> onComplete)
        {
            // 1. 기본 조건 검사
            if (!CanUse(owner, context) || playedCard.Type != CardType.Attack || playedCard.CharacterId != "ELLIE")
            {
                onComplete?.Invoke(null);
                return;
            }

            // 2. 유효한 카드 필터링 (FirstOrDefault 대신 List로 추출)
            var validCards = owner.Hand.Where(c =>
                c.Type == CardType.Attack &&
                c.Speed > playedCard.Speed &&
                (c.Cost == 0 || owner.CanAfford(c.Cost))).ToList();

            if (validCards.Count == 0)
            {
                onComplete?.Invoke(null);
                return;
            }

            // 3. [추가됨] 1단계: 발동 여부 묻기 (타임아웃 적용)
            bool shouldActivate = false;
            if (owner.Type == UserType.Bot)
            {
                // ★ 봇 자동 발동 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {owner.Name}: 엘리 능력(후속 타격) 자동 발동 결정");
                shouldActivate = true; // 봇은 조건이 맞으면 무조건 후속타 발동
            }
            else
            {
                shouldActivate = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                    cb => EventManager.OnRequireOptionalAction?.Invoke(owner, "엘리 능력을 발동하여 후속 공격을 이어가시겠습니까?", context, cb),
                    () => false, // 시간 초과 시 발동 취소
                    GameLogicHelpers.GetChooseTimeoutMs(owner)
                );
            }

            if (!shouldActivate)
            {
                onComplete?.Invoke(null);
                return;
            }

            // 4. [추가됨] 2단계: 발동할 카드 선택 (타임아웃 적용)
            Card chosenCard = null;
            if (owner.Type == UserType.Bot)
            {
                // 봇은 가능한 카드 중 랜덤으로 하나 선택
                chosenCard = validCards.OrderBy(c => Guid.NewGuid()).FirstOrDefault();
                // ★ 봇 카드 선택 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {owner.Name}: 패에서 '{chosenCard?.Name}' 자동 선택");
            }
            else
            {
                var selectedList = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<System.Collections.Generic.List<Card>>(
                    cb => EventManager.OnRequireCardPick?.Invoke(owner, validCards, 1, cb),
                    () => new System.Collections.Generic.List<Card> { validCards.OrderBy(c => Guid.NewGuid()).First() }, // 시간 초과 시 무작위 강제 선택
                    GameLogicHelpers.GetChooseTimeoutMs(owner)
                );
                chosenCard = selectedList?.FirstOrDefault();
            }

            // 5. 최종 집행 (이미 완성되어 있던 존 이동 및 마킹 로직)
            if (chosenCard != null && validCards.Contains(chosenCard))
            {
                EventManager.OnLogMessage?.Invoke($"  ▶ [{owner.Name}] 엘리 능력 발동! [{chosenCard.Name}] 대기열 추가 (Speed: {chosenCard.Speed} / Cost: {chosenCard.Cost})");

                owner.ExtractCard(ZoneType.Hand, chosenCard);
                owner.MarkCharacterAbilityUsed(CharacterCardId);

                onComplete?.Invoke(chosenCard); // 선택된 카드를 메인 페이즈 큐에 넣도록 반환
            }
            else
            {
                onComplete?.Invoke(null);
            }
        }
    }
}
