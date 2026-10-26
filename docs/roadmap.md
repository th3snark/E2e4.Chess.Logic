# Roadmap

Nothing here is promised or scheduled. It lists what is still missing, found by comparing the library with what teaching and analysis tools offer
([Lichess studies](https://lichess.org/blog/Wsdc0C4AAIwAH4_L/lichess-study-and-our-favorite-user-created-content),
[ChessBase](https://en.chessbase.com/newsroom/post/annotating-in-chessbase-arrows-and-highlighted-squares?page=0),
[Chessground](https://npmjs.com/package/chessground), [python-chess](https://python-chess.readthedocs.io/en/latest/pgn.html)). Anything that
only draws or stores board annotations (arrows, circles, `[%cal]`/`[%csl]`) belongs in E2e4.Chess.Controls, which has its own roadmap. Finished work
leaves this file; the README describes what the library does today.

| Gap | Value | Effort | Notes |
|-----|-------|--------|-------|
| **SAN parsing** | High | Low to medium | `Move.GetAlgebraicNotation` writes SAN (disambiguation, `+`, `#`), but `Move.Create(position, string)` only reads coordinate notation (`e2e4`). A reader needs to resolve a SAN string against the legal moves, tolerate missing marks and `0-0` for castling, and report an ambiguous or illegal move as `Move.Invalid`. Figurine or localised piece letters would come later. |
| **PGN with variations, comments and NAGs** | High | High | The biggest gap: every study tool uses PGN, and nothing here reads or writes it. Needs SAN parsing first. Includes the comment commands `[%cal]`, `[%csl]`, `[%clk]` and `[%eval]`. Decide first whether it lives in this package, a separate one, or Controls. It must keep the no-dependency, no-I/O rule (parse from a string, never open files). |
| **Control map**: per-square attack counts for each colour | Medium | Low | A square-by-square overlay ("who controls what"). Bitboards make it cheap. Piece mobility counts fit the same API. |
| **Exchange-aware hanging pieces** | Medium | Low to medium | `HangingPieces` means attacked and undefended. A defended piece attacked by a cheaper one can still lose material; a static exchange evaluation would catch it. |
| **Relative pins** | Low | Low | `Pins` only reports absolute pins against the king. A piece pinned to a queen or rook is a common lesson. |
| **Position tools for drills** | Low to medium | Low | Mirror, flip colours, rotate; FEN normalisation. |
| **Chess960** | Low | Medium | Needs castling rules and FEN (X-FEN or Shredder) changes; the perft tests would need Chess960 reference counts. |
