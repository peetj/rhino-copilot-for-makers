using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhinoCopilotForMakers.Contracts;

public static class CopilotSchema
{
  public const string Version = "2026-06-12";
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<ResponseType>))]
public enum ResponseType
{
  ChatResponse,
  ClarificationRequest,
  PlanResponse,
  StepRequest,
  PlanProgress,
  PlanCompleted,
  ErrorResponse
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<RouteMode>))]
public enum RouteMode
{
  Informational,
  Clarifying,
  SingleAction,
  MultiStepPlan,
  UnsafeOrDisallowed
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<ApprovalMode>))]
public enum ApprovalMode
{
  ApprovePlan,
  ApproveEachStep
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<RiskLevel>))]
public enum RiskLevel
{
  Low,
  Medium,
  High
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<ExecutionMode>))]
public enum ExecutionMode
{
  Stepwise,
  Autonomous
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<StepType>))]
public enum StepType
{
  NativeCommand,
  CustomCommand,
  DirectGeometryAction,
  UserInteractionStep,
  ValidationStep
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<StepStrategy>))]
public enum StepStrategy
{
  InteractiveNativeCommand,
  ScriptedNativeCommand,
  InteractiveCustomCommand,
  DeterministicGeometryWrite
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<StepEventType>))]
public enum StepEventType
{
  Queued,
  Started,
  WaitingForUser,
  Progress,
  Completed,
  Failed,
  Cancelled
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<FinalStatus>))]
public enum FinalStatus
{
  Completed,
  Failed,
  Cancelled
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<AllowedCommandState>))]
public enum AllowedCommandState
{
  IdleOnly,
  Always
}

[JsonConverter(typeof(SnakeCaseEnumJsonConverter<ExecutionReadiness>))]
public enum ExecutionReadiness
{
  InformationalOnly,
  ReadyToPlan,
  NeedsClarification,
  Unsafe
}

public sealed record TenantRef(
  [property: JsonPropertyName("tenant_id")] string TenantId);

public sealed record UserRef(
  [property: JsonPropertyName("user_id")] string UserId,
  [property: JsonPropertyName("display_name")] string? DisplayName = null);

public sealed record SessionRef(
  [property: JsonPropertyName("session_id")] string SessionId,
  [property: JsonPropertyName("panel_id")] string? PanelId = null);

public sealed record DocumentRef(
  [property: JsonPropertyName("document_id")] string DocumentId,
  [property: JsonPropertyName("document_name")] string? DocumentName = null,
  [property: JsonPropertyName("document_fingerprint")] string? DocumentFingerprint = null);

public sealed record ClientCapabilities(
  [property: JsonPropertyName("supports_streaming")] bool SupportsStreaming = true,
  [property: JsonPropertyName("supports_approvals")] bool SupportsApprovals = true,
  [property: JsonPropertyName("supports_local_execution")] bool SupportsLocalExecution = true,
  [property: JsonPropertyName("supports_stepwise_execution")] bool SupportsStepwiseExecution = true,
  [property: JsonPropertyName("supports_execution_events")] bool SupportsExecutionEvents = true);

public sealed record TurnPayload(
  [property: JsonPropertyName("turn_id")] string TurnId,
  [property: JsonPropertyName("parent_turn_id")] string? ParentTurnId,
  [property: JsonPropertyName("message_text")] string MessageText,
  [property: JsonPropertyName("client_capabilities")] ClientCapabilities ClientCapabilities);

public sealed record ConversationMessagePayload(
  [property: JsonPropertyName("turn_id")] string? TurnId,
  [property: JsonPropertyName("role")] string Role,
  [property: JsonPropertyName("text")] string Text,
  [property: JsonPropertyName("created_at")] string? CreatedAt = null);

public sealed record CommandStatePayload(
  [property: JsonPropertyName("is_command_running")] bool IsCommandRunning,
  [property: JsonPropertyName("active_command_name")] string? ActiveCommandName);

