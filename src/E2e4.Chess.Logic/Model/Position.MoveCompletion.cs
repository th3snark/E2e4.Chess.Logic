using System;
using E2e4.Chess.Logic.Hashing;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Encapsulates the move making component of the chess position.
/// </summary>
public sealed partial class Position : IEquatable<Position>
{
    /// <summary>
    /// For each square, the castling rights that survive a move that starts or ends there. A king
    /// leaving its home square loses both of its side's rights, a rook leaving (or being captured
    /// on) its corner loses that side's right, and every other square keeps everything.
    /// </summary>
    private static readonly CastlingRights[] CastlingMask = BuildCastlingMask();

    private static CastlingRights[] BuildCastlingMask()
    {
        CastlingRights[] mask = new CastlingRights[64];
        for (int square = 0; square < mask.Length; square++)
            mask[square] = CastlingRights.All;

        mask[0] = CastlingRights.All & ~CastlingRights.BlackQueenside;                                // a8
        mask[4] = CastlingRights.All & ~(CastlingRights.BlackKingside | CastlingRights.BlackQueenside); // e8
        mask[7] = CastlingRights.All & ~CastlingRights.BlackKingside;                                 // h8
        mask[56] = CastlingRights.All & ~CastlingRights.WhiteQueenside;                               // a1
        mask[60] = CastlingRights.All & ~(CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside); // e1
        mask[63] = CastlingRights.All & ~CastlingRights.WhiteKingside;                                // h1
        return mask;
    }

    /// <summary>
    /// For a castling move of the king from <paramref name="from"/> to <paramref name="to"/>, gives the squares the
    /// rook moves between: the corner and the square the king crossed. Queenside is the king moving towards a lower
    /// square number.
    /// </summary>
    private static void CastlingRookSquares(int from, int to, out int rookFrom, out int rookTo)
    {
        int rankStart = Square.Rank(to) * 8;
        if (to < from)
        {
            rookFrom = rankStart;
            rookTo = 3 + rankStart;
        }
        else
        {
            rookFrom = 7 + rankStart;
            rookTo = 5 + rankStart;
        }
    }

