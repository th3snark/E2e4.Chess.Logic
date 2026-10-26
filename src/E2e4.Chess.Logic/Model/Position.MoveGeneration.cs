using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using E2e4.Chess.Logic.Bitboards;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Encapsulates the move generation component of the chess position.
/// </summary>
public sealed partial class Position : IEquatable<Position>
{

    /// <summary>
    /// Returns the list of legal moves for the position. Don't use this overload
    /// when performance is important. Legality is tested by making and unmaking moves on this
    /// instance, so a <see cref="Position"/> must not be used from several threads at once;
    /// give each thread its own <see cref="DeepClone"/>.
    /// </summary>
    /// <returns>The list of legal moves for the position.</returns>
    public List<int> LegalMoves()
    {
        int[] moves = new int[MaxMoves];
        int movesCount = LegalMoves(moves);
        List<int> list = new List<int>(movesCount);
        for (int i = 0; i < movesCount; i++)
            list.Add(moves[i]);
        return list;
    }

    /// <summary>
    /// Returns the legal moves of the piece on the given square for the side to move; empty when the square is
    /// empty or holds a piece of the other side. Each promotion counts as a separate move.
    /// </summary>
    /// <param name="square">The square of the piece.</param>
    /// <returns>The legal moves from the square.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The square is outside 0 to 63.</exception>
    public List<int> LegalMovesFrom(int square)
    {
        if ((uint)square > 63)
            throw new ArgumentOutOfRangeException(nameof(square));

        List<int> moves = LegalMoves();
        moves.RemoveAll(move => Move.From(move) != square);
        return moves;
    }

    /// <summary>
    /// Populates the given array with the legal moves for the position and
    /// returns the number of legal moves.
    /// </summary>
    /// <param name="moves">The array to populate with the legal moves; it must have room for
    /// <see cref="MaxMoves"/> entries.</param>
    /// <returns>The number of legal moves for the position.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="moves"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="moves"/> is shorter than
    /// <see cref="MaxMoves"/>.</exception>
    public int LegalMoves(int[] moves)
    {
        if (moves == null)
            throw new ArgumentNullException(nameof(moves));
        if (moves.Length < MaxMoves)
            throw new ArgumentException("The array must have room for at least " + MaxMoves + " moves.", nameof(moves));

        return GenerateLegalMoves(moves);
    }

#if NETSTANDARD2_1 || NET
    /// <summary>
    /// Populates the start of the given span with the legal moves for the position and
    /// returns the number of legal moves. Unlike the array overload this accepts any slice
    /// of a larger buffer, or stack memory. Not available on the older target frameworks,
    /// which have no <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="moves">The span to populate with the legal moves; it must have room for
    /// <see cref="MaxMoves"/> entries.</param>
    /// <returns>The number of legal moves for the position.</returns>
    /// <exception cref="ArgumentException"><paramref name="moves"/> is shorter than
    /// <see cref="MaxMoves"/>.</exception>
    public int LegalMoves(Span<int> moves)
    {
        if (moves.Length < MaxMoves)
            throw new ArgumentException("The span must have room for at least " + MaxMoves + " moves.", nameof(moves));

        return GenerateLegalMoves(moves);
    }

