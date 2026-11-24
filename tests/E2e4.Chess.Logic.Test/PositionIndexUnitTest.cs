using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Games;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// The position-keyed opening index, <see cref="PositionIndex"/>.
/// </summary>
public class PositionIndexUnitTest
{
    private static GameRecord Game(string pgn)
    {
        Assert.True(Pgn.TryParseGame(pgn, out GameRecord? game));
        Assert.Empty(game!.Errors);
        return game;
    }

    [Fact]
    public void CountsMovesAndResultsPerPosition()
    {
        PositionIndex index = new();
        index.Add(Game("1. e4 e5 2. Nf3 1-0"));
        index.Add(Game("1. e4 c5 0-1"));
        index.Add(Game("1. d4 d5 1/2-1/2"));
        index.Add(Game("1. e4 e5 *"));

        IReadOnlyList<PositionIndexMove> first = index.Lookup(new Position(Position.StartingFEN));

        Assert.Equal(4, index.GameCount);
        Assert.Equal(new[] { "e4", "d4" }, first.Select(m => m.San));
        Assert.Equal(3, first[0].Count);
        Assert.Equal(1, first[0].WhiteWins);
        Assert.Equal(1, first[0].BlackWins);
        Assert.Equal(0, first[0].Draws);
        Assert.Equal(1, first[1].Draws);

        GameRecord probe = Game("1. e4 *");
        IReadOnlyList<PositionIndexMove> replies = index.Lookup(probe.Root.Next!.ZobristKey);
        Assert.Equal(new[] { "e5", "c5" }, replies.Select(m => m.San));
        Assert.Equal(2, replies[0].Count);
    }

    [Fact]
    public void TranspositionsShareAnEntry()
    {
        PositionIndex index = new();
        GameRecord viaNf3 = Game("1. Nf3 d5 2. d4 Nf6 3. c4 *");
        GameRecord viaD4 = Game("1. d4 d5 2. Nf3 Nf6 3. Bf4 *");
        index.Add(viaNf3);
        index.Add(viaD4);

        GameNode afterNf6 = viaNf3.NodeAt(new[] { 0, 0, 0, 0 })!;
        Assert.Equal(viaD4.NodeAt(new[] { 0, 0, 0, 0 })!.ZobristKey, afterNf6.ZobristKey);

        IReadOnlyList<PositionIndexMove> moves = index.Lookup(afterNf6.ZobristKey);
        Assert.Equal(new[] { "Bf4", "c4" }, moves.Select(m => m.San));
    }

    [Fact]
    public void VariationsCountOnlyWhenAsked()
    {
        GameRecord game = Game("1. e4 e5 (1... c5) (1... e6) *");

        PositionIndex mainLine = new();
        mainLine.Add(game);
        PositionIndex everything = new();
        everything.Add(game, includeVariations: true);

        ulong afterE4 = game.Root.Next!.ZobristKey;
        Assert.Single(mainLine.Lookup(afterE4));
        Assert.Equal(3, everything.Lookup(afterE4).Count);
        Assert.Equal(2, mainLine.PositionCount);
    }

    [Fact]
    public void AGameCountsOnceForARepeatedPositionAndMove()
    {
        PositionIndex index = new();
        index.Add(Game("1. Nf3 Nf6 2. Ng1 Ng8 3. Nf3 Nf6 *"));

        IReadOnlyList<PositionIndexMove> moves = index.Lookup(new Position(Position.StartingFEN));

        Assert.Equal(1, Assert.Single(moves).Count);
    }

    [Fact]
    public void AnUnknownPositionHasNoMoves()
    {
        PositionIndex index = new();

        Assert.Empty(index.Lookup(new Position(Position.StartingFEN)));
        Assert.Equal(0, index.PositionCount);
    }
}
