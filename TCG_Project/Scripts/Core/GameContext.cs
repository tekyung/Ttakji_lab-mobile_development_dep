using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Core
{
    public class GameContext
    {
        // 전체 플레이어 목록
        public List<Player> Players { get; set; }

        // 현재 턴을 진행 중인 플레이어 (행동 주체)
        public Player ActivePlayer { get; set; }

        // 타겟팅된 플레이어 (공격 대상 등) - 상황에 따라 null일 수 있음
        public Player TargetPlayer { get; set; }

        // 유틸리티: 적 찾기 (1:1 상황 가정 시 편의 기능)
        public Player GetOpponent(Player me)
        {
            // 나(me)가 아닌 첫 번째 플레이어를 반환
            return Players.Find(p => p != me);
        }
    }
}
