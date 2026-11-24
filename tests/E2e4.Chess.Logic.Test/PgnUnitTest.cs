using System;
using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Games;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// Reading and writing PGN with <see cref="Pgn"/>, and the comment commands of <see cref="PgnCommand"/>.
/// </summary>
public class PgnUnitTest
{
    // Morphy v Duke Karl / Count Isouard, Paris 1858 (the "Opera Game"): a public game record.
    private const string OperaGame = """
        [Event "Paris"]
        [Site "Paris FRA"]
        [Date "1858.??.??"]
        [Round "?"]
        [White "Paul Morphy"]
        [Black "Duke Karl / Count Isouard"]
        [Result "1-0"]

        1. e4 e5 2. Nf3 d6 3. d4 Bg4 4. dxe5 Bxf3 5. Qxf3 dxe5 6. Bc4 Nf6 7. Qb3 Qe7
        8. Nc3 c6 9. Bg5 b5 10. Nxb5 cxb5 11. Bxb5+ Nbd7 12. O-O-O Rd8 13. Rxd7 Rxd7
        14. Rd1 Qe6 15. Bxd7+ Nxd7 16. Qb8+ Nxb8 17. Rd8# 1-0
        """;

    // A synthetic annotated study: variations (nested, and alternatives to Black's move), comments before and after
    // moves, NAGs written both ways, clock commands and a drawing command that must survive.
    private const string Study = """
        [Event "Training"]
        [White "White"]
        [Black "Black"]
        [Result "*"]
        [Annotator "Coach"]

        {An opening comment.} 1. e4 {[%clk 0:03:00]} e5 $1 (1... c5!? {The Sicilian.} 2. Nf3 (2. c3 d5) 2... d6)
        (1... e6 {[%cal Gd7d5]}) 2. Nf3 Nc6 3. Bb5 a6?! ({Instead} 3... Nf6 4. O-O) 4. Ba4 {[%clk 0:02:55.5] [%emt 0:00:04]} *
        """;

    [Fact]
    public void ReadsAPlainGame()
    {
        Assert.True(Pgn.TryParseGame(OperaGame, out GameRecord? game));

        Assert.Empty(game!.Errors);
        Assert.Equal("Paul Morphy", game.White);
        Assert.Equal("Duke Karl / Count Isouard", game.Black);
        Assert.Equal("1-0", game.Result);
        Assert.Equal(33, game.MainLine().Count());
        GameNode last = game.LastMainLineNode();
        Assert.Equal("Rd8#", last.San);
        Assert.Empty(last.ToPosition().LegalMoves());
        Assert.All(game.MainLine(), node => Assert.Single(node.Parent!.Children));
    }

    [Fact]
    public void ReadsVariationsCommentsNagsAndClocks()
    {
        Assert.True(Pgn.TryParseGame(Study, out GameRecord? game));
        Assert.Empty(game!.Errors);
        Assert.Equal("Coach", game.GetTag("Annotator"));
        Assert.Equal("An opening comment.", game.Root.Comment);

        GameNode e4 = game.Root.Next!;
        Assert.Equal(TimeSpan.FromMinutes(3), e4.Clock);
        Assert.Equal("", e4.Comment);

        Assert.Equal(new[] { "e5", "c5", "e6" }, e4.Children.Select(n => n.San));
        GameNode e5 = e4.Children[0];
        GameNode c5 = e4.Children[1];
        GameNode e6 = e4.Children[2];
        Assert.Equal(new[] { 1 }, e5.Nags);
        Assert.Equal(new[] { 5 }, c5.Nags);
        Assert.Equal("The Sicilian.", c5.Comment);
        Assert.Equal(new[] { "Nf3", "c3" }, c5.Children.Select(n => n.San));
        Assert.Equal("d6", c5.Children[0].Next!.San);
        Assert.Equal("d5", c5.Children[1].Next!.San);
        Assert.Equal("[%cal Gd7d5]", e6.Comment);
        Assert.Equal("Gd7d5", PgnCommand.GetArguments(e6.Comment, "cal"));

        GameNode a6 = game.NodeAt(new[] { 0, 0, 0, 0, 0, 0 })!;
        Assert.Equal("a6", a6.San);
        Assert.Equal(new[] { 6 }, a6.Nags);
        GameNode nf6 = a6.Parent!.Children[1];
        Assert.Equal("Nf6", nf6.San);
        Assert.Equal("Instead", nf6.PreComment);
        Assert.Equal("O-O", nf6.Next!.San);

        GameNode ba4 = game.LastMainLineNode();
        Assert.Equal("Ba4", ba4.San);
        Assert.Equal(TimeSpan.FromSeconds(175.5), ba4.Clock);
        Assert.Equal(TimeSpan.FromSeconds(4), ba4.MoveTime);
        Assert.Equal("", ba4.Comment);
        Assert.Equal(GameRecord.UnknownResult, game.Result);
    }

