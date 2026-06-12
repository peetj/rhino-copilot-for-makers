# Rhino Planner Agent

## Responsibility

- Interpret natural Rhino language with LLM support
- Handle typos, shorthand, follow-ups, and implicit context
- Convert user intent into semantic operations

## Output shape

The planner should emit semantic actions such as:

- `create_rectangle_profile`
- `create_circle_profile`
- `create_sphere_solid`
- `extrude_profile`
- `move_objects`
- `boolean_difference`

It should not emit raw Rhino command strings as its primary abstraction.

