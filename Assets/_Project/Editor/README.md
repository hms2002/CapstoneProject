# Editor Folder Guide

Editor contains project-owned code that must not ship in builds.

Use:

- `Tools`
- `Inspectors`
- `Build`
- `Balance`
- `MapTool`

Any tool that searches paths with `AssetDatabase`, `Directory.GetFiles`, or literal
`Assets/...` strings must be updated when related folders move.
