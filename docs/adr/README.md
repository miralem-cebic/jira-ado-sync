# Architecture decision records

Each record describes one decision: the context, what was decided, and what follows from it. An accepted record is
not edited; a decision that is replaced keeps its file, and its status points to the new record. New records:
`/add-adr <title>` in Claude Code, or copy the sections of an existing one.

The initial design (Azure Function, Table Storage, loop prevention, last write wins) is described in
[../DESIGN.md](../DESIGN.md). Decisions that change it get a record here.

| No. | Decision | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-dotnet-10-with-the-baseline-build-rules.md) | .NET 10 with the engineering-baseline build rules | Accepted |
