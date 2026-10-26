using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Encapsulates the FEN handling component of the chess position.
/// </summary>
public sealed partial class Position : IEquatable<Position>
{

    /// <summary>
    /// Attempts to create a position from the given FEN string. Unlike the
    /// constructor, which substitutes the starting position, this method reports
    /// failure for any FEN string that does not describe a playable position. The
    /// rules are those of <see cref="PositionSetup.Validate"/>: a missing counter or
    /// side to move takes its default (White to move, no castling, no en passant,
    /// clocks at their start), and any error it reports makes the parse fail, while
    /// its warnings (a position no game could reach, or one that has ended) do not.
    /// </summary>
    /// <param name="fen">The FEN string to parse.</param>
    /// <param name="position">The parsed position, or null if the FEN string is
    /// invalid.</param>
    /// <returns>Whether the FEN string was valid.</returns>
    public static bool TryParse(string? fen, [NotNullWhen(true)] out Position? position)
    {
        position = PositionSetup.TryParse(fen, out PositionSetup? setup) ? setup.ToPosition() : null;
        return position != null;
    }

    /// <summary>
    /// Builds the position of a setup that has no errors; the caller has checked.
    /// </summary>
    internal static Position Create(PositionSetup setup)
    {
        Position position = new Position();
        position.Load(setup);
        return position;
    }

    private void Load(PositionSetup setup)
    {
        for (int square = 0; square < Board.Length; square++)
        {
            int piece = setup[square];
            Board[square] = piece;
            if (piece != Piece.Empty)
            {
                int colour = piece & Colour.Mask;
                PieceBitboards[piece] |= 1UL << square;
                PieceBitboards[colour] |= 1UL << square;
                OccupiedBitboard |= 1UL << square;
            }
        }

        SideToMove = setup.SideToMove;
        Castling = setup.Castling;
        EnPassantSquare = setup.EnPassantSquare;
        FiftyMovesClock = setup.FiftyMovesClock;
        HalfMoves = (int)(2L * (setup.MoveNumber - 1) + SideToMove);

        // History starts here: moves can only be unmade back to this ply.
        _rootHalfMoves = HalfMoves;
        for (int ply = 0; ply < EnPassantHistory.Length; ply++)
            EnPassantHistory[ply] = InvalidSquare;
        EnPassantHistory[HalfMoves] = EnPassantSquare;
        FiftyMovesHistory[HalfMoves] = FiftyMovesClock;
        CastlingHistory[HalfMoves] = Castling;
        ZobristKey = GetZobristKey();
        ZobristKeyHistory[HalfMoves] = ZobristKey;
    }

    /// <summary>
    /// Returns the FEN string that describes the position.
    /// </summary>
    /// <returns>The FEN string that describes the position.</returns>
    public string GetFEN()
    {
        StringBuilder sb = new StringBuilder();

        for (int rank = 0; rank < 8; rank++)
        {
            int spaces = 0;
            for (int file = 0; file < 8; file++)
            {
                int square = file + rank * 8;
                if (Board[square] == Piece.Empty)
                    spaces++;
                else
                {
                    if (spaces > 0)
                    {
                        sb.Append(spaces);
                        spaces = 0;
                    }
                    string piece = Piece.GetInitial(Board[square]);
                    if ((Board[square] & Colour.Mask) == Colour.Black)
                        piece = piece.ToLowerInvariant();
                    sb.Append(piece);
                }
            }
            if (spaces > 0)
                sb.Append(spaces);
            if (rank < 7)
                sb.Append('/');
        }

        sb.Append(' ');
        sb.Append(SideToMove == Colour.White ? 'w' : 'b');
        sb.Append(' ');

        if ((Castling & CastlingRights.WhiteKingside) != 0)
            sb.Append('K');
        if ((Castling & CastlingRights.WhiteQueenside) != 0)
            sb.Append('Q');
        if ((Castling & CastlingRights.BlackKingside) != 0)
            sb.Append('k');
        if ((Castling & CastlingRights.BlackQueenside) != 0)
            sb.Append('q');
        if (sb[sb.Length - 1] == ' ')
            sb.Append('-');
        sb.Append(' ');

        if (EnPassantSquare != InvalidSquare)
            sb.Append(Square.GetName(EnPassantSquare));
        else
            sb.Append('-');
        sb.Append(' ');

        sb.Append(FiftyMovesClock);
        sb.Append(' ');
        sb.Append(HalfMoves / 2 + 1);

        return sb.ToString();
    }
}
