using System;
using System.Globalization;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Provides methods for square encoding and decoding. 
/// </summary>
public static class Square
{
    /// <summary>
    /// Returns the file of the given square.
    /// </summary>
    /// <param name="square">The square to determine the file of.</param>
    /// <returns>The file of the given square.</returns>
    public static Int32 File(Int32 square)
    {
        return square & 7;
    }

    /// <summary>
    /// Returns the rank of the given square.
    /// </summary>
    /// <param name="square">The square to determine the rank of.</param>
    /// <returns>The rank of the given square.</returns>
    public static Int32 Rank(Int32 square)
    {
        return square >> 3;
    }

    /// <summary>
    /// Returns the square with the given name.
    /// </summary>
    /// <param name="name">The name of the square.</param>
    /// <returns>The square with the given name</returns>
    public static Int32 SquareAt(string name)
    {
        return (Int32)(name[0] - 'a' + ('8' - name[1]) * 8);
    }

    /// <summary>
    /// Returns the text representation of the file for the given square. 
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the file for the given square.</returns>
    public static string GetFileName(Int32 square)
    {
        return ((Char)(File(square) + 'a')).ToString();
    }

    /// <summary>
    /// Returns the text representation of the rank for the given square. 
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the rank for the given square.</returns>
    public static string GetRankName(Int32 square)
    {
        return (8 - Rank(square)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the text representation of the given square in coordinate 
    /// notation.
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the given square.</returns>
    public static string GetName(Int32 square)
    {
        return GetFileName(square) + GetRankName(square);
    }
}
