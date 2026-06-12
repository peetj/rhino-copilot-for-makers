# Execution Strategy Matrix

This file defines how the copilot should choose between:

- `interactive_native_command`
- `scripted_native_command`
- `deterministic_geometry_write`

The goal is simple:

- if the user already supplied a parameter, do not ask for it again
- if Rhino can reliably execute the step without further user input, automate it
- only fall back to interaction when a required value is genuinely missing

## Default rules

### Use `deterministic_geometry_write`

Use this when:

- the requested geometry is fully specified
- the step can be created safely with RhinoCommon
- the result can be associated with later plan steps

Examples:

- create a `120 x 80` rectangle
- extrude a closed planar curve by `3 mm`
- create a circle with known radius/diameter on a known plane

### Use `scripted_native_command`

Use this when:

- Rhino already has a command that does the job well
- the needed parameters are known
- command-line execution is reliable enough
- direct geometry write would be more complex than the command is worth

Examples:

- fillet corners with a known radius
- offset a curve with a known distance
- join a known selection

### Use `interactive_native_command`

Use this only when:

- important values are missing
- placement or selection should come from the viewport
- the command is highly contextual or visually guided

Examples:

- `create a rectangle` with no dimensions
- `extrude this by eye`
- `place the box here`

## Current local POC behavior

The plugin-side mocked planner now applies these rules to rectangle/extrude requests:

- width and height found:
  - rectangle becomes `deterministic_geometry_write`
- width and height missing:
  - rectangle stays `interactive_native_command`
- extrusion distance found:
  - extrusion becomes `deterministic_geometry_write`
- extrusion distance missing:
  - extrusion stays `interactive_native_command`
- fillet requested with radius found:
  - fillet becomes `scripted_native_command`
- fillet requested without radius:
  - fillet stays `interactive_native_command`

## Product rule

The useful behavior is:

- automate deterministic steps
- preserve object results between steps
- minimize repeated manual work
- pause only where the user genuinely adds value
