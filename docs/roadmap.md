# Roadmap

Nothing here is promised or scheduled. It lists what is still missing, found by comparing the library with what teaching and analysis tools offer
([Lichess studies](https://lichess.org/blog/Wsdc0C4AAIwAH4_L/lichess-study-and-our-favorite-user-created-content),
[ChessBase](https://en.chessbase.com/newsroom/post/annotating-in-chessbase-arrows-and-highlighted-squares?page=0),
[Chessground](https://npmjs.com/package/chessground), [python-chess](https://python-chess.readthedocs.io/en/latest/pgn.html)). Anything that
only draws or stores board annotations (arrows, circles, `[%cal]`/`[%csl]`) belongs in E2e4.Chess.Controls, which has its own roadmap. Finished work
leaves this file; the README describes what the library does today. The game record, PGN and position index follow the design in
[game-record.md](game-record.md).

| Gap | Value | Effort | Notes |
|-----|-------|--------|-------|
| **Polyglot opening books** | Low to medium | Medium | `PositionIndex` is keyed by this library's own Zobrist keys. Reading Polyglot `.bin` books (from bytes, never a file) needs Polyglot's fixed table of 781 random keys and its rule of hashing the en passant file only when a capture is possible. May belong in its own package. |
| **Null moves in games** | Low | Low | PGN `--` (a null move, used in analysis) ends its line when read, because a `GameNode` holds only legal moves. Storing one needs a null-move node that `ToPosition()` replays with `MakeNull`. |
| **Figurine and localised SAN** | Low | Low | `Move.FromAlgebraicNotation` reads English piece letters only; figurines (♘) and other languages' letters (S for Springer) would need a letter table. |
| **Control map**: per-square attack counts for each colour | Medium | Low | A square-by-square overlay ("who controls what"). Bitboards make it cheap. Piece mobility counts fit the same API. |
| **Exchange-aware hanging pieces** | Medium | Low to medium | `HangingPieces` means attacked and undefended. A defended piece attacked by a cheaper one can still lose material; a static exchange evaluation would catch it. |
| **Relative pins** | Low | Low | `Pins` only reports absolute pins against the king. A piece pinned to a queen or rook is a common lesson. |
| **Position tools for drills** | Low to medium | Low | Mirror, flip colours, rotate; FEN normalisation. |
| **Chess960** | Low | Medium | Needs castling rules and FEN (X-FEN or Shredder) changes; the perft tests would need Chess960 reference counts. |
