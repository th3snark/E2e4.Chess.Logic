#if NET
using System.Numerics;
#endif
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Bitboards;

/// <summary>
/// Provides methods and constants for bitwise operations. A bitboard is a 64-bit set of
/// squares: bit <c>s</c> is square <c>s</c>, numbered 0 = a8 .. 63 = h1 like the rest of the
/// library. On modern runtimes the bit scans use the CPU's intrinsics through
/// <c>BitOperations</c>; older targets use <see cref="PortableBitOperations"/>, which behaves identically
/// and is checked against the intrinsics by the unit tests.
/// </summary>
internal static class Bitboard
{
    /// <summary>RayN[s] gives a bitboard of the ray of squares strictly to the north of square s.</summary>
    public static readonly ulong[] RayN = new ulong[64];

    /// <summary>RayE[s] gives a bitboard of the ray of squares strictly to the east of square s.</summary>
    public static readonly ulong[] RayE = new ulong[64];

    /// <summary>RayS[s] gives a bitboard of the ray of squares strictly to the south of square s.</summary>
    public static readonly ulong[] RayS = new ulong[64];

    /// <summary>RayW[s] gives a bitboard of the ray of squares strictly to the west of square s.</summary>
    public static readonly ulong[] RayW = new ulong[64];

    /// <summary>RayNE[s] gives a bitboard of the ray of squares strictly to the northeast of square s.</summary>
    public static readonly ulong[] RayNE = new ulong[64];

    /// <summary>RayNW[s] gives a bitboard of the ray of squares strictly to the northwest of square s.</summary>
    public static readonly ulong[] RayNW = new ulong[64];

    /// <summary>RaySE[s] gives a bitboard of the ray of squares strictly to the southeast of square s.</summary>
    public static readonly ulong[] RaySE = new ulong[64];

    /// <summary>RaySW[s] gives a bitboard of the ray of squares strictly to the southwest of square s.</summary>
    public static readonly ulong[] RaySW = new ulong[64];

    /// <summary>
    /// The horizontal and vertical rays extending from a given square. Axes[s] gives a bitboard
    /// of the squares on the same rank and file as square s, not including s itself.
    /// </summary>
    public static readonly ulong[] Axes = new ulong[64];

    /// <summary>
    /// The diagonal rays extending from a given square. Diagonals[s] gives a bitboard of the
    /// squares along the diagonals of square s, not including s itself.
    /// </summary>
    public static readonly ulong[] Diagonals = new ulong[64];

    /// <summary>
    /// The bitboard of all light squares.
    /// </summary>
    public const ulong LightSquares = 0xAA55AA55AA55AA55UL;

    /// <summary>
    /// Initializes lookup tables.
    /// </summary>
    static Bitboard()
    {
        for (int square = 0; square < 64; square++)
        {
            // Initialize ray tables.
            RayN[square] = LineFill(square, 0, -1) ^ (1UL << square);
            RayE[square] = LineFill(square, 1, 0) ^ (1UL << square);
            RayS[square] = LineFill(square, 0, 1) ^ (1UL << square);
            RayW[square] = LineFill(square, -1, 0) ^ (1UL << square);
            RayNE[square] = LineFill(square, 1, -1) ^ (1UL << square);
            RayNW[square] = LineFill(square, -1, -1) ^ (1UL << square);
            RaySE[square] = LineFill(square, 1, 1) ^ (1UL << square);
            RaySW[square] = LineFill(square, -1, 1) ^ (1UL << square);
            Axes[square] = RayN[square] | RayE[square] | RayS[square] | RayW[square];
            Diagonals[square] = RayNE[square] | RayNW[square] | RaySE[square] | RaySW[square];
        }
    }

    /// <summary>
    /// Removes and returns the index of the least significant set bit in the given bitboard,
    /// which must not be empty.
    /// </summary>
    /// <param name="bitboard">The bitboard to pop.</param>
    /// <returns>The index of the least significant set bit.</returns>
    public static int Pop(ref ulong bitboard)
    {
        int index = Scan(bitboard);
        bitboard &= bitboard - 1;
        return index;
    }

