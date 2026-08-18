---
inclusion: always
---

# Penpot MCP API Patterns

Conventions, critical gotchas, and reusable patterns for the Penpot MCP plugin (`mcp_penpot_execute_code`). Use `mcp_penpot_penpot_api_info` for detailed type documentation.

## Workflow

1. Call `mcp_penpot_high_level_overview` once per session to confirm connection.
2. Discover existing design system before creating anything (see Discovery section).
3. Use idempotent helpers for all library/page/board creation — calls are retried after partial failures.
4. Limit each `execute_code` call to ~10 mutations max; verify after each batch.
5. Separate page navigation from writes into distinct calls (defensive habit for all Penpot versions).

## Critical API Gotchas

| Property / Behaviour | Status | Correct Approach |
|---|---|---|
| `shape.width` / `shape.height` | READ-ONLY | `shape.resize(w, h)` |
| `shape.parentX` / `shape.parentY` | READ-ONLY | `penpotUtils.setParentXY(shape, x, y)` |
| `shape.x` / `shape.y` for parented shapes | READ-ONLY | `penpotUtils.setParentXY(shape, x, y)` |
| `shape.x` / `shape.y` for root-level shapes | Works | Direct assignment OK for top-level boards |
| Z-ordering via `appendChild` | Ignores order | `parent.insertChild(index, shape)` |
| `penpot.createText(...)` | Nullable | Always null-check before styling |
| Text clips after `resize()` | Reset required | Set `growType` after every `text.resize()` |
| Flex children order (column) | Reversed | Last inserted = visually top |
| Page switch + write in same call | Unreliable | Two separate calls: navigate, then write |
| Large batch writes | Silent timeout | Max ~10 ops per call; verify structurally |
| `shape.remove()` | Unreliable cross-call | Verify removal in a subsequent call |
| Library `fontSize` | Must be string | `"16"` not `16` |
| Shadow `color` field | Color object | `{ color: '#hex', opacity: 0.15 }` |
| `LibraryColor.color` | Different from fill | `color.color = '#hex'` not `color.fillColor` |
| Library typography field | `fontFamilies` | Not `fontFamily` (plural) |

## Core Read Operations

```javascript
// Page and shape discovery
penpotUtils.getPages();                          // [{id, name}]
penpotUtils.getPageByName("Mobile");             // Page | null
penpotUtils.shapeStructure(penpot.root);         // tree overview
penpotUtils.findShapes((s) => condition, penpot.root); // predicate search
penpotUtils.findShape((s) => s.name === "X");    // first match
penpotUtils.findShapeById("uuid");               // by ID
page.findShapes({ type: "board" });              // criteria-based on Page

// Library
penpot.library.local.components;
penpot.library.local.colors;
penpot.library.local.typographies;
penpot.library.local.tokens;        // TokenCatalog
penpot.library.local.tokens.sets;   // TokenSet[]
penpot.library.local.tokens.themes; // TokenTheme[]
```

## Core Create/Modify Operations

```javascript
// Create shapes
const board = penpot.createBoard();
const rect = penpot.createRectangle();
const text = penpot.createText("Hello");  // returns Text | null
const ellipse = penpot.createEllipse();

// Size — always resize(), never assign width/height
shape.resize(400, 300);

// Position — root-level: direct assign; parented: utility
board.x = 100; board.y = 200;              // root-level only
penpotUtils.setParentXY(child, 100, 200);  // parented shapes

// Text — always pair resize with growType
text.resize(200, 0);
text.growType = "auto-height";  // MUST follow every resize
text.fontFamily = "Inter";
text.fontSize = "16";           // string
text.fontWeight = "500";        // string

// Fills, strokes, effects
shape.fills = [{ fillColor: "#3451B2", fillOpacity: 1 }];
shape.strokes = [{ strokeColor: "#2e3434", strokeOpacity: 1, strokeStyle: "solid", strokeWidth: 2, strokeAlignment: "center" }];
shape.shadows = [{ style: "drop-shadow", offsetX: 0, offsetY: 8, blur: 32, spread: 0, color: { color: "#000000", opacity: 0.08 }, hidden: false }];
shape.blurs = [{ type: "layer-blur", value: 20, hidden: false }];
shape.borderRadius = 8;
shape.opacity = 0.9;

// Flex layout
const layout = board.addFlexLayout();
layout.dir = "row";  // 'row' | 'column' | 'row-reverse' | 'column-reverse'
layout.gap = 16;
layout.padding = { top: 16, right: 16, bottom: 16, left: 16 };
layout.justifyContent = "center";  // 'start' | 'center' | 'end' | 'space-between'
layout.alignItems = "center";      // 'start' | 'center' | 'end' | 'stretch'
```

