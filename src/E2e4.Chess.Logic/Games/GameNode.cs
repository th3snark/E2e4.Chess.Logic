using System;
using System.Collections.Generic;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// One position in a <see cref="GameRecord"/>: the move that reached it (none for the root), the moves that can follow
/// it, and its annotations.
/// </summary>
/// <remarks>
/// <para><see cref="Children"/> is ordered: <c>Children[0]</c> continues the main line and the others are
/// variations. A plain game is a tree without branches.</para>
/// <para>Each node stores its <see cref="Fen"/>, <see cref="ZobristKey"/> and <see cref="San"/>, worked out once
/// when the node is added, so showing any move is a lookup. A playable <see cref="Position"/>, with the history that
/// repetition and the fifty-move rule need, comes from <see cref="ToPosition"/>, which replays the moves from the
/// start. Nodes are not thread-safe.</para>
/// </remarks>
public sealed class GameNode
{
    private readonly List<GameNode> _children = new List<GameNode>();
    private readonly List<int> _nags = new List<int>();
    private string _comment = "";
    private string _preComment = "";

    internal GameNode(GameRecord game, GameNode? parent, int move, string san, string fen, ulong zobristKey, int ply)
    {
        Game = game;
        Parent = parent;
        Move = move;
        San = san;
        Fen = fen;
        ZobristKey = zobristKey;
        Ply = ply;
    }

    /// <summary>The game this node belongs to.</summary>
    public GameRecord Game { get; }

    /// <summary>The node before this one, or null for the root. A removed node keeps its parent.</summary>
    public GameNode? Parent { get; private set; }

    /// <summary>The move that reached this node, or <see cref="Model.Move.Invalid"/> for the root.</summary>
    public int Move { get; }

    /// <summary>The move in standard algebraic notation, with its check or mate mark; empty for the root.</summary>
    public string San { get; }

    /// <summary>The FEN of the position after the move (for the root, the start position).</summary>
    public string Fen { get; }

    /// <summary>The Zobrist key of the position after the move: equal for the same position however it was
    /// reached.</summary>
    public ulong ZobristKey { get; }

    /// <summary>The number of plies from move one to this position (<see cref="Position.HalfMoves"/>).</summary>
    public int Ply { get; }

    /// <summary>The colour to move in this node's position.</summary>
    public int SideToMove => Ply % 2 == 0 ? Colour.White : Colour.Black;

    /// <summary>The move number of the move that reached this node (for the root, of the next move).</summary>
    public int MoveNumber => IsRoot ? Ply / 2 + 1 : (Ply - 1) / 2 + 1;

    /// <summary>Whether this is the root, the start position.</summary>
    public bool IsRoot => Parent == null;

    /// <summary>The moves that follow, the main continuation first.</summary>
    public IReadOnlyList<GameNode> Children => _children;

    /// <summary>The main continuation, or null at the end of a line.</summary>
    public GameNode? Next => _children.Count > 0 ? _children[0] : null;

    /// <summary>Whether this node is on the game's main line: every node from the root to here is its parent's first
    /// child.</summary>
    public bool IsMainLine
    {
        get
        {
            for (GameNode node = this; node.Parent != null; node = node.Parent)
            {
                if (node.Parent._children.Count == 0 || node.Parent._children[0] != node)
                    return false;
            }
            return true;
        }
    }

    /// <summary>Whether the node is still part of its game (false once it, or an ancestor, has been
    /// removed).</summary>
    public bool IsAttached
    {
        get
        {
            GameNode node = this;
            while (node.Parent != null)
            {
                if (!node.Parent._children.Contains(node))
                    return false;
                node = node.Parent;
            }
            return node == Game.Root;
        }
    }

    /// <summary>The comment after the move (for the root, the comment before the first move). Never null; set null
    /// to clear it.</summary>
    public string Comment
    {
        get { return _comment; }
        set { _comment = value ?? ""; }
    }

    /// <summary>The comment before the move, used at the start of a variation. Never null; set null to clear
    /// it.</summary>
    public string PreComment
    {
        get { return _preComment; }
        set { _preComment = value ?? ""; }
    }

    /// <summary>The numeric annotation glyphs of the move, such as 1 for "!" or 4 for "??" (PGN <c>$1</c>,
    /// <c>$4</c>). Values from 0 to 255.</summary>
    public IList<int> Nags => _nags;

    /// <summary>The clock time remaining after the move, if known (PGN <c>[%clk]</c>).</summary>
    public TimeSpan? Clock { get; set; }

    /// <summary>The time taken over the move, if known (PGN <c>[%emt]</c>, FICS move times).</summary>
    public TimeSpan? MoveTime { get; set; }

    /// <summary>
    /// Returns the child reached by the given move, adding it after the existing children when there is none, so the
    /// same move is never stored twice.
    /// </summary>
    /// <param name="move">A move legal in this node's position, such as one from <see cref="Position.LegalMoves()"/>
    /// or <see cref="Model.Move.Find"/>.</param>
    /// <returns>The node after the move.</returns>
    /// <exception cref="ArgumentException">The move is not legal here.</exception>
    /// <exception cref="InvalidOperationException">The game would pass <see cref="Position.HalfMovesLimit"/>
    /// plies.</exception>
    public GameNode AddMove(int move)
    {
        GameNode? existing = FindChild(move);
        if (existing != null)
            return existing;

        Position position = ToPosition();
        if (!position.LegalMoves().Contains(move))
            throw new ArgumentException("The move is not legal in this position.", nameof(move));
        return AddChild(position, move);
    }

