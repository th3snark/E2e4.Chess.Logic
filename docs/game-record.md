# Game record design

How a whole game (its moves, variations and annotations) should be held, so that clicking any move shows that position at once. This note
records the research and the decisions behind the game-record rows in [roadmap.md](roadmap.md). Nothing here is implemented yet.

## What other projects do

| Project | Structure | Lesson |
|---|---|---|
| [WinBoard/XBoard](https://sources.debian.org/data/main/x/xboard/4.9.1-5/backend.c) | Parallel arrays indexed by ply (`boards[]`, `moveList[]`, `parseList[]`, `commentList[]`) with `currentMove`, `forwardMostMove` and `backwardMostMove`. Variations are a stack of at most 10 stashed tails, not a tree. | A full position per ply makes jumping instant, but arrays cannot hold a tree. |
| [python-chess](https://raw.githubusercontent.com/niklasf/python-chess/master/chess/pgn.py) | `GameNode` with parent, move, ordered `variations`, comment, starting comment and NAGs. `variations[0]` is the main line. The board is rebuilt by replaying from the root. | The right shape: one node tree, with promote and demote. |
| [Lichess](https://raw.githubusercontent.com/lichess-org/lila/master/modules/tree/src/main/tree.scala) | Tree nodes store the move and the `fen`. A node's address is the concatenated two-character ids of the moves leading to it. `children[0]` is the main line. | Keep a FEN on every node, so a click hands the board a FEN directly. |
| [Scid](https://raw.githubusercontent.com/benini/scid/master/src/movetree.h) / ChessBase | Linked move nodes in memory. On disk, about one byte per move plus marker tokens for variation start/end, comments and NAGs. | Compact binary encoding pays off only at database scale. |
| [Polyglot](http://hgm.nubati.net/book_format.html), [Lichess explorer](https://github.com/lichess-org/lila-openingexplorer) | Keyed by position hash, so transpositions merge into one entry (a graph, not a tree). | Opening databases are a separate structure. A position key on each game node is the link between the two. |
| FICS ([smoves](https://www.freechess.org/Help/HelpFiles/smoves.html), [style12](https://www.freechess.org/Help/HelpFiles/style12.html)) | `moves`/`smoves` print a header and a table of SAN moves with elapsed times. Style 12 sends a full board and the SAN of each move as it is played. | SAN plus a time per ply maps straight onto the tree's main line. |

The [PGN standard](https://www.saremba.de/chessgml/standards/pgn/pgn-complete.htm) defines how these travel as text:

- `( … )` holds an alternative to the move just before it, and variations can nest.
- `{ … }` holds a comment.
- `$n` is a NAG; `$1` to `$6` are `!`, `?`, `!!`, `??`, `!?` and `?!`.
- Commands are embedded in comments as `[%name args]`: `%clk`, `%emt`, and the `%cal`/`%csl` arrows and squares described in the
  [supplement](https://raw.githubusercontent.com/ornicar/pgn4web/master/pgn-standard-proposed-supplement.txt).

## Decisions

1. **Use one tree, not a main line plus a separate variation structure.**
   - Each node has an ordered list of children. `Children[0]` continues the main line and the rest are variations.
   - A plain game is a tree with no branches, so FICS games, PGN games and annotated studies share one type.
2. **Store moves and cache positions; don't store a list of FENs.**
   - The source of truth on each node is its move (the existing packed `int`).
   - When a node is added, the builder already holds the `Position`, so it also stores the node's FEN, Zobrist key and SAN then.
   - A click costs O(1): the board gets `node.Fen`, and `Move.From`/`Move.To` give the last-move squares.
   - That is roughly 150–250 bytes per node, which is small even with many variations.
3. **Get a `Position` by replaying.**
   - A `Position` carries its undo history (about 20 KB at `HalfMovesLimit` 1024), so it isn't cached per node.
   - `node.ToPosition()` replays the moves from the root. That is fast, and it keeps repetition and 50-move history correct for playing on
     or for the analysis queries.
4. **Use the in-memory move as is, and PGN for interchange.** A compact binary encoding (a 16-bit move plus tokens, as Scid does) is optional
   and only worth having for storing many games.
5. **Keep opening books separate.**
   - A book is keyed by position and merges transpositions; a game tree keeps move order.
   - The per-node `ZobristKey` links the two.
   - Polyglot books use their own fixed random table, so reading one would need a separate Polyglot key.
6. **Store comments verbatim.**
   - PGN round-trips without loss.
   - Logic lists the embedded `[%name args]` commands and promotes `%clk`/`%emt` to typed times.
   - Drawing `%cal`/`%csl` is the UI's job.

## API sketch (E2e4.Chess.Logic)

Names are provisional.

- **SAN reader.** Resolves a SAN string against the legal moves.
  - It tolerates a missing `+`/`#` and accepts `0-0` for castling.
  - It returns `Move.Invalid` for an ambiguous or illegal move.
- **`GameRecord`** holds:
  - the tag pairs (ordered, with Seven Tag Roster helpers);
  - the start position (a FEN, or the standard start);
  - the `Root` node;
  - the result.
- **`GameNode`** state: `Parent`, `Move`, `Children`, `Ply`, `San`, `Fen`, `ZobristKey`, `Comment`, `PreComment` (before a variation's
  first move), `Nags`, `Clock` and `MoveTime` (nullable `TimeSpan`).
- **`GameNode`** operations:
  - `AddMove(int)` and `AddMove(string san)`. These reuse an existing child for the same move rather than duplicating it.
  - `PromoteToMainLine`, `Promote`, `Demote` and `Remove`.
  - `MainLine()` and `ToPosition()`.
  - A path address (child indices, e.g. `0.0.2.0`) for selection, undo and serialization.
- **PGN.**
  - Reading parses a string that may hold several games, with variations, comments, NAGs and clocks.
  - Reading is lenient: an unreadable move ends that line and is recorded as an error. It never throws.
  - Writing round-trips what was read.
  - No file I/O.
- **No change notifications.** These stay plain classes; a UI wraps them.

Compare nodes by `ZobristKey` or FEN, not `Position.Equals`: `Position` equality includes move history, so the same board reached by a
different move order compares unequal.

## What goes where

| Library | Responsibility |
|---|---|
| E2e4.Chess.Logic | SAN reading, `GameRecord`/`GameNode`, PGN reading and writing, comment command listing. No I/O, no UI, and net45-compatible. |
| E2e4.Chess.Controls | A move list / notation control with a selected node, and navigation (start, back, forward, end, next/previous variation). It binds `FenString` and the last-move squares to the selected node, and branches through `AddMove` when a move is played at an earlier node. It draws `%cal`/`%csl` as arrows and circles. Its keyboard shortcuts must not clash with the board's arrow, Home and End cursor keys. |
| E2e4.Chess.Fics.Client | Parses `moves`/`smoves` output into plain records: header, players, ratings, time control, `(san, elapsed)` per ply, and the result with its reason. It stays free of Logic and of chess rules. |
| Consumers (demos, glue) | Turn FICS records into a `GameRecord` with `AddMove(san)`, setting `MoveTime` from the elapsed times. During a live game, append each style 12 move the same way, and check `node.Fen` against `Style12.ToFen()`. |

## FICS mapping

Shortened from the `smoves` help page:

```
DAV (1696) vs. Hawk (1789) --- Wed Apr 10, 20:12 CDT
Rated Blitz match, initial time: 3 minutes, increment: 0 seconds.

Move  DAV                Hawk
----  ----------------   ----------------
  1.  c4      (0:01)     Nf6     (0:02)
  2.  g3      (0:01)     g6      (0:02)
      {Game drawn because both players ran out of time} 1/2-1/2
```

How each part maps:

| FICS output | GameRecord |
|---|---|
| Player line (names, ratings, date) | Tags: `White`, `Black`, `WhiteElo`, `BlackElo`, `Date` |
| Match line (rated/unrated, type, time control) | `TimeControl` and `Event` tags |
| Each move | A main-line node, with the elapsed time (`m:ss`, or `m:ss.mmm` with millisecond timing) as its `MoveTime` |
| Brace line | The result, and the reason as the `Termination` text |

A live `moves` reply is preceded by a `Movelist for game N:` line.

## Follow-ups in sibling repositories

These are listed here only; each belongs on that repository's own roadmap.

- **E2e4.Chess.Controls:** the notation control and navigation above, plus `%cal`/`%csl` drawing (already planned there).
- **E2e4.Chess.Fics.Client:** a parser for the `moves`/`smoves` output, with a fixture taken from a real session.
