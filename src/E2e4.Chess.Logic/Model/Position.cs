using System;
using System.Collections.Generic;
using System.Text;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// A chess position together with the history needed to make and unmake moves: piece
/// placement, side to move, castling rights, en passant square, move clocks and the hash keys
/// of every position reached. Create one from a FEN string, enumerate
/// <see cref="LegalMoves()"/>, and play them with <see cref="Make"/> / <see cref="Unmake"/>.
/// </summary>
/// <remarks>
/// Instances are mutable and not thread-safe (even move generation temporarily edits the
/// position); give each thread its own copy with <see cref="DeepClone"/>.
/// </remarks>
public sealed partial class Position : IEquatable<Position>
{
    #region Public Constants

    /// <summary>
    /// The FEN string of the starting chess position.
    /// </summary>
    public const String StartingFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    /// The maximum number of plies the position is to support, counting from move one.
    /// </summary>
    public const Int32 HalfMovesLimit = 1024;

    /// <summary>The largest fifty-moves clock a FEN string may carry; larger values are rejected.</summary>
    public const Int32 MaxFiftyMovesClock = 10000;

    /// <summary>The longest FEN string that is parsed; longer text is rejected as invalid.</summary>
    public const Int32 MaxFenLength = 256;

    /// <summary>
    /// The value representing an invalid square.
    /// </summary>
    public const Int32 InvalidSquare = -1;

    /// <summary>
    /// The minimum length of the array passed to <see cref="LegalMoves(Int32[])"/>. No known 
    /// position has more than 218 legal moves; this leaves headroom. 
    /// </summary>
    public const Int32 MaxMoves = 256;

    #endregion

    #region State

    /// <summary>
    /// The pieces on the squares, 0 = a8 .. 63 = h1. Exposed read-only as <see cref="Squares"/>.
    /// </summary>
    internal readonly Int32[] Board;

    /// <summary>
    /// The sets of pieces. PieceBitboards[p] is the set of squares holding piece p, and
    /// PieceBitboards[c] for a colour c is the set of all squares holding a piece of that colour.
    /// </summary>
    internal readonly UInt64[] PieceBitboards;

    /// <summary>
    /// The set of all occupied squares.
    /// </summary>
    internal UInt64 OccupiedBitboard;

    /// <summary>
    /// The EnPassantSquare values for every ply up to and including the current ply.
    /// </summary>
    internal readonly Int32[] EnPassantHistory;

    /// <summary>
    /// The FiftyMovesClock values for every ply up to and including the current ply.
    /// </summary>
    internal readonly Int32[] FiftyMovesHistory;

    /// <summary>
    /// The ZobristKey values for every ply up to and including the current ply.
    /// </summary>
    internal readonly UInt64[] ZobristKeyHistory;

    /// <summary>
    /// The Castling values for every ply up to and including the current ply.
    /// </summary>
    internal readonly CastlingRights[] CastlingHistory;

    /// <summary>
    /// The ply the position was created at (non-zero for a FEN with a move number). Moves can only be
    /// unmade back to this point, because no history exists before it. 
    /// </summary>
    private Int32 _rootHalfMoves;

    private System.Collections.ObjectModel.ReadOnlyCollection<Int32>? _squares;

    /// <summary>
    /// The pieces on the squares of the chessboard. Squares[n] gives the piece on the nth square
    /// where 0 is a8 and 63 is h1; combine with <see cref="Piece"/> and <see cref="Colour"/>.
    /// </summary>
    public IReadOnlyList<Int32> Squares
    {
        get { return _squares ?? (_squares = Array.AsReadOnly(Board)); }
    }

    /// <summary>
    /// The colour that is to make the next move.
    /// </summary>
    public Int32 SideToMove { get; private set; }

    /// <summary>
    /// The total number of plies the position has advanced from move one.
    /// </summary>
    public Int32 HalfMoves { get; private set; }

    /// <summary>
    /// The castling moves that are still possible.
    /// </summary>
    public CastlingRights Castling { get; private set; }

    /// <summary>
    /// The square a pawn capturing en passant would move to, or <see cref="InvalidSquare"/> if
    /// the last move was not a two-square pawn advance.
    /// </summary>
    public Int32 EnPassantSquare { get; private set; }

    /// <summary>
    /// The number of plies since the last capture or pawn move, for the fifty-move rule.
    /// </summary>
    public Int32 FiftyMovesClock { get; private set; }

    /// <summary>
    /// The Zobrist hash value of the position, updated incrementally as moves are made.
    /// </summary>
    public UInt64 ZobristKey { get; private set; }

    #endregion

    #region Construction

    /// <summary>
    /// Constructs a position with the given FEN string. If the FEN string is
    /// invalid the start position is used; use <see cref="TryParse"/> to detect that instead.
    /// </summary>
    /// <param name="fen">The FEN of the position.</param>
    public Position(String fen)
        : this()
    {
        try
        {
            ParseFen(fen);
        }
        catch (FormatException)
        {
            ParseFen(StartingFEN);
        }
    }

    /// <summary>
    /// Constructs a position with an empty, invalid state, to be filled in by parsing.
    /// </summary>
    private Position()
    {
        Board = new Int32[64];
        PieceBitboards = new UInt64[16];
        EnPassantHistory = new Int32[HalfMovesLimit];
        FiftyMovesHistory = new Int32[HalfMovesLimit];
        ZobristKeyHistory = new UInt64[HalfMovesLimit];
        CastlingHistory = new CastlingRights[HalfMovesLimit];
        EnPassantSquare = InvalidSquare;
    }

