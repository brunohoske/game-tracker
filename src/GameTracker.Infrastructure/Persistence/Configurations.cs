using GameTracker.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameTracker.Infrastructure.Persistence;

internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Key).HasMaxLength(512).IsRequired();
        builder.HasIndex(g => g.Key).IsUnique();

        builder.Property(g => g.Name).HasMaxLength(256).IsRequired();
        builder.Property(g => g.ExternalId).HasMaxLength(128);
        builder.Property(g => g.InstallDirectory).HasMaxLength(1024);
        builder.Property(g => g.ExecutablePath).HasMaxLength(1024);
        builder.Property(g => g.Developer).HasMaxLength(256);
        builder.Property(g => g.Publisher).HasMaxLength(256);
        builder.Property(g => g.CoverImagePath).HasMaxLength(1024);
        builder.Property(g => g.BackgroundImagePath).HasMaxLength(1024);

        builder.HasMany(g => g.Sessions)
            .WithOne(s => s.Game)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("GameSessions");
        builder.HasKey(s => s.Id);
        builder.Ignore(s => s.Duration);

        builder.HasIndex(s => new { s.GameId, s.StartedAtUtc });
        builder.HasIndex(s => s.StartedAtUtc);
    }
}
