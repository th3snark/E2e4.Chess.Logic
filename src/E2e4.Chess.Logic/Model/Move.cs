using System;
using System.Collections.Generic;
using System.Text;
using E2e4.Chess.Logic.Enums;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Provides methods for move encoding and decoding.
/// </summary>
public static class Move
{
    /// <summary>
    /// The value representing an invalid move.
    /// </summary>
    public const Int32 Invalid = 0;

    /// <summary>
    /// The amount the to square is shifted when encoding the move.
    /// </summary>
    private const Int32 ToShift = 6;

    /// <summary>
    /// The amount the moving piece is shifted when encoding the move.
    /// </summary>
    private const Int32 PieceShift = ToShift + 6;

    /// <summary>
    /// The amount the captured piece is shifted when encoding the move.
    /// </summary>
    private const Int32 CaptureShift = PieceShift + 4;

    /// <summary>
    /// The amount the special piece is shifted when encoding the move.
    /// </summary>
    private const Int32 SpecialShift = CaptureShift + 4;

    /// <summary>
    /// The mask for extracting the unshifted square from a move.
    /// </summary>
    private const Int32 SquareMask = (1 << 6) - 1;

    /// <summary>
    /// The mask for extracting an unshifted piece from a move.
    /// </summary>
    private const Int32 PieceMask = (1 << 4) - 1;

    /// <summary>
    /// The mask for extracting the shifted type of the captured piece from a
    /// move.
    /// </summary>
    private const Int32 TypeCaptureShifted = Piece.Mask << CaptureShift;

    /// <summary>
    /// The value of the captured empty piece exactly as it is represented in
    /// the move.
    /// </summary>
    private const Int32 EmptyCaptureShifted = Piece.Empty << CaptureShift;

    /// <summary>
    /// The mask for extracting the shifted type of the special piece from a move.
    /// </summary>
    private const Int32 TypeSpecialShifted = Piece.Mask << SpecialShift;

    /// <summary>
    /// The value of the king special (castling) exactly as it is represented in
    /// the move.
    /// </summary>
    private const Int32 KingSpecialShifted = Piece.King << SpecialShift;

    /// <summary>
    /// The value of the pawn special (en passant) exactly as it is represented
    /// in the move.
    /// </summary>
    private const Int32 PawnSpecialShifted = Piece.Pawn << SpecialShift;

    /// <summary>
    /// The value of the queen special (promotion to queen) exactly as it is
    /// represented in the move.
    /// </summary>
    private const Int32 QueenSpecialShifted = Piece.Queen << SpecialShift;

    /// <summary>
    /// The mask for extracting the shifted type of the moving piece from a move.
    /// </summary>
    private const Int32 TypePieceShifted = Piece.Mask << PieceShift;

    /// <summary>
    /// The value of the moving pawn exactly as it is represented in the move.
    /// </summary>
    private const Int32 PawnPieceShifted = Piece.Pawn << PieceShift;

    /// <summary>
    /// Returns the pawn's move bitboard for the given square as the given
    /// colour.
    /// </summary>
    /// <param name="square">The square the pawn is on.</param>
    /// <param name="colour">The colour of the pawn.</param>
    /// <returns>The pawn's move bitboard.</returns>
    public static UInt64 Pawn(Int32 square, Int32 colour)
    {
        return 1UL << (square + 16 * colour - 8);
    }

    /// <summary>
    /// Returns a move encoded from the given parameters.
    /// </summary>
    /// <param name="position">The position the move is to be played on.</param>
    /// <param name="from">The from square of the move.</param>
    /// <param name="to">The to square of the move.</param>
    /// <param name="special">The special piece of the move.</param>
    /// <returns>A move encoded from the given parameters.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A square is outside 0 to 63.</exception>
    public static Int32 Create(Position position, Int32 from, Int32 to, Int32 special = Piece.Empty)
    {
        if ((UInt32)from > 63)
            throw new ArgumentOutOfRangeException(nameof(from));
        if ((UInt32)to > 63)
            throw new ArgumentOutOfRangeException(nameof(to));
        return from | (to << ToShift) | (position.Board[from] << PieceShift) | (position.Board[to] << CaptureShift) | (special << SpecialShift);
    }

