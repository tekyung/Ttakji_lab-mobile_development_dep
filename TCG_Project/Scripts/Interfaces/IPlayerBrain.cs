// Scripts/Interfaces/IPlayerBrain.cs — Phase 8 재설계: 6페이즈 메서드 기반
using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Interfaces
{
    /// <summary>
    /// 플레이어 행동 결정 인터페이스 (Phase 8 — 룰북 6페이즈 기반).
    /// BotBrain(AI)과 HumanBrain(UI 입력)이 이를 구현한다.
    /// Unity BattleManager는 Human 플레이어의 경우 EventManager 이벤트로 직접 처리한다.
    /// </summary>
    public interface IPlayerBrain
    {
        // ─── 6페이즈 결정 메서드 ────────────────────────────────────

        /// <summary>
        /// 세트 페이즈: 패에서 세트존에 내려놓을 카드 1장을 선택한다.
        /// 패가 비어있으면 null을 반환한다.
        /// </summary>
        Card ChooseSetCard(Player me, GameContext ctx);

        /// <summary>
        /// 오픈 페이즈: 세트 카드를 공개(Open)할지 폐기(Abandon)할지 결정한다.
        /// effectiveCost는 CostReductionBattlefieldEffect가 반영된 실효 코스트다.
        /// </summary>
        OpenPhaseChoice ChooseOpenOrAbandon(Player me, Card setCard, int effectiveCost, GameContext ctx);

        /// <summary>
        /// 스택 발동: StackZone의 stackCard를 상대 opponentCard에 반응해 발동할지 결정한다.
        /// </summary>
        bool ChooseStackActivation(Player me, Card stackCard, Card opponentCard, GameContext ctx);

        /// <summary>
        /// 카드 선택: 특정 존에서 filter 조건에 맞는 카드를 count장 선택한다.
        /// filter 형식: "character:ELLI,type:Attack" (쉼표 구분 AND 조건).
        /// Bot은 즉시 반환, Human은 EventManager.OnRequireCardChoice 이벤트로 처리.
        /// </summary>
        List<Card> ChooseCardsFromZone(Player me, ZoneType zone, int count, string filter, GameContext ctx);

    }
}
