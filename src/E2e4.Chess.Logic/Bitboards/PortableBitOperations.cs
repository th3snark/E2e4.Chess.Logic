using System;

namespace E2e4.Chess.Logic.Bitboards;

/// <summary>
/// Plain C# versions of the four bit operations <see cref="Bitboard"/> needs, for the target frameworks that have no
/// <c>System.Numerics.BitOperations</c> (net45, netstandard2.0 and netstandard2.1). They are compiled for every
/// target, including net10.0 where <see cref="Bitboard"/> does not call them, so that the unit tests can check them
/// against the hardware intrinsics: an error in one of them would otherwise only show on a runtime the tests never use.
/// </summary>
/// <remarks>
/// Like the intrinsics they replace, the bit scans require a non-empty bitboard; their result for an empty one is
/// unspecified and differs from <c>BitOperations</c>.
/// </remarks>
internal static class PortableBitOperations
{
    private const UInt64 DeBruijn = 0x07EDD5E59A4E28C2UL;

    /// <summary>Maps the de Bruijn product of an isolated bit to the index of that bit.</summary>
    private static readonly Int32[] BitIndex = CreateBitIndex();

    private static Int32[] CreateBitIndex()
    {
        Int32[] table = new Int32[64];
        for (Int32 i = 0; i < 64; i++)
            table[((1UL << i) * DeBruijn) >> 58] = i;
        return table;
    }

    /// <summary>The index of the least significant set bit, by de Bruijn multiplication.</summary>
    public static Int32 TrailingZeroCount(UInt64 bitboard)
    {
        return BitIndex[((bitboard & (0UL - bitboard)) * DeBruijn) >> 58];
    }

    /// <summary>The index of the most significant set bit, by binary search.</summary>
    public static Int32 HighestSetBit(UInt64 bitboard)
    {
        Int32 result = 0;
        if (bitboard > 0xFFFFFFFF)
        {
            bitboard >>= 32;
            result = 32;
        }
        if (bitboard > 0xFFFF)
        {
            bitboard >>= 16;
            result += 16;
        }
        if (bitboard > 0xFF)
        {
            bitboard >>= 8;
            result += 8;
        }
        if (bitboard > 0xF)
        {
            bitboard >>= 4;
            result += 4;
        }
        if (bitboard > 0x3)
        {
            bitboard >>= 2;
            result += 2;
        }
        if (bitboard > 0x1)
            result++;
        return result;
    }

    /// <summary>The bitboard with only the most significant set bit kept (0 for an empty bitboard).</summary>
    public static UInt64 IsolateHighestBit(UInt64 bitboard)
    {
        bitboard |= (bitboard >> 1);
        bitboard |= (bitboard >> 2);
        bitboard |= (bitboard >> 4);
        bitboard |= (bitboard >> 8);
        bitboard |= (bitboard >> 16);
        bitboard |= (bitboard >> 32);
        return bitboard & ~(bitboard >> 1);
    }

    /// <summary>The number of set bits, by the SWAR (parallel bit-counting) method.</summary>
    public static Int32 PopCount(UInt64 bitboard)
    {
        bitboard -= (bitboard >> 1) & 0x5555555555555555UL;
        bitboard = (bitboard & 0x3333333333333333UL) + ((bitboard >> 2) & 0x3333333333333333UL);
        return (Int32)(((bitboard + (bitboard >> 4) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
    }
}
