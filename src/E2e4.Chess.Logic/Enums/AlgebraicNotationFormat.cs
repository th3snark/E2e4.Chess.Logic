namespace E2e4.Chess.Logic.Enums;

/// <summary>
/// Specifies whether to use compact or spaced notation for move sequences. Both number the moves
/// from the position's real move number and mark a leading Black move with "...".
/// </summary>
public enum AlgebraicNotationFormat
{
    /// <summary>Compact, e.g. <c>1...e5 2.Nf3 Nc6</c>.</summary>
    None,
    /// <summary>Spaced, e.g. <c>1... e5 2. Nf3 Nc6</c>.</summary>
    Proper,
}
