namespace PermaLocke.App.Services;

/// <summary>What a notice is about. Decides its tab, its colour and the mark drawn on its sprite.</summary>
public enum ToastKind
{
    /// <summary>PermaLocke saying something about itself.</summary>
    Info,

    /// <summary>The Poké Balls were taken away.</summary>
    BallsTaken,

    /// <summary>The Poké Balls were given back.</summary>
    BallsBack,

    /// <summary>A route's first wild battle.</summary>
    FirstEncounter,

    /// <summary>A shiny, which can always be caught.</summary>
    Shiny,

    /// <summary>One of the static captures the competition allows.</summary>
    AllowedCapture,

    /// <summary>A Pokémon died.</summary>
    Death,

    /// <summary>The whole team fell, or the fallen were marked in the save.</summary>
    TeamWipe,

    /// <summary>A prize was handed over.</summary>
    Reward,

    /// <summary>Something PermaLocke could not do and the player should know.</summary>
    Warning
}
