// CharacterAbilityBase.cs — Phase 14: 기본 no-op 구현
using System;
using TCG_Project.Scripts.Core;
using System.Threading.Tasks;

namespace TCG_Project.Scripts.Abilities
{
    public abstract class CharacterAbilityBase : ICharacterAbility
    {
        public abstract string CharacterCardId { get; }

        public virtual bool CanUse(Player me, GameContext ctx)
        {
            // 주/부 캐릭터 중 하나이며, 아직 이 게임에서 사용하지 않았을 때만 true
            return me.HasCharacter(CharacterCardId) && !me.HasUsedCharacterAbility(CharacterCardId);
        }

        // 오버라이드하지 않은 훅은 기본적으로 false(스킵)를 반환하고 즉시 종료됨
        public virtual void OnDrawPhase(Player me, GameContext ctx, Action<bool> onComplete) { onComplete?.Invoke(false); }
        public virtual void OnSetPhase(Player me, GameContext ctx, Action<bool> onComplete) { onComplete?.Invoke(false); }
        public virtual void OnOpenPhaseAbandon(Player me, GameContext ctx, Action<bool> onComplete) { onComplete?.Invoke(false); }
        //public virtual void OnMainPhaseAfterAttack(Player me, Card played, Player enemy, GameContext ctx, Action<bool> onComplete) { onComplete?.Invoke(false); }
        public virtual void OnMainPhaseAfterAttack(Player owner, Card playedCard, Player enemy, GameContext context, Action<Card> onComplete) { onComplete?.Invoke(null); }
        public virtual bool ShouldReplaceDraw(Player me) => false;
        public virtual void ExecuteDrawPhase(Player me, GameContext ctx, Card chosenCard) { }

        public virtual bool CanSoniaPreSet(Player me) => false;
        public virtual void ExecuteSetPhase(Player me, GameContext ctx, Card chosenCard) { }

        public virtual void OnOpenPhaseAbandon(Player me, GameContext ctx) { }

        public virtual bool CanEllieExtraAttack(Player me, Card playedCard) => false;
        public virtual void ExecuteMainPhaseAfterAttack(Player me, Card played, Player enemy, GameContext ctx, Card chosenCard, Action onComplete = null) { }
    }
    
}
