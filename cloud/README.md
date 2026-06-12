# Cloud Agent Workspace

This folder is the future Cloudflare-hosted orchestration layer for Rhino Copilot.

## Rules

- Commit `cloud/.env.example`, never commit a real `.env`.
- Prefer `wrangler login` and Wrangler-managed secrets over storing provider keys in project env files.
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

Recommended plugin-facing variables:

- `CLOUDFLARE_WORKER_URL`
- `PLUGIN_SHARED_SECRET`

Recommended worker-side secrets:

- `OPENAI_API_KEY`
- `OPENAI_BASE_URL`
- `OPENAI_MODEL`

Recommended local Worker dev file:

- `cloud/.dev.vars`

Notes:

- If you already use `wrangler`, you typically do not need `CLOUDFLARE_ACCOUNT_ID` or `CLOUDFLARE_API_TOKEN` in this repo.
- Those credentials normally come from your existing Wrangler auth/session.
- The OpenAI key should usually live in Worker secrets or `.dev.vars`, not in the committed env template.

## Current status

This is a scaffold only. The repo does not yet contain the deployed Cloudflare worker implementation.
