using System;
using System.Linq;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class SquareGeometryUnitTest
{
    private static int At(string name)
    {
        return Square.SquareAt(name);
    }

    private static string[] Names(System.Collections.Generic.IEnumerable<int> squares)
    {
        return squares.Select(Square.GetName).ToArray();
    }

    [Fact]
    public void DiagonalsGroupSquaresOnTheSameDiagonal()
    {
        Assert.Equal(Square.RisingDiagonal(At("a1")), Square.RisingDiagonal(At("h8")));
        Assert.Equal(Square.RisingDiagonal(At("c3")), Square.RisingDiagonal(At("e5")));
        Assert.NotEqual(Square.RisingDiagonal(At("a1")), Square.RisingDiagonal(At("a2")));
        Assert.Equal(Square.FallingDiagonal(At("a8")), Square.FallingDiagonal(At("h1")));
        Assert.Equal(Square.FallingDiagonal(At("c5")), Square.FallingDiagonal(At("e3")));
        Assert.NotEqual(Square.FallingDiagonal(At("a8")), Square.FallingDiagonal(At("a7")));
    }

    [Theory]
    [InlineData("e1", "e8", true)]
    [InlineData("a4", "h4", true)]
    [InlineData("a1", "h8", true)]
    [InlineData("h1", "a8", true)]
    [InlineData("b1", "c3", false)]
    [InlineData("a1", "b3", false)]
    [InlineData("e4", "e4", false)]
    public void SameLineFollowsFilesRanksAndDiagonals(string from, string to, bool expected)
    {
        Assert.Equal(expected, Square.SameLine(At(from), At(to)));
        Assert.Equal(expected, Square.SameLine(At(to), At(from)));
    }

    [Theory]
    [InlineData("b1", "c3", true)]
    [InlineData("b1", "a3", true)]
    [InlineData("g8", "e7", true)]
    [InlineData("e4", "e5", false)]
    [InlineData("e4", "g6", false)]
    [InlineData("e4", "e4", false)]
    public void KnightStepsAreRecognised(string from, string to, bool expected)
    {
        Assert.Equal(expected, Square.IsKnightStep(At(from), At(to)));
    }

    [Fact]
    public void BetweenListsTheSquaresOnTheLineNearestFirst()
    {
        Assert.Equal(new[] { "b2", "c3", "d4", "e5", "f6", "g7" }, Names(Square.Between(At("a1"), At("h8"))));
        Assert.Equal(new[] { "e7", "e6", "e5" }, Names(Square.Between(At("e8"), At("e4"))));
        Assert.Equal(new[] { "d4", "c4" }, Names(Square.Between(At("e4"), At("b4"))));
        Assert.Equal(new[] { "g2" }, Names(Square.Between(At("h1"), At("f3"))));
    }

    [Fact]
    public void BetweenIsEmptyForNeighboursAndSquaresOffAnyLine()
    {
        Assert.Empty(Square.Between(At("e4"), At("e5")));
        Assert.Empty(Square.Between(At("e4"), At("e4")));
        Assert.Empty(Square.Between(At("b1"), At("c3")));
    }

    [Fact]
    public void SquaresOutsideTheBoardAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Square.SameLine(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Square.SameLine(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => Square.IsKnightStep(64, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Square.Between(0, -1));
    }
}
