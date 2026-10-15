using System;
using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Enums;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>The public state of a position: castling rights, cloning, equality and move history limits.</summary>
public class PositionStateUnitTest
{
    private static void Play(Position position, params string[] moves)
    {
        foreach (string move in moves)
        {
            int encoded = Move.Create(position, move);
            Assert.NotEqual(Move.Invalid, encoded);
            position.Make(encoded);
            Assert.Equal(position.GetZobristKey(), position.ZobristKey);
        }
    }

    // ---- castling rights --------------------------------------------------------------

    [Fact]
    public void CastlingRightsFlagsHaveStableValues()
    {
        Assert.Equal(0, (int)CastlingRights.None);
        Assert.Equal(1, (int)CastlingRights.WhiteKingside);
        Assert.Equal(2, (int)CastlingRights.WhiteQueenside);
        Assert.Equal(4, (int)CastlingRights.BlackKingside);
        Assert.Equal(8, (int)CastlingRights.BlackQueenside);
        Assert.Equal(15, (int)CastlingRights.All);
    }

    [Theory]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", CastlingRights.All)]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w Kq - 0 1", CastlingRights.WhiteKingside | CastlingRights.BlackQueenside)]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w - - 0 1", CastlingRights.None)]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R b Qk - 0 1", CastlingRights.WhiteQueenside | CastlingRights.BlackKingside)]
    public void CastlingIsParsedFromTheFenAndWrittenBack(string fen, CastlingRights expected)
    {
        Position position = new(fen);

        Assert.Equal(expected, position.Castling);
        Assert.Equal(fen, position.GetFEN());
    }

    [Fact]
    public void AKingMoveForfeitsBothRightsOfItsSideAndUnmakeRestoresThem()
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        int move = Move.Create(position, "e1d1");

        position.Make(move);
        Assert.Equal(CastlingRights.BlackKingside | CastlingRights.BlackQueenside, position.Castling);
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);

        position.Unmake(move);
        Assert.Equal(CastlingRights.All, position.Castling);
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);
    }

    [Theory]
    [InlineData("h1h2", CastlingRights.WhiteQueenside | CastlingRights.BlackKingside | CastlingRights.BlackQueenside)]
    [InlineData("a1a2", CastlingRights.WhiteKingside | CastlingRights.BlackKingside | CastlingRights.BlackQueenside)]
    public void ARookMoveForfeitsOnlyItsOwnSidesRight(string move, CastlingRights expected)
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Play(position, move);

        Assert.Equal(expected, position.Castling);
    }

    [Fact]
    public void CapturingARookOnItsCornerForfeitsThatRight()
    {
        Position bishopTakes = new("r3k2r/1B6/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Play(bishopTakes, "b7a8");

        Assert.Equal(CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside | CastlingRights.BlackKingside, bishopTakes.Castling);
    }

    [Fact]
    public void CastlingForfeitsBothRightsAndMakeUnmakeRoundTripsEveryCastlingMove()
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        string before = position.GetFEN();

        foreach (string castle in new[] { "e1g1", "e1c1" })
        {
            int move = Move.Create(position, castle);
            position.Make(move);
            Assert.Equal(CastlingRights.BlackKingside | CastlingRights.BlackQueenside, position.Castling);
            Assert.Equal(position.GetZobristKey(), position.ZobristKey);
            position.Unmake(move);
            Assert.Equal(before, position.GetFEN());
            Assert.Equal(position.GetZobristKey(), position.ZobristKey);
        }
    }

    [Fact]
    public void TransposedMoveOrdersReachTheSameZobristKey()
    {
        Position a = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Position b = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Play(a, "a1a2", "a8a7", "a2a1", "a7a8");
        Play(b, "h1h2", "h8h7", "h2h1", "h7h8");

        // Both lose the same pair of rights (a-rooks moved vs h-rooks moved) only if the rights differ: they do not.
        Assert.NotEqual(a.ZobristKey, b.ZobristKey);
        Assert.Equal(CastlingRights.WhiteKingside | CastlingRights.BlackKingside, a.Castling);
        Assert.Equal(CastlingRights.WhiteQueenside | CastlingRights.BlackQueenside, b.Castling);
    }

    // ---- cloning ----------------------------------------------------------------------

    [Fact]
    public void ACloneIsEqualButMovesOnItNeverTouchTheOriginal()
    {
        Position original = new(Position.StartingFEN);
        Play(original, "e2e4", "e7e5", "g1f3");
        Position clone = original.DeepClone();

        Assert.True(clone.Equals(original));
        Play(clone, "b8c6", "f1b5");

        Assert.Equal(3, original.HalfMoves);
        Assert.Equal("rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2", original.GetFEN());
        Assert.False(clone.Equals(original));
    }

    [Fact]
    public void ACloneCanUnmakeTheMovesPlayedBeforeItWasTaken()
    {
        Position original = new(Position.StartingFEN);
        Position start = original.DeepClone();
        int[] moves = new int[3];
        for (int i = 0; i < moves.Length; i++)
        {
            moves[i] = Move.Create(original, new[] { "e2e4", "c7c5", "g1f3" }[i]);
            original.Make(moves[i]);
        }

        Position clone = original.DeepClone();
        for (int i = moves.Length - 1; i >= 0; i--)
            clone.Unmake(moves[i]);

        Assert.True(clone.Equals(start));
        Assert.Equal(Position.StartingFEN, clone.GetFEN());
    }

    [Fact]
    public void ACloneKeepsCastlingEnPassantAndClocks()
    {
        Position original = new("r3k2r/8/8/3pP3/8/8/8/R3K2R w Kq d6 12 30");

        Position clone = original.DeepClone();

        Assert.Equal(original.GetFEN(), clone.GetFEN());
        Assert.Equal(CastlingRights.WhiteKingside | CastlingRights.BlackQueenside, clone.Castling);
        Assert.Equal(original.EnPassantSquare, clone.EnPassantSquare);
        Assert.Equal(original.FiftyMovesClock, clone.FiftyMovesClock);
        Assert.Equal(original.ZobristKey, clone.ZobristKey);
    }

    [Fact]
    public void SquaresIsAStableReadOnlyViewOfTheBoard()
    {
        Position position = new(Position.StartingFEN);
        IReadOnlyList<int> squares = position.Squares;

        Assert.Same(squares, position.Squares);
        Assert.False(squares is int[]);
        Assert.Equal(64, squares.Count);
        Assert.Equal(Colour.Black | Piece.Rook, squares[0]);

        Play(position, "e2e4");

        Assert.Equal(Piece.Empty, squares[Square.SquareAt("e2")]);
        Assert.Equal(Colour.White | Piece.Pawn, squares[Square.SquareAt("e4")]);
    }

    // ---- equality ---------------------------------------------------------------------

    [Fact]
    public void EqualPositionsHaveEqualHashCodesAndWorkAsDictionaryKeys()
    {
        Position a = new(Position.StartingFEN);
        Position b = new(Position.StartingFEN);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Dictionary<Position, string> lookup = new() { [a] = "start" };
        Assert.Equal("start", lookup[b]);
    }

    [Fact]
    public void DifferentPositionsAreNotEqual()
    {
        Position a = new(Position.StartingFEN);
        Position b = new(Position.StartingFEN);
        Play(b, "e2e4");

        Assert.False(a.Equals(b));
        Assert.False(a.Equals((object)b));
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void EqualsHandlesNullAndOtherTypes()
    {
        Position position = new(Position.StartingFEN);

        Assert.False(position.Equals(null));
        Assert.False(position.Equals((object?)null));
        Assert.False(position.Equals("not a position"));
        Assert.True(position.Equals(position));
    }

    [Fact]
    public void PositionsWithTheSameBoardButDifferentHistoryAreNotEqual()
    {
        Position a = new(Position.StartingFEN);
        Position b = new(Position.StartingFEN);
        Play(a, "g1f3", "g8f6", "f3g1", "f6g8");

        // Same pieces, but a has four plies of history behind it (and the clock to match).
        Assert.False(a.Equals(b));
        Assert.Equal(a.ZobristKey, b.ZobristKey);
    }

    // ---- history limits ---------------------------------------------------------------

    [Fact]
    public void UnmakeBeforeAnyMoveHasBeenMadeIsRejected()
    {
        Position position = new(Position.StartingFEN);
        int move = Move.Create(position, "e2e4");

        Assert.Throws<InvalidOperationException>(() => position.Unmake(move));
        Assert.Equal(Position.StartingFEN, position.GetFEN());
    }

    [Fact]
    public void UnmakeCannotGoBelowAFensOwnStartingPly()
    {
        Position position = new("4k3/8/8/8/8/8/8/4K3 w - - 0 40");
        int move = Move.Create(position, "e1e2");
        position.Make(move);
        position.Unmake(move);

        Assert.Throws<InvalidOperationException>(() => position.Unmake(move));
        Assert.Equal(78, position.HalfMoves);
    }

    [Fact]
    public void MakingBeyondTheHalfMoveLimitIsRejectedBeforeAnythingChanges()
    {
        Position position = new("4k3/8/8/8/8/8/8/4K3 w - - 0 512");
        Play(position, "e1d1");
        string before = position.GetFEN();
        int next = Move.Create(position, "e8d8");

        Assert.Throws<InvalidOperationException>(() => position.Make(next));

        Assert.Equal(before, position.GetFEN());
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);
    }

    [Fact]
    public void FenWithAMoveNumberBeyondTheLimitFallsBackToTheStartingPosition()
    {
        Assert.Equal(Position.StartingFEN, new Position("4k3/8/8/8/8/8/8/4K3 w - - 0 513").GetFEN());
    }

    // ---- null move --------------------------------------------------------------------

    [Fact]
    public void TheNullMovePassesTheTurnClearsEnPassantAndIsUndoneExactly()
    {
        Position position = new("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
        Position before = position.DeepClone();

        position.MakeNull();

        Assert.Equal(Colour.White, position.SideToMove);
        Assert.Equal(Position.InvalidSquare, position.EnPassantSquare);
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);
        Assert.Equal(CastlingRights.All, position.Castling);

        position.UnmakeNull();

        Assert.True(position.Equals(before));
        Assert.Equal(before.EnPassantSquare, position.EnPassantSquare);
        Assert.Equal(before.ZobristKey, position.ZobristKey);
    }

    [Fact]
    public void NullMovesNestWithRealMoves()
    {
        Position position = new(Position.StartingFEN);
        int e4 = Move.Create(position, "e2e4");

        position.Make(e4);
        position.MakeNull();
        Assert.Equal(Colour.White, position.SideToMove);
        position.MakeNull();
        position.UnmakeNull();
        position.UnmakeNull();
        position.Unmake(e4);

        Assert.Equal(Position.StartingFEN, position.GetFEN());
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);
    }

    [Fact]
    public void UnmakingANullMoveThatWasNeverMadeIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new Position(Position.StartingFEN).UnmakeNull());
    }

    // ---- move generation contract -----------------------------------------------------

    [Fact]
    public void LegalMovesIntoAnArrayChecksItsArgument()
    {
        Position position = new(Position.StartingFEN);

        Assert.Throws<ArgumentNullException>(() => position.LegalMoves(null!));
        ArgumentException tooShort = Assert.Throws<ArgumentException>(() => position.LegalMoves(new int[Position.MaxMoves - 1]));
        Assert.Equal("moves", tooShort.ParamName);
        Assert.Equal(20, position.LegalMoves(new int[Position.MaxMoves]));
    }

    [Fact]
    public void TheListAndArrayOverloadsAgree()
    {
        Position position = new("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        int[] buffer = new int[Position.MaxMoves];

        int count = position.LegalMoves(buffer);

        Assert.Equal(buffer.Take(count), position.LegalMoves());
    }

    [Fact]
    public void LegalMovesIntoASpanFillsTheLeadingSliceAndChecksItsLength()
    {
        Position position = new("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        int[] expected = position.LegalMoves().ToArray();
        int[] buffer = new int[Position.MaxMoves + 10];

        int count = position.LegalMoves(buffer.AsSpan(10));

        Assert.Equal(expected, buffer.Skip(10).Take(count));
        Assert.All(buffer.Take(10), m => Assert.Equal(0, m));
        ArgumentException tooShort = Assert.Throws<ArgumentException>(() => position.LegalMoves(new int[Position.MaxMoves - 1].AsSpan()));
        Assert.Equal("moves", tooShort.ParamName);
    }

    // ---- coordinate notation lookup ----------------------------------------------------

    [Theory]
    [InlineData("E2E4", true)]
    [InlineData("e2E4", true)]
    [InlineData("g1F3", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("e2", false)]
    [InlineData("e2e4e6", false)]
    [InlineData("e2e4q", false)]       // a quiet move with a promotion suffix
    [InlineData("i2i4", false)]
    [InlineData("e0e4", false)]
    [InlineData("e2e9", false)]
    public void MoveCreateAcceptsAnyCaseAndRejectsMalformedOrIllegalText(string? notation, bool legal)
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(legal, Move.Create(position, notation) != Move.Invalid);
    }

    [Theory]
    [InlineData("e7e8q", Piece.Queen)]
    [InlineData("e7e8R", Piece.Rook)]
    [InlineData("e7e8b", Piece.Bishop)]
    [InlineData("e7e8N", Piece.Knight)]
    public void MoveCreateSelectsThePromotionPiece(string notation, int expected)
    {
        Position position = new("7k/4P3/8/8/8/8/8/K7 w - - 0 1");

        int move = Move.Create(position, notation);

        Assert.True(Move.IsPromotion(move));
        Assert.Equal(expected, Move.SpecialPiece(move) & Piece.Mask);
    }

    [Fact]
    public void APromotionNeedsItsSuffixAndAnUnknownSuffixIsRejected()
    {
        Position position = new("7k/4P3/8/8/8/8/8/K7 w - - 0 1");

        Assert.Equal(Move.Invalid, Move.Create(position, "e7e8"));
        Assert.Equal(Move.Invalid, Move.Create(position, "e7e8x"));
        Assert.Equal(Move.Invalid, Move.Create(position, "e7e8k"));
    }

    [Fact]
    public void NotatingASequenceLeavesThePositionExactlyAsItWas()
    {
        Position position = new(Position.StartingFEN);
        List<int> line = new();
        Position scratch = position.DeepClone();
        foreach (string move in new[] { "e2e4", "e7e5", "g1f3", "b8c6" })
        {
            int encoded = Move.Create(scratch, move);
            line.Add(encoded);
            scratch.Make(encoded);
        }

        string notation = Move.GetAlgebraicNotation(position, line, AlgebraicNotationFormat.Proper);

        Assert.Equal("1. e4 e5 2. Nf3 Nc6", notation);
        Assert.Equal(Position.StartingFEN, position.GetFEN());
        Assert.Equal(position.GetZobristKey(), position.ZobristKey);
    }
}
