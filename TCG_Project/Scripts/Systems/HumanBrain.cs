// Scripts/Systems/HumanBrain.cs 신규 생성
using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    public class HumanBrain : IPlayerBrain
    {
        private Player me;
        public HumanBrain(Player player) { me = player; }

        public void ExecuteMainPhase(GameContext context, Player enemy, Action onPhaseFinished)
        {
            // UI에 신호 전송: "내 턴이다! 카드 클릭 가능하게 활성화하고, 완료 버튼 열어줘!"
            // 유저가 UI에서 [턴 종료] 또는 [배틀 돌입] 버튼을 누르면 UI가 onPhaseFinished를 실행해줄 것입니다.
            EventManager.OnRequireMainPhaseAction?.Invoke(me, context, onPhaseFinished);
        }

        public void ExecuteBattlePhase(GameContext context, Player enemy, Action onPhaseFinished)
        {
            // UI에 신호 전송: "내 유닛들 클릭해서 적 유닛 드래그로 타겟팅하게 해줘!"
            EventManager.OnRequireBattlePhaseAction?.Invoke(me, context, onPhaseFinished);
        }

        public void SelectTarget(List<Target> candidates, int count, Action<List<Target>> onTargetSelected)
        {
            // 스펠(파이어볼 등)을 썼을 때 타겟을 고르는 상황
            // UI에 신호 전송: "candidates 애들 테두리 빨갛게 빛내고, 유저가 클릭하면 onTargetSelected 실행해줘!"
            EventManager.OnRequireTargetSelection?.Invoke(candidates, count, onTargetSelected);
        }
    }
}