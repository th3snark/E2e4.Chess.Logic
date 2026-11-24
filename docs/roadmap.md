# Roadmap

Nothing here is promised or scheduled. It lists what is still missing, found by comparing the library with what teaching and analysis tools offer
([Lichess studies](https://lichess.org/blog/Wsdc0C4AAIwAH4_L/lichess-study-and-our-favorite-user-created-content),
[ChessBase](https://en.chessbase.com/newsroom/post/annotating-in-chessbase-arrows-and-highlighted-squares?page=0),
[Chessground](https://npmjs.com/package/chessground), [python-chess](https://python-chess.readthedocs.io/en/latest/pgn.html)). Anything that
only draws or stores board annotations (arrows, circles, `[%cal]`/`[%csl]`) belongs in E2e4.Chess.Controls, which has its own roadmap. Finished work
leaves this file; the README describes what the library does today. The game-record rows (SAN parsing to opening books) follow the design in
[game-record.md](game-record.md) and are listed in the order they build on each other.

| Gap | Value | Effort | Notes |
|-----|-------|--------|-------|
| **SAN parsing** | High | Low to medium | `Move.GetAlgebraicNotation` writes SAN (disambiguation, `+`, `#`), but `Move.Create(position, string)` only reads coordinate notation (`e2e4`). A reader needs to resolve a SAN string against the legal moves, tolerate missing marks and `0-0` for castling, and report an ambiguous or illegal move as `Move.Invalid`. Figurine or localised piece letters would come later. Unblocks the game record, FICS move lists and PGN. |
| **Game record tree** | High | Medium | `GameRecord` (tags, start position, result) and `GameNode` (parent, move, ordered children with `Children[0]` as the main line, comments, NAGs, clock and move time). Each node caches its FEN, Zobrist key and SAN when added, so selecting a move is O(1) for the board. Also path addressing, promote/demote/remove, and `ToPosition()` by replay from the root. One tree serves plain games and annotated studies. |
| **PGN with variations, comments and NAGs** | High | High | The biggest gap: every study tool uses PGN. Lives in this package and builds on the game record. Reads many games from a string, leniently (a bad move ends that line and is reported, never throws), and writes back what it read. Comments are kept verbatim; `[%clk]`/`[%emt]` become node times. No dependencies, no I/O. |
| **Comment commands** | Low | Low | List the `[%name args]` commands in a comment. Logic does not interpret `[%cal]`/`[%csl]`: E2e4.Chess.Controls draws them. |
| **Compact binary game encoding** | Low | Medium | A 16-bit move per ply plus tokens for variations, comments and NAGs (as Scid does), for storing many games. PGN stays the interchange format. |
| **Opening book / position index** | Low to medium | Medium | A position-keyed graph (transpositions merge), separate from the game tree and linked by each node's position key. A Polyglot reader needs Polyglot's own hash keys. May belong in its own package. |
| **Lighter `Position`** | Low | Low | Grow the four history stacks lazily (start at about 64 plies, doubling up to `HalfMovesLimit`) so a shallow `Position` costs 1–2 KB instead of about 20 KB. Benchmark `Make` before and after; perft counts must not change. |
| **Control map**: per-square attack counts for each colour | Medium | Low | A square-by-square overlay ("who controls what"). Bitboards make it cheap. Piece mobility counts fit the same API. |
| **Exchange-aware hanging pieces** | Medium | Low to medium | `HangingPieces` means attacked and undefended. A defended piece attacked by a cheaper one can still lose material; a static exchange evaluation would catch it. |
| **Relative pins** | Low | Low | `Pins` only reports absolute pins against the king. A piece pinned to a queen or rook is a common lesson. |
| **Position tools for drills** | Low to medium | Low | Mirror, flip colours, rotate; FEN normalisation. |
| **Chess960** | Low | Medium | Needs castling rules and FEN (X-FEN or Shredder) changes; the perft tests would need Chess960 reference counts. |
