import {
  SCHEMA_VERSION,
  type IntentInterpretationPayload,
  type MissingInputPayload,
  type RouteMode,
  type TurnRequest,
  type TurnResponse
} from "../../Contracts/rhino-copilot-protocol";

export function buildChatResponse(
  request: TurnRequest,
  routeMode: RouteMode,
  text: string,
  reason: string,
  interpretation?: IntentInterpretationPayload | null
): TurnResponse {
  return {
    schema_version: SCHEMA_VERSION,
    response_type: "chat_response",
    request_id: request.request_id,
    turn_id: request.turn.turn_id,
    routing: {
      mode: routeMode,
      confidence: interpretation?.confidence ?? null,
      reason
    },
    message: {
      role: "assistant",
      text
    },
    interpretation: interpretation ?? null
  };
}

export function buildClarificationResponse(
  request: TurnRequest,
  text: string,
  reason: string,
  interpretation: IntentInterpretationPayload,
  missingInputs: MissingInputPayload[]
): TurnResponse {
  return {
    schema_version: SCHEMA_VERSION,
    response_type: "clarification_request",
    request_id: request.request_id,
    turn_id: request.turn.turn_id,
    routing: {
      mode: "clarifying",
      confidence: interpretation.confidence ?? null,
      reason
    },
    message: {
      role: "assistant",
      text
    },
    interpretation,
    missing_inputs: missingInputs
  };
}
