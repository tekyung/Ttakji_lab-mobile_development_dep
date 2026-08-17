using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Managers
{
    /// <summary>
    /// 3판 2선승 매치(세트)를 관리한다.
    ///
    /// 사용 방법:
    ///   1. MatchManager match = new MatchManager();
    ///   2. 각 게임 종료 후 match.RecordResult(winner, p1, p2) 호출
    ///   3. match.IsMatchOver() 로 세트 종료 여부 확인
    ///   4. match.GetMatchWinner(p1, p2) 로 세트 승자 조회
    /// </summary>
    public class MatchManager
    {
        private readonly int _gamesToWin; // 세트 승리에 필요한 게임 수 (기본 2)
        private readonly int _maxGames;   // 최대 게임 수 (기본 3)

        private int _p1Wins = 0;
        private int _p2Wins = 0;
        private int _draws = 0;
        private int _gamesPlayed = 0;

        public int GamesPlayed => _gamesPlayed;

        public MatchManager(int gamesToWin = 2, int maxGames = 3)
        {
            _gamesToWin = gamesToWin;
            _maxGames = maxGames;
        }

        /// <summary>
        /// 한 게임의 결과를 기록한다.
        /// </summary>
        /// <param name="winner">게임 승자 (null이면 무승부)</param>
        /// <param name="p1">플레이어 1</param>
        /// <param name="p2">플레이어 2</param>
        public void RecordResult(Player winner, Player p1, Player p2)
        {
            _gamesPlayed++;

            if (winner == null)
            {
                _draws++;
                EventManager.OnLogMessage?.Invoke(
                    $"  [매치] 게임 {_gamesPlayed} 무승부. " +
                    $"전적: {p1.Name} {_p1Wins}승 — {p2.Name} {_p2Wins}승 ({_draws}무)");
            }
            else if (winner == p1)
            {
                _p1Wins++;
                EventManager.OnLogMessage?.Invoke(
                    $"  [매치] 게임 {_gamesPlayed} {p1.Name} 승리. " +
                    $"전적: {p1.Name} {_p1Wins}승 — {p2.Name} {_p2Wins}승 ({_draws}무)");
            }
            else
            {
                _p2Wins++;
                EventManager.OnLogMessage?.Invoke(
                    $"  [매치] 게임 {_gamesPlayed} {p2.Name} 승리. " +
                    $"전적: {p1.Name} {_p1Wins}승 — {p2.Name} {_p2Wins}승 ({_draws}무)");
            }
        }

        /// <summary>
        /// 세트가 종료되었는지 확인한다.
        /// 어느 한 쪽이 필요한 승 수에 도달했거나 최대 게임 수를 초과하면 true.
        /// </summary>
        public bool IsMatchOver()
        {
            if (_p1Wins >= _gamesToWin || _p2Wins >= _gamesToWin) return true;
            if (_gamesPlayed >= _maxGames) return true;
            return false;
        }

        /// <summary>
        /// 세트 승자를 반환한다.
        /// 어느 쪽도 우세하지 않으면 null(세트 무승부).
        /// </summary>
        public Player GetMatchWinner(Player p1, Player p2)
        {
            if (_p1Wins > _p2Wins) return p1;
            if (_p2Wins > _p1Wins) return p2;
            return null;
        }

        /// <summary>
        /// 특정 플레이어의 현재 세트 승 수를 반환한다.
        /// </summary>
        public int GetWins(Player p, Player p1, Player p2)
        {
            if (p == p1) return _p1Wins;
            if (p == p2) return _p2Wins;
            return 0;
        }

        public string GetStatusString(Player p1, Player p2)
        {
            return $"{p1.Name} {_p1Wins}승 — {p2.Name} {_p2Wins}승 ({_draws}무)  " +
                    $"[{_gamesPlayed}/{_maxGames}게임]";
        }
    }
}
