using System;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Bitboards;

/// <summary>
/// Provides methods for attack bitboard generation.
/// </summary>
static class AttackBitboard
{
    /// <summary>
    /// The bitboard with all bits set except those on edges and corners.
    /// </summary>
    private const ulong BorderlessBitboard = 0x007E7E7E7E7E7E00UL;

    /// <summary>
    /// The collection of king attack bitboards. KingAttack[s] gives a bitboard
    /// of the squares attacked by a king on square s.
    /// </summary>
    private static readonly ulong[] KingAttack = new ulong[64];

    /// <summary>
    /// The collection of knight attack bitboards. KnightAttack[s] gives a
    /// bitboard of the squares attacked by a knight on square s.
    /// </summary>
    private static readonly ulong[] KnightAttack = new ulong[64];

    /// <summary>
    /// The collection of pawn attack bitboards. PawnAttack[c][s] gives a
    /// bitboard of the squares attacked by a pawn of colour c on square s.
    /// </summary>
    private static readonly ulong[][] PawnAttack = { new ulong[64], new ulong[64] };

    /// <summary>
    /// Initializes lookup tables.
    /// </summary>
    static AttackBitboard()
    {
        for (int square = 0; square < 64; square++)
        {

            int file = Square.File(square);
            int rank = Square.Rank(square);

            // Initialize king attack bitboards.
            for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                    if (a != 0 || b != 0)
                        KingAttack[square] ^= TryGetBitboard(file + a, rank + b);

            // Initialize knight attack bitboards.
            for (int a = -2; a <= 2; a++)
                for (int b = -2; b <= 2; b++)
                    if (Math.Abs(a) + Math.Abs(b) == 3)
                        KnightAttack[square] ^= TryGetBitboard(file + a, rank + b);

            // Initialize pawn attack bitboards.
            PawnAttack[Colour.White][square] ^= TryGetBitboard(file - 1, rank - 1);
            PawnAttack[Colour.White][square] ^= TryGetBitboard(file + 1, rank - 1);
            PawnAttack[Colour.Black][square] ^= TryGetBitboard(file - 1, rank + 1);
            PawnAttack[Colour.Black][square] ^= TryGetBitboard(file + 1, rank + 1);
        }
    }

    /// <summary>
    /// Returns a bitboard consisting of a single filled square with the given
    /// file and rank. If an invalid square is specified the empty bitboard is
    /// returned.
    /// </summary>
    /// <param name="file">The file of the square.</param>
    /// <param name="rank">The rank of the square.</param>
    /// <returns>A bitboard consisting of a single filled square.</returns>
    public static ulong TryGetBitboard(int file, int rank)
    {
        if (file < 0 || file >= 8 || rank < 0 || rank >= 8)
            return 0;
        return 1UL << (file + rank * 8);
    }

    /// <summary>
    /// Returns the king's attack bitboard for the given square.
    /// </summary>
    /// <param name="square">The square the king is on.</param>
    /// <returns>The king's attack bitboard.</returns>
    public static ulong King(int square)
    {
        return KingAttack[square];
    }

    /// <summary>
    /// Returns the queen's attack bitboard for the given square with the given
    /// occupancy bitboard.
    /// </summary>
    /// <param name="square">The square the queen is on.</param>
    /// <param name="occupiedBitboard">The occupancy bitboard.</param>
    /// <returns>The queen's attack bitboard.</returns>
    public static ulong Queen(int square, ulong occupiedBitboard)
    {
        return Rook(square, occupiedBitboard) | Bishop(square, occupiedBitboard);
    }

    /// <summary>
    /// Returns the rook's attack bitboard for the given square with the given
    /// occupancy bitboard.
    /// </summary>
    /// <param name="square">The square the rook is on.</param>
    /// <param name="occupiedBitboard">The occupancy bitboard.</param>
    /// <returns>The rook's attack bitboard.</returns>
    public static ulong Rook(int square, ulong occupiedBitboard)
    {
        ulong attackBitboard = Bitboard.RayN[square];
        ulong blockBitboard = attackBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            attackBitboard ^= Bitboard.RayN[Bitboard.ScanReverse(blockBitboard)];

        ulong partialBitboard = Bitboard.RayE[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RayE[Bitboard.Scan(blockBitboard)];
        attackBitboard |= partialBitboard;

        partialBitboard = Bitboard.RayS[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RayS[Bitboard.Scan(blockBitboard)];
        attackBitboard |= partialBitboard;

        partialBitboard = Bitboard.RayW[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RayW[Bitboard.ScanReverse(blockBitboard)];
        return attackBitboard | partialBitboard;
    }

    /// <summary>
    /// Returns the bishop's attack bitboard for the given square with the given
    /// occupancy bitboard.
    /// </summary>
    /// <param name="square">The square the bishop is on.</param>
    /// <param name="occupiedBitboard">The occupancy bitboard.</param>
    /// <returns>The bishop's attack bitboard.</returns>
    public static ulong Bishop(int square, ulong occupiedBitboard)
    {
        occupiedBitboard &= BorderlessBitboard;

        ulong attackBitboard = Bitboard.RayNE[square];
        ulong blockBitboard = attackBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            attackBitboard ^= Bitboard.RayNE[Bitboard.ScanReverse(blockBitboard)];

        ulong partialBitboard = Bitboard.RayNW[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RayNW[Bitboard.ScanReverse(blockBitboard)];
        attackBitboard |= partialBitboard;

        partialBitboard = Bitboard.RaySE[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RaySE[Bitboard.Scan(blockBitboard)];
        attackBitboard |= partialBitboard;

        partialBitboard = Bitboard.RaySW[square];
        blockBitboard = partialBitboard & occupiedBitboard;
        if (blockBitboard != 0)
            partialBitboard ^= Bitboard.RaySW[Bitboard.Scan(blockBitboard)];
        return attackBitboard | partialBitboard;
    }

    /// <summary>
    /// Returns the knight's attack bitboard for the given square.
    /// </summary>
    /// <param name="square">The square the knight is on.</param>
    /// <returns>The knight's attack bitboard.</returns>
    public static ulong Knight(int square)
    {
        return KnightAttack[square];
    }

    /// <summary>
    /// Returns the pawn's attack bitboard for the given square as the given
    /// colour.
    /// </summary>
    /// <param name="square">The square the pawn is on.</param>
    /// <param name="colour">The colour of the pawn.</param>
    /// <returns>The pawn's attack bitboard.</returns>
    public static ulong Pawn(int square, int colour)
    {
        return PawnAttack[colour][square];
    }
}