public sealed record RhinoContextPayload(
  [property: JsonPropertyName("rhino_version")] string RhinoVersion,
  [property: JsonPropertyName("document_units")] string DocumentUnits,
  [property: JsonPropertyName("active_viewport")] string ActiveViewport,
  [property: JsonPropertyName("absolute_tolerance")] double? AbsoluteTolerance,
  [property: JsonPropertyName("angle_tolerance_degrees")] double? AngleToleranceDegrees,
  [property: JsonPropertyName("selected_object_count")] int SelectedObjectCount,
  [property: JsonPropertyName("selected_object_types")] IReadOnlyDictionary<string, int> SelectedObjectTypes,
  [property: JsonPropertyName("selected_bounding_box")] string? SelectedBoundingBox,
  [property: JsonPropertyName("selected_layer_names")] IReadOnlyList<string> SelectedLayerNames,
  [property: JsonPropertyName("document_layer_names")] IReadOnlyList<string> DocumentLayerNames,
  [property: JsonPropertyName("command_state")] CommandStatePayload CommandState);

public sealed record TurnRequest(
  [property: JsonPropertyName("schema_version")] string SchemaVersion,
  [property: JsonPropertyName("request_id")] string RequestId,
  [property: JsonPropertyName("sent_at")] string SentAt,
  [property: JsonPropertyName("tenant")] TenantRef Tenant,
  [property: JsonPropertyName("user")] UserRef User,
  [property: JsonPropertyName("session")] SessionRef Session,
  [property: JsonPropertyName("document")] DocumentRef Document,
  [property: JsonPropertyName("turn")] TurnPayload Turn,
  [property: JsonPropertyName("conversation")] IReadOnlyList<ConversationMessagePayload>? Conversation,
  [property: JsonPropertyName("rhino_context")] RhinoContextPayload RhinoContext);

public sealed record RoutingPayload(
  [property: JsonPropertyName("mode")] RouteMode Mode,
  [property: JsonPropertyName("confidence")] double? Confidence = null,
  [property: JsonPropertyName("reason")] string? Reason = null);

public sealed record AssistantMessagePayload(
  [property: JsonPropertyName("role")] string Role,
  [property: JsonPropertyName("text")] string Text);

public sealed record MissingInputPayload(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("type")] string Type,
  [property: JsonPropertyName("unit")] string? Unit = null,
  [property: JsonPropertyName("required")] bool Required = true);

public sealed record InterpretedParameterPayload(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("value")] object? Value,
  [property: JsonPropertyName("unit")] string? Unit = null,
  [property: JsonPropertyName("source_text")] string? SourceText = null,
  [property: JsonPropertyName("confidence")] double? Confidence = null);

public sealed record InterpretedOperationPayload(
  [property: JsonPropertyName("operation_id")] string OperationId,
  [property: JsonPropertyName("action")] string Action,
  [property: JsonPropertyName("target")] string Target,
  [property: JsonPropertyName("depends_on")] IReadOnlyList<string> DependsOn,
  [property: JsonPropertyName("parameters")] IReadOnlyList<InterpretedParameterPayload> Parameters,
  [property: JsonPropertyName("confidence")] double? Confidence = null,
  [property: JsonPropertyName("can_execute_deterministically")] bool CanExecuteDeterministically = false);

public sealed record IntentInterpretationPayload(
  [property: JsonPropertyName("primary_intent")] string PrimaryIntent,
  [property: JsonPropertyName("execution_readiness")] ExecutionReadiness ExecutionReadiness,
  [property: JsonPropertyName("confidence")] double? Confidence = null,
  [property: JsonPropertyName("operations")] IReadOnlyList<InterpretedOperationPayload>? Operations = null,
  [property: JsonPropertyName("missing_inputs")] IReadOnlyList<MissingInputPayload>? MissingInputs = null,
  [property: JsonPropertyName("assumptions")] IReadOnlyList<string>? Assumptions = null);

public sealed record StepConditionPayload(
  [property: JsonPropertyName("type")] string Type,
  [property: JsonPropertyName("required")] bool? Required = null,
  [property: JsonPropertyName("source")] string? Source = null,
  [property: JsonPropertyName("value")] int? Value = null);

