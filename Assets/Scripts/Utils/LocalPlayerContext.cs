// LocalPlayerContext.cs — "이 클라이언트가 조작하는 플레이어는 누구인가"를 판정하는 단일 창구
//
// 로컬 플레이에서는 사람이 한 명뿐이라 UI 전 계층이 `UserType.Human`으로 "나"를 판정해 왔다.
// 온라인(사람 vs 사람)은 **호스트·게스트 둘 다 `UserType.Human`**이라 그 판정이 무너진다:
// 호스트 화면에 게스트에게 물어야 할 다이얼로그가 뜨고 호스트가 대신 답해 버리며,
// 상대 보드(EnemyVisualTester)는 "봇이 아니다"라는 이유로 아무것도 그리지 않는다.
//
// 그래서 판정을 여기 한 곳으로 모으고, 온라인일 때만 `GameData.MyRole`로 가른다.
// 엔진의 플레이어 이름은 온라인에서 "HOST"/"GUEST"로 고정되어 있고(session_game_manage),
// `GameData.MyRole`도 같은 값을 쓴다.
//
// ★ 로컬 모드의 판정 결과는 종전과 완전히 같다 (사람=나 / 봇=상대 / 봇vs봇=조작 주체 없음).
//   그래서 봇 vs 봇 회귀 기준선에 영향이 없다.
using TCG_Project.Scripts.Core;

public static class LocalPlayerContext
{
    private static bool _warnedMissingRole;

    /// <summary>이 클라이언트가 조작하는 플레이어인가.</summary>
    public static bool IsMine(Player player)
    {
        if (player == null) return false;

        if (OnlineMatchStarter.IsOnlineSessionActive)
        {
            if (string.IsNullOrEmpty(GameData.MyRole))
            {
                // 여기 걸리면 보드·입력 UI가 통째로 죽는다. 조용히 넘기지 않는다.
                if (!_warnedMissingRole)
                {
                    _warnedMissingRole = true;
                    UnityEngine.Debug.LogWarning(
                        "[LocalPlayerContext] 온라인 세션인데 GameData.MyRole이 비어 있다. " +
                        "매칭(session_manage)을 거치지 않고 씬에 진입했을 수 있다 — 내 보드/입력이 동작하지 않는다.");
                }
                return false;
            }

            return player.Name == GameData.MyRole;
        }

        return player.Type == UserType.Human;
    }

    /// <summary>
    /// 상대 보드(화면 위쪽)에 그려야 할 플레이어인가.
    /// 봇 vs 봇 관전에서는 조작 주체가 없으므로 <b>양쪽 모두 true</b>다 — 종전 <c>Type == Bot</c>과 같다.
    /// </summary>
    public static bool IsOpponent(Player player) => player != null && !IsMine(player);

    /// <summary>
    /// 내가 조작하는 플레이어. 조작 주체가 없으면(봇 vs 봇 관전) null.
    /// 표시용으로 "하단 보드 주인"이 필요할 때는 호출부에서 p1으로 폴백한다.
    /// </summary>
    public static Player ResolveMine(Player p1, Player p2)
    {
        if (IsMine(p1)) return p1;
        if (IsMine(p2)) return p2;
        return null;
    }
}
