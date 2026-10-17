using System;
using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Bitboards;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>Chess rules that perft totals alone would not pin down, each on a small hand-built position.</summary>
public class PositionRulesUnitTest
{
    private static string[] MovesOf(string fen) =>
        new Position(fen).LegalMoves().Select(Move.GetCoordinateNotation).OrderBy(m => m, StringComparer.Ordinal).ToArray();

    private static string[] MovesFrom(string fen, string square) =>
        MovesOf(fen).Where(m => m.StartsWith(square, StringComparison.Ordinal)).ToArray();

    private static void Play(Position position, params string[] moves)
    {
        foreach (string move in moves)
        {
            int encoded = Move.Create(position, move);
            Assert.NotEqual(Move.Invalid, encoded);
            position.Make(encoded);
        }
    }

    // ---- pins -------------------------------------------------------------------------

    [Fact]
    public void APinnedKnightCannotMove()
    {
        Assert.Empty(MovesFrom("4k3/8/8/8/1b6/8/3N4/4K3 w - - 0 1", "d2"));
    }

    [Fact]
    public void ABishopPinnedAlongARankCannotMove()
    {
        Assert.Empty(MovesFrom("4k3/8/8/8/8/8/8/r2BK3 w - - 0 1", "d1"));
    }

    [Fact]
    public void APinnedRookMayMoveAlongThePinAndCaptureThePinner()
    {
        Assert.Equal(new[] { "d1a1", "d1b1", "d1c1" }, MovesFrom("4k3/8/8/8/8/8/8/r2RK3 w - - 0 1", "d1"));
    }

    [Fact]
    public void APinnedPawnMayOnlyCaptureThePinningPiece()
    {
        Assert.Equal(new[] { "c3b4" }, MovesFrom("4k3/8/8/8/1b6/2P5/8/4K3 w - - 0 1", "c3"));
    }

    [Fact]
    public void EnPassantThatWouldExposeTheKingAlongTheRankIsIllegal()
    {
        // Taking en passant removes both pawns from the fifth rank, which would expose the
        // white king on a5 to the black rook on h5.
        string[] moves = MovesOf("8/8/8/KPp4r/8/8/8/7k w - c6 0 1");

        Assert.DoesNotContain("b5c6", moves);
        Assert.Contains("b5b6", moves);
    }

    // ---- check ------------------------------------------------------------------------

    [Fact]
    public void SingleCheckAllowsOnlyKingMovesBlocksAndCaptures()
    {
        Assert.Equal(
            new[] { "e1d1", "e1d2", "e1f1", "e1f2", "g1e2" },
            MovesOf("4r1k1/8/8/8/8/8/8/R3K1N1 w - - 0 1"));
    }

    [Fact]
    public void DoubleCheckAllowsOnlyKingMoves()
    {
        Assert.Equal(
            new[] { "e1d1", "e1f1", "e1f2" },
            MovesOf("4r1k1/8/8/8/1b6/8/8/R3K1N1 w - - 0 1"));
    }

    [Fact]
    public void ACheckingPawnCanBeCapturedEnPassant()
    {
        // Black has just played d7-d5; the pawn on d5 attacks the white king on e4, and the only
        // way to deal with that by capturing is en passant.
        string[] moves = MovesOf("4k3/8/8/3pP3/4K3/8/8/8 w - d6 0 1");

        Assert.Contains("e5d6", moves);
    }

    [Fact]
    public void FoolsMateIsCheckmate()
    {
        Position position = new("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");

        Assert.True(position.InCheck(Colour.White));
        Assert.Empty(position.LegalMoves());
    }

    [Fact]
    public void StalemateHasNoMovesAndNoCheck()
    {
        Position position = new("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");

        Assert.False(position.InCheck(Colour.Black));
        Assert.Empty(position.LegalMoves());
    }

    // ---- castling ---------------------------------------------------------------------