    [Theory]
    [InlineData(OperaGame)]
    [InlineData(Study)]
    public void WritingAndReadingBackGivesTheSameText(string pgn)
    {
        Assert.True(Pgn.TryParseGame(pgn, out GameRecord? game));

        string written = Pgn.Write(game!);
        Assert.True(Pgn.TryParseGame(written, out GameRecord? again));

        Assert.Empty(again!.Errors);
        Assert.Equal(written, Pgn.Write(again));
        Assert.All(written.Split('\n'), line => Assert.True(line.Length <= 80, line));
        AssertSameTree(game!.Root, again.Root);
    }

    [Fact]
    public void WritesExportFormat()
    {
        Assert.True(Pgn.TryParseGame(Study, out GameRecord? game));

        string written = Pgn.Write(game!);

        string expected = string.Join("\n",
            "[Event \"Training\"]",
            "[Site \"?\"]",
            "[Date \"????.??.??\"]",
            "[Round \"?\"]",
            "[White \"White\"]",
            "[Black \"Black\"]",
            "[Result \"*\"]",
            "[Annotator \"Coach\"]",
            "",
            "{An opening comment.} 1. e4 {[%clk 0:03:00]} 1... e5 $1 (1... c5 $5 {The",
            "Sicilian.} 2. Nf3 (2. c3 d5) 2... d6) (1... e6 {[%cal Gd7d5]}) 2. Nf3 Nc6 3. Bb5",
            "a6 $6 ({Instead} 3... Nf6 4. O-O) 4. Ba4 {[%clk 0:02:55.5] [%emt 0:00:04]} *",
            "");
        Assert.Equal(expected, written);
    }

    [Fact]
    public void WritesAStartPositionAndEscapesTagValues()
    {
        Assert.True(GameRecord.TryCreate("4k3/8/8/8/8/8/4P3/4K3 b - - 0 30", out GameRecord? game));
        game.Event = "Say \"hi\" \\ bye";
        Assert.True(game.Root.TryAddMove("Kd7", out GameNode? kd7));
        kd7!.Comment = "Braces } cannot be escaped";

        string written = Pgn.Write(game);

        Assert.Contains("[Event \"Say \\\"hi\\\" \\\\ bye\"]", written);
        Assert.Contains("[SetUp \"1\"]\n[FEN \"4k3/8/8/8/8/8/4P3/4K3 b - - 0 30\"]", written);
        Assert.Contains("30... Kd7 {Braces cannot be escaped} *", written);
        Assert.True(Pgn.TryParseGame(written, out GameRecord? again));
        Assert.Equal(game.Event, again!.Event);
        Assert.Equal(game.StartFen, again.StartFen);
        Assert.Equal("Kd7", again.Root.Next!.San);
    }

    [Fact]
    public void ReadsSeveralGamesAndKeepsResultsApart()
    {
        string text = OperaGame + "\n\n" + Study + "\n\n" + "1. d4 d5 1/2-1/2\n";

        List<GameRecord> games = Pgn.Parse(text);

        Assert.Equal(3, games.Count);
        Assert.Equal("1-0", games[0].Result);
        Assert.Equal("*", games[1].Result);
        Assert.Equal("1/2-1/2", games[2].Result);
        Assert.Equal(2, games[2].MainLine().Count());
        Assert.All(games, g => Assert.Empty(g.Errors));
        Assert.Equal(3, Pgn.Parse(Pgn.Write(games)).Count);
    }

    [Fact]
    public void AcceptsMoveNumbersGluedToMovesAndTraditionalGlyphs()
    {
        Assert.True(Pgn.TryParseGame("1.e4 e5 2.Nf3!! 2...Nc6 3.Bb5?? ! *", out GameRecord? game));

        Assert.Empty(game!.Errors);
        GameNode[] moves = game.MainLine().ToArray();
        Assert.Equal(new[] { "e4", "e5", "Nf3", "Nc6", "Bb5" }, moves.Select(n => n.San));
        Assert.Equal(new[] { 3 }, moves[2].Nags);
        Assert.Equal(new[] { 4, 1 }, moves[4].Nags);
    }

