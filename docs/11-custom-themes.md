# Custom Themes

Vyshyvanka Designer supports a flexible JSON-based theming system. You can create your own theme by writing a JSON file and uploading it via **Settings → Themes → Upload custom theme JSON**.

## Theme JSON Structure

```json
{
  "id": "my-custom-theme",
  "name": "My Custom Theme",
  "description": "A short description of the theme",
  "author": "Your Name",
  "baseMode": "light",
  "preview": {
    "bg": "#ffffff",
    "accent": "#3366ff",
    "surface": "#f5f5f5"
  },
  "colors": { },
  "icons": { },
  "canvas": {
    "pattern": "dots"
  }
}
```

## Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (lowercase, dashes). Must not conflict with built-in theme IDs. |
| `name` | string | Display name shown in the theme selector dropdown. |
| `baseMode` | string | `"light"` or `"dark"`. Controls `color-scheme` CSS property and the initial flash-prevention logic. |

## Optional Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `description` | string | `""` | Short description for reference. |
| `author` | string | `""` | Theme author name. |
| `preview` | object | `{}` | Three color swatches shown in the selector UI. |
| `preview.bg` | string | `"#ffffff"` | Background swatch color. |
| `preview.accent` | string | `"#c62828"` | Accent swatch color. |
| `preview.surface` | string | `"#ffffff"` | Surface swatch color. |
| `icons` | object | `{}` | Icon class overrides (see Icons section). |
| `canvas.pattern` | string | `"dots"` | Canvas background: `"vyshyvanka"`, `"dots"`, or `"none"`. |

## Color Variables

All keys in the `colors` object map directly to CSS custom properties (without the `--` prefix). For example, `"bg-primary": "#f8f5f0"` sets `--bg-primary: #f8f5f0`.

### Backgrounds

| Key | Description |
|-----|-------------|
| `bg-primary` | Main page background |
| `bg-surface` | Card/panel backgrounds |
| `bg-elevated` | Elevated surface (modals, popovers) |
| `bg-input` | Input field backgrounds |
| `bg-canvas` | Workflow canvas background |
| `bg-code` | Code editor background |
| `bg-hover` | Hover state background |
| `bg-gradient-start` | Gradient start color |
| `bg-gradient-end` | Gradient end color |

### Borders

| Key | Description |
|-----|-------------|
| `border-color` | Default border color |
| `border-hover` | Border on hover |
| `border-active` | Border on active/focus |
| `border-subtle` | Subtle/light border |

### Text

| Key | Description |
|-----|-------------|
| `text-primary` | Main text color |
| `text-secondary` | Secondary/supporting text |
| `text-muted` | Muted/placeholder text |
| `text-on-accent` | Text color on accent-colored backgrounds |
| `text-on-surface-dark` | Text on dark surface (always white typically) |

### Accent

| Key | Description |
|-----|-------------|
| `accent` | Primary accent color (links, active states, selection) |
| `accent-hover` | Accent hover state |
| `accent-light` | Light accent background (use rgba for transparency) |
| `accent-border` | Accent-tinted border color |

### Links

| Key | Description |
|-----|-------------|
| `link-color` | Default link color |
| `link-hover` | Link hover color |

### Buttons

| Key | Description |
|-----|-------------|
| `btn-primary-bg` | Primary button background |
| `btn-primary-border` | Primary button border |
| `btn-primary-hover` | Primary button hover background |
| `btn-primary-text` | Primary button text color |
| `btn-secondary-bg` | Secondary button background |
| `btn-secondary-border` | Secondary button border |
| `btn-secondary-hover` | Secondary button hover background |
| `btn-secondary-text` | Secondary button text color |

### Status Colors

| Key | Description |
|-----|-------------|
| `success` | Success/positive color (execution completed, valid states) |
| `success-bg` | Success background (use rgba for transparency) |
| `danger` | Error/destructive color |
| `danger-bg` | Error background |
| `warning` | Warning/pending color |
| `warning-bg` | Warning background |
| `info` | Informational color |
| `info-bg` | Informational background |

### Shadows