    [Fact]
    public void CastlingIsOfferedWhenNothingStandsInTheWay()
    {
        string[] moves = MovesOf("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Assert.Contains("e1g1", moves);
        Assert.Contains("e1c1", moves);
    }

    [Fact]
    public void CannotCastleThroughAnAttackedSquare()
    {
        string[] moves = MovesOf("r3k2r/8/8/8/8/5r2/8/R3K2R w KQkq - 0 1");

        Assert.DoesNotContain("e1g1", moves);
        Assert.Contains("e1c1", moves);
    }

    [Fact]
    public void CannotCastleOutOfCheck()
    {
        string[] moves = MovesOf("4r1k1/8/8/8/8/8/8/R3K2R w KQ - 0 1");

        Assert.DoesNotContain("e1g1", moves);
        Assert.DoesNotContain("e1c1", moves);
    }

    [Fact]
    public void QueensideCastlingIsLegalEvenIfOnlyTheRooksPathSquareIsAttacked()
    {
        Assert.Contains("e1c1", MovesOf("1r2k3/8/8/8/8/8/8/R3K3 w Q - 0 1"));
    }

    [Fact]
    public void CannotCastleThroughPieces()
    {
        string[] moves = MovesOf("r3k2r/8/8/8/8/8/8/RN2K1NR w KQkq - 0 1");

        Assert.DoesNotContain("e1g1", moves);
        Assert.DoesNotContain("e1c1", moves);
    }

    [Fact]
    public void CapturingTheRookOnItsCornerRemovesThatCastlingRight()
    {
        Position position = new("r3k2r/8/8/8/8/8/6b1/R3K2R b KQkq - 0 1");

        Play(position, "g2h1");

        Assert.Equal("r3k2r/8/8/8/8/8/8/R3K2b w Qkq - 0 2", position.GetFEN());
        Assert.DoesNotContain("e1g1", position.LegalMoves().Select(Move.GetCoordinateNotation));
    }

    [Fact]
    public void MovingTheKingGivesUpBothCastlingRightsAndUnmakeRestoresThem()
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        int move = Move.Create(position, "e1e2");

        position.Make(move);
        Assert.Equal("r3k2r/8/8/8/8/8/4K3/R6R b kq - 1 1", position.GetFEN());

        position.Unmake(move);
        Assert.Equal("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", position.GetFEN());
    }

    [Fact]
    public void MovingARookGivesUpOnlyThatSidesRight()
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Play(position, "h1h2");

