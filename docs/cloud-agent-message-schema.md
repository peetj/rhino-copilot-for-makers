# Cloud Agent Message Schema

This document defines the first shared wire contract between:

- the Rhino plugin
- the Cloudflare orchestration layer

The contract is intentionally plan-oriented. Simple one-shot command requests are treated as a degenerate case of a plan with one step.

## Design rules

- The Rhino plugin is the only trusted executor.
- Cloud may propose plans and steps, but it cannot directly mutate Rhino.
- A dedicated interpretation stage must normalize free-text prompts before planning or execution.
- Each user turn is first routed into one of:
  - `informational`
  - `clarifying`
  - `single_action`
  - `multi_step_plan`
  - `unsafe_or_disallowed`
- Every payload includes `schema_version`.
- Every request includes a unique `request_id`.
- Every turn includes a unique `turn_id`.
- Every plan includes a unique `plan_id`.
- Every step includes a unique `step_id`.
- Every local execution attempt includes a unique `execution_id`.

## Request / response flow

### 1. Turn request

Plugin sends:

- identity envelope: tenant, user, session, document
- user message text
- recent conversation turns
- current Rhino context snapshot

Main type:

- `TurnRequest`

Conversation history is optional on the wire but should be supplied in practice so the planner can handle short follow-ups and clarifications.

### 2. Cloud response types

Cloud returns one of:

- `chat_response`
- `clarification_request`
- `plan_response`
- `step_request`
- `plan_progress`
- `plan_completed`
- `error_response`

Main type:

- `TurnResponse`

### 2a. Interpretation payload

Before any Rhino execution is allowed, the interpreter must return:

- `primary_intent`
- `execution_readiness`
- `confidence`
- `operations`
- `missing_inputs`
- `assumptions`

This payload now lives on `TurnResponse.interpretation`.

Execution policy:

- `ready_to_plan`: planner may build executable steps
- `needs_clarification`: plugin must not execute; ask for missing values
- `informational_only`: answer normally without execution
- `unsafe`: reject or escalate

### 3. Approval

When a plan requires approval, the plugin returns:

- `ApprovalDecisionRequest`

### 4. Step execution events

During local execution, the plugin streams step lifecycle events:

- `StepExecutionEventRequest`

### 5. Step execution results

When a step finishes, the plugin sends:

- `StepExecutionResultRequest`

## Step model

Each plan step may be one of:

- `native_command`
- `custom_command`
- `direct_geometry_action`
- `user_interaction_step`
- `validation_step`

Each step also carries a strategy:

- `interactive_native_command`
- `scripted_native_command`
- `interactive_custom_command`
- `deterministic_geometry_write`

## First realistic example

User prompt:

`Create a centered rectangle, fillet the corners, then extrude it 3 mm`

Expected route:

- `multi_step_plan`

Expected plan:

1. start Rhino `Rectangle` interactively
2. run `FilletCorners` with a known radius
3. run `ExtrudeCrv` with `Solid=Yes`

Notes:

- Step 1 is interactive and pauses for viewport input.
- Step 2 depends on the object created by Step 1.
- Step 3 depends on the result of Step 2.

## Local executor policy

The plugin must reject any step if:

- the step type is unknown
- the strategy is unsupported
- the command is not locally allowlisted
- approval has not been granted
- Rhino is in an invalid command state for that step

This is the main safety boundary of the system.

## Files

- C# DTOs: `Contracts/CopilotWireProtocol.cs`
- TypeScript interfaces: `contracts/rhino-copilot-protocol.ts`
- Interpreter interface: `Services/IIntentInterpreter.cs`
- Cloud-backed interpreter: `Services/CloudIntentInterpreter.cs`
- Default heuristic implementation: `Services/HeuristicIntentInterpreter.cs`
- Fallback chain: `Services/CompositeIntentInterpreter.cs`
