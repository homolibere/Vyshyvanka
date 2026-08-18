# Colors

The color system is built on three layers: brand colors, a warm-tone neutral scale (stone), and semantic/functional colors.

## Brand Colors

| Name | Hex | Token | Usage |
|------|-----|-------|-------|
| Primary | `#C62828` | `color.brand.primary` | Main CTA, selected states, active nav, brand accent |
| Primary Hover | `#A51C1C` | `color.brand.primary-hover` | Primary button hover state |
| Primary Light | `#FFEBEE` | `color.brand.primary-light` | Primary tinted backgrounds, badges |
| Secondary | `#1A237E` | `color.brand.secondary` | Secondary actions, alternative emphasis |
| Secondary Hover | `#0D1557` | `color.brand.secondary-hover` | Secondary button hover state |

## Neutral Scale (Stone)

A warm-toned gray scale. Use higher numbers for darker surfaces.

| Name | Hex | Token | Typical Usage |
|------|-----|-------|---------------|
| White | `#FFFFFF` | `color.neutral.white` | Toolbar bg (light mode), card bg, modal bg |
| 50 | `#FAFAF9` | `color.neutral.50` | Page background (light screens) |
| 100 | `#F5F5F4` | `color.neutral.100` | Secondary surface, icon button bg |
| 200 | `#E7E5E4` | `color.neutral.200` | Borders (light mode), dividers |
| 300 | `#D6D3D1` | `color.neutral.300` | Secondary button border, toggle off bg |
| 400 | `#A8A29E` | `color.neutral.400` | Port default stroke, muted icons, placeholder text |
| 500 | `#78716C` | `color.neutral.500` | Muted text, connection default, connected port |
| 600 | `#57534E` | `color.neutral.600` | Secondary text |
| 700 | `#44403C` | `color.neutral.700` | Node border, canvas grid lines |
| 800 | `#292524` | `color.neutral.800` | Node body bg, panel bg (dark mode), toolbar bg (dark) |
| 900 | `#1C1917` | `color.neutral.900` | Canvas background, designer screen bg |
| 950 | `#0C0A09` | `color.neutral.950` | Deepest dark — reserved for extreme contrast |

## Semantic Colors

For status feedback, alerts, and validation states.

| Name | Hex | Token | Usage |
|------|-----|-------|-------|
| Success | `#16A34A` | `color.success` | Success toasts, completion indicators |
| Success Light | `#DCFCE7` | `color.success-light` | Success toast background |
| Warning | `#D97706` | `color.warning` | Warning badges, pending execution state |
| Warning Light | `#FEF3C7` | `color.warning-light` | Warning alert background |
| Danger | `#DC2626` | `color.danger` | Error toasts, danger buttons, failed state |
| Danger Light | `#FEE2E2` | `color.danger-light` | Error toast background |
| Info | `#2563EB` | `color.info` | Info badges, links, running execution |
| Info Light | `#DBEAFE` | `color.info-light` | Info alert background |

## Node Category Colors

Used as the node header background color to communicate node type at a glance.

| Category | Hex | Token | Header Fill |
|----------|-----|-------|-------------|
| Trigger | `#16A34A` | `color.node.trigger` | Green — entry points |
| Action | `#2563EB` | `color.node.action` | Blue — operations |
| Logic | `#D97706` | `color.node.logic` | Amber — branching/control flow |
| Transform | `#7C3AED` | `color.node.transform` | Purple — data transformation |

## Canvas Colors

Specific to the dark workflow designer canvas.

| Name | Hex | Token | Usage |
|------|-----|-------|-------|
| Canvas Background | `#1C1917` | `color.canvas.bg` | Designer screen fill |
| Canvas Grid | `#292524` | `color.canvas.grid` | Dot/grid pattern on canvas |
| Node Background | `#292524` | `color.canvas.node-bg` | Node card body fill |
| Node Border | `#44403C` | `color.canvas.node-border` | Node default border |
| Connection | `#78716C` | `color.canvas.connection` | Default connection line |
| Port | `#A8A29E` | `color.canvas.port` | Port default stroke |

## CSS Variable Mapping

When implementing, map these tokens to CSS custom properties:

```css
:root {
  /* Brand */
  --color-primary: #C62828;
  --color-primary-hover: #A51C1C;
  --color-primary-light: #FFEBEE;
  --color-secondary: #1A237E;
  --color-secondary-hover: #0D1557;

  /* Neutrals */
  --color-white: #FFFFFF;
  --color-neutral-50: #FAFAF9;
  --color-neutral-100: #F5F5F4;
  --color-neutral-200: #E7E5E4;
  --color-neutral-300: #D6D3D1;
  --color-neutral-400: #A8A29E;
  --color-neutral-500: #78716C;
  --color-neutral-600: #57534E;
  --color-neutral-700: #44403C;
  --color-neutral-800: #292524;
  --color-neutral-900: #1C1917;
  --color-neutral-950: #0C0A09;

  /* Semantic */
  --color-success: #16A34A;
  --color-success-light: #DCFCE7;
  --color-warning: #D97706;
  --color-warning-light: #FEF3C7;
  --color-danger: #DC2626;
  --color-danger-light: #FEE2E2;
  --color-info: #2563EB;
  --color-info-light: #DBEAFE;

  /* Node categories */
  --color-node-trigger: #16A34A;
  --color-node-action: #2563EB;
  --color-node-logic: #D97706;
  --color-node-transform: #7C3AED;

  /* Canvas */
  --color-canvas-bg: #1C1917;
  --color-canvas-grid: #292524;
  --color-canvas-node-bg: #292524;
  --color-canvas-node-border: #44403C;
  --color-canvas-connection: #78716C;
  --color-canvas-port: #A8A29E;
}
```
