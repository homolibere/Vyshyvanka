# Vyshyvanka Design System

## Overview

The Vyshyvanka Design System provides a consistent foundation of tokens, components, and patterns for the Designer UI. It ensures visual consistency, reduces CSS duplication, and improves accessibility.

## Architecture

```
wwwroot/css/
├── design-tokens.css   → Spacing, typography, radii, transitions, z-index, sizes
├── theme.css           → Color tokens (light/dark fallbacks, overridden by theme JSON)
├── components.css      → Shared component classes (v-btn, v-input, v-modal, etc.)
└── app.css             → Global app styles, SVG canvas styles, Bootstrap overrides
```

**Load order matters:** `design-tokens.css` → `theme.css` → `components.css` → `app.css`

## Design Tokens

### Spacing Scale (4px base unit)

| Token | Value | Use |
|-------|-------|-----|
| `--space-0` | 0 | Reset |
| `--space-1` | 4px | Tight gaps, inline spacing |
| `--space-2` | 8px | Button icon gaps, small padding |
| `--space-3` | 12px | Input padding, panel headers |
| `--space-4` | 16px | Standard padding, section spacing |
| `--space-5` | 20px | Modal padding |
| `--space-6` | 24px | Card padding, grid gaps |
| `--space-7` | 32px | Section separators |
| `--space-8` | 40px | Empty state padding |
| `--space-9` | 48px | Large section spacing |
| `--space-10` | 64px | Page-level spacing |

### Typography

**Families:**
- `--font-family-base`: Inter (with Helvetica Neue fallback)
- `--font-family-mono`: JetBrains Mono (for code, expressions)

**Size Scale:**

| Token | Value | Use |
|-------|-------|-----|
| `--font-size-xs` | 11px | Port labels, badges |
| `--font-size-sm` | 12px | Toolbar buttons, node titles, labels |
| `--font-size-md` | 13px | Compact text, secondary info |
| `--font-size-base` | 14px | Body text, inputs, buttons |
| `--font-size-lg` | 16px | Section titles, feature text |
| `--font-size-xl` | 18px | Modal titles, h3 |
| `--font-size-2xl` | 20px | Page subtitles |
| `--font-size-3xl` | 24px | Page titles |
| `--font-size-4xl` | 32px | Large headings |
| `--font-size-5xl` | 40px | Hero headings |

**Weights:** `regular` (400), `medium` (500), `semibold` (600), `bold` (700)

### Border Radius

| Token | Value | Use |
|-------|-------|-----|
| `--radius-sm` | 4px | Toolbar buttons, small elements |
| `--radius-md` | 6px | Standard buttons, inputs |
| `--radius-lg` | 8px | Cards, nodes |
| `--radius-xl` | 12px | Modals, panels |
| `--radius-2xl` | 16px | Large cards |
| `--radius-full` | 9999px | Badges, pills, avatars |

### Transitions

| Token | Use |
|-------|-----|
| `--transition-fast` | 100ms — hover states on small elements |
| `--transition-normal` | 150ms — standard interactive feedback |
| `--transition-slow` | 250ms — modal open/close |
| `--transition-color` | Multi-property color transition |
| `--transition-transform` | Scale/translate effects |
| `--transition-shadow` | Shadow elevation changes |

### Z-Index Layers

| Token | Value | Use |
|-------|-------|-----|
| `--z-dropdown` | 100 | Autocomplete, popovers |
| `--z-sticky` | 200 | Sticky headers |
| `--z-overlay` | 300 | Backdrop overlays |
| `--z-modal` | 1000 | Modal dialogs |
| `--z-popover` | 1100 | Popovers over modals |
| `--z-toast` | 1200 | Toast notifications |
| `--z-tooltip` | 1300 | Tooltips (highest) |

## Component Classes

All shared classes use the `v-` prefix to avoid conflicts with Bootstrap.

### Buttons (`v-btn`)

```html
<!-- Primary -->
<button class="v-btn v-btn--primary">Save</button>

<!-- Secondary (default) -->
<button class="v-btn v-btn--secondary">Cancel</button>

<!-- Ghost (minimal) -->
<button class="v-btn v-btn--ghost">Close</button>

<!-- Icon-only -->
<button class="v-btn v-btn--icon v-btn--ghost" aria-label="Close">
    <i class="fa-solid fa-xmark"></i>
</button>

<!-- Sizes -->
<button class="v-btn v-btn--primary v-btn--sm">Small</button>
<button class="v-btn v-btn--primary v-btn--lg">Large</button>
```

### Toolbar Buttons (`v-toolbar__btn`)

Compact buttons for the designer toolbar:

