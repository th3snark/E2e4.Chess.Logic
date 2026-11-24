using System;
using System.Collections.Generic;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// The history stacks behind make/unmake start small and grow; these check that growing loses nothing.
/// </summary>
public class PositionHistoryUnitTest
{
    private static readonly string[] KnightCycle = { "g1f3", "g8f6", "f3g1", "f6g8" };

    [Fact]
    public void AFreshPositionStartsWithSmallHistory()
    {
        Position position = new(Position.StartingFEN);

        Assert.Equal(Position.InitialHistoryCapacity, position.ZobristKeyHistory.Length);
        Assert.Equal(Position.InitialHistoryCapacity, position.EnPassantHistory.Length);
    }

    [Fact]
    public void MakingAndUnmakingPastEveryGrowthKeepsTheHistory()
    {
        Position position = new(Position.StartingFEN);
        List<int> made = new();
        List<string> fens = new() { position.GetFEN() };

        // A double pawn push early on records an en passant square that must survive each growth.
        foreach (string coordinates in new[] { "e2e4", "d7d5" })
        {
            int move = Move.Create(position, coordinates);
            position.Make(move);
            made.Add(move);
            fens.Add(position.GetFEN());
        }
        while (position.HalfMoves < Position.HalfMovesLimit - 1)
        {
            int move = Move.Create(position, KnightCycle[(made.Count - 2) % 4]);
            Assert.NotEqual(Move.Invalid, move);
            position.Make(move);
            made.Add(move);
            fens.Add(position.GetFEN());
        }

        Assert.Equal(Position.HalfMovesLimit, position.ZobristKeyHistory.Length);
        Assert.Throws<InvalidOperationException>(() => position.Make(Move.Create(position, KnightCycle[(made.Count - 2) % 4])));
        Assert.True(position.HasRepeated(3));

        Position clone = position.DeepClone();
        Assert.Equal(position, clone);

        for (int i = made.Count - 1; i >= 0; i--)
        {
            position.Unmake(made[i]);
            Assert.Equal(fens[i], position.GetFEN());
        }
        Assert.Equal("d6", Square.GetName(EnPassantAfterSecondMove(clone, made)));
    }

    private static int EnPassantAfterSecondMove(Position clone, List<int> made)
    {
        for (int i = made.Count - 1; i >= 2; i--)
            clone.Unmake(made[i]);
        return clone.EnPassantSquare;
    }

    [Fact]
    public void APositionLoadedLateInAGameHasRoomForItsPly()
    {
        Assert.True(Position.TryParse("4k3/8/8/8/8/8/4P3/4K3 w - - 0 400", out Position? position));

        Assert.Equal(798, position.HalfMoves);
        Assert.True(position.ZobristKeyHistory.Length > 798);
        position.Make(Move.Create(position, "e2e4"));
        Assert.Equal("4k3/8/8/8/4P3/8/8/4K3 b - e3 0 400", position.GetFEN());
        Position clone = position.DeepClone();
        Assert.Equal(position, clone);
        Assert.Equal(position.ZobristKey, clone.ZobristKey);
    }

    [Fact]
    public void NullMovesGrowTheHistoryToo()
    {
        Position position = new("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        for (int i = 0; i < 200; i++)
            position.MakeNull();
        for (int i = 0; i < 200; i++)
            position.UnmakeNull();

        Assert.Equal("4k3/8/8/8/8/8/8/4K3 w - - 0 1", position.GetFEN());
    }
}
