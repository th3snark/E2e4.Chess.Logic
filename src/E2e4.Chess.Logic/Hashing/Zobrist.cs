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
    public static readonly UInt64[][] PiecePosition = new UInt64[Piece.Max + 1][];

    /// <summary>
    /// The table giving the hash value for a set of castling rights, indexed by the
    /// <see cref="CastlingRights"/> value: the XOR of the keys of the rights in the set.
    /// </summary>
    public static readonly UInt64[] Castling = new UInt64[16];

    /// <summary>
    /// The table giving the hash value for ability to perform en passant on a
    /// given square. Only the file matters, so every square of a file shares a key. The key is
    /// applied after any two-square pawn advance, whether or not a capture is actually possible,
    /// so positions that differ only in an unusable en passant square hash differently.
    /// </summary>
    public static readonly UInt64[] EnPassant = new UInt64[64];

    /// <summary>
    /// The hash value for black side to move.
    /// </summary>
    public static readonly UInt64 ColourToMove;

    /// <summary>
    /// The seed for the pseudorandom number generator used to generate the hash
    /// values.
    /// </summary>
    private static UInt64 _seed = 0xA42F59FEB1F6ECEDUL;

    /// <summary>
    /// Initializes hash values.
    /// </summary>
    static Zobrist()
    {
        for (Int32 piece = Piece.Min; piece <= Piece.Max; piece++)
        {
            PiecePosition[piece] = new UInt64[64];
            for (Int32 square = 0; square < 64; square++)
                PiecePosition[piece][square] = NextUInt64();
        }

        // One key per castling right, in the order of the CastlingRights bits (white kingside,
        // white queenside, black kingside, black queenside); a set of rights hashes to their XOR.
        UInt64[] rightKeys = new UInt64[4];
        for (Int32 right = 0; right < rightKeys.Length; right++)
            rightKeys[right] = NextUInt64();
        for (Int32 rights = 0; rights < Castling.Length; rights++)
            for (Int32 right = 0; right < rightKeys.Length; right++)
                if ((rights & (1 << right)) != 0)
                    Castling[rights] ^= rightKeys[right];

        for (Int32 file = 0; file < 8; file++)
        {
            UInt64 hashValue = NextUInt64();
            for (Int32 rank = 0; rank < 8; rank++)
                EnPassant[file + rank * 8] = hashValue;
        }

        ColourToMove = NextUInt64();
    }

    /// <summary>
    /// Returns a pseudorandom unsigned 64-bit integer.
    /// </summary>
    /// <returns>A pseudorandom unsigned 64-bit integer.</returns>
    private static UInt64 NextUInt64()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 7;
        _seed ^= _seed << 17;
        return _seed;
    }
}
