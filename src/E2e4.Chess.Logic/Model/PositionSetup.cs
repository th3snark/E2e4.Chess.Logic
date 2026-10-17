using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// An editable chess position for set-up and puzzle composition. Unlike <see cref="Position"/>, which only ever
/// describes a position that can arise in play, a setup may be incomplete or impossible while it is being
/// edited (no kings, a pawn on the last rank, ...). <see cref="Validate"/> lists what is wrong and
/// <see cref="ToPosition"/> yields a playable <see cref="Position"/> once nothing is.
/// </summary>
public sealed class PositionSetup
{
    private const Int32 NoSquare = Position.InvalidSquare;

    private readonly Int32[] _squares = new Int32[64];
    private Int32 _sideToMove;
    private Int32 _enPassantSquare;

    /// <summary>Creates an empty board, White to move, with no castling rights.</summary>
    public PositionSetup()
    {
        EnPassantSquare = NoSquare;
        MoveNumber = 1;
    }

    /// <summary>The side to move: <see cref="Colour.White"/> or <see cref="Colour.Black"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is neither colour.</exception>
    public Int32 SideToMove
    {
        get => _sideToMove;
        set
        {
            if (value != Colour.White && value != Colour.Black)
                throw new ArgumentOutOfRangeException(nameof(value));
            _sideToMove = value;
        }
    }

    /// <summary>The castling moves still claimed. Whether they are consistent is reported by <see cref="Validate"/>.</summary>
    public CastlingRights Castling { get; set; }

    /// <summary>The en passant target square, or <see cref="Position.InvalidSquare"/> for none.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is neither a square nor <see cref="Position.InvalidSquare"/>.</exception>
    public Int32 EnPassantSquare
    {
        get => _enPassantSquare;
        set
        {
            if (value != NoSquare)
                CheckSquare(value);
            _enPassantSquare = value;
        }
    }

    /// <summary>Half moves since the last capture or pawn move.</summary>
    public Int32 FiftyMovesClock { get; set; }

    /// <summary>The full move number, starting at 1.</summary>
    public Int32 MoveNumber { get; set; }

    /// <summary>
    /// The piece on a square (<see cref="Piece.Empty"/> or a colour | piece value), squares numbered as in
    /// <see cref="Position.Squares"/> (a8 = 0, h1 = 63).
    /// </summary>
    public Int32 this[Int32 square]
    {
        get
        {
            CheckSquare(square);
            return _squares[square];
        }
        set
        {
            CheckSquare(square);
            if (value != Piece.Empty && (value < Piece.Min || value > Piece.Max))
                throw new ArgumentOutOfRangeException(nameof(value));
            _squares[square] = value;
        }
    }

    private static void CheckSquare(Int32 square)
    {
        if (square < 0 || square > 63)
            throw new ArgumentOutOfRangeException(nameof(square));
    }

    /// <summary>Copies a playable position.</summary>
    public static PositionSetup FromPosition(Position position)
    {
        if (position == null)
            throw new ArgumentNullException(nameof(position));
        // The FEN of a playable position always parses.
        _ = TryParse(position.GetFEN(), out PositionSetup? setup);
        return setup!;
    }

    /// <summary>The setup of the standard starting position.</summary>
    public static PositionSetup Standard()
    {
        _ = TryParse(Position.StartingFEN, out PositionSetup? setup);
        return setup!;
    }

    /// <summary>Returns an independent copy.</summary>
    public PositionSetup Clone()
    {
        PositionSetup copy = new PositionSetup
        {
            SideToMove = SideToMove,
            Castling = Castling,
            EnPassantSquare = EnPassantSquare,
            FiftyMovesClock = FiftyMovesClock,
            MoveNumber = MoveNumber,
        };
        Array.Copy(_squares, copy._squares, _squares.Length);
        return copy;
    }

    /// <summary>Empties the board and resets every other field.</summary>
    public void Clear()
    {
        Array.Clear(_squares, 0, _squares.Length);
        SideToMove = Colour.White;
        Castling = CastlingRights.None;
        EnPassantSquare = NoSquare;
        FiftyMovesClock = 0;
        MoveNumber = 1;
    }

    /// <summary>
    /// Parses a FEN string leniently: the piece placement must be eight ranks of eight files of known pieces, and the
    /// side to move must be 'w' or 'b' (a missing side to move means White, so a bare placement is accepted), but the
    /// position need not be legal. The other fields are optional; a malformed one fails the parse.
    /// </summary>
    public static Boolean TryParse(String? fen, [NotNullWhen(true)] out PositionSetup? setup)
    {
        setup = null;
        if (fen == null)
            return false;

        String[] terms = fen.Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length < 1)
            return false;

