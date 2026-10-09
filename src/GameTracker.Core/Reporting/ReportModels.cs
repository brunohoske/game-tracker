using GameTracker.Core.Domain;

namespace GameTracker.Core.Reporting;

public sealed record SessionSpan(long SessionId, int GameId, DateTime StartedAtUtc, DateTime EndedAtUtc, bool IsOpen)
{
    public TimeSpan Duration => EndedAtUtc - StartedAtUtc;
}

public sealed record GameSummary(
    int GameId,
    string Name,
    string? CoverImagePath,
    TimeSpan Playtime,
    int SessionCount,
    DateTime? LastPlayedUtc);

public sealed record LibraryReport(IReadOnlyList<GameSummary> Games, TimeSpan TotalPlaytime, int SessionCount)
{
    public TimeSpan AverageSession => SessionCount == 0 ? TimeSpan.Zero : TotalPlaytime / SessionCount;
}

public sealed record GameInfo(
    int Id,
    string Name,
    GameSource Source,
    string? ExternalId,
    string? InstallDirectory,
    string? ExecutablePath,
    string? Summary,
    IReadOnlyList<string> Genres,
    string? Developer,
    string? Publisher,
    DateOnly? ReleaseDate,
    string? CoverImagePath,
    string? BackgroundImagePath,
    bool IsIgnored,
    bool IsSuggested);

public sealed record MonthlyPlaytime(YearMonth Month, TimeSpan Playtime);

public sealed record GameReport(
    GameInfo Game,
    TimeSpan Playtime,
    TimeSpan AllTimePlaytime,
    int SessionCount,
    TimeSpan AverageSession,
    TimeSpan LongestSession,
    DateTime? FirstPlayedUtc,
    DateTime? LastPlayedUtc,
    IReadOnlyList<SessionSpan> Sessions,
    IReadOnlyList<MonthlyPlaytime> Monthly);

public interface IReportService
{
    Task<LibraryReport> GetLibraryReportAsync(ReportPeriod period, CancellationToken cancellationToken = default);

    Task<GameReport?> GetGameReportAsync(int gameId, ReportPeriod period, CancellationToken cancellationToken = default);

    /// <summary>Months (local time) that have at least one session, most recent first.</summary>
    Task<IReadOnlyList<YearMonth>> GetActiveMonthsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every month with activity, most recent first, keeping only the top games of each month.</summary>
    Task<IReadOnlyList<MonthRecap>> GetMonthlyRecapsAsync(int maxGamesPerMonth, CancellationToken cancellationToken = default);

    /// <summary>One month with all its games, or null when nothing was played.</summary>
    Task<MonthRecap?> GetMonthRecapAsync(YearMonth month, CancellationToken cancellationToken = default);
}
