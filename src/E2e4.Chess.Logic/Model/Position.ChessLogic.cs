using System;
using System.Collections.Generic;
using E2e4.Chess.Logic.Bitboards;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Encapsulates general chess logic routines for the chess position.
/// </summary>
public sealed partial class Position : IEquatable<Position>
{
    /// <summary>
    /// Returns whether the given side is in check.
    /// </summary>
    /// <param name="colour">The side to test for check.</param>
    /// <returns>Whether the given side is in check.</returns>
    public bool InCheck(int colour)
    {
        return IsAttacked(colour, Bitboard.Read(PieceBitboards[colour | Piece.King]));
    }

    /// <summary>
    /// Returns whether the given side is attacked on the given square.
    /// </summary>
    /// <param name="colour">The side to test for being attacked.</param>
    /// <param name="square">The square to test for attacks.</param>
    /// <returns>Whether the given side is attacked on the given square</returns>
    public bool IsAttacked(int colour, int square)
    {
        int enemy = 1 - colour;

        if ((PieceBitboards[enemy | Piece.Knight] & AttackBitboard.Knight(square)) != 0
         || (PieceBitboards[enemy | Piece.Pawn] & AttackBitboard.Pawn(square, colour)) != 0
         || (PieceBitboards[enemy | Piece.King] & AttackBitboard.King(square)) != 0)
            return true;

        ulong bishopQueenBitboard = PieceBitboards[enemy | Piece.Bishop] | PieceBitboards[enemy | Piece.Queen];
        if ((bishopQueenBitboard & Bitboard.Diagonals[square]) != 0
         && (bishopQueenBitboard & AttackBitboard.Bishop(square, OccupiedBitboard)) != 0)
            return true;

        ulong rookQueenBitboard = PieceBitboards[enemy | Piece.Rook] | PieceBitboards[enemy | Piece.Queen];
        if ((rookQueenBitboard & Bitboard.Axes[square]) != 0
         && (rookQueenBitboard & AttackBitboard.Rook(square, OccupiedBitboard)) != 0)
            return true;

        return false;
    }

    /// <summary>
    /// Returns the squares of the enemy pieces that give check to the given side, in ascending square order;
    /// empty when the side is not in check. Two entries mean a double check. The squares between a checking
    /// slider and the king, where a block would go, are <see cref="Square.Between"/>.
    /// </summary>
    /// <param name="colour">The side whose king is tested.</param>
    /// <returns>The squares of the checking pieces.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The colour is neither colour.</exception>
    public List<int> Checkers(int colour)
    {
        CheckColour(colour);
        ulong king = PieceBitboards[colour | Piece.King];
        return king == 0 ? new List<int>() : AttackersOf(1 - colour, Bitboard.Read(king));
    }

    /// <summary>
    /// Returns the pieces of the given colour that are pinned to their king, in ascending order of the pinned
    /// square. Only absolute pins count (a pin against the king, not against a more valuable piece), and the
    /// pinned piece may still move along the pin line; <see cref="LegalMovesFrom"/> says where it can go.
    /// </summary>
    /// <param name="colour">The side whose pieces are tested.</param>
    /// <returns>The pins; the line of each is <see cref="Square.Between"/> the king and the pinner.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The colour is neither colour.</exception>
    public List<Pin> Pins(int colour)
    {
        CheckColour(colour);
        List<Pin> pins = new List<Pin>();
        ulong kingBitboard = PieceBitboards[colour | Piece.King];
        if (kingBitboard == 0)
            return pins;

        int king = Bitboard.Read(kingBitboard);
        int enemy = 1 - colour;
        ulong sliders = PieceBitboards[enemy | Piece.Bishop] | PieceBitboards[enemy | Piece.Rook] | PieceBitboards[enemy | Piece.Queen];
        while (sliders != 0)
        {
            int pinner = Bitboard.Pop(ref sliders);
            if (!Square.SameLine(king, pinner))
                continue;

            // A bishop moves on diagonals only and a rook on files and ranks only; a queen on either.
            bool straight = Square.File(king) == Square.File(pinner) || Square.Rank(king) == Square.Rank(pinner);
            int kind = Board[pinner] & Piece.Mask;
            if ((kind == Piece.Bishop && straight) || (kind == Piece.Rook && !straight))
                continue;

            int blocker = -1;
            int blockers = 0;
            foreach (int square in Square.Between(king, pinner))
            {
                if (Board[square] == Piece.Empty)
                    continue;
                blocker = square;
                blockers++;
            }
            if (blockers == 1 && (Board[blocker] & Colour.Mask) == colour)
                pins.Add(new Pin(blocker, pinner));
        }
        pins.Sort((a, b) => a.Pinned.CompareTo(b.Pinned));
        return pins;
    }