```html
<button class="v-toolbar__btn">
    <i class="fa-solid fa-floppy-disk v-icon v-icon--sm"></i>
    <span>Save</span>
</button>

<!-- Active state -->
<button class="v-toolbar__btn v-toolbar__btn--active">Active</button>

<!-- Primary variant -->
<button class="v-toolbar__btn v-toolbar__btn--primary">Save</button>
```

### Inputs (`v-input`)

```html
<input class="v-input" type="text" placeholder="Search..." />
<input class="v-input v-input--sm" />
<textarea class="v-input v-textarea"></textarea>
<select class="v-input v-select">...</select>
```

### Cards (`v-card`)

```html
<div class="v-card">
    <div class="v-card__header">Title</div>
    <div class="v-card__body">Content</div>
    <div class="v-card__footer">Actions</div>
</div>

<!-- Interactive (hover effects) -->
<div class="v-card v-card--interactive">...</div>
```

### Modals (`v-overlay` + `v-modal`)

```html
<div class="v-overlay v-overlay--open">
    <div class="v-modal v-modal--md">
        <div class="v-modal__header">
            <h2 class="v-modal__title">Title</h2>
            <button class="v-btn v-btn--icon v-btn--ghost">×</button>
        </div>
        <div class="v-modal__body">Content</div>
        <div class="v-modal__footer">
            <button class="v-btn v-btn--secondary">Cancel</button>
            <button class="v-btn v-btn--primary">Confirm</button>
        </div>
    </div>
</div>
```

Sizes: `v-modal--sm` (420px), `v-modal--md` (640px), `v-modal--lg` (900px), `v-modal--xl` (1400px), `v-modal--full` (1400px + 85vh height).

### Badges (`v-badge`)

```html
<span class="v-badge v-badge--success">Completed</span>
<span class="v-badge v-badge--danger">Failed</span>
<span class="v-badge v-badge--warning">Pending</span>
<span class="v-badge v-badge--info">Running</span>
<span class="v-badge v-badge--accent">Active</span>
```

### Panels (`v-panel`)

```html
<div class="v-panel">
    <div class="v-panel__header">
        <span class="v-panel__title">Node Palette</span>
    </div>
    <div class="v-panel__body v-scrollbar">Content</div>
</div>
```

### Status Indicators

```html
<span class="v-status-dot v-status-dot--active"></span>
<span class="v-status-dot v-status-dot--running"></span>
<span class="v-spinner"></span>
<span class="v-spinner v-spinner--sm"></span>
```

### Empty States

```html
<div class="v-empty">
    <i class="v-empty__icon fa-solid fa-inbox"></i>
    <p class="v-empty__title">No workflows yet</p>
    <p class="v-empty__description">Create your first workflow to get started.</p>
    <button class="v-btn v-btn--primary">Create Workflow</button>
</div>
```

## Accessibility

- All interactive elements must have `aria-label` when text content is not sufficient
- Use `v-sr-only` class for screen-reader-only text
- Focus rings use `v-focusable` class or `:focus-visible` pseudo-class
- Toolbar toggle buttons use `aria-pressed`
- Modals use `role="dialog"` and `aria-modal="true"`
- Status dots and badges provide color + text (never color alone)

## Icons

Use Font Awesome 6 icons exclusively. The theme system maps logical icon keys to FA classes:

| Key | Class | Use |
|-----|-------|-----|
| `trigger` | `fa-solid fa-bolt` | Trigger nodes |
| `action` | `fa-solid fa-cog` | Action nodes |
| `logic` | `fa-solid fa-code-branch` | Logic nodes |
| `execute` | `fa-solid fa-play` | Run/execute |
| `save` | `fa-solid fa-floppy-disk` | Save actions |
| `delete` | `fa-solid fa-trash` | Delete actions |
| `search` | `fa-solid fa-magnifying-glass` | Search |
| `close` | `fa-solid fa-xmark` | Close/dismiss |

**Do NOT use emoji in the UI.** Always use FA icons with the `v-icon` wrapper:

```html
<i class="fa-solid fa-play v-icon v-icon--sm"></i>
```

## Migration Guide

When refactoring existing component CSS:

1. Replace hardcoded px values with `--space-*` tokens
2. Replace font-sizes with `--font-size-*` tokens
3. Replace border-radius with `--radius-*` tokens
4. Replace `transition: all 0.15s ease` with `transition: var(--transition-color)` or specific composites
5. Replace `z-index: 1100` with `var(--z-popover)`
6. Use shared classes (`v-btn`, `v-input`, `v-modal`) instead of re-defining locally
7. Replace emoji with Font Awesome + `v-icon`
8. Add `aria-label` to icon-only buttons