| Key | Description |
|-----|-------------|
| `shadow-sm` | Small shadow (cards, inputs) |
| `shadow-md` | Medium shadow (dropdowns, popovers) |
| `shadow-lg` | Large shadow (modals) |
| `overlay-bg` | Modal overlay background (use rgba) |

### Canvas & Nodes

| Key | Description |
|-----|-------------|
| `node-bg` | Node body background |
| `node-border` | Node border color |
| `node-header-bg` | Default node header background (when no category applies) |
| `node-selected` | Node border when selected |
| `connection-color` | Connection line color |
| `connection-hover` | Connection line hover color |
| `connection-selected` | Selected connection color |
| `port-fill` | Port circle fill |
| `port-stroke` | Port circle stroke |
| `port-label` | Port label text color |
| `grid-color` | Canvas grid base color |
| `grid-motif` | Canvas pattern motif color |
| `grid-accent` | Canvas pattern accent color |

### Node Category Colors

These color the header bar of nodes based on their category:

| Key | Description |
|-----|-------------|
| `category-trigger` | Trigger nodes (Manual Trigger, Webhook, Schedule) |
| `category-action` | Action nodes (HTTP Request, Database Query, Code, Email) |
| `category-logic` | Logic nodes (If, Switch, Loop, Merge) |
| `category-transform` | Transform nodes |

### UI Chrome

| Key | Description |
|-----|-------------|
| `spinner-track` | Loading spinner track color |
| `spinner-fill` | Loading spinner fill color |
| `focus-ring-outer` | Focus ring outer color |
| `focus-ring-inner` | Focus ring inner color |
| `scrollbar-track` | Scrollbar track (use `transparent` for invisible) |
| `scrollbar-thumb` | Scrollbar thumb color |
| `scrollbar-thumb-hover` | Scrollbar thumb hover color |

## Icons

The `icons` object maps logical icon keys to CSS class strings (typically Font Awesome classes). Components that support themed icons use `ThemeService.GetIcon(key)` to resolve the class.

| Key | Default | Used For |
|-----|---------|----------|
| `trigger` | `fa-solid fa-bolt` | Trigger category in palettes and labels |
| `action` | `fa-solid fa-cog` | Action category |
| `logic` | `fa-solid fa-code-branch` | Logic category |
| `transform` | `fa-solid fa-shuffle` | Transform category |
| `default` | `fa-solid fa-cube` | Fallback for unknown categories |
| `settings` | `fa-solid fa-gear` | Settings page/button |
| `workflow` | `fa-solid fa-diagram-project` | Workflow references |
| `execute` | `fa-solid fa-play` | Run/execute actions |
| `delete` | `fa-solid fa-trash` | Delete actions |
| `add` | `fa-solid fa-plus` | Add/create actions |
| `save` | `fa-solid fa-floppy-disk` | Save actions |
| `search` | `fa-solid fa-magnifying-glass` | Search inputs |
| `close` | `fa-solid fa-xmark` | Close/dismiss |
| `menu` | `fa-solid fa-bars` | Menu/hamburger |
| `back` | `fa-solid fa-arrow-left` | Navigation back |
| `user` | `fa-solid fa-user` | User/profile |
| `key` | `fa-solid fa-key` | API keys |
| `package` | `fa-solid fa-box` | Packages/plugins |
| `credential` | `fa-solid fa-shield-halved` | Credentials |

You can use any icon library loaded in the app. Font Awesome 6 is included by default.

## Canvas Patterns

| Value | Description |
|-------|-------------|
| `"vyshyvanka"` | Ukrainian cross-stitch embroidery pattern |
| `"dots"` | Simple dot grid |
| `"none"` | Blank canvas (no pattern) |

## Built-in Theme IDs (reserved)

These IDs cannot be used for custom themes:

- `vyshyvanka-light`
- `vyshyvanka-dark`
- `slate`
- `ocean-dark`
- `minimal`

## Example: Creating a Theme

Here's a minimal dark theme with purple accent:

