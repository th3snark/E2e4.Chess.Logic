using System;
using E2e4.Chess.Logic.Hashing;

namespace E2e4.Chess.Logic.Model;

public sealed partial class Position : IEquatable<Position>
{
    /// <summary>
    /// Generates the hash key for the position from scratch. This is expensive
    /// and should only be used for initialization and testing. Normally, use the
    /// incrementally updated <see cref="ZobristKey"/>.
    /// </summary>
    /// <returns>The hash key for the position.</returns>
    public ulong GetZobristKey()
    {
        ulong key = 0;

        for (int square = 0; square < Board.Length; square++)
            if (Board[square] != Piece.Empty)
                key ^= Zobrist.PiecePosition[Board[square]][square];

        if (EnPassantSquare != InvalidSquare)
            key ^= Zobrist.EnPassant[EnPassantSquare];

        if (SideToMove != Colour.White)
            key ^= Zobrist.ColourToMove;

        key ^= Zobrist.Castling[(int)Castling];
        return key;
    }

}
