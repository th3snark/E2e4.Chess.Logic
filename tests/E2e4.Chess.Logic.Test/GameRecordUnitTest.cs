using System;
using System.Collections.Generic;
using System.Linq;
using E2e4.Chess.Logic.Games;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// The game record tree: <see cref="GameRecord"/> and <see cref="GameNode"/>.
/// </summary>
public class GameRecordUnitTest
{
    private static GameNode Play(GameNode node, params string[] moves)
    {
        foreach (string move in moves)
        {
            Assert.True(node.TryAddMove(move, out GameNode? next), "Could not play " + move);
            node = next!;
        }
        return node;
    }

    [Fact]
    public void ANewGameStartsFromTheStandardPosition()
    {
        GameRecord game = new();

        Assert.True(game.HasStandardStart);
        Assert.Equal(Position.StartingFEN, game.StartFen);
        Assert.True(game.Root.IsRoot);
        Assert.Equal(Position.StartingFEN, game.Root.Fen);
        Assert.Equal(0, game.Root.Ply);
        Assert.Equal(Colour.White, game.Root.SideToMove);
        Assert.Equal(Move.Invalid, game.Root.Move);
        Assert.Empty(game.Root.Children);
        Assert.Empty(game.MainLine());
        Assert.Same(game.Root, game.LastMainLineNode());
        Assert.Equal(GameRecord.UnknownResult, game.Result);
        Assert.Empty(game.Errors);
    }

    [Fact]
    public void EachNodeCachesTheFenKeyAndSanOfItsPosition()
    {
        GameRecord game = new();
        Play(game.Root, "e4", "e5", "Nf3", "Nc6", "Bb5", "a6", "Bxc6", "dxc6", "O-O");

        Position replay = new(Position.StartingFEN);
        foreach (GameNode node in game.MainLine())
        {
            Assert.Equal(Move.GetAlgebraicNotation(replay, node.Move), node.San);
            replay.Make(node.Move);
            Assert.Equal(replay.GetFEN(), node.Fen);
            Assert.Equal(replay.ZobristKey, node.ZobristKey);
            Assert.Equal(replay.HalfMoves, node.Ply);
            Assert.Equal(replay.SideToMove, node.SideToMove);
            Assert.Equal(replay.GetFEN(), node.ToPosition().GetFEN());
        }

        GameNode last = game.LastMainLineNode();
        Assert.Equal("O-O", last.San);
        Assert.Equal(5, last.MoveNumber);
        Assert.Equal("5. O-O", last.ToString());
        Assert.Equal("4... dxc6", last.Parent!.ToString());
    }

    [Fact]
    public void ToPositionReplaysTheHistorySoRepetitionIsSeen()
    {
        GameRecord game = new();
        GameNode node = Play(game.Root, "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1", "Ng8");

        Position position = node.ToPosition();

        Assert.True(position.HasRepeated(3));
        Assert.Equal(Position.StartingFEN.Replace(" 0 1", " 8 5"), node.Fen);
    }

    [Fact]
    public void AddingAnExistingMoveReturnsTheSameNode()
    {
        GameRecord game = new();
        GameNode e4 = Play(game.Root, "e4");

        Assert.True(game.Root.TryAddMove("e2e4", out GameNode? again));
        Assert.Same(e4, again);
        Assert.Same(e4, game.Root.AddMove(e4.Move));
        Assert.Single(game.Root.Children);
    }

    [Fact]
    public void AddMoveRejectsAnIllegalMoveAndTryAddMoveRejectsBadText()
    {
        GameRecord game = new();
        int e2e4 = Move.Create(new Position(Position.StartingFEN), 52, 36);
        int e2e5 = Move.Create(new Position(Position.StartingFEN), 52, 28);

        Assert.Equal("e4", game.Root.AddMove(e2e4).San);
        Assert.Throws<ArgumentException>(() => game.Root.AddMove(e2e5));
        Assert.False(game.Root.TryAddMove("e5", out GameNode? none));
        Assert.Null(none);
        Assert.False(game.Root.TryAddMove(null, out _));
    }

