using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// Reads and writes games in Portable Game Notation (PGN), including variations, comments, NAGs and the
/// <c>[%clk]</c>/<c>[%emt]</c> clock commands. It works on strings only: reading and writing files is left to the
/// caller.
/// </summary>
public static class Pgn
{
    private const int LineWidth = 80;

    #region Reading

    /// <summary>
    /// Reads every game in the text.
    /// </summary>
    /// <remarks>
    /// <para>The reader is lenient and never throws for bad text. A move that is illegal or ambiguous ends its line:
    /// the rest of that variation (or, on the main line, the rest of the game) is skipped and the problem is listed in
    /// <see cref="GameRecord.Errors"/>. A game missing its result ends at the next tag section. Move numbers are not
    /// checked; the moves themselves decide whose turn it is.</para>
    /// <para>The <c>FEN</c> tag sets the start position. <c>[%clk]</c> and <c>[%emt]</c> in a comment after a move
    /// become <see cref="GameNode.Clock"/> and <see cref="GameNode.MoveTime"/>; other comment text is kept with its
    /// white space collapsed. The glyphs <c>!</c>, <c>?</c>, <c>!!</c>, <c>??</c>, <c>!?</c> and <c>?!</c> become NAGs
    /// 1 to 6. Null moves (<c>--</c>) cannot be stored and end their line.</para>
    /// </remarks>
    /// <param name="text">The PGN text; null or empty gives no games.</param>
    /// <returns>The games, in order.</returns>
    public static List<GameRecord> Parse(string? text)
    {
        List<GameRecord> games = new List<GameRecord>();
        if (string.IsNullOrEmpty(text))
            return games;

        Reader reader = new Reader(text!, games);
        reader.Run();
        return games;
    }

    /// <summary>
    /// Reads the first game in the text.
    /// </summary>
    /// <param name="text">The PGN text.</param>
    /// <param name="game">The first game, or null when the text holds none.</param>
    /// <returns>Whether a game was found. It may still have <see cref="GameRecord.Errors"/>.</returns>
    public static bool TryParseGame(string? text, out GameRecord? game)
    {
        List<GameRecord> games = Parse(text);
        game = games.Count > 0 ? games[0] : null;
        return game != null;
    }

    /// <summary>
    /// The state of one pass over the text.
    /// </summary>
    private sealed class Reader
    {
        private readonly string _text;
        private readonly List<GameRecord> _games;
        private int _index;

        // The game being read: its tags until the movetext starts, then its builder.
        private readonly List<KeyValuePair<string, string>> _tags = new List<KeyValuePair<string, string>>();
        private readonly List<string> _tagErrors = new List<string>();
        private GameBuilder? _builder;

        // After a bad move the rest of its line is skipped: -1 when not skipping, otherwise how many variations were
        // open when the line went bad, with _skipNesting counting the parentheses opened inside the skipped text.
        private int _skipDepth = -1;
        private int _skipNesting;

        public Reader(string text, List<GameRecord> games)
        {
            _text = text;
            _games = games;
        }

        public void Run()
        {
            bool lineStart = true;
            while (_index < _text.Length)
            {
                char c = _text[_index];
                if (c == '\n')
                {
                    lineStart = true;
                    _index++;
                    continue;
                }
                if (char.IsWhiteSpace(c) || c == '﻿')
                {
                    if (c != '\r')
                        lineStart = false;
                    _index++;
                    continue;
                }
                if (c == '%' && lineStart)
                {
                    SkipToEndOfLine();
                    continue;
                }
                lineStart = false;

                switch (c)
                {
                    case '[':
                        if (_builder != null)
                            FinishGame(null);
                        ReadTag();
                        break;
                    case '{':
                        ReadBraceComment();
                        break;
                    case ';':
                        int start = ++_index;
                        SkipToEndOfLine();
                        Comment(_text.Substring(start, _index - start));
                        break;
                    case '(':
                        _index++;
                        OpenVariation();
                        break;
                    case ')':
                        _index++;
                        CloseVariation();
                        break;
                    case '$':
                        _index++;
                        ReadNag();
                        break;
                    case ']':
                    case '}':
                    case '.':
                        _index++;
                        break;
                    default:
                        ReadSymbol();
                        break;
                }
            }
            if (_builder != null || _tags.Count > 0)
                FinishGame(null);
        }

