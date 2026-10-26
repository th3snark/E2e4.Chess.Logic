using System;
using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class TeachingQueriesUnitTest
{
    private static string[] Names(IEnumerable<int> squares)
    {
        return squares.Select(Square.GetName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    private static string[] Described(IEnumerable<Pin> pins)
    {
        return pins.Select(p => Square.GetName(p.Pinned) + "<-" + Square.GetName(p.Pinner)).ToArray();
    }

    [Fact]
    public void CheckersListsTheCheckingPiecesOnly()
    {
        Position position = new("4r2k/8/8/8/1b6/8/8/4K3 w - - 0 10");

        Assert.Equal(new[] { "b4", "e8" }, Names(position.Checkers(Colour.White)));
        Assert.Empty(position.Checkers(Colour.Black));
    }

    [Fact]
    public void CheckersRejectsABadColour()
    {
        Position position = new(Position.StartingFEN);

        Assert.Throws<ArgumentOutOfRangeException>(() => position.Checkers(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.Pins(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.HangingPieces(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.AttackersOf(2, 0));
    }

    [Fact]
    public void ARookPinsAPieceOnAFile()
    {
        Position position = new("4k3/4r3/8/8/8/4N3/4K3/8 w - - 0 1");

        Assert.Equal(new[] { "e3<-e7" }, Described(position.Pins(Colour.White)));
        Assert.Empty(position.Pins(Colour.Black));
    }

    [Fact]
    public void ABishopPinsAPieceOnADiagonalButARookDoesNot()
    {
        Assert.Equal(new[] { "c3<-e5" }, Described(new Position("4k3/8/8/4b3/8/2N5/1K6/8 w - - 0 1").Pins(Colour.White)));
        Assert.Empty(new Position("4k3/8/8/4r3/8/2N5/1K6/8 w - - 0 1").Pins(Colour.White));
        Assert.Empty(new Position("4k3/4b3/8/8/8/4N3/4K3/8 w - - 0 1").Pins(Colour.White));
    }

    [Fact]
    public void AQueenPinsOnEitherKindOfLine()
    {
        Assert.Equal(new[] { "e3<-e7" }, Described(new Position("4k3/4q3/8/8/8/4N3/4K3/8 w - - 0 1").Pins(Colour.White)));
        Assert.Equal(new[] { "c3<-e5" }, Described(new Position("4k3/8/8/4q3/8/2N5/1K6/8 w - - 0 1").Pins(Colour.White)));
    }

    [Fact]
    public void TwoPiecesOnTheLineAreNotPinned()
    {
        Assert.Empty(new Position("4k3/4r3/8/8/4P3/4N3/4K3/8 w - - 0 1").Pins(Colour.White));
        Assert.Empty(new Position("4k3/4r3/8/8/4n3/4N3/4K3/8 w - - 0 1").Pins(Colour.White));
    }

    [Fact]
    public void SeveralPinsAreListedInSquareOrder()
    {
        Position position = new("4k3/4r3/8/8/7b/8/4NN2/4K3 w - - 0 1");

        Assert.Equal(new[] { "e2<-e7", "f2<-h4" }, Described(position.Pins(Colour.White)));
    }

    [Fact]
    public void ThePinnedPieceCanStillMoveAlongTheLine()
    {
        Position position = new("4k3/4r3/8/8/8/4R3/4K3/8 w - - 0 1");

        Assert.Equal(new[] { "e3<-e7" }, Described(position.Pins(Colour.White)));
        Assert.All(position.LegalMovesFrom(Square.SquareAt("e3")), move => Assert.Equal(4, Square.File(Move.To(move))));
    }

    [Fact]
    public void DefendersOfListsTheOwnPiecesThatCoverTheSquare()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(new[] { "d1", "e1", "f1", "g1" }, Names(position.DefendersOf(Square.SquareAt("e2"))));
        Assert.Equal(new[] { "b1", "c1", "d1", "e1" }, Names(position.DefendersOf(Square.SquareAt("d2"))));
        Assert.Empty(position.DefendersOf(Square.SquareAt("e4")));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.DefendersOf(64));
    }

    [Fact]
    public void AnAttackedPieceWithoutDefendersHangs()
    {
        Position position = new("4k3/8/8/3p4/8/2N5/8/4K3 w - - 0 1");

        Assert.Equal(new[] { "d5" }, Names(position.HangingPieces(Colour.Black)));
        Assert.Empty(position.HangingPieces(Colour.White));
    }

    [Fact]
    public void ADefendedPieceDoesNotHang()
    {
        Position position = new("8/8/4k3/3p4/8/2N5/8/4K3 w - - 0 1");

        Assert.Equal(new[] { "e6" }, Names(position.DefendersOf(Square.SquareAt("d5"))));
        Assert.Empty(position.HangingPieces(Colour.Black));
    }

    [Fact]
    public void KingsAreNeverListedAsHanging()
    {
        Position position = new("4r2k/8/8/8/8/8/8/4K3 w - - 0 1");

        Assert.Empty(position.HangingPieces(Colour.White));
    }
}
