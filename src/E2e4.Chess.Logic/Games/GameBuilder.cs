using System;
using System.Collections.Generic;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// Builds a game's move tree from a stream of moves, variation starts and ends, comments and NAGs, as the PGN and
/// binary readers meet them. It keeps one <see cref="Position"/> in step with the current node, so adding a move costs
/// one move generation rather than a replay from the start.
/// </summary>
internal sealed class GameBuilder
{
    private readonly Position _position;
    private readonly Stack<GameNode> _variations = new Stack<GameNode>();
    private string _pendingPreComment = "";
    private bool _atVariationStart;

    public GameBuilder(GameRecord game)
    {
        Game = game;
        _position = game.CreateStartPosition();
        Current = game.Root;
    }

    public GameRecord Game { get; }

    /// <summary>The node the next move follows.</summary>
    public GameNode Current { get; private set; }

    /// <summary>The position at <see cref="Current"/>. Callers only read it.</summary>
    public Position Position => _position;

    /// <summary>How many variations are open.</summary>
    public int Depth => _variations.Count;

    /// <summary>
    /// Adds the move named in algebraic notation after the current node. Returns false, changing nothing, when it is
    /// not exactly one legal move.
    /// </summary>
    public bool TryAddSan(string san)
    {
        int move = Move.FromAlgebraicNotation(_position, san);
        return move != Move.Invalid && TryAdd(move);
    }

    /// <summary>
    /// Adds a move legal in the current position. Returns false at the ply limit.
    /// </summary>
    public bool TryAdd(int move)
    {
        if (_position.HalfMoves >= Position.HalfMovesLimit - 1)
            return false;
        GameNode? existing = Current.FindChild(move);
        if (existing != null)
        {
            _position.Make(move);
            Current = existing;
        }
        else
        {
            Current = Current.AddChild(_position, move);
        }
        if (_pendingPreComment.Length > 0)
        {
            Current.PreComment = Join(Current.PreComment, _pendingPreComment);
            _pendingPreComment = "";
        }
        _atVariationStart = false;
        return true;
    }

    /// <summary>
    /// Starts a variation: an alternative to the current node's move. Returns false at the root, where there is no move
    /// to vary.
    /// </summary>
    public bool StartVariation()
    {
        if (Current.Parent == null)
            return false;
        _variations.Push(Current);
        _position.Unmake(Current.Move);
        Current = Current.Parent;
        _atVariationStart = true;
        _pendingPreComment = "";
        return true;
    }

    /// <summary>
    /// Ends the innermost variation, returning to the move it was an alternative to. Returns false when none is open.
    /// </summary>
    public bool EndVariation()
    {
        if (_variations.Count == 0)
            return false;
        GameNode saved = _variations.Pop();
        while (Current != saved.Parent)
        {
            _position.Unmake(Current.Move);
            Current = Current.Parent!;
        }
        _position.Make(saved.Move);
        Current = saved;
        _atVariationStart = false;
        _pendingPreComment = "";
        return true;
    }

    /// <summary>
    /// Adds a comment: before the next move at the start of a variation, otherwise after the current move (or, before
    /// the first move, as the game's opening comment). <c>[%clk]</c> and <c>[%emt]</c> commands after a move become the
    /// node's times.
    /// </summary>
    public void AddComment(string text)
    {
        string comment = PgnCommand.NormalizeWhiteSpace(text);
        if (comment.Length == 0)
            return;
        if (_atVariationStart)
        {
            _pendingPreComment = Join(_pendingPreComment, comment);
            return;
        }
        if (Current.Parent != null)
        {
            comment = TakeTime(comment, "clk", out TimeSpan? clock);
            if (clock.HasValue)
                Current.Clock = clock;
            comment = TakeTime(comment, "emt", out TimeSpan? moveTime);
            if (moveTime.HasValue)
                Current.MoveTime = moveTime;
        }
        Current.Comment = Join(Current.Comment, comment);
    }

    /// <summary>
    /// Adds a numeric annotation glyph to the current move. Returns false when there is no move to annotate.
    /// </summary>
    public bool AddNag(int nag)
    {
        if (_atVariationStart || Current.Parent == null || nag < 0 || nag > 255)
            return false;
        if (!Current.Nags.Contains(nag))
            Current.Nags.Add(nag);
        return true;
    }

    private static string TakeTime(string comment, string name, out TimeSpan? time)
    {
        time = null;
        string? arguments = PgnCommand.GetArguments(comment, name);
        if (arguments == null || !PgnCommand.TryParseTime(arguments, out TimeSpan value))
            return comment;
        time = value;
        return PgnCommand.Remove(comment, name);
    }

    private static string Join(string first, string second)
    {
        if (first.Length == 0)
            return second;
        if (second.Length == 0)
            return first;
        return first + " " + second;
    }
}