## Gradient Fills

```javascript
// Linear gradient
shape.fills = [{ fillColorGradient: {
  type: "linear", startX: 0.5, startY: 0, endX: 0.5, endY: 1, width: 1,
  stops: [{ color: "#FF0000", opacity: 1, offset: 0 }, { color: "#0000FF", opacity: 1, offset: 1 }]
}}];

// Radial gradient
shape.fills = [{ fillColorGradient: {
  type: "radial", startX: 0.5, startY: 0.5, endX: 1, endY: 0.5, width: 0.5,
  stops: [{ color: "#FFFFFF", opacity: 0.2, offset: 0 }, { color: "#FFFFFF", opacity: 0, offset: 1 }]
}}];

// Image fill (async — must await)
const imageData = await penpot.uploadMediaUrl("name", "https://trusted-source.com/img.jpg");
shape.fills = [{ fillOpacity: 1, fillImage: imageData }];
```

## Interactions & Prototyping

```javascript
// Add navigation interaction
source.addInteraction("click", {
  type: "navigate-to", destination: targetBoard,
  animation: { type: "dissolve", duration: 300, easing: "ease-in-out" }
});

// Triggers: 'click' | 'mouse-enter' | 'mouse-leave' | 'after-delay'
// Actions: 'navigate-to' | 'open-overlay' | 'toggle-overlay' | 'close-overlay' | 'previous-screen' | 'open-url'
// Animations: { type: 'dissolve'|'slide'|'push', duration, easing?, direction?, way? }

// Overlay
source.addInteraction("click", {
  type: "open-overlay", destination: overlayBoard,
  position: "center", closeWhenClickOutside: true, addBackgroundOverlay: true,
  animation: { type: "dissolve", duration: 200 }
});

// Create prototype flow (entry point)
penpot.currentPage.createFlow("FlowName", entryBoard);
```

Animation duration guide: 100ms (state toggle), 200ms (component transition), 300ms (navigation), 400ms+ (onboarding/hero).

## Idempotent Helpers (always use these)

```javascript
function ensurePage(name) {
  const existing = penpotUtils.getPageByName(name);
  if (existing) { penpot.openPage(existing); return existing; }
  const page = penpot.createPage();
  page.name = name;
  penpot.openPage(page);
  return page;
}

function ensureBoard(name, x, y, w, h, fill = "#F5F5F5") {
  const existing = penpotUtils.findShape((s) => s.type === "board" && s.name === name);
  if (existing) return existing;
  const board = penpot.createBoard();
  board.name = name; board.resize(w, h); board.x = x; board.y = y;
  board.fills = [{ fillColor: fill, fillOpacity: 1 }];
  return board;
}

function ensureColor(name, hex) {
  return penpot.library.local.colors.find((c) => c.name === name) || (() => {
    const c = penpot.library.local.createColor(); c.name = name; c.color = hex; return c;
  })();
}

function ensureTypography(name, fontFamilies, weight, size, lineHeight, letterSpacing) {
  return penpot.library.local.typographies.find((t) => t.name === name) || (() => {
    const t = penpot.library.local.createTypography();
    t.name = name; t.fontFamilies = fontFamilies; t.fontWeight = weight;
    t.fontSize = size; t.lineHeight = lineHeight; t.letterSpacing = letterSpacing;
    return t;
  })();
}

function ensureSet(name) {
  return penpot.library.local.tokens.sets.find((s) => s.name === name)
    || penpot.library.local.tokens.addSet({ name });
}

function addToken(set, type, name, value) {
  return set.tokens.find((t) => t.name === name && t.type === type)
    || set.addToken({ type, name, value: String(value) });
}
```

## Token API (W3C DTCG)

```javascript
const catalog = penpot.library.local.tokens;
// TokenTypes: 'color' | 'dimension' | 'spacing' | 'typography' | 'shadow' | 'opacity'
//   | 'borderRadius' | 'borderWidth' | 'fontWeights' | 'fontSizes' | 'fontFamilies'
//   | 'letterSpacing' | 'textDecoration' | 'textCase' | 'number' | 'sizing'

// Token values can reference others: '{color.base.500}'
const set = ensureSet("brand/base");
addToken(set, "color", "color.brand.primary", "#3451B2");
addToken(set, "spacing", "spacing.md", "16");

// Themes group sets; activate/deactivate
const theme = catalog.addTheme({ group: "Theme", name: "Light" });
theme.addSet(set);
theme.toggleActive();
```

## Design System Discovery (run before any design work)

