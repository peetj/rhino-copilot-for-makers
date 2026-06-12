import {
  SCHEMA_VERSION,
  type RouteMode,
  type TurnRequest,
  type TurnResponse
} from "../../../Contracts/rhino-copilot-protocol";

function looksExecutable(message: string): boolean {
  return /\b(create|make|draw|put|place|add|move|rotate|scale|extrud\w*|fillet|circle|rectangle|sphere|box|cylinder|line|surface|solid)\b/i.test(message);
}

export async function handleTurn(request: TurnRequest): Promise<TurnResponse> {
  const routeMode: RouteMode = looksExecutable(request.turn.message_text)
    ? "single_action"
    : "informational";

  return {
    schema_version: SCHEMA_VERSION,
    response_type: "chat_response",
    request_id: request.request_id,
    turn_id: request.turn.turn_id,
    routing: {
      mode: routeMode,
      confidence: 0.2,
      reason: "Cloud orchestrator scaffold is live, but planner/critic/execution compiler are not implemented yet."
    },
    message: {
      role: "assistant",
      text: routeMode === "single_action"
        ? "Cloud orchestrator is connected. Execution routing will move here next, but the planner and compiler are still scaffolded only."
        : "Cloud orchestrator is connected. Informational and execution routing will be implemented here."
    }
  };
}

