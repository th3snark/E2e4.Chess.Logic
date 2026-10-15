using System.Collections.Generic;
using E2e4.Chess.Logic.Enums;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

public class MoveRulesUnitTest
{
    private static int Find(Position position, string coordinates)
    {
        int move = Move.Create(position, coordinates);
        Assert.NotEqual(Move.Invalid, move);
        return move;
    }

    [Theory]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 5 1", "e7e8q")]               // quiet promotion
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 5 1", "e7e8n")]               // quiet underpromotion
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 5 1", "e5d6")]            // en passant
    [InlineData("4k3/8/8/8/8/8/4P3/4K3 w - - 5 1", "e2e3")]              // pawn push
    [InlineData("4k3/8/8/3p4/4P3/8/8/4K3 w - - 5 1", "e4d5")]            // capture
    public void MakeResetsFiftyMovesClock(string fen, string coordinates)
    {
        Position position = new(fen);

        position.Make(Find(position, coordinates));

        Assert.Equal(0, position.FiftyMovesClock);
    }

    [Fact]
    public void MakeIncrementsFiftyMovesClockForQuietPieceMove()
    {
        Position position = new("4k3/8/8/8/8/8/8/4K3 w - - 5 1");

        position.Make(Find(position, "e1e2"));

        Assert.Equal(6, position.FiftyMovesClock);
    }

    [Theory]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 5 1", "e7e8q")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 5 1", "e5d6")]
    public void UnmakeRestoresFiftyMovesClock(string fen, string coordinates)
    {
        Position position = new(fen);
        int move = Find(position, coordinates);

        position.Make(move);
        position.Unmake(move);

        Assert.Equal(5, position.FiftyMovesClock);
        Assert.Equal(fen, position.GetFEN());
    }

    [Theory]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8q", "e8=Q+")]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8r", "e8=R+")]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8b", "e8=B")]
    [InlineData("7k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7e8n", "e8=N")]
    [InlineData("3r3k/4P3/8/8/8/8/8/K7 w - - 0 1", "e7d8q", "exd8=Q+")]
    [InlineData("3k4/8/8/8/8/8/8/R3K3 w Q - 0 1", "e1c1", "O-O-O+")]
    [InlineData("3k4/8/8/8/8/8/8/4K2R w K - 0 1", "e1g1", "O-O")]
    public void AlgebraicNotationForPromotionsAndCastling(string fen, string coordinates, string expected)
    {
        Position position = new(fen);

        Assert.Equal(expected, Move.GetAlgebraicNotation(position, Find(position, coordinates)));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/R6R/4K3 w - - 0 1", "a2d2", "Rad2")]     // disambiguate by file
    [InlineData("4k3/R7/8/8/R7/8/8/4K3 w - - 0 1", "a7a5", "R7a5")]     // disambiguate by rank
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1", "e1e2", "Ke2")]
    public void AlgebraicNotationDisambiguates(string fen, string coordinates, string expected)
    {
        Position position = new(fen);

        Assert.Equal(expected, Move.GetAlgebraicNotation(position, Find(position, coordinates)));
    }

    [Fact]
    public void AlgebraicNotationShowsCheckmate()
    {
        Position position = new("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1");

        Assert.Equal("Ra8#", Move.GetAlgebraicNotation(position, Find(position, "a1a8")));
    }

    [Fact]
    public void ProperAlgebraicNotationNumbersFromBlackToMove()
    {
        Position position = new("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
        List<int> line = new();
        int black = Find(position, "e7e5");
        line.Add(black);
        position.Make(black);
        line.Add(Find(position, "g1f3"));
        position.Unmake(black);

        Assert.Equal("1... e5 2. Nf3", Move.GetAlgebraicNotation(position, line, AlgebraicNotationFormat.Proper));
    }

    private static List<int> BuildLine(Position position, params string[] moves)
    {
        List<int> line = new();
        foreach (string text in moves)
        {
            int move = Find(position, text);
            line.Add(move);
            position.Make(move);
        }
        for (int i = line.Count - 1; i >= 0; i--)
            position.Unmake(line[i]);
        return line;
    }

    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "1.e4 e5 2.Nf3", new[] { "e2e4", "e7e5", "g1f3" })]
    [InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", "1...e5 2.Nf3 Nc6", new[] { "e7e5", "g1f3", "b8c6" })]
    [InlineData("4k3/8/8/8/8/8/4P3/4K3 w - - 0 30", "30.e4 Kd7 31.Kd2", new[] { "e2e4", "e8d7", "e1d2" })]
    [InlineData("4k3/8/8/8/4P3/8/8/4K3 b - - 0 30", "30...Kd7 31.Kd2", new[] { "e8d7", "e1d2" })]
    public void CompactAlgebraicNotationUsesTheRealMoveNumberAndSide(string fen, string expected, string[] moves)
    {
        Position position = new(fen);
        List<int> line = BuildLine(position, moves);

        Assert.Equal(expected, Move.GetAlgebraicNotation(position, line));
        Assert.Equal(fen, position.GetFEN());
    }

    [Fact]
    public void CompactAndProperNotationDifferOnlyBySpacing()
    {
        Position position = new("4k3/8/8/8/4P3/8/8/4K3 b - - 0 30");
        List<int> line = BuildLine(position, "e8d7", "e1d2");

        Assert.Equal("30... Kd7 31. Kd2", Move.GetAlgebraicNotation(position, line, AlgebraicNotationFormat.Proper));
    }
}
