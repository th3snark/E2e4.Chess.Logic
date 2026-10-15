using System;
using E2e4.Chess.Logic.Bitboards;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class BitboardUnitTest
{
    [Theory]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 1)]
    [InlineData(Bitboard.LightSquares, 32)]
    [InlineData(ulong.MaxValue, 64)]
    [InlineData(0b1011UL, 3)]
    public void CountReturnsNumberOfSetBitsForVariousBitboards(ulong bitboard, int expected)
    {
        Assert.Equal(expected, Bitboard.Count(bitboard));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(Bitboard.LightSquares)]
    [InlineData(ulong.MaxValue)]
    [InlineData(0b1011UL)]
    public void CountSparseAgreesWithCountForVariousBitboards(ulong bitboard)
    {
        Assert.Equal(Bitboard.Count(bitboard), Bitboard.CountSparse(bitboard));
    }

    [Theory]
    [InlineData(1UL, 0)]
    [InlineData(1UL << 5, 5)]
    [InlineData(0b1100UL, 2)]
    [InlineData(1UL << 63, 63)]
    public void ScanReturnsIndexOfLeastSignificantSetBit(ulong bitboard, int expected)
    {
        Assert.Equal(expected, Bitboard.Scan(bitboard));
    }

    [Theory]
    [InlineData(1UL, 0)]
    [InlineData(0b1100UL, 3)]
    [InlineData(1UL << 63, 63)]
    [InlineData(ulong.MaxValue, 63)]
    public void ScanReverseReturnsIndexOfMostSignificantSetBit(ulong bitboard, int expected)
    {
        Assert.Equal(expected, Bitboard.ScanReverse(bitboard));
    }

    [Fact]
    public void PopRemovesLeastSignificantBitAndReturnsItsIndex()
    {
        ulong bitboard = 0b1010;

        Assert.Equal(1, Bitboard.Pop(ref bitboard));
        Assert.Equal(0b1000UL, bitboard);

        Assert.Equal(3, Bitboard.Pop(ref bitboard));
        Assert.Equal(0UL, bitboard);
    }

    [Theory]
    [InlineData(1UL, 0)]
    [InlineData(1UL << 10, 10)]
    [InlineData(1UL << 63, 63)]
    public void ReadReturnsIndexOfSingleSetBit(ulong bitboard, int expected)
    {
        Assert.Equal(expected, Bitboard.Read(bitboard));
    }

    [Theory]
    [InlineData(0b1010UL, 0b0010UL)]
    [InlineData(0b1000UL, 0b1000UL)]
    public void IsolateKeepsOnlyLeastSignificantSetBit(ulong bitboard, ulong expected)
    {
        Assert.Equal(expected, Bitboard.Isolate(bitboard));
    }

    [Theory]
    [InlineData(0b1010UL, 0b1000UL)]
    [InlineData(1UL, 1UL)]
    public void IsolateReverseKeepsOnlyMostSignificantSetBit(ulong bitboard, ulong expected)
    {
        Assert.Equal(expected, Bitboard.IsolateReverse(bitboard));
    }

    [Fact]
    public void ToStringUInt64ProducesSixtyFourCharacterBinaryString()
    {
        string zero = Bitboard.ToString(0UL);
        string lightSquares = Bitboard.ToString(Bitboard.LightSquares);

        Assert.Equal(64, zero.Length);
        Assert.DoesNotContain('1', zero);

        Assert.Equal(64, lightSquares.Length);
        Assert.Equal(32, CountSetChars(lightSquares));
    }

    [Fact]
    public void ToMatrixProducesEightByEightNewlineDelimitedGrid()
    {
        string matrix = Bitboard.ToMatrix(Bitboard.LightSquares);
        string[] rows = matrix.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        Assert.Equal(8, rows.Length);
        Assert.All(rows, row => Assert.Equal(8, row.Length));
        Assert.Equal(32, CountSetChars(matrix));
    }

    private static int CountSetChars(string binary)
    {
        int count = 0;
        foreach (char c in binary)
            if (c == '1')
                count++;
        return count;
    }
}
