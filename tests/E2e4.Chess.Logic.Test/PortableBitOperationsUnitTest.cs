using System;
using System.Collections.Generic;
using System.Numerics;
using E2e4.Chess.Logic.Bitboards;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// The test project runs on net10.0, where <see cref="Bitboard"/> uses the CPU intrinsics, so the portable versions that
/// the net45 and netstandard builds use would never run. These tests check them against the intrinsics instead.
/// </summary>
public class PortableBitOperationsUnitTest
{
    /// <summary>Every shape of bitboard that has gone wrong in bit-twiddling code: single bits, pairs, runs, patterns, and random sparse, dense and uniform values.</summary>
    private static List<ulong> Samples()
    {
        List<ulong> samples = new() { 0UL, ulong.MaxValue, 0x5555555555555555UL, 0xAAAAAAAAAAAAAAAAUL, 0x00000000FFFFFFFFUL, 0xFFFFFFFF00000000UL };

        for (int i = 0; i < 64; i++)
        {
            samples.Add(1UL << i);
            samples.Add(~(1UL << i));
            samples.Add(ulong.MaxValue >> i);
            samples.Add(ulong.MaxValue << i);

            for (int j = i + 1; j < 64; j++)
                samples.Add((1UL << i) | (1UL << j));
        }

        Random random = new(20261015);
        byte[] bytes = new byte[8];
        for (int i = 0; i < 20000; i++)
        {
            ulong a = Next(random, bytes);
            ulong b = Next(random, bytes);
            ulong c = Next(random, bytes);
            samples.Add(a);
            samples.Add(a & b & c);   // sparse
            samples.Add(a | b | c);   // dense
        }

        return samples;
    }

    private static ulong Next(Random random, byte[] buffer)
    {
        random.NextBytes(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }

    [Fact]
    public void TrailingZeroCountMatchesTheIntrinsicForEveryNonEmptyBitboard()
    {
        foreach (ulong value in Samples())
        {
            if (value == 0)
                continue;
            Assert.True(
                BitOperations.TrailingZeroCount(value) == PortableBitOperations.TrailingZeroCount(value),
                $"TrailingZeroCount differs for 0x{value:X16}");
        }
    }

    [Fact]
    public void HighestSetBitMatchesTheIntrinsicForEveryNonEmptyBitboard()
    {
        foreach (ulong value in Samples())
        {
            if (value == 0)
                continue;
            Assert.True(
                63 - BitOperations.LeadingZeroCount(value) == PortableBitOperations.HighestSetBit(value),
                $"HighestSetBit differs for 0x{value:X16}");
        }
    }

    [Fact]
    public void IsolateHighestBitMatchesTheIntrinsicIncludingTheEmptyBitboard()
    {
        foreach (ulong value in Samples())
        {
            ulong expected = value == 0 ? 0UL : 1UL << (63 - BitOperations.LeadingZeroCount(value));
            Assert.True(expected == PortableBitOperations.IsolateHighestBit(value), $"IsolateHighestBit differs for 0x{value:X16}");
        }
    }

    [Fact]
    public void PopCountMatchesTheIntrinsicIncludingTheEmptyBitboard()
    {
        foreach (ulong value in Samples())
            Assert.True(BitOperations.PopCount(value) == PortableBitOperations.PopCount(value), $"PopCount differs for 0x{value:X16}");
    }

    [Fact]
    public void TheBitboardOperationsAgreeWithThePortableOnes()
    {
        // On net10.0 Bitboard calls the intrinsics; on the older targets it calls the portable versions. Either way the answers must be these.
        foreach (ulong value in Samples())
        {
            Assert.Equal(PortableBitOperations.PopCount(value), Bitboard.Count(value));
            Assert.Equal(PortableBitOperations.IsolateHighestBit(value), Bitboard.IsolateReverse(value));
            if (value == 0)
                continue;
            Assert.Equal(PortableBitOperations.TrailingZeroCount(value), Bitboard.Scan(value));
            Assert.Equal(PortableBitOperations.HighestSetBit(value), Bitboard.ScanReverse(value));
        }
    }
}
