using System;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Hashing;

/// <summary>
/// Provides methods for Zobrist hashing.
/// </summary>
static class Zobrist
{
    /// <summary>
    /// The table giving the hash value for a given piece on a given square.
    /// </summary>
    public static readonly ulong[][] PiecePosition = new ulong[Piece.Max + 1][];

    /// <summary>
    /// The table giving the hash value for a set of castling rights, indexed by the
    /// <see cref="CastlingRights"/> value: the XOR of the keys of the rights in the set.
    /// </summary>
    public static readonly ulong[] Castling = new ulong[16];

    /// <summary>
    /// The table giving the hash value for ability to perform en passant on a
    /// given square. Only the file matters, so every square of a file shares a key. The key is
    /// applied after any two-square pawn advance, whether or not a capture is actually possible,
    /// so positions that differ only in an unusable en passant square hash differently.
    /// </summary>
    public static readonly ulong[] EnPassant = new ulong[64];

    /// <summary>
    /// The hash value for black side to move.
    /// </summary>
    public static readonly ulong ColourToMove;

    /// <summary>
    /// The seed for the pseudorandom number generator used to generate the hash
    /// values.
    /// </summary>
    private static ulong _seed = 0xA42F59FEB1F6ECEDUL;

    /// <summary>
    /// Initializes hash values.
    /// </summary>
    static Zobrist()
    {
        for (int piece = Piece.Min; piece <= Piece.Max; piece++)
        {
            PiecePosition[piece] = new ulong[64];
            for (int square = 0; square < 64; square++)
                PiecePosition[piece][square] = NextUInt64();
        }

        // One key per castling right, in the order of the CastlingRights bits (white kingside,
        // white queenside, black kingside, black queenside); a set of rights hashes to their XOR.
        ulong[] rightKeys = new ulong[4];
        for (int right = 0; right < rightKeys.Length; right++)
            rightKeys[right] = NextUInt64();
        for (int rights = 0; rights < Castling.Length; rights++)
            for (int right = 0; right < rightKeys.Length; right++)
                if ((rights & (1 << right)) != 0)
                    Castling[rights] ^= rightKeys[right];

        for (int file = 0; file < 8; file++)
        {
            ulong hashValue = NextUInt64();
            for (int rank = 0; rank < 8; rank++)
                EnPassant[file + rank * 8] = hashValue;
        }

        ColourToMove = NextUInt64();
    }

    /// <summary>
    /// Returns a pseudorandom unsigned 64-bit integer.
    /// </summary>
    /// <returns>A pseudorandom unsigned 64-bit integer.</returns>
    private static ulong NextUInt64()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 7;
        _seed ^= _seed << 17;
        return _seed;
    }
}
