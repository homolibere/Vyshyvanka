# Typography

Two font families serve distinct roles. Inter handles all UI text; JetBrains Mono is reserved for code, expressions, and technical content.

## Font Families

| Role | Family | Token | Fallback |
|------|--------|-------|----------|
| UI (sans-serif) | Inter | `fontFamily.sans` | system-ui, -apple-system, sans-serif |
| Code (monospace) | JetBrains Mono | `fontFamily.mono` | ui-monospace, monospace |

## Type Scale — Inter (UI)

### Headings

| Style | Size | Weight | Line Height | Letter Spacing | Usage |
|-------|------|--------|-------------|----------------|-------|
| Display | 48px | 700 (Bold) | 1.1 | -0.02em | Hero text, empty state headlines |
| H1 | 30px | 700 (Bold) | 1.2 | -0.01em | Page titles |
| H2 | 24px | 600 (Semibold) | 1.3 | -0.01em | Section headers |
| H3 | 20px | 600 (Semibold) | 1.4 | 0 | Card titles, panel headers |
| H4 | 18px | 600 (Semibold) | 1.4 | 0 | Subsection headers |

### Body

| Style | Size | Weight | Line Height | Letter Spacing | Usage |
|-------|------|--------|-------------|----------------|-------|
| Large | 16px | 400 (Regular) | 1.5 | 0 | Lead paragraphs, descriptions |
| Default | 14px | 400 (Regular) | 1.5 | 0 | Body text, form labels |
| Default (Medium) | 14px | 500 (Medium) | 1.4 | 0 | Button text, nav items, emphasis |
| Small | 12px | 400 (Regular) | 1.5 | 0 | Helper text, secondary info |
| Small (Medium) | 12px | 500 (Medium) | 1.4 | 0 | Node header labels, badges |
| Caption | 11px | 400 (Regular) | 1.4 | 0.01em | Captions, port labels, timestamps |

## Type Scale — JetBrains Mono (Code)

| Style | Size | Weight | Line Height | Letter Spacing | Usage |
|-------|------|--------|-------------|----------------|-------|
| Code Default | 13px | 400 (Regular) | 1.6 | 0 | Expression editor, code blocks |
| Code Small | 12px | 400 (Regular) | 1.5 | 0 | Inline code, node property values |

## Font Weight Tokens

| Token | Value | CSS Name |
|-------|-------|----------|
| `fontWeight.regular` | 400 | Regular |
| `fontWeight.medium` | 500 | Medium |
| `fontWeight.semibold` | 600 | Semibold |
| `fontWeight.bold` | 700 | Bold |

## Font Size Tokens

| Token | Value |
|-------|-------|
| `fontSize.xs` | 11px |
| `fontSize.sm` | 12px |
| `fontSize.md` | 14px |
| `fontSize.lg` | 16px |
| `fontSize.xl` | 18px |
| `fontSize.2xl` | 20px |
| `fontSize.3xl` | 24px |
| `fontSize.4xl` | 30px |
| `fontSize.5xl` | 36px |
| `fontSize.6xl` | 48px |

## Usage Guidelines

- **Minimum size:** 11px (caption only). Never go below 11px for legibility.
- **Default reading size:** 14px for all standard content.
- **Buttons and interactive elements:** Always 14px medium (500).
- **Node port labels:** 12px regular (they're tight on space).
- **Headings:** Use semibold (600) for H2–H4, bold (700) only for H1 and Display.
- **Letter spacing:** Negative tracking only on 24px+ headings. Never add extra letter-spacing to body text.
- **Line height:** 1.4–1.5 for body, 1.1–1.3 for headings, 1.6 for code (to account for ascenders/descenders with monospace).

## CSS Variable Mapping

```css
:root {
  --font-family-sans: 'Inter', system-ui, -apple-system, sans-serif;
  --font-family-mono: 'JetBrains Mono', ui-monospace, monospace;

  --font-size-xs: 0.6875rem;   /* 11px */
  --font-size-sm: 0.75rem;     /* 12px */
  --font-size-md: 0.875rem;    /* 14px */
  --font-size-lg: 1rem;        /* 16px */
  --font-size-xl: 1.125rem;    /* 18px */
  --font-size-2xl: 1.25rem;    /* 20px */
  --font-size-3xl: 1.5rem;     /* 24px */
  --font-size-4xl: 1.875rem;   /* 30px */
  --font-size-5xl: 2.25rem;    /* 36px */
  --font-size-6xl: 3rem;       /* 48px */

  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-semibold: 600;
  --font-weight-bold: 700;
}
```
