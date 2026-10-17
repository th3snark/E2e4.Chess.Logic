using System;
using System.Linq;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class PositionSetupUnitTest
{
    private static PositionSetup Parse(string fen)
    {
        Assert.True(PositionSetup.TryParse(fen, out PositionSetup? setup), fen);
        return setup!;
    }

    private static PositionProblemKind[] Kinds(string fen)
    {
        return Parse(fen).Validate().Select(p => p.Kind).ToArray();
    }

    [Fact]
    public void StandardRoundTripsTheStartingFen()
    {
        Assert.Equal(Position.StartingFEN, PositionSetup.Standard().ToFen());
    }

    [Fact]
    public void AnEmptyBoardParsesAndReportsBothMissingKings()
    {
        const string fen = "8/8/8/8/8/8/8/8 w - - 0 1";
        PositionSetup setup = Parse(fen);

        Assert.Equal(new[] { PositionProblemKind.MissingKing, PositionProblemKind.MissingKing }, Kinds(fen));
        Assert.Equal(fen, setup.ToFen());
        Assert.Null(setup.ToPosition());
    }

    [Fact]
    public void ABarePlacementIsAccepted()
    {
        PositionSetup setup = Parse("4k3/8/8/8/8/8/8/4K3");

        Assert.Equal(Colour.White, setup.SideToMove);
        Assert.Empty(setup.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/9 w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/7x w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/8 x - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/8 w Z - 0 1")]
    [InlineData("8/8/8/8/8/8/8/8 w - e9 0 1")]
    [InlineData("8/8/8/8/8/8/8/8 w - - x 1")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 0")]
    public void StructuralGarbageFailsTheParse(string? fen)
    {
        Assert.False(PositionSetup.TryParse(fen, out PositionSetup? setup));
        Assert.Null(setup);
    }

    [Fact]
    public void ALegalPositionRoundTripsAndConvertsToAPosition()
    {
        const string fen = "r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 3 20";
        PositionSetup setup = Parse(fen);

        Assert.Empty(setup.Validate());
        Assert.Equal(fen, setup.ToFen());
        Assert.Equal(fen, setup.ToPosition()!.GetFEN());
    }

    [Fact]
    public void FromPositionCopiesEverything()
    {
        Position position = new Position("r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 3 20");

        Assert.Equal(position.GetFEN(), PositionSetup.FromPosition(position).ToFen());
    }

    [Fact]
    public void EditingASquareChangesTheFen()
    {
        PositionSetup setup = Parse("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        setup[Square.SquareAt("d4")] = Colour.White | Piece.Queen;
        setup[Square.SquareAt("d5")] = Colour.Black | Piece.Knight;

        Assert.Equal("4k3/8/8/3n4/3Q4/8/8/4K3 w - - 0 1", setup.ToFen());
        Assert.Equal(Colour.White | Piece.Queen, setup[Square.SquareAt("d4")]);
    }

    [Fact]
    public void ASquareRejectsNonsense()
    {
        PositionSetup setup = new PositionSetup();

        Assert.Throws<ArgumentOutOfRangeException>(() => setup[64]);
        Assert.Throws<ArgumentOutOfRangeException>(() => setup[-1] = Piece.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => setup[0] = 99);
    }

    [Fact]
    public void ClearResetsEverything()
    {
        PositionSetup setup = PositionSetup.Standard();
        setup.Clear();

        Assert.Equal("8/8/8/8/8/8/8/8 w - - 0 1", setup.ToFen());
    }

    [Fact]
    public void CloneIsIndependent()
    {
        PositionSetup setup = PositionSetup.Standard();
        PositionSetup copy = setup.Clone();
        copy[0] = Piece.Empty;

        Assert.Equal(Position.StartingFEN, setup.ToFen());
        Assert.NotEqual(setup.ToFen(), copy.ToFen());
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/8 w - - 0 1", PositionProblemKind.MissingKing)]
    [InlineData("4kk2/8/8/8/8/8/8/4K3 w - - 0 1", PositionProblemKind.ExtraKing)]
    [InlineData("P3k3/8/8/8/8/8/8/4K3 w - - 0 1", PositionProblemKind.PawnOnLastRank)]
    [InlineData("4k3/8/8/8/8/8/8/p3K3 w - - 0 1", PositionProblemKind.PawnOnLastRank)]
    [InlineData("4k3/pppppppp/p7/8/8/8/8/4K3 w - - 0 1", PositionProblemKind.TooManyPawns)]
    [InlineData("4k3/QQQQQQQQ/QQ6/8/8/8/8/4K3 w - - 0 1", PositionProblemKind.ImpossibleMaterial)]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w KQ - 0 1", PositionProblemKind.InvalidCastlingRights)]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - e3 0 1", PositionProblemKind.InvalidEnPassant)]
    [InlineData("8/8/8/8/8/8/8/3kK3 w - - 0 1", PositionProblemKind.KingsAdjacent)]
    public void EachRuleIsReported(string fen, PositionProblemKind expected)
    {
        Assert.Contains(expected, Kinds(fen));
        Assert.Null(Parse(fen).ToPosition());
    }

    [Fact]
    public void ABareLegalPositionHasNoProblems()
    {
        Assert.Empty(Kinds("4k3/8/8/8/8/8/8/4K3 w - - 0 1"));
        Assert.Empty(Kinds("4k3/P7/8/8/8/8/8/4K3 w - - 0 1"));
    }

    [Theory]
    [InlineData(0, 0)]        // move number below 1
    [InlineData(600, 0)]      // beyond HalfMovesLimit
    [InlineData(1, -1)]       // negative clock
    [InlineData(1, 10001)]    // clock beyond MaxFiftyMovesClock
    public void OutOfRangeCountersAreReportedAndNeverBecomeAPosition(int moveNumber, int clock)
    {
        PositionSetup setup = Parse("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        setup.MoveNumber = moveNumber;
        setup.FiftyMovesClock = clock;

        Assert.Contains(PositionProblemKind.InvalidCounters, setup.Validate().Select(p => p.Kind));
        Assert.Null(setup.ToPosition());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("i1")]
    [InlineData("a9")]
    [InlineData("e22")]
    public void SquareAtRejectsNamesThatAreNotSquares(string name)
    {
        Assert.Throws<ArgumentException>(() => Square.SquareAt(name));
    }

    [Fact]
    public void MoveCreateRejectsSquaresOffTheBoard()
    {
        Position position = new Position(Position.StartingFEN);
        Assert.Throws<ArgumentOutOfRangeException>(() => Move.Create(position, 64, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Move.Create(position, 0, -1));
    }

    [Fact]
    public void TheSideNotToMoveMayNotBeInCheck()
    {
        // The rook on e1 attacks the black king on e8, so it must be Black's move.
        Assert.Contains(PositionProblemKind.SideNotToMoveInCheck, Kinds("4k3/8/8/8/8/8/8/4RK2 w - - 0 1"));
        Assert.Empty(Kinds("4k3/8/8/8/8/8/8/4RK2 b - - 0 1"));
    }

    [Fact]
    public void ASideToMoveInCheckIsFine()
    {
        Assert.Empty(Kinds("4k3/8/8/8/8/8/4r3/4K3 w - - 0 1"));
    }

    [Fact]
    public void CheckmateAndStalemateAreWarningsNotErrors()
    {
        PositionSetup mate = Parse("R5k1/5ppp/8/8/8/8/8/4K3 b - - 0 1");
        PositionProblem[] mateProblems = mate.Validate().ToArray();
        Assert.Single(mateProblems);
        Assert.Equal(PositionProblemSeverity.Warning, mateProblems[0].Severity);
        Assert.Contains("checkmated", mateProblems[0].Message);
        Assert.NotNull(mate.ToPosition());

        PositionSetup stale = Parse("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        PositionProblem[] staleProblems = stale.Validate().ToArray();
        Assert.Single(staleProblems);
        Assert.Contains("stalemated", staleProblems[0].Message);
    }

    [Fact]
    public void AnImpossibleBoardIsReportedWithoutThrowing()
    {
        PositionSetup setup = Parse("PPPPPPPP/PPPPPPPP/8/8/8/8/8/8 w KQkq e3 0 1");

        Assert.NotEmpty(setup.Validate());
        Assert.Null(setup.ToPosition());
    }

    [Fact]
    public void DroppingInvalidCastlingRightsKeepsTheSupportedOnes()
    {
        PositionSetup setup = Parse("r3k3/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        Assert.Contains(PositionProblemKind.InvalidCastlingRights, setup.Validate().Select(p => p.Kind));
        setup.DropInvalidCastlingRights();

        Assert.Equal(CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside | CastlingRights.BlackQueenside, setup.Castling);
        Assert.Empty(setup.Validate());
    }

    [Fact]
    public void PossibleCastlingFollowsTheBoard()
    {
        Assert.Equal(CastlingRights.All, PositionSetup.Standard().PossibleCastling);
        Assert.Equal(CastlingRights.None, new PositionSetup().PossibleCastling);
    }

    [Fact]
    public void EnPassantFilesFollowThePawnsAndTheSideToMove()
    {
        PositionSetup setup = Parse("4k3/8/8/3pP3/8/8/8/4K3 w - - 0 1");
        Assert.Equal(new[] { 3 }, setup.PossibleEnPassantFiles.ToArray());

        setup.EnPassantSquare = Square.SquareAt("d6");
        Assert.Empty(setup.Validate());

        setup.SideToMove = Colour.Black;
        Assert.Empty(setup.PossibleEnPassantFiles);
        Assert.Contains(PositionProblemKind.InvalidEnPassant, setup.Validate().Select(p => p.Kind));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(7)]
    public void TheSideToMoveMustBeAColour(int value)
    {
        PositionSetup setup = new();

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value), () => setup.SideToMove = value);
        Assert.Equal(Colour.White, setup.SideToMove);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(64)]
    [InlineData(99)]
    public void TheEnPassantSquareMustBeASquareOrNone(int value)
    {
        PositionSetup setup = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => setup.EnPassantSquare = value);
        Assert.Equal(Position.InvalidSquare, setup.EnPassantSquare);

        setup.EnPassantSquare = 0;
        setup.EnPassantSquare = 63;
        setup.EnPassantSquare = Position.InvalidSquare;
        Assert.Equal(Position.InvalidSquare, setup.EnPassantSquare);
    }

    [Fact]
    public void DroppingAnInvalidEnPassantSquareKeepsAValidOne()
    {
        PositionSetup setup = Parse("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
        setup.DropInvalidEnPassant();
        Assert.Equal(Square.SquareAt("d6"), setup.EnPassantSquare);

        setup[Square.SquareAt("d5")] = Piece.Empty;
        setup.DropInvalidEnPassant();
        Assert.Equal(Position.InvalidSquare, setup.EnPassantSquare);
    }

    [Fact]
    public void ProblemsDescribeThemselves()
    {
        PositionProblem problem = Parse("8/8/8/8/8/8/8/8 w - - 0 1").Validate()[0];

        Assert.Equal(problem.Message, problem.ToString());
        Assert.Equal(PositionProblemSeverity.Error, problem.Severity);
        Assert.False(string.IsNullOrWhiteSpace(problem.Message));
    }
}
