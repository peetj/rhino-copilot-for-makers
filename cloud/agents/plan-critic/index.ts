import type { IntentInterpretationPayload, InterpretedOperationPayload, MissingInputPayload, RiskLevel, TurnRequest } from "../../../Contracts/rhino-copilot-protocol";
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
  let interpretation = toIntentInterpretation(plannerOutput);
  let missingInputs = interpretation.missing_inputs ?? [];
  let operations = (interpretation.operations ?? []).filter(isUsableOperation);

  if (operations.length === 0 && (interpretation.execution_readiness === "ready_to_plan" || interpretation.execution_readiness === "needs_clarification")) {
    const recovered = recoverSupportedOperations(request.turn.message_text, request.rhino_context.document_units);
    if (recovered) {
      interpretation = {
        ...interpretation,
        primary_intent: interpretation.primary_intent?.trim() || recovered.primary_intent,
        execution_readiness: recovered.missing_inputs.length > 0 ? "needs_clarification" : "ready_to_plan",
        confidence: Math.max(interpretation.confidence ?? 0, 0.86),
        operations: recovered.operations,
        missing_inputs: recovered.missing_inputs.length > 0 ? recovered.missing_inputs : [],
        assumptions: [...new Set([...(interpretation.assumptions ?? []), ...recovered.assumptions])]
      };
      missingInputs = interpretation.missing_inputs ?? [];
      operations = interpretation.operations ?? [];
    }
  }

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

function recoverSupportedOperations(text: string, units: string): {
  primary_intent: string;
  operations: InterpretedOperationPayload[];
  missing_inputs: MissingInputPayload[];
  assumptions: string[];
} | null {
  const normalized = text.toLowerCase();
  const hasRectangleIntent = /\brect(?:angle)?\b/i.test(normalized);
  const hasCircleIntent = /\bcircl[e]?\b/i.test(normalized);
  const hasExtrudeIntent = /\bextrud\w*\b/i.test(normalized) || /\b(?:tall|high)\b/i.test(normalized);
  const hasFilletIntent = hasRectangleIntent && /\bfillet\b/i.test(normalized);

  if (!hasRectangleIntent && !hasCircleIntent) {
    return null;
  }

  const operations: InterpretedOperationPayload[] = [];
  const missingInputs: MissingInputPayload[] = [];
  const assumptions: string[] = [];
  const centered = /\bcenter(?:ed|red)?\b/i.test(normalized);

  if (hasRectangleIntent) {
    const size = tryParseRectangleSize(text);
    if (!centered) {
      assumptions.push("If not otherwise specified, create the rectangle from the active CPlane origin.");
    }

    if (!size) {
      missingInputs.push({
        name: "rectangle_size",
        type: "2d_size",
        unit: units,
        required: true
      });
    } else {
      operations.push({
        operation_id: "op_create_rectangle_profile",
        action: "create_rectangle_profile",
        target: "active_cplane",
        depends_on: [],
        parameters: [
          parameter("width", size.width, units, String(size.width)),
          parameter("height", size.height, units, String(size.height)),
          parameter("centered", centered, null, centered ? "centered" : "origin")
        ],
        confidence: 0.98,
        can_execute_deterministically: true
      });
    }
  }

  if (hasCircleIntent) {
    const radius = tryParseCircleRadius(text);
    const center = tryParsePoint2(text) ?? { x: 0, y: 0 };

    assumptions.push(center.x === 0 && center.y === 0
      ? "If not otherwise specified, create the circle at the active CPlane origin."
      : "Create the circle on the active CPlane using the supplied center point.");

    if (!radius) {
      missingInputs.push({
        name: "circle_radius",
        type: "distance",
        unit: units,
        required: true
      });
    } else {
      operations.push({
        operation_id: "op_create_circle_profile",
        action: "create_circle_profile",
        target: "active_cplane",
        depends_on: [],
        parameters: [
          parameter("radius", radius, units, String(radius)),
          parameter("center_x", center.x, units, String(center.x)),
          parameter("center_y", center.y, units, String(center.y))
        ],
        confidence: 0.96,
        can_execute_deterministically: true
      });
    }
  }

  if (hasFilletIntent) {
    const filletRadius = tryParseFilletRadius(text);
    if (!filletRadius) {
      missingInputs.push({
        name: "fillet_radius",
        type: "distance",
        unit: units,
        required: true
      });
    } else {
      const dependency = operations.some(operation => operation.operation_id === "op_create_rectangle_profile")
        ? "op_create_rectangle_profile"
        : "op_create_circle_profile";

      operations.push({
        operation_id: "op_fillet_profile_corners",
        action: "fillet_profile_corners",
        target: "latest_profile",
        depends_on: [dependency],
        parameters: [
          parameter("radius", filletRadius, units, String(filletRadius))
        ],
        confidence: 0.94,
        can_execute_deterministically: true
      });
    }
  }

  if (hasExtrudeIntent) {
    const extrudeHeight = tryParseExtrudeHeight(text);
    if (!extrudeHeight) {
      missingInputs.push({
        name: "extrude_height",
        type: "distance",
        unit: units,
        required: true
      });
    } else {
      const dependency = operations.some(operation => operation.operation_id === "op_fillet_profile_corners")
        ? "op_fillet_profile_corners"
        : operations.some(operation => operation.operation_id === "op_create_rectangle_profile")
          ? "op_create_rectangle_profile"
          : "op_create_circle_profile";

      operations.push({
        operation_id: "op_extrude_profile",
        action: "extrude_profile",
        target: "latest_profile",
        depends_on: [dependency],
        parameters: [
          parameter("distance", extrudeHeight, units, String(extrudeHeight)),
          parameter("cap", true, null, "solid")
        ],
        confidence: 0.96,
        can_execute_deterministically: true
      });
    }
  }

  if (operations.length === 0 && missingInputs.length === 0) {
    return null;
  }

  return {
    primary_intent: operations.some(operation => operation.action === "extrude_profile")
      ? "create_profile_then_extrude"
      : operations.some(operation => operation.action === "fillet_profile_corners")
        ? "create_and_finish_profile"
        : operations.some(operation => operation.action === "create_circle_profile")
          ? "create_circle_profile"
          : "create_rectangle_profile",
    operations,
    missing_inputs: missingInputs,
    assumptions
  };
}

