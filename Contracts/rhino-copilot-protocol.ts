export const SCHEMA_VERSION = "2026-06-12" as const;

export type ResponseType =
  | "chat_response"
  | "clarification_request"
  | "plan_response"
  | "step_request"
  | "plan_progress"
  | "plan_completed"
  | "error_response";

export type RouteMode =
  | "informational"
  | "clarifying"
  | "single_action"
  | "multi_step_plan"
  | "unsafe_or_disallowed";

export type ApprovalMode = "approve_plan" | "approve_each_step";
export type RiskLevel = "low" | "medium" | "high";
export type ExecutionMode = "stepwise" | "autonomous";
export type StepType =
  | "native_command"
  | "custom_command"
  | "direct_geometry_action"
  | "user_interaction_step"
  | "validation_step";
export type StepStrategy =
  | "interactive_native_command"
  | "scripted_native_command"
  | "interactive_custom_command"
  | "deterministic_geometry_write";
export type StepEventType =
  | "queued"
  | "started"
  | "waiting_for_user"
  | "progress"
  | "completed"
  | "failed"
  | "cancelled";
export type FinalStatus = "completed" | "failed" | "cancelled";
export type AllowedCommandState = "idle_only" | "always";
export type ExecutionReadiness =
  | "informational_only"
  | "ready_to_plan"
  | "needs_clarification"
  | "unsafe";

export interface TenantRef {
  tenant_id: string;
}

export interface UserRef {
  user_id: string;
  display_name?: string | null;
}

export interface SessionRef {
  session_id: string;
  panel_id?: string | null;
}

export interface DocumentRef {
  document_id: string;
  document_name?: string | null;
  document_fingerprint?: string | null;
}

export interface ClientCapabilities {
  supports_streaming: boolean;
  supports_approvals: boolean;
  supports_local_execution: boolean;
  supports_stepwise_execution: boolean;
  supports_execution_events: boolean;
}

export interface TurnPayload {
  turn_id: string;
  parent_turn_id?: string | null;
  message_text: string;
  client_capabilities: ClientCapabilities;
}

export interface ConversationMessagePayload {
  turn_id?: string | null;
  role: "system" | "user" | "assistant";
  text: string;
  created_at?: string | null;
}

export interface CommandStatePayload {
  is_command_running: boolean;
  active_command_name?: string | null;
}

export interface RhinoContextPayload {
  rhino_version: string;
  document_units: string;
  active_viewport: string;
  absolute_tolerance?: number | null;
  angle_tolerance_degrees?: number | null;
  selected_object_count: number;
  selected_object_types: Record<string, number>;
  selected_bounding_box?: string | null;
  selected_layer_names: string[];
  document_layer_names: string[];
  command_state: CommandStatePayload;
}

export interface TurnRequest {
  schema_version: typeof SCHEMA_VERSION;
  request_id: string;
  sent_at: string;
  tenant: TenantRef;
  user: UserRef;
  session: SessionRef;
  document: DocumentRef;
  turn: TurnPayload;
  conversation?: ConversationMessagePayload[] | null;
  rhino_context: RhinoContextPayload;
}

export interface RoutingPayload {
  mode: RouteMode;
  confidence?: number | null;
  reason?: string | null;
}

export interface AssistantMessagePayload {
  role: string;
  text: string;
}

export interface MissingInputPayload {
  name: string;
  type: string;
  unit?: string | null;
  required: boolean;
}

export interface InterpretedParameterPayload {
  name: string;
  value: unknown;
  unit?: string | null;
  source_text?: string | null;
  confidence?: number | null;
}

export interface InterpretedOperationPayload {
  operation_id: string;
  action: string;
  target: string;
  depends_on: string[];
  parameters: InterpretedParameterPayload[];
  confidence?: number | null;
  can_execute_deterministically: boolean;
}

export interface IntentInterpretationPayload {
  primary_intent: string;
  execution_readiness: ExecutionReadiness;
  confidence?: number | null;
  operations?: InterpretedOperationPayload[] | null;
  missing_inputs?: MissingInputPayload[] | null;
  assumptions?: string[] | null;
}

export interface StepConditionPayload {
  type: string;
  required?: boolean | null;
  source?: string | null;
  value?: number | null;
}

export interface HumanGuidancePayload {
  before_run?: string | null;
  after_run?: string | null;
}

export interface ExecutionStepPayload {
  step_id: string;
  sequence: number;
  type: StepType;
  command_name: string;
  strategy: StepStrategy;
  macro?: string | null;
  interactive: boolean;
  depends_on: string[];
  preconditions: StepConditionPayload[];
  postconditions: StepConditionPayload[];
  parameters: Record<string, unknown>;
  human_guidance?: HumanGuidancePayload | null;
  allowed_in_command_state: AllowedCommandState;
}

export interface ExecutionPlanPayload {
  plan_id: string;
  intent: string;
  summary: string;
  requires_approval: boolean;
  approval_mode: ApprovalMode;
  risk_level: RiskLevel;
  execution_mode: ExecutionMode;
  steps: ExecutionStepPayload[];
}

export interface PlanProgressPayload {
  completed_steps: number;
  total_steps: number;
  current_step_id?: string | null;
  state: string;
}

export interface PlanResultPayload {
  final_status: FinalStatus;
  completed_steps: number;
  total_steps: number;
}

export interface ErrorPayload {
  code: string;
  message: string;
  retryable: boolean;
}

export interface TurnResponse {
  schema_version: typeof SCHEMA_VERSION;
  response_type: ResponseType;
  request_id?: string | null;
  turn_id: string;
  routing?: RoutingPayload | null;
  message?: AssistantMessagePayload | null;
  interpretation?: IntentInterpretationPayload | null;
  missing_inputs?: MissingInputPayload[] | null;
  plan?: ExecutionPlanPayload | null;
  step?: ExecutionStepPayload | null;
  progress?: PlanProgressPayload | null;
  result?: PlanResultPayload | null;
  error?: ErrorPayload | null;
}

export interface ApprovalDecisionPayload {
  approved: boolean;
  approval_mode: ApprovalMode;
  approved_at: string;
}

export interface ApprovalDecisionRequest {
  schema_version: typeof SCHEMA_VERSION;
  request_id: string;
  turn_id: string;
  plan_id: string;
  decision: ApprovalDecisionPayload;
}

export interface ExecutionEventPayload {
  execution_id: string;
  event_type: StepEventType;
  timestamp: string;
  status_text: string;
  rhino_command_name?: string | null;
}

export interface StepExecutionEventRequest {
  schema_version: typeof SCHEMA_VERSION;
  request_id: string;
  turn_id: string;
  plan_id: string;
  step_id: string;
  execution: ExecutionEventPayload;
}

export interface ExecutionErrorPayload {
  code: string;
  message: string;
}

export interface ExecutionResultPayload {
  execution_id: string;
  final_status: FinalStatus;
  started_at: string;
  finished_at: string;
  summary: string;
  result_data: Record<string, unknown>;
  error?: ExecutionErrorPayload | null;
}

export interface StepExecutionResultRequest {
  schema_version: typeof SCHEMA_VERSION;
  request_id: string;
  turn_id: string;
  plan_id: string;
  step_id: string;
  execution: ExecutionResultPayload;
  updated_rhino_context: RhinoContextPayload;
}