        private void SkipToEndOfLine()
        {
            while (_index < _text.Length && _text[_index] != '\n')
                _index++;
        }

        private void ReadTag()
        {
            // [Name "value"], with \" and \\ escapes in the value.
            int lineEnd = _text.IndexOf('\n', _index);
            if (lineEnd < 0)
                lineEnd = _text.Length;
            _index++;
            while (_index < lineEnd && char.IsWhiteSpace(_text[_index]))
                _index++;
            int nameStart = _index;
            while (_index < lineEnd && _text[_index] < 128 && (char.IsLetterOrDigit(_text[_index]) || _text[_index] == '_'))
                _index++;
            string name = _text.Substring(nameStart, _index - nameStart);
            while (_index < lineEnd && char.IsWhiteSpace(_text[_index]))
                _index++;

            if (!GameRecord.IsValidTagName(name) || _index >= lineEnd || _text[_index] != '"')
            {
                _tagErrors.Add("A tag pair is malformed: " + Snippet(nameStart - 1, lineEnd));
                SkipTagRest(lineEnd);
                return;
            }

            _index++;
            StringBuilder value = new StringBuilder();
            bool closed = false;
            while (_index < lineEnd)
            {
                char c = _text[_index++];
                if (c == '\\' && _index < lineEnd && (_text[_index] == '"' || _text[_index] == '\\'))
                {
                    value.Append(_text[_index++]);
                }
                else if (c == '"')
                {
                    closed = true;
                    break;
                }
                else if (c != '\r')
                {
                    value.Append(c);
                }
            }
            if (!closed)
            {
                _tagErrors.Add("The value of tag " + name + " is not closed.");
                _index = lineEnd;
                return;
            }
            SkipTagRest(lineEnd);
            _tags.Add(new KeyValuePair<string, string>(name, value.ToString()));
        }

        private void SkipTagRest(int lineEnd)
        {
            int close = _text.IndexOf(']', _index, lineEnd - _index);
            _index = close >= 0 ? close + 1 : lineEnd;
        }

        private void ReadBraceComment()
        {
            int start = _index + 1;
            int close = _text.IndexOf('}', start);
            int end = close >= 0 ? close : _text.Length;
            _index = close >= 0 ? close + 1 : _text.Length;
            Comment(_text.Substring(start, end - start));
        }

        private void ReadNag()
        {
            int start = _index;
            int nag = 0;
            while (_index < _text.Length && _index - start < 4 && _text[_index] >= '0' && _text[_index] <= '9')
                nag = nag * 10 + (_text[_index++] - '0');
            if (_index == start || nag > 255)
            {
                Error("A NAG is malformed near " + Snippet(start - 1, _index + 1));
                return;
            }
            Nag(nag);
        }

        private void ReadSymbol()
        {
            int start = _index;
            while (_index < _text.Length && !IsDelimiter(_text[_index]))
                _index++;
            if (_index == start)
            {
                _index++;
                return;
            }
            string token = _text.Substring(start, _index - start);

            switch (token)
            {
                case "1-0":
                case "0-1":
                case "1/2-1/2":
                case "*":
                    FinishGame(token);
                    return;
            }

            // A move number ("12." or "12...") can be glued to the move ("12.e4").
            int digits = 0;
            while (digits < token.Length && char.IsDigit(token[digits]))
                digits++;
            if (digits > 0 && (digits == token.Length || token[digits] == '.'))
            {
                int dots = digits;
                while (dots < token.Length && token[dots] == '.')
                    dots++;
                token = token.Substring(dots);
                if (token.Length == 0)
                    return;
            }

            // Annotation glyphs, alone or glued to the move.
            int glyphStart = token.Length;
            while (glyphStart > 0 && (token[glyphStart - 1] == '!' || token[glyphStart - 1] == '?'))
                glyphStart--;
            string glyph = token.Substring(glyphStart);
            string move = token.Substring(0, glyphStart);

            if (move.Length > 0)
                Move(move);
            if (glyph.Length > 0)
            {
                int nag = GlyphToNag(glyph);
                if (nag > 0)
                    Nag(nag);
                else
                    Error("An annotation glyph is not recognised: " + glyph);
            }
        }

        private static bool IsDelimiter(char c)
        {
            switch (c)
            {
                case '[':
                case ']':
                case '{':
                case '}':
                case '(':
                case ')':
                case ';':
                case '$':
                    return true;
                default:
                    return char.IsWhiteSpace(c);
            }
        }

