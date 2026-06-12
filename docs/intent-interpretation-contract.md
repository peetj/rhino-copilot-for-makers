# Intent Interpretation Contract

This contract sits between free-text user prompts and any Rhino execution plan.

Its purpose is to prevent the executor from inferring meaning directly from raw user text.

## Minimum required fields

- `primary_intent`
  - Normalized label for the workflow, for example `create_profile_then_extrude`.
- `execution_readiness`
  - One of:
    - `informational_only`
    - `ready_to_plan`
    - `needs_clarification`
    - `unsafe`
- `confidence`
  - Interpreter confidence in the normalization.
- `operations`
  - Ordered operation list with dependencies and normalized parameters.
- `missing_inputs`
  - Required values that block safe execution.
- `assumptions`
  - Defaults the planner is allowed to rely on.

## Operation shape

Each interpreted operation should define:

- `operation_id`
- `action`
- `target`
- `depends_on`
- `parameters`
- `can_execute_deterministically`

## Example

User prompt:

`Create a rect 80x25 and extrude it by 25mm`

Interpretation result:

- `primary_intent`: `create_profile_then_extrude`
- `execution_readiness`: `ready_to_plan`
- `operations`:
  - `create_rectangle_profile`
  - `extrude_profile`
- `missing_inputs`: none
- `assumptions`:
  - create from active CPlane origin unless user says otherwise

## Current execution path

Primary interpretation now happens in the cloud worker planner/critic pipeline.

The plugin still keeps a narrow local heuristic fallback for development and safety-bound testing. That fallback currently supports only a small subset of workflows:

- `rect` / `rectangle`
- `circle`
- optional `fillet`
- optional `extrude`

Files:

- `cloud/agents/rhino-planner/index.ts`
- `cloud/agents/plan-critic/index.ts`
- `cloud/agents/execution-compiler/index.ts`
- `Services/HeuristicIntentInterpreter.cs`
- `Services/CopilotCloudClient.cs`