    /// <summary>
    /// Adds the move named in algebraic or coordinate notation (see <see cref="Model.Move.FromAlgebraicNotation"/>),
    /// or returns the existing child for it.
    /// </summary>
    /// <param name="notation">The move, such as <c>Nf3</c>, <c>O-O</c> or <c>e2e4</c>.</param>
    /// <param name="node">The node after the move, or null.</param>
    /// <returns>Whether the text named one legal move.</returns>
    public bool TryAddMove(string? notation, out GameNode? node)
    {
        node = null;
        Position position = ToPosition();
        int move = Model.Move.FromAlgebraicNotation(position, notation);
        if (move == Model.Move.Invalid || position.HalfMoves >= Position.HalfMovesLimit - 1)
            return false;
        node = FindChild(move) ?? AddChild(position, move);
        return true;
    }

    /// <summary>
    /// Returns the child reached by the given move, or null.
    /// </summary>
    public GameNode? FindChild(int move)
    {
        foreach (GameNode child in _children)
        {
            if (child.Move == move)
                return child;
        }
        return null;
    }

    /// <summary>
    /// Adds a child for a move legal in <paramref name="position"/>, which must be this node's position. The position is
    /// left after the move.
    /// </summary>
    internal GameNode AddChild(Position position, int move)
    {
        string san = Model.Move.GetAlgebraicNotation(position, move);
        position.Make(move);
        GameNode child = new GameNode(Game, this, move, san, position.GetFEN(), position.ZobristKey, position.HalfMoves);
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Returns a playable position for this node, with the history of the moves that led to it, by replaying them from
    /// the start. Each call returns a new position.
    /// </summary>
    public Position ToPosition()
    {
        Position position = Game.CreateStartPosition();
        List<int> moves = new List<int>();
        for (GameNode node = this; node.Parent != null; node = node.Parent)
            moves.Add(node.Move);
        for (int i = moves.Count - 1; i >= 0; i--)
            position.Make(moves[i]);
        return position;
    }

    /// <summary>
    /// Returns the moves from the root to this node, oldest first.
    /// </summary>
    public List<int> GetMoves()
    {
        List<int> moves = new List<int>();
        for (GameNode node = this; node.Parent != null; node = node.Parent)
            moves.Add(node.Move);
        moves.Reverse();
        return moves;
    }

    /// <summary>
    /// Returns the nodes of the main continuation after this one: <c>Next</c>, its <c>Next</c>, and so on.
    /// </summary>
    public IEnumerable<GameNode> MainLine()
    {
        for (GameNode? node = Next; node != null; node = node.Next)
            yield return node;
    }

    /// <summary>
    /// Returns this node's address: the index of each node among its parent's children, from the root's child down to
    /// this node. The root's path is empty, and a main-line node's path is all zeros. Pass it to
    /// <see cref="GameRecord.NodeAt"/>.
    /// </summary>
    public int[] GetPath()
    {
        List<int> path = new List<int>();
        for (GameNode node = this; node.Parent != null; node = node.Parent)
            path.Add(node.Parent._children.IndexOf(node));
        path.Reverse();
        return path.ToArray();
    }

    /// <summary>
    /// Makes the line through this node the main line: at this node and every ancestor, the node moves to the front of
    /// its parent's children.
    /// </summary>
    public void PromoteToMainLine()
    {
        for (GameNode node = this; node.Parent != null; node = node.Parent)
        {
            List<GameNode> siblings = node.Parent._children;
            int index = siblings.IndexOf(node);
            if (index > 0)
            {
                siblings.RemoveAt(index);
                siblings.Insert(0, node);
            }
        }
    }

    /// <summary>
    /// Moves this node one place towards the front of its parent's children; the first child becomes the main
    /// continuation. Returns false when it is already first, or is the root.
    /// </summary>
    public bool Promote()
    {
        if (Parent == null)
            return false;
        List<GameNode> siblings = Parent._children;
        int index = siblings.IndexOf(this);
        if (index <= 0)
            return false;
        siblings[index] = siblings[index - 1];
        siblings[index - 1] = this;
        return true;
    }

    /// <summary>
    /// Moves this node one place towards the back of its parent's children. Returns false when it is already last, or is
    /// the root.
    /// </summary>
    public bool Demote()
    {
        if (Parent == null)
            return false;
        List<GameNode> siblings = Parent._children;
        int index = siblings.IndexOf(this);
        if (index < 0 || index >= siblings.Count - 1)
            return false;
        siblings[index] = siblings[index + 1];
        siblings[index + 1] = this;
        return true;
    }

    /// <summary>
    /// Removes this node, and everything after it, from its parent. Returns false for the root or a node already
    /// removed.
    /// </summary>
    public bool Remove()
    {
        return Parent != null && Parent._children.Remove(this);
    }

    /// <summary>
    /// Removes every child, ending the line here.
    /// </summary>
    public void ClearChildren()
    {
        _children.Clear();
    }

    /// <summary>
    /// Returns the move number and move, such as <c>12. Nf3</c> or <c>12... Nf6</c>, or an empty string for the root.
    /// </summary>
    public override string ToString()
    {
        if (IsRoot)
            return "";
        return MoveNumber + (SideToMove == Colour.Black ? ". " : "... ") + San;
    }
}
