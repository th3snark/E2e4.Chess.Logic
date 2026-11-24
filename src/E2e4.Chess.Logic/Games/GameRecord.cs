using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// A whole game: its tags (players, event, result, ...), its start position, and a tree of moves with variations
/// and annotations hanging from <see cref="Root"/>. Read and write it as PGN with <see cref="Pgn"/>.
/// </summary>
/// <remarks>
/// The start position is fixed when the game is created; the PGN <c>FEN</c> and <c>SetUp</c> tags are derived from it
/// and cannot be set as tags. Instances are not thread-safe.
/// </remarks>
public sealed class GameRecord
{
    /// <summary>The value of the Result tag for a game still in progress or of unknown result.</summary>
    public const string UnknownResult = "*";

    private static readonly string[] SevenTagRoster = { "Event", "Site", "Date", "Round", "White", "Black", "Result" };

    private readonly List<KeyValuePair<string, string>> _tags = new List<KeyValuePair<string, string>>();
    private readonly PositionSetup _start;
    private readonly List<string> _errors = new List<string>();

    /// <summary>
    /// Creates a game from the standard start position.
    /// </summary>
    public GameRecord()
        : this(PositionSetup.Standard())
    {
    }

    private GameRecord(PositionSetup start)
    {
        _start = start;
        Position position = CreateStartPosition();
        StartFen = position.GetFEN();
        Root = new GameNode(this, null, Move.Invalid, "", StartFen, position.ZobristKey, position.HalfMoves);
    }

    /// <summary>
    /// Creates a game that starts from the position the given FEN describes.
    /// </summary>
    /// <param name="startFen">The start position; null or empty means the standard start.</param>
    /// <param name="game">The game, or null when the FEN is not a playable position (see
    /// <see cref="Position.TryParse"/>).</param>
    /// <returns>Whether the FEN was playable.</returns>
    public static bool TryCreate(string? startFen, [NotNullWhen(true)] out GameRecord? game)
    {
        game = null;
        if (string.IsNullOrEmpty(startFen))
        {
            game = new GameRecord();
            return true;
        }
        if (!PositionSetup.TryParse(startFen, out PositionSetup? setup) || setup.ToPosition() == null)
            return false;
        game = new GameRecord(setup);
        return true;
    }

    /// <summary>The start position: the root node, whose children are the first moves.</summary>
    public GameNode Root { get; }

    /// <summary>The FEN of the start position.</summary>
    public string StartFen { get; }

    /// <summary>Whether the game starts from the standard start position.</summary>
    public bool HasStandardStart => StartFen == Position.StartingFEN;

    /// <summary>
    /// The tags in their order, without <c>FEN</c> and <c>SetUp</c> (which come from the start position).
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Tags => _tags;

    /// <summary>
    /// Problems found while the game was read (an illegal move, a bad tag, ...). Each one dropped some of the text;
    /// the rest of the game was kept. Empty for a game built in code.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>The result, from the <c>Result</c> tag: <c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c> or
    /// <see cref="UnknownResult"/> (the default).</summary>
    public string Result
    {
        get { return GetTag("Result") ?? UnknownResult; }
        set { SetTag("Result", string.IsNullOrEmpty(value) ? UnknownResult : value); }
    }

    /// <summary>The White player, from the <c>White</c> tag, or null.</summary>
    public string? White
    {
        get { return GetTag("White"); }
        set { SetOrRemoveTag("White", value); }
    }

    /// <summary>The Black player, from the <c>Black</c> tag, or null.</summary>
    public string? Black
    {
        get { return GetTag("Black"); }
        set { SetOrRemoveTag("Black", value); }
    }

    /// <summary>The event, from the <c>Event</c> tag, or null.</summary>
    public string? Event
    {
        get { return GetTag("Event"); }
        set { SetOrRemoveTag("Event", value); }
    }

    /// <summary>The site, from the <c>Site</c> tag, or null.</summary>
    public string? Site
    {
        get { return GetTag("Site"); }
        set { SetOrRemoveTag("Site", value); }
    }

    /// <summary>The date, from the <c>Date</c> tag (PGN form <c>YYYY.MM.DD</c>, with <c>??</c> for unknown parts), or
    /// null.</summary>
    public string? Date
    {
        get { return GetTag("Date"); }
        set { SetOrRemoveTag("Date", value); }
    }

