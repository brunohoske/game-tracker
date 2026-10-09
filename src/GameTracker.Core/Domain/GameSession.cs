namespace GameTracker.Core.Domain;

public sealed class GameSession
{
    public long Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }

    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// End of the session. While the session is open this is the last heartbeat, so a crash or power loss
    /// loses at most one heartbeat interval of playtime.
    /// </summary>
    public DateTime EndedAtUtc { get; set; }

    public bool IsOpen { get; set; }

    public TimeSpan Duration => EndedAtUtc - StartedAtUtc;
}
