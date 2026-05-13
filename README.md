# RaceFlow Planner

RaceFlow Planner is a portable race-building and theme-prep tool for **RaceFlow**, a Guild Wars 2 race overlay system.
It is designed to let race organizers build complete race routes, capture checkpoint telemetry, tune the OBS overlay layout, create/edit themes, and preview the final broadcast output without manually editing JSON files.
The goal is simple:
> Build the race visually. Tune the overlay visually. Export clean files for RaceFlow Admin, Racer HUD, overlays, and OBS.

---

## What RaceFlow Planner Does

RaceFlow Planner can:
- Create RaceFlow race routes using a node graph.
- Add starts, checkpoints, splits, converges, segment ends, and final race nodes.
- Capture live Guild Wars 2 telemetry directly into nodes.
- Update checkpoint coordinates later without recreating nodes.
- Save and load editable `.planrf` project files.
- Export RaceFlow Admin-compatible FlowMap JSON.
- Export lightweight checkpoint XML.
- Import legacy Speedometer-style checkpoint CSV files.
- Build and edit RaceFlow themes without hand-editing JSON.
- Import theme icons.
- Tune global, section, and segment overlay positioning.
- Preview the final themed output inside the app.
- Open a localhost OBS/browser output for live testing and final tuning.

---

## Download / Release Contents

The release includes:
```text
RFPlanner.exe
```

Optional sample package:
```text
Sample Themes.zip
```

The sample themes are not required. They are included only so users can inspect example results or use them as a starting point.

---

## Portable App

RaceFlow Planner is portable.
There is no installer required.
To run it:

1. Download the release.
2. Run:
```text
RFPlanner.exe
```

---

# Main Concepts

## `.planrf` Project File

The `.planrf` file is the editable Planner project.

It stores:
- Node graph
- RaceFlow node metadata
- Checkpoint telemetry
- FlowMap tuning
- Theme Builder working data
- Active theme reference

Use `.planrf` when you want to keep editing a race later.

---

## FlowMap JSON

The FlowMap JSON is the main runtime export used by RaceFlow Admin.

It contains:
- Race sections
- Segments
- Nodes
- Connections
- Split/converge flow
- Checkpoint trigger data
- Finish metadata
- FlowMap tuning

This is the file you load into RaceFlow Admin (Admin v2 or later).

---

## Checkpoint XML

Checkpoint XML is a lightweight checkpoint-only export.

It contains:
- checkpoint index
- label
- map ID
- X/Y/Z position
- radius
- angle
- note

It does **not** contain race flow logic, splits, branches, segment logic, or finish behavior. Use it for simple checkpoint overlays or Speedometer-style checkpoint visualization.

---

## Theme JSON

Theme JSON controls the visual style of the overlay.

It can include:
- Default node icons
- Line colors
- Split/converge line colors
- Line thickness
- Node visibility
- Title visibility
- Shadow color/opacity/blur/offset
- Node type overrides
- Segment overrides
- Node overrides
- Racer dot/name settings

RaceFlow Planner includes a Theme Builder so users do not need to manually edit the theme JSON anymore.

---

# How to Use

## 1. Launch RaceFlow Planner

Run:
```text
RFPlanner.exe
```

You will see two main workspace tabs:
```text
Telemetry & Flow
Theme Builder
```

Start in **Telemetry & Flow**.

---

## 2. Create a New Race Route

Use the toolbar buttons to build your race:
```text
New Segment
Checkpoint
Path CP
Split
Converge
End Segment
Final
```

A basic race usually looks like:
```text
Segment 1 - Start
CP1
CP2
CP3
Final
```

A multi-map or multi-part race might use:
```text
Segment 1 - Start
CP1
CP2
Segment End 1

Segment 2 - Start
CP3
CP4
Final
```

---

## 3. Node Types

### New Segment
Creates a new segment start node.

Use this for:
- Start of the race
- Start of a new map
- Start of a new major race section

A start node has one output and can optionally have one input from a previous segment.

### Checkpoint
Creates a normal checkpoint node.

- Use this for standard race checkpoints.
- A checkpoint has one input and one output.

### Split
- Creates a split node.
- Use this when racers can choose between multiple paths.
- A split has one input and multiple outputs.

### Path CP
- Creates a path checkpoint.
- Use this for checkpoints inside split branches.
- Path checkpoints are named like:
```text
S1_P1_CP1
S1_P2_CP1
```

This means:
```text
Split 1, Path 1, Checkpoint 1
Split 1, Path 2, Checkpoint 1
```

### Converge
- Creates a converge node.
- Use this when split paths come back together.
- A converge has multiple inputs and one output.

### End Segment
Creates a segment end node.

Use this for:
- Map swaps
- Mount changes
- Major race transitions
- Segment handoffs

A segment end can connect into another segment start.

