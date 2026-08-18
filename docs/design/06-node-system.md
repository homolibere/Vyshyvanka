# Node System

The node system defines how workflow nodes appear on the dark canvas. Nodes are the primary interactive element in the designer.

## Node Anatomy

```
┌─────────────────────────────── 220px ───────────────────────────────┐
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │  HEADER (36px)                                                   │ │
│ │  [●] Node Name                   (category-colored background)  │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│  ● input-port    Port Label                                          │
│  ● input-port    Port Label                                          │
│                                              Port Label  output-port ●│
│                                              Port Label  output-port ●│
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

## Base Node Dimensions

| Property | Value |
|----------|-------|
| Width | 220px (fixed for all nodes) |
| Min height | 80px (1 output port, no inputs — e.g., Manual Trigger) |
| Typical height | 108px (1 input + 2 outputs) |
| Max height | 136px (1 input + 3 outputs — e.g., Switch) |
| Body background | `#292524` (canvas.node-bg) |
| Body border | 1px solid `#44403C` (canvas.node-border), inner alignment |
| Body radius | 8px (`radius.lg`) |
| Shadow | `0 4px 12px -2px rgba(0, 0, 0, 0.3)` |

## Node Header

| Property | Value |
|----------|-------|
| Height | 36px |
| Width | 220px (full node width) |
| Radius | 8px top corners only (matches node body radius) |
| Background | Category color (see below) |
| Icon | 8×8 circle, white at 70% opacity |
| Title font | 12px/600 Inter, white |
| Padding | ~12px horizontal |

### Category Colors

| Category | Header Color | Token |
|----------|-------------|-------|
| Trigger | `#16A34A` (Green) | `color.node.trigger` |
| Action | `#2563EB` (Blue) | `color.node.action` |
| Logic | `#D97706` (Amber) | `color.node.logic` |
| Transform | `#7C3AED` (Purple) | `color.node.transform` |

## Ports

Ports are the connection points on nodes. Input ports sit on the left edge; output ports on the right edge.

### Port Appearance (Inside Node Components)

| Property | Value |
|----------|-------|
| Size | 10×10px ellipse |
| Fill | Category color of the node |
| Position — Input | Left edge, vertically spaced 28px apart starting 48px from top |
| Position — Output | Right edge, same vertical spacing |
| Label font | 12px/400 Inter, Neutral-400 |
| Label position | 8px inset from port |

### Port Interactive States (Canvas)

These are the larger interactive port targets shown on the Node System page.

| State | Size | Fill | Stroke | Stroke Width |
|-------|------|------|--------|--------------|
| Default | 12×12 | `#292524` (node-bg) | `#A8A29E` (port) | 1.5px |
| Hover / Active | 12×12 | `#C62828` (primary) | `#C62828` (primary) | 1.5px |
| Connected | 12×12 | `#78716C` (neutral-500) | `#78716C` (neutral-500) | 1.5px |

## Node States

### Default State

Normal appearance as described in base dimensions above.

### Selected State

| Property | Change |
|----------|--------|
| Border color | `#C62828` (Primary) — replaces default `#44403C` |
| Border width | 2px (from 1px) |
| Shadow | Same as default |

### Hover State (implied)

| Property | Change |
|----------|--------|
| Border color | Slightly lighter than default (Neutral-600) |
| Cursor | `pointer` |

## Connection Lines

Connections are drawn between output and input ports as curves/bezier paths on the canvas.

| State | Color | Width | Style | Notes |
|-------|-------|-------|-------|-------|
| Default | `#78716C` (neutral-500) | 2px | Solid | Idle connection |
| Hover | `#C62828` (primary) | 2px | Solid | Mouse over connection line |
| Selected | `#D97706` (warning/amber) | 3px | Solid | Active/selected connection |
| Drawing (Pending) | `#A8A29E` (neutral-400) | 2px | Dashed | While user is dragging a new connection |

## Execution State Indicators

During workflow execution visualization, each node shows a state indicator (small colored dot or border glow).

| State | Color | Token Reference | Visual Treatment |
|-------|-------|----------------|-----------------|
| Pending | `#D97706` (Amber) | `color.warning` | Amber dot/ring |
| Running | `#2563EB` (Blue) | `color.info` | Blue dot/ring, possibly animated |
| Completed | `#16A34A` (Green) | `color.success` | Green dot/checkmark |
| Failed | `#DC2626` (Red) | `color.danger` | Red dot/X |
| Cancelled | `#78716C` (Gray) | `color.neutral.500` | Gray dot |

## Node Components Inventory

### Trigger Nodes (Green header)

| Component | Ports (In → Out) | Height |
|-----------|-------------------|--------|
| Webhook Trigger | — → data, headers | 108px |
| Schedule Trigger | — → timestamp | 80px |
| Manual Trigger | — → input | 80px |

### Action Nodes (Blue header)

| Component | Ports (In → Out) | Height |
|-----------|-------------------|--------|
| Http Request | input → response, status | 108px |
| Database Query | query, params → rows, count | 108px |
| Email Send | to, body → result | 108px |
| Code | data → output | 80px |

### Logic Nodes (Amber header)

| Component | Ports (In → Out) | Height |
|-----------|-------------------|--------|
| If Condition | input → true, false | 108px |
| Switch | input → case-1, case-2, default | 136px |
| Loop | items → item, done | 108px |
| Merge | input-1, input-2 → output | 108px |

## Canvas Rendering Notes

- **Grid pattern:** Dot grid with `#292524` dots on `#1C1917` background, spaced at 20px intervals.
- **Zoom levels:** Nodes should be legible at 50%–200% zoom. Port labels can hide below 60%.
- **Z-ordering:** Selected node always on top, followed by hovered, then default. Connections render below nodes.
- **Minimum spacing:** Recommend 80px gap between nodes for readable connections.
- **Connection routing:** Bezier curves with horizontal bias (output goes right, curves to input on left).