    [Fact]
    public void VariationsBranchAndChildrenZeroIsTheMainLine()
    {
        GameRecord game = new();
        GameNode e4 = Play(game.Root, "e4");
        GameNode e5 = Play(e4, "e5");
        GameNode c5 = Play(e4, "c5");
        GameNode e6 = Play(e4, "e6");
        GameNode nf3 = Play(c5, "Nf3");

        Assert.Equal(new[] { e5, c5, e6 }, e4.Children);
        Assert.Same(e5, e4.Next);
        Assert.True(e5.IsMainLine);
        Assert.False(c5.IsMainLine);
        Assert.False(nf3.IsMainLine);
        Assert.Equal(new[] { 0, 1, 0 }, nf3.GetPath());
        Assert.Same(nf3, game.NodeAt(new[] { 0, 1, 0 }));
        Assert.Same(game.Root, game.NodeAt(Array.Empty<int>()));
        Assert.Null(game.NodeAt(new[] { 0, 3 }));
        Assert.Null(game.NodeAt(new[] { -1 }));
        Assert.Equal(new[] { e4, e5 }, game.MainLine());
    }

    [Fact]
    public void PromoteDemoteAndPromoteToMainLineReorderVariations()
    {
        GameRecord game = new();
        GameNode e4 = Play(game.Root, "e4");
        GameNode e5 = Play(e4, "e5");
        GameNode c5 = Play(e4, "c5");
        GameNode e6 = Play(e4, "e6");
        GameNode d4 = Play(game.Root, "d4");
        GameNode d5 = Play(d4, "d5");

        Assert.True(e6.Promote());
        Assert.Equal(new[] { e5, e6, c5 }, e4.Children);
        Assert.True(e5.Demote());
        Assert.Equal(new[] { e6, e5, c5 }, e4.Children);
        Assert.False(e6.Promote());
        Assert.False(c5.Demote());
        Assert.False(game.Root.Promote());
        Assert.False(game.Root.Demote());

        d5.PromoteToMainLine();
        Assert.Equal(new[] { d4, e4 }, game.Root.Children);
        Assert.Equal(new[] { d4, d5 }, game.MainLine());
        Assert.True(d5.IsMainLine);
    }

    [Fact]
    public void RemoveDetachesANodeAndEverythingAfterIt()
    {
        GameRecord game = new();
        GameNode e4 = Play(game.Root, "e4");
        GameNode c5 = Play(e4, "c5");
        GameNode nf3 = Play(c5, "Nf3");
        Play(e4, "e5");

        Assert.True(c5.Remove());
        Assert.False(c5.Remove());
        Assert.False(game.Root.Remove());
        Assert.Single(e4.Children);
        Assert.False(c5.IsAttached);
        Assert.False(nf3.IsAttached);
        Assert.True(e4.IsAttached);

        e4.ClearChildren();
        Assert.Empty(e4.Children);
        Assert.Same(e4, game.LastMainLineNode());
    }

    [Fact]
    public void AGameCanStartFromAnyPlayablePosition()
    {
        const string fen = "4k3/8/8/8/8/8/4P3/4K3 b - - 3 40";

        Assert.True(GameRecord.TryCreate(fen, out GameRecord? game));
        Assert.False(game.HasStandardStart);
        Assert.Equal(fen, game.StartFen);
        Assert.Equal(79, game.Root.Ply);
        Assert.Equal(Colour.Black, game.Root.SideToMove);
        Assert.Equal(40, game.Root.MoveNumber);

        GameNode kd7 = Play(game.Root, "Kd7");
        Assert.Equal("40... Kd7", kd7.ToString());
        GameNode e4 = Play(kd7, "e4");
        Assert.Equal(41, e4.MoveNumber);
        Assert.Equal(fen, game.CreateStartPosition().GetFEN());

        Assert.True(GameRecord.TryCreate(null, out GameRecord? standard));
        Assert.True(standard.HasStandardStart);
        Assert.False(GameRecord.TryCreate("not a fen", out GameRecord? bad));
        Assert.Null(bad);
        Assert.False(GameRecord.TryCreate("4k3/8/8/8/8/8/8/8 w - - 0 1", out _));
    }

