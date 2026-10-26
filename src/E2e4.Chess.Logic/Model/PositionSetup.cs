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
    private const int NoSquare = Position.InvalidSquare;

    private readonly int[] _squares = new int[64];
    private int _sideToMove;
    private int _enPassantSquare;

    /// <summary>Creates an empty board, White to move, with no castling rights.</summary>
    public PositionSetup()
    {
        EnPassantSquare = NoSquare;
        MoveNumber = 1;
    }

    /// <summary>The side to move: <see cref="Colour.White"/> or <see cref="Colour.Black"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is neither colour.</exception>
    public int SideToMove
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
    public int EnPassantSquare
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
    public int FiftyMovesClock { get; set; }

    /// <summary>The full move number, starting at 1.</summary>
    public int MoveNumber { get; set; }

    /// <summary>
    /// The piece on a square (<see cref="Piece.Empty"/> or a colour | piece value), squares numbered as in
    /// <see cref="Position.Squares"/> (a8 = 0, h1 = 63).
    /// </summary>
    public int this[int square]
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

    private static void CheckSquare(int square)
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
    public static bool TryParse(string? fen, [NotNullWhen(true)] out PositionSetup? setup)
    {
        setup = null;
        if (fen == null)
            return false;

        if (fen.Length > Position.MaxFenLength)
            return false;

        string[] terms = fen.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
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

        int number;
        if (terms.Length > 4)
        {
            if (!int.TryParse(terms[4], NumberStyles.None, CultureInfo.InvariantCulture, out number))
                return false;
            candidate.FiftyMovesClock = number;
        }
        if (terms.Length > 5)
        {
            if (!int.TryParse(terms[5], NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1)
                return false;
            candidate.MoveNumber = number;
        }

        setup = candidate;
        return true;
    }

    private bool TryParsePlacement(string placement)
    {
        int file = 0;
        int rank = 0;
        foreach (char c in placement)
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
                int piece = Piece.FromInitial(c);
                if (piece == Piece.Empty)
                    return false;
                if (file >= 8)
                    return false;
                _squares[file++ + rank * 8] = (char.IsUpper(c) ? Colour.White : Colour.Black) | piece;
            }
        }
        return rank == 7 && file == 8;
    }

    private bool TryParseCastling(string rights)
    {
        if (rights == "-")
            return true;
        if (rights.Length == 0 || rights.Length > 4)
            return false;
        foreach (char c in rights)
        {
            CastlingRights right;
            switch (c)
            {
                case 'K': right = CastlingRights.WhiteKingside; break;
                case 'Q': right = CastlingRights.WhiteQueenside; break;
                case 'k': right = CastlingRights.BlackKingside; break;
                case 'q': right = CastlingRights.BlackQueenside; break;
                default: return false;
            }
            if ((Castling & right) != 0)
                return false;
            Castling |= right;
        }
        return true;
    }

    /// <summary>Returns the FEN string of the setup, whether or not it is legal.</summary>
    public string ToFen()
    {
        StringBuilder sb = new StringBuilder();
        for (int rank = 0; rank < 8; rank++)
        {
            int spaces = 0;
            for (int file = 0; file < 8; file++)
            {
                int piece = _squares[file + rank * 8];
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

    private static char PieceLetter(int piece)
    {
        char letter = Piece.GetInitial(piece)[0];
        return (piece & Colour.Mask) == Colour.White ? letter : char.ToLowerInvariant(letter);
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

    private bool Holds(int square, int piece)
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
    public IReadOnlyList<int> PossibleEnPassantFiles
    {
        get
        {
            List<int> files = new List<int>();
            for (int file = 0; file < 8; file++)
            {
                if (IsEnPassantConsistent(TargetOnFile(file)))
                    files.Add(file);
            }
            return files;
        }
    }

    private int TargetOnFile(int file)
    {
        // After White moves (Black to move) the target is on rank 3 (row 5), otherwise on rank 6 (row 2).
        return file + (SideToMove == Colour.Black ? 5 : 2) * 8;
    }

    private bool IsEnPassantConsistent(int target)
    {
        if (target < 0 || target > 63)
            return false;
        bool blackToMove = SideToMove == Colour.Black;
        if (Square.Rank(target) != (blackToMove ? 5 : 2))
            return false;
        int pushed = blackToMove ? target - 8 : target + 8;
        int origin = blackToMove ? target + 8 : target - 8;
        int pawn = (blackToMove ? Colour.White : Colour.Black) | Piece.Pawn;
        return _squares[target] == Piece.Empty && _squares[origin] == Piece.Empty && _squares[pushed] == pawn;
    }

    /// <summary>
    /// Checks the setup and lists every problem found. Errors mean <see cref="ToPosition"/> fails. Warnings
    /// leave the position playable but flag one that is probably unintended, cannot arise in a game
    /// (an impossible check, pawns that would need more captures than were made, an inconsistent fifty-moves
    /// clock) or has already ended; an empty list means none of that was found.
    /// </summary>
    public IReadOnlyList<PositionProblem> Validate()
    {
        List<PositionProblem> problems = FindErrors();

        if (problems.Count == 0)
        {
            Position position = Position.Create(this);
            FindUnreachable(position, problems);
            if (position.LegalMoves().Count == 0)
            {
                string message = position.InCheck(SideToMove)
                    ? "The side to move is checkmated."
                    : "The side to move is stalemated.";
                problems.Add(new PositionProblem(PositionProblemKind.NoLegalMoves, PositionProblemSeverity.Warning, message));
            }
            if (position.InsufficientMaterial())
                problems.Add(new PositionProblem(PositionProblemKind.InsufficientMaterial, PositionProblemSeverity.Warning, "Neither side has the material to deliver checkmate."));
        }

        return problems;
    }

    private List<PositionProblem> FindErrors()
    {
        List<PositionProblem> problems = new List<PositionProblem>();
        int[][] counts = { new int[Piece.King + 1], new int[Piece.King + 1] };
        int[] kingSquare = { NoSquare, NoSquare };
        bool pawnOnEdge = false;

        for (int square = 0; square < 64; square++)
        {
            int piece = _squares[square];
            if (piece == Piece.Empty)
                continue;
            int colour = piece & Colour.Mask;
            int kind = piece & Piece.Mask;
            counts[colour][kind]++;
            if (kind == Piece.King)
                kingSquare[colour] = square;
            if (kind == Piece.Pawn && (Square.Rank(square) == 0 || Square.Rank(square) == 7))
                pawnOnEdge = true;
        }

        bool kingsPresent = true;
        for (int colour = Colour.White; colour <= Colour.Black; colour++)
        {
            string name = Colour.GetName(colour);
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

        if (FiftyMovesClock < 0 || FiftyMovesClock > Position.MaxFiftyMovesClock
            || MoveNumber < 1 || 2L * (MoveNumber - 1) + SideToMove >= Position.HalfMovesLimit)
            problems.Add(new PositionProblem(PositionProblemKind.InvalidCounters, PositionProblemSeverity.Error, "The move number or fifty-moves clock is outside the supported range."));

        if (pawnOnEdge)
            problems.Add(new PositionProblem(PositionProblemKind.PawnOnLastRank, PositionProblemSeverity.Error, "A pawn cannot stand on the first or last rank."));

        bool materialOk = true;
        for (int colour = Colour.White; colour <= Colour.Black; colour++)
        {
            int[] side = counts[colour];
            int pawns = side[Piece.Pawn];
            int promoted = Math.Max(0, side[Piece.Queen] - 1) + Math.Max(0, side[Piece.Rook] - 2)
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
            int fileGap = Math.Abs(Square.File(kingSquare[Colour.White]) - Square.File(kingSquare[Colour.Black]));
            int rankGap = Math.Abs(Square.Rank(kingSquare[Colour.White]) - Square.Rank(kingSquare[Colour.Black]));
            bool adjacent = fileGap <= 1 && rankGap <= 1;
            if (adjacent)
                problems.Add(new PositionProblem(PositionProblemKind.KingsAdjacent, PositionProblemSeverity.Error, "The two kings cannot stand next to each other."));

            if (!pawnOnEdge && materialOk && !adjacent)
            {
                int waiting = 1 - SideToMove;
                if (BoardOnly().InCheck(waiting))
                    problems.Add(new PositionProblem(PositionProblemKind.SideNotToMoveInCheck, PositionProblemSeverity.Error, "The " + Colour.GetName(waiting) + " king is in check but it is not " + Colour.GetName(waiting) + "'s move."));
            }
        }

        return problems;
    }

    // Rules a playable position can still break because no game could have reached it. The checks are necessary
    // conditions only: they reject provably unreachable positions and never claim a position is reachable.
    private void FindUnreachable(Position position, List<PositionProblem> problems)
    {
        if (IsImpossibleCheck(position))
            problems.Add(new PositionProblem(PositionProblemKind.ImpossibleCheck, PositionProblemSeverity.Warning, "The " + Colour.GetName(SideToMove) + " king is in a check that no single move could have given."));

        for (int colour = Colour.White; colour <= Colour.Black; colour++)
        {
            int enemyPieces = 0;
            for (int square = 0; square < 64; square++)
            {
                if (_squares[square] != Piece.Empty && (_squares[square] & Colour.Mask) != colour)
                    enemyPieces++;
            }
            if (CapturesNeeded(colour) > 16 - enemyPieces)
                problems.Add(new PositionProblem(PositionProblemKind.PawnsNeedMoreCaptures, PositionProblemSeverity.Warning, "The " + Colour.GetName(colour) + " pawns have left their files by more captures than the " + Colour.GetName(1 - colour) + " side has lost pieces."));
        }

        long plies = 2L * (MoveNumber - 1) + SideToMove;
        if (FiftyMovesClock > plies)
            problems.Add(new PositionProblem(PositionProblemKind.InconsistentClock, PositionProblemSeverity.Warning, "The fifty-moves clock is larger than the number of half moves played."));
        else if (EnPassantSquare != NoSquare && (FiftyMovesClock != 0 || plies < 1))
            problems.Add(new PositionProblem(PositionProblemKind.InconsistentClock, PositionProblemSeverity.Warning, "A two-square pawn advance has just been played, so the fifty-moves clock must be zero."));
    }

    private bool IsImpossibleCheck(Position position)
    {
        int king = -1;
        for (int square = 0; square < 64; square++)
        {
            if (_squares[square] == (SideToMove | Piece.King))
                king = square;
        }

        List<int> checkers = position.AttackersOf(1 - SideToMove, king);
        if (checkers.Count == 0)
            return false;

        if (EnPassantSquare != NoSquare)
        {
            // The pawn that has just advanced two squares must be the checker, or have uncovered the check.
            if (checkers.Count > 1)
                return true;
            int pushedTo = SideToMove == Colour.Black ? EnPassantSquare - 8 : EnPassantSquare + 8;
            int pushedFrom = SideToMove == Colour.Black ? EnPassantSquare + 8 : EnPassantSquare - 8;
            if (checkers[0] == pushedTo)
                return false;
            PositionSetup before = Clone();
            before.Castling = CastlingRights.None;
            before.EnPassantSquare = NoSquare;
            before._squares[pushedFrom] = before._squares[pushedTo];
            before._squares[pushedTo] = Piece.Empty;
            Position? earlier = before.ToPosition();
            return earlier != null && earlier.AttackersOf(1 - SideToMove, king).Count > 0;
        }

        if (checkers.Count > 2)
            return true;
        if (checkers.Count == 2)
        {
            // A double check needs a discovered one, which only a bishop, rook or queen can give, and two sliders
            // on one line through the king would have blocked each other.
            return (!IsSlider(checkers[0]) && !IsSlider(checkers[1])) || Aligned(checkers[0], checkers[1], king);
        }
        return false;
    }

    private bool IsSlider(int square)
    {
        int kind = _squares[square] & Piece.Mask;
        return kind == Piece.Bishop || kind == Piece.Rook || kind == Piece.Queen;
    }

    private static bool Aligned(int a, int b, int c)
    {
        if (!Square.SameLine(a, b))
            return false;
        int cross = (Square.File(b) - Square.File(a)) * (Square.Rank(c) - Square.Rank(a))
                    - (Square.Rank(b) - Square.Rank(a)) * (Square.File(c) - Square.File(a));
        return cross == 0;
    }

    // The fewest captures the pawns of a colour have made, from the files they stand on: each pawn started on its
    // own file and moves one file per capture, and pawns keep their order, so match them to distinct start files.
    private int CapturesNeeded(int colour)
    {
        List<int> files = new List<int>();
        for (int square = 0; square < 64; square++)
        {
            if (_squares[square] == (colour | Piece.Pawn))
                files.Add(Square.File(square));
        }
        files.Sort();

        const int unreachable = 1000;
        int[] previous = new int[9];
        for (int pawn = 0; pawn < files.Count; pawn++)
        {
            int[] current = new int[9];
            current[0] = unreachable;
            for (int origin = 1; origin <= 8; origin++)
                current[origin] = Math.Min(current[origin - 1], previous[origin - 1] + Math.Abs(files[pawn] - (origin - 1)));
            previous = current;
        }
        return previous[8];
    }

    // The board alone as a position, so attacks can be measured before the other fields are consistent.
    private Position BoardOnly()
    {
        PositionSetup bare = Clone();
        bare.Castling = CastlingRights.None;
        bare.EnPassantSquare = NoSquare;
        bare.FiftyMovesClock = 0;
        bare.MoveNumber = 1;
        return Position.Create(bare);
    }

    /// <summary>
    /// Returns the playable <see cref="Position"/>, or null when <see cref="Validate"/> reports an error.
    /// </summary>
    public Position? ToPosition()
    {
        return IsPlayable() ? Position.Create(this) : null;
    }

    internal bool IsPlayable()
    {
        return FindErrors().Count == 0;
    }
}
