using System.Collections.Generic;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Core
{
    // 나중에 발동될 효과 정보
    public class PendingEffect
    {
        public GamePhase TriggerPhase; // 언제 발동?
        public Player OwnerPlayer;     // 누구 턴에? (예: '나의' 다음 턴 시작 시)
        public ICardEffect Effect;     // 실행할 효과 (주로 역연산)
        public GameContext Context;    // 당시의 상황 스냅샷
    }
}