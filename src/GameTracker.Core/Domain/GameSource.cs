namespace GameTracker.Core.Domain;

/// <summary>Where a game was discovered. Persisted as integer: never renumber.</summary>
public enum GameSource
{
    Unknown = 0,
    Steam = 1,
    Epic = 2,
    Gog = 3,
    Ubisoft = 4,
    Folder = 10,

    /// <summary>Recognised by behaviour (fullscreen + 3D API); starts as a suggestion the user confirms.</summary>
    Detected = 15,

    Manual = 20,
}