    private int GenerateLegalMoves(Span<int> moves)
#else
    private int GenerateLegalMoves(int[] moves)
#endif
    {
        // Initialize bitboards and squares that describe the position.
        int enemy = 1 - SideToMove;
        int kingSquare = Bitboard.Read(PieceBitboards[SideToMove | Piece.King]);

        ulong selfBitboard = PieceBitboards[SideToMove];
        ulong enemyBitboard = PieceBitboards[enemy];
        ulong targetBitboard = ~selfBitboard;

        ulong enemyBishopQueenBitboard = PieceBitboards[enemy | Piece.Bishop] | PieceBitboards[enemy | Piece.Queen];
        ulong enemyRookQueenBitboard = PieceBitboards[enemy | Piece.Rook] | PieceBitboards[enemy | Piece.Queen];

        // Initialize variables for move generation.
        ulong checkingBitboard = 0;
        ulong pinningBitboard = 0;
        int index = 0;

        // Consider knight and pawn checks.
        checkingBitboard |= PieceBitboards[enemy | Piece.Knight] & AttackBitboard.Knight(kingSquare);
        checkingBitboard |= PieceBitboards[enemy | Piece.Pawn] & AttackBitboard.Pawn(kingSquare, SideToMove);

        // Consider bishop and queen checks and pins.
        if ((enemyBishopQueenBitboard & Bitboard.Diagonals[kingSquare]) != 0)
        {
            checkingBitboard |= enemyBishopQueenBitboard & AttackBitboard.Bishop(kingSquare, OccupiedBitboard);

            // Determine pinning pieces by removing the first line of defence around the
            // the king, then seeing which pieces are able to attack.
            ulong defenceRemovedBitboard = OccupiedBitboard;
            ulong defenceBitboard = Bitboard.RayNE[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.IsolateReverse(defenceBitboard);
            defenceBitboard = Bitboard.RayNW[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.IsolateReverse(defenceBitboard);
            defenceBitboard = Bitboard.RaySE[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.Isolate(defenceBitboard);
            defenceBitboard = Bitboard.RaySW[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.Isolate(defenceBitboard);

            if (defenceRemovedBitboard != OccupiedBitboard)
                pinningBitboard |= enemyBishopQueenBitboard & AttackBitboard.Bishop(kingSquare, defenceRemovedBitboard);
        }

        // Consider rook and queen checks and pins.
        if ((enemyRookQueenBitboard & Bitboard.Axes[kingSquare]) != 0)
        {
            checkingBitboard |= enemyRookQueenBitboard & AttackBitboard.Rook(kingSquare, OccupiedBitboard);

            // Determine pinning pieces by removing the first line of defence around the
            // the king, then seeing which pieces are able to attack.
            ulong defenceRemovedBitboard = OccupiedBitboard;
            ulong defenceBitboard = Bitboard.RayN[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.IsolateReverse(defenceBitboard);
            defenceBitboard = Bitboard.RayE[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.Isolate(defenceBitboard);
            defenceBitboard = Bitboard.RayS[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.Isolate(defenceBitboard);
            defenceBitboard = Bitboard.RayW[kingSquare] & selfBitboard;
            if (defenceBitboard != 0)
                defenceRemovedBitboard ^= Bitboard.IsolateReverse(defenceBitboard);

            if (defenceRemovedBitboard != OccupiedBitboard)
                pinningBitboard |= enemyRookQueenBitboard & AttackBitboard.Rook(kingSquare, defenceRemovedBitboard);
        }

        // Consider castling. This is always fully tested for legality.
        if (checkingBitboard == 0)
        {
            int rank = -56 * SideToMove + 56;

            if ((Castling & (SideToMove == Colour.White ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside)) != 0 && (Board[1 + rank] | Board[2 + rank] | Board[3 + rank]) == Piece.Empty)
                if (!IsAttacked(SideToMove, 3 + rank) && !IsAttacked(SideToMove, 2 + rank))
                    moves[index++] = Move.Create(this, kingSquare, 2 + rank, SideToMove | Piece.King);

            if ((Castling & (SideToMove == Colour.White ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside)) != 0 && (Board[5 + rank] | Board[6 + rank]) == Piece.Empty)
                if (!IsAttacked(SideToMove, 5 + rank) && !IsAttacked(SideToMove, 6 + rank))
                    moves[index++] = Move.Create(this, kingSquare, 6 + rank, SideToMove | Piece.King);
        }

        // Consider en passant. This is always fully tested for legality.
        if (EnPassantSquare != InvalidSquare)
        {

            ulong enPassantPawnBitboard = PieceBitboards[SideToMove | Piece.Pawn] & AttackBitboard.Pawn(EnPassantSquare, enemy);
            ulong enPassantVictimBitboard = Move.Pawn(EnPassantSquare, enemy);
            while (enPassantPawnBitboard != 0)
            {

                // Perform minimal state changes to mimic en passant and check for
                // legality.
                int from = Bitboard.Pop(ref enPassantPawnBitboard);
                PieceBitboards[enemy | Piece.Pawn] ^= enPassantVictimBitboard;
                OccupiedBitboard ^= enPassantVictimBitboard;
                OccupiedBitboard ^= (1UL << from) | (1UL << EnPassantSquare);

                // Check for legality and add move.
                if (!IsAttacked(SideToMove, kingSquare))
                    moves[index++] = Move.Create(this, from, EnPassantSquare, enemy | Piece.Pawn);

                // Revert state changes.
                PieceBitboards[enemy | Piece.Pawn] ^= enPassantVictimBitboard;
                OccupiedBitboard ^= enPassantVictimBitboard;
                OccupiedBitboard ^= (1UL << from) | (1UL << EnPassantSquare);
            }
        }

        // Consider king moves. This is always fully tested for legality.
        {
            int from = kingSquare;
            ulong moveBitboard = targetBitboard & AttackBitboard.King(from);
            while (moveBitboard != 0)
            {
                int to = Bitboard.Pop(ref moveBitboard);
                if (LeavesKingSafe(from, to, to))
                    moves[index++] = Move.Create(this, from, to);
            }
        }

        // Consider the other pieces. If we are not in check and there are no pinned pieces, none of their moves
        // can be illegal, so they need no test. If there are pinned pieces or a single check we can still move,
        // but every move is tested. If there is a double check only the king can move, so there is nothing to do.
        bool testForLegality = checkingBitboard != 0 | pinningBitboard != 0;
        if (!testForLegality || (checkingBitboard & (checkingBitboard - 1)) == 0)
        {

            // Consider pawn moves.
            ulong pieceBitboard = PieceBitboards[SideToMove | Piece.Pawn];
            while (pieceBitboard != 0)
            {

                // Consider single square advance.
                int from = Bitboard.Pop(ref pieceBitboard);
                int to = from + 16 * SideToMove - 8;
                ulong moveBitboard = ~OccupiedBitboard & (1UL << to);

                // Consider two square advance.
                if (moveBitboard != 0 && (from - 16) * (from - 47) > 0 && (to - 8) * (to - 55) < 0)
                    moveBitboard |= ~OccupiedBitboard & (1UL << (from + 32 * SideToMove - 16));

                // Consider captures.
                ulong attackBitboard = AttackBitboard.Pawn(from, SideToMove);
                moveBitboard |= enemyBitboard & attackBitboard;

                // Populate pawn moves.
                while (moveBitboard != 0)
                {
                    to = Bitboard.Pop(ref moveBitboard);
                    if (testForLegality && !LeavesKingSafe(from, to, kingSquare))
                        continue;

                    if ((to - 8) * (to - 55) > 0)
                    {
                        moves[index++] = Move.Create(this, from, to, SideToMove | Piece.Queen);
                        moves[index++] = Move.Create(this, from, to, SideToMove | Piece.Knight);
                        moves[index++] = Move.Create(this, from, to, SideToMove | Piece.Rook);
                        moves[index++] = Move.Create(this, from, to, SideToMove | Piece.Bishop);
                    }
                    else
                        moves[index++] = Move.Create(this, from, to);
                }
            }

            // Consider knight moves.
            pieceBitboard = PieceBitboards[SideToMove | Piece.Knight];
            while (pieceBitboard != 0)
            {
                int from = Bitboard.Pop(ref pieceBitboard);
                ulong moveBitboard = targetBitboard & AttackBitboard.Knight(from);
                while (moveBitboard != 0)
                {
                    int to = Bitboard.Pop(ref moveBitboard);
                    if (!testForLegality || LeavesKingSafe(from, to, kingSquare))
                        moves[index++] = Move.Create(this, from, to);
                }
            }

            // Consider bishop moves.
            pieceBitboard = PieceBitboards[SideToMove | Piece.Bishop];
            while (pieceBitboard != 0)
            {
                int from = Bitboard.Pop(ref pieceBitboard);
                ulong moveBitboard = targetBitboard & AttackBitboard.Bishop(from, OccupiedBitboard);
                while (moveBitboard != 0)
                {
                    int to = Bitboard.Pop(ref moveBitboard);
                    if (!testForLegality || LeavesKingSafe(from, to, kingSquare))
                        moves[index++] = Move.Create(this, from, to);
                }
            }

            // Consider queen moves.
            pieceBitboard = PieceBitboards[SideToMove | Piece.Queen];
            while (pieceBitboard != 0)
            {
                int from = Bitboard.Pop(ref pieceBitboard);
                ulong moveBitboard = targetBitboard & AttackBitboard.Queen(from, OccupiedBitboard);
                while (moveBitboard != 0)
                {
                    int to = Bitboard.Pop(ref moveBitboard);
                    if (!testForLegality || LeavesKingSafe(from, to, kingSquare))
                        moves[index++] = Move.Create(this, from, to);
                }
            }

            // Consider rook moves.
            pieceBitboard = PieceBitboards[SideToMove | Piece.Rook];
            while (pieceBitboard != 0)
            {
                int from = Bitboard.Pop(ref pieceBitboard);
                ulong moveBitboard = targetBitboard & AttackBitboard.Rook(from, OccupiedBitboard);
                while (moveBitboard != 0)
                {
                    int to = Bitboard.Pop(ref moveBitboard);
                    if (!testForLegality || LeavesKingSafe(from, to, kingSquare))
                        moves[index++] = Move.Create(this, from, to);
                }
            }
        }
        return index;
    }

    /// <summary>
    /// Performs the minimal state changes to mimic moving a piece from one square to another, reports whether our king
    /// on <paramref name="kingSquare"/> is then safe from attack, and reverts the changes. Only the bitboards are
    /// touched, never <c>Board</c>, so a move can be created from the position afterwards.
    /// </summary>
    /// <param name="from">The square the piece moves from.</param>
    /// <param name="to">The square the piece moves to, which may hold an enemy piece.</param>
    /// <param name="kingSquare">The square of our king after the move (<paramref name="to"/> when the king moves).</param>
    /// <returns>Whether our king is not attacked after the move.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool LeavesKingSafe(int from, int to, int kingSquare)
    {
        ulong occupiedBitboardCopy = OccupiedBitboard;
        int capture = Board[to];
        PieceBitboards[capture] ^= 1UL << to;
        OccupiedBitboard ^= 1UL << from;
        OccupiedBitboard |= 1UL << to;

        bool safe = !IsAttacked(SideToMove, kingSquare);

        PieceBitboards[capture] ^= 1UL << to;
        OccupiedBitboard = occupiedBitboardCopy;
        return safe;
    }
}
