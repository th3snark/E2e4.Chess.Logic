using System;
using System.Globalization;

namespace E2e4.Chess.Logic.Model;

/// <summary>
/// Defines pieces and masks.
/// </summary>
public static class Piece
{
    /// <summary>
    /// The mask for extracting the type of a piece.
    /// </summary>
    public const Int32 Mask = 14;  // 1110

    /// <summary>
    /// The smallest value used to represent a piece.
    /// </summary>
    public const Int32 Min = Colour.White | Pawn;

    /// <summary>
    /// The largest value used to represent a piece.
    /// </summary>
    public const Int32 Max = Colour.Black | King;

    /// <summary>
    /// The empty, or non-existent, piece.
    /// </summary>
    public const Int32 Empty = 0;  // 0000

    /// <summary>
    /// The pawn piece.
    /// </summary>
    public const Int32 Pawn = 2;   // 0010

    /// <summary>
    /// The knight piece.
    /// </summary>
    public const Int32 Knight = 4; // 0100

    /// <summary>
    /// The bishop piece.
    /// </summary>
    public const Int32 Bishop = 6; // 0110

    /// <summary>
    /// The rook piece.
    /// </summary>
    public const Int32 Rook = 8;   // 1000

    /// <summary>
    /// The queen piece.
    /// </summary>
    public const Int32 Queen = 10; // 1010

    /// <summary>
    /// The king piece.
    /// </summary>
    public const Int32 King = 12;  // 1100

    /// <summary>
    /// Returns the text representation of the given piece.
    /// </summary>
    /// <param name="piece">The piece to identify.</param>
    /// <returns>The text representation of the given piece.</returns>
    public static string GetName(Int32 piece)
    {
        switch (piece & Piece.Mask)
        {
            case Piece.King:
                return "King";
            case Piece.Queen:
                return "Queen";
            case Piece.Rook:
                return "Rook";
            case Piece.Bishop:
                return "Bishop";
            case Piece.Knight:
                return "Knight";
            case Piece.Pawn:
                return "Pawn";
        }
        throw new ArgumentOutOfRangeException(nameof(piece), piece, String.Format(CultureInfo.InvariantCulture, "{0:x} is not a recognized chess piece value.", piece));
    }

    /// <summary>
    /// Returns the text representation of the given piece as an initial.
    /// </summary>
    /// <param name="piece">The piece to identify.</param>
    /// <returns>The text representation of the given piece as an initial.</returns>
    public static string GetInitial(Int32 piece)
    {
        if ((piece & Piece.Mask) == Piece.Knight)
            return "N";
        return GetName(piece)[0].ToString();
    }

    /// <summary>
    /// Returns the piece, without a colour, that a FEN piece letter stands for, in either case; the inverse of
    /// <see cref="GetInitial"/>.
    /// </summary>
    /// <param name="letter">The letter to decode.</param>
    /// <returns>The piece, or <see cref="Empty"/> if the letter does not stand for a piece.</returns>
    internal static Int32 FromInitial(Char letter)
    {
        switch (Char.ToUpperInvariant(letter))
        {
            case 'K': return King;
            case 'Q': return Queen;
            case 'R': return Rook;
            case 'B': return Bishop;
            case 'N': return Knight;
            case 'P': return Pawn;
            default: return Empty;
        }
    }
}
