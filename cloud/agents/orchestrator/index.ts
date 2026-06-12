import {
  SCHEMA_VERSION,
  type TurnRequest,
  type TurnResponse
} from "../../../Contracts/rhino-copilot-protocol";
import { compileExecutionPlan } from "../execution-compiler/index";
import { critiquePlan } from "../plan-critic/index";
import { planTurn } from "../rhino-planner/index";
import type { Env } from "../../shared/env";
import { buildChatResponse, buildClarificationResponse } from "../../shared/response-builders";

export async function handleTurn(request: TurnRequest, env: Env): Promise<TurnResponse> {
  const plannerOutput = await planTurn(env, request);
  const criticOutput = critiquePlan(request, plannerOutput);

  switch (criticOutput.disposition) {
    case "informational":
      return buildChatResponse(
        request,
        criticOutput.route_mode,
        criticOutput.response_text,
        criticOutput.reason,
        criticOutput.interpretation
      );

    case "clarify":
      return buildClarificationResponse(
        request,
        criticOutput.response_text,
        criticOutput.reason,
        criticOutput.interpretation,
        criticOutput.missing_inputs
      );

    case "unsafe":
    case "unsupported":
      return buildChatResponse(
        request,
        criticOutput.route_mode,
        criticOutput.response_text,
        criticOutput.reason,
        criticOutput.interpretation
      );

    case "compile": {
      const compiled = compileExecutionPlan(
        request,
        criticOutput.interpretation,
        criticOutput.route_mode,
        criticOutput.risk_level
      );

      return {
        schema_version: SCHEMA_VERSION,
        response_type: "plan_response",
        request_id: request.request_id,
        turn_id: request.turn.turn_id,
        routing: {
          mode: compiled.route_mode,
          confidence: criticOutput.interpretation.confidence ?? null,
          reason: criticOutput.reason
        },
        message: {
          role: "assistant",
          text: compiled.response_text
        },
        interpretation: criticOutput.interpretation,
        plan: compiled.plan
      };
    }
  }
}