    /// <summary>
    /// Returns the index of the bit in a bitboard with a single set bit.
    /// </summary>
    /// <param name="bitboard">The bitboard to read.</param>
    /// <returns>The index of the single set bit.</returns>
    public static int Read(ulong bitboard)
    {
        return Scan(bitboard);
    }

    /// <summary>
    /// Returns the index of the least significant set bit in the given bitboard, which must not
    /// be empty.
    /// </summary>
    /// <param name="bitboard">The bitboard to scan.</param>
    /// <returns>The index of the least significant set bit.</returns>
    public static int Scan(ulong bitboard)
    {
#if NET
        return BitOperations.TrailingZeroCount(bitboard);
#else
        return PortableBitOperations.TrailingZeroCount(bitboard);
#endif
    }

    /// <summary>
    /// Returns the index of the most significant set bit in the given bitboard, which must not
    /// be empty.
    /// </summary>
    /// <param name="bitboard">The bitboard to scan.</param>
    /// <returns>The index of the most significant set bit.</returns>
    public static int ScanReverse(ulong bitboard)
    {
#if NET
        return 63 - BitOperations.LeadingZeroCount(bitboard);
#else
        return PortableBitOperations.HighestSetBit(bitboard);
#endif
    }

    /// <summary>
    /// Returns the given bitboard with only the least significant bit set.
    /// </summary>
    /// <param name="bitboard">The bitboard to isolate the least significant bit.</param>
    /// <returns>The given bitboard with only the least significant bit set.</returns>
    public static ulong Isolate(ulong bitboard)
    {
        return bitboard & (0UL - bitboard);
    }

    /// <summary>
    /// Returns the given bitboard with only the most significant bit set.
    /// </summary>
    /// <param name="bitboard">The bitboard to isolate the most significant bit.</param>
    /// <returns>The given bitboard with only the most significant bit set.</returns>
    public static ulong IsolateReverse(ulong bitboard)
    {
#if NET
        return bitboard == 0 ? 0UL : 1UL << (63 - BitOperations.LeadingZeroCount(bitboard));
#else
        return PortableBitOperations.IsolateHighestBit(bitboard);
#endif
    }

    /// <summary>
    /// Returns the number of set bits in the given bitboard.
    /// </summary>
    /// <param name="bitboard">The bitboard to count.</param>
    /// <returns>The number of set bits.</returns>
    public static int Count(ulong bitboard)
    {
#if NET
        return BitOperations.PopCount(bitboard);
#else
        return PortableBitOperations.PopCount(bitboard);
#endif
    }

    /// <summary>
    /// Returns the number of set bits in the given bitboard by clearing one bit at a time. A
    /// reference implementation of <see cref="Count"/> that is also fast for very sparse
    /// bitboards.
    /// </summary>
    /// <param name="bitboard">The bitboard to count.</param>
    /// <returns>The number of set bits.</returns>
    public static int CountSparse(ulong bitboard)
    {
        int count = 0;
        while (bitboard != 0)
        {
            count++;
            bitboard &= bitboard - 1;
        }
        return count;
    }

    /// <summary>
    /// Returns a bitboard that has set bits along a given line, starting at the given square
    /// and stepping by (dx, dy) until the edge of the board.
    /// </summary>
    /// <param name="index">A point on the line.</param>
    /// <param name="dx">The x component of the line's direction vector.</param>
    /// <param name="dy">The y component of the line's direction vector.</param>
    /// <returns>The bitboard that is the result of the line fill.</returns>
    public static ulong LineFill(int index, int dx, int dy)
    {
        if (index < 0 || index > 63)
            return 0;
        ulong bitboard = 1UL << index;

        // Stay on the same rank: the rank of a square is index / 8 (floor, so -1 is rank -1).
        if ((index >> 3) == ((index + dx) >> 3))
            bitboard |= LineFill(index + dx + dy * 8, dx, dy);
        return bitboard;
    }
}