```javascript
const allShapes = penpotUtils.findShapes(() => true, penpot.root);
const colors = new Set();
allShapes.forEach((s) => {
  if (s.fills) s.fills.forEach((f) => { if (f.fillColor) colors.add(f.fillColor); });
});
return {
  pages: penpotUtils.getPages(),
  components: penpot.library.local.components.length,
  colorStyles: penpot.library.local.colors.length,
  typographies: penpot.library.local.typographies.length,
  tokenSets: penpot.library.local.tokens.sets.length,
  uniqueColorsInUse: colors.size,
  textStyles: [...new Set(allShapes.filter((s) => s.type === "text")
    .map((s) => `${s.fontFamily} ${s.fontSize}/${s.fontWeight}`))].slice(0, 10)
};
```

## storage Global (Cross-Call State)

The `storage` object persists across `execute_code` calls within a session. Use it to share computed data between calls. Always use `|| fallback` when reading since it resets on server restart.

```javascript
// Store
storage.designSystem = { colors: { primary: "#3451B2" } };
// Retrieve (later call)
const DS = storage.designSystem || { colors: {} };
```

## Board Positioning Conventions

```javascript
// Find rightmost edge for next board placement
const boards = penpotUtils.findShapes((s) => s.type === "board", penpot.root);
let nextX = 0;
boards.forEach((b) => { const edge = b.x + b.width; if (edge + 100 > nextX) nextX = edge + 100; });
```

- 100px gap: related screens (same flow)
- 200px+ gap: separate flows/sections
- Wireframes left, final designs right

## Platform Sizes

| Platform | Width | Height |
|---|---|---|
| Mobile | 375 | 812 |
| Tablet | 768 | 1024 |
| Desktop | 1440 | 900 |

## Plugin Data (Persistent Metadata)

```javascript
shape.setPluginData("key", "value");     // shape-scoped
page.setPluginData("role", "foundations"); // page-scoped
penpot.library.local.setPluginData("spec", JSON.stringify(data)); // file-scoped
shape.setSharedPluginData("namespace", "key", "value"); // cross-plugin
```

## Community Plugin Boundaries

The Penpot MCP Server cannot list, install, or invoke other community plugins. It only accesses file content, libraries, comments, user data, and plugin/shared plugin data. If a task requires a community plugin, ask the user to run it manually, then re-inspect via MCP.

## Page Management

```javascript
// Create and navigate (separate calls for navigation + writes)
const page = penpot.createPage(); page.name = "Foundations";
penpot.openPage(page);  // navigate — writes go in NEXT call

// Ruler guides
page.addRulerGuide("vertical", 320);
page.addRulerGuide("horizontal", 64, board); // board-scoped
```

## Validation Checklist Patterns

```javascript
// Run to audit current page
const boards = penpotUtils.findShapes((s) => s.type === "board", penpot.root);
return {
  tinyText: penpotUtils.findShapes((s) => s.type === "text" && Number(s.fontSize) < 12, penpot.root).length,
  autoNamed: penpotUtils.findShapes((s) => /^(Rectangle|Ellipse|Text|Group|Frame|Board)\s*\d+$/.test(s.name), penpot.root).length,
  unwiredBoards: boards.filter((b) => !b.interactions?.length).map((b) => b.name),
  maxDepth: (function getDepth(s, d=0) { return s.children?.length ? Math.max(...s.children.map((c) => getDepth(c, d+1))) : d; })(penpot.root)
};
```

## Component Design Standards

- Buttons: min 44x44px touch target, states (default/hover/active/disabled/loading), WCAG AA contrast
- Inputs: label above (never placeholder-only), states (default/focus/error/disabled), min 44px height
- Navigation: active state indicated, max 7 items, 48px touch targets on mobile
- Cards: clear hierarchy, hover/focus if interactive, empty state
- Overlays: boards prefixed `overlay/`, close-on-outside-click, backdrop

## Default Fallback Tokens (only when no existing design system)

- Spacing (8px base): xs=4, sm=8, md=16, lg=24, xl=32, 2xl=48
- Border radius: sm=4, md=8, lg=16, full=9999, overlay=20
- Typography: Display 48-64/700, H1 32-40/700, H2 24-28/600, H3 20-22/600, Body 16/400, Small 14/400, Caption 12/400
- Semantic colors: Success #22C55E, Warning #F59E0B, Error #EF4444

## CSS Export

```javascript
const css = penpot.generateStyle(penpot.selection[0], { type: "css", includeChildren: true });
```

Note: `export_shape` (raster/SVG) may fail with HTTP errors. Always verify structurally via the API; treat export as best-effort.
