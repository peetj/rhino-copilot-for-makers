import type { IntentInterpretationPayload, InterpretedOperationPayload, RiskLevel, TurnRequest } from "../../../Contracts/rhino-copilot-protocol";
import type { CriticAgentOutput, PlannerAgentOutput } from "../../shared/agent-types";
import { toIntentInterpretation } from "../../shared/agent-types";

const COMPILER_SUPPORTED_ACTIONS = new Set([
  "create_rectangle_profile",
  "create_circle_profile",
  "fillet_profile_corners",
  "extrude_profile"
]);

export function critiquePlan(
  request: TurnRequest,
  plannerOutput: PlannerAgentOutput
): CriticAgentOutput {
  const interpretation = toIntentInterpretation(plannerOutput);
  const missingInputs = interpretation.missing_inputs ?? [];
  const operations = (interpretation.operations ?? []).filter(isUsableOperation);
  const invalidOperationCount = (interpretation.operations ?? []).length - operations.length;
  const unsupportedActions = operations
    .map(operation => operation.action)
    .filter(action => !COMPILER_SUPPORTED_ACTIONS.has(action));

  if (request.rhino_context.command_state.is_command_running) {
    return {
      disposition: "unsafe",
      route_mode: "unsafe_or_disallowed",
      reason: "Rhino is already running a command.",
      response_text: `Rhino is currently running ${request.rhino_context.command_state.active_command_name || "another command"}. Finish or cancel it before I start a new plan.`,
      interpretation,
      missing_inputs: [],
      unsupported_actions: [],
      risk_level: "medium"
    };
  }

  if (!request.turn.client_capabilities.supports_local_execution) {
    return {
      disposition: "informational",
      route_mode: "informational",
      reason: "Client cannot execute local Rhino steps.",
      response_text: plannerOutput.response_text,
      interpretation,
      missing_inputs: [],
      unsupported_actions: [],
      risk_level: "low"
    };
  }

  switch (interpretation.execution_readiness) {
    case "informational_only":
      return {
        disposition: "informational",
        route_mode: "informational",
        reason: "Planner classified the turn as advisory or explanatory.",
        response_text: plannerOutput.response_text,
        interpretation,
        missing_inputs: [],
        unsupported_actions: [],
        risk_level: "low"
      };

    case "needs_clarification":
      return {
        disposition: "clarify",
        route_mode: "clarifying",
        reason: "Planner identified missing inputs before safe execution.",
        response_text: buildClarificationText(plannerOutput.response_text, missingInputs),
        interpretation,
        missing_inputs: missingInputs,
        unsupported_actions: [],
        risk_level: "low"
      };

    case "unsafe":
      return {
        disposition: "unsafe",
        route_mode: "unsafe_or_disallowed",
        reason: "Planner marked the request unsafe or disallowed.",
        response_text: plannerOutput.response_text,
        interpretation,
        missing_inputs: [],
        unsupported_actions: [],
        risk_level: "high"
      };

    case "ready_to_plan":
      if (operations.length == 0) {
        return {
          disposition: "clarify",
          route_mode: "clarifying",
          reason: "Planner marked the turn executable but did not return any usable operations.",
          response_text: plannerOutput.response_text?.trim()
            || "I understood that as an executable Rhino request, but I need to restate the action more concretely before I can build a plan.",
          interpretation: {
            ...interpretation,
            operations
          },
          missing_inputs: missingInputs,
          unsupported_actions: [],
          risk_level: "low"
        };
      }

      if (invalidOperationCount > 0) {
        return {
          disposition: "clarify",
          route_mode: "clarifying",
          reason: "Planner returned malformed operations.",
          response_text: "I partially understood the Rhino request, but part of the planned action came back malformed. Please restate it once and I will rebuild the plan.",
          interpretation: {
            ...interpretation,
            operations
          },
          missing_inputs: missingInputs,
          unsupported_actions: [],
          risk_level: "low"
        };
      }

      if (unsupportedActions.length > 0) {
        return {
          disposition: "unsupported",
          route_mode: operations.length > 1 ? "multi_step_plan" : "single_action",
          reason: "Planner understood the request but the local execution compiler does not support all operations yet.",
          response_text: buildUnsupportedText(plannerOutput, unsupportedActions),
          interpretation,
          missing_inputs: [],
          unsupported_actions: unsupportedActions,
          risk_level: computeRiskLevel(operations.length)
        };
      }

      return {
        disposition: "compile",
        route_mode: operations.length > 1 ? "multi_step_plan" : "single_action",
        reason: "Planner output is complete and compatible with the current execution compiler.",
        response_text: plannerOutput.response_text,
        interpretation,
        missing_inputs: [],
        unsupported_actions: [],
        risk_level: computeRiskLevel(operations.length)
      };
  }
}

function buildClarificationText(responseText: string, missingInputs: IntentInterpretationPayload["missing_inputs"]): string {
  if (responseText?.trim()) {
    return responseText.trim();
  }

  const names = (missingInputs ?? [])
    .map(input => input.name.replaceAll("_", " "))
    .join(", ");

  return `I understand the Rhino task, but I still need: ${names}.`;
}

function buildUnsupportedText(plannerOutput: PlannerAgentOutput, unsupportedActions: string[]): string {
  const readable = unsupportedActions
    .filter((action): action is string => typeof action === "string" && action.trim().length > 0)
    .map(action => action.replaceAll("_", " "))
    .join(", ");

  if (plannerOutput.response_text?.trim()) {
    return `${plannerOutput.response_text.trim()} I understood the intent, but the current local executor cannot run these actions yet: ${readable || "unknown actions"}.`;
  }

  return `I understood the Rhino intent, but the current local executor cannot run these actions yet: ${readable || "unknown actions"}.`;
}

function computeRiskLevel(operationCount: number): RiskLevel {
  if (operationCount >= 4) {
    return "medium";
  }

  return "low";
}

function isUsableOperation(operation: InterpretedOperationPayload): boolean {
  return !!operation
    && typeof operation.operation_id === "string"
    && operation.operation_id.trim().length > 0
    && typeof operation.action === "string"
    && operation.action.trim().length > 0
    && Array.isArray(operation.parameters)
    && Array.isArray(operation.depends_on);
}
