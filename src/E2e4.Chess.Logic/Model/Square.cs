using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Returns the rising diagonal (a1-h8 direction) the square lies on: squares share one if and only if
    /// they have the same value, 0 to 14.
    /// </summary>
    /// <param name="square">The square to locate.</param>
    /// <returns>The index of the rising diagonal of the given square.</returns>
    public static int RisingDiagonal(int square)
    {
        return File(square) + Rank(square);
    }

    /// <summary>
    /// Returns the falling diagonal (a8-h1 direction) the square lies on: squares share one if and only if
    /// they have the same value, 0 to 14.
    /// </summary>
    /// <param name="square">The square to locate.</param>
    /// <returns>The index of the falling diagonal of the given square.</returns>
    public static int FallingDiagonal(int square)
    {
        return File(square) - Rank(square) + 7;
    }

    /// <summary>
    /// Returns whether two different squares lie on one file, rank or diagonal, so that a rook, bishop or queen
    /// could travel between them on an empty board.
    /// </summary>
    /// <param name="from">The first square.</param>
    /// <param name="to">The second square.</param>
    /// <returns>Whether the squares are different and share a file, rank or diagonal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A square is outside 0 to 63.</exception>
    public static bool SameLine(int from, int to)
    {
        CheckSquare(from, nameof(from));
        CheckSquare(to, nameof(to));
        return from != to
            && (File(from) == File(to) || Rank(from) == Rank(to)
                || RisingDiagonal(from) == RisingDiagonal(to) || FallingDiagonal(from) == FallingDiagonal(to));
    }

    /// <summary>
    /// Returns whether a knight on one square attacks the other.
    /// </summary>
    /// <param name="from">The first square.</param>
    /// <param name="to">The second square.</param>
    /// <returns>Whether the squares are a knight's move apart.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A square is outside 0 to 63.</exception>
    public static bool IsKnightStep(int from, int to)
    {
        CheckSquare(from, nameof(from));
        CheckSquare(to, nameof(to));
        int files = Math.Abs(File(from) - File(to));
        int ranks = Math.Abs(Rank(from) - Rank(to));
        return files * ranks == 2;
    }

    /// <summary>
    /// Returns the squares strictly between two squares on one file, rank or diagonal, nearest to
    /// <paramref name="from"/> first; empty when the squares do not share a line or are adjacent.
    /// </summary>
    /// <param name="from">The first square.</param>
    /// <param name="to">The second square.</param>
    /// <returns>The squares between the two.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A square is outside 0 to 63.</exception>
    public static IReadOnlyList<int> Between(int from, int to)
    {
        List<int> squares = new List<int>();
        if (!SameLine(from, to))
            return squares;

        int fileStep = Math.Sign(File(to) - File(from));
        int rankStep = Math.Sign(Rank(to) - Rank(from));
        int step = fileStep + rankStep * 8;
        for (int square = from + step; square != to; square += step)
            squares.Add(square);
        return squares;
    }

    private static void CheckSquare(int square, string parameter)
    {
        if ((uint)square > 63)
            throw new ArgumentOutOfRangeException(parameter);
    }
}