        Assert.StartsWith("r3k2r/8/8/8/8/8/7R/R3K3 b Qkq", position.GetFEN());
    }

    [Fact]
    public void CastlingMovesTheRookToo()
    {
        Position position = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Play(position, "e1g1");

        Assert.StartsWith("r3k2r/8/8/8/8/8/8/R4RK1 b kq", position.GetFEN());
    }

    // ---- repetition and the fifty-move clock ----------------------------------------------

    [Fact]
    public void ARepeatedPositionIsDetectedAfterAKnightShuffle()
    {
        Position position = new(Position.StartingFEN);
        Assert.False(position.HasRepeated(2));

        Play(position, "g1f3", "g8f6", "f3g1", "f6g8");

        Assert.True(position.HasRepeated(2));
        Assert.False(position.HasRepeated(3));

        Play(position, "g1f3", "g8f6", "f3g1", "f6g8");

        Assert.True(position.HasRepeated(3));
    }

    [Fact]
    public void PositionsBeforeAnIrreversibleMoveDoNotCountAsRepetitions()
    {
        Position position = new(Position.StartingFEN);
        Play(position, "g1f3", "g8f6", "f3g1", "f6g8", "e2e4");
        Assert.False(position.HasRepeated(2));

        Play(position, "g8f6", "g1f3", "f6g8", "f3g1");

        Assert.False(position.HasRepeated(3));
    }

    [Fact]
    public void FiftyMoveClockCountsQuietMovesAndResetsOnPawnMovesAndCaptures()
    {
        Position position = new(Position.StartingFEN);

        Play(position, "g1f3", "g8f6", "f3g1");
        Assert.Equal(3, position.FiftyMovesClock);

        Play(position, "e7e5");
        Assert.Equal(0, position.FiftyMovesClock);

        Play(position, "g1f3", "b8c6", "f3e5");
        Assert.Equal(0, position.FiftyMovesClock);
    }

    [Fact]
    public void UnmakeRestoresTheFiftyMoveClockAcrossAReset()
    {
        Position position = new("4k3/8/8/8/8/8/4P3/4K3 w - - 17 40");
        int push = Move.Create(position, "e2e4");

        position.Make(push);
        Assert.Equal(0, position.FiftyMovesClock);
        position.Unmake(push);

        Assert.Equal(17, position.FiftyMovesClock);
    }

    // ---- insufficient material --------------------------------------------------------------

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1", true)]                    // K v K
    [InlineData("4k3/8/8/8/8/8/8/3BK3 w - - 0 1", true)]                   // K+B v K
    [InlineData("4k3/8/8/8/8/8/8/3NK3 w - - 0 1", true)]                   // K+N v K
    [InlineData("4k3/8/8/8/8/8/8/2NNK3 w - - 0 1", true)]                  // K+NN v K (treated as a draw)
    [InlineData("4kb2/8/8/8/8/8/8/2B1K3 w - - 0 1", true)]                 // bishops on the same colour
    [InlineData("4k1b1/8/8/8/8/8/8/2B1K3 w - - 0 1", false)]               // bishops on opposite colours
    [InlineData("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1", false)]                 // a pawn
    [InlineData("4k3/8/8/8/8/8/8/3RK3 w - - 0 1", false)]                  // a rook
    [InlineData("4k3/8/8/8/8/8/8/3QK3 w - - 0 1", false)]                  // a queen
    [InlineData("4k3/8/8/8/8/8/8/2BNK3 w - - 0 1", false)]                 // bishop and knight
    [InlineData("4k3/8/8/8/8/8/8/1NBNK3 w - - 0 1", false)]                // more than a minor piece each
    public void InsufficientMaterialTable(string fen, bool expected)
    {
        Assert.Equal(expected, new Position(fen).InsufficientMaterial());
    }

    // ---- move lookup ------------------------------------------------------------------------

    [Theory]
    [InlineData("e2e4", true)]
    [InlineData("g1f3", true)]
    [InlineData("e2e5", false)]
    [InlineData("zz", false)]
    [InlineData("", false)]
    public void MoveCreateFindsOnlyLegalMovesByCoordinateNotation(string notation, bool legal)
    {
        Position position = new(Position.StartingFEN);

        int move = Move.Create(position, notation);

        Assert.Equal(legal, move != Move.Invalid);
    }

    [Fact]
    public void PromotionsAreSelectedByTheirSuffix()
    {
        Position position = new("7k/4P3/8/8/8/8/8/K7 w - - 0 1");

        int queen = Move.Create(position, "e7e8q");
        int knight = Move.Create(position, "e7e8n");

        Assert.True(Move.IsPromotion(queen));
        Assert.True(Move.IsQueenPromotion(queen));
        Assert.False(Move.IsQueenPromotion(knight));
        Assert.Equal(Move.Invalid, Move.Create(position, "e7e8"));
    }

    [Fact]
    public void MoveFlagsDescribeCapturesCastlingAndEnPassant()
    {
        Position castling = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Assert.True(Move.IsCastle(Move.Create(castling, "e1g1")));
        Assert.False(Move.IsCapture(Move.Create(castling, "e1g1")));

        Position enPassant = new("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
        int capture = Move.Create(enPassant, "e5d6");
        Assert.True(Move.IsEnPassant(capture));

        Position takes = new("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1");
        Assert.True(Move.IsCapture(Move.Create(takes, "e4d5")));
    }
}

public class SquareAndPieceNamingUnitTest
{
    [Theory]
    [InlineData(Piece.Pawn)]
    [InlineData(Piece.Knight)]
    [InlineData(Piece.Bishop)]
    [InlineData(Piece.Rook)]
    [InlineData(Piece.Queen)]
    [InlineData(Piece.King)]
    public void AnInitialDecodesBackToItsPieceInEitherCase(int piece)
    {
        char initial = Piece.GetInitial(piece)[0];

        Assert.Equal(piece, Piece.FromInitial(initial));
        Assert.Equal(piece, Piece.FromInitial(char.ToLowerInvariant(initial)));
        Assert.Equal(piece, Piece.FromInitial(Piece.GetInitial(Colour.Black | piece)[0]));
    }

    [Theory]
    [InlineData('x')]
    [InlineData('1')]
    [InlineData('/')]
    [InlineData(' ')]
    public void ACharacterThatIsNoPieceDecodesToEmpty(char letter)
    {
        Assert.Equal(Piece.Empty, Piece.FromInitial(letter));
    }

    [Fact]
    public void EverySquareNameRoundTripsAndMatchesItsFileAndRank()
    {
        for (int square = 0; square < 64; square++)
        {
            string name = Square.GetName(square);

            Assert.Equal(2, name.Length);
            Assert.Equal(square, Square.SquareAt(name));
            Assert.Equal(square % 8, Square.File(square));
            Assert.Equal(square / 8, Square.Rank(square));
            Assert.Equal(name[0].ToString(), Square.GetFileName(square));
            Assert.Equal(name[1].ToString(), Square.GetRankName(square));
        }
    }

