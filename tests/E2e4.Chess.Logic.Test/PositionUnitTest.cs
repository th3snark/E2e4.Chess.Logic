using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class PositionUnitTest
{
    private const string FenStart = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string FenAfterE4 = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";
    private const string FenAfterE4C5 = "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2";
    private const string FenAfterNf3 = "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2";
    private const string FenRuyLopez = "r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 0 1";
    private const string FenTruncatedMidgame = "r1k4r/p2nb1p1/2b4p/1p1n1p2/2PP4/3Q1NB1/1P3PPP/R5K1 w";

    [Theory]
    [InlineData(Position.StartingFEN)]
    [InlineData(FenAfterE4)]
    [InlineData(FenAfterE4C5)]
    [InlineData(FenRuyLopez)]
    public void GetFenRoundTripsForFullySpecifiedFenStrings(string fen)
    {
        Position position = new(fen);

        Assert.Equal(fen, position.GetFEN());
    }

    [Fact]
    public void ConstructingFromExplicitStartPositionFenEqualsPositionFromStartingFenConstant()
    {
        Position startPosition = new(Position.StartingFEN);
        Position position = new(FenStart);

        Assert.True(position.Equals(startPosition));
    }

    [Fact]
    public void ConstructingFromFenAfterE4SetsBlackToMoveAndEnPassantSquareOnE3()
    {
        Position position = new(FenAfterE4);

        Assert.Equal(Colour.Black, position.SideToMove);
        Assert.Equal(Square.SquareAt("e3"), position.EnPassantSquare);
        Assert.Equal(1, position.HalfMoves);
    }

    [Fact]
    public void ConstructingFromFenAfterNf3ClearsEnPassantSquareAndAdvancesToThirdPly()
    {
        Position position = new(FenAfterNf3);

        Assert.Equal(Colour.Black, position.SideToMove);
        Assert.Equal(Position.InvalidSquare, position.EnPassantSquare);
        Assert.Equal(3, position.HalfMoves);
    }

    [Fact]
    public void ConstructingFromRuyLopezFenPlacesWhiteBishopOnB5()
    {
        Position position = new(FenRuyLopez);

        Assert.Equal(Colour.White | Piece.Bishop, position.Squares[Square.SquareAt("b5")]);
    }

    [Fact]
    public void ConstructingFromTruncatedTwoFieldFenDefaultsCastlingRightsEnPassantAndClocks()
    {
        Position position = new(FenTruncatedMidgame);

        Assert.Equal(Colour.White, position.SideToMove);
        Assert.Equal(CastlingRights.None, position.Castling);
        Assert.Equal(Position.InvalidSquare, position.EnPassantSquare);
        Assert.Equal(0, position.FiftyMovesClock);
        Assert.Equal(0, position.HalfMoves);
    }

    [Fact]
    public void ConstructingFromUnparseableFenFallsBackToStartingPosition()
    {
        Position position = new(string.Empty);

        Assert.True(position.Equals(new Position(Position.StartingFEN)));
    }

    [Fact]
    public void DeepCloneProducesEqualButIndependentPosition()
    {
        Position original = new(FenRuyLopez);
        Position clone = original.DeepClone();

        Assert.True(clone.Equals(original));
        Assert.NotSame(original.Board, clone.Board);

        clone.Board[0] = Piece.Empty;

        Assert.NotEqual(Piece.Empty, original.Squares[0]);
    }

    [Fact]
    public void EqualsReturnsFalseForPositionsWithDifferentSideToMove()
    {
        Position afterE4 = new(FenAfterE4);
        Position afterE4C5 = new(FenAfterE4C5);

        Assert.False(afterE4.Equals(afterE4C5));
    }
}