    [Fact]
    public void TagsKeepTheirOrderAndExportStartsWithTheSevenTagRoster()
    {
        GameRecord game = new();
        game.SetTag("Annotator", "Someone");
        game.White = "Alice";
        game.Black = "Bob";
        game.Result = "1-0";
        game.SetTag("Annotator", "Someone else");

        Assert.Equal(new[] { "Annotator", "White", "Black", "Result" }, game.Tags.Select(t => t.Key));
        Assert.Equal("Someone else", game.GetTag("Annotator"));
        Assert.Null(game.Event);

        List<KeyValuePair<string, string>> export = game.GetExportTags();
        Assert.Equal(new[] { "Event", "Site", "Date", "Round", "White", "Black", "Result", "Annotator" }, export.Select(t => t.Key));
        Assert.Equal("????.??.??", export[2].Value);
        Assert.Equal("?", export[0].Value);

        game.White = null;
        Assert.Null(game.GetTag("White"));
        Assert.True(game.RemoveTag("Annotator"));
        Assert.False(game.RemoveTag("Annotator"));
        game.Result = "";
        Assert.Equal(GameRecord.UnknownResult, game.Result);

        Assert.Throws<ArgumentException>(() => game.SetTag("FEN", "x"));
        Assert.Throws<ArgumentException>(() => game.SetTag("SetUp", "1"));
        Assert.Throws<ArgumentException>(() => game.SetTag("Bad Name", "x"));
        Assert.Throws<ArgumentException>(() => game.SetTag("", "x"));
    }

    [Fact]
    public void ExportTagsIncludeSetUpAndFenForANonStandardStart()
    {
        Assert.True(GameRecord.TryCreate("4k3/8/8/8/8/8/4P3/4K3 w - - 0 1", out GameRecord? game));

        List<KeyValuePair<string, string>> export = game.GetExportTags();

        Assert.Equal("SetUp", export[7].Key);
        Assert.Equal("1", export[7].Value);
        Assert.Equal("FEN", export[8].Key);
        Assert.Equal(game.StartFen, export[8].Value);
    }

    [Fact]
    public void AnnotationsAreNeverNull()
    {
        GameRecord game = new();
        GameNode e4 = Play(game.Root, "e4");

        e4.Comment = null!;
        e4.PreComment = null!;
        e4.Nags.Add(1);
        e4.Clock = TimeSpan.FromMinutes(3);

        Assert.Equal("", e4.Comment);
        Assert.Equal("", e4.PreComment);
        Assert.Equal(new[] { 1 }, e4.Nags);
        Assert.Equal(TimeSpan.FromMinutes(3), e4.Clock);
        Assert.Null(e4.MoveTime);
    }

    [Fact]
    public void GetMovesListsTheMovesFromTheStart()
    {
        GameRecord game = new();
        GameNode node = Play(game.Root, "d4", "d5", "c4");

        List<int> moves = node.GetMoves();

        Assert.Equal(new[] { "d2d4", "d7d5", "c2c4" }, moves.Select(Move.GetCoordinateNotation));
        Assert.Empty(game.Root.GetMoves());
    }

    [Fact]
    public void AGameStopsAtThePlyLimit()
    {
        // Knights shuffle without pawn moves or captures; nothing in the tree stops a game short of the ply limit.
        GameRecord game = new();
        GameNode node = game.Root;
        string[] cycle = { "Nf3", "Nf6", "Ng1", "Ng8" };
        int plies = 0;
        while (node.TryAddMove(cycle[plies % 4], out GameNode? next))
        {
            node = next!;
            plies++;
        }

        Assert.Equal(Position.HalfMovesLimit - 1, node.Ply);
        Assert.Throws<InvalidOperationException>(() => node.AddMove(Move.FromAlgebraicNotation(node.ToPosition(), cycle[plies % 4])));
    }
}
