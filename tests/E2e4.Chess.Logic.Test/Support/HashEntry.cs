using System;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Hashing;

/// <summary>
/// Represents an entry in the transposition hash table: the key of a position, the best move
/// found for it and a packed type, search depth and value.
/// </summary>
internal readonly struct HashEntry
{
    /// <summary>
    /// Specifies the hash entry is invalid.
    /// </summary>
    public const int Invalid = 0;

    /// <summary>
    /// Specifies the value associated with the hash entry gives an exact value.
    /// </summary>
    public const int Exact = 1;

    /// <summary>
    /// Specifies the value associated with the hash entry gives a lower bound
    /// value.
    /// </summary>
    public const int Alpha = 2;

    /// <summary>
    /// Specifies the value associated with the hash entry gives an upper bound
    /// value.
    /// </summary>
    public const int Beta = 3;

    /// <summary>
    /// The size of a hash entry in bytes.
    /// </summary>
    public const int Size = 16;

    /// <summary>
    /// The number of bits used for encoding the type in the miscellaneous field.
    /// </summary>
    private const int TypeBits = 2;

    /// <summary>
    /// The number of bits used for encoding the depth in the miscellaneous field.
    /// </summary>
    private const int DepthBits = 8;

    /// <summary>
    /// The amount the depth is shifted in the miscellaneous field.
    /// </summary>
    private const int DepthShift = TypeBits;

    /// <summary>
    /// The amount the value is shifted in the miscellaneous field.
    /// </summary>
    private const int ValueShift = DepthShift + DepthBits;

    /// <summary>
    /// The value for normalizing the depth in the miscellaneous field. Adding
    /// this factor will guarantee a positive depth.
    /// </summary>
    private const int DepthNormal = 1 << (DepthBits - 1);

    /// <summary>
    /// The value for normalizing the value in the miscellaneous field. Adding
    /// this factor will guarantee a positive value.
    /// </summary>
    private const int ValueNormal = short.MaxValue;

    /// <summary>
    /// The mask for extracting the unshifted type from the miscellaneous field.
    /// </summary>
    private const int TypeMask = (1 << TypeBits) - 1;

    /// <summary>
    /// The mask for extracting the unshifted depth from the miscellaneous field.
    /// </summary>
    private const int DepthMask = (1 << DepthBits) - 1;

    /// <summary>
    /// The Zobrist key of the position associated with the hash entry.
    /// </summary>
    public readonly ulong Key;

    /// <summary>
    /// The best move for the position associated with the hash entry.
    /// </summary>
    public readonly int Move;

    /// <summary>
    /// Contains the entry type, search depth, and search value associated with
    /// the hash entry. The properties are rolled into a single value for space
    /// efficiency.
    /// </summary>
    public readonly int Misc;

    /// <summary>
    /// The type of the value associated with the hash entry.
    /// </summary>
    public int Type
    {
        get { return Misc & TypeMask; }
    }

    /// <summary>
    /// The search depth associated with the hash entry.
    /// </summary>
    public int Depth
    {
        get { return ((Misc >> DepthShift) & DepthMask) - DepthNormal; }
    }

    /// <summary>
    /// Constructs a hash entry.
    /// </summary>
    /// <param name="position">The position to associate with the hash entry.</param>
    /// <param name="depth">The depth of the search, from -128 to 127.</param>
    /// <param name="move">The best move for the position.</param>
    /// <param name="value">The value of the search, from -32767 to 32767.</param>
    /// <param name="type">The type of the value.</param>
    public HashEntry(Position position, int depth, int move, int value, int type)
    {
        if (depth < -DepthNormal || depth >= DepthNormal)
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The depth does not fit the packed hash entry.");
        if (value < -ValueNormal || value > ValueNormal)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value does not fit the packed hash entry.");
        if (type < Invalid || type > Beta)
            throw new ArgumentOutOfRangeException(nameof(type), type, "The type is not a valid hash entry type.");

        Key = position.ZobristKey;
        Move = move;
        Misc = type | ((depth + DepthNormal) << DepthShift) | (value + ValueNormal) << ValueShift;
    }

    /// <summary>
    /// Returns the value associated with the hash entry.
    /// </summary>
    /// <returns>The value associated with the hash entry.</returns>
    public int GetValue()
    {
        return (Misc >> ValueShift) - ValueNormal;
    }
}
