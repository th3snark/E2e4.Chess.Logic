using System;

namespace E2e4.Chess.Logic.Hashing;

/// <summary>
/// Represents a transposition hash table.
/// </summary>
internal sealed class HashTable
{
    /// <summary>
    /// The collection of hash entries.
    /// </summary>
    private readonly HashEntry[] _entries;

    /// <summary>
    /// The number of entries stored in the hash table.
    /// </summary>
    public int Count
    {
        get;
        private set;
    }

    /// <summary>
    /// The number of entries that can be stored in the hash table.
    /// </summary>
    public int Capacity
    {
        get { return (int)_capacity; }
    }
    private readonly ulong _capacity;

    /// <summary>
    /// The size of the hash table in bytes.
    /// </summary>
    public int Size
    {
        get { return HashEntry.Size * Capacity; }
    }

    /// <summary>
    /// Constructs a hash table of the given size in bytes.
    /// </summary>
    /// <param name="bytes">The size of the new hash table in bytes.</param>
    public HashTable(int bytes)
    {
        if (bytes < HashEntry.Size)
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "The hash table must hold at least one entry.");
        _capacity = (ulong)(bytes / HashEntry.Size);
        _entries = new HashEntry[Capacity];
    }

    /// <summary>
    /// Finds the entry in the hash table for the given key. The return value
    /// indicates whether the entry was found.
    /// </summary>
    /// <param name="key">The key to find the entry for.</param>
    /// <param name="entry">Contains the entry found when the method returns.</param>
    /// <returns>Whether the entry was found in the hash table.</returns>
    public bool TryProbe(ulong key, out HashEntry entry)
    {
        entry = _entries[key % _capacity];
        return entry.Type != HashEntry.Invalid && entry.Key == key;
    }

    /// <summary>
    /// Stores the given entry in the hash table.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    public void Store(HashEntry entry)
    {
        ulong index = entry.Key % _capacity;
        if (_entries[index].Type == HashEntry.Invalid)
            Count++;
        _entries[index] = entry;
    }

    /// <summary>
    /// Clears the hash table of all entries.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_entries, 0, Capacity);
        Count = 0;
    }
}
