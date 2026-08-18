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
        /// <summary>무한 대기를 뜻하는 타임아웃 값. Task.Delay(-1)은 영원히 완료되지 않는다.</summary>
        public const int NoTimeout = -1;

        /// <summary>
        /// 사람 입력에 제한 시간을 두지 않을지 여부. 기본값은 <c>true</c>(로컬 플레이).
        ///
        /// ★ <b>온라인에서는 반드시 <c>false</c>여야 한다.</b>
        ///   한쪽이 카드 선택 창을 닫지 않거나 접속이 끊기면 <b>양쪽 모두 영구 정지</b>한다.
        ///   용병 능력·카드 선택 대기는 호스트 코루틴의 <c>WaitUntil(done || IsGameOver)</c>에 걸려 있는데,
        ///   응답이 없으면 <c>done</c>도 <c>IsGameOver</c>도 되지 않기 때문이다.
        ///
        ///   전환은 <c>OnlineMatchStarter</c>가 매치 준비 시 자동으로 한다. 직접 만질 일은 거의 없다.
        /// </summary>
        public static bool AllowUnlimitedHumanInput { get; set; } = true;

        /// <summary>
        /// 입력 대기 제한 시간(밀리초)을 플레이어 유형과 <see cref="AllowUnlimitedHumanInput"/>에 따라 결정한다.
        ///
        /// - Human + 로컬: 제한 없음(<see cref="NoTimeout"/>). 사람은 얼마든지 생각할 수 있어야 한다
        /// - Human + 온라인: <c>GameRules.ChooseWaitTime</c> — 상대를 무한정 기다리게 할 수 없다
        /// - Bot 및 그 외: <c>GameRules.ChooseWaitTime</c> (CommonConfig.json의 choose_wait_time)
        /// </summary>
        public static int GetChooseTimeoutMs(Player player)
        {
            bool isHuman = player != null && player.Type == UserType.Human;
            return isHuman && AllowUnlimitedHumanInput ? NoTimeout : GameRules.ChooseWaitTime;
        }

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
                    if (eff is BattlefieldEffect bf && !string.IsNullOrEmpty(bf.CostReductionFilter))
                    {
                        bool isMatch = false;
                        // ★ 추가: character: 필터일 경우 확실하게 하드코딩으로 검증
                        if (bf.CostReductionFilter.StartsWith("character:"))
                        {
                            string targetChar = bf.CostReductionFilter.Split(':')[1]; // 예: "SONIA"

                            // "SONI-02"에서 "SONI"만 추출
                            string cardPrefix = card.Id.Contains("-") ? card.Id.Split('-')[0] : card.Id;

                            // ★ 수정됨: 카드의 CharacterId가 정확히 일치하거나, 타겟명("SONIA")이 카드접두사("SONI")로 시작하는지 검사
                            isMatch = (card.CharacterId == targetChar) || targetChar.StartsWith(cardPrefix);
                        }
                        else
                        {
                            isMatch = CardSelector.MatchesSingle(card, bf.CostReductionFilter);
                        }

                        if (isMatch)
                        {
                            cost = Math.Max(0, cost - bf.CostReduction);
                        }
                    }
                }
            }
            return cost;
        }

        /// <summary>
        /// 드로우 페이즈: 전장 카드의 매 턴 효과 적용 (지속 효과 Wake)
        /// </summary>
        public static bool ApplyBattlefieldTurnEffects(Player p, GameContext ctx)
        {
            if (p.BattlefieldCard == null) return false;

            foreach (var eff in p.BattlefieldCard.Effects)
            {
                if (eff is not BattlefieldEffect bf) continue;

                // ★ 지속 효과: Execute를 부르지 않고 수치만 추출하여 1회성 버프로 장전 (Wake)
                if (bf.PerTurnEffect is BuffEffect buff)
                {
                    if (buff.TypeOfBuff == BuffType.Armor)
                    {
                        p.BattlefieldArmor = buff.Amount;
                        EventManager.OnLogMessage?.Invoke($"  [전장 효과] {p.Name} '{p.BattlefieldCard.Name}' (1회성 아머 +{buff.Amount})");
                    }
                    else if (buff.TypeOfBuff == BuffType.Firepower)
                    {
                        p.BattlefieldFirepower = buff.Amount;
                        EventManager.OnLogMessage?.Invoke($"  [전장 효과] {p.Name} '{p.BattlefieldCard.Name}' (1회성 화력 +{buff.Amount})");
                    }
                }

                if (!string.IsNullOrEmpty(bf.CostReductionFilter))
                    EventManager.OnLogMessage?.Invoke($"  [전장] {p.Name} '{p.BattlefieldCard.Name}': 코스트 감소 상시 적용 중");
            }
            return false;
        }

        /// <summary>
        /// 자원 페이즈: 전장 카드의 자원페이즈 기동 효과 적용 (ELLI-11 무작위 노획 등)
        /// </summary>
        public static bool ApplyBattlefieldResourcePhaseEffects(Player p, GameContext ctx)
        {
            if (p.BattlefieldCard == null) return false;

            foreach (var eff in p.BattlefieldCard.Effects)
            {
                if (eff is BattlefieldEffect bf && bf.PerResourcePhaseEffect != null)
                {
                    EventManager.OnLogMessage?.Invoke($"  ▶ [전장 기동] {p.Name} '{p.BattlefieldCard.Name}' 효과 발동!");
                    ctx.ActivePlayer = p;
                    ctx.TargetPlayer = ctx.GetOpponent(p);
                    ctx.LastEffectSucceeded = true;

                    // 기동 효과는 1회성 스탯이 아니라 실제 카드 이동/효과이므로 정상 실행
                    bf.PerResourcePhaseEffect.Execute(ctx, () => { });
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