namespace E2e4.Chess.Logic.Model;

/// <summary>
/// A piece that cannot leave the line between its king and an enemy slider without exposing the king to check.
/// </summary>
public readonly struct Pin
{
    /// <summary>Creates a pin.</summary>
    public Pin(int pinned, int pinner)
    {
        Pinned = pinned;
        Pinner = pinner;
    }

    /// <summary>The square of the pinned piece.</summary>
    public int Pinned { get; }

    /// <summary>The square of the enemy bishop, rook or queen that pins it.</summary>
    public int Pinner { get; }
}
