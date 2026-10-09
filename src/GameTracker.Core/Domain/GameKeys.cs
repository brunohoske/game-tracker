using GameTracker.Core.Common;

namespace GameTracker.Core.Domain;

/// <summary>Builds the stable identity (<see cref="Game.Key"/>) of a game.</summary>
public static class GameKeys
{
    public static string ForLibrary(GameSource source, string externalId) =>
        $"{source.ToString().ToLowerInvariant()}:{externalId.Trim().ToLowerInvariant()}";

    public static string ForFolder(string directory) => "dir:" + PathUtil.Normalize(directory);

    public static string ForExecutable(string executablePath) => "exe:" + PathUtil.Normalize(executablePath);
}
