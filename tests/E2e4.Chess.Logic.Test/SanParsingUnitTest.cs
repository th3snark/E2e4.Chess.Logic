using System.Collections.Generic;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// Reading moves in standard algebraic notation with <see cref="Move.FromAlgebraicNotation"/>.
/// </summary>
public class SanParsingUnitTest
{
    private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    private const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
    private const string Position6 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

    [Theory]
    [InlineData(Position.StartingFEN)]
    [InlineData(Kiwipete)]
    [InlineData(Position3)]
    [InlineData(Position4)]
    [InlineData(Position5)]
    [InlineData(Position6)]
    public void EveryMoveReadsBackFromItsSanAndCoordinatesTwoPliesDeep(string fen)
    {
        // The writer covers disambiguation, captures, en passant, promotion, castling, check and mate, so a round trip
        // through it exercises the reader on every kind of move these positions reach.
        Position position = new(fen);
        int checkedMoves = 0;
        foreach (int first in position.LegalMoves())
        {
            AssertReadsBack(position, first);
            checkedMoves++;
            position.Make(first);
            foreach (int second in position.LegalMoves())
            {
                AssertReadsBack(position, second);
                checkedMoves++;
            }
            position.Unmake(first);
        }
        Assert.True(checkedMoves > 0);
        Assert.Equal(fen, position.GetFEN());
    }

    private static void AssertReadsBack(Position position, int move)
    {
        string san = Move.GetAlgebraicNotation(position, move);
        Assert.Equal(move, Move.FromAlgebraicNotation(position, san));
        Assert.Equal(move, Move.FromAlgebraicNotation(position, Move.GetCoordinateNotation(move)));
    }

    [Theory]
    [InlineData(Position.StartingFEN, "e4", "e2e4")]
    [InlineData(Position.StartingFEN, " Nf3 ", "g1f3")]
    [InlineData(Position.StartingFEN, "Ng1f3", "g1f3")]
    [InlineData(Position.StartingFEN, "Ng1-f3", "g1f3")]
    [InlineData(Position.StartingFEN, "e2-e4", "e2e4")]
    [InlineData(Position.StartingFEN, "Pe4", "e2e4")]
    [InlineData(Position.StartingFEN, "e4!?", "e2e4")]
    [InlineData(Position.StartingFEN, "Nf3?!", "g1f3")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "O-O", "e1g1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "0-0", "e1g1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "o-o-o", "e1c1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "0-0-0+", "e1c1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1", "e1g1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "Ke1g1", "e1g1")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "O-O-O", "e8c8")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "exd6", "e5d6")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "exd6 e.p.", "e5d6")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "ed6", "e5d6")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e:d6", "e5d6")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8=Q+", "b7b8q")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8Q", "b7b8q")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8=N", "b7b8n")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8/R", "b7b8r")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b7b8b", "b7b8b")]
    [InlineData("4k3/8/8/8/8/8/4K3/R6R w - - 0 1", "Rad1", "a1d1")]
    [InlineData("4k3/8/8/8/8/8/4K3/R6R w - - 0 1", "Rhf1", "h1f1")]
    [InlineData("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1", "R1a3", "a1a3")]
    [InlineData("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1", "R5a3", "a5a3")]
    [InlineData("7k/8/2Q1Q3/8/2Q5/8/8/4K3 w - - 0 1", "Qc6d5", "c6d5")]
    [InlineData("7k/8/2Q1Q3/8/2Q5/8/8/4K3 w - - 0 1", "Qe6d5", "e6d5")]
    public void ReadsTheNamedMove(string fen, string text, string expected)
    {
        Position position = new(fen);

        int move = Move.FromAlgebraicNotation(position, text);

        Assert.NotEqual(Move.Invalid, move);
        Assert.Equal(expected, Move.GetCoordinateNotation(move));
        Assert.Equal(fen, position.GetFEN());
    }

    [Theory]
    [InlineData(Position.StartingFEN, null)]
    [InlineData(Position.StartingFEN, "")]
    [InlineData(Position.StartingFEN, "   ")]
    [InlineData(Position.StartingFEN, "e5")]
    [InlineData(Position.StartingFEN, "E4")]
    [InlineData(Position.StartingFEN, "nf3")]
    [InlineData(Position.StartingFEN, "Nf4")]
    [InlineData(Position.StartingFEN, "Ke2")]
    [InlineData(Position.StartingFEN, "O-O")]
    [InlineData(Position.StartingFEN, "e9")]
    [InlineData(Position.StartingFEN, "i4")]
    [InlineData(Position.StartingFEN, "Ze4")]
    [InlineData(Position.StartingFEN, "e4=Q")]
    [InlineData(Position.StartingFEN, "Nxx f3")]
    [InlineData(Position.StartingFEN, "--")]
    [InlineData(Position.StartingFEN, "1-0")]
    [InlineData(Position.StartingFEN, "Nf3Nf3Nf3Nf3")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8=K")]
    [InlineData("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1", "b8=P")]
    [InlineData("4k3/8/8/8/8/8/4K3/R6R w - - 0 1", "Rd1")]
    [InlineData("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1", "Ra3")]
    [InlineData("7k/8/2Q1Q3/8/2Q5/8/8/4K3 w - - 0 1", "Qd5")]
    [InlineData("7k/8/2Q1Q3/8/2Q5/8/8/4K3 w - - 0 1", "Qcd5")]
    public void RejectsTextThatNamesNoSingleLegalMove(string fen, string? text)
    {
        Position position = new(fen);

        Assert.Equal(Move.Invalid, Move.FromAlgebraicNotation(position, text));
        Assert.Equal(fen, position.GetFEN());
    }

    [Fact]
    public void RandomTextNeverThrowsAndNeverChangesThePosition()
    {
        System.Random random = new(20261124);
        const string alphabet = "abcdefghKQRBNPkqrbnpxX=+#!?-0Oo12345678:/. e";
        Position position = new(Position.StartingFEN);
        List<int> legal = position.LegalMoves();
        for (int i = 0; i < 20000; i++)
        {
            char[] chars = new char[random.Next(0, 10)];
            for (int c = 0; c < chars.Length; c++)
                chars[c] = alphabet[random.Next(alphabet.Length)];

            int move = Move.FromAlgebraicNotation(position, new string(chars));

            Assert.True(move == Move.Invalid || legal.Contains(move));
        }
        Assert.Equal(Position.StartingFEN, position.GetFEN());
    }
}
