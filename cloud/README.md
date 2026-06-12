# Cloud Agent Workspace

This folder is the future Cloudflare-hosted orchestration layer for Rhino Copilot.

## Rules

- Commit `cloud/.env.example`, never commit a real `.env`.
- The Rhino plugin is the only trusted executor.
- Cloud agents may interpret, route, critique, compile, and audit, but must not directly mutate Rhino.
- Shared schemas belong in the repo-level `Contracts/` and `docs/` folders, not duplicated per agent.

## Intended layout

- `agents/orchestrator/`
- `agents/rhino-planner/`
- `agents/plan-critic/`
- `agents/execution-compiler/`
- `shared/`
- `worker/`

## Runtime model

1. Rhino plugin sends turn + context to Cloudflare.
2. Orchestrator decides chat vs execution.
3. Rhino Planner interprets user intent semantically with LLM support.
4. Plan Critic checks safety, completeness, and executor compatibility.
5. Execution Compiler converts semantic actions into executor-ready steps.
6. Plugin executes approved steps locally and streams results back.

## Local configuration

Create `cloud/.env` from `cloud/.env.example`.

Recommended first variables:

- `OPENAI_API_KEY`
- `OPENAI_BASE_URL`
- `OPENAI_MODEL`
- `PLUGIN_SHARED_SECRET`

## Current status

This is a scaffold only. The repo does not yet contain the deployed Cloudflare worker implementation.

