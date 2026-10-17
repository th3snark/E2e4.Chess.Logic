using System;
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
    public Boolean InCheck(Int32 colour)
    {
        return IsAttacked(colour, Bitboard.Read(PieceBitboards[colour | Piece.King]));
    }

    /// <summary>
    /// Returns whether the given side is attacked on the given square.
    /// </summary>
    /// <param name="colour">The side to test for being attacked.</param>
    /// <param name="square">The square to test for attacks.</param>
    /// <returns>Whether the given side is attacked on the given square</returns>
    public Boolean IsAttacked(Int32 colour, Int32 square)
    {
        Int32 enemy = 1 - colour;

        if ((PieceBitboards[enemy | Piece.Knight] & AttackBitboard.Knight(square)) != 0
         || (PieceBitboards[enemy | Piece.Pawn] & AttackBitboard.Pawn(square, colour)) != 0
         || (PieceBitboards[enemy | Piece.King] & AttackBitboard.King(square)) != 0)
            return true;

        UInt64 bishopQueenBitboard = PieceBitboards[enemy | Piece.Bishop] | PieceBitboards[enemy | Piece.Queen];
        if ((bishopQueenBitboard & Bitboard.Diagonals[square]) != 0
         && (bishopQueenBitboard & AttackBitboard.Bishop(square, OccupiedBitboard)) != 0)
            return true;

        UInt64 rookQueenBitboard = PieceBitboards[enemy | Piece.Rook] | PieceBitboards[enemy | Piece.Queen];
        if ((rookQueenBitboard & Bitboard.Axes[square]) != 0
         && (rookQueenBitboard & AttackBitboard.Rook(square, OccupiedBitboard)) != 0)
            return true;

        return false;
    }

    /// <summary>
    /// Returns whether the given move puts the opponent in check.
    /// </summary>
    /// <param name="move">The move to test for check.</param>
    /// <returns>Whether the given move puts the opponent in check.</returns>
    public Boolean CausesCheck(Int32 move)
    {
        UInt64 fromBitboard = 1UL << Move.From(move);
        UInt64 toBitboard = 1UL << Move.To(move);
        Int32 piece = Move.MovingPiece(move);
        Int32 special = Move.SpecialPiece(move);
        UInt64 occupiedBitboardCopy = OccupiedBitboard;

        Boolean value = false;
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
                UInt64 rookToBitboard = 1UL << ((toBitboard < fromBitboard ? 3 : 5) + Square.Rank(Move.To(move)) * 8);
                PieceBitboards[SideToMove | Piece.Rook] ^= rookToBitboard;
                OccupiedBitboard ^= fromBitboard;
                value = InCheck(1 - SideToMove);
                PieceBitboards[SideToMove | Piece.Rook] ^= rookToBitboard;
                OccupiedBitboard = occupiedBitboardCopy;
                break;

            // Consider en passant.
            case Piece.Pawn:
                UInt64 enPassantPawnBitboard = Move.Pawn(EnPassantSquare, 1 - SideToMove);
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
    public Boolean InsufficientMaterial()
    {
        Int32 pieces = Bitboard.Count(OccupiedBitboard);
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
    public Boolean HasRepeated(Int32 times)
    {
        // Nothing before the root of the game was recorded, so the scan must not read below it.
        Int32 repetitions = 1;
        for (Int32 i = HalfMoves - 4; i >= Math.Max(_rootHalfMoves, HalfMoves - FiftyMovesClock); i -= 2)
            if (ZobristKeyHistory[i] == ZobristKey)
                if (++repetitions >= times)
                    return true;
        return false;
    }
}
