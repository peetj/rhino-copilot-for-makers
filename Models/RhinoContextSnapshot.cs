using System.Collections.Generic;

namespace RhinoCopilotForMakers.Models;

public sealed record RhinoContextSnapshot(
  string RhinoVersion,
  string DocumentUnits,
  string ActiveViewport,
  double? AbsoluteTolerance,
  double? AngleToleranceDegrees,
  int SelectedObjectCount,
  IReadOnlyDictionary<string, int> SelectedObjectTypes,
  string? SelectedBoundingBox,
  IReadOnlyList<string> SelectedLayerNames,
  IReadOnlyList<string> DocumentLayerNames
);