    /// <summary>
    /// Returns a move to be played on the given position that has the given
    /// representation in coordinate notation.
    /// </summary>
    /// <param name="position">The position the move is to be played on.</param>
    /// <param name="name">The representation of the move in coordinate notation.</param>
    /// <returns>A move that has the given representation in coordinate notation.</returns>
    public static Int32 Create(Position position, String? name)
    {
        // Coordinate notation is "e2e4", with a promotion piece suffix such as "e7e8q"; case is ignored.
        if (name == null || (name.Length != 4 && name.Length != 5))
            return Invalid;

        Int32 from = ParseSquare(name[0], name[1]);
        Int32 to = ParseSquare(name[2], name[3]);
        if (from < 0 || to < 0)
            return Invalid;

        Int32 promotion = Piece.Empty;
        if (name.Length == 5)
        {
            switch (Char.ToLowerInvariant(name[4]))
            {
                case 'q': promotion = Piece.Queen; break;
                case 'r': promotion = Piece.Rook; break;
                case 'b': promotion = Piece.Bishop; break;
                case 'n': promotion = Piece.Knight; break;
                default: return Invalid;
            }
        }

        Int32[] moves = new Int32[Position.MaxMoves];
        Int32 count = position.LegalMoves(moves);
        for (Int32 i = 0; i < count; i++)
        {
            Int32 move = moves[i];
            if (From(move) == from && To(move) == to
             && (IsPromotion(move) ? (SpecialPiece(move) & Piece.Mask) == promotion : promotion == Piece.Empty))
                return move;
        }
        return Invalid;
    }

    /// <summary>
    /// Returns the square for a file letter and rank digit, or -1 if they do not name a square.
    /// </summary>
    private static Int32 ParseSquare(Char file, Char rank)
    {
        file = Char.ToLowerInvariant(file);
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return -1;
        return file - 'a' + ('8' - rank) * 8;
    }

    /// <summary>
    /// Returns the from square of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The from square of the given move.</returns>
    public static Int32 From(Int32 move)
    {
        return move & SquareMask;
    }

    /// <summary>
    /// Returns the to square of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The to square of the given move.</returns>
    public static Int32 To(Int32 move)
    {
        return (move >> ToShift) & SquareMask;
    }

    /// <summary>
    /// Returns the moving piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The moving piece of the given move.</returns>
    public static Int32 MovingPiece(Int32 move)
    {
        return (move >> PieceShift) & PieceMask;
    }

    /// <summary>
    /// Returns the captured piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The captured piece of the given move.</returns>
    public static Int32 CapturedPiece(Int32 move)
    {
        return (move >> CaptureShift) & PieceMask;
    }

    /// <summary>
    /// Returns the special piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The special piece of the given move.</returns>
    public static Int32 SpecialPiece(Int32 move)
    {
        return move >> SpecialShift;
    }

    /// <summary>
    /// Returns whether the given move captures an opposing piece.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move captures an opposing piece.</returns>
    public static Boolean IsCapture(Int32 move)
    {
        return (move & TypeCaptureShifted) != EmptyCaptureShifted;
    }

    /// <summary>
    /// Returns whether the given move is a castling move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move is a castling move.</returns>
    public static Boolean IsCastle(Int32 move)
    {
        return (move & TypeSpecialShifted) == KingSpecialShifted;
    }

    /// <summary>
    /// Returns whether the given move promotes a pawn.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move promotes a pawn.</returns>
    public static Boolean IsPromotion(Int32 move)
    {
        if ((move & TypePieceShifted) != PawnPieceShifted)
            return false;
        Int32 to = (move >> ToShift) & SquareMask;
        return (to - 8) * (to - 55) > 0;
    }

    /// <summary>
    /// Returns whether the given move is en passant.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move is en passant.</returns>
    public static Boolean IsEnPassant(Int32 move)
    {
        return (move & TypeSpecialShifted) == PawnSpecialShifted;
    }

    /// <summary>
    /// Returns whether the given move promotes a pawn to a queen.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move promotes a pawn to a queen.</returns>
    public static Boolean IsQueenPromotion(Int32 move)
    {
        return (move & TypeSpecialShifted) == QueenSpecialShifted;
    }

