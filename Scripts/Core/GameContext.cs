using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Core
{
    public class GameContext
    {
        public Player Player { get; set; }
        public Player Opponent { get; set; }
        // 게임 매니저 등 필요한 정보 추가
    }
}