    /// <summary>
    /// Returns the squares of the pieces that defend the piece on the given square: the pieces of its own
    /// colour that attack the square, so that a capture there could be answered by a recapture. Empty when the
    /// square is empty or the piece is undefended.
    /// </summary>
    /// <param name="square">The square of the defended piece.</param>
    /// <returns>The squares of the defenders.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The square is outside 0 to 63.</exception>
    public List<int> DefendersOf(int square)
    {
        if ((uint)square > 63)
            throw new ArgumentOutOfRangeException(nameof(square));

        int piece = Board[square];
        return piece == Piece.Empty ? new List<int>() : AttackersOf(piece & Colour.Mask, square);
    }

    /// <summary>
    /// Returns the squares of the given colour's pieces that are attacked and undefended, in ascending square
    /// order. Kings are never listed. This is the simple teaching sense of a hanging piece: a defended piece
    /// that an enemy pawn or other cheaper piece attacks can still lose material, and is not listed.
    /// </summary>
    /// <param name="colour">The side whose pieces are tested.</param>
    /// <returns>The squares of the hanging pieces.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The colour is neither colour.</exception>
    public List<int> HangingPieces(int colour)
    {
        CheckColour(colour);
        List<int> hanging = new List<int>();
        ulong pieces = PieceBitboards[colour] & ~PieceBitboards[colour | Piece.King];
        while (pieces != 0)
        {
            int square = Bitboard.Pop(ref pieces);
            if (IsAttacked(colour, square) && DefendersOf(square).Count == 0)
                hanging.Add(square);
        }
        return hanging;
    }

    private static void CheckColour(int colour)
    {
        if (colour != Colour.White && colour != Colour.Black)
            throw new ArgumentOutOfRangeException(nameof(colour));
    }

    /// <summary>
    /// Returns the squares of the pieces of the given colour that attack the given square (whatever stands on it),
    /// in ascending square order. With a king's square this lists the checkers.
    /// </summary>
    /// <param name="colour">The colour of the attacking pieces.</param>
    /// <param name="square">The square attacked.</param>
    /// <returns>The squares of the attackers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The colour is neither colour or the square is outside 0 to 63.</exception>
    public List<int> AttackersOf(int colour, int square)
    {
        CheckColour(colour);
        if ((uint)square > 63)
            throw new ArgumentOutOfRangeException(nameof(square));

        ulong bishopQueen = PieceBitboards[colour | Piece.Bishop] | PieceBitboards[colour | Piece.Queen];
        ulong rookQueen = PieceBitboards[colour | Piece.Rook] | PieceBitboards[colour | Piece.Queen];
        ulong attackers = (PieceBitboards[colour | Piece.Knight] & AttackBitboard.Knight(square))
                        | (PieceBitboards[colour | Piece.Pawn] & AttackBitboard.Pawn(square, 1 - colour))
                        | (PieceBitboards[colour | Piece.King] & AttackBitboard.King(square))
                        | (bishopQueen & AttackBitboard.Bishop(square, OccupiedBitboard))
                        | (rookQueen & AttackBitboard.Rook(square, OccupiedBitboard));
        return ToSquares(attackers);
    }

    /// <summary>
    /// Returns the squares the piece on the given square attacks, in ascending square order, whether they are
    /// empty, hold an enemy piece or hold one of its own (which it then defends). Sliders stop at the first
    /// piece. This is attack geometry, not legal moves: it ignores pins, checks, pawn pushes and castling.
    /// </summary>
    /// <param name="square">The square of the piece.</param>
    /// <returns>The attacked squares; empty for an empty square.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The square is outside 0 to 63.</exception>
    public List<int> AttacksFrom(int square)
    {
        if ((uint)square > 63)
            throw new ArgumentOutOfRangeException(nameof(square));

        int piece = Board[square];
        ulong attacks;
        switch (piece & Piece.Mask)
        {
            case Piece.Pawn: attacks = AttackBitboard.Pawn(square, piece & Colour.Mask); break;
            case Piece.Knight: attacks = AttackBitboard.Knight(square); break;
            case Piece.Bishop: attacks = AttackBitboard.Bishop(square, OccupiedBitboard); break;
            case Piece.Rook: attacks = AttackBitboard.Rook(square, OccupiedBitboard); break;
            case Piece.Queen: attacks = AttackBitboard.Queen(square, OccupiedBitboard); break;
            case Piece.King: attacks = AttackBitboard.King(square); break;
            default: attacks = 0; break;
        }
        return ToSquares(attacks);
    }

    private static List<int> ToSquares(ulong bitboard)
    {
        List<int> squares = new List<int>();
        while (bitboard != 0)
            squares.Add(Bitboard.Pop(ref bitboard));
        return squares;
    }

