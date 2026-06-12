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

Recommended worker-side secrets:

- `OPENAI_API_KEY`
- `OPENAI_BASE_URL`
- `OPENAI_MODEL`
- `PLUGIN_SHARED_SECRET`

Recommended local Worker dev file:

- `cloud/.dev.vars`

Notes:

- `cloud/.dev.vars.example` is a template only. Keep real values in the untracked `cloud/.dev.vars`.
- `CLOUDFLARE_WORKER_URL` is produced after deploy and then fed into the plugin config.
- If you already use `wrangler`, you typically do not need `CLOUDFLARE_ACCOUNT_ID` or `CLOUDFLARE_API_TOKEN` in this repo.
- Those credentials normally come from your existing Wrangler auth/session.
- The OpenAI key should usually live in Worker secrets or `.dev.vars`, not in the committed env template.

## Current status

The repo now contains a minimal Cloudflare worker scaffold:

- `GET /health`
- `POST /turn`

The current `/turn` route now runs the first real cloud pipeline:

1. `orchestrator` receives the turn.
2. `rhino-planner` uses the model to interpret natural language semantically.
3. `plan-critic` validates readiness, missing inputs, and current executor support.
4. `execution-compiler` converts supported semantic actions into plan steps for the plugin.

The current compiler target is intentionally narrow:

- `create_rectangle_profile`
- `create_circle_profile`
- `fillet_profile_corners`
- `extrude_profile`

That keeps the intelligence layer open-ended while keeping the local executor boundary strict.
