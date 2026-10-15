# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

`E2e4.Chess.Logic` is the chess rules library (NuGet package of the same name, MIT), split out of
<https://github.com/th3snark/E2e4.Chess>. It is consumed there as a package (pinned in that repo's `Directory.Packages.props`): change it
here, release it with a `vX.Y.Z` tag, then bump the version in E2e4.Chess.

## Layout

- `src/E2e4.Chess.Logic/` — the library: bitboards (`Bitboards/`), move generation, FEN parsing, Zobrist hashing and the `Position` model
  (partial classes `Position.cs`, `.ChessLogic`, `.MoveGeneration`, `.MoveCompletion`, `.FEN`, `.Zobrist`), `Move`, `Piece`/`Colour`/`Square`,
  and `PositionSetup` (lenient, editable, validating position; `Validate()` lists `PositionProblem`s, `ToPosition()` gives the playable one).
  `Polyfills.cs` supplies `[NotNullWhen]` and friends for the old TFMs.
- `tests/E2e4.Chess.Logic.Test/` — xUnit tests (net10.0), including perft node counts for six reference positions: treat any change in those
  counts as a move-generation bug. `PortableBitOperationsUnitTest` checks the portable bit tricks against the intrinsics, because the
  tests only run on net10.0 while the library also compiles for net45/netstandard.
- `assets/icon.png` / `icon.svg` — the package icon: the E2e4.Chess base icon plus the Logic badge. It is built by
  `Branding/Build-Icons.ps1` in the E2e4.Chess repo (badge `Branding/badges/E2e4.Chess.Logic.svg`); never draw a standalone icon.

## Rules

- Multi-targets `net45;netstandard2.0;netstandard2.1;net10.0`: keep code compatible with the lowest TFM (no newer APIs) unless you narrow
  `TargetFrameworks`. `BitOperations` is used under `#if NET`, `PortableBitOperations` everywhere else.
- No dependencies, no WPF/WinForms/`System.Drawing`, no I/O. CI fails if the library gains a package dependency.
- Public surface is small on purpose (`Position`, `Move`, `Piece`/`Colour`/`Square`, `PositionSetup`, `CastlingRights`, `PositionProblem`,
  `AlgebraicNotationFormat`); `Bitboard`, `HashEntry`, `HashTable` and the raw board arrays are `internal` (tests use `InternalsVisibleTo`).
- Nullable is on and warnings are errors: the tree must build with 0 warnings. File-scoped namespaces are enforced by `.editorconfig`.
- Source files use CRLF line endings (the editor tools tend to write LF; convert before committing, `git ls-files --eol` shows the state).
- Fixtures and test data stay synthetic or public facts; keep the parser robust (bad FEN returns false, never throws or loops).

## Build and test

```
dotnet build E2e4.Chess.Logic.slnx
dotnet test E2e4.Chess.Logic.slnx
```

CI (`.github/workflows/ci.yml`) runs the tests on `ubuntu-24.04`, `ubuntu-26.04` (pinned while GitHub moves `ubuntu-latest`; drop 24.04
afterwards) and `windows-latest`, which also checks whitespace formatting (`dotnet format whitespace --verify-no-changes`).

## Releasing

Pushing a `vX.Y.Z` tag runs `.github/workflows/publish.yml`: tests, pack and push to nuget.org via Trusted Publishing (no API key; the policy
names this repo, `publish.yml` and the package glob `E2e4.Chess.Logic*`; the repo secret `NUGET_USER` is the nuget.org user name). **Only tag when
the owner says so**: nuget.org cannot delete a version, only unlist it. A manual run (`gh workflow run publish.yml --ref master`) only checks the
login.
