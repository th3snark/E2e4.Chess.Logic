using System;
using System.Linq;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class PositionQueriesUnitTest
{
    private static string[] Names(System.Collections.Generic.IEnumerable<int> squares)
    {
        return squares.Select(Square.GetName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    private static int At(string name)
    {
        return Square.SquareAt(name);
    }

    [Fact]
    public void AttackersOfListsEveryPieceOfTheColourThatAttacksTheSquare()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(new[] { "e2", "g1", "g2" }, Names(position.AttackersOf(Colour.White, At("f3"))));
        Assert.Equal(new[] { "d8", "e8", "f8", "g8" }, Names(position.AttackersOf(Colour.Black, At("e7"))));
        Assert.Empty(position.AttackersOf(Colour.Black, At("f3")));
    }

    [Fact]
    public void AttackersOfStopsSlidersAtTheFirstPiece()
    {
        Position position = new("4k3/8/8/8/4r3/8/4R3/4K3 w - - 0 10");

        Assert.Equal(new[] { "e4" }, Names(position.AttackersOf(Colour.Black, At("e2"))));
        Assert.Equal(new[] { "e2" }, Names(position.AttackersOf(Colour.White, At("e4"))));
        Assert.Empty(position.AttackersOf(Colour.Black, At("e1")));
    }

    [Fact]
    public void AttackersOfWithTheKingSquareListsTheCheckers()
    {
        Position position = new("4r2k/8/8/8/1b6/8/8/4K3 w - - 0 10");

        Assert.Equal(new[] { "b4", "e8" }, Names(position.AttackersOf(Colour.Black, At("e1"))));
    }

    [Fact]
    public void AttackGeometryRejectsBadArguments()
    {
        Position position = new(Position.StartingFEN);

        Assert.Throws<ArgumentOutOfRangeException>(() => position.AttackersOf(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.AttackersOf(Colour.White, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.AttacksFrom(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.LegalMovesFrom(64));
    }

    [Fact]
    public void AttacksFromIncludesDefendedPiecesAndStopsAtBlockers()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(new[] { "a2", "b1" }, Names(position.AttacksFrom(At("a1"))));
        Assert.Equal(new[] { "a3", "c3", "d2" }, Names(position.AttacksFrom(At("b1"))));
        Assert.Equal(new[] { "d3", "f3" }, Names(position.AttacksFrom(At("e2"))));
        Assert.Equal(new[] { "c7", "c8", "d7", "e7", "e8" }, Names(position.AttacksFrom(At("d8"))));
        Assert.Empty(position.AttacksFrom(At("e4")));
    }

    [Fact]
    public void AttacksFromTreatsPawnsOfEachColourAsAttackingForward()
    {
        Position position = new("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 10");

        Assert.Equal(new[] { "d5", "f5" }, Names(position.AttacksFrom(At("e4"))));
        Assert.Equal(new[] { "c4", "e4" }, Names(position.AttacksFrom(At("d5"))));
    }

    [Fact]
    public void LegalMovesFromListsOnlyThatPiecesLegalMoves()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(new[] { "e2e3", "e2e4" }, position.LegalMovesFrom(At("e2")).Select(Move.GetCoordinateNotation).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "b1a3", "b1c3" }, position.LegalMovesFrom(At("b1")).Select(Move.GetCoordinateNotation).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Empty(position.LegalMovesFrom(At("a1")));
        Assert.Empty(position.LegalMovesFrom(At("e4")));
        Assert.Empty(position.LegalMovesFrom(At("e7")));
    }

    [Fact]
    public void LegalMovesFromHonoursPins()
    {
        Position position = new("4k3/8/8/8/4r3/8/4N3/4K3 w - - 0 10");

        Assert.Empty(position.LegalMovesFrom(At("e2")));
    }

    [Fact]
    public void FindReturnsTheLegalMoveBetweenTwoSquares()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(Move.Create(position, "e2e4"), Move.Find(position, At("e2"), At("e4")));
        Assert.Equal(Move.Invalid, Move.Find(position, At("e2"), At("e5")));
        Assert.Equal(Move.Invalid, Move.Find(position, At("e4"), At("e5")));
        Assert.Equal(Move.Invalid, Move.Find(position, At("e7"), At("e5")));
    }

    [Fact]
    public void FindFillsInCastlingAndEnPassant()
    {
        Position castling = new("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 10");
        Assert.True(Move.IsCastle(Move.Find(castling, At("e1"), At("g1"))));

        Position enPassant = new("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 10");
        Assert.True(Move.IsEnPassant(Move.Find(enPassant, At("e5"), At("d6"))));
    }

    [Fact]
    public void FindPromotesToAQueenUnlessToldOtherwise()
    {
        Position position = new("4k3/P7/8/8/8/8/8/4K3 w - - 0 10");

        Assert.Equal("a7a8q", Move.GetCoordinateNotation(Move.Find(position, At("a7"), At("a8"))));
        Assert.Equal("a7a8n", Move.GetCoordinateNotation(Move.Find(position, At("a7"), At("a8"), Piece.Knight)));
        Assert.Equal("a7a8r", Move.GetCoordinateNotation(Move.Find(position, At("a7"), At("a8"), Piece.Rook)));
    }

    [Fact]
    public void FindRejectsSquaresOutsideTheBoard()
    {
        Position position = new(Position.StartingFEN);

        Assert.Throws<ArgumentOutOfRangeException>(() => Move.Find(position, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Move.Find(position, 0, 64));
    }
}
