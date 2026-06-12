# Agents

Each subfolder represents one cloud responsibility boundary.

Keep the agent set small:

- `orchestrator`
- `rhino-planner`
- `plan-critic`
- `execution-compiler`

Do not duplicate schemas inside each agent. Shared contracts stay in `Contracts/` and `docs/`.

