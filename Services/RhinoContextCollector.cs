using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
// (UnitSystem is a type in RhinoCommon; no namespace import needed)
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

public sealed class RhinoContextCollector
{
  public RhinoContextSnapshot Collect()
  {
    var doc = RhinoDoc.ActiveDoc;

    var rhinoVersion = RhinoApp.Version.ToString();
    var units = doc is null ? "(no document)" : doc.ModelUnitSystem.ToString();
    var viewport = doc?.Views.ActiveView?.ActiveViewport?.Name ?? "(unknown)";

    double? absTol = null;
    double? angTolDeg = null;
    if (doc is not null)
    {
      absTol = doc.ModelAbsoluteTolerance;
      angTolDeg = RhinoMath.ToDegrees(doc.ModelAngleToleranceRadians);
    }

    var selected = (doc?.Objects.GetSelectedObjects(false, false) ?? Array.Empty<RhinoObject>()).ToArray();
    var selectedCount = selected.Length;

    var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var ro in selected)
    {
      var key = ClassifyObject(ro);
      typeCounts[key] = typeCounts.TryGetValue(key, out var c) ? c + 1 : 1;
    }

    string? bboxStr = null;
    if (selectedCount > 0)
    {
      var bbox = BoundingBox.Empty;
      foreach (var ro in selected)
      {
        var gb = ro.Geometry?.GetBoundingBox(true);
        if (gb is null) continue;
        bbox = bbox.IsValid ? BoundingBox.Union(bbox, gb.Value) : gb.Value;
      }

      if (bbox.IsValid)
      {
        var dx = Math.Abs(bbox.Max.X - bbox.Min.X);
        var dy = Math.Abs(bbox.Max.Y - bbox.Min.Y);
        var dz = Math.Abs(bbox.Max.Z - bbox.Min.Z);
        bboxStr = $"{dx:0.###} x {dy:0.###} x {dz:0.###} ({units})";
      }
    }

    var selectedLayerNames = selected
      .Select(ro => ro.Attributes?.LayerIndex ?? -1)
      .Where(i => i >= 0)
      .Distinct()
      .Select(i =>
      {
        try
        {
          var layer = doc?.Layers[i];
          return layer?.FullPath ?? layer?.Name ?? $"LayerIndex:{i}";
        }
        catch
        {
          return $"LayerIndex:{i}";
        }
      })
      .Where(n => !string.IsNullOrWhiteSpace(n))
      .Take(30)
      .ToList();

    var layerNames = (doc?.Layers ?? Enumerable.Empty<Layer>())
      .Where(l => !l.IsDeleted)
      .Select(l => l.FullPath)
      .Where(n => !string.IsNullOrWhiteSpace(n))
      .Take(30)
      .ToList();

    return new RhinoContextSnapshot(
      RhinoVersion: rhinoVersion,
      DocumentUnits: units,
      ActiveViewport: viewport,
      AbsoluteTolerance: absTol,
      AngleToleranceDegrees: angTolDeg,
      SelectedObjectCount: selectedCount,
      SelectedObjectTypes: typeCounts,
      SelectedBoundingBox: bboxStr,
      SelectedLayerNames: selectedLayerNames,
      DocumentLayerNames: layerNames
    );
  }

  private static string ClassifyObject(RhinoObject ro)
  {
    // Prefer geometry-type labels the LLM can reason about.
    // This is intentionally compact and conservative.
    var g = ro.Geometry;
    if (g is null) return "unknown";

    return g switch
    {
      Mesh => "mesh",
      Brep => "brep",
      Curve => "curve",
      Extrusion => "extrusion",
      Surface => "surface",
      SubD => "subd",
      Point => "point",
      PointCloud => "pointcloud",
      _ => ro.ObjectType.ToString().ToLowerInvariant()
    };
  }
}
