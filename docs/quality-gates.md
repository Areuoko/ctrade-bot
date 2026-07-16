# Quality gates

Every layer: declared scope and Rule IDs; dependency direction preserved; no future-only code; diff self-reviewed; relevant Build passes.

Release: `dotnet restore` passes; Release Build has zero errors; exactly one expected current `.algo` exists; no critical review finding; checksum and release report are generated. Any failure stops publication.
