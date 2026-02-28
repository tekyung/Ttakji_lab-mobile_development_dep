// Scripts/Interfaces/IPlayerBrain.cs 신규 생성
using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Interfaces
{
    public interface IPlayerBrain
    {
        /// <summary>
        /// 메인 페이즈의 행동(유닛 소환, 스펠 발동, 턴 종료 등)을 수행합니다.
        /// 완료되면 onPhaseFinished 콜백을 호출하여 시스템에 끝났음을 알립니다.
        /// </summary>
        void ExecuteMainPhase(GameContext context, Player enemy, Action onPhaseFinished);

        /// <summary>
        /// 배틀 페이즈의 행동(공격할 유닛 및 타겟 지정)을 수행합니다.
        /// 완료되면 onPhaseFinished 콜백을 호출합니다.
        /// </summary>
        void ExecuteBattlePhase(GameContext context, Player enemy, Action onPhaseFinished);

        /// <summary>
        /// 스펠이나 효과 발동 시 타겟을 선택해야 할 때 호출됩니다.
        /// </summary>
        void SelectTarget(List<Target> candidates, int count, Action<List<Target>> onTargetSelected);
    }
}