    /// <summary>
    /// Constructs a deep copy of the given position.
    /// </summary>
    private Position(Position source)
    {
        Board = (Int32[])source.Board.Clone();
        PieceBitboards = (UInt64[])source.PieceBitboards.Clone();
        OccupiedBitboard = source.OccupiedBitboard;
        SideToMove = source.SideToMove;
        HalfMoves = source.HalfMoves;
        _rootHalfMoves = source._rootHalfMoves;
        Castling = source.Castling;
        EnPassantSquare = source.EnPassantSquare;
        FiftyMovesClock = source.FiftyMovesClock;
        ZobristKey = source.ZobristKey;

        // Only the history up to the current ply is meaningful; the rest holds the defaults a
        // freshly parsed position has (no en passant square), so copy the used prefix.
        EnPassantHistory = new Int32[HalfMovesLimit];
        FiftyMovesHistory = new Int32[HalfMovesLimit];
        ZobristKeyHistory = new UInt64[HalfMovesLimit];
        CastlingHistory = new CastlingRights[HalfMovesLimit];
        Int32 used = source.HalfMoves + 1;
        Array.Copy(source.EnPassantHistory, EnPassantHistory, used);
        Array.Copy(source.FiftyMovesHistory, FiftyMovesHistory, used);
        Array.Copy(source.ZobristKeyHistory, ZobristKeyHistory, used);
        Array.Copy(source.CastlingHistory, CastlingHistory, used);
        for (Int32 ply = used; ply < HalfMovesLimit; ply++)
            EnPassantHistory[ply] = InvalidSquare;
    }

    #endregion

    #region Cloning

    /// <summary>
    /// Returns a deep clone of the position.
    /// </summary>
    /// <returns>A deep clone of the position.</returns>
    public Position DeepClone()
    {
        return new Position(this);
    }

    #endregion

    /// <summary>
    /// Returns a text drawing of the position.
    /// </summary>
    /// <returns>A text drawing of the position</returns>
    public override string ToString()
    {
        return ToString(String.Empty);
    }

    /// <summary>
    /// Returns a text drawing of the position with the given comments displayed.
    /// </summary>
    /// <param name="comments">The comments to display.</param>
    /// <returns>A text drawing of the position with the given comments displayed</returns>
    public string ToString(params String[] comments)
    {
        StringBuilder sb = new StringBuilder("   +------------------------+ ", 400);
        Int32 commentIndex = 0;
        if (commentIndex < comments.Length)
            sb.Append(comments[commentIndex++]);

        for (Int32 rank = 0; rank < 8; rank++)
        {
            sb.Append(Environment.NewLine);
            sb.Append(' ');
            sb.Append(8 - rank);
            sb.Append(" |");
            for (Int32 file = 0; file < 8; file++)
            {
                Int32 piece = Board[file + rank * 8];
                if (piece != Piece.Empty)
                {
                    sb.Append((piece & Colour.Mask) == Colour.White ? '<' : '[');
                    sb.Append(Piece.GetInitial(piece));
                    sb.Append((piece & Colour.Mask) == Colour.White ? '>' : ']');
                }
                else
                    sb.Append((file + rank) % 2 == 1 ? ":::" : "   ");
            }
            sb.Append("| ");
            if (commentIndex < comments.Length)
                sb.Append(comments[commentIndex++]);
        }

        sb.Append(Environment.NewLine);
        sb.Append("   +------------------------+ ");
        if (commentIndex < comments.Length)
            sb.Append(comments[commentIndex++]);

        sb.Append(Environment.NewLine);
        sb.Append("     a  b  c  d  e  f  g  h   ");
        if (commentIndex < comments.Length)
            sb.Append(comments[commentIndex++]);

        return sb.ToString();
    }

    #region Equality

    /// <summary>
    /// Returns whether the position is equal to another position: the same pieces, side to move,
    /// castling rights, en passant square and clocks, reached through the same history of
    /// positions (so two positions that reach the same board by different move orders are not
    /// equal).
    /// </summary>
    /// <param name="other">The position to compare with.</param>
    /// <returns>Whether the position is equal to another position</returns>
    public bool Equals(Position? other)
    {
        if (ReferenceEquals(other, null))
            return false;
        if (ReferenceEquals(other, this))
            return true;

        if (ZobristKey != other.ZobristKey
         || OccupiedBitboard != other.OccupiedBitboard
         || HalfMoves != other.HalfMoves
         || FiftyMovesClock != other.FiftyMovesClock
         || EnPassantSquare != other.EnPassantSquare
         || SideToMove != other.SideToMove
         || Castling != other.Castling)
            return false;

        for (Int32 ply = 0; ply < HalfMoves; ply++)
            if (FiftyMovesHistory[ply] != other.FiftyMovesHistory[ply]
             || EnPassantHistory[ply] != other.EnPassantHistory[ply]
             || ZobristKeyHistory[ply] != other.ZobristKeyHistory[ply]
             || CastlingHistory[ply] != other.CastlingHistory[ply])
                return false;

        for (Int32 piece = 0; piece < PieceBitboards.Length; piece++)
            if (PieceBitboards[piece] != other.PieceBitboards[piece])
                return false;

        for (Int32 square = 0; square < Board.Length; square++)
            if (Board[square] != other.Board[square])
                return false;

        return true;
    }

    /// <summary>
    /// Returns whether the object is a position equal to this one.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as Position);
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(Position)"/>: equal positions have
    /// the same Zobrist key.
    /// </summary>
    public override int GetHashCode()
    {
        return unchecked((Int32)(ZobristKey ^ (ZobristKey >> 32)));
    }

    #endregion
}
