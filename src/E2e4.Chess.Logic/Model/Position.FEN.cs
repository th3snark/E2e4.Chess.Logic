using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    /// failure for any FEN string that does not describe a legal board layout.
    /// </summary>
    /// <param name="fen">The FEN string to parse.</param>
    /// <param name="position">The parsed position, or null if the FEN string is
    /// invalid.</param>
    /// <returns>Whether the FEN string was valid.</returns>
    public static Boolean TryParse(String? fen, [NotNullWhen(true)] out Position? position)
    {
        Position candidate = new Position();
        try
        {
            candidate.ParseFen(fen);
        }
        catch (FormatException)
        {
            position = null;
            return false;
        }
        position = candidate;
        return true;
    }

    /// <summary>
    /// Resets every piece of position state to that of an empty board, so that a
    /// failed parse can never leave remnants behind.
    /// </summary>
    private void ResetState()
    {
        Array.Clear(Board, 0, Board.Length);
        Array.Clear(PieceBitboards, 0, PieceBitboards.Length);
        OccupiedBitboard = 0;
        SideToMove = Colour.White;
        HalfMoves = 0;
        _rootHalfMoves = 0;
        Castling = CastlingRights.None;
        EnPassantSquare = InvalidSquare;
        for (Int32 i = 0; i < EnPassantHistory.Length; i++)
            EnPassantHistory[i] = InvalidSquare;
        FiftyMovesClock = 0;
        Array.Clear(FiftyMovesHistory, 0, FiftyMovesHistory.Length);
        ZobristKey = 0;
        Array.Clear(ZobristKeyHistory, 0, ZobristKeyHistory.Length);
        Array.Clear(CastlingHistory, 0, CastlingHistory.Length);
    }

    /// <summary>
    /// Parses the given FEN string and changes the position's state to represent
    /// that of the FEN string. The position is reset first, so a position is
    /// either fully described by the FEN string or left empty.
    /// </summary>
    /// <param name="fen">The FEN string to parse.</param>
    /// <exception cref="FormatException">The FEN string is invalid.</exception>
    private void ParseFen(String? fen)
    {
        ResetState();

        if (fen == null)
            throw new FormatException("The FEN string is null.");

        // Split FEN into terms based on whitespace.
        String[] terms = fen.Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length < 2)
            throw new FormatException("A FEN string needs at least piece placement and side to move.");

        ParsePiecePlacement(terms[0]);

        // Determine side to move.
        if (terms[1] == "w")
            SideToMove = Colour.White;
        else if (terms[1] == "b")
            SideToMove = Colour.Black;
        else
            throw new FormatException("The side to move must be 'w' or 'b'.");

        if (terms.Length > 2)
            ParseCastlingRights(terms[2]);

        if (terms.Length > 3)
            ParseEnPassantSquare(terms[3]);

        // Determine fifty-moves clock and plies advanced.
        if (terms.Length > 4)
            FiftyMovesClock = ParseCounter(terms[4], 0);
        if (terms.Length > 5)
        {
            Int64 halfMoves = 2L * (ParseCounter(terms[5], 1) - 1) + SideToMove;
            if (halfMoves >= HalfMovesLimit)
                throw new FormatException("The move number exceeds the supported game length.");
            HalfMoves = (Int32)halfMoves;
        }

        // Initialize history information.
        _rootHalfMoves = HalfMoves;
        EnPassantHistory[HalfMoves] = EnPassantSquare;
        FiftyMovesHistory[HalfMoves] = FiftyMovesClock;
        CastlingHistory[HalfMoves] = Castling;

        // Initialize bitboards.
        for (Int32 square = 0; square < Board.Length; square++)
            if (Board[square] != Piece.Empty)
            {
                Int32 colour = Board[square] & Colour.Mask;
                PieceBitboards[Board[square]] |= 1UL << square;
                PieceBitboards[colour] |= 1UL << square;
                OccupiedBitboard |= 1UL << square;
            }

        // Initialize Zobrist key and history.
        ZobristKey = GetZobristKey();
        ZobristKeyHistory[HalfMoves] = ZobristKey;
    }

    /// <summary>
    /// Populates the squares from the piece placement field of a FEN string and
    /// checks the board is a legal layout: eight ranks of eight files, one king
    /// per colour, no pawns on the first or last rank, and no more material than the
    /// starting pieces and promotions can account for.
    /// </summary>
    private void ParsePiecePlacement(String placement)
    {
        Int32 file = 0;
        Int32 rank = 0;
        Int32[] kings = new Int32[2];
        Int32[][] counts = { new Int32[Piece.King + 1], new Int32[Piece.King + 1] };

        foreach (Char c in placement)
        {
            if (c == '/')
            {
                if (file != 8 || rank == 7)
                    throw new FormatException("Each of the eight ranks must describe eight files.");
                file = 0;
                rank++;
            }
            else if (c >= '1' && c <= '8')
            {
                file += c - '0';
                if (file > 8)
                    throw new FormatException("A rank describes more than eight files.");
            }
            else
            {
                Int32 colour = Char.IsUpper(c) ? Colour.White : Colour.Black;
                Int32 piece = Piece.FromInitial(c);
                if (piece == Piece.Empty)
                    throw new FormatException("Unknown piece '" + c + "'.");
                if (piece == Piece.King)
                    kings[colour]++;
                else if (piece == Piece.Pawn && (rank == 0 || rank == 7))
                    throw new FormatException("Pawns cannot stand on the first or last rank.");
                if (file >= 8)
                    throw new FormatException("A rank describes more than eight files.");
                counts[colour][piece]++;
                Board[file++ + rank * 8] = colour | piece;
            }
        }

        if (rank != 7 || file != 8)
            throw new FormatException("A FEN string must describe eight ranks of eight files.");
        if (kings[Colour.White] != 1 || kings[Colour.Black] != 1)
            throw new FormatException("Each side must have exactly one king.");

        RequirePlausibleMaterial(counts[Colour.White]);
        RequirePlausibleMaterial(counts[Colour.Black]);
    }

    /// <summary>
    /// Rejects a side with more than eight pawns, or with more extra queens, rooks, bishops and
    /// knights than its missing pawns could have promoted into (every promotion costs a pawn).
    /// </summary>
    private static void RequirePlausibleMaterial(Int32[] counts)
    {
        Int32 pawns = counts[Piece.Pawn];
        if (pawns > 8)
            throw new FormatException("A side cannot have more than eight pawns.");

        Int32 promoted = Math.Max(0, counts[Piece.Queen] - 1)
                       + Math.Max(0, counts[Piece.Rook] - 2)
                       + Math.Max(0, counts[Piece.Bishop] - 2)
                       + Math.Max(0, counts[Piece.Knight] - 2);
        if (pawns + promoted > 8)
            throw new FormatException("A side has more pieces than its pawns could have promoted into.");
    }

    /// <summary>
    /// Parses the castling rights field, requiring the king and rook concerned
    /// to stand on their original squares.
    /// </summary>
    private void ParseCastlingRights(String rights)
    {
        if (rights == "-")
            return;
        if (rights.Length == 0 || rights.Length > 4)
            throw new FormatException("Invalid castling rights.");

        foreach (Char c in rights)
        {
            switch (c)
            {
                case 'K':
                    RequireCastlingPieces(Colour.White, 60, 63);
                    Castling |= CastlingRights.WhiteKingside;
                    break;
                case 'Q':
                    RequireCastlingPieces(Colour.White, 60, 56);
                    Castling |= CastlingRights.WhiteQueenside;
                    break;
                case 'k':
                    RequireCastlingPieces(Colour.Black, 4, 7);
                    Castling |= CastlingRights.BlackKingside;
                    break;
                case 'q':
                    RequireCastlingPieces(Colour.Black, 4, 0);
                    Castling |= CastlingRights.BlackQueenside;
                    break;
                default:
                    throw new FormatException("Invalid castling rights.");
            }
        }
    }

    private void RequireCastlingPieces(Int32 colour, Int32 kingSquare, Int32 rookSquare)
    {
        if (Board[kingSquare] != (colour | Piece.King) || Board[rookSquare] != (colour | Piece.Rook))
            throw new FormatException("A castling right requires the king and rook on their original squares.");
    }

    /// <summary>
    /// Parses the en passant field, requiring a pawn that has just advanced two
    /// squares past the empty target square.
    /// </summary>
    private void ParseEnPassantSquare(String field)
    {
        if (field == "-")
            return;

        // After white moves (black to move) the target is on rank 3; otherwise rank 6.
        Char expectedRank = SideToMove == Colour.Black ? '3' : '6';
        if (field.Length != 2 || field[0] < 'a' || field[0] > 'h' || field[1] != expectedRank)
            throw new FormatException("Invalid en passant square.");

        Int32 target = Square.SquareAt(field);
        Int32 pushed = SideToMove == Colour.Black ? target - 8 : target + 8;
        Int32 origin = SideToMove == Colour.Black ? target + 8 : target - 8;
        Int32 pawn = (SideToMove == Colour.Black ? Colour.White : Colour.Black) | Piece.Pawn;
        if (Board[target] != Piece.Empty || Board[origin] != Piece.Empty || Board[pushed] != pawn)
            throw new FormatException("The en passant square is inconsistent with the board.");

        EnPassantSquare = target;
    }

    /// <summary>
    /// Parses a non-negative decimal counter, rejecting signs, overflow and
    /// values below the minimum.
    /// </summary>
    private static Int32 ParseCounter(String text, Int32 minimum)
    {
        Int32 value;
        if (!Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < minimum)
            throw new FormatException("Invalid move counter '" + text + "'.");
        return value;
    }

    /// <summary>
    /// Returns the FEN string that describes the position.
    /// </summary>
    /// <returns>The FEN string that describes the position.</returns>
    public String GetFEN()
    {
        StringBuilder sb = new StringBuilder();

        for (Int32 rank = 0; rank < 8; rank++)
        {
            Int32 spaces = 0;
            for (Int32 file = 0; file < 8; file++)
            {
                Int32 square = file + rank * 8;
                if (Board[square] == Piece.Empty)
                    spaces++;
                else
                {
                    if (spaces > 0)
                    {
                        sb.Append(spaces);
                        spaces = 0;
                    }
                    String piece = Piece.GetInitial(Board[square]);
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
