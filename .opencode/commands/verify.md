---
description: Restore, Release Build, fix scoped errors and verify algo output
agent: implementer
---
This is verification, not feature work. Run `dotnet restore` and `dotnet build --configuration Release`. Fix only certain Build issues with the smallest patch and repeat. Verify the expected `.algo` exists. Do not refactor or add features. If Build fails, block release and report the blocker.
