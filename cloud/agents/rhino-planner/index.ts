import type {
  ConversationMessagePayload,
  InterpretedOperationPayload,
  InterpretedParameterPayload,
  MissingInputPayload,
  TurnRequest
} from "../../../Contracts/rhino-copilot-protocol";
import type { PlannerAgentOutput } from "../../shared/agent-types";
import type { Env } from "../../shared/env";
import { createJsonCompletion } from "../../shared/openai";

const PLANNER_SYSTEM_PROMPT = [
  "You are the Rhino Planner agent for Nexgen Copilot for Rhino.",
  "Interpret the user's Rhino request semantically, tolerating typos, shorthand, and natural language.",
  "Return JSON only. No markdown. No commentary outside the JSON object.",
  "You must decide whether the turn is informational_only, ready_to_plan, needs_clarification, or unsafe.",
  "Use semantic operation names rather than raw Rhino macros.",
  "Supported operation families include create_rectangle_profile, create_circle_profile, create_line_curve, create_box_solid, create_cylinder_solid, create_sphere_solid, fillet_profile_corners, extrude_profile, move_objects, rotate_objects, scale_objects, boolean_union, boolean_difference, boolean_intersection, offset_curve, offset_surface, create_loft_surface, create_sweep_surface, create_patch_surface.",
  "If a normal Rhino advice question is being asked, set execution_readiness to informational_only, leave operations empty, and answer the question in response_text.",
  "If an executable request is missing required values, set execution_readiness to needs_clarification and list only the genuinely missing inputs.",
  "If execution_readiness is ready_to_plan or needs_clarification, operations must be an array reflecting the intended Rhino actions. Do not leave operations empty for executable requests.",
  "Make safe standard assumptions when appropriate and record them in assumptions.",
  "Use recent conversation and Rhino context to resolve short follow-ups like '2', 'centered', 'same again', or 'put it on top of that'.",
  "Output shape:",
  "{ primary_intent, execution_readiness, confidence, operations, missing_inputs, assumptions, response_text }"
].join(" ");

export async function planTurn(env: Env, request: TurnRequest): Promise<PlannerAgentOutput> {
  const userPrompt = [
    "Interpret this Rhino copilot turn.",
    "",
    "Current turn:",
    request.turn.message_text,
    "",
    "Recent conversation:",
    formatConversation(request.conversation),
    "",
    "Rhino context:",
    JSON.stringify(request.rhino_context, null, 2)
  ].join("\n");

  const llmOutput = await createJsonCompletion<PlannerAgentOutput>(env, {
    systemPrompt: PLANNER_SYSTEM_PROMPT,
    userPrompt
  });

  return normalizePlannerOutput(llmOutput, request);
}

function formatConversation(conversation?: ConversationMessagePayload[] | null): string {
  if (!conversation?.length) {
    return "(none supplied)";
  }

  return conversation
    .slice(-8)
    .map(message => `${message.role}: ${message.text}`)
    .join("\n");
}

function normalizePlannerOutput(
  output: PlannerAgentOutput,
  request: TurnRequest
): PlannerAgentOutput {
  const operations = output.operations ?? [];
  if (operations.length > 0) {
    return output;
  }

  if (output.execution_readiness !== "ready_to_plan" && output.execution_readiness !== "needs_clarification") {
    return output;
  }

  const recovered = recoverSupportedOperations(request.turn.message_text, request.rhino_context.document_units);
  if (!recovered) {
    return output;
  }

  return {
    ...output,
    primary_intent: output.primary_intent?.trim() || recovered.primary_intent,
    execution_readiness: recovered.missing_inputs.length > 0 ? "needs_clarification" : "ready_to_plan",
    confidence: Math.max(output.confidence ?? 0, 0.86),
    operations: recovered.operations,
    missing_inputs: recovered.missing_inputs.length > 0 ? recovered.missing_inputs : [],
    assumptions: mergeAssumptions(output.assumptions, recovered.assumptions),
    response_text: output.response_text?.trim() || recovered.response_text
  };
}

function recoverSupportedOperations(text: string, units: string): {
  primary_intent: string;
  operations: InterpretedOperationPayload[];
  missing_inputs: MissingInputPayload[];
  assumptions: string[];
  response_text: string;
} | null {
  const normalized = normalizeIntentText(text);
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
    primary_intent: buildPrimaryIntent(operations),
    operations,
    missing_inputs: missingInputs,
    assumptions,
    response_text: buildRecoveryResponse(operations, missingInputs, units)
  };
}

function buildRecoveryResponse(
  operations: InterpretedOperationPayload[],
  missingInputs: MissingInputPayload[],
  units: string
): string {
  if (missingInputs.length > 0) {
    const names = missingInputs.map(input => input.name.replaceAll("_", " ")).join(", ");
    return `I understand the Rhino workflow, but I still need: ${names}.`;
  }

  const rectangle = operations.find(operation => operation.action === "create_rectangle_profile");
  const circle = operations.find(operation => operation.action === "create_circle_profile");
  const extrude = operations.find(operation => operation.action === "extrude_profile");
  const fillet = operations.find(operation => operation.action === "fillet_profile_corners");

  const parts: string[] = [];
  if (rectangle) {
    parts.push(`Create a ${num(rectangle, "width")} by ${num(rectangle, "height")} ${units.toLowerCase()} rectangle`);
  } else if (circle) {
    parts.push(`Create a circle with radius ${num(circle, "radius")} ${units.toLowerCase()}`);
  }

  if (fillet) {
    parts.push(`fillet the corners to ${num(fillet, "radius")} ${units.toLowerCase()}`);
  }

  if (extrude) {
    parts.push(`extrude the closed profile ${num(extrude, "distance")} ${units.toLowerCase()} as a solid`);
  }

  return parts.join(", then ") + ".";
}

function buildPrimaryIntent(operations: InterpretedOperationPayload[]): string {
  if (operations.some(operation => operation.action === "extrude_profile")) {
    return "create_profile_then_extrude";
  }

  if (operations.some(operation => operation.action === "fillet_profile_corners")) {
    return "create_and_finish_profile";
  }

  if (operations.some(operation => operation.action === "create_circle_profile")) {
    return "create_circle_profile";
  }

  return "create_rectangle_profile";
}

function mergeAssumptions(existing?: string[] | null, recovered?: string[]): string[] {
  const merged = new Set<string>();
  for (const value of existing ?? []) {
    if (value?.trim()) {
      merged.add(value.trim());
    }
  }

  for (const value of recovered ?? []) {
    if (value?.trim()) {
      merged.add(value.trim());
    }
  }

  return [...merged];
}

function parameter(name: string, value: unknown, unit: string | null, sourceText: string | null): InterpretedParameterPayload {
  return {
    name,
    value,
    unit,
    source_text: sourceText,
    confidence: 0.98
  };
}

function num(operation: InterpretedOperationPayload, name: string): string {
  const value = operation.parameters.find(parameter => parameter.name === name)?.value;
  return typeof value === "number" ? `${value}` : `${Number(value ?? 0)}`;
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

function normalizeIntentText(text: string): string {
  return text
    .toLowerCase()
    .replace(/\brec\b/g, "rect")
    .replace(/\bret\b/g, "rect")
    .replace(/\brectanle\b/g, "rectangle")
    .replace(/\bextrd\b/g, "extrude");
}
