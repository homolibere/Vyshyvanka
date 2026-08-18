---
inclusion: always
---

# Penpot Design-to-Code

Translating Penpot visual designs into Blazor WASM components for the Vyshyvanka Designer project.

## Workflow

1. **Inspect** the Penpot design via MCP — extract board structure, shapes, fills, typography, spacing, and layout.
2. **Map** visual properties to existing design tokens (see Token Mapping below).
3. **Identify** reusable component classes (`v-btn`, `v-input`, `v-modal`, etc.) that match the design intent.
4. **Generate** the Blazor 3-file component set: `.razor`, `.razor.cs`, `.razor.css`.
5. **Verify** accessibility, token usage, and theme compatibility.

## Component File Structure

Every component produces exactly 3 files:

| File | Purpose |
|------|---------|
| `Name.razor` | Markup only — no `@code` blocks, no `<style>` blocks |
| `Name.razor.cs` | Code-behind — `partial` class, `[Parameter]` props, event handlers |
| `Name.razor.css` | Scoped styles — uses CSS variables from the design system |

### Markup rules (`.razor`)

```razor
@namespace Vyshyvanka.Designer.Components
@* Brief component description *@

<div class="component-root" role="dialog" aria-modal="true">
    @* Content here *@
</div>
```

- First line: `@namespace Vyshyvanka.Designer.Components` (always, regardless of subfolder)
- Use `@onclick`, `@onkeydown` for interactivity — never inline JS
- Use `@onclick:stopPropagation="true"` for overlay click-through prevention
- Conditional classes via ternary: `class="item @(IsActive ? "active" : "")"`

### Code-behind rules (`.razor.cs`)

```csharp
namespace Vyshyvanka.Designer.Components;

public partial class ComponentName
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public string Title { get; set; } = "Default";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    [Inject] private ThemeService ThemeService { get; set; } = default!;
}
```

- `partial` class matching the component name
- `[Inject]` for services — never `@inject` in markup
- `[Parameter]` for all public API surface
- `EventCallback` / `EventCallback<T>` for output events
- Collection parameters use `IReadOnlyList<T>` with `= []` default

### Scoped CSS rules (`.razor.css`)

- Reference CSS variables exclusively — never hardcode colors, sizes, or spacing
- Use `var(--space-*)` for all spacing and padding
- Use `var(--font-size-*)` and `var(--font-weight-*)` for typography
- Use `var(--radius-*)` for border-radius
- Use theme color variables (`var(--bg-surface)`, `var(--text-primary)`, `var(--accent)`) for colors
- Use `var(--transition-*)` or `var(--duration-*)` with `var(--ease-*)` for animations

## Token Mapping: Penpot → CSS Variables

### Spacing (4px base grid)

Map Penpot padding/margin/gap pixel values to the nearest token:

| Penpot px | Token |
|-----------|-------|
| 4 | `--space-1` |
| 8 | `--space-2` |
| 12 | `--space-3` |
| 16 | `--space-4` |
| 20 | `--space-5` |
| 24 | `--space-6` |
| 32 | `--space-7` |
| 40 | `--space-8` |
| 48 | `--space-9` |
| 64 | `--space-10` |

### Typography

| Penpot font size | Token |
|------------------|-------|
| 11px | `--font-size-xs` |
| 12px | `--font-size-sm` |
| 13px | `--font-size-md` |
| 14px | `--font-size-base` |
| 16px | `--font-size-lg` |
| 18px | `--font-size-xl` |
| 20px | `--font-size-2xl` |
| 24px | `--font-size-3xl` |

Font families: `--font-family-base` (Inter) for UI text, `--font-family-mono` (JetBrains Mono) for code/expressions.

### Border Radius

| Penpot radius | Token |
|---------------|-------|
| 4px | `--radius-sm` |
| 6px | `--radius-md` |
| 8px | `--radius-lg` |
| 12px | `--radius-xl` |
| 16px | `--radius-2xl` |
| 9999px / pill | `--radius-full` |

### Colors

Never extract hex codes from Penpot and hardcode them. Map to semantic CSS variables:

