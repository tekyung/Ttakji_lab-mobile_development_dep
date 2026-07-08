// DainaAbility.cs — Phase 14: DAIN-01 다이나 고유 능력
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Abilities
{
    public class DainaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "DAIN-01";

        // Gemini 버전
        public override void OnOpenPhaseAbandon(Player me, GameContext ctx, Action<bool> onComplete)
        {
            // 발동 조건: 오픈 페이즈에 카드 폐기 선택 + 라이프 토큰이 최대치 미만
            if (me.LifeTokens == GameRules.LifeTokens) { onComplete?.Invoke(false); return; }
            
            EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 다이나 능력 발동: 폐기 선택 → 라이프 +1");
            me.GainLife(1);
            me.MarkCharacterAbilityUsed(CharacterCardId);
            onComplete?.Invoke(true);
        }
    }
}
