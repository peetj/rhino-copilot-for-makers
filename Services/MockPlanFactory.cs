using System;
using System.Collections.Generic;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal static class MockPlanFactory
{
  public static TurnResponse? TryCreate(string userText, RhinoContextSnapshot context)
  {
    if (string.IsNullOrWhiteSpace(userText))
      return null;

    var normalized = userText.Trim().ToLowerInvariant();
    if (!normalized.Contains("rectangle"))
      return null;

    var planId = "plan_" + Guid.NewGuid().ToString("N")[..8];
    var step1 = new ExecutionStepPayload(
      StepId: "step_rectangle",
      Sequence: 1,
      Type: StepType.NativeCommand,
      CommandName: "Rectangle",
      Strategy: StepStrategy.InteractiveNativeCommand,
      Macro: "_Rectangle",
      Interactive: true,
      DependsOn: Array.Empty<string>(),
      Preconditions: new[]
      {
        new StepConditionPayload("rhino_command_idle", Required: true)
      },
      Postconditions: new[]
      {
        new StepConditionPayload("objects_added_min", Value: 1)
      },
      Parameters: new Dictionary<string, object?>
      {
        ["document_units"] = context.DocumentUnits,
        ["placement_hint"] = "Use Rhino prompts to place the rectangle in the active viewport."
      },
      HumanGuidance: new HumanGuidancePayload(
        BeforeRun: "Rhino will ask you to place the rectangle in the viewport."),
      AllowedInCommandState: AllowedCommandState.IdleOnly);

    var step2 = new ExecutionStepPayload(
      StepId: "step_fillet_corners",
      Sequence: 2,
      Type: StepType.NativeCommand,
      CommandName: "FilletCorners",
      Strategy: StepStrategy.InteractiveNativeCommand,
      Macro: "_FilletCorners",
      Interactive: true,
      DependsOn: new[] { step1.StepId },
      Preconditions: new[]
      {
        new StepConditionPayload("rhino_command_idle", Required: true)
      },
      Postconditions: Array.Empty<StepConditionPayload>(),
      Parameters: new Dictionary<string, object?>
      {
        ["hint"] = "Select the rectangle and choose a corner radius when Rhino prompts you."
      },
      HumanGuidance: new HumanGuidancePayload(
        BeforeRun: "Rhino will ask you to select the curve and enter the corner radius."),
      AllowedInCommandState: AllowedCommandState.IdleOnly);

    var step3 = new ExecutionStepPayload(
      StepId: "step_extrude_curve",
      Sequence: 3,
      Type: StepType.NativeCommand,
      CommandName: "ExtrudeCrv",
      Strategy: StepStrategy.InteractiveNativeCommand,
      Macro: "_ExtrudeCrv",
      Interactive: true,
      DependsOn: new[] { step2.StepId },
      Preconditions: new[]
      {
        new StepConditionPayload("rhino_command_idle", Required: true)
      },
      Postconditions: Array.Empty<StepConditionPayload>(),
      Parameters: new Dictionary<string, object?>
      {
        ["hint"] = "Select the curve and enter the extrusion distance in Rhino."
      },
      HumanGuidance: new HumanGuidancePayload(
        BeforeRun: "Rhino will ask you to select the curve and define the extrusion distance."),
      AllowedInCommandState: AllowedCommandState.IdleOnly);

    var plan = new ExecutionPlanPayload(
      PlanId: planId,
      Intent: "mock_rectangle_build",
      Summary: "Run Rectangle, FilletCorners, then ExtrudeCrv as a stepwise Rhino plan.",
      RequiresApproval: true,
      ApprovalMode: ApprovalMode.ApprovePlan,
      RiskLevel: RiskLevel.Low,
      ExecutionMode: ExecutionMode.Stepwise,
      Steps: new[] { step1, step2, step3 });

    return new TurnResponse(
      SchemaVersion: CopilotSchema.Version,
      ResponseType: ResponseType.PlanResponse,
      RequestId: null,
      TurnId: "turn_local_mock",
      Routing: new RoutingPayload(
        Mode: RouteMode.MultiStepPlan,
        Confidence: 1.0,
        Reason: "Local mock planner recognized a rectangle-oriented build request."),
      Message: new AssistantMessagePayload(
        Role: "assistant",
        Text: "I can turn that into a three-step Rhino plan: Rectangle, FilletCorners, then ExtrudeCrv. Approve the plan to start Step 1, and I will walk Rhino through each command interactively."),
      Plan: plan);
  }
}
