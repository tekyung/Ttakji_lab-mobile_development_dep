// Scripts/Systems/BotBrain.cs — Phase 8 구현: 6페이즈 AI 전략
using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// AI(봇) 플레이어의 행동 결정 구현체.
    /// 모든 결정은 즉시 동기적으로 반환된다.
    /// </summary>
    public class BotBrain : IPlayerBrain
    {
        private readonly Player _me;
        public BotBrain(Player player) { _me = player; }

        // ─── 6페이즈 결정 ─────────────────────────────────────────────

        /// <summary>
        /// 봇 세트 전략: Speed가 낮은(빠른) 카드 우선, 같으면 Defense > Attack > Support.
        /// 룰북 타입(Attack/Defense/Support) 카드를 우선하고, 없으면 패의 첫 번째 카드.
        /// </summary>
        public Card ChooseSetCard(Player me, GameContext ctx)
        {
            if (me.Hand.Count == 0) return null;

            var candidate = me.Hand
                .Where(c => c.Type == CardType.Attack || c.Type == CardType.Defense || c.Type == CardType.Support)
                .OrderBy(c => c.Speed == CardSpeed.None ? 999 : (int)c.Speed)
                .ThenBy(c => c.Type == CardType.Defense ? 0 : c.Type == CardType.Attack ? 1 : 2)
                .FirstOrDefault();

            return candidate ?? me.Hand.First();
        }

        /// <summary>
        /// 봇 오픈 전략: effectiveCost(전장 감소 반영)를 지불 가능하면 공개, 불가능하면 폐기.
        /// </summary>
        public OpenPhaseChoice ChooseOpenOrAbandon(Player me, Card setCard, int effectiveCost, GameContext ctx)
        {
            bool canAfford = effectiveCost == 0 || me.CanAfford(effectiveCost);
            if (canAfford)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"{me.Name}: 코스트 충분 (필요: {effectiveCost} / 자원존: {me.GetResourceCount()}) → 공개");
                return OpenPhaseChoice.Open;
            }

            EventManager.OnLogMessage?.Invoke(
                $"{me.Name}: 코스트 부족 (필요: {effectiveCost} / 자원존: {me.GetResourceCount()}) → 폐기");
            return OpenPhaseChoice.Abandon;
        }

        /// <summary>
        /// 봇 스택 전략: 상대가 Attack/Support 카드를 낼 때 방어(Defense) 스택 자동 발동.
        /// </summary>
        public bool ChooseStackActivation(Player me, Card stackCard, Card opponentCard, GameContext ctx)
        {
            if (stackCard.Type == CardType.Defense &&
                (opponentCard.Type == CardType.Attack || opponentCard.Type == CardType.Support))
            {
                EventManager.OnLogMessage?.Invoke(
                    $"{me.Name}: [{stackCard.Name}] 스택 자동 발동! ← 상대: [{opponentCard.Name}]");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 봇 카드 선택: filter 조건에 맞는 카드 중 앞에서 count장 선택.
        /// </summary>
        public List<Card> ChooseCardsFromZone(Player me, ZoneType zone, int count, string filter, GameContext ctx)
        {
            var pool = me.GetZone(zone);
            var filtered = string.IsNullOrEmpty(filter)
                ? pool
                : pool.Where(c => CardSelector.MatchesSingle(c, filter)).ToList();

            return filtered.Take(count).ToList();
        }
    }
}