function parameter(name: string, value: unknown, unit: string | null, sourceText: string | null) {
  return {
    name,
    value,
    unit,
    source_text: sourceText,
    confidence: 0.98
  };
}

function tryParseRectangleSize(text: string): { width: number; height: number } | null {
  const match = text.match(/(?<w>\d+(?:\.\d+)?)\s*(?:x|×|by)\s*(?<h>\d+(?:\.\d+)?)/i);
  if (!match?.groups) {
    return null;
  }

  const width = Number(match.groups.w);
  const height = Number(match.groups.h);
  return width > 0 && height > 0 ? { width, height } : null;
}

function tryParseExtrudeHeight(text: string): number | null {
  const patterns = [
    /extrud\w*\s+(?:it\s+)?(?:to|by)?\s*(?<d>\d+(?:\.\d+)?)/i,
    /height\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)/i,
    /thick(?:ness)?\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)/i,
    /make\s+it\s+(?<d>\d+(?:\.\d+)?)\s*(?:mm|cm|m|in|inch|inches)?\s*(?:tall|high)/i,
    /(?<d>\d+(?:\.\d+)?)\s*(?:mm|cm|m|in|inch|inches)?\s*(?:tall|high)\b/i
  ];

  for (const pattern of patterns) {
    const match = text.match(pattern);
    const value = Number(match?.groups?.d);
    if (value > 0) {
      return value;
    }
  }

  return null;
}

function tryParseFilletRadius(text: string): number | null {
  const patterns = [
    /fillet(?:\s+radius)?(?:\s+of|\s+to|\s*=)?\s*(?<r>\d+(?:\.\d+)?)/i,
    /fillet(?:\s+the)?(?:\s+\w+){0,4}\s+to\s+(?<r>\d+(?:\.\d+)?)/i,
    /radius\s+(?:of|to|=)?\s*(?<r>\d+(?:\.\d+)?)/i
  ];

  for (const pattern of patterns) {
    const match = text.match(pattern);
    const value = Number(match?.groups?.r);
    if (value > 0) {
      return value;
    }
  }

  return null;
}

function tryParseCircleRadius(text: string): number | null {
  const radiusMatch = text.match(/radius\s+(?:of|to|=)?\s*(?<r>\d+(?:\.\d+)?)/i)
    ?? text.match(/\br(?:adius)?\s*(?<r>\d+(?:\.\d+)?)/i);
  const radius = Number(radiusMatch?.groups?.r);
  if (radius > 0) {
    return radius;
  }

  const diameter = Number(text.match(/diameter\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)/i)?.groups?.d);
  return diameter > 0 ? diameter / 2 : null;
}

function tryParsePoint2(text: string): { x: number; y: number } | null {
  const match = text.match(/\bat\s*(?<x>-?\d+(?:\.\d+)?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)/i);
  if (!match?.groups) {
    return null;
  }

  const x = Number(match.groups.x);
  const y = Number(match.groups.y);
  return Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null;
}