| Visual role | Variable |
|-------------|----------|
| Page background | `--bg-primary` |
| Card/panel surface | `--bg-surface` |
| Elevated surface | `--bg-elevated` |
| Input background | `--bg-input` |
| Hover state bg | `--bg-hover` |
| Primary border | `--border-color` |
| Hover border | `--border-hover` |
| Heading text | `--text-primary` |
| Body text | `--text-secondary` |
| Disabled/hint text | `--text-muted` |
| Primary action | `--accent` |
| Primary action hover | `--accent-hover` |
| Success indicator | `--success` |
| Error/danger | `--danger` |
| Warning | `--warning` |
| Info | `--info` |

### Shadows & Overlays

| Penpot effect | Variable |
|---------------|----------|
| Subtle shadow | `--shadow-sm` |
| Card shadow | `--shadow-md` |
| Modal/elevated shadow | `--shadow-lg` |
| Backdrop dim | `--overlay-bg` |

## Reusable Component Classes

Before writing custom CSS, check if a shared `v-*` class already covers the need:

| Design element | Class |
|----------------|-------|
| Button (filled) | `v-btn v-btn--primary` |
| Button (outline) | `v-btn v-btn--secondary` |
| Button (minimal) | `v-btn v-btn--ghost` |
| Icon button | `v-btn v-btn--icon v-btn--ghost` |
| Toolbar button | `v-toolbar__btn` |
| Text input / textarea | `v-input` / `v-input v-textarea` |
| Dropdown | `v-input v-select` |
| Card | `v-card` with `v-card__header`, `v-card__body`, `v-card__footer` |
| Modal | `v-overlay` + `v-modal v-modal--md` |
| Badge/pill | `v-badge v-badge--success` |
| Panel | `v-panel` with `v-panel__header`, `v-panel__body` |
| Empty state | `v-empty` with `v-empty__icon`, `v-empty__title` |
| Spinner | `v-spinner` |
| Status dot | `v-status-dot v-status-dot--active` |

## Icons

- Use Font Awesome 6 exclusively — never emoji, never SVG icons inline
- Wrap with `v-icon` class: `<i class="fa-solid fa-play v-icon v-icon--sm"></i>`
- Icon-only buttons require `aria-label`
- Theme-mapped icons resolve via `ThemeService.GetIcon(key)` for node categories

## Layout Patterns

### Flex layout (most common)

```css
.container {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-5);
}
```

### Modal structure

```css
.modal-overlay { z-index: var(--z-modal); }
.modal-content {
    background: var(--bg-surface);
    border: 1px solid var(--border-color);
    border-radius: var(--radius-xl);
    box-shadow: var(--shadow-lg);
}
```

### Scrollable regions

Add `v-scrollbar` class or use `overflow-y: auto` with themed scrollbar styling.

## Accessibility Checklist

When converting a Penpot design to code, ensure:

- Interactive elements have minimum 44x44px touch targets
- Icon-only buttons have `aria-label`
- Modals use `role="dialog"` and `aria-modal="true"`
- Status information is conveyed by text, not color alone (use badges with text)
- Toggle buttons use `aria-pressed`
- Lists use semantic `<ul>`/`<ol>` elements
- Focus is managed for overlays (trap focus inside modal)
- Use `role="alert"` for toast notifications

## Penpot Inspection Prompt

Use this to extract design specs from a Penpot board:

```text
"For the board [NAME], extract:
1. Layout type (flex row/column, grid) and gaps
2. All text styles: font family, size (px), weight, color
3. All fills: background colors as hex
4. Spacing: padding, margins between elements (in px)
5. Border radius values
6. Shadow effects
7. Interactive states visible (hover, active, disabled variants)

Output as a structured mapping to Vyshyvanka design tokens.
Do not generate code yet — confirm the mapping first."
```

## Do NOT

- Hardcode hex colors — always use CSS variables from theme.css
- Use `px` for spacing — map to `--space-*` tokens
- Use `@code` blocks in `.razor` files
- Use `<style>` in `.razor` files
- Use `@inject` in markup — use `[Inject]` in code-behind
- Create components without all 3 files (.razor, .razor.cs, .razor.css)
- Use emoji for icons — use Font Awesome
- Skip `aria-label` on icon-only buttons
- Invent new color tokens — use existing semantic variables
