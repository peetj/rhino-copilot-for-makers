# Execution Compiler

## Responsibility

- Convert semantic actions into executor-ready structured steps
- Bind parameters
- Attach preconditions, assumptions, and risk metadata

## Preference

Prefer deterministic code here rather than another LLM call.

This layer is where semantic plans are translated into the strict local executor contract used by the Rhino plugin.