        private GameBuilder Builder()
        {
            if (_builder != null)
                return _builder;

            string? fen = null;
            foreach (KeyValuePair<string, string> tag in _tags)
            {
                if (tag.Key == "FEN")
                    fen = tag.Value;
            }

            if (!GameRecord.TryCreate(fen, out GameRecord? game))
            {
                game = new GameRecord();
                game.AddError("The FEN tag is not a playable position, so the moves were skipped: " + fen);
                _skipDepth = 0;
                _skipNesting = 0;
            }
            foreach (string error in _tagErrors)
                game.AddError(error);
            foreach (KeyValuePair<string, string> tag in _tags)
            {
                if (tag.Key != "FEN" && tag.Key != "SetUp")
                    game.SetTag(tag.Key, tag.Value);
            }
            _builder = new GameBuilder(game);
            return _builder;
        }

        private bool Skipping => _skipDepth >= 0;

        private void Move(string san)
        {
            GameBuilder builder = Builder();
            if (Skipping)
                return;
            if (builder.TryAddSan(san))
                return;

            string where = builder.Current.IsRoot ? "at the start" : "after " + builder.Current;
            if (san == "--" || san == "Z0")
                Error("Null moves cannot be stored; the rest of the line was skipped (" + where + ").");
            else if (builder.Position.HalfMoves >= Model.Position.HalfMovesLimit - 1)
                Error("The game is longer than " + Model.Position.HalfMovesLimit + " plies; the rest of the line was skipped.");
            else
                Error("The move " + san + " is not legal or is ambiguous " + where + "; the rest of the line was skipped.");
            _skipDepth = builder.Depth;
            _skipNesting = 0;
        }

        private void OpenVariation()
        {
            GameBuilder builder = Builder();
            if (Skipping)
            {
                _skipNesting++;
                return;
            }
            if (!builder.StartVariation())
            {
                Error("A variation has no move to be an alternative to; it was skipped.");
                _skipDepth = builder.Depth + 1;
                _skipNesting = 0;
            }
        }

        private void CloseVariation()
        {
            GameBuilder builder = Builder();
            if (Skipping)
            {
                if (_skipNesting > 0)
                {
                    _skipNesting--;
                    return;
                }
                if (_skipDepth == 0)
                    return;
                int depth = _skipDepth;
                _skipDepth = -1;
                // A variation that never opened (one at the root) has nothing to close.
                if (depth > builder.Depth)
                    return;
            }
            if (!builder.EndVariation())
                Error("A ')' has no matching '('.");
        }

        private void Comment(string text)
        {
            GameBuilder builder = Builder();
            if (!Skipping)
                builder.AddComment(text);
        }

        private void Nag(int nag)
        {
            GameBuilder builder = Builder();
            if (!Skipping)
                builder.AddNag(nag);
        }

        private void Error(string message)
        {
            Builder().Game.AddError(message);
        }

        private void FinishGame(string? result)
        {
            GameBuilder builder = Builder();
            GameRecord game = builder.Game;
            if (result != null)
            {
                string? tagged = game.GetTag("Result");
                if (tagged == null || tagged == GameRecord.UnknownResult)
                    game.Result = result;
            }
            else
            {
                game.AddError("The game has no result at the end of its moves.");
            }
            _games.Add(game);

            _builder = null;
            _tags.Clear();
            _tagErrors.Clear();
            _skipDepth = -1;
            _skipNesting = 0;
        }

        private string Snippet(int start, int end)
        {
            start = Math.Max(0, start);
            end = Math.Min(_text.Length, Math.Max(start, end));
            string snippet = _text.Substring(start, Math.Min(end - start, 40)).Trim();
            return snippet;
        }
    }

    /// <summary>
    /// Returns the NAG for a traditional glyph (<c>!</c> = 1 ... <c>?!</c> = 6), or 0.
    /// </summary>
    private static int GlyphToNag(string glyph)
    {
        switch (glyph)
        {
            case "!": return 1;
            case "?": return 2;
            case "!!": return 3;
            case "??": return 4;
            case "!?": return 5;
            case "?!": return 6;
            default: return 0;
        }
    }

    #endregion

    #region Writing

