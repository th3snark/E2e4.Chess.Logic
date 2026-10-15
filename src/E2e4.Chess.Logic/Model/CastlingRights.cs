using System;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// The castling moves that are still possible in a position: a king and rook that have not
/// moved (and a rook that has not been captured on its home square). Possible is not the
/// same as legal right now: a right can be held while castling is blocked or attacked.
/// </summary>
[Flags]
public enum CastlingRights
{
    /// <summary>Neither side may castle.</summary>
    None = 0,

    /// <summary>White may castle on the king side (O-O).</summary>
    WhiteKingside = 1,

    /// <summary>White may castle on the queen side (O-O-O).</summary>
    WhiteQueenside = 2,

    /// <summary>Black may castle on the king side (O-O).</summary>
    BlackKingside = 4,

    /// <summary>Black may castle on the queen side (O-O-O).</summary>
    BlackQueenside = 8,

    /// <summary>Every castling move is still possible.</summary>
    All = WhiteKingside | WhiteQueenside | BlackKingside | BlackQueenside,
}
