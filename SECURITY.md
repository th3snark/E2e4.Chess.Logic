# Security policy

This library parses untrusted text (FEN strings and move lists), so reports are welcome.

Please report a vulnerability privately through GitHub: **Security > Report a vulnerability** on
<https://github.com/th3snark/E2e4.Chess.Logic>. Do not open a public issue for it. Include the version, the input that
triggers it and a minimal reproduction. You can expect an acknowledgement within a few days.

In scope: crashes, hangs, out-of-range access or unbounded memory use on crafted FEN strings or move sequences, and a
position that is accepted as legal but breaks the rules of chess. Out of scope: anything that needs a modified build.