    /// <summary>
    /// Writes a game as PGN in export form: the tag section (Seven Tag Roster first), a blank line, then the movetext
    /// wrapped at 80 characters and ending with the result. Lines end with <c>\n</c>.
    /// </summary>
    /// <remarks>
    /// Comments are written in braces, with any <c>}</c> in them dropped because PGN cannot escape it. Clock and move
    /// times are written as <c>[%clk]</c> and <c>[%emt]</c> commands in the comment after the move. Reading the output
    /// back gives the same game.
    /// </remarks>
    public static string Write(GameRecord game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, string> tag in game.GetExportTags())
        {
            sb.Append('[').Append(tag.Key).Append(" \"");
            foreach (char c in tag.Value)
            {
                if (c == '"' || c == '\\')
                    sb.Append('\\');
                if (c != '\r' && c != '\n')
                    sb.Append(c);
            }
            sb.Append("\"]\n");
        }
        sb.Append('\n');

        List<string> tokens = new List<string>();
        AddComment(tokens, game.Root.Comment);
        WriteLine(tokens, game.Root.Next, needNumber: true);
        string result = game.Result;
        tokens.Add(result == "1-0" || result == "0-1" || result == "1/2-1/2" ? result : GameRecord.UnknownResult);
        Wrap(sb, tokens);
        return sb.ToString();
    }

    /// <summary>
    /// Writes several games, separated by blank lines.
    /// </summary>
    public static string Write(IEnumerable<GameRecord> games)
    {
        if (games == null)
            throw new ArgumentNullException(nameof(games));
        StringBuilder sb = new StringBuilder();
        foreach (GameRecord game in games)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(Write(game));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes a line of play starting at <paramref name="node"/>: each move with its annotations, followed by the
    /// variations that are alternatives to it.
    /// </summary>
    private static void WriteLine(List<string> tokens, GameNode? node, bool needNumber)
    {
        for (; node != null; node = node.Next)
        {
            GameNode parent = node.Parent!;
            if (node.PreComment.Length > 0)
            {
                AddComment(tokens, node.PreComment);
                needNumber = true;
            }

            if (parent.SideToMove == Model.Colour.White)
                tokens.Add(node.MoveNumber.ToString(CultureInfo.InvariantCulture) + ".");
            else if (needNumber)
                tokens.Add(node.MoveNumber.ToString(CultureInfo.InvariantCulture) + "...");
            tokens.Add(node.San);
            needNumber = false;

            foreach (int nag in node.Nags)
                tokens.Add("$" + nag.ToString(CultureInfo.InvariantCulture));

            string comment = node.Comment;
            if (node.Clock.HasValue)
                comment = PgnCommand.Set(comment, "clk", PgnCommand.FormatTime(node.Clock.Value));
            if (node.MoveTime.HasValue)
                comment = PgnCommand.Set(comment, "emt", PgnCommand.FormatTime(node.MoveTime.Value));
            if (AddComment(tokens, comment))
                needNumber = true;

            if (parent.Children.Count > 1 && parent.Children[0] == node)
            {
                for (int i = 1; i < parent.Children.Count; i++)
                {
                    tokens.Add("(");
                    WriteLine(tokens, parent.Children[i], needNumber: true);
                    tokens.Add(")");
                }
                needNumber = true;
            }
        }
    }

    /// <summary>
    /// Adds a brace comment as one token per word, so long comments wrap. Returns false for an empty comment.
    /// </summary>
    private static bool AddComment(List<string> tokens, string comment)
    {
        string text = PgnCommand.NormalizeWhiteSpace(comment.Replace("}", ""));
        if (text.Length == 0)
            return false;
        string[] words = text.Split(' ');
        words[0] = "{" + words[0];
        words[words.Length - 1] += "}";
        tokens.AddRange(words);
        return true;
    }

    private static void Wrap(StringBuilder sb, List<string> tokens)
    {
        int lineLength = 0;
        string previous = "";
        foreach (string token in tokens)
        {
            bool space = lineLength > 0 && previous != "(" && token != ")";
            int needed = token.Length + (space ? 1 : 0);
            if (lineLength > 0 && lineLength + needed > LineWidth)
            {
                sb.Append('\n');
                lineLength = 0;
                space = false;
                needed = token.Length;
            }
            if (space)
                sb.Append(' ');
            sb.Append(token);
            lineLength += needed;
            previous = token;
        }
        sb.Append('\n');
    }

    #endregion
}
