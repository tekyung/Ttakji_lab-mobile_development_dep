using System;

public class session_data
{
    // ★ host/guest 에는 <b>신원(playerId)</b>이 들어간다. 사람이 읽는 이름이 아니다.
    //   ExitSession이 이 값으로 호스트와 게스트를 가르므로 겹치면 안 되기 때문이다.
    //   화면에 띄울 이름은 아래 hostName/guestName 이 따로 나른다.
    public string host;     
    public string guest;    

    /// <summary>호스트의 표시 이름(<c>이름#1234</c>). 게스트 화면이 읽는다.</summary>
    public string hostName;

    /// <summary>게스트의 표시 이름. 호스트가 보고 [시작]/[퇴장]을 정한다.</summary>
    public string guestName;
    public string state;
    public string currentTime;
    public string secret;
    public string turn;

    public session_data(string host, string guest, string state, string time, string secret, string turn)
    {
        this.host = host;
        this.guest = guest;
        this.state = state;
        this.currentTime = time;
        this.secret = secret;
        this.turn = turn;
    }
}

/// <summary>
/// 방 입장 시도의 결과.
///
/// ★ 예전에는 <c>bool</c> 하나뿐이라 "방이 없다"와 "자리가 찼다"를 가르지 못했다.
///   씬에 <c>PopupRoomNotFound</c>와 <c>PopupRoomFull</c>이 둘 다 있는데 어느 것도
///   켜지지 않던 이유가 이것이다 — 화면이 구분할 근거를 받지 못했다.
/// </summary>
public enum JoinResult
{
    Success,

    /// <summary>그 번호의 방이 서버에 없다.</summary>
    NotFound,

    /// <summary>이미 다른 사람이 앉아 있거나, 사람을 더 받지 않는 상태(READY/PLAYING)다.</summary>
    Full,

    /// <summary>방은 멀쩡한데 쓰기가 실패했다. 네트워크 문제로 본다.</summary>
    Failed,
}

public static class SessionStatus
{
    public const string STATE_WAITING = "WAITING";
    public const string STATE_READY   = "READY";
    public const string STATE_PLAYING = "PLAYING";
    public const string STATE_PRIVATE = "PRIVATE";
    public const string STATE_PUBLIC  = "PUBLIC";
}