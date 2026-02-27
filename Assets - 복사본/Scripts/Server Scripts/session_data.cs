using System;

public class session_data
{
    public string host;     
    public string guest;    
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

public static class SessionStatus
{
    public const string STATE_WAITING = "WAITING";
    public const string STATE_READY   = "READY";
    public const string STATE_PLAYING = "PLAYING";
    public const string STATE_PRIVATE = "PRIVATE";
    public const string STATE_PUBLIC  = "PUBLIC";
}