public sealed record HumanGuidancePayload(
  [property: JsonPropertyName("before_run")] string? BeforeRun = null,
  [property: JsonPropertyName("after_run")] string? AfterRun = null);

public sealed record ExecutionStepPayload(
  [property: JsonPropertyName("step_id")] string StepId,
  [property: JsonPropertyName("sequence")] int Sequence,
  [property: JsonPropertyName("type")] StepType Type,
  [property: JsonPropertyName("command_name")] string CommandName,
  [property: JsonPropertyName("strategy")] StepStrategy Strategy,
  [property: JsonPropertyName("macro")] string? Macro,
  [property: JsonPropertyName("interactive")] bool Interactive,
  [property: JsonPropertyName("depends_on")] IReadOnlyList<string> DependsOn,
  [property: JsonPropertyName("preconditions")] IReadOnlyList<StepConditionPayload> Preconditions,
  [property: JsonPropertyName("postconditions")] IReadOnlyList<StepConditionPayload> Postconditions,
  [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, object?> Parameters,
  [property: JsonPropertyName("human_guidance")] HumanGuidancePayload? HumanGuidance = null,
  [property: JsonPropertyName("allowed_in_command_state")] AllowedCommandState AllowedInCommandState = AllowedCommandState.IdleOnly);

public sealed record ExecutionPlanPayload(
  [property: JsonPropertyName("plan_id")] string PlanId,
  [property: JsonPropertyName("intent")] string Intent,
  [property: JsonPropertyName("summary")] string Summary,
  [property: JsonPropertyName("requires_approval")] bool RequiresApproval,
  [property: JsonPropertyName("approval_mode")] ApprovalMode ApprovalMode,
  [property: JsonPropertyName("risk_level")] RiskLevel RiskLevel,
  [property: JsonPropertyName("execution_mode")] ExecutionMode ExecutionMode,
  [property: JsonPropertyName("steps")] IReadOnlyList<ExecutionStepPayload> Steps);

public sealed record PlanProgressPayload(
  [property: JsonPropertyName("completed_steps")] int CompletedSteps,
  [property: JsonPropertyName("total_steps")] int TotalSteps,
  [property: JsonPropertyName("current_step_id")] string? CurrentStepId,
  [property: JsonPropertyName("state")] string State);

public sealed record PlanResultPayload(
  [property: JsonPropertyName("final_status")] FinalStatus FinalStatus,
  [property: JsonPropertyName("completed_steps")] int CompletedSteps,
  [property: JsonPropertyName("total_steps")] int TotalSteps);

public sealed record ErrorPayload(
  [property: JsonPropertyName("code")] string Code,
  [property: JsonPropertyName("message")] string Message,
  [property: JsonPropertyName("retryable")] bool Retryable);

public sealed record TurnResponse(
  [property: JsonPropertyName("schema_version")] string SchemaVersion,
  [property: JsonPropertyName("response_type")] ResponseType ResponseType,
  [property: JsonPropertyName("request_id")] string? RequestId,
  [property: JsonPropertyName("turn_id")] string TurnId,
  [property: JsonPropertyName("routing")] RoutingPayload? Routing = null,
  [property: JsonPropertyName("message")] AssistantMessagePayload? Message = null,
  [property: JsonPropertyName("interpretation")] IntentInterpretationPayload? Interpretation = null,
  [property: JsonPropertyName("missing_inputs")] IReadOnlyList<MissingInputPayload>? MissingInputs = null,
  [property: JsonPropertyName("plan")] ExecutionPlanPayload? Plan = null,
  [property: JsonPropertyName("step")] ExecutionStepPayload? Step = null,
  [property: JsonPropertyName("progress")] PlanProgressPayload? Progress = null,
  [property: JsonPropertyName("result")] PlanResultPayload? Result = null,
  [property: JsonPropertyName("error")] ErrorPayload? Error = null);

public sealed record ApprovalDecisionPayload(
  [property: JsonPropertyName("approved")] bool Approved,
  [property: JsonPropertyName("approval_mode")] ApprovalMode ApprovalMode,
  [property: JsonPropertyName("approved_at")] string ApprovedAt);

public sealed record ApprovalDecisionRequest(
  [property: JsonPropertyName("schema_version")] string SchemaVersion,
  [property: JsonPropertyName("request_id")] string RequestId,
  [property: JsonPropertyName("turn_id")] string TurnId,
  [property: JsonPropertyName("plan_id")] string PlanId,
  [property: JsonPropertyName("decision")] ApprovalDecisionPayload Decision);

public sealed record ExecutionEventPayload(
  [property: JsonPropertyName("execution_id")] string ExecutionId,
  [property: JsonPropertyName("event_type")] StepEventType EventType,
  [property: JsonPropertyName("timestamp")] string Timestamp,
  [property: JsonPropertyName("status_text")] string StatusText,
  [property: JsonPropertyName("rhino_command_name")] string? RhinoCommandName = null);

public sealed record StepExecutionEventRequest(
  [property: JsonPropertyName("schema_version")] string SchemaVersion,
  [property: JsonPropertyName("request_id")] string RequestId,
  [property: JsonPropertyName("turn_id")] string TurnId,
  [property: JsonPropertyName("plan_id")] string PlanId,
  [property: JsonPropertyName("step_id")] string StepId,
  [property: JsonPropertyName("execution")] ExecutionEventPayload Execution);

public sealed record ExecutionErrorPayload(
  [property: JsonPropertyName("code")] string Code,
  [property: JsonPropertyName("message")] string Message);

public sealed record ExecutionResultPayload(
  [property: JsonPropertyName("execution_id")] string ExecutionId,
  [property: JsonPropertyName("final_status")] FinalStatus FinalStatus,
  [property: JsonPropertyName("started_at")] string StartedAt,
  [property: JsonPropertyName("finished_at")] string FinishedAt,
  [property: JsonPropertyName("summary")] string Summary,
  [property: JsonPropertyName("result_data")] IReadOnlyDictionary<string, object?> ResultData,
  [property: JsonPropertyName("error")] ExecutionErrorPayload? Error = null);

public sealed record StepExecutionResultRequest(
  [property: JsonPropertyName("schema_version")] string SchemaVersion,
  [property: JsonPropertyName("request_id")] string RequestId,
  [property: JsonPropertyName("turn_id")] string TurnId,
  [property: JsonPropertyName("plan_id")] string PlanId,
  [property: JsonPropertyName("step_id")] string StepId,
  [property: JsonPropertyName("execution")] ExecutionResultPayload Execution,
  [property: JsonPropertyName("updated_rhino_context")] RhinoContextPayload UpdatedRhinoContext);

public sealed class SnakeCaseEnumJsonConverter<TEnum> : JsonConverter<TEnum>
  where TEnum : struct, Enum
{
  private static readonly Dictionary<string, TEnum> FromWire = Enum
    .GetValues<TEnum>()
    .ToDictionary(ToSnakeCase, value => value, StringComparer.OrdinalIgnoreCase);

  private static readonly Dictionary<TEnum, string> ToWire = Enum
    .GetValues<TEnum>()
    .ToDictionary(value => value, ToSnakeCase);

  public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    var wireValue = reader.GetString();
    if (wireValue is null || !FromWire.TryGetValue(wireValue, out var parsed))
      throw new JsonException($"Unknown {typeof(TEnum).Name} value '{wireValue}'.");

    return parsed;
  }

  public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
  {
    if (!ToWire.TryGetValue(value, out var wireValue))
      throw new JsonException($"Unknown {typeof(TEnum).Name} value '{value}'.");

    writer.WriteStringValue(wireValue);
  }

  private static string ToSnakeCase(TEnum value)
  {
    var name = value.ToString();
    var sb = new StringBuilder(name.Length + 8);

    for (var i = 0; i < name.Length; i++)
    {
      var c = name[i];
      if (char.IsUpper(c))
      {
        if (i > 0)
          sb.Append('_');

        sb.Append(char.ToLowerInvariant(c));
      }
      else
      {
        sb.Append(c);
      }
    }

    return sb.ToString();
  }
}