    /// <summary>
    /// Makes the given move on the position. The move must be legal in the position (see
    /// <see cref="LegalMoves()"/>); making an illegal move leaves the position inconsistent.
    /// </summary>
    /// <param name="move">The move to make.</param>
    /// <exception cref="InvalidOperationException">The position has reached
    /// <see cref="HalfMovesLimit"/> plies.</exception>
    public void Make(int move)
    {
        if (HalfMoves >= HalfMovesLimit - 1)
            throw new InvalidOperationException("The position cannot advance beyond " + HalfMovesLimit + " plies.");

        int from = Move.From(move);
        int to = Move.To(move);
        int piece = Move.MovingPiece(move);
        int capture = Move.CapturedPiece(move);
        int special = Move.SpecialPiece(move);

        // Update core board state.
        Board[to] = piece;
        Board[from] = Piece.Empty;
        PieceBitboards[piece] ^= (1UL << from) | (1UL << to);
        PieceBitboards[SideToMove] ^= (1UL << from) | (1UL << to);
        OccupiedBitboard ^= (1UL << from) | (1UL << to);

        // Update metainformation.
        ZobristKey ^= Zobrist.PiecePosition[piece][from] ^ Zobrist.PiecePosition[piece][to];
        ZobristKey ^= Zobrist.ColourToMove;
        if (EnPassantSquare != InvalidSquare)
        {
            // The en passant key depends only on the file, so this cancels the key added below.
            ZobristKey ^= Zobrist.EnPassant[EnPassantSquare];
            EnPassantSquare = InvalidSquare;
        }
        FiftyMovesClock++;
        HalfMoves++;

        // A move from or to a king or rook home square forfeits the matching castling rights.
        // This covers king moves, rook moves, castling itself and capturing a rook on its corner.
        CastlingRights rights = Castling & CastlingMask[from] & CastlingMask[to];
        if (rights != Castling)
        {
            ZobristKey ^= Zobrist.Castling[(int)Castling] ^ Zobrist.Castling[(int)rights];
            Castling = rights;
        }

        // Handle capture if applicable.
        if ((capture & Piece.Mask) != Piece.Empty)
        {
            PieceBitboards[capture] ^= 1UL << to;
            PieceBitboards[(1 - SideToMove)] ^= 1UL << to;
            OccupiedBitboard |= 1UL << to;
            ZobristKey ^= Zobrist.PiecePosition[capture][to];
            FiftyMovesClock = 0;
        }

        switch (special & Piece.Mask)
        {
            // Handle regular move (not en passant, castling, or pawn promotion).
            case Piece.Empty:
                // For pawn move, update fifty moves clock and en passant state.
                if ((piece & Piece.Mask) == Piece.Pawn)
                {
                    FiftyMovesClock = 0;
                    if ((from - to) * (from - to) == 256)
                    {
                        // Hashed after every double push, even when no capture is possible, so
                        // positions differing only by an unusable en passant square hash differently.
                        // The key is per file, so indexing by the origin square equals indexing by
                        // the skipped square; keep Zobrist.EnPassant per-file or change both places.
                        ZobristKey ^= Zobrist.EnPassant[from];
                        EnPassantHistory[HalfMoves] = EnPassantSquare = (from + to) / 2;
                    }
                }
                break;
            // Handle castling: the king has moved, now move the rook.
            case Piece.King:
                CastlingRookSquares(from, to, out int rookFrom, out int rookTo);
                PieceBitboards[SideToMove | Piece.Rook] ^= (1UL << rookFrom) | (1UL << rookTo);
                PieceBitboards[SideToMove] ^= (1UL << rookFrom) | (1UL << rookTo);
                OccupiedBitboard ^= (1UL << rookFrom) | (1UL << rookTo);
                ZobristKey ^= Zobrist.PiecePosition[SideToMove | Piece.Rook][rookFrom];
                ZobristKey ^= Zobrist.PiecePosition[SideToMove | Piece.Rook][rookTo];
                Board[rookFrom] = Piece.Empty;
                Board[rookTo] = SideToMove | Piece.Rook;
                break;
            // Handle en passant: remove the pawn that was passed.
            case Piece.Pawn:
                FiftyMovesClock = 0;
                int victim = Square.File(to) + Square.Rank(from) * 8;
                Board[victim] = Piece.Empty;
                PieceBitboards[special] ^= 1UL << victim;
                PieceBitboards[(1 - SideToMove)] ^= 1UL << victim;
                OccupiedBitboard ^= 1UL << victim;
                ZobristKey ^= Zobrist.PiecePosition[special][victim];
                break;
            // Handle pawn promotion.
            default:
                FiftyMovesClock = 0;
                PieceBitboards[piece] ^= 1UL << to;
                PieceBitboards[special] ^= 1UL << to;
                ZobristKey ^= Zobrist.PiecePosition[piece][to];
                ZobristKey ^= Zobrist.PiecePosition[special][to];
                Board[to] = special;
                break;
        }

        SideToMove = 1 - SideToMove;
        FiftyMovesHistory[HalfMoves] = FiftyMovesClock;
        ZobristKeyHistory[HalfMoves] = ZobristKey;
        CastlingHistory[HalfMoves] = Castling;
    }

