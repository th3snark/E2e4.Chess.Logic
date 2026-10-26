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
    public const string StartingFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    /// The maximum number of plies the position is to support, counting from move one.
    /// </summary>
    public const int HalfMovesLimit = 1024;

    /// <summary>The largest fifty-moves clock a FEN string may carry; larger values are rejected.</summary>
    public const int MaxFiftyMovesClock = 10000;

    /// <summary>The longest FEN string that is parsed; longer text is rejected as invalid.</summary>
    public const int MaxFenLength = 256;

    /// <summary>
    /// The value representing an invalid square.
    /// </summary>
    public const int InvalidSquare = -1;

    /// <summary>
    /// The minimum length of the array passed to <see cref="LegalMoves(int[])"/>. No known
    /// position has more than 218 legal moves; this leaves headroom.
    /// </summary>
    public const int MaxMoves = 256;

    #endregion

    #region State

    /// <summary>
    /// The pieces on the squares, 0 = a8 .. 63 = h1. Exposed read-only as <see cref="Squares"/>.
    /// </summary>
    internal readonly int[] Board;

    /// <summary>
    /// The sets of pieces. PieceBitboards[p] is the set of squares holding piece p, and
    /// PieceBitboards[c] for a colour c is the set of all squares holding a piece of that colour.
    /// </summary>
    internal readonly ulong[] PieceBitboards;

    /// <summary>
    /// The set of all occupied squares.
    /// </summary>
    internal ulong OccupiedBitboard;

    /// <summary>
    /// The EnPassantSquare values for every ply up to and including the current ply.
    /// </summary>
    internal readonly int[] EnPassantHistory;

    /// <summary>
    /// The FiftyMovesClock values for every ply up to and including the current ply.
    /// </summary>
    internal readonly int[] FiftyMovesHistory;

    /// <summary>
    /// The ZobristKey values for every ply up to and including the current ply.
    /// </summary>
    internal readonly ulong[] ZobristKeyHistory;

    /// <summary>
    /// The Castling values for every ply up to and including the current ply.
    /// </summary>
    internal readonly CastlingRights[] CastlingHistory;

    /// <summary>
    /// The ply the position was created at (non-zero for a FEN with a move number). Moves can only be
    /// unmade back to this point, because no history exists before it.
    /// </summary>
    private int _rootHalfMoves;

    private System.Collections.ObjectModel.ReadOnlyCollection<int>? _squares;

    /// <summary>
    /// The pieces on the squares of the chessboard. Squares[n] gives the piece on the nth square
    /// where 0 is a8 and 63 is h1; combine with <see cref="Piece"/> and <see cref="Colour"/>.
    /// </summary>
    public IReadOnlyList<int> Squares
    {
        get { return _squares ?? (_squares = Array.AsReadOnly(Board)); }
    }

    /// <summary>
    /// The colour that is to make the next move.
    /// </summary>
    public int SideToMove { get; private set; }

    /// <summary>
    /// The total number of plies the position has advanced from move one.
    /// </summary>
    public int HalfMoves { get; private set; }

    /// <summary>
    /// The castling moves that are still possible.
    /// </summary>
    public CastlingRights Castling { get; private set; }

    /// <summary>
    /// The square a pawn capturing en passant would move to, or <see cref="InvalidSquare"/> if
    /// the last move was not a two-square pawn advance.
    /// </summary>
    public int EnPassantSquare { get; private set; }

    /// <summary>
    /// The number of plies since the last capture or pawn move, for the fifty-move rule.
    /// </summary>
    public int FiftyMovesClock { get; private set; }

    /// <summary>
    /// The Zobrist hash value of the position, updated incrementally as moves are made.
    /// </summary>
    public ulong ZobristKey { get; private set; }

    #endregion

    #region Construction

    /// <summary>
    /// Constructs a position with the given FEN string. If the FEN string is
    /// invalid the start position is used; use <see cref="TryParse"/> to detect that instead.
    /// </summary>
    /// <param name="fen">The FEN of the position.</param>
    public Position(string fen)
        : this()
    {
        Load(PositionSetup.TryParse(fen, out PositionSetup? parsed) && parsed.IsPlayable() ? parsed : PositionSetup.Standard());
    }

    /// <summary>
    /// Constructs a position with an empty, invalid state, to be filled in by parsing.
    /// </summary>
    private Position()
    {
        Board = new int[64];
        PieceBitboards = new ulong[16];
        EnPassantHistory = new int[HalfMovesLimit];
        FiftyMovesHistory = new int[HalfMovesLimit];
        ZobristKeyHistory = new ulong[HalfMovesLimit];
        CastlingHistory = new CastlingRights[HalfMovesLimit];
        EnPassantSquare = InvalidSquare;
    }

    /// <summary>
    /// Constructs a deep copy of the given position.
    /// </summary>
    private Position(Position source)
    {
        Board = (int[])source.Board.Clone();
        PieceBitboards = (ulong[])source.PieceBitboards.Clone();
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
        EnPassantHistory = new int[HalfMovesLimit];
        FiftyMovesHistory = new int[HalfMovesLimit];
        ZobristKeyHistory = new ulong[HalfMovesLimit];
        CastlingHistory = new CastlingRights[HalfMovesLimit];
        int used = source.HalfMoves + 1;
        Array.Copy(source.EnPassantHistory, EnPassantHistory, used);
        Array.Copy(source.FiftyMovesHistory, FiftyMovesHistory, used);
        Array.Copy(source.ZobristKeyHistory, ZobristKeyHistory, used);
        Array.Copy(source.CastlingHistory, CastlingHistory, used);
        for (int ply = used; ply < HalfMovesLimit; ply++)
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
        return ToString(string.Empty);
    }

    /// <summary>
    /// Returns a text drawing of the position with the given comments displayed.
    /// </summary>
    /// <param name="comments">The comments to display.</param>
    /// <returns>A text drawing of the position with the given comments displayed</returns>
    public string ToString(params string[] comments)
    {
        StringBuilder sb = new StringBuilder("   +------------------------+ ", 400);
        int commentIndex = 0;
        if (commentIndex < comments.Length)
            sb.Append(comments[commentIndex++]);

        for (int rank = 0; rank < 8; rank++)
        {
            sb.Append(Environment.NewLine);
            sb.Append(' ');
            sb.Append(8 - rank);
            sb.Append(" |");
            for (int file = 0; file < 8; file++)
            {
                int piece = Board[file + rank * 8];
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

        for (int ply = 0; ply < HalfMoves; ply++)
            if (FiftyMovesHistory[ply] != other.FiftyMovesHistory[ply]
             || EnPassantHistory[ply] != other.EnPassantHistory[ply]
             || ZobristKeyHistory[ply] != other.ZobristKeyHistory[ply]
             || CastlingHistory[ply] != other.CastlingHistory[ply])
                return false;

        for (int piece = 0; piece < PieceBitboards.Length; piece++)
            if (PieceBitboards[piece] != other.PieceBitboards[piece])
                return false;

        for (int square = 0; square < Board.Length; square++)
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
        return unchecked((int)(ZobristKey ^ (ZobristKey >> 32)));
    }

    #endregion
}
