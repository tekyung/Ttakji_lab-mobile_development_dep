// EllieAbility.cs — Phase 14: ELLI-01 엘리 고유 능력
using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Abilities
{
    public class EllieAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "ELLI-01";

        public override Card OnMainPhaseAfterAttack(Player owner, Card playedCard, Player enemy, GameContext context)
        {
            // 조건: 공격 카드 사용 후, 능력을 아직 안 썼을 때
            if (!CanUse(owner, context) || playedCard.Type != CardType.Attack)
                return null;

            // 패에서 다음 공격 카드 탐색 (발동한 카드보다 느리고, 코스트 지불 가능한 공격 카드)
            var nextAttack = owner.Hand.FirstOrDefault(c =>
                c.Type == CardType.Attack && c.Speed > playedCard.Speed && (c.Cost == 0 || owner.CanAfford(c.Cost)));

            if (nextAttack != null)
            {
                EventManager.OnLogMessage?.Invoke($"  ▶ [{owner.Name}] 엘리 능력 발동! [{nextAttack.Name}] 대기열 추가 (Speed: {nextAttack.Speed} / Cost: {nextAttack.Cost})");

                owner.ExtractCard(ZoneType.Hand, nextAttack); // 패에서 제거
                owner.MarkCharacterAbilityUsed(CharacterCardId);

                // ★ 큐에 넣을 카드를 ConsoleRunner에게 반환!
                return nextAttack;
            }
            return null;
        }
    }
}