        PositionSetup candidate = new PositionSetup();
        if (!candidate.TryParsePlacement(terms[0]))
            return false;

        if (terms.Length > 1)
        {
            if (terms[1] == "w")
                candidate.SideToMove = Colour.White;
            else if (terms[1] == "b")
                candidate.SideToMove = Colour.Black;
            else
                return false;
        }

        if (terms.Length > 2 && !candidate.TryParseCastling(terms[2]))
            return false;

        if (terms.Length > 3)
        {
            if (terms[3] != "-")
            {
                if (terms[3].Length != 2 || terms[3][0] < 'a' || terms[3][0] > 'h' || terms[3][1] < '1' || terms[3][1] > '8')
                    return false;
                candidate.EnPassantSquare = Square.SquareAt(terms[3]);
            }
        }

        Int32 number;
        if (terms.Length > 4)
        {
            if (!Int32.TryParse(terms[4], NumberStyles.None, CultureInfo.InvariantCulture, out number))
                return false;
            candidate.FiftyMovesClock = number;
        }
        if (terms.Length > 5)
        {
            if (!Int32.TryParse(terms[5], NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1)
                return false;
            candidate.MoveNumber = number;
        }

        setup = candidate;
        return true;
    }

    private Boolean TryParsePlacement(String placement)
    {
        Int32 file = 0;
        Int32 rank = 0;
        foreach (Char c in placement)
        {
            if (c == '/')
            {
                if (file != 8 || rank == 7)
                    return false;
                file = 0;
                rank++;
            }
            else if (c >= '1' && c <= '8')
            {
                file += c - '0';
                if (file > 8)
                    return false;
            }
            else
            {
                Int32 piece = Piece.FromInitial(c);
                if (piece == Piece.Empty)
                    return false;
                if (file >= 8)
                    return false;
                _squares[file++ + rank * 8] = (Char.IsUpper(c) ? Colour.White : Colour.Black) | piece;
            }
        }
        return rank == 7 && file == 8;
    }

    private Boolean TryParseCastling(String rights)
    {
        if (rights == "-")
            return true;
        if (rights.Length == 0 || rights.Length > 4)
            return false;
        foreach (Char c in rights)
        {
            switch (c)
            {
                case 'K': Castling |= CastlingRights.WhiteKingside; break;
                case 'Q': Castling |= CastlingRights.WhiteQueenside; break;
                case 'k': Castling |= CastlingRights.BlackKingside; break;
                case 'q': Castling |= CastlingRights.BlackQueenside; break;
                default: return false;
            }
        }
        return true;
    }

