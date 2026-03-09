// GameLogicHelpers.cs — Phase 13: ConsoleRunner·BattleManager 공유 로직
// Zero Unity Dependency — Scripts/Systems에 위치
using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// 게임 루프에서 ConsoleRunner와 BattleManager가 공유하는 정적 헬퍼.
    /// </summary>
    public static class GameLogicHelpers
    {
        /// <summary>
        /// BattlefieldEffect.CostReductionFilter를 반영한 실효 코스트 계산.
        /// </summary>
        public static int GetEffectiveCost(Card card, Player p)
        {
            int cost = card.Cost;
            if (p.BattlefieldCard != null)
            {
                foreach (var eff in p.BattlefieldCard.Effects)
                {
                    if (eff is BattlefieldEffect bf &&
                        !string.IsNullOrEmpty(bf.CostReductionFilter) &&
                        CardSelector.MatchesSingle(card, bf.CostReductionFilter))
                    {
                        cost = Math.Max(0, cost - bf.CostReduction);
                    }
                }
            }
            return cost;
        }

        /// <summary>
        /// 드로우 페이즈: 전장 카드의 매 턴 효과 적용 (VERO-11, DAIN-11, SONI-11).
        /// 반환값: 효과 적용 중 누군가 사망하여 게임이 오버되었는지 여부 (SBA 대응)
        /// </summary>
        public static bool ApplyBattlefieldTurnEffects(Player p, GameContext ctx)
        {
            if (p.BattlefieldCard == null) return false;

            foreach (var eff in p.BattlefieldCard.Effects)
            {
                if (eff is not BattlefieldEffect bf) continue;

                if (bf.PerTurnEffect != null)
                {
                    EventManager.OnLogMessage?.Invoke($"  [전장 효과 발동] {p.Name}의 '{p.BattlefieldCard.Name}'");
                    ctx.ActivePlayer = p;
                    // TargetPlayer는 상황에 따라 달라질 수 있으나, 일반적으로 상대방으로 설정
                    ctx.TargetPlayer = ctx.GetOpponent(p); 
                    
                    // Execute 직접 호출 대신, Card.Play 등을 모방하여 명시적으로 실행
                    // (전장 효과는 스택 반응이 아니므로 isStackTrigger = false 취급이 맞지만,
                    // BattlefieldEffect 내부에 감싸진 서브 이펙트이므로 직접 실행을 유지하되 컨텍스트를 보호합니다)
                    ctx.LastEffectSucceeded = true;
                    bf.PerTurnEffect.Execute(ctx, () => { });

                    // ★ SBA 대응: 전장 효과(데미지 등)로 인해 즉사했는지 바로 확인
                    if (ctx.IsGameOver) return true; 
                }

                if (!string.IsNullOrEmpty(bf.CostReductionFilter))
                {
                    EventManager.OnLogMessage?.Invoke(
                        $"  [전장] {p.Name} '{p.BattlefieldCard.Name}': 조건 카드 코스트 감소 효과 유지 중");
                }
            }
            return false;
        }

        /// <summary>
        /// 자원 페이즈: 전장 카드의 자원페이즈 효과 적용 (ELLI-11 무작위 노획).
        /// 반환값: 효과 적용 중 게임 오버 여부
        /// </summary>
        public static bool ApplyBattlefieldResourcePhaseEffects(Player p, GameContext ctx)
        {
            if (p.BattlefieldCard == null) return false;

            foreach (var eff in p.BattlefieldCard.Effects)
            {
                if (eff is BattlefieldEffect bf && bf.PerResourcePhaseEffect != null)
                {
                    EventManager.OnLogMessage?.Invoke($"  [전장 효과 발동] {p.Name}의 '{p.BattlefieldCard.Name}'");
                    ctx.ActivePlayer = p;
                    ctx.TargetPlayer = ctx.GetOpponent(p);

                    ctx.LastEffectSucceeded = true;
                    bf.PerResourcePhaseEffect.Execute(ctx, () => { });

                    // ★ SBA 대응
                    if (ctx.IsGameOver) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 덱에서 패로 N장 드로우. OnCardDraw 및 OnCardMove 이벤트 발행.
        /// </summary>
        public static void DrawCards(Player p, int count, GameContext ctx = null)
        {
            int available = Math.Min(count, p.Deck.Count);
            int drawn = 0;
            for (int i = 0; i < available; i++)
            {
                var c = p.ExtractCard(ZoneType.Deck, "Top");
                if (c != null)
                {
                    p.InsertCard(ZoneType.Hand, c);
                    drawn++;
                    
                    // ★ 유니티 연출을 위한 필수 이벤트(이동 및 드로우)를 모두 쏩니다.
                    EventManager.OnCardMove?.Invoke(c, p, ZoneType.Deck, p, ZoneType.Hand);
                    EventManager.OnCardDraw?.Invoke(c, p, ZoneType.Deck);
                }
            }

            if (drawn < count)
                EventManager.OnLogMessage?.Invoke($"{p.Name} 드로우: {drawn}/{count}장 (덱 고갈)");
            else
                EventManager.OnLogMessage?.Invoke($"{p.Name} 드로우: {drawn}장 (남은 덱: {p.Deck.Count}장)");
        }
    }
}