    /// <summary>The round, from the <c>Round</c> tag, or null.</summary>
    public string? Round
    {
        get { return GetTag("Round"); }
        set { SetOrRemoveTag("Round", value); }
    }

    /// <summary>
    /// Returns the value of the tag with the given name (names are case-sensitive, as in PGN), or null.
    /// </summary>
    public string? GetTag(string name)
    {
        foreach (KeyValuePair<string, string> tag in _tags)
        {
            if (tag.Key == name)
                return tag.Value;
        }
        return null;
    }

    /// <summary>
    /// Sets a tag, replacing any tag of the same name in place, or adding it at the end.
    /// </summary>
    /// <param name="name">The tag name: letters, digits and underscores, starting with a letter or digit.</param>
    /// <param name="value">The value; null is stored as empty.</param>
    /// <exception cref="ArgumentException">The name is not a valid tag name, or is <c>FEN</c> or <c>SetUp</c>, which
    /// follow the start position.</exception>
    public void SetTag(string name, string? value)
    {
        if (!IsValidTagName(name))
            throw new ArgumentException("A tag name holds letters, digits and underscores, starting with a letter or digit.", nameof(name));
        if (name == "FEN" || name == "SetUp")
            throw new ArgumentException("The FEN and SetUp tags follow the start position; create the game with TryCreate instead.", nameof(name));

        KeyValuePair<string, string> tag = new KeyValuePair<string, string>(name, value ?? "");
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Key == name)
            {
                _tags[i] = tag;
                return;
            }
        }
        _tags.Add(tag);
    }

    /// <summary>
    /// Removes the tag with the given name. Returns whether there was one.
    /// </summary>
    public bool RemoveTag(string name)
    {
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Key == name)
            {
                _tags.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the tags in PGN export order: the Seven Tag Roster first (with <c>?</c> for any missing, as the PGN
    /// standard asks), then <c>SetUp</c> and <c>FEN</c> when the game does not start from the standard position, then
    /// the other tags in their order.
    /// </summary>
    public List<KeyValuePair<string, string>> GetExportTags()
    {
        List<KeyValuePair<string, string>> tags = new List<KeyValuePair<string, string>>();
        foreach (string name in SevenTagRoster)
        {
            string? value = GetTag(name);
            if (value == null)
                value = name == "Date" ? "????.??.??" : name == "Result" ? UnknownResult : "?";
            tags.Add(new KeyValuePair<string, string>(name, value));
        }
        if (!HasStandardStart)
        {
            tags.Add(new KeyValuePair<string, string>("SetUp", "1"));
            tags.Add(new KeyValuePair<string, string>("FEN", StartFen));
        }
        foreach (KeyValuePair<string, string> tag in _tags)
        {
            if (Array.IndexOf(SevenTagRoster, tag.Key) < 0)
                tags.Add(tag);
        }
        return tags;
    }

    /// <summary>
    /// Returns the nodes of the main line, from the first move to the last.
    /// </summary>
    public IEnumerable<GameNode> MainLine()
    {
        return Root.MainLine();
    }

    /// <summary>
    /// Returns the last node of the main line (the root when there are no moves).
    /// </summary>
    public GameNode LastMainLineNode()
    {
        GameNode node = Root;
        while (node.Next != null)
            node = node.Next;
        return node;
    }

    /// <summary>
    /// Returns the node at the given path (see <see cref="GameNode.GetPath"/>), or null when the path leads nowhere.
    /// </summary>
    public GameNode? NodeAt(IReadOnlyList<int> path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        GameNode node = Root;
        foreach (int index in path)
        {
            if (index < 0 || index >= node.Children.Count)
                return null;
            node = node.Children[index];
        }
        return node;
    }

    /// <summary>
    /// Returns a new playable position at the start of the game.
    /// </summary>
    public Position CreateStartPosition()
    {
        // The setup was validated when the game was created, so it always gives a position.
        return _start.ToPosition()!;
    }

    internal void AddError(string error)
    {
        _errors.Add(error);
    }

    private void SetOrRemoveTag(string name, string? value)
    {
        if (value == null)
            RemoveTag(name);
        else
            SetTag(name, value);
    }

    internal static bool IsValidTagName(string? name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsLetterOrDigit(name![0]))
            return false;
        foreach (char c in name)
        {
            if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '_')))
                return false;
        }
        return true;
    }
}