    /// <summary>
    /// Returns whether the given move puts the opponent in check.
    /// </summary>
    /// <param name="move">The move to test for check.</param>
    /// <returns>Whether the given move puts the opponent in check.</returns>
    public bool CausesCheck(int move)
    {
        ulong fromBitboard = 1UL << Move.From(move);
        ulong toBitboard = 1UL << Move.To(move);
        int piece = Move.MovingPiece(move);
        int special = Move.SpecialPiece(move);
        ulong occupiedBitboardCopy = OccupiedBitboard;

        bool value = false;
        switch (special & Piece.Mask)
        {

            // Consider normal move.
            case Piece.Empty:
                PieceBitboards[piece] ^= fromBitboard | toBitboard;
                OccupiedBitboard ^= fromBitboard;
                OccupiedBitboard |= toBitboard;
                value = InCheck(1 - SideToMove);
                PieceBitboards[piece] ^= fromBitboard | toBitboard;
                OccupiedBitboard = occupiedBitboardCopy;
                break;

            // Consider castling.
            case Piece.King:
                ulong rookToBitboard = 1UL << ((toBitboard < fromBitboard ? 3 : 5) + Square.Rank(Move.To(move)) * 8);
                PieceBitboards[SideToMove | Piece.Rook] ^= rookToBitboard;
                OccupiedBitboard ^= fromBitboard;
                value = InCheck(1 - SideToMove);
                PieceBitboards[SideToMove | Piece.Rook] ^= rookToBitboard;
                OccupiedBitboard = occupiedBitboardCopy;
                break;

            // Consider en passant.
            case Piece.Pawn:
                ulong enPassantPawnBitboard = Move.Pawn(EnPassantSquare, 1 - SideToMove);
                PieceBitboards[piece] ^= fromBitboard | toBitboard;
                OccupiedBitboard ^= fromBitboard | toBitboard | enPassantPawnBitboard;
                value = InCheck(1 - SideToMove);
                PieceBitboards[piece] ^= fromBitboard | toBitboard;
                OccupiedBitboard = occupiedBitboardCopy;
                break;

            // Consider pawn promotion.
            default:
                PieceBitboards[SideToMove | Piece.Pawn] ^= fromBitboard;
                PieceBitboards[special] ^= toBitboard;
                OccupiedBitboard ^= fromBitboard;
                OccupiedBitboard |= toBitboard;
                value = InCheck(1 - SideToMove);
                PieceBitboards[SideToMove | Piece.Pawn] ^= fromBitboard;
                PieceBitboards[special] ^= toBitboard;
                OccupiedBitboard = occupiedBitboardCopy;
                break;
        }
        return value;
    }

    /// <summary>
    /// Returns whether the position represents a draw by insufficient material: a bare king, a single minor
    /// piece against a bare king, or one bishop each on squares of the same colour. A king and two knights
    /// against a bare king is also reported as a draw, as mate cannot be forced although it can be reached
    /// with the defender's help; pawns, rooks and queens always count as sufficient.
    /// </summary>
    /// <returns>Whether the position represents a draw by insufficient material.</returns>
    public bool InsufficientMaterial()
    {
        int pieces = Bitboard.Count(OccupiedBitboard);
        if (pieces > 4)
            return false;
        if (pieces <= 2)
            return true;
        if (pieces <= 3)
            return PieceBitboards[Colour.White | Piece.Knight] != 0
                || PieceBitboards[Colour.White | Piece.Bishop] != 0
                || PieceBitboards[Colour.Black | Piece.Knight] != 0
                || PieceBitboards[Colour.Black | Piece.Bishop] != 0;
        if (Bitboard.CountSparse(PieceBitboards[Colour.White | Piece.Knight]) == 2
         || Bitboard.CountSparse(PieceBitboards[Colour.Black | Piece.Knight]) == 2)
            return true;
        if (PieceBitboards[Colour.White | Piece.Bishop] != 0
         && PieceBitboards[Colour.Black | Piece.Bishop] != 0
         && ((PieceBitboards[Colour.White | Piece.Bishop] & Bitboard.LightSquares) != 0)
         == ((PieceBitboards[Colour.Black | Piece.Bishop] & Bitboard.LightSquares) != 0))
            return true;
        return false;
    }

    /// <summary>
    /// Returns whether the position has repeated the given number of times.
    /// </summary>
    /// <param name="times">The number of repetitions to test for.</param>
    /// <returns>Whether the position has repeated the given number of times.</returns>
    public bool HasRepeated(int times)
    {
        // Nothing before the root of the game was recorded, so the scan must not read below it.
        int repetitions = 1;
        for (int i = HalfMoves - 4; i >= Math.Max(_rootHalfMoves, HalfMoves - FiftyMovesClock); i -= 2)
            if (ZobristKeyHistory[i] == ZobristKey)
                if (++repetitions >= times)
                    return true;
        return false;
    }
}