    /// <summary>Returns the FEN string of the setup, whether or not it is legal.</summary>
    public String ToFen()
    {
        StringBuilder sb = new StringBuilder();
        for (Int32 rank = 0; rank < 8; rank++)
        {
            Int32 spaces = 0;
            for (Int32 file = 0; file < 8; file++)
            {
                Int32 piece = _squares[file + rank * 8];
                if (piece == Piece.Empty)
                {
                    spaces++;
                    continue;
                }
                if (spaces > 0)
                {
                    sb.Append(spaces);
                    spaces = 0;
                }
                sb.Append(PieceLetter(piece));
            }
            if (spaces > 0)
                sb.Append(spaces);
            if (rank < 7)
                sb.Append('/');
        }

        sb.Append(SideToMove == Colour.White ? " w " : " b ");
        if (Castling == CastlingRights.None)
            sb.Append('-');
        else
        {
            if ((Castling & CastlingRights.WhiteKingside) != 0) sb.Append('K');
            if ((Castling & CastlingRights.WhiteQueenside) != 0) sb.Append('Q');
            if ((Castling & CastlingRights.BlackKingside) != 0) sb.Append('k');
            if ((Castling & CastlingRights.BlackQueenside) != 0) sb.Append('q');
        }
        sb.Append(' ');
        sb.Append(EnPassantSquare == NoSquare ? "-" : Square.GetName(EnPassantSquare));
        sb.Append(' ').Append(FiftyMovesClock.ToString(CultureInfo.InvariantCulture));
        sb.Append(' ').Append(MoveNumber.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static Char PieceLetter(Int32 piece)
    {
        Char letter = Piece.GetInitial(piece)[0];
        return (piece & Colour.Mask) == Colour.White ? letter : Char.ToLowerInvariant(letter);
    }

    /// <summary>
    /// The castling rights the board can actually support: those whose king and rook stand on their original squares.
    /// </summary>
    public CastlingRights PossibleCastling
    {
        get
        {
            CastlingRights possible = CastlingRights.None;
            if (Holds(60, Colour.White | Piece.King) && Holds(63, Colour.White | Piece.Rook)) possible |= CastlingRights.WhiteKingside;
            if (Holds(60, Colour.White | Piece.King) && Holds(56, Colour.White | Piece.Rook)) possible |= CastlingRights.WhiteQueenside;
            if (Holds(4, Colour.Black | Piece.King) && Holds(7, Colour.Black | Piece.Rook)) possible |= CastlingRights.BlackKingside;
            if (Holds(4, Colour.Black | Piece.King) && Holds(0, Colour.Black | Piece.Rook)) possible |= CastlingRights.BlackQueenside;
            return possible;
        }
    }

    private Boolean Holds(Int32 square, Int32 piece)
    {
        return _squares[square] == piece;
    }

    /// <summary>Removes every castling right the board cannot support (see <see cref="PossibleCastling"/>).</summary>
    public void DropInvalidCastlingRights()
    {
        Castling &= PossibleCastling;
    }

    /// <summary>Clears the en passant square when the board and the side to move no longer support it.</summary>
    public void DropInvalidEnPassant()
    {
        if (EnPassantSquare != NoSquare && !IsEnPassantConsistent(EnPassantSquare))
            EnPassantSquare = NoSquare;
    }

    /// <summary>
    /// The files (0 = a ... 7 = h) on which an en passant capture could be claimed for the current side to move:
    /// a pawn of the other side stands just past an empty target square with its origin square empty too.
    /// </summary>
    public IReadOnlyList<Int32> PossibleEnPassantFiles
    {
        get
        {
            List<Int32> files = new List<Int32>();
            for (Int32 file = 0; file < 8; file++)
            {
                if (IsEnPassantConsistent(TargetOnFile(file)))
                    files.Add(file);
            }
            return files;
        }
    }

    private Int32 TargetOnFile(Int32 file)
    {
        // After White moves (Black to move) the target is on rank 3 (row 5), otherwise on rank 6 (row 2).
        return file + (SideToMove == Colour.Black ? 5 : 2) * 8;
    }

    private Boolean IsEnPassantConsistent(Int32 target)
    {
        if (target < 0 || target > 63)
            return false;
        Boolean blackToMove = SideToMove == Colour.Black;
        if (Square.Rank(target) != (blackToMove ? 5 : 2))
            return false;
        Int32 pushed = blackToMove ? target - 8 : target + 8;
        Int32 origin = blackToMove ? target + 8 : target - 8;
        Int32 pawn = (blackToMove ? Colour.White : Colour.Black) | Piece.Pawn;
        return _squares[target] == Piece.Empty && _squares[origin] == Piece.Empty && _squares[pushed] == pawn;
    }

    /// <summary>
    /// Checks the setup and lists every problem found. An empty list means <see cref="ToPosition"/> will succeed
    /// and the position can arise in play; warnings do not stop that.
    /// </summary>
    public IReadOnlyList<PositionProblem> Validate()
    {
        List<PositionProblem> problems = FindErrors();

        if (problems.Count == 0)
        {
            Position? position = ToPosition();
            if (position != null && position.LegalMoves().Count == 0)
            {
                String message = position.InCheck(SideToMove)
                    ? "The side to move is checkmated."
                    : "The side to move is stalemated.";
                problems.Add(new PositionProblem(PositionProblemKind.NoLegalMoves, PositionProblemSeverity.Warning, message));
            }
        }

        return problems;
    }

    private List<PositionProblem> FindErrors()
    {
        List<PositionProblem> problems = new List<PositionProblem>();
        Int32[][] counts = { new Int32[Piece.King + 1], new Int32[Piece.King + 1] };
        Int32[] kingSquare = { NoSquare, NoSquare };
        Boolean pawnOnEdge = false;

        for (Int32 square = 0; square < 64; square++)
        {
            Int32 piece = _squares[square];
            if (piece == Piece.Empty)
                continue;
            Int32 colour = piece & Colour.Mask;
            Int32 kind = piece & Piece.Mask;
            counts[colour][kind]++;
            if (kind == Piece.King)
                kingSquare[colour] = square;
            if (kind == Piece.Pawn && (Square.Rank(square) == 0 || Square.Rank(square) == 7))
                pawnOnEdge = true;
        }

        Boolean kingsPresent = true;
        for (Int32 colour = Colour.White; colour <= Colour.Black; colour++)
        {
            String name = Colour.GetName(colour);
            if (counts[colour][Piece.King] == 0)
            {
                problems.Add(new PositionProblem(PositionProblemKind.MissingKing, PositionProblemSeverity.Error, "There is no " + name + " king."));
                kingsPresent = false;
            }
            else if (counts[colour][Piece.King] > 1)
            {
                problems.Add(new PositionProblem(PositionProblemKind.ExtraKing, PositionProblemSeverity.Error, "There is more than one " + name + " king."));
                kingsPresent = false;
            }
        }

        if (pawnOnEdge)
            problems.Add(new PositionProblem(PositionProblemKind.PawnOnLastRank, PositionProblemSeverity.Error, "A pawn cannot stand on the first or last rank."));

        Boolean materialOk = true;
        for (Int32 colour = Colour.White; colour <= Colour.Black; colour++)
        {
            Int32[] side = counts[colour];
            Int32 pawns = side[Piece.Pawn];
            Int32 promoted = Math.Max(0, side[Piece.Queen] - 1) + Math.Max(0, side[Piece.Rook] - 2)
                           + Math.Max(0, side[Piece.Bishop] - 2) + Math.Max(0, side[Piece.Knight] - 2);
            if (pawns > 8)
            {
                problems.Add(new PositionProblem(PositionProblemKind.TooManyPawns, PositionProblemSeverity.Error, "The " + Colour.GetName(colour) + " side has more than eight pawns."));
                materialOk = false;
            }
            else if (pawns + promoted > 8)
            {
                problems.Add(new PositionProblem(PositionProblemKind.ImpossibleMaterial, PositionProblemSeverity.Error, "The " + Colour.GetName(colour) + " side has more pieces than its pawns could have promoted into."));
                materialOk = false;
            }
        }

        CastlingRights possible = PossibleCastling;
        if ((Castling & ~possible) != 0)
            problems.Add(new PositionProblem(PositionProblemKind.InvalidCastlingRights, PositionProblemSeverity.Error, "A castling right needs its king and rook on their original squares."));

        if (EnPassantSquare != NoSquare && !IsEnPassantConsistent(EnPassantSquare))
            problems.Add(new PositionProblem(PositionProblemKind.InvalidEnPassant, PositionProblemSeverity.Error, "The en passant square does not match the board and the side to move."));

        if (kingsPresent)
        {
            Int32 fileGap = Math.Abs(Square.File(kingSquare[Colour.White]) - Square.File(kingSquare[Colour.Black]));
            Int32 rankGap = Math.Abs(Square.Rank(kingSquare[Colour.White]) - Square.Rank(kingSquare[Colour.Black]));
            Boolean adjacent = fileGap <= 1 && rankGap <= 1;
            if (adjacent)
                problems.Add(new PositionProblem(PositionProblemKind.KingsAdjacent, PositionProblemSeverity.Error, "The two kings cannot stand next to each other."));

            if (!pawnOnEdge && materialOk && !adjacent)
            {
                Position? board = PositionWithoutExtras();
                if (board != null)
                {
                    Int32 waiting = 1 - SideToMove;
                    if (board.InCheck(waiting))
                        problems.Add(new PositionProblem(PositionProblemKind.SideNotToMoveInCheck, PositionProblemSeverity.Error, "The " + Colour.GetName(waiting) + " king is in check but it is not " + Colour.GetName(waiting) + "'s move."));
                }
            }
        }

        return problems;
    }

    // A playable position of this board alone, so attacks can be measured before the other fields are consistent.
    private Position? PositionWithoutExtras()
    {
        PositionSetup bare = Clone();
        bare.Castling = CastlingRights.None;
        bare.EnPassantSquare = NoSquare;
        // Null when the position cannot be played; the caller reports that through its own problem list.
        _ = Position.TryParseBoard(bare.ToFen(), true, out Position? position);
        return position;
    }

    /// <summary>
    /// Returns the playable <see cref="Position"/>, or null when <see cref="Validate"/> reports an error.
    /// </summary>
    public Position? ToPosition()
    {
        if (FindErrors().Count > 0)
            return null;
        return Position.TryParse(ToFen(), out Position? position) ? position : null;
    }
}
