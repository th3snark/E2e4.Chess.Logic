using System;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Defines colours and masks.
/// </summary>
public static class Colour
{
    /// <summary>
    /// The mask for extracting the colour of a piece.
    /// </summary>
    public const int Mask = Black; // 0001

    /// <summary>
    /// The colour white.
    /// </summary>
    public const int White = 0;    // 0000

    /// <summary>
    /// The colour black.
    /// </summary>
    public const int Black = 1;    // 0001

    /// <summary>
    /// Returns the text representation of the given colour.
    /// </summary>
    /// <param name="colour">The colour to identify.</param>
    /// <returns>The text representation of the given colour</returns>
    public static string GetName(int colour)
    {
        return (colour & Colour.Mask) == Colour.White ? "White" : "Black";
    }

    /// <summary>
    /// Returns the text representation of the given colour as an initial.
    /// </summary>
    /// <param name="colour">The colour to identify.</param>
    /// <returns>The text representation of the given colour as an initial.</returns>
    public static string GetInitial(int colour)
    {
        return GetName(colour)[0].ToString();
    }
}
