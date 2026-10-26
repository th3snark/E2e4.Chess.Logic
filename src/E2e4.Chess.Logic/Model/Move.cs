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
    public const int Invalid = 0;

    /// <summary>
    /// The amount the to square is shifted when encoding the move.
    /// </summary>
    private const int ToShift = 6;

    /// <summary>
    /// The amount the moving piece is shifted when encoding the move.
    /// </summary>
    private const int PieceShift = ToShift + 6;

    /// <summary>
    /// The amount the captured piece is shifted when encoding the move.
    /// </summary>
    private const int CaptureShift = PieceShift + 4;

    /// <summary>
    /// The amount the special piece is shifted when encoding the move.
    /// </summary>
    private const int SpecialShift = CaptureShift + 4;

    /// <summary>
    /// The mask for extracting the unshifted square from a move.
    /// </summary>
    private const int SquareMask = (1 << 6) - 1;

    /// <summary>
    /// The mask for extracting an unshifted piece from a move.
    /// </summary>
    private const int PieceMask = (1 << 4) - 1;

    /// <summary>
    /// The mask for extracting the shifted type of the captured piece from a
    /// move.
    /// </summary>
    private const int TypeCaptureShifted = Piece.Mask << CaptureShift;

    /// <summary>
    /// The value of the captured empty piece exactly as it is represented in
    /// the move.
    /// </summary>
    private const int EmptyCaptureShifted = Piece.Empty << CaptureShift;

    /// <summary>
    /// The mask for extracting the shifted type of the special piece from a move.
    /// </summary>
    private const int TypeSpecialShifted = Piece.Mask << SpecialShift;

    /// <summary>
    /// The value of the king special (castling) exactly as it is represented in
    /// the move.
    /// </summary>
    private const int KingSpecialShifted = Piece.King << SpecialShift;

    /// <summary>
    /// The value of the pawn special (en passant) exactly as it is represented
    /// in the move.
    /// </summary>
    private const int PawnSpecialShifted = Piece.Pawn << SpecialShift;

    /// <summary>
    /// The value of the queen special (promotion to queen) exactly as it is
    /// represented in the move.
    /// </summary>
    private const int QueenSpecialShifted = Piece.Queen << SpecialShift;

    /// <summary>
    /// The mask for extracting the shifted type of the moving piece from a move.
    /// </summary>
    private const int TypePieceShifted = Piece.Mask << PieceShift;

    /// <summary>
    /// The value of the moving pawn exactly as it is represented in the move.
    /// </summary>
    private const int PawnPieceShifted = Piece.Pawn << PieceShift;

    /// <summary>
    /// Returns the pawn's move bitboard for the given square as the given
    /// colour.
    /// </summary>
    /// <param name="square">The square the pawn is on.</param>
    /// <param name="colour">The colour of the pawn.</param>
    /// <returns>The pawn's move bitboard.</returns>
    public static ulong Pawn(int square, int colour)
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
    public static int Create(Position position, int from, int to, int special = Piece.Empty)
    {
        if ((uint)from > 63)
            throw new ArgumentOutOfRangeException(nameof(from));
        if ((uint)to > 63)
            throw new ArgumentOutOfRangeException(nameof(to));
        return from | (to << ToShift) | (position.Board[from] << PieceShift) | (position.Board[to] << CaptureShift) | (special << SpecialShift);
    }

    /// <summary>
    /// Returns the legal move from one square to another, or <see cref="Invalid"/> when there is none. Unlike
    /// <see cref="Create(Position, int, int, int)"/>, which encodes whatever it is given, this tells whether a
    /// user's drag or arrow is a move, and it fills in the castling, en passant and promotion flags.
    /// </summary>
    /// <param name="position">The position the move is to be played on.</param>
    /// <param name="from">The from square of the move.</param>
    /// <param name="to">The to square of the move.</param>
    /// <param name="promotion">The piece kind to promote to (<see cref="Piece.Queen"/>, <see cref="Piece.Rook"/>,
    /// <see cref="Piece.Bishop"/> or <see cref="Piece.Knight"/>); <see cref="Piece.Empty"/> means a queen when
    /// the move is a promotion.</param>
    /// <returns>The legal move, or <see cref="Invalid"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A square is outside 0 to 63.</exception>
    public static int Find(Position position, int from, int to, int promotion = Piece.Empty)
    {
        if ((uint)from > 63)
            throw new ArgumentOutOfRangeException(nameof(from));
        if ((uint)to > 63)
            throw new ArgumentOutOfRangeException(nameof(to));

        if (promotion == Piece.Empty)
            promotion = Piece.Queen;
        foreach (int move in position.LegalMovesFrom(from))
        {
            if (To(move) == to && (!IsPromotion(move) || (SpecialPiece(move) & Piece.Mask) == promotion))
                return move;
        }
        return Invalid;
    }

    /// <summary>
    /// Returns a move to be played on the given position that has the given
    /// representation in coordinate notation.
    /// </summary>
    /// <param name="position">The position the move is to be played on.</param>
    /// <param name="name">The representation of the move in coordinate notation.</param>
    /// <returns>A move that has the given representation in coordinate notation.</returns>
    public static int Create(Position position, string? name)
    {
        // Coordinate notation is "e2e4", with a promotion piece suffix such as "e7e8q"; case is ignored.
        if (name == null || (name.Length != 4 && name.Length != 5))
            return Invalid;

        int from = ParseSquare(name[0], name[1]);
        int to = ParseSquare(name[2], name[3]);
        if (from < 0 || to < 0)
            return Invalid;

        int promotion = Piece.Empty;
        if (name.Length == 5)
        {
            switch (char.ToLowerInvariant(name[4]))
            {
                case 'q': promotion = Piece.Queen; break;
                case 'r': promotion = Piece.Rook; break;
                case 'b': promotion = Piece.Bishop; break;
                case 'n': promotion = Piece.Knight; break;
                default: return Invalid;
            }
        }

        int[] moves = new int[Position.MaxMoves];
        int count = position.LegalMoves(moves);
        for (int i = 0; i < count; i++)
        {
            int move = moves[i];
            if (From(move) == from && To(move) == to
             && (IsPromotion(move) ? (SpecialPiece(move) & Piece.Mask) == promotion : promotion == Piece.Empty))
                return move;
        }
        return Invalid;
    }

    /// <summary>
    /// Returns the square for a file letter and rank digit, or -1 if they do not name a square.
    /// </summary>
    private static int ParseSquare(char file, char rank)
    {
        file = char.ToLowerInvariant(file);
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return -1;
        return file - 'a' + ('8' - rank) * 8;
    }

    /// <summary>
    /// Returns the from square of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The from square of the given move.</returns>
    public static int From(int move)
    {
        return move & SquareMask;
    }

    /// <summary>
    /// Returns the to square of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The to square of the given move.</returns>
    public static int To(int move)
    {
        return (move >> ToShift) & SquareMask;
    }

    /// <summary>
    /// Returns the moving piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The moving piece of the given move.</returns>
    public static int MovingPiece(int move)
    {
        return (move >> PieceShift) & PieceMask;
    }

    /// <summary>
    /// Returns the captured piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The captured piece of the given move.</returns>
    public static int CapturedPiece(int move)
    {
        return (move >> CaptureShift) & PieceMask;
    }

    /// <summary>
    /// Returns the special piece of the given move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>The special piece of the given move.</returns>
    public static int SpecialPiece(int move)
    {
        return move >> SpecialShift;
    }

    /// <summary>
    /// Returns whether the given move captures an opposing piece.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move captures an opposing piece.</returns>
    public static bool IsCapture(int move)
    {
        return (move & TypeCaptureShifted) != EmptyCaptureShifted;
    }

    /// <summary>
    /// Returns whether the given move is a castling move.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move is a castling move.</returns>
    public static bool IsCastle(int move)
    {
        return (move & TypeSpecialShifted) == KingSpecialShifted;
    }

    /// <summary>
    /// Returns whether the given move promotes a pawn.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move promotes a pawn.</returns>
    public static bool IsPromotion(int move)
    {
        if ((move & TypePieceShifted) != PawnPieceShifted)
            return false;
        int to = (move >> ToShift) & SquareMask;
        return (to - 8) * (to - 55) > 0;
    }

    /// <summary>
    /// Returns whether the given move is en passant.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move is en passant.</returns>
    public static bool IsEnPassant(int move)
    {
        return (move & TypeSpecialShifted) == PawnSpecialShifted;
    }

    /// <summary>
    /// Returns whether the given move promotes a pawn to a queen.
    /// </summary>
    /// <param name="move">The move to decode.</param>
    /// <returns>Whether the given move promotes a pawn to a queen.</returns>
    public static bool IsQueenPromotion(int move)
    {
        return (move & TypeSpecialShifted) == QueenSpecialShifted;
    }

    /// <summary>
    /// Returns the text representation of the given move in coordinate notation.
    /// </summary>
    /// <param name="move">The move to identify.</param>
    /// <returns>The text representation of the given move in coordinate notation.</returns>
    public static string GetCoordinateNotation(int move)
    {
        string coordinates = Square.GetName(Move.From(move)) + Square.GetName(Move.To(move));
        int special = Move.SpecialPiece(move);

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
    public static string GetCoordinateNotation(List<int> moves)
    {
        if (moves.Count == 0)
            return "";
        StringBuilder sb = new StringBuilder(6 * moves.Count);
        foreach (int move in moves)
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
    public static string GetAlgebraicNotation(Position position, int move)
    {
        if (Move.IsCastle(move))
            return (Move.To(move) < Move.From(move) ? "O-O-O" : "O-O") + GetCheckSuffix(position, move);

        // Determine the piece associated with the move. Pawns are not explicitly
        // identified.
        string piece = (Move.MovingPiece(move) & Piece.Mask) == Piece.Pawn ? "" : Piece.GetInitial(Move.MovingPiece(move));

        // Determine the necessary disambiguation property for the move. If two or
        // more pieces of the same type are moving to the same square, disambiguate
        // with the square that it is moving from's file, rank, or both, in that
        // order.
        string disambiguation = "";
        List<int> alternatives = new List<int>();
        foreach (int alt in position.LegalMoves())
            if (Move.From(alt) != Move.From(move) && Move.MovingPiece(alt) == Move.MovingPiece(move) && Move.To(alt) == Move.To(move))
                alternatives.Add(alt);

        if (alternatives.Count > 0)
        {
            bool uniqueFile = true;
            bool uniqueRank = true;
            foreach (int alt in alternatives)
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
        bool isCapture = Move.IsCapture(move) || Move.IsEnPassant(move);
        string capture = isCapture ? "x" : "";
        if ((Move.MovingPiece(move) & Piece.Mask) == Piece.Pawn && isCapture)
            if (disambiguation == "")
                disambiguation = Square.GetFileName(Move.From(move));

        // Determine the square property for the move.
        string square = Square.GetName(Move.To(move));

        // Determine the necessary promotion property for the move.
        string promotion = Move.IsPromotion(move) ? "=" + Piece.GetInitial(Move.SpecialPiece(move)) : "";

        return piece + disambiguation + capture + square + promotion + GetCheckSuffix(position, move);
    }

    /// <summary>
    /// Returns "+" if the given move gives check, "#" if it gives checkmate, and
    /// the empty string otherwise.
    /// </summary>
    private static string GetCheckSuffix(Position position, int move)
    {
        string check = "";
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
    public static string GetAlgebraicNotation(Position position, List<int> moves, AlgebraicNotationFormat moveFormat = AlgebraicNotationFormat.None)
    {
        if (moves.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();
        bool proper = moveFormat == AlgebraicNotationFormat.Proper;
        int halfMoves = position.HalfMoves;

        if (position.SideToMove == Colour.Black)
        {
            sb.Append(halfMoves / 2 + 1);
            sb.Append(proper ? "... " : "...");
        }

        int made = 0;
        try
        {
            foreach (int move in moves)
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
            for (int i = made - 1; i >= 0; i--)
                position.Unmake(moves[i]);
        }

        return sb.ToString(0, sb.Length - 1);
    }
}