### Final
- Creates the final race node.
- This is the true end of the race.
- A final node has one input and no outputs.

---

## 4. Connect Nodes
Connect nodes in the graph to define race order.

Typical flow example:
```text
Start → CP1 → CP2 → CP3 → Final
```

Split flow example:
```text
CP2 → Split 1
Split 1 → S1_P1_CP1 → S1_P1_CP2 → Converge 1
Split 1 → S1_P2_CP1 → S1_P2_CP2 → Converge 1
Converge 1 → CP3
```

---

## 5. Select Nodes and Edit Properties

Click a node to edit its properties. Common properties include:

- Node Name
- Display Name
- Node Position X/Y
- Node Color
- Map ID
- X/Y/Z world position
- Radius
- Angle
- Notes

The **Display Name** is the label used for map/admin display. If it is blank, the node name is used.

---

## 6. Capture Live Telemetry

RaceFlow Planner can read Guild Wars 2 telemetry directly. When telemetry is active, the lower-left indicator will show live status.

To capture checkpoint data:
1. Stand at the checkpoint location in-game.
2. Click the node button you want to create.
3. The node will capture the current:
   - Map ID
   - X
   - Y
   - Z

You can also repair an existing node:
1. Select the node.
2. Stand at the corrected location in-game.
3. Click:
```text
Update Position
```

This updates the node’s telemetry without the need to recreate it.

---

## 7. Save Your Project

Use:
```text
Save
```

Save file with any name. Default saves as:
```text
MyRace.planrf
```

This is your editable project file.

Use `.planrf` whenever you want to continue editing later.

---

## 8. Load a Project

Use:
```text
Load
```

Select a `.planrf` file and the project will restore:

- graph
- node metadata
- telemetry
- theme state
- tuning values

---

## 9. Import Legacy Speedometer CSV

RaceFlow Planner can import legacy Speedometer-style checkpoint CSV files.

Use:
```text
Import CSV
```

Supported CSV format:
```text
STEP,STEPNAME,X,Y,Z,RADIUS,ANGLE
```

Older files without `ANGLE` are also supported:
```text
STEP,STEPNAME,X,Y,Z,RADIUS
```

Supported `STEPNAME` values:
```text
start
*
end
reset
```

Import behavior:
- `start` becomes a Start node.
- `*` becomes a Checkpoint node.
- `end` becomes a Final node.
- `reset` rows are ignored.

Legacy CSV files do not contain map IDs, so Planner will ask for a map ID during import.

You can either:
- enter a map ID and apply it to all imported checkpoints
- continue without a map ID and update nodes later

---

# Theme Builder

Switch to:
```text
Theme Builder
```

Theme Builder is where you tune the overlay and build/edit themes.

---

## 10. FlowMap Tuning

FlowMap tuning does not require a theme.

You can adjust:

- Global/admin output scale
- Global/admin output offset
- Section scale/offset
- Segment scale/offset
- Node text scale
- Racer text scale

You can click and drag sections or segments in the Theme Builder preview to tune layout visually. These tuning values export into the FlowMap JSON.

---

## 11. Create a New Theme

Click:
```text
New Theme
```

Enter:
- Theme name
- Save location

Planner will create:
```text
my_theme.json
my_theme/
```

The folder is where icons are stored.

Example:
```text
test_theme.json
test_theme/
├─ start.png
├─ checkpoint.png
├─ split.png
├─ converge.png
├─ end.png
└─ boss.png
```

---

## 12. Load an Existing Theme

Click:
```text
Load Theme
```

Select an existing theme JSON file.

Planner will:
- load the theme data
- reference the matching icon folder beside the JSON
- store the active theme reference inside the `.planrf` project

If someone sends you a `.planrf` file and theme folder, and the file paths no longer match your computer, simply load the theme again to relink it.

---

## 13. Import Icons

Click:
```text
Import Icons
```
- Only available if there is a theme active
- Select one or more PNG files.
- Recommended icon size:
```text
500x500 PNG
```

Planner will copy the selected PNGs into the active theme icon folder.

---

## 14. Default Theme Settings (Properties Pane)

Default Theme Settings control the overall look of the overlay. These settings affect the whole theme unless overridden by node type, segment, or node overrides.

---

## 15. Node Type Overrides

Click:
```text
Node Type Override
```

Choose the node types you want to override:
```text
Start
Checkpoint
Split
Converge
End
Final / Boss
```

Selected node types appear in the bottom of the Default properties pane. Node Type Overrides apply to every node of that type.

Example:

If you override `Checkpoint`, every checkpoint node can share:
- custom icon
- visibility
- scale
- title visibility
- title scale
- title offset
- image offset

---

## 16. Segment Overrides

- Select one or more segments in Theme Builder.
- Then click:

```text
Segment Override
```

