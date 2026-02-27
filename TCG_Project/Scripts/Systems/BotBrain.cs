// Scripts/Systems/BotBrain.cs 신규 생성
using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class BotBrain : IPlayerBrain
    {
        private Player me;
        public BotBrain(Player player) { me = player; }

        public void ExecuteMainPhase(GameContext context, Player enemy, Action onPhaseFinished)
        {
            // 봇은 대기 시간이 필요 없으므로 기존의 while(actionTaken) 루프를 여기서 즉시 실행
            // ... (기존 메인 페이즈 로직 복사) ...

            // 모든 연산이 끝나면 즉시 콜백 호출 -> 다음 페이즈로 넘어감
            onPhaseFinished?.Invoke();
        }

        public void ExecuteBattlePhase(GameContext context, Player enemy, Action onPhaseFinished)
        {
            // ... (기존 배틀 페이즈 타겟팅 및 공격 로직 복사) ...
            onPhaseFinished?.Invoke();
        }

        public void SelectTarget(List<Target> candidates, int count, Action<List<Target>> onTargetSelected)
        {
            // 봇은 고민하지 않고 (HighestPower 모드 등으로) 즉시 타겟을 골라서 콜백으로 넘겨줌
            var selected = candidates.OrderByDescending(t => t.CardVal?.Power ?? 0).Take(count).ToList();
            onTargetSelected?.Invoke(selected);
        }
    }
}