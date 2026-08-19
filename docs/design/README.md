# Vyshyvanka Design System

Comprehensive design system reference extracted from the Penpot source file. This documentation serves as the single source of truth for the UI redesign.

## Penpot File Structure

| Page | Purpose |
|------|---------|
| Cover | Title page |
| Foundations | Color palette, typography scale, spacing, radius, and shadow definitions |
| Components | Reusable UI components (buttons, inputs, cards, dialogs, layout shells) |
| Node System | Canvas node rendering — categories, states, ports, connections, execution indicators |
| Screen Layouts | Full-screen compositions showing page structure at 1440×900 |

## Documentation Index

| File | Contents |
|------|----------|
| [01-colors.md](01-colors.md) | Full color palette — brand, neutral scale, semantic, node category, canvas |
| [02-typography.md](02-typography.md) | Typography styles, font families, type scale |
| [03-tokens.md](03-tokens.md) | Design tokens — spacing, radius, font sizes, weights, families |
| [04-shadows.md](04-shadows.md) | Elevation system — shadow levels |
| [05-components.md](05-components.md) | Component library — buttons, inputs, toggles, cards, dialogs, layout |
| [06-node-system.md](06-node-system.md) | Node canvas — anatomy, categories, states, ports, connections, execution |
| [07-screen-layouts.md](07-screen-layouts.md) | Screen templates — layout shells, regions, dimensions |

## Design Principles

1. **Dark canvas, light chrome** — The workflow designer uses a dark background (`neutral.900`) for immersive node editing; management screens (browser, settings) use a light background (`neutral.50`).
2. **Color = category** — Node headers communicate category at a glance: green for triggers, blue for actions, amber for logic, purple for transforms.
3. **Stone neutral palette** — The neutral scale is warm-toned (stone family, not pure gray), giving the interface a distinctive warmth.
4. **Red brand identity** — Primary brand color is a deep red (`#C62828`) with Ukrainian cultural resonance, used sparingly for CTAs and selection indicators.
5. **Single typeface system** — Inter for all UI text, JetBrains Mono for code/expressions. No decorative fonts.
6. **4px grid** — All spacing values are multiples of 4 (with a 2px option for tight contexts).

## Quick Reference

- **Target resolution:** 1440×900 (desktop-first)
- **Font stack:** Inter (sans), JetBrains Mono (mono)
- **Base font size:** 14px / weight 400–500
- **Sidebar width:** 240px
- **Toolbar height:** 48px (designer) / 56px (management)
- **Node width:** 220px fixed
- **Border radius default:** 6px (controls) / 8px (cards/nodes) / 12px (modals)
