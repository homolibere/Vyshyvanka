# Shadows (Elevation System)

Four elevation levels define depth hierarchy. All shadows use black with varying opacity — never colored shadows.

## Shadow Scale

| Level | offsetX | offsetY | Blur | Spread | Opacity | CSS Value |
|-------|---------|---------|------|--------|---------|-----------|
| `shadow-sm` | 0 | 1px | 3px | 0 | 0.10 | `0 1px 3px rgba(0, 0, 0, 0.10)` |
| `shadow-md` | 0 | 4px | 12px | -2px | 0.10 | `0 4px 12px -2px rgba(0, 0, 0, 0.10)` |
| `shadow-lg` | 0 | 8px | 24px | -4px | 0.12 | `0 8px 24px -4px rgba(0, 0, 0, 0.12)` |
| `shadow-xl` | 0 | 16px | 40px | -6px | 0.15 | `0 16px 40px -6px rgba(0, 0, 0, 0.15)` |

## Usage

| Element | Shadow Level | Notes |
|---------|-------------|-------|
| Card on light background | `shadow-sm` | Subtle lift |
| Workflow card (hover) | `shadow-md` | Interaction feedback |
| Node on canvas | `shadow-md` (0.3 opacity) | Higher opacity on dark bg for visibility |
| Dropdown / popover | `shadow-lg` | Floating above content |
| Modal dialog | `shadow-xl` | Maximum elevation |
| Node editor modal | Custom: `0 20px 60px -12px rgba(0, 0, 0, 0.3)` | Extra large for dramatic separation |

## Special Node Shadow

Nodes on the dark canvas use a modified `shadow-md` with **0.3 opacity** instead of 0.1:

```css
/* Node shadow (dark canvas needs stronger shadow) */
box-shadow: 0 4px 12px -2px rgba(0, 0, 0, 0.3);
```

## Design Notes

- **Negative spread** creates tighter, more natural shadows (the shadow doesn't extend wider than the element).
- **On light backgrounds** (neutral-50), use standard opacity values (0.10–0.15).
- **On dark backgrounds** (neutral-900), increase opacity to 0.25–0.3 for shadow visibility.
- All shadows are `drop-shadow` style (external to the element), never `inner-shadow`.
- Shadows always cast downward (positive Y offset) — light source is above.

## CSS Variables

```css
:root {
  --shadow-sm: 0 1px 3px 0 rgba(0, 0, 0, 0.10);
  --shadow-md: 0 4px 12px -2px rgba(0, 0, 0, 0.10);
  --shadow-lg: 0 8px 24px -4px rgba(0, 0, 0, 0.12);
  --shadow-xl: 0 16px 40px -6px rgba(0, 0, 0, 0.15);

  /* Dark canvas variant (higher opacity) */
  --shadow-node: 0 4px 12px -2px rgba(0, 0, 0, 0.30);
  --shadow-modal: 0 20px 60px -12px rgba(0, 0, 0, 0.30);
}
```
