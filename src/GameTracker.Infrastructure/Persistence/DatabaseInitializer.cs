using Microsoft.EntityFrameworkCore;

namespace GameTracker.Infrastructure.Persistence;

public sealed class DatabaseInitializer(IDbContextFactory<GameTrackerDbContext> contextFactory)
{
    /// <summary>Applies pending migrations and enables WAL so the UI can read while the tracker writes.</summary>
    public void Initialize()
    {
        using var db = contextFactory.CreateDbContext();
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}
