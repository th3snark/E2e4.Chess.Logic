using System;
using E2e4.Chess.Logic.Hashing;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class HashingUnitTest
{
    private static readonly Position Start = new(Position.StartingFEN);

    [Theory]
    [InlineData(-128)]
    [InlineData(-5)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(127)]
    public void DepthRoundTripsIncludingNegativeValues(int depth)
    {
        HashEntry entry = new(Start, depth, 123, 50, HashEntry.Exact);

        Assert.Equal(depth, entry.Depth);
    }

    [Theory]
    [InlineData(-32767, HashEntry.Exact, -128)]
    [InlineData(-1, HashEntry.Alpha, -1)]
    [InlineData(0, HashEntry.Beta, 0)]
    [InlineData(32767, HashEntry.Exact, 127)]
    public void ValueTypeAndDepthDoNotInterfere(int value, int type, int depth)
    {
        HashEntry entry = new(Start, depth, 99, value, type);

        Assert.Equal(value, entry.GetValue());
        Assert.Equal(type, entry.Type);
        Assert.Equal(depth, entry.Depth);
        Assert.Equal(Start.ZobristKey, entry.Key);
        Assert.Equal(99, entry.Move);
    }

    [Theory]
    [InlineData(128, 0, HashEntry.Exact)]
    [InlineData(-129, 0, HashEntry.Exact)]
    [InlineData(0, 32768, HashEntry.Exact)]
    [InlineData(0, -32768, HashEntry.Exact)]
    [InlineData(0, 0, 4)]
    [InlineData(0, 0, -1)]
    public void ConstructorRejectsFieldsThatDoNotFitThePackedEncoding(int depth, int value, int type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashEntry(Start, depth, 0, value, type));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(-16)]
    public void HashTableRejectsSizesSmallerThanOneEntry(int bytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashTable(bytes));
    }

    [Fact]
    public void ProbeOfEmptyTableDoesNotMatchZeroKey()
    {
        HashTable table = new(HashEntry.Size * 4);

        Assert.False(table.TryProbe(0, out _));
    }

    [Fact]
    public void StoredEntryCanBeProbedAndCleared()
    {
        HashTable table = new(HashEntry.Size * 1024);
        HashEntry entry = new(Start, 3, 7, 25, HashEntry.Beta);

        table.Store(entry);

        Assert.Equal(1, table.Count);
        Assert.True(table.TryProbe(Start.ZobristKey, out HashEntry found));
        Assert.Equal(entry.Move, found.Move);
        Assert.Equal(3, found.Depth);

        table.Clear();

        Assert.Equal(0, table.Count);
        Assert.False(table.TryProbe(Start.ZobristKey, out _));
    }

    [Fact]
    public void StoringOverAnOccupiedSlotDoesNotIncreaseCount()
    {
        HashTable table = new(HashEntry.Size);

        table.Store(new HashEntry(Start, 1, 1, 0, HashEntry.Exact));
        table.Store(new HashEntry(Start, 2, 2, 0, HashEntry.Exact));

        Assert.Equal(1, table.Count);
    }
}