```json
{
  "id": "purple-night",
  "name": "Purple Night",
  "description": "Dark theme with purple accents",
  "author": "Example",
  "baseMode": "dark",
  "preview": { "bg": "#1a1a2e", "accent": "#9b59b6", "surface": "#16213e" },
  "colors": {
    "bg-primary": "#1a1a2e",
    "bg-surface": "#16213e",
    "bg-elevated": "#16213e",
    "bg-input": "#1a1a2e",
    "bg-canvas": "#1a1a2e",
    "bg-code": "#141c30",
    "bg-hover": "#1f2b47",
    "bg-gradient-start": "#16213e",
    "bg-gradient-end": "#1a1a2e",

    "border-color": "#2c3e6e",
    "border-hover": "#3d5291",
    "border-active": "#4e68b4",
    "border-subtle": "#1e2d52",

    "text-primary": "#e8e8f0",
    "text-secondary": "#a8a8c0",
    "text-muted": "#6a6a8a",
    "text-on-accent": "#ffffff",
    "text-on-surface-dark": "#ffffff",

    "accent": "#9b59b6",
    "accent-hover": "#b07cc8",
    "accent-light": "rgba(155, 89, 182, 0.12)",
    "accent-border": "rgba(155, 89, 182, 0.35)",

    "link-color": "#9b59b6",
    "link-hover": "#b07cc8",

    "btn-primary-bg": "#9b59b6",
    "btn-primary-border": "#8e44ad",
    "btn-primary-hover": "#8e44ad",
    "btn-primary-text": "#ffffff",
    "btn-secondary-bg": "#1f2b47",
    "btn-secondary-border": "#2c3e6e",
    "btn-secondary-hover": "#3d5291",
    "btn-secondary-text": "#e8e8f0",

    "success": "#2ecc71",
    "success-bg": "rgba(46, 204, 113, 0.15)",
    "danger": "#e74c3c",
    "danger-bg": "rgba(231, 76, 60, 0.15)",
    "warning": "#f39c12",
    "warning-bg": "rgba(243, 156, 18, 0.15)",
    "info": "#3498db",
    "info-bg": "rgba(52, 152, 219, 0.15)",

    "shadow-sm": "0 1px 3px rgba(0, 0, 0, 0.4)",
    "shadow-md": "0 4px 16px rgba(0, 0, 0, 0.5)",
    "shadow-lg": "0 8px 32px rgba(0, 0, 0, 0.6)",
    "overlay-bg": "rgba(0, 0, 0, 0.55)",

    "node-bg": "#16213e",
    "node-border": "#2c3e6e",
    "node-header-bg": "#1f2b47",
    "node-selected": "#9b59b6",
    "connection-color": "#9b59b6",
    "connection-hover": "#b07cc8",
    "connection-selected": "#f39c12",
    "port-fill": "#16213e",
    "port-stroke": "#9b59b6",
    "port-label": "#6a6a8a",
    "grid-color": "rgba(255, 255, 255, 0.03)",
    "grid-motif": "#2c3e6e",
    "grid-accent": "#9b59b6",

    "category-trigger": "#2ecc71",
    "category-action": "#3498db",
    "category-logic": "#f39c12",
    "category-transform": "#9b59b6",

    "spinner-track": "#2c3e6e",
    "spinner-fill": "#9b59b6",

    "focus-ring-outer": "#1a1a2e",
    "focus-ring-inner": "#9b59b6",

    "scrollbar-track": "transparent",
    "scrollbar-thumb": "#2c3e6e",
    "scrollbar-thumb-hover": "#3d5291"
  },
  "icons": {
    "trigger": "fa-solid fa-bolt",
    "action": "fa-solid fa-cog",
    "logic": "fa-solid fa-code-branch",
    "transform": "fa-solid fa-shuffle",
    "default": "fa-solid fa-cube"
  },
  "canvas": {
    "pattern": "dots"
  }
}
```

## Tips

- Use `baseMode: "dark"` for dark themes and `"light"` for light themes — this sets `color-scheme` which affects native browser controls (scrollbars, form elements).
- For `*-bg` status colors, use `rgba()` with low opacity (0.08–0.2) to blend with the surface background.
- Shadows should be stronger (higher opacity) for dark themes and lighter for light themes.
- The `preview` colors are only used in the theme selector dropdown — pick values that visually represent the theme at a glance.
- Any keys omitted from `colors` will fall back to the values defined in `theme.css` (the Vyshyvanka Light defaults).
- You can export any built-in theme from Settings and modify it as a starting point.
