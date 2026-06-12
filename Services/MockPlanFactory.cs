using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

    var widthHeight = TryParseRectangleSize(userText);
    var extrudeHeight = normalized.Contains("extrude") ? TryParseExtrudeHeight(userText) : null;
    var centered = normalized.Contains("centered") || normalized.Contains("centred");
    var hasFilletIntent = normalized.Contains("fillet");
    var filletRadius = hasFilletIntent ? TryParseFilletRadius(userText) : null;

    var planId = "plan_" + Guid.NewGuid().ToString("N")[..8];
    var steps = new List<ExecutionStepPayload>();

    var rectangleStep = widthHeight is { } size
      ? new ExecutionStepPayload(
        StepId: "step_create_rectangle",
        Sequence: NextSequence(steps),
        Type: StepType.DirectGeometryAction,
        CommandName: "CreateRectangle",
        Strategy: StepStrategy.DeterministicGeometryWrite,
        Macro: null,
        Interactive: false,
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
          ["width"] = size.Width,
          ["height"] = size.Height,
          ["centered"] = centered,
          ["document_units"] = context.DocumentUnits
        },
        HumanGuidance: new HumanGuidancePayload(
          BeforeRun: centered
            ? $"I will create a {size.Width:0.###} x {size.Height:0.###} rectangle centered on the active CPlane origin."
            : $"I will create a {size.Width:0.###} x {size.Height:0.###} rectangle from the active CPlane origin."),
        AllowedInCommandState: AllowedCommandState.IdleOnly)
      : new ExecutionStepPayload(
        StepId: "step_rectangle",
        Sequence: NextSequence(steps),
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

    steps.Add(rectangleStep);

    if (hasFilletIntent)
    {
      var filletStep = filletRadius.HasValue
        ? new ExecutionStepPayload(
          StepId: "step_fillet_corners",
          Sequence: NextSequence(steps),
          Type: StepType.NativeCommand,
          CommandName: "FilletCorners",
          Strategy: StepStrategy.ScriptedNativeCommand,
          Macro: $"! _SelNone _SelId {SelectionToken(rectangleStep.StepId)} _FilletCorners Radius={filletRadius.Value.ToString("0.###", CultureInfo.InvariantCulture)} _Enter",
          Interactive: false,
          DependsOn: new[] { rectangleStep.StepId },
          Preconditions: new[]
          {
            new StepConditionPayload("rhino_command_idle", Required: true)
          },
          Postconditions: Array.Empty<StepConditionPayload>(),
          Parameters: new Dictionary<string, object?>
          {
            ["radius"] = filletRadius.Value
          },
          HumanGuidance: new HumanGuidancePayload(
            BeforeRun: $"I will fillet the rectangle corners using radius {filletRadius.Value:0.###}."),
          AllowedInCommandState: AllowedCommandState.IdleOnly)
        : new ExecutionStepPayload(
          StepId: "step_fillet_corners",
          Sequence: NextSequence(steps),
          Type: StepType.NativeCommand,
          CommandName: "FilletCorners",
          Strategy: StepStrategy.InteractiveNativeCommand,
          Macro: "_FilletCorners",
          Interactive: true,
          DependsOn: new[] { rectangleStep.StepId },
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
            BeforeRun: "Rhino will ask you to confirm the curve and enter the corner radius."),
          AllowedInCommandState: AllowedCommandState.IdleOnly);

      steps.Add(filletStep);
    }

    if (normalized.Contains("extrude"))
    {
      var sourceStep = steps.Last();
      var extrudeStep = extrudeHeight.HasValue
        ? new ExecutionStepPayload(
          StepId: "step_extrude_curve",
          Sequence: NextSequence(steps),
          Type: StepType.DirectGeometryAction,
          CommandName: "ExtrudeCurve",
          Strategy: StepStrategy.DeterministicGeometryWrite,
          Macro: null,
          Interactive: false,
          DependsOn: new[] { sourceStep.StepId },
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
            ["distance"] = extrudeHeight.Value,
            ["cap"] = true
          },
          HumanGuidance: new HumanGuidancePayload(
            BeforeRun: $"I will extrude the resulting closed curve by {extrudeHeight.Value:0.###} {context.DocumentUnits}."),
          AllowedInCommandState: AllowedCommandState.IdleOnly)
        : new ExecutionStepPayload(
          StepId: "step_extrude_curve",
          Sequence: NextSequence(steps),
          Type: StepType.NativeCommand,
          CommandName: "ExtrudeCrv",
          Strategy: StepStrategy.InteractiveNativeCommand,
          Macro: "_ExtrudeCrv",
          Interactive: true,
          DependsOn: new[] { sourceStep.StepId },
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
            BeforeRun: "Rhino will ask you to confirm the curve and define the extrusion distance."),
          AllowedInCommandState: AllowedCommandState.IdleOnly);

      steps.Add(extrudeStep);
    }

    var plan = new ExecutionPlanPayload(
      PlanId: planId,
      Intent: "mock_rectangle_build",
      Summary: BuildSummary(widthHeight, extrudeHeight, hasFilletIntent, filletRadius, context.DocumentUnits),
      RequiresApproval: true,
      ApprovalMode: ApprovalMode.ApprovePlan,
      RiskLevel: RiskLevel.Low,
      ExecutionMode: ExecutionMode.Stepwise,
      Steps: steps);

    var automationSummary = string.Join(", ", steps.Select(step => $"{step.CommandName} ({DescribeStrategy(step.Strategy)})"));

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
        Text: $"I can turn that into a {steps.Count}-step Rhino plan. Known parameters will be executed automatically where possible: {automationSummary}."),
      Plan: plan);
  }

  private static string DescribeStrategy(StepStrategy strategy) => strategy switch
  {
    StepStrategy.DeterministicGeometryWrite => "automatic",
    StepStrategy.ScriptedNativeCommand => "scripted",
    StepStrategy.InteractiveNativeCommand => "interactive",
    _ => "guided"
  };

  private static string BuildSummary((double Width, double Height)? widthHeight, double? extrudeHeight, bool hasFilletIntent, double? filletRadius, string units)
  {
    var parts = new List<string>();

    if (widthHeight is { } size)
      parts.Add($"Create a {size.Width:0.###} x {size.Height:0.###} rectangle");
    else
      parts.Add("Create a rectangle");

    if (hasFilletIntent)
      parts.Add(filletRadius.HasValue
        ? $"fillet the corners with radius {filletRadius.Value:0.###}"
        : "fillet the corners");

    if (extrudeHeight.HasValue)
      parts.Add($"extrude it by {extrudeHeight.Value:0.###} {units}");
    else if (parts.Count > 0)
      parts.Add("then continue with the next requested operation in Rhino");

    return string.Join(", ", parts) + ".";
  }

  private static string SelectionToken(string stepId) => $"__STEP_RESULT__:{stepId}";

  private static int NextSequence(IReadOnlyCollection<ExecutionStepPayload> steps) => steps.Count + 1;

  private static (double Width, double Height)? TryParseRectangleSize(string text)
  {
    var match = Regex.Match(text, @"(?<w>\d+(?:\.\d+)?)\s*(?:x|×|by)\s*(?<h>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
    if (!match.Success)
      return null;

    if (!double.TryParse(match.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
      return null;
    if (!double.TryParse(match.Groups["h"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
      return null;

    return width > 0 && height > 0 ? (width, height) : null;
  }

  private static double? TryParseExtrudeHeight(string text)
  {
    var patterns = new[]
    {
      @"extrud\w*\s+(?:it\s+)?(?:to|by)?\s*(?<d>\d+(?:\.\d+)?)",
      @"height\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)",
      @"thick(?:ness)?\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)"
    };

    foreach (var pattern in patterns)
    {
      var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
      if (match.Success &&
          double.TryParse(match.Groups["d"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) &&
          distance > 0)
        return distance;
    }

    return null;
  }

  private static double? TryParseFilletRadius(string text)
  {
    var match = Regex.Match(text, @"fillet(?:\s+radius)?(?:\s+of|\s+to|\s*=)?\s*(?<r>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
    if (!match.Success)
      return null;

    return double.TryParse(match.Groups["r"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) && radius > 0
      ? radius
      : null;
  }
}
