// Scripts/Systems/HumanBrain.cs — Phase 8 구현: 6페이즈 Human 입력 위임
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// Human 플레이어의 행동 결정 구현체.
    ///
    /// Unity BattleManager에서는 Human 플레이어의 결정을 이 클래스를 통해 직접 호출하지 않고,
    /// BattleManager가 player.Type == UserType.Human을 확인한 뒤
    /// EventManager의 OnRequire* 이벤트를 발생시키고 WaitUntil로 응답을 기다린다.
    ///
    /// 이 클래스의 IPlayerBrain 구현은 비상용 폴백(콘솔 테스트 등)에만 사용된다.
    /// </summary>
    public class HumanBrain : IPlayerBrain
    {
        private readonly Player _me;
        public HumanBrain(Player player) { _me = player; }

        // ─── 6페이즈 결정 (BattleManager에서 직접 호출되지 않음) ─────────────

        /// <summary>
        /// 세트 카드 선택. Unity에서는 BattleManager가 OnRequireSetPhaseAction 이벤트로 처리한다.
        /// 폴백: 패의 첫 번째 카드 반환.
        /// </summary>
        public Card ChooseSetCard(Player me, GameContext ctx)
            => me.Hand.Count > 0 ? me.Hand[0] : null;

        /// <summary>
        /// 오픈/폐기 결정. Unity에서는 BattleManager가 OnRequireOpenPhaseAction 이벤트로 처리한다.
        /// 폴백: 항상 폐기 (안전한 기본값).
        /// </summary>
        public OpenPhaseChoice ChooseOpenOrAbandon(Player me, Card setCard, int effectiveCost, GameContext ctx)
            => OpenPhaseChoice.Abandon;

        /// <summary>
        /// 스택 발동 결정. Unity에서는 BattleManager가 OnRequireStackResponse 이벤트로 처리한다.
        /// 폴백: 발동하지 않음.
        /// </summary>
        public bool ChooseStackActivation(Player me, Card stackCard, Card opponentCard, GameContext ctx)
            => false;

        /// <summary>
        /// 존에서 카드 선택. Unity에서는 BattleManager가 OnRequireCardChoice 이벤트로 처리한다.
        /// 폴백: 빈 목록 반환.
        /// </summary>
        public List<Card> ChooseCardsFromZone(Player me, ZoneType zone, int count, string filter, GameContext ctx)
            => new List<Card>();

    }
}