    [Fact]
    public void ReadsSemicolonAndEscapeLineComments()
    {
        string text = "% an escaped line\n1. e4 ; a rest-of-line comment\ne5 *";

        Assert.True(Pgn.TryParseGame(text, out GameRecord? game));

        Assert.Empty(game!.Errors);
        Assert.Equal("a rest-of-line comment", game.Root.Next!.Comment);
        Assert.Equal("e5", game.Root.Next.Next!.San);
    }

    [Fact]
    public void AnIllegalMoveEndsItsLineButKeepsTheRest()
    {
        string text = "1. e4 e5 (1... c5 2. Ke3 Nc6 (2... d6) 3. Nf3) 2. Nf3 Nc6 3. Qh8 Nf6 *";

        Assert.True(Pgn.TryParseGame(text, out GameRecord? game));

        Assert.Equal(2, game!.Errors.Count);
        Assert.Contains("Ke3", game.Errors[0]);
        Assert.Contains("Qh8", game.Errors[1]);
        Assert.Equal(new[] { "e4", "e5", "Nf3", "Nc6" }, game.MainLine().Select(n => n.San));
        GameNode c5 = game.Root.Next!.Children[1];
        Assert.Equal("c5", c5.San);
        Assert.Empty(c5.Children);
    }

    [Fact]
    public void ABadFenTagSkipsTheMovesAndIsReported()
    {
        Assert.True(Pgn.TryParseGame("[FEN \"nonsense\"]\n1. e4 e5 *", out GameRecord? game));

        Assert.Single(game!.Errors);
        Assert.True(game.HasStandardStart);
        Assert.Empty(game.Root.Children);
    }

    [Fact]
    public void AFenTagSetsTheStartPosition()
    {
        Assert.True(Pgn.TryParseGame("[SetUp \"1\"]\n[FEN \"4k3/8/8/8/8/8/4P3/4K3 w - - 0 1\"]\n\n1. e4 Kd7 *", out GameRecord? game));

        Assert.Empty(game!.Errors);
        Assert.Equal("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1", game.StartFen);
        Assert.Null(game.GetTag("FEN"));
        Assert.Null(game.GetTag("SetUp"));
        Assert.Equal(2, game.MainLine().Count());
    }

