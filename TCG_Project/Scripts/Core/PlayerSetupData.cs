using System.Collections.Generic;

namespace TCG_Project.Scripts.Core
{
    /// <summary>
    /// 게임 시작 전, 로비/매치메이킹 시스템에서 조립하여 BattleManager로 넘겨주는 플레이어 설정 데이터입니다.
    /// </summary>
    public class PlayerSetupData
    {
        public string PlayerName { get; set; }
        public UserType Type { get; set; }
        public string MainCharacterId { get; set; }
        public string SubCharacterId { get; set; } // 단일 덱일 경우 null
        public List<string> DeckCardIds { get; set; } // 유저가 구성한 20장 덱의 카드 ID 목록
    }
}