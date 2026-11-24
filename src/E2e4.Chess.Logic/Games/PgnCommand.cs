using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace E2e4.Chess.Logic.Games;

/// <summary>
/// A command embedded in a PGN comment, written <c>[%name arguments]</c>: for example <c>[%clk 0:02:31]</c>,
/// <c>[%cal Ge2e4,Rd8d1]</c> or <c>[%csl Gd4]</c>.
/// </summary>
/// <remarks>
/// This library reads <c>%clk</c> and <c>%emt</c> into <see cref="GameNode.Clock"/> and
/// <see cref="GameNode.MoveTime"/>; every other command stays in the comment text, and these helpers find, add and
/// remove them there. Drawing <c>%cal</c> arrows and <c>%csl</c> squares is left to the user interface.
/// </remarks>
public sealed class PgnCommand
{
    /// <summary>
    /// Creates a command.
    /// </summary>
    /// <param name="name">The name without the <c>%</c>, such as <c>cal</c>.</param>
    /// <param name="arguments">The arguments, such as <c>Ge2e4,Rd8d1</c>.</param>
    public PgnCommand(string name, string arguments)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Arguments = arguments ?? "";
    }

    /// <summary>The name without the <c>%</c>, such as <c>clk</c>.</summary>
    public string Name { get; }

    /// <summary>The text after the name, trimmed.</summary>
    public string Arguments { get; }

    /// <summary>
    /// Returns the command as written in a comment, such as <c>[%clk 0:02:31]</c>.
    /// </summary>
    public override string ToString()
    {
        return Arguments.Length == 0 ? "[%" + Name + "]" : "[%" + Name + " " + Arguments + "]";
    }

    /// <summary>
    /// Returns the commands in a comment, in order. Text that is not a well-formed command is ignored.
    /// </summary>
    public static List<PgnCommand> Find(string? comment)
    {
        List<PgnCommand> commands = new List<PgnCommand>();
        if (comment == null)
            return commands;
        int index = 0;
        while (TryFindNext(comment, ref index, out _, out PgnCommand? command))
            commands.Add(command!);
        return commands;
    }

    /// <summary>
    /// Returns the arguments of the first command with the given name, or null.
    /// </summary>
    public static string? GetArguments(string? comment, string name)
    {
        foreach (PgnCommand command in Find(comment))
        {
            if (command.Name == name)
                return command.Arguments;
        }
        return null;
    }

    /// <summary>
    /// Returns the comment without any command of the given name, with white space tidied where they were.
    /// </summary>
    public static string Remove(string? comment, string name)
    {
        if (string.IsNullOrEmpty(comment))
            return "";
        StringBuilder sb = new StringBuilder(comment!.Length);
        int index = 0;
        int copied = 0;
        while (TryFindNext(comment, ref index, out int start, out PgnCommand? command))
        {
            if (command!.Name != name)
                continue;
            sb.Append(comment, copied, start - copied);
            copied = index;
        }
        sb.Append(comment, copied, comment.Length - copied);
        return NormalizeWhiteSpace(sb.ToString());
    }

    /// <summary>
    /// Returns the comment with its commands of the given name replaced by one with the given arguments, added at the
    /// end of the text.
    /// </summary>
    public static string Set(string? comment, string name, string arguments)
    {
        string text = Remove(comment, name);
        string command = new PgnCommand(name, arguments).ToString();
        return text.Length == 0 ? command : text + " " + command;
    }

    /// <summary>
    /// Formats a time as PGN clock commands write it: <c>h:mm:ss</c>, with up to three decimals when it has
    /// fractions of a second.
    /// </summary>
    public static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        long hours = (long)time.TotalHours;
        string text = hours.ToString(CultureInfo.InvariantCulture) + ":" + time.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + ":" + time.Seconds.ToString("00", CultureInfo.InvariantCulture);
        if (time.Milliseconds != 0)
            text += "." + time.Milliseconds.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
        return text;
    }

    /// <summary>
    /// Reads a time written <c>h:mm:ss</c>, <c>m:ss</c> or <c>s</c>, each optionally with decimals of a second.
    /// </summary>
    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrEmpty(text))
            return false;
        string[] parts = text!.Trim().Split(':');
        if (parts.Length > 3)
            return false;
        double seconds = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            bool last = i == parts.Length - 1;
            string part = parts[i];
            if (part.Length == 0 || part.Length > 12 || part[0] == '-' || part[0] == '+')
                return false;
            if (!double.TryParse(part, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value))
                return false;
            if (!last && value != Math.Floor(value))
                return false;
            seconds = seconds * 60 + value;
        }
        if (seconds > TimeSpan.MaxValue.TotalSeconds / 2)
            return false;
        time = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
        return true;
    }

    /// <summary>
    /// Collapses runs of white space to single spaces and trims the ends.
    /// </summary>
    internal static string NormalizeWhiteSpace(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (space)
                sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool IsCommandName(string name)
    {
        foreach (char c in name)
        {
            if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '_')))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Finds the next command at or after <paramref name="index"/>; on success, <paramref name="start"/> is where it
    /// begins and <paramref name="index"/> is just after it.
    /// </summary>
    private static bool TryFindNext(string comment, ref int index, out int start, out PgnCommand? command)
    {
        command = null;
        start = -1;
        while (index < comment.Length)
        {
            int open = comment.IndexOf("[%", index, StringComparison.Ordinal);
            if (open < 0)
                break;
            int close = comment.IndexOf(']', open + 2);
            if (close < 0)
                break;

            int nameEnd = open + 2;
            while (nameEnd < close && !char.IsWhiteSpace(comment[nameEnd]))
                nameEnd++;
            string name = comment.Substring(open + 2, nameEnd - open - 2);
            if (name.Length == 0 || !IsCommandName(name))
            {
                index = open + 2;
                continue;
            }
            command = new PgnCommand(name, comment.Substring(nameEnd, close - nameEnd).Trim());
            start = open;
            index = close + 1;
            return true;
        }
        index = comment.Length;
        return false;
    }
}
