using System.Collections.Generic;
using E2e4.Chess.Logic.Bitboards;
using E2e4.Chess.Logic.Model;
using Xunit;

namespace E2e4.Chess.Logic.Test;

/// <summary>
/// Move-generation correctness via perft: the node counts below are the widely
/// published reference values for these positions (Chess Programming Wiki).
/// </summary>
public class PerftUnitTest
{
    private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    private const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
    private const string Position6 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

    [Theory]
    [InlineData(Position.StartingFEN, 1, 20)]
    [InlineData(Position.StartingFEN, 2, 400)]
    [InlineData(Position.StartingFEN, 3, 8902)]
    [InlineData(Position.StartingFEN, 4, 197281)]
    [InlineData(Position.StartingFEN, 5, 4865609)]
    [InlineData(Kiwipete, 1, 48)]
    [InlineData(Kiwipete, 2, 2039)]
    [InlineData(Kiwipete, 3, 97862)]
    [InlineData(Kiwipete, 4, 4085603)]
    [InlineData(Position3, 1, 14)]
    [InlineData(Position3, 2, 191)]
    [InlineData(Position3, 3, 2812)]
    [InlineData(Position3, 4, 43238)]
    [InlineData(Position3, 5, 674624)]
    [InlineData(Position4, 1, 6)]
    [InlineData(Position4, 2, 264)]
    [InlineData(Position4, 3, 9467)]
    [InlineData(Position5, 1, 44)]
    [InlineData(Position5, 2, 1486)]
    [InlineData(Position5, 3, 62379)]
    [InlineData(Position6, 1, 46)]
    [InlineData(Position6, 2, 2079)]
    [InlineData(Position6, 3, 89890)]
    public void PerftMatchesPublishedNodeCounts(string fen, int depth, long expected)
    {
        Position position = new(fen);

        Assert.Equal(expected, Perft(position, depth, new int[depth + 1][]));
    }

    [Fact]
    public void MoveGenerationIsCorrectWhenRunConcurrentlyOnSeparatePositions()
    {
        // Sliding-piece attack generation must not share mutable state between threads.
        string[] fens = { Kiwipete, Position3, Position4, Position5, Position6 };
        long[] expected = { 97862, 2812, 9467, 62379, 89890 };
        int[] depths = { 3, 3, 3, 3, 3 };

        System.Threading.Tasks.Parallel.For(0, 40, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            int index = i % fens.Length;
            Position position = new(fens[index]);
            Assert.Equal(expected[index], Perft(position, depths[index], new int[depths[index] + 1][]));
        });
    }

    [Theory]
    [InlineData(Position.StartingFEN)]
    [InlineData(Kiwipete)]
    [InlineData(Position3)]
    [InlineData(Position4)]
    [InlineData(Position5)]
    [InlineData(Position6)]
    public void MakeAndUnmakeKeepAllStateConsistentToDepthThree(string fen)
    {
        Position position = new(fen);

        CheckInvariants(position, 3);
    }

    [Theory]
    [InlineData(Position.StartingFEN)]
    [InlineData(Kiwipete)]
    [InlineData(Position3)]
    [InlineData(Position4)]
    [InlineData(Position5)]
    [InlineData(Position6)]
    public void CausesCheckAgreesWithMakingTheMove(string fen)
    {
        Position position = new(fen);

        foreach (int move in position.LegalMoves())
        {
            bool predicted = position.CausesCheck(move);
            position.Make(move);
            bool actual = position.InCheck(position.SideToMove);
            position.Unmake(move);

            Assert.True(predicted == actual,
                $"CausesCheck({Move.GetCoordinateNotation(move)}) was {predicted} but making the move gives {actual} in {fen}");
        }
    }

    private static long Perft(Position position, int depth, int[][] buffers)
    {
        if (depth == 0)
            return 1;

        int[] moves = buffers[depth] ??= new int[256];
        int count = position.LegalMoves(moves);
        if (depth == 1)
            return count;

        long nodes = 0;
        for (int i = 0; i < count; i++)
        {
            position.Make(moves[i]);
            nodes += Perft(position, depth - 1, buffers);
            position.Unmake(moves[i]);
        }
        return nodes;
    }

    private static void CheckInvariants(Position position, int depth)
    {
        AssertConsistent(position);
        if (depth == 0)
            return;

        Position before = position.DeepClone();
        List<int> moves = position.LegalMoves();
        foreach (int move in moves)
        {
            string text = Move.GetCoordinateNotation(move);
            position.Make(move);
            Assert.True(position.ZobristKey == position.GetZobristKey(), $"Zobrist key stale after {text} from {before.GetFEN()}");
            CheckInvariants(position, depth - 1);
            position.Unmake(move);
            Assert.True(position.Equals(before), $"Unmake({text}) did not restore {before.GetFEN()}");
            Assert.Equal(before.GetFEN(), position.GetFEN());
        }
    }

    private static void AssertConsistent(Position position)
    {
        ulong white = 0, black = 0;
        for (int square = 0; square < 64; square++)
        {
            int piece = position.Squares[square];
            if (piece == Piece.Empty)
                continue;
            ulong bit = 1UL << square;
            Assert.NotEqual(0UL, position.PieceBitboards[piece] & bit);
            if ((piece & Colour.Mask) == Colour.White) white |= bit; else black |= bit;
        }
        Assert.Equal(white, position.PieceBitboards[Colour.White]);
        Assert.Equal(black, position.PieceBitboards[Colour.Black]);
        Assert.Equal(white | black, position.OccupiedBitboard);
        Assert.Equal(Bitboard.Count(white | black), Bitboard.Count(position.OccupiedBitboard));
    }
}
