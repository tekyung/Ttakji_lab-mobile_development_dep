// ICharacterAbility.cs — Phase 14: 캐릭터 능력 추상화
using System;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Abilities
{
    /// <summary>
    /// 캐릭터 고유 능력 인터페이스.
    /// 각 능력은 해당 페이즈 훅만 구현하고, 나머지는 no-op 또는 false 반환.
    /// </summary>
    public interface ICharacterAbility
    {
        string CharacterCardId { get; }

        // 능력 사용 가능 여부 (Phase 17: 1회 제한 및 보유 여부 판단)
        bool CanUse(Player me, GameContext ctx);

        // 페이즈별 발동 훅. onComplete(true)면 능력을 사용한 것.
        void OnDrawPhase(Player me, GameContext ctx, Action<bool> onComplete);
        void OnSetPhase(Player me, GameContext ctx, Action<bool> onComplete);
        void OnOpenPhaseAbandon(Player me, GameContext ctx, Action<bool> onComplete);
        Card OnMainPhaseAfterAttack(Player owner, Card playedCard, Player enemy, GameContext context);
        /// <summary>드로우 페이즈: 일반 드로우 대신 이 능력으로 대체할지.</summary>
        bool ShouldReplaceDraw(Player me);

        /// <summary>드로우 페이즈 실행 (chosenCard: 베로니카가 선택한 1장).</summary>
        void ExecuteDrawPhase(Player me, GameContext ctx, Card chosenCard);

        /// <summary>세트 페이즈 전: 폐기존에서 회수할 카드가 있는지.</summary>
        bool CanSoniaPreSet(Player me);

        /// <summary>세트 페이즈 전 실행 (chosenCard: 소니아가 선택한 1장).</summary>
        void ExecuteSetPhase(Player me, GameContext ctx, Card chosenCard);

        /// <summary>오픈 페이즈 폐기 선택 시 (다이나 라이프 회복).</summary>
        void OnOpenPhaseAbandon(Player me, GameContext ctx);

        /// <summary>메인 페이즈 공격 카드 발동 후: 추가 공격 발동 가능한지.</summary>
        bool CanEllieExtraAttack(Player me, Card playedCard);

        /// <summary>메인 페이즈 추가 공격 실행. onComplete: Unity 연출 완료 시 호출 (null이면 무시).</summary>
        //void ExecuteMainPhaseAfterAttack(Player me, Card played, Player enemy, GameContext ctx, Card chosenCard, Action onComplete = null);
    }
}
