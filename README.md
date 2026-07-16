# CleanPull M15 Pro — OpenCode starter

This package configures a modular, testable C# cBot workflow for VS Code and OpenCode. It intentionally contains no trading implementation.

## Order of use
1. Put the source PDF in `docs/reference/`.
2. Run `/bootstrap`.
3. Normalize and approve the rules in `docs/specification.md` and `docs/traceability-matrix.md`.
4. Run layer commands in order: domain, data, signal, risk, execution, state.
5. Run `/verify` after each layer, then `/polish`, and finally `/release`.

Release Build: `dotnet restore` then `dotnet build --configuration Release`.
The expected cTrader artifact is `.algo`; release may copy it to `artifacts/release/` only after all gates pass.
