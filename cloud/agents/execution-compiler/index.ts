import {
  type AllowedCommandState,
  type ApprovalMode,
  type ExecutionMode,
  type ExecutionPlanPayload,
  type ExecutionStepPayload,
  type HumanGuidancePayload,
  type InterpretedOperationPayload,
  type RiskLevel,
  type RouteMode,
  type StepConditionPayload,
  type StepStrategy,
  type StepType,
  type TurnRequest
} from "../../../Contracts/rhino-copilot-protocol";
import type { CompilerAgentOutput } from "../../shared/agent-types";

export function compileExecutionPlan(
  request: TurnRequest,
  interpretation: {
    primary_intent: string;
    operations?: InterpretedOperationPayload[] | null;
  },
  routeMode: RouteMode,
  riskLevel: RiskLevel
): CompilerAgentOutput {
  const operations = interpretation.operations ?? [];
  const stepIdsByOperation = new Map<string, string>();
  const steps: ExecutionStepPayload[] = operations.map((operation, index) => {
    const stepId = `step_${operation.operation_id || index + 1}`;
    stepIdsByOperation.set(operation.operation_id, stepId);
    return compileOperation(request, operation, index + 1, stepIdsByOperation);
  });

  const plan = buildPlan(
    interpretation.primary_intent,
    routeMode,
    riskLevel,
    steps
  );

  return {
    route_mode: routeMode,
    response_text: buildPlanMessage(steps),
    plan
  };
}

function compileOperation(
  request: TurnRequest,
  operation: InterpretedOperationPayload,
  sequence: number,
  stepIdsByOperation: Map<string, string>
): ExecutionStepPayload {
  const stepId = stepIdsByOperation.get(operation.operation_id) ?? `step_${sequence}`;
  const dependsOn = operation.depends_on
    .map(dependency => stepIdsByOperation.get(dependency) ?? `step_${dependency}`);

  switch (operation.action) {
    case "create_rectangle_profile":
      return {
        step_id: stepId,
        sequence,
        type: "direct_geometry_action",
        command_name: "CreateRectangle",
        strategy: "deterministic_geometry_write",
        macro: null,
        interactive: false,
        depends_on: [],
        preconditions: [idlePrecondition()],
        postconditions: [objectsAddedPostcondition()],
        parameters: {
          width: getNumber(operation, "width"),
          height: getNumber(operation, "height"),
          centered: getBoolean(operation, "centered"),
          document_units: request.rhino_context.document_units
        },
        human_guidance: {
          before_run: `Create a ${fmt(getNumber(operation, "width"))} x ${fmt(getNumber(operation, "height"))} rectangle on the active CPlane.`,
          after_run: "The resulting profile will remain selected for the next step."
        },
        allowed_in_command_state: "idle_only"
      };

    case "create_circle_profile":
      return {
        step_id: stepId,
        sequence,
        type: "direct_geometry_action",
        command_name: "CreateCircle",
        strategy: "deterministic_geometry_write",
        macro: null,
        interactive: false,
        depends_on: [],
        preconditions: [idlePrecondition()],
        postconditions: [objectsAddedPostcondition()],
        parameters: {
          radius: getNumber(operation, "radius"),
          center_x: getNumber(operation, "center_x"),
          center_y: getNumber(operation, "center_y"),
          document_units: request.rhino_context.document_units
        },
        human_guidance: {
          before_run: `Create a circle on the active CPlane with radius ${fmt(getNumber(operation, "radius"))}.`,
          after_run: "The resulting profile will remain selected for the next step."
        },
        allowed_in_command_state: "idle_only"
      };

    case "fillet_profile_corners":
      return {
        step_id: stepId,
        sequence,
        type: "native_command",
        command_name: "FilletCorners",
        strategy: "scripted_native_command",
        macro: `! _SelNone _SelId ${selectionToken(dependsOn[0])} _FilletCorners Radius=${fmt(getNumber(operation, "radius"))} _Enter`,
        interactive: false,
        depends_on: dependsOn,
        preconditions: [idlePrecondition()],
        postconditions: [],
        parameters: {
          radius: getNumber(operation, "radius")
        },
        human_guidance: {
          before_run: `Fillet the corners using radius ${fmt(getNumber(operation, "radius"))}.`
        },
        allowed_in_command_state: "idle_only"
      };

    case "extrude_profile":
      return {
        step_id: stepId,
        sequence,
        type: "direct_geometry_action",
        command_name: "ExtrudeCurve",
        strategy: "deterministic_geometry_write",
        macro: null,
        interactive: false,
        depends_on: dependsOn,
        preconditions: [idlePrecondition()],
        postconditions: [objectsAddedPostcondition()],
        parameters: {
          distance: getNumber(operation, "distance"),
          cap: getBoolean(operation, "cap", true)
        },
        human_guidance: {
          before_run: `Extrude the selected closed profile by ${fmt(getNumber(operation, "distance"))} ${request.rhino_context.document_units}.`
        },
        allowed_in_command_state: "idle_only"
      };

    default:
      throw new Error(`Unsupported action for compilation: ${operation.action}`);
  }
}

function buildPlan(
  intent: string,
  routeMode: RouteMode,
  riskLevel: RiskLevel,
  steps: ExecutionStepPayload[]
): ExecutionPlanPayload {
  const approvalMode: ApprovalMode = "approve_plan";
  const executionMode: ExecutionMode = "autonomous";

  return {
    plan_id: `plan_${crypto.randomUUID().replaceAll("-", "").slice(0, 12)}`,
    intent,
    summary: summarizeSteps(steps),
    requires_approval: true,
    approval_mode: approvalMode,
    risk_level: riskLevel,
    execution_mode: executionMode,
    steps
  };
}

function buildPlanMessage(steps: ExecutionStepPayload[]): string {
  const summary = steps.map(step => `${step.command_name} (${describeStrategy(step.strategy)})`).join(", ");
  return `I built a ${steps.length}-step Rhino plan. Known values will be executed automatically where possible: ${summary}.`;
}

function summarizeSteps(steps: ExecutionStepPayload[]): string {
  const verbs = steps.map(step => step.command_name);
  const routeLabel = steps.length > 1 ? "workflow" : "step";
  return `Compiled Rhino ${routeLabel}: ${verbs.join(" -> ")}.`;
}

function describeStrategy(strategy: StepStrategy): string {
  switch (strategy) {
    case "deterministic_geometry_write":
      return "automatic";
    case "scripted_native_command":
      return "scripted";
    case "interactive_native_command":
      return "interactive";
    case "interactive_custom_command":
      return "guided";
  }
}

function idlePrecondition(): StepConditionPayload {
  return {
    type: "rhino_command_idle",
    required: true
  };
}

function objectsAddedPostcondition(): StepConditionPayload {
  return {
    type: "objects_added_min",
    value: 1
  };
}

function selectionToken(stepId?: string): string {
  return `__STEP_RESULT__:${stepId || ""}`;
}

function getNumber(operation: InterpretedOperationPayload, name: string): number {
  const value = operation.parameters.find(parameter => parameter.name === name)?.value;
  if (typeof value === "number") {
    return value;
  }

  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function getBoolean(operation: InterpretedOperationPayload, name: string, defaultValue = false): boolean {
  const value = operation.parameters.find(parameter => parameter.name === name)?.value;
  if (typeof value === "boolean") {
    return value;
  }

  if (typeof value === "string") {
    return value.toLowerCase() === "true";
  }

  return defaultValue;
}

function fmt(value: number): string {
  return value.toFixed(3).replace(/\.?0+$/, "");
}
