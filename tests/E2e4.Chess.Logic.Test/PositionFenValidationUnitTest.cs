using System.Linq;
using E2e4.Chess.Logic.Bitboards;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class PositionFenValidationUnitTest
{
    private const string BareKings = "4k3/8/8/8/8/8/8/4K3";

    public static TheoryData<string> InvalidFens() => new()
    {
        "",
        "   ",
        "8/8/8/8/8/8/8/8",                                                       // no side to move
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1",              // bad side to move
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR W KQkq - 0 1",              // side to move is case-sensitive
        "rnbqkbnrr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",             // nine files
        "rnbqkbn/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",               // seven files
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR/8 w KQkq - 0 1",            // nine ranks
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1",                       // seven ranks
        "rnbqkbnr/pppppppp/9/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",              // digit 9
        "rnbqkbnr/pppppppp/0/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",              // digit 0
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQXBNR w KQkq - 0 1",              // unknown piece
        "rnbq1bnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",              // no black king
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQ1BNR w KQkq - 0 1",              // no white king
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBKR w KQkq - 0 1",              // two white kings
        "Pnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",              // pawn on rank 8
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNp w KQkq - 0 1",              // pawn on rank 1
        BareKings + " w K - 0 1",                                                // castling right, no rook
        BareKings + " w Z - 0 1",                                                // bad castling field
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq e9 0 1",             // en passant off the board
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq z3 0 1",             // en passant bad file
        "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e4 0 1",           // en passant on wrong rank
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq e3 0 1",             // en passant without the pawn
        BareKings + " w - - abc 1",                                              // non-numeric clock
        BareKings + " w - - -1 1",                                               // negative clock
        BareKings + " w - - 0 0",                                                // fullmove below 1
        BareKings + " w - - 0 x",                                                // non-numeric fullmove
        BareKings + " w - - 0 600",                                              // beyond HalfMovesLimit
        "4k3/8/8/8/8/P7/PPPPPPPP/4K3 w - - 0 1",                                 // nine white pawns
        "4k3/pppppppp/p7/8/8/8/8/4K3 w - - 0 1",                                 // nine black pawns
        "4k3/pppppppp/8/8/8/8/PPPPPPPP/QQ2K3 w - - 0 1",                         // eight pawns and two queens
        "4k3/8/8/8/8/8/PPPPPPP1/QQQ1K3 w - - 0 1",                               // seven pawns and three queens
        "rrr1k3/pppppppp/8/8/8/8/8/4K3 w - - 0 1",                               // eight pawns and three rooks
        "4k3/8/8/8/8/8/8/4RK2 w - - 0 1",                                        // black king in check, white to move
        "8/8/8/8/8/8/3k4/3K4 w - - 0 1",                                         // adjacent kings
        BareKings + " w - - 10001 1",                                            // clock beyond MaxFiftyMovesClock
        BareKings + " w - - 0 1 " + new string('x', 300),                        // longer than MaxFenLength
    };

    [Theory]
    [InlineData("4k3/8/8/8/8/8/PPPPPPPP/4K3 w - - 0 1")]                         // eight pawns
    [InlineData("4k3/8/8/8/8/8/PPPPPPP1/QQ2K3 w - - 0 1")]                       // seven pawns, one promotion
    [InlineData("4k3/8/8/8/8/8/QQQQQQQQ/QK6 b - - 0 1")]                         // no pawns, eight promotions
    [InlineData("rrr1k3/8/8/8/8/8/8/4K3 w - - 0 1")]                             // no pawns, a third rook
    public void TryParseAcceptsMaterialThatPromotionsCanExplain(string fen)
    {
        Assert.True(Position.TryParse(fen, out Position? position));
        Assert.NotNull(position);
    }

    [Theory]
    [MemberData(nameof(InvalidFens))]
    public void TryParseRejectsInvalidFen(string fen)
    {
        Assert.False(Position.TryParse(fen, out Position? position));
        Assert.Null(position);
    }

    [Fact]
    public void TryParseRejectsNull()
    {
        Assert.False(Position.TryParse(null!, out _));
    }

    [Theory]
    [MemberData(nameof(InvalidFens))]
    public void ConstructorFallsBackToACleanStartingPositionForInvalidFen(string fen)
    {
        Position position = new(fen);

        AssertIsPristineStartingPosition(position);
    }

    [Fact]
    public void FallbackDoesNotLeakEnPassantSquareFromPartiallyParsedFen()
    {
        // The en passant field parses before the clock fails; it must not survive the fallback.
        Position position = new("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 abc 1");

        AssertIsPristineStartingPosition(position);
    }

    [Fact]
    public void FallbackDoesNotOverlayStartingPositionOnStaleBitboards()
    {
        Position position = new("k7/8/8/8/8/8/8/K7 w - e9 0 1");

        AssertIsPristineStartingPosition(position);
    }

    [Fact]
    public void TryParseAcceptsStartingFen()
    {
        Assert.True(Position.TryParse(Position.StartingFEN, out Position? position));
        Assert.Equal(Position.StartingFEN, position!.GetFEN());
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 30 1")]                   // clock larger than the ply count
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R b Kq - 12 40")]
    [InlineData("rnbqkbnr/pppp1ppp/8/4pP2/8/8/PPPPP1PP/RNBQKBNR w KQkq e6 0 3")]
    [InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1")]
    public void ValidFenRoundTrips(string fen)
    {
        Assert.True(Position.TryParse(fen, out Position? position));
        Assert.Equal(fen, position!.GetFEN());
    }

    [Fact]
    public void TryParseAcceptsTwoFieldFen()
    {
        Assert.True(Position.TryParse("4k3/8/8/8/8/8/8/4K3 b", out Position? position));
        Assert.Equal(Colour.Black, position!.SideToMove);
    }

    [Fact]
    public void HasRepeatedDoesNotThrowWhenFiftyMoveClockExceedsPlyCount()
    {
        Position position = new("4k3/8/8/8/8/8/8/4K3 w - - 30 1");

        Assert.False(position.HasRepeated(2));
    }

    private static void AssertIsPristineStartingPosition(Position position)
    {
        Position expected = new(Position.StartingFEN);

        Assert.Equal(Position.StartingFEN, position.GetFEN());
        Assert.Equal(Position.InvalidSquare, position.EnPassantSquare);
        Assert.Equal(expected.OccupiedBitboard, position.OccupiedBitboard);
        Assert.Equal(32, Bitboard.Count(position.OccupiedBitboard));
        for (int piece = 0; piece < expected.PieceBitboards.Length; piece++)
            Assert.Equal(expected.PieceBitboards[piece], position.PieceBitboards[piece]);
        Assert.Equal(expected.ZobristKey, position.ZobristKey);
        Assert.True(position.Equals(expected));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/R3K3")]                    // bare placement: White to move, nothing else
    [InlineData("4k3/8/8/8/8/8/8/R3K3 b")]
    public void ShortFensTakeTheirDefaults(string fen)
    {
        Assert.True(Position.TryParse(fen, out Position? position));
        Assert.Equal(PositionSetup.TryParse(fen, out PositionSetup? setup) ? setup.ToFen() : null, position.GetFEN());
    }

    [Theory]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KK - 0 1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkqq - 0 1")]
    public void RepeatedCastlingLettersAreRejectedByBothParsers(string fen)
    {
        Assert.False(Position.TryParse(fen, out _));
        Assert.False(PositionSetup.TryParse(fen, out _));
    }

    [Theory]
    [InlineData(Position.StartingFEN)]
    [InlineData("r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 0 20")]
    [InlineData("4k3/8/8/8/8/3n1n2/8/4K3 w - - 0 10")]       // unreachable but playable
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1")]            // dead position
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 5 1")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1")]                // no kings
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w KQ - 0 1")]          // castling without rooks
    [InlineData("4k3/4P3/8/8/8/8/8/4K3 b - - 0 1")]          // opponent in check
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - e3 0 1")]          // en passant without a pawn
    public void PositionAcceptsExactlyWhatTheSetupHasNoErrorsFor(string fen)
    {
        Assert.True(PositionSetup.TryParse(fen, out PositionSetup? setup));
        bool playable = setup.Validate().All(p => p.Severity != PositionProblemSeverity.Error);

        Assert.Equal(playable, Position.TryParse(fen, out _));
        Assert.Equal(playable, new Position(fen).GetFEN() == setup.ToFen());
    }
}
