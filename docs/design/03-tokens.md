# Design Tokens

All design tokens are organized in 4 token sets under the `Vyshyvanka / Default` theme. All sets are active simultaneously.

## Token Sets Overview

| Set Name | Token Count | Purpose |
|----------|-------------|---------|
| `core/colors` | 35 | All color values — brand, neutral, semantic, node, canvas |
| `core/spacing` | 13 | Spacing scale from 2px to 96px |
| `core/radius` | 7 | Border radius values from 0 to pill (9999) |
| `core/typography` | 16 | Font sizes, weights, and family declarations |

## core/spacing

Base unit: 4px. The scale covers micro adjustments (2px) through large section spacing (96px).

| Token | Value | Use Case |
|-------|-------|----------|
| `spacing.2xs` | 2px | Hairline gaps, icon-to-text micro spacing |
| `spacing.xs` | 4px | Tight padding (port labels, badge internal) |
| `spacing.sm` | 8px | Compact spacing (between related items, icon gaps) |
| `spacing.md` | 12px | Default inner padding, form field internal |
| `spacing.lg` | 16px | Standard padding (cards, panels, buttons), gap between items |
| `spacing.xl` | 20px | Comfortable padding, section content inset |
| `spacing.2xl` | 24px | Panel padding, form group spacing, config-panel gap |
| `spacing.3xl` | 32px | Section spacing, larger gaps between groups |
| `spacing.4xl` | 40px | Component group spacing |
| `spacing.5xl` | 48px | Toolbar height (designer), large section margins |
| `spacing.6xl` | 64px | Hero spacing, modal content padding |
| `spacing.7xl` | 80px | Page-level spacing |
| `spacing.8xl` | 96px | Maximum spacing — used sparingly for very open layouts |

### Spacing Usage Patterns

- **Button padding:** `spacing.md` (12px) vertical, `spacing.lg` (16px) horizontal
- **Card padding:** `spacing.xl` (20px)
- **Form field gap:** `spacing.sm` (8px) label-to-input, `spacing.lg` (16px) between fields
- **Panel internal:** `spacing.2xl` (24px)
- **Sidebar nav items:** `spacing.2xs` (2px) gap between rows
- **Toolbar padding:** `spacing.lg` (16px) horizontal

## core/radius

| Token | Value | Use Case |
|-------|-------|----------|
| `radius.none` | 0px | Sharp corners (dividers, full-bleed elements) |
| `radius.sm` | 4px | Small elements (badges, tags, inline code) |
| `radius.md` | 6px | Controls (buttons, inputs, selects, icon buttons) |
| `radius.lg` | 8px | Cards, nodes, panels, toasts |
| `radius.xl` | 12px | Modals, large cards, toggle tracks |
| `radius.2xl` | 16px | Large overlays, feature cards |
| `radius.full` | 9999px | Pills, circular avatars, rounded toggles |

### Radius Assignment

| Element | Radius Token |
|---------|--------------|
| Button | `radius.md` (6px) |
| Input field | `radius.md` (6px) |
| Icon button | `radius.md` (6px) |
| Node card | `radius.lg` (8px) |
| Node header | `radius.lg` (8px) top corners only |
| Toast notification | `radius.lg` (8px) |
| Workflow card | `radius.lg` (8px) |
| Modal dialog | `radius.xl` (12px) |
| Confirm dialog | `radius.xl` (12px) |
| Toggle switch track | `radius.xl` (12px) |
| Avatar / status dot | `radius.full` |
| Badge / pill | `radius.full` |

## core/typography

See [02-typography.md](02-typography.md) for the full type scale. Token names:

### Font Sizes

`fontSize.xs` (11) · `fontSize.sm` (12) · `fontSize.md` (14) · `fontSize.lg` (16) · `fontSize.xl` (18) · `fontSize.2xl` (20) · `fontSize.3xl` (24) · `fontSize.4xl` (30) · `fontSize.5xl` (36) · `fontSize.6xl` (48)

### Font Weights

`fontWeight.regular` (400) · `fontWeight.medium` (500) · `fontWeight.semibold` (600) · `fontWeight.bold` (700)

### Font Families

`fontFamily.sans` → Inter · `fontFamily.mono` → JetBrains Mono

## core/colors

See [01-colors.md](01-colors.md) for the full palette. Token hierarchy:

```
color.brand.primary
color.brand.primary-hover
color.brand.primary-light
color.brand.secondary
color.brand.secondary-hover
color.neutral.white / .50 / .100 / .200 / .300 / .400 / .500 / .600 / .700 / .800 / .900 / .950
color.success / .success-light
color.warning / .warning-light
color.danger / .danger-light
color.info / .info-light
color.node.trigger / .action / .logic / .transform
color.canvas.bg / .grid / .node-bg / .node-border / .connection / .port
```

## Token → CSS Variable Naming Convention

Transform token dot-notation to CSS variable kebab-case:

| Token | CSS Variable |
|-------|--------------|
| `color.brand.primary` | `--color-brand-primary` |
| `spacing.lg` | `--spacing-lg` |
| `radius.md` | `--radius-md` |
| `fontSize.md` | `--font-size-md` |
| `fontWeight.medium` | `--font-weight-medium` |
| `fontFamily.sans` | `--font-family-sans` |
