import type {
  ExecutionPlanPayload,
  ExecutionReadiness,
  IntentInterpretationPayload,
  MissingInputPayload,
  RiskLevel,
  RouteMode
} from "../../Contracts/rhino-copilot-protocol";

export interface PlannerAgentOutput extends IntentInterpretationPayload {
  response_text: string;
}

export type CriticDisposition =
  | "informational"
  | "clarify"
  | "unsafe"
  | "unsupported"
  | "compile";

export interface CriticAgentOutput {
  disposition: CriticDisposition;
  route_mode: RouteMode;
  reason: string;
  response_text: string;
  interpretation: IntentInterpretationPayload;
  missing_inputs: MissingInputPayload[];
  unsupported_actions: string[];
  risk_level: RiskLevel;
}

export interface CompilerAgentOutput {
  route_mode: RouteMode;
  response_text: string;
  plan: ExecutionPlanPayload;
}

export function toIntentInterpretation(output: PlannerAgentOutput): IntentInterpretationPayload {
  const {
    primary_intent,
    execution_readiness,
    confidence,
    operations,
    missing_inputs,
    assumptions
  } = output;

  return {
    primary_intent,
    execution_readiness: execution_readiness as ExecutionReadiness,
    confidence,
    operations,
    missing_inputs,
    assumptions
  };
}