    [Fact]
    public void CornerSquaresHaveTheExpectedNames()
    {
        Assert.Equal("a8", Square.GetName(0));
        Assert.Equal("h8", Square.GetName(7));
        Assert.Equal("a1", Square.GetName(56));
        Assert.Equal("h1", Square.GetName(63));
    }

    [Theory]
    [InlineData(Piece.King, "King", "K")]
    [InlineData(Piece.Queen, "Queen", "Q")]
    [InlineData(Piece.Rook, "Rook", "R")]
    [InlineData(Piece.Bishop, "Bishop", "B")]
    [InlineData(Piece.Knight, "Knight", "N")]
    [InlineData(Piece.Pawn, "Pawn", "P")]
    public void PieceNamesAndInitialsIgnoreTheColourBit(int piece, string name, string initial)
    {
        Assert.Equal(name, Piece.GetName(piece));
        Assert.Equal(name, Piece.GetName(piece | Colour.Black));
        Assert.Equal(initial, Piece.GetInitial(piece | Colour.Black));
    }

    [Fact]
    public void NamingAnEmptySquareThrowsAnArgumentOutOfRangeExceptionNamingTheParameter()
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => Piece.GetName(Piece.Empty));

        Assert.Equal("piece", error.ParamName);
    }

    [Fact]
    public void ColourNamesAndInitials()
    {
        Assert.Equal("White", Colour.GetName(Colour.White));
        Assert.Equal("Black", Colour.GetName(Colour.Black));
        Assert.Equal("W", Colour.GetInitial(Colour.White).ToUpperInvariant());
        Assert.Equal("B", Colour.GetInitial(Colour.Black).ToUpperInvariant());
    }
}

public class BitboardExhaustiveUnitTest
{
    [Fact]
    public void ScanReadAndScanReverseAgreeForEverySingleBit()
    {
        for (int square = 0; square < 64; square++)
        {
            ulong bit = 1UL << square;

            Assert.Equal(square, Bitboard.Scan(bit));
            Assert.Equal(square, Bitboard.ScanReverse(bit));
            Assert.Equal(square, Bitboard.Read(bit));
            Assert.Equal(1, Bitboard.Count(bit));
        }
    }

    [Fact]
    public void ScanAndScanReverseFindTheExtremesOfAMultiBitValue()
    {
        ulong value = (1UL << 5) | (1UL << 20) | (1UL << 41);

        Assert.Equal(5, Bitboard.Scan(value));
        Assert.Equal(41, Bitboard.ScanReverse(value));
        Assert.Equal(1UL << 5, Bitboard.Isolate(value));
        Assert.Equal(1UL << 41, Bitboard.IsolateReverse(value));
    }

    [Fact]
    public void CountAndCountSparseAgreeOnPseudoRandomValues()
    {
        Random random = new(12345);
        byte[] bytes = new byte[8];
        for (int i = 0; i < 2000; i++)
        {
            random.NextBytes(bytes);
            ulong value = BitConverter.ToUInt64(bytes, 0);

            Assert.Equal(Bitboard.CountSparse(value), Bitboard.Count(value));
        }
    }

    [Fact]
    public void PoppingEveryBitVisitsEachSetSquareOnceInAscendingOrder()
    {
        ulong value = 0x8100_0000_0000_0081UL;
        List<int> squares = new();

        while (value != 0)
        {
            squares.Add(Bitboard.Pop(ref value));
        }

        Assert.Equal(new[] { 0, 7, 56, 63 }, squares);
    }

    [Fact]
    public void RaysNeverIncludeTheirOwnSquareAndStayOnTheirLine()
    {
        for (int square = 0; square < 64; square++)
        {
            ulong self = 1UL << square;
            foreach (ulong[] ray in new[] { Bitboard.RayN, Bitboard.RayE, Bitboard.RayS, Bitboard.RayW, Bitboard.RayNE, Bitboard.RayNW, Bitboard.RaySE, Bitboard.RaySW })
            {
                Assert.Equal(0UL, ray[square] & self);
            }

        }

        Assert.Equal(0UL, Bitboard.RayN[0]);
        Assert.Equal(7, Bitboard.Count(Bitboard.RayS[0]));
        Assert.Equal(7, Bitboard.Count(Bitboard.RayE[0]));
        Assert.Equal(7, Bitboard.Count(Bitboard.RaySE[0]));
    }
}
