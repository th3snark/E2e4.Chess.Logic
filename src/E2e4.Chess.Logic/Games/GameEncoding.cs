using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using E2e4.Chess.Logic.Model;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// A compact binary form of a <see cref="GameRecord"/>, for storing many games: two bytes per move, plus tokens for
/// variations, comments, NAGs and times. PGN stays the form for exchanging games.
/// </summary>
/// <remarks>
/// <para>Layout (little-endian): the bytes <c>E2G</c> and a version byte; a flags byte (bit 0: a start FEN follows);
/// the start FEN; the tag count and each tag's name and value; then a stream of 16-bit units ending with an end token.
/// A move unit is <c>from | to &lt;&lt; 6 | promotion &lt;&lt; 12</c> with squares 0 (a8) to 63 (h1) and promotion 0
/// none, 1 knight, 2 bishop, 3 rook, 4 queen; castling is the king's two squares. Units from <c>0xF000</c> are tokens.
/// Strings are a variable-length byte count followed by UTF-8; times are milliseconds as a variable-length
/// number.</para>
/// <para>The tree is written in PGN order: each move, its annotations, then the variations that are alternatives to
/// it. Reading checks every move against the rules, and bad data returns false rather than throwing.</para>
/// </remarks>
public static class GameEncoding
{
    private const byte Version = 1;

    private const int EndGame = 0xF000;
    private const int StartVariation = 0xF001;
    private const int EndVariation = 0xF002;
    private const int CommentToken = 0xF003;
    private const int PreCommentToken = 0xF004;
    private const int NagToken = 0xF005;
    private const int ClockToken = 0xF006;
    private const int MoveTimeToken = 0xF007;

    /// <summary>
    /// Encodes the game.
    /// </summary>
    public static byte[] Encode(GameRecord game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        List<byte> bytes = new List<byte>(64 + 2 * game.Root.Children.Count);
        bytes.Add((byte)'E');
        bytes.Add((byte)'2');
        bytes.Add((byte)'G');
        bytes.Add(Version);
        bytes.Add(game.HasStandardStart ? (byte)0 : (byte)1);
        if (!game.HasStandardStart)
            WriteString(bytes, game.StartFen);
        WriteNumber(bytes, (ulong)game.Tags.Count);
        foreach (KeyValuePair<string, string> tag in game.Tags)
        {
            WriteString(bytes, tag.Key);
            WriteString(bytes, tag.Value);
        }

        if (game.Root.Comment.Length > 0)
        {
            WriteUnit(bytes, CommentToken);
            WriteString(bytes, game.Root.Comment);
        }
        WriteLine(bytes, game.Root.Next);
        WriteUnit(bytes, EndGame);
        return bytes.ToArray();
    }

    private static void WriteLine(List<byte> bytes, GameNode? node)
    {
        for (; node != null; node = node.Next)
        {
            if (node.PreComment.Length > 0)
            {
                WriteUnit(bytes, PreCommentToken);
                WriteString(bytes, node.PreComment);
            }
            WriteUnit(bytes, EncodeMove(node.Move));
            foreach (int nag in node.Nags)
            {
                WriteUnit(bytes, NagToken);
                bytes.Add((byte)nag);
            }
            if (node.Comment.Length > 0)
            {
                WriteUnit(bytes, CommentToken);
                WriteString(bytes, node.Comment);
            }
            if (node.Clock.HasValue)
            {
                WriteUnit(bytes, ClockToken);
                WriteNumber(bytes, Milliseconds(node.Clock.Value));
            }
            if (node.MoveTime.HasValue)
            {
                WriteUnit(bytes, MoveTimeToken);
                WriteNumber(bytes, Milliseconds(node.MoveTime.Value));
            }

            GameNode parent = node.Parent!;
            if (parent.Children.Count > 1 && parent.Children[0] == node)
            {
                for (int i = 1; i < parent.Children.Count; i++)
                {
                    WriteUnit(bytes, StartVariation);
                    WriteLine(bytes, parent.Children[i]);
                    WriteUnit(bytes, EndVariation);
                }
            }
        }
    }

    private static int EncodeMove(int move)
    {
        int promotion = 0;
        if (Move.IsPromotion(move))
        {
            switch (Move.SpecialPiece(move) & Piece.Mask)
            {
                case Piece.Knight: promotion = 1; break;
                case Piece.Bishop: promotion = 2; break;
                case Piece.Rook: promotion = 3; break;
                default: promotion = 4; break;
            }
        }
        return Move.From(move) | (Move.To(move) << 6) | (promotion << 12);
    }

    private static ulong Milliseconds(TimeSpan time)
    {
        return time <= TimeSpan.Zero ? 0UL : (ulong)(time.Ticks / TimeSpan.TicksPerMillisecond);
    }

    private static void WriteUnit(List<byte> bytes, int unit)
    {
        bytes.Add((byte)unit);
        bytes.Add((byte)(unit >> 8));
    }