    /// <summary>
    /// Unmakes the given move from the position. It must be the most recent move made.
    /// </summary>
    /// <param name="move">The move to unmake.</param>
    /// <exception cref="InvalidOperationException">No move has been made on the position.</exception>
    public void Unmake(int move)
    {
        if (HalfMoves <= _rootHalfMoves)
            throw new InvalidOperationException("There is no move to unmake.");

        int from = Move.From(move);
        int to = Move.To(move);
        int piece = Move.MovingPiece(move);
        int capture = Move.CapturedPiece(move);
        int special = Move.SpecialPiece(move);

        // Rewind core board state.
        SideToMove = 1 - SideToMove;
        Board[from] = piece;
        Board[to] = capture;
        PieceBitboards[piece] ^= (1UL << from) | (1UL << to);
        PieceBitboards[SideToMove] ^= (1UL << from) | (1UL << to);
        OccupiedBitboard ^= (1UL << from) | (1UL << to);

        // Rewind metainformation.
        ZobristKey = ZobristKeyHistory[HalfMoves - 1];
        EnPassantHistory[HalfMoves] = InvalidSquare;
        EnPassantSquare = EnPassantHistory[HalfMoves - 1];
        FiftyMovesClock = FiftyMovesHistory[HalfMoves - 1];
        Castling = CastlingHistory[HalfMoves - 1];
        HalfMoves--;

        // Rewind capture if applicable.
        if ((capture & Piece.Mask) != Piece.Empty)
        {
            PieceBitboards[capture] ^= 1UL << to;
            PieceBitboards[(1 - SideToMove)] ^= 1UL << to;
            OccupiedBitboard |= 1UL << to;
        }

        switch (special & Piece.Mask)
        {
            // Rewind regular move: nothing more to restore.
            case Piece.Empty:
                break;
            // Rewind castling: move the rook back.
            case Piece.King:
                CastlingRookSquares(from, to, out int rookFrom, out int rookTo);
                PieceBitboards[SideToMove | Piece.Rook] ^= (1UL << rookFrom) | (1UL << rookTo);
                PieceBitboards[SideToMove] ^= (1UL << rookFrom) | (1UL << rookTo);
                OccupiedBitboard ^= (1UL << rookFrom) | (1UL << rookTo);
                Board[rookFrom] = SideToMove | Piece.Rook;
                Board[rookTo] = Piece.Empty;
                break;
            // Rewind en passant: put the passed pawn back.
            case Piece.Pawn:
                int victim = Square.File(to) + Square.Rank(from) * 8;
                Board[victim] = special;
                PieceBitboards[special] ^= 1UL << victim;
                PieceBitboards[(1 - SideToMove)] ^= 1UL << victim;
                OccupiedBitboard ^= 1UL << victim;
                break;
            // Rewind pawn promotion.
            default:
                PieceBitboards[piece] ^= 1UL << to;
                PieceBitboards[special] ^= 1UL << to;
                break;
        }
    }

    /// <summary>
    /// Makes the null move on the position: the side to move passes. Only meaningful when the
    /// side to move is not in check.
    /// </summary>
    /// <exception cref="InvalidOperationException">The position has reached
    /// <see cref="HalfMovesLimit"/> plies.</exception>
    public void MakeNull()
    {
        if (HalfMoves >= HalfMovesLimit - 1)
            throw new InvalidOperationException("The position cannot advance beyond " + HalfMovesLimit + " plies.");

        ZobristKey ^= Zobrist.ColourToMove;
        if (EnPassantSquare != InvalidSquare)
        {
            ZobristKey ^= Zobrist.EnPassant[EnPassantSquare];
            EnPassantSquare = InvalidSquare;
        }
        SideToMove = 1 - SideToMove;
        FiftyMovesClock++;
        HalfMoves++;
        FiftyMovesHistory[HalfMoves] = FiftyMovesClock;
        ZobristKeyHistory[HalfMoves] = ZobristKey;
        CastlingHistory[HalfMoves] = Castling;
    }

    /// <summary>
    /// Unmakes the null move on the position.
    /// </summary>
    /// <exception cref="InvalidOperationException">No move has been made on the position.</exception>
    public void UnmakeNull()
    {
        if (HalfMoves <= _rootHalfMoves)
            throw new InvalidOperationException("There is no move to unmake.");

        FiftyMovesClock = FiftyMovesHistory[HalfMoves - 1];
        ZobristKey = ZobristKeyHistory[HalfMoves - 1];
        EnPassantSquare = EnPassantHistory[HalfMoves - 1];
        Castling = CastlingHistory[HalfMoves - 1];
        SideToMove = 1 - SideToMove;
        HalfMoves--;
    }
}
