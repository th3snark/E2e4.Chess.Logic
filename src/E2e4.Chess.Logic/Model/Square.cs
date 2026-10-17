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
    public static int File(int square)
    {
        return square & 7;
    }

    /// <summary>
    /// Returns the rank of the given square.
    /// </summary>
    /// <param name="square">The square to determine the rank of.</param>
    /// <returns>The rank of the given square.</returns>
    public static int Rank(int square)
    {
        return square >> 3;
    }

    /// <summary>
    /// Returns the square with the given name.
    /// </summary>
    /// <param name="name">The name of the square.</param>
    /// <returns>The square with the given name</returns>
    /// <exception cref="ArgumentException">The name is not a square from a8 to h1.</exception>
    public static int SquareAt(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (name.Length != 2 || name[0] < 'a' || name[0] > 'h' || name[1] < '1' || name[1] > '8')
            throw new ArgumentException("'" + name + "' is not a square name.", nameof(name));
        return (int)(name[0] - 'a' + ('8' - name[1]) * 8);
    }

    /// <summary>
    /// Returns the text representation of the file for the given square.
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the file for the given square.</returns>
    public static string GetFileName(int square)
    {
        return ((char)(File(square) + 'a')).ToString();
    }

    /// <summary>
    /// Returns the text representation of the rank for the given square.
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the rank for the given square.</returns>
    public static string GetRankName(int square)
    {
        return (8 - Rank(square)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the text representation of the given square in coordinate
    /// notation.
    /// </summary>
    /// <param name="square">The square to identify.</param>
    /// <returns>The name of the given square.</returns>
    public static string GetName(int square)
    {
        return GetFileName(square) + GetRankName(square);
    }
}
