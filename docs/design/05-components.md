# Components

28 reusable components organized by category. All measurements are from the Penpot source file.

## Buttons

All buttons share: height 40px, border-radius 6px (`radius.md`), font 14px/500 Inter, flex centered content.

### Primary Button

| Property | Value |
|----------|-------|
| Size | 140×40 |
| Background | `#C62828` (Primary) |
| Text color | `#FFFFFF` |
| Border | None |
| Radius | 6px |
| Hover bg | `#A51C1C` (Primary Hover) |

### Secondary Button

| Property | Value |
|----------|-------|
| Size | 140×40 |
| Background | `#FFFFFF` |
| Text color | Neutral-800 |
| Border | 1px solid `#D6D3D1` (Neutral-300), inner alignment |
| Radius | 6px |

### Ghost Button

| Property | Value |
|----------|-------|
| Size | 140×40 |
| Background | Transparent |
| Text color | Neutral-800 |
| Border | None |
| Radius | 6px |

### Danger Button

| Property | Value |
|----------|-------|
| Size | 140×40 |
| Background | `#DC2626` (Danger) |
| Text color | `#FFFFFF` |
| Border | None |
| Radius | 6px |

### Icon Button

| Property | Value |
|----------|-------|
| Size | 36×36 |
| Background | `#F5F5F4` (Neutral-100) |
| Icon color | Neutral-700 |
| Border | None |
| Radius | 6px |
| Layout | Flex row, centered |

## Form Inputs

### Text Input

| Property | Value |
|----------|-------|
| Overall size | 280×68 (label + field) |
| Layout | Flex column, 8px gap |
| Label | 14px/500 Inter |
| Field height | 40px |
| Field bg | `#FFFFFF` |
| Field border | 1px solid Neutral-300 |
| Field radius | 6px |
| Placeholder | Neutral-400, 14px/400 |

### Select (Dropdown)

| Property | Value |
|----------|-------|
| Overall size | 280×68 (label + field) |
| Layout | Flex column |
| Field height | 40px |
| Field bg | `#FFFFFF` |
| Chevron icon | Right-aligned in field |

### Checkbox — Checked State

| Property | Value |
|----------|-------|
| Overall size | 120×24 |
| Box size | 18×18 |
| Box fill | Primary (`#C62828`) |
| Box radius | 4px |
| Check mark | White |
| Layout | Flex row, centered, 8px gap |

### Checkbox — Unchecked State

| Property | Value |
|----------|-------|
| Box size | 18×18 |
| Box fill | `#FFFFFF` |
| Box border | 1px solid Neutral-300 |
| Box radius | 4px |
| Label | 14px/400 Inter, Neutral-800 |

### Toggle — On

| Property | Value |
|----------|-------|
| Track size | 44×24 |
| Track fill | Primary (`#C62828`) |
| Track radius | 12px (pill) |
| Knob size | 20×20 |
| Knob fill | `#FFFFFF` |
| Knob position | Right-aligned (2px inset) |

### Toggle — Off

| Property | Value |
|----------|-------|
| Track size | 44×24 |
| Track fill | `#D6D3D1` (Neutral-300) |
| Track radius | 12px |
| Knob size | 20×20 |
| Knob fill | `#FFFFFF` |
| Knob position | Left-aligned (2px inset) |

## Feedback

### Success Toast

| Property | Value |
|----------|-------|
| Size | 320×48 |
| Layout | Flex row, centered vertically, 12px gap |
| Background | Success Light (`#DCFCE7`) |
| Icon | Success color, left |
| Text | 14px/400, Neutral-800 |
| Radius | 8px |

### Error Toast

| Property | Value |
|----------|-------|
| Size | 320×48 |
| Layout | Flex row, centered vertically, 12px gap |
| Background | Danger Light (`#FEE2E2`) |
| Icon | Danger color, left |
| Text | 14px/400, Neutral-800 |
| Radius | 8px |

### Confirm Dialog

| Property | Value |
|----------|-------|
| Size | 420×220 |
| Layout | Flex column, 24px gap, 24px padding |
| Background | `#FFFFFF` |
| Radius | 12px |
| Shadow | `shadow-xl` |
| Title | 18px/600 (H4) |
| Body | 14px/400, Neutral-600 |
| Actions | Flex row, right-aligned, 12px gap |

## Layout Components

### Toolbar

| Property | Value |
|----------|-------|
| Size | 700×56 (light) or 1440×48 (dark designer) |
| Layout | Flex row, space-between, center aligned |
| Background | `#FFFFFF` (light) or `#292524` (dark) |
| Content | Brand text (left), search box (center), avatar (right) |
| Search box | 240×32, Neutral-100 bg, radius 6px |
| Avatar | 32×32 circle |

### Sidebar

| Property | Value |
|----------|-------|
| Size | 240×400+ (expands to fill height) |
| Layout | Flex column, 2px gap |
| Background | `#FFFFFF` (light mode) |
| Nav item height | 36px |
| Nav item width | 224px (8px padding from sidebar edge) |
| Nav item radius | 6px |
| Nav item active | Primary-Light bg with Primary text |

### Workflow Card

| Property | Value |
|----------|-------|
| Size | 320×160 |
| Layout | Flex column, 16px padding |
| Background | `#FFFFFF` |
| Border | 1px solid Neutral-200 |
| Radius | 8px |
| Shadow | `shadow-sm` on idle, `shadow-md` on hover |
| Title | 16px/600 |
| Description | 14px/400, Neutral-500, 2-line clamp |
| Footer | 24px height, meta info (timestamp, node count) |

## Component Composition Rules

1. **Min touch target:** All interactive elements maintain 36px minimum (icon buttons) or 40px (standard buttons).
2. **Label placement:** Always above the input field, never inline or floating.
3. **Button ordering:** Primary action rightmost, secondary/cancel leftmost.
4. **Dialog actions:** Right-aligned with 12px gap; typically Cancel (Ghost) + Confirm (Primary or Danger).
5. **Toast dismissal:** Auto-dismiss after 5s or manual close via icon button.
6. **Sidebar items:** Icons optional but consistent — if one nav item has an icon, all should.
