using System;
using System.Collections.Generic;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// An in-memory opening index: for every position in the games added, the moves played from it, how often, and how
/// those games ended. It is keyed by position, so transpositions (the same position reached by different move orders)
/// share one entry, unlike a <see cref="GameRecord"/>, which keeps move order.
/// </summary>
/// <remarks>
/// <para>Positions are matched by <see cref="Position.ZobristKey"/>, which includes the side to move, castling rights
/// and the en passant square after any double pawn push (even one no pawn can capture), so positions that differ only
/// in those are kept apart. A game counts once for each position and move it contains, however often it repeats
/// them.</para>
/// <para>This index uses this library's own keys; reading Polyglot opening books needs Polyglot's keys and is not
/// supported. Instances are not thread-safe.</para>
/// </remarks>
public sealed class PositionIndex
{
    private readonly Dictionary<ulong, List<PositionIndexMove>> _entries = new Dictionary<ulong, List<PositionIndexMove>>();

    /// <summary>The number of games added.</summary>
    public int GameCount { get; private set; }

    /// <summary>The number of distinct positions that have at least one move recorded.</summary>
    public int PositionCount => _entries.Count;

    /// <summary>
    /// Adds a game's moves to the index.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="includeVariations">Whether to add the moves of its variations too, not just the main line. Every
    /// line then counts towards the game's result.</param>
    public void Add(GameRecord game, bool includeVariations = false)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        int outcome = OutcomeOf(game.Result);
        HashSet<KeyValuePair<ulong, int>> seen = new HashSet<KeyValuePair<ulong, int>>();
        Stack<GameNode> pending = new Stack<GameNode>();
        pending.Push(game.Root);
        while (pending.Count > 0)
        {
            GameNode node = pending.Pop();
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                if (i > 0 && !includeVariations)
                    continue;
                GameNode child = node.Children[i];
                if (seen.Add(new KeyValuePair<ulong, int>(node.ZobristKey, child.Move)))
                    Record(node.ZobristKey, child, outcome);
                pending.Push(child);
            }
        }
        GameCount++;
    }

    /// <summary>
    /// Returns the moves recorded from the given position, most played first. The list is empty for a position the
    /// index has not seen.
    /// </summary>
    public IReadOnlyList<PositionIndexMove> Lookup(Position position)
    {
        if (position == null)
            throw new ArgumentNullException(nameof(position));
        return Lookup(position.ZobristKey);
    }

    /// <summary>
    /// Returns the moves recorded from the position with the given Zobrist key (such as
    /// <see cref="GameNode.ZobristKey"/>), most played first.
    /// </summary>
    public IReadOnlyList<PositionIndexMove> Lookup(ulong zobristKey)
    {
        if (!_entries.TryGetValue(zobristKey, out List<PositionIndexMove>? moves))
            return new List<PositionIndexMove>();
        List<PositionIndexMove> sorted = new List<PositionIndexMove>(moves);
        sorted.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : string.CompareOrdinal(a.San, b.San));
        return sorted;
    }

    private void Record(ulong key, GameNode child, int outcome)
    {
        if (!_entries.TryGetValue(key, out List<PositionIndexMove>? moves))
        {
            moves = new List<PositionIndexMove>();
            _entries.Add(key, moves);
        }
        PositionIndexMove? entry = null;
        foreach (PositionIndexMove candidate in moves)
        {
            if (candidate.Move == child.Move)
            {
                entry = candidate;
                break;
            }
        }
        if (entry == null)
        {
            entry = new PositionIndexMove(child.Move, child.San);
            moves.Add(entry);
        }
        entry.Count++;
        if (outcome == 1)
            entry.WhiteWins++;
        else if (outcome == -1)
            entry.BlackWins++;
        else if (outcome == 0)
            entry.Draws++;
    }

    /// <summary>
    /// Returns 1 for a White win, -1 for a Black win, 0 for a draw, and 2 for an unknown result.
    /// </summary>
    private static int OutcomeOf(string result)
    {
        switch (result)
        {
            case "1-0": return 1;
            case "0-1": return -1;
            case "1/2-1/2": return 0;
            default: return 2;
        }
    }
}

/// <summary>
/// A move recorded in a <see cref="PositionIndex"/> and the games that played it.
/// </summary>
public sealed class PositionIndexMove
{
    internal PositionIndexMove(int move, string san)
    {
        Move = move;
        San = san;
    }

    /// <summary>The move.</summary>
    public int Move { get; }

    /// <summary>The move in standard algebraic notation, as first recorded.</summary>
    public string San { get; }

    /// <summary>How many games played the move from this position.</summary>
    public int Count { get; internal set; }

    /// <summary>How many of those games White won.</summary>
    public int WhiteWins { get; internal set; }

    /// <summary>How many of those games were drawn.</summary>
    public int Draws { get; internal set; }

    /// <summary>How many of those games Black won.</summary>
    public int BlackWins { get; internal set; }
}