    /// <summary>
    /// Returns the text representation of the given move in coordinate notation.
    /// </summary>
    /// <param name="move">The move to identify.</param>
    /// <returns>The text representation of the given move in coordinate notation.</returns>
    public static String GetCoordinateNotation(Int32 move)
    {
        String coordinates = Square.GetName(Move.From(move)) + Square.GetName(Move.To(move));
        Int32 special = Move.SpecialPiece(move);

        switch (special & Piece.Mask)
        {
            default:
                return coordinates;
            case Piece.Queen:
            case Piece.Rook:
            case Piece.Bishop:
            case Piece.Knight:
                return coordinates + Piece.GetInitial(special).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Returns the text representation of the given sequence of moves in
    /// coordinate notation.
    /// </summary>
    /// <param name="moves">The sequence of moves to identify.</param>
    /// <returns>The text representation of the given sequence of moves in coordinate notation.</returns>
    public static string GetCoordinateNotation(List<Int32> moves)
    {
        if (moves.Count == 0)
            return "";
        StringBuilder sb = new StringBuilder(6 * moves.Count);
        foreach (Int32 move in moves)
        {
            sb.Append(Move.GetCoordinateNotation(move));
            sb.Append(' ');
        }
        return sb.ToString(0, sb.Length - 1);
    }

    /// <summary>
    /// Returns the text representation of the given move in algebraic notation.
    /// </summary>
    /// <param name="position">The position on which the move is to be played.</param>
    /// <param name="move">The move to identify.</param>
    /// <returns>The text representation of the given move in algebraic notation</returns>
    public static string GetAlgebraicNotation(Position position, Int32 move)
    {
        if (Move.IsCastle(move))
            return (Move.To(move) < Move.From(move) ? "O-O-O" : "O-O") + GetCheckSuffix(position, move);

        // Determine the piece associated with the move. Pawns are not explicitly
        // identified.
        String piece = (Move.MovingPiece(move) & Piece.Mask) == Piece.Pawn ? "" : Piece.GetInitial(Move.MovingPiece(move));

        // Determine the necessary disambiguation property for the move. If two or
        // more pieces of the same type are moving to the same square, disambiguate
        // with the square that it is moving from's file, rank, or both, in that
        // order.
        String disambiguation = "";
        List<Int32> alternatives = new List<Int32>();
        foreach (Int32 alt in position.LegalMoves())
            if (Move.From(alt) != Move.From(move) && Move.MovingPiece(alt) == Move.MovingPiece(move) && Move.To(alt) == Move.To(move))
                alternatives.Add(alt);

        if (alternatives.Count > 0)
        {
            Boolean uniqueFile = true;
            Boolean uniqueRank = true;
            foreach (Int32 alt in alternatives)
            {
                if (Square.File(Move.From(alt)) == Square.File(Move.From(move)))
                    uniqueFile = false;
                if (Square.Rank(Move.From(alt)) == Square.Rank(Move.From(move)))
                    uniqueRank = false;
            }
            if (uniqueFile)
                disambiguation = Square.GetFileName(Move.From(move));
            else if (uniqueRank)
                disambiguation = Square.GetRankName(Move.From(move));
            else
                disambiguation = Square.GetName(Move.From(move));
        }

        // Determine if the capture flag is necessary for the move. If the capturing
        // piece is a pawn, it is identified by the file it is moving from.
        Boolean isCapture = Move.IsCapture(move) || Move.IsEnPassant(move);
        String capture = isCapture ? "x" : "";
        if ((Move.MovingPiece(move) & Piece.Mask) == Piece.Pawn && isCapture)
            if (disambiguation == "")
                disambiguation = Square.GetFileName(Move.From(move));

        // Determine the square property for the move.
        String square = Square.GetName(Move.To(move));

        // Determine the necessary promotion property for the move.
        String promotion = Move.IsPromotion(move) ? "=" + Piece.GetInitial(Move.SpecialPiece(move)) : "";

        return piece + disambiguation + capture + square + promotion + GetCheckSuffix(position, move);
    }

    /// <summary>
    /// Returns "+" if the given move gives check, "#" if it gives checkmate, and
    /// the empty string otherwise.
    /// </summary>
    private static String GetCheckSuffix(Position position, Int32 move)
    {
        String check = "";
        position.Make(move);
        try
        {
            if (position.InCheck(position.SideToMove))
                check = position.LegalMoves().Count > 0 ? "+" : "#";
        }
        finally
        {
            position.Unmake(move);
        }
        return check;
    }

    /// <summary>
    /// Returns the text representation of the given sequence of moves in algebraic notation.
    /// </summary>
    /// <param name="position">The position on which the sequence of moves are to be played.</param>
    /// <param name="moves">The sequence of moves to identify.</param>
    /// <param name="moveFormat">The identification option specifying whether to be absolutely proper.</param>
    /// <remarks>The moves are played on <paramref name="position"/> and always taken back, even if
    /// an exception occurs, but the position is changed in between and is not thread-safe.</remarks>
    /// <returns>The text representation of the given sequence of moves in algebraic notation</returns>
    public static string GetAlgebraicNotation(Position position, List<Int32> moves, AlgebraicNotationFormat moveFormat = AlgebraicNotationFormat.None)
    {
        if (moves.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();
        Boolean proper = moveFormat == AlgebraicNotationFormat.Proper;
        Int32 halfMoves = position.HalfMoves;

        if (position.SideToMove == Colour.Black)
        {
            sb.Append(halfMoves / 2 + 1);
            sb.Append(proper ? "... " : "...");
        }

        Int32 made = 0;
        try
        {
            foreach (Int32 move in moves)
            {
                if ((halfMoves++ % 2) == 0)
                {
                    sb.Append(halfMoves / 2 + 1);
                    sb.Append('.');
                    if (proper)
                        sb.Append(' ');
                }
                sb.Append(Move.GetAlgebraicNotation(position, move));
                sb.Append(' ');
                position.Make(move);
                made++;
            }
        }
        finally
        {
            for (Int32 i = made - 1; i >= 0; i--)
                position.Unmake(moves[i]);
        }

        return sb.ToString(0, sb.Length - 1);
    }
}
