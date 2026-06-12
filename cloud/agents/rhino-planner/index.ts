import type {
  ConversationMessagePayload,
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

  return createJsonCompletion<PlannerAgentOutput>(env, {
    systemPrompt: PLANNER_SYSTEM_PROMPT,
    userPrompt
  });
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
