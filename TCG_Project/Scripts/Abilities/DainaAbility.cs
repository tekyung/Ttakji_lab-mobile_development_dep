// DainaAbility.cs — Phase 14: DAIN-01 다이나 고유 능력
using System;
using System.Threading.Tasks;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

namespace TCG_Project.Scripts.Abilities
{
    public class DainaAbility : CharacterAbilityBase
    {
        public override string CharacterCardId => "DAIN-01";

        public override async void OnOpenPhaseAbandon(Player me, GameContext ctx, Action<bool> onComplete)
        {
            if (me.LifeTokens == GameRules.LifeTokens) { onComplete?.Invoke(false); return; }
            
            bool shouldActivate = false;
            if (me.Type == UserType.Bot)
            {
                // ★ 봇 자동 발동 로그 추가
                EventManager.OnLogMessage?.Invoke($" 🤖 [Bot AI] {me.Name}: 다이나 능력(라이프 회복) 자동 발동 결정");
                shouldActivate = true; // 봇은 체력이 깎여있으면 바로 힐
            }
            else
            {
                shouldActivate = await AsyncTimeoutHelper.WaitForChoiceWithTimeout<bool>(
                    cb => EventManager.OnRequireOptionalAction?.Invoke(me, "다이나 능력을 발동하여 라이프를 1 회복하시겠습니까?", ctx, cb),
                    () => false, // 타임아웃 시 힐 포기
                    GameRules.ChooseWaitTime
                );
            }

            if (shouldActivate)
            {
                EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 다이나 능력 발동: 폐기 선택 → 라이프 +1");
                me.GainLife(1);
                me.MarkCharacterAbilityUsed(CharacterCardId);
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }
    }
}
/*
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
*/