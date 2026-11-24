using System;
using System.Linq;
using E2e4.Chess.Logic.Games;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// The compact binary form of a game, <see cref="GameEncoding"/>.
/// </summary>
public class GameEncodingUnitTest
{
    private const string Annotated = """
        [Event "Training"]
        [White "White"]
        [Black "Black"]
        [Result "1-0"]

        {Start.} 1. e4 {[%clk 0:03:00]} e5 $1 (1... c5 $5 {Sicilian} 2. Nf3 (2. c3 d5) 2... d6)
        (1... e6) 2. Nf3 Nc6 3. Bb5 a6 ({Instead} 3... Nf6 4. O-O) 4. Ba4 {[%emt 0:00:04.25]} 1-0
        """;

    [Fact]
    public void AGameSurvivesEncodingExactly()
    {
        Assert.True(Pgn.TryParseGame(Annotated, out GameRecord? game));

        byte[] data = GameEncoding.Encode(game!);
        Assert.True(GameEncoding.TryDecode(data, out GameRecord? decoded));

        Assert.Equal(game!.Tags, decoded.Tags);
        Assert.Equal(game.StartFen, decoded.StartFen);
        Assert.Equal(game.Root.Comment, decoded.Root.Comment);
        PgnUnitTest.AssertSameTree(game.Root, decoded.Root);
        Assert.Equal(Pgn.Write(game), Pgn.Write(decoded));
    }

    [Fact]
    public void ExactCommentTextIsKept()
    {
        GameRecord game = new();
        Assert.True(game.Root.TryAddMove("e4", out GameNode? e4));
        e4!.Comment = "  two  spaces\nand a line break  ";

        Assert.True(GameEncoding.TryDecode(GameEncoding.Encode(game), out GameRecord? decoded));

        Assert.Equal(e4.Comment, decoded.Root.Next!.Comment);
    }

    [Fact]
    public void MovesTakeTwoBytesEach()
    {
        string moves = "1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 5. O-O Be7 6. Re1 b5 7. Bb3 d6 8. c3 O-O *";
        Assert.True(Pgn.TryParseGame(moves, out GameRecord? game));
        int header = GameEncoding.Encode(new GameRecord { Result = "*" }).Length;

        byte[] data = GameEncoding.Encode(game!);

        Assert.Equal(header + 2 * 16, data.Length);
    }

    [Fact]
    public void ANonStandardStartAndPromotionsAreKept()
    {
        Assert.True(GameRecord.TryCreate("4k3/1P6/8/8/8/8/6p1/4K3 w - - 0 50", out GameRecord? game));
        Assert.True(game.Root.TryAddMove("b8=N", out GameNode? b8));
        Assert.True(b8!.TryAddMove("g1=R+", out GameNode? g1));
        Assert.True(game.Root.TryAddMove("b8=Q+", out _));

        Assert.True(GameEncoding.TryDecode(GameEncoding.Encode(game), out GameRecord? decoded));

        Assert.Equal(game.StartFen, decoded.StartFen);
        Assert.Equal(new[] { "b8=N", "b8=Q+" }, decoded.Root.Children.Select(n => n.San));
        Assert.Equal(g1!.San, decoded.Root.Next!.Next!.San);
    }

    [Fact]
    public void CorruptDataIsRejectedWithoutThrowing()
    {
        Assert.True(Pgn.TryParseGame(Annotated, out GameRecord? game));
        byte[] data = GameEncoding.Encode(game!);

        Assert.False(GameEncoding.TryDecode(null, out _));
        Assert.False(GameEncoding.TryDecode(Array.Empty<byte>(), out _));
        for (int length = 0; length < data.Length; length++)
            Assert.False(GameEncoding.TryDecode(data.Take(length).ToArray(), out _));
        Assert.False(GameEncoding.TryDecode(data.Concat(new byte[] { 0 }).ToArray(), out _));

        Random random = new(20261124);
        for (int i = 0; i < 5000; i++)
        {
            byte[] corrupt = (byte[])data.Clone();
            corrupt[random.Next(corrupt.Length)] ^= (byte)(1 << random.Next(8));
            if (GameEncoding.TryDecode(corrupt, out GameRecord? decoded))
                Assert.NotNull(Pgn.Write(decoded));
        }
        for (int i = 0; i < 2000; i++)
        {
            byte[] noise = new byte[random.Next(0, 64)];
            random.NextBytes(noise);
            if (noise.Length > 3)
            {
                noise[0] = (byte)'E';
                noise[1] = (byte)'2';
                noise[2] = (byte)'G';
                noise[3] = 1;
            }
            GameEncoding.TryDecode(noise, out _);
        }
    }
}