- Segment Overrides apply to selected segment IDs (IDs are unique from other flow files).
- They are useful for changing the look of a whole segment without changing the entire theme.
- Use this when a specific segment needs unique styling.

---

## 17. Node Overrides

- Select one or more nodes in Theme Builder.
- Then click:
```text
Node Override
```

- Node Overrides apply to specific node IDs.
- They are useful for special checkpoints or unique icons.

---

## 18. OBS Output Preview

In Theme Builder, click:
```text
OBS Output
```

Planner starts a localhost browser output and opens your default browser.

Default URL:
```text
http://localhost:5057/obs
```

- Use this URL in OBS as a Browser Source.
- The OBS output is transparent and renders the current Planner graph/theme state live.
- It does not show editor containers, section labels, or backdrop headings.

It is intended to preview what the final overlay will look like in OBS.

---

# Exporting

Click:
```text
Export...
```

You can export:
```text
FlowMap JSON
Checkpoint XML
```

---

## 19. Export FlowMap JSON
Choose:

```text
FlowMap JSON
```

This creates the runtime race file for RaceFlow Admin.

Typical filename:
```text
flowmap.json
```

Before export, Planner validates important issues such as missing final nodes or missing telemetry.

---

## 20. Export Checkpoint XML

Choose:
```text
Checkpoint XML
```

This creates a lightweight checkpoint file.

Typical filename:
```text
checkpoints.xml
```

XML export is intended for simple checkpoint overlays or Speedometer-style systems. It does not contain full graph flow logic.

---

## 21. Export Theme

In Theme Builder, click:
```text
Export Theme
```

This writes the current theme builder settings into the linked theme JSON file.

Theme edits are safely stored in the `.planrf` project before export, so you can save and continue working later.

---

# Recommended Workflow (Video Coming Soon)

A normal full workflow looks like this:
```text
1. Open RFPlanner.exe
2. Create or load a .planrf project
3. Build the race graph
4. Capture or update telemetry positions
5. Save the .planrf project
6. Switch to Theme Builder
7. Tune section/segment layout
8. Create or load a theme
9. Import icons
10. Adjust default theme settings
11. Add node type, segment, or node overrides as needed
12. Open OBS Output and preview in browser/OBS
13. Export FlowMap JSON
14. Export Theme JSON
15. Optional: Export Checkpoint XML
16. Load FlowMap JSON and theme into RaceFlow Admin (v2 or later)
```

---

# Notes for OBS

Use this browser source URL:
```text
http://localhost:5057/obs
```

Recommended OBS Browser Source settings:
```text
Width: same as video broadcast
Height: same as video boradcast
Custom CSS: leave blank
Refresh browser when scene becomes active: optional
```

The output background is transparent.

---

# Theme Icon Notes

Recommended icon format:
```text
PNG
500x500
transparent background
centered artwork
```

Icons can be larger or smaller, but 500x500 PNGs are the preferred target.

---

# Troubleshooting

## The OBS output does not open

Make sure no other app is already using:
```text
localhost:5057
```

Close other RaceFlow tools if needed and try again.

---

## Theme icons do not appear

Check that:
- a theme is loaded or created
- icons were imported into the active theme folder
- the selected filename exists in that folder
- the theme was exported if testing outside Planner

---

## Checkpoints export with blank map IDs

This means the nodes do not have map IDs assigned yet.
Use live telemetry or manually enter the map ID in node properties.

---

## My theme changes are not visible in Admin yet

Planner’s Theme Builder supports the expanded theme schema. RaceFlow Admin may need a matching update to fully support newer fields such as:
- shadow color
- shadow blur
- node/image visibility overrides
- split/converge segment line colors
- expanded segment override controls

The Planner preview and OBS output are intended to show the newer theme behavior directly.

---

# Current Limitations

- Theme Builder OBS output is a preview of Planner’s current render state.
- Admin may need matching theme parser/render updates for newer theme fields.
- Individual node movement is done in the Telemetry & Flow tab, not Theme Builder.
- Theme Builder can select nodes for overrides, but does not move nodes.
- Checkpoint XML is lightweight and does not replace FlowMap JSON.

---

# File Types Summary

| File Type | Purpose |
|---|---|
| `.planrf` | Editable RaceFlow Planner project |
| `flowmap.json` | RaceFlow Admin/runtime race graph |
| `checkpoints.xml` | Lightweight checkpoint-only export |
| `theme.json` | Theme styling data |
| `.csv` | Legacy Speedometer checkpoint import |

---

# Credits / Project

RaceFlow Planner is part of the RaceFlow toolset for building and broadcasting Guild Wars 2 races.
It is designed around visual race creation, fast iteration, OBS-ready output, and removing the need for users to manually edit JSON files.

## Example of First Ever RaceFlow Broadcast
https://www.youtube.com/watch?v=reR9NkkocWs