    [Theory]
    [InlineData("1. e4 e5", 1, "no result")]
    [InlineData("1. e4 e5 2. Nf3\n[Event \"Next\"]\n1. d4 *", 1, "no result")]
    [InlineData("( 1. e4 ) 1. d4 *", 1, "no move")]
    [InlineData("1. e4 ) e5 *", 1, "no matching")]
    [InlineData("1. e4 -- e5 *", 1, "Null moves")]
    [InlineData("1. e4 $999 e5 *", 1, "NAG")]
    [InlineData("1. e4 e5 ?!? *", 1, "glyph")]
    [InlineData("[Event Paris]\n1. e4 *", 1, "malformed")]
    [InlineData("[Event \"Paris]\n1. e4 *", 1, "not closed")]
    public void ProblemsAreReportedNotThrown(string text, int games, string error)
    {
        List<GameRecord> parsed = Pgn.Parse(text);

        Assert.Equal(games, parsed.Count(g => g.Errors.Count > 0));
        Assert.Contains(parsed.SelectMany(g => g.Errors), e => e.Contains(error, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n ")]
    public void EmptyTextHasNoGames(string? text)
    {
        Assert.Empty(Pgn.Parse(text));
        Assert.False(Pgn.TryParseGame(text, out GameRecord? game));
        Assert.Null(game);
    }

    [Fact]
    public void AnUnclosedVariationOrCommentStillGivesAGame()
    {
        List<GameRecord> games = Pgn.Parse("1. e4 e5 (1... c5 2. Nf3 {never closed");

        GameRecord game = Assert.Single(games);
        Assert.Equal(new[] { "e4", "e5" }, game.MainLine().Select(n => n.San));
        Assert.Equal("never closed", game.Root.Next!.Children[1].Next!.Comment);
    }

    [Fact]
    public void RandomTextNeverThrows()
    {
        Random random = new(20261124);
        string[] pieces = { "1.", "e4", "e5", "Nf3", "Nc6", "(", ")", "{", "}", "[", "]", "\"", "$", "5", ";", "\n", "%",
            "*", "1-0", "O-O", "--", "!", "?", " ", "[%clk 0:01:00]", "Event", "\\", "x", "Bb5", "a6", "..." };
        for (int i = 0; i < 3000; i++)
        {
            int count = random.Next(0, 40);
            string text = string.Concat(Enumerable.Range(0, count).Select(_ => pieces[random.Next(pieces.Length)]));

            List<GameRecord> games = Pgn.Parse(text);
            foreach (GameRecord game in games)
                Assert.NotNull(Pgn.Write(game));
        }
    }

    [Fact]
    public void ALongGameStopsAtThePlyLimitWithAnError()
    {
        string moves = string.Concat(Enumerable.Repeat("Nf3 Nf6 Ng1 Ng8 ", 300));

        Assert.True(Pgn.TryParseGame(moves + "*", out GameRecord? game));

        Assert.Single(game!.Errors);
        Assert.Contains("plies", game.Errors[0]);
        Assert.Equal(Position.HalfMovesLimit - 1, game.LastMainLineNode().Ply);
    }

    [Fact]
    public void PgnCommandsAreFoundAddedAndRemoved()
    {
        const string comment = "Look here [%cal Ge2e4,Rd8d1] and [%csl Gd4] [%bad";

        List<PgnCommand> commands = PgnCommand.Find(comment);

        Assert.Equal(new[] { "cal", "csl" }, commands.Select(c => c.Name));
        Assert.Equal("Ge2e4,Rd8d1", commands[0].Arguments);
        Assert.Equal("[%csl Gd4]", commands[1].ToString());
        Assert.Equal("Look here and [%csl Gd4] [%bad", PgnCommand.Remove(comment, "cal"));
        Assert.Equal("Look here and [%bad [%cal Ge2e4]", PgnCommand.Set(PgnCommand.Remove(comment, "csl"), "cal", "Ge2e4"));
        Assert.Equal("[%clk 0:01:00]", PgnCommand.Set(null, "clk", "0:01:00"));
        Assert.Null(PgnCommand.GetArguments(comment, "clk"));
        Assert.Empty(PgnCommand.Find(null));
        Assert.Empty(PgnCommand.Find("[% x] [%] [%a b c"));
    }

    [Theory]
    [InlineData("0:03:00", 180000)]
    [InlineData("1:00:00", 3600000)]
    [InlineData("2:05", 125000)]
    [InlineData("42", 42000)]
    [InlineData("0:00:01.25", 1250)]
    [InlineData(" 0:00:09 ", 9000)]
    public void ReadsTimes(string text, int milliseconds)
    {
        Assert.True(PgnCommand.TryParseTime(text, out TimeSpan time));
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a:00")]
    [InlineData("-1:00")]
    [InlineData("1:2:3:4")]
    [InlineData("1.5:00")]
    [InlineData("::")]
    [InlineData("99999999999999")]
    public void RejectsBadTimes(string? text)
    {
        Assert.False(PgnCommand.TryParseTime(text, out _));
    }

    [Fact]
    public void FormatsTimes()
    {
        Assert.Equal("0:03:00", PgnCommand.FormatTime(TimeSpan.FromMinutes(3)));
        Assert.Equal("1:02:03", PgnCommand.FormatTime(new TimeSpan(1, 2, 3)));
        Assert.Equal("0:00:01.25", PgnCommand.FormatTime(TimeSpan.FromMilliseconds(1250)));
        Assert.Equal("0:00:00", PgnCommand.FormatTime(TimeSpan.FromSeconds(-5)));
        Assert.Equal("26:00:00", PgnCommand.FormatTime(TimeSpan.FromHours(26)));
    }

    internal static void AssertSameTree(GameNode expected, GameNode actual)
    {
        Assert.Equal(expected.Move, actual.Move);
        Assert.Equal(expected.Fen, actual.Fen);
        Assert.Equal(expected.Comment, actual.Comment);
        Assert.Equal(expected.PreComment, actual.PreComment);
        Assert.Equal(expected.Nags, actual.Nags);
        Assert.Equal(expected.Clock, actual.Clock);
        Assert.Equal(expected.MoveTime, actual.MoveTime);
        Assert.Equal(expected.Children.Count, actual.Children.Count);
        for (int i = 0; i < expected.Children.Count; i++)
            AssertSameTree(expected.Children[i], actual.Children[i]);
    }
}
