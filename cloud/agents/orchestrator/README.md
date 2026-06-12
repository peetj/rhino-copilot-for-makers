# Orchestrator Agent

## Responsibility

- Own session-level routing
- Decide `informational` vs `clarifying` vs `execution` vs `unsafe`
- Gather recent history and Rhino context
- Call specialist agents in order
- Return the final payload to the plugin

## Inputs

- user turn
- recent session history
- Rhino context snapshot
- approval and execution events

## Outputs

- `TurnResponse`
- follow-up clarification
- execution-ready plan payload

