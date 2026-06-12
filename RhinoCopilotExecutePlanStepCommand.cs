using System;
using Rhino;
using Rhino.Commands;

namespace RhinoCopilotForMakers;

/// <summary>
/// Command: RhinoCopilotExecutePlanStep
/// Internal script-runner that executes the next approved mocked plan step.
/// </summary>
[CommandStyle(Style.ScriptRunner)]
public sealed class RhinoCopilotExecutePlanStepCommand : Command
{
  public override string EnglishName => "RhinoCopilotExecutePlanStep";

  protected override Result RunCommand(RhinoDoc doc, RunMode mode)
  {
    try
    {
      return RhinoCopilotPlugin.Instance!.PlanExecutionCoordinator.ExecuteCurrentStep(doc);
    }
    catch (Exception ex)
    {
      RhinoApp.WriteLine($"RhinoCopilotExecutePlanStep failed: {ex.Message}");
      return Result.Failure;
    }
  }
}
