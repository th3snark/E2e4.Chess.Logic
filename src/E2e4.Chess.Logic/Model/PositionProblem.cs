using System;

namespace E2e4.Chess.Logic.Model;

/// <summary>How serious a <see cref="PositionProblem"/> is.</summary>
public enum PositionProblemSeverity
{
    /// <summary>The setup cannot become a playable <see cref="Position"/>.</summary>
    Error,

    /// <summary>The setup is playable but probably not what was meant.</summary>
    Warning,
}

/// <summary>What a <see cref="PositionProblem"/> is about.</summary>
public enum PositionProblemKind
{
    /// <summary>A side has no king.</summary>
    MissingKing,

    /// <summary>A side has more than one king.</summary>
    ExtraKing,

    /// <summary>A pawn stands on the first or last rank.</summary>
    PawnOnLastRank,

    /// <summary>A side has more than eight pawns.</summary>
    TooManyPawns,

    /// <summary>A side has more pieces than its missing pawns could have promoted into.</summary>
    ImpossibleMaterial,

    /// <summary>A castling right lacks its king or rook on the original square.</summary>
    InvalidCastlingRights,

    /// <summary>The en passant square does not fit the board or the side to move.</summary>
    InvalidEnPassant,

    /// <summary>The kings stand next to each other.</summary>
    KingsAdjacent,

    /// <summary>The side that is not to move is in check.</summary>
    SideNotToMoveInCheck,

    /// <summary>The side to move has no legal move (checkmate or stalemate).</summary>
    NoLegalMoves,

    /// <summary>The move number or fifty-moves clock is outside the supported range.</summary>
    InvalidCounters,
}

/// <summary>One thing found wrong with a <see cref="PositionSetup"/> by <see cref="PositionSetup.Validate"/>.</summary>
public sealed class PositionProblem
{
    /// <summary>Creates a problem.</summary>
    public PositionProblem(PositionProblemKind kind, PositionProblemSeverity severity, string message)
    {
        Kind = kind;
        Severity = severity;
        Message = message;
    }

    /// <summary>What the problem is about.</summary>
    public PositionProblemKind Kind { get; }

    /// <summary>Whether it stops the setup becoming a position.</summary>
    public PositionProblemSeverity Severity { get; }

    /// <summary>A sentence describing it, for display.</summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Message;
    }
}