    private static void WriteNumber(List<byte> bytes, ulong value)
    {
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }
        bytes.Add((byte)value);
    }

    private static void WriteString(List<byte> bytes, string text)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        WriteNumber(bytes, (ulong)utf8.Length);
        bytes.AddRange(utf8);
    }

    /// <summary>
    /// Decodes a game written by <see cref="Encode"/>.
    /// </summary>
    /// <param name="data">The encoded game.</param>
    /// <param name="game">The game, or null when the data is malformed or holds an illegal move.</param>
    /// <returns>Whether the data was a valid game.</returns>
    public static bool TryDecode(byte[]? data, [NotNullWhen(true)] out GameRecord? game)
    {
        game = null;
        if (data == null)
            return false;
        Decoder decoder = new Decoder(data);
        return decoder.TryRead(out game);
    }

    private sealed class Decoder
    {
        private readonly byte[] _data;
        private int _index;

        public Decoder(byte[] data)
        {
            _data = data;
        }

        public bool TryRead([NotNullWhen(true)] out GameRecord? game)
        {
            game = null;
            if (_data.Length < 6 || _data[0] != 'E' || _data[1] != '2' || _data[2] != 'G' || _data[3] != Version)
                return false;
            _index = 4;
            byte flags = _data[_index++];
            if ((flags & ~1) != 0)
                return false;

            string? fen = null;
            if ((flags & 1) != 0 && !TryReadString(out fen))
                return false;
            if (!GameRecord.TryCreate(fen, out GameRecord? created))
                return false;

            if (!TryReadNumber(out ulong tagCount) || tagCount > (ulong)(_data.Length - _index))
                return false;
            for (ulong t = 0; t < tagCount; t++)
            {
                if (!TryReadString(out string? name) || !TryReadString(out string? value))
                    return false;
                if (!GameRecord.IsValidTagName(name) || name == "FEN" || name == "SetUp")
                    return false;
                created.SetTag(name!, value);
            }

            GameBuilder builder = new GameBuilder(created);
            string pendingPreComment = "";
            while (true)
            {
                if (!TryReadUnit(out int unit))
                    return false;
                switch (unit)
                {
                    case EndGame:
                        if (_index != _data.Length || builder.Depth != 0 || pendingPreComment.Length > 0)
                            return false;
                        game = created;
                        return true;
                    case StartVariation:
                        if (!builder.StartVariation())
                            return false;
                        break;
                    case EndVariation:
                        if (!builder.EndVariation())
                            return false;
                        break;
                    case PreCommentToken:
                        if (!TryReadString(out string? preComment) || pendingPreComment.Length > 0)
                            return false;
                        pendingPreComment = preComment!;
                        break;
                    case CommentToken:
                        if (!TryReadString(out string? comment) || pendingPreComment.Length > 0)
                            return false;
                        builder.Current.Comment = comment;
                        break;
                    case NagToken:
                        if (_index >= _data.Length || builder.Current.IsRoot)
                            return false;
                        builder.Current.Nags.Add(_data[_index++]);
                        break;
                    case ClockToken:
                    case MoveTimeToken:
                        if (!TryReadNumber(out ulong ms) || ms > (ulong)TimeSpan.MaxValue.TotalMilliseconds / 2 || builder.Current.IsRoot)
                            return false;
                        TimeSpan time = TimeSpan.FromMilliseconds(ms);
                        if (unit == ClockToken)
                            builder.Current.Clock = time;
                        else
                            builder.Current.MoveTime = time;
                        break;
                    default:
                        if (unit > 0x4FFF || !TryAddMove(builder, unit))
                            return false;
                        if (pendingPreComment.Length > 0)
                        {
                            builder.Current.PreComment = pendingPreComment;
                            pendingPreComment = "";
                        }
                        break;
                }
            }
        }

        private static bool TryAddMove(GameBuilder builder, int unit)
        {
            int from = unit & 63;
            int to = (unit >> 6) & 63;
            int promotion;
            switch (unit >> 12)
            {
                case 0: promotion = Piece.Empty; break;
                case 1: promotion = Piece.Knight; break;
                case 2: promotion = Piece.Bishop; break;
                case 3: promotion = Piece.Rook; break;
                case 4: promotion = Piece.Queen; break;
                default: return false;
            }
            int move = Move.Find(builder.Position, from, to, promotion);
            if (move == Move.Invalid || (promotion != Piece.Empty) != Move.IsPromotion(move))
                return false;
            return builder.TryAdd(move);
        }

        private bool TryReadUnit(out int unit)
        {
            unit = 0;
            if (_index + 2 > _data.Length)
                return false;
            unit = _data[_index] | (_data[_index + 1] << 8);
            _index += 2;
            return true;
        }

        private bool TryReadNumber(out ulong value)
        {
            value = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                if (_index >= _data.Length)
                    return false;
                byte b = _data[_index++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    return true;
            }
            return false;
        }

        private bool TryReadString([NotNullWhen(true)] out string? text)
        {
            text = null;
            if (!TryReadNumber(out ulong length) || length > (ulong)(_data.Length - _index))
                return false;
            text = Encoding.UTF8.GetString(_data, _index, (int)length);
            _index += (int)length;
            return true;
        }
    }
}
