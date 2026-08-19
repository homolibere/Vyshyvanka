---
inclusion: always
---

# Prototyping Workflows

Flows, interactions, animations, overlays, and high-fidelity prototype construction via Penpot MCP.

## Table of Contents

1. [File Setup for Prototyping](#1-file-setup-for-prototyping)
2. [Wiring a Linear Flow](#2-wiring-a-linear-flow)
3. [Overlay & Modal Patterns](#3-overlay--modal-patterns)
4. [Multi-Flow Prototype](#4-multi-flow-prototype)
5. [Interaction Audit](#5-interaction-audit)
6. [Low-Fidelity to High-Fidelity Progression](#6-low-fidelity-to-high-fidelity-progression)
7. [Animation Selection Guide](#7-animation-selection-guide)

---

## 1. File Setup for Prototyping

Before wiring interactions, ensure boards are named and positioned correctly.

### Board naming convention

```text
/flows/[journey]-start    ← flow entry points
/screens/[screen-name]    ← intermediate screens
/screens/[screen-name]-empty  ← empty state variant
/screens/[screen-name]-error  ← error state variant
overlay/[overlay-name]    ← overlay/modal boards
```

### Discovery prompt

```text
"List all boards on this page. For each board output:
{ name, x, y, width, height, interactionCount }
Flag any boards not following the /flows/ or /screens/ or overlay/ naming convention.
Propose renames. Do not apply until approved."
```

### Positioning check prompt

```text
"Check board layout on this page. Are boards in a left-to-right logical flow order?
Group boards by user journey and identify gaps or orphaned boards.
Describe the proposed layout. Do not move anything yet."
```

---

## 2. Wiring a Linear Flow

Use when: connecting a series of screens in sequence.

### Step 1: Map the flow

```text
"List all boards named /screens/* on this page in left-to-right order.
Output: [ { name, x, interactionCount } ]
I will confirm the intended navigation sequence before wiring."
```

### Step 2: Wire one connection at a time

```javascript
// Example: wire Home → Login with slide animation
const boards = penpotUtils.findShapes((s) => s.type === "board", penpot.root);
const home = boards.find((b) => b.name === "/flows/onboarding-start");
const login = boards.find((b) => b.name === "/screens/login");

if (!home || !login)
  return { error: "Board not found", boards: boards.map((b) => b.name) };

home.addInteraction("click", {
  type: "navigate-to",
  destination: login,
  animation: {
    type: "slide",
    way: "in",
    direction: "left",
    duration: 300,
    easing: "ease-in-out",
  },
});

return { wired: `${home.name} → ${login.name}`, status: "ok" };
```

### Step 3: Wire back navigation

```javascript
const back = penpotUtils.findShapes((s) => s.name === "btn-back", login)[0];
if (!back) return "btn-back not found on login board";

back.addInteraction("click", {
  type: "navigate-to",
  destination: home,
  animation: {
    type: "slide",
    way: "out",
    direction: "left",
    duration: 300,
    easing: "ease-in-out",
  },
});
```

### Step 4: Wire entire sequence programmatically

```javascript
// Wire a sequence: boards must be in order
const sequence = [
  "/screens/screen-1",
  "/screens/screen-2",
  "/screens/screen-3",
  "/screens/screen-4",
];
const boards = penpotUtils.findShapes((s) => s.type === "board", penpot.root);
const ordered = sequence.map((name) => boards.find((b) => b.name === name));

const missing = sequence.filter((_, i) => !ordered[i]);
if (missing.length > 0) return { error: "Missing boards", missing };

const results = [];
// Wire forward only — 5 connections max per call
for (let i = 0; i < Math.min(ordered.length - 1, 5); i++) {
  ordered[i].addInteraction("click", {
    type: "navigate-to",
    destination: ordered[i + 1],
    animation: { type: "dissolve", duration: 250 },
  });
  results.push(`${ordered[i].name} → ${ordered[i + 1].name}`);
}
return { wired: results };
```

---

## 3. Overlay & Modal Patterns

Use when: adding modals, tooltips, drawers, or confirmation dialogs.

### Overlay board setup

```javascript
// Overlay boards should be sized to match their content, not the full screen
// Position doesn't matter on canvas — Penpot positions them relative to trigger at runtime

const sourceBoard = penpotUtils.findShape((s) => s.name === "/screens/settings");
const boards = penpotUtils.findShapes((s) => s.type === "board", penpot.root);
const modal = boards.find((b) => b.name === "overlay/confirm-delete");
const trigger = sourceBoard
  ? penpotUtils.findShapes((s) => s.name === "btn-delete", sourceBoard)[0]
  : null;

if (!modal || !trigger) {
  return {
    skipped: true,
    reason: "Missing overlay board or trigger",
    modal: Boolean(modal),
    trigger: Boolean(trigger),
  };
}

trigger.addInteraction("click", {
  type: "open-overlay",
  destination: modal,
  position: "center", // 'center'|'manual'|'top-left'|'bottom-right'|...
  closeWhenClickOutside: true,
  addBackgroundOverlay: true, // dim background
  animation: { type: "dissolve", duration: 200, easing: "ease-out" },
});
```

### Close button wiring (inside overlay board)

```javascript
const closeBtn = penpotUtils.findShapes(
  (s) => s.name === "btn-close",
  modal,
)[0];
closeBtn.addInteraction("click", { type: "close-overlay" });
```

### Toggle overlay (e.g. tooltip, dropdown)

```javascript
triggerShape.addInteraction("click", {
  type: "toggle-overlay",
  destination: tooltipBoard,
  position: "bottom-left",
  closeWhenClickOutside: true,
  animation: { type: "dissolve", duration: 150 },
});
```

### Drawer (slide-in overlay)

```javascript
triggerShape.addInteraction("click", {
  type: "open-overlay",
  destination: drawerBoard,
  position: "manual",
  manualPositionLocation: { x: 0, y: 0 }, // left edge
  closeWhenClickOutside: true,
  animation: {
    type: "slide",
    way: "in",
    direction: "left",
    duration: 300,
    easing: "ease-out",
  },
});
```

---

## 4. Multi-Flow Prototype

Use when: designing a prototype with multiple independent user journeys.

### Flow entry board naming

```text
/flows/guest-checkout-start
/flows/member-login-start
/flows/onboarding-start
/flows/error-recovery-start
```

### Audit prompt

```text
"List all boards prefixed /flows/ on this page.
For each flow entry, trace the complete connected graph of interactions.
Output: { flowName, steps: [{board, trigger, action, destination}], deadEnds: [] }
Flag any dead ends (boards reachable in the flow with no onward interaction)."
```

### Wire branching flows

```javascript
// Conditional path: login success → dashboard, login fail → error screen
const loginBtn = penpotUtils.findShapes(
  (s) => s.name === "btn-login",
  loginBoard,
)[0];
const dashboard = boards.find((b) => b.name === "/screens/dashboard");
// Note: Penpot MCP can't add conditional logic — wire the happy path first
// Add error state as a separate screen accessible from a secondary trigger

loginBtn.addInteraction("click", {
  type: "navigate-to",
  destination: dashboard,
  animation: { type: "dissolve", duration: 300 },
});

// Wire error state from separate "Show error" trigger
const showErrorBtn = penpotUtils.findShapes(
  (s) => s.name === "btn-show-error",
  loginBoard,
)[0];
const errorScreen = boards.find((b) => b.name === "/screens/login-error");
if (showErrorBtn && errorScreen) {
  showErrorBtn.addInteraction("click", {
    type: "navigate-to",
    destination: errorScreen,
    animation: { type: "dissolve", duration: 200 },
  });
}
```

---

## 5. Interaction Audit

Use when: checking prototype completeness before a user testing session or handoff.

### Full audit

```text
"Run a full interaction audit on this page:
1. List all boards and their interaction count
2. List all boards with 0 interactions (potential dead ends)
3. List all broken interactions (destination board doesn't exist)
4. List all flow entry points (/flows/* boards)
5. Calculate prototype coverage: (boards with interactions / total boards) × 100

Output as JSON with a 'prototypeCoverage' percentage and a 'readyForTesting' boolean
(true if coverage ≥ 80% and 0 broken interactions)."
```

### Remove all interactions (reset)

```javascript
// Use carefully — no undo via MCP
const allBoards = penpotUtils.findShapes(
  (s) => s.type === "board",
  penpot.root,
);
let removed = 0;
// Batch: 5 boards at a time
const processed = allBoards.slice(0, 5);
processed.forEach((board) => {
  (board.interactions || []).forEach((i) => {
    i.remove();
    removed++;
  });
});
return { removedCount: removed, remaining: allBoards.length - processed.length };
```

---

## 6. Low-Fidelity to High-Fidelity Progression

### Lo-fi board creation

```javascript
// Greyscale wireframe boards — no components, just structure
const screens = ["Home", "Search", "Detail", "Cart", "Checkout"];
let x = 0;
screens.forEach((name) => {
  const board = penpot.createBoard();
  board.name = `/wireframes/${name}`;
  board.resize(375, 812);
  board.fills = [{ fillColor: "#F5F5F5", fillOpacity: 1 }];
  penpotUtils.setParentXY(board, x, 0);

  // Add placeholder header bar
  const header = penpot.createRectangle();
  header.name = "header-bar";
  header.resize(375, 56);
  header.fills = [{ fillColor: "#E0E0E0", fillOpacity: 1 }];
  board.appendChild(header);
  penpotUtils.setParentXY(header, 0, 44); // below status bar

  x += 375 + 100;
});
return { created: screens.length };
```

### Hi-fi upgrade prompt

```text
"For each wireframe board on this page:
1. Identify the equivalent hi-fi screen in the design system (match by function, not name)
2. List components from the design system library that should replace each wireframe element
3. Do not make any changes — produce a mapping table:
   { wireframeBoard, hiFiEquivalent, componentsToUse: [], tokensToApply: [] }"
```

---

## 7. Animation Selection Guide

| Transition type                | Recommended animation | Duration | Notes              |
| ------------------------------ | --------------------- | -------- | ------------------ |
| Screen → screen (forward)      | Slide in-left         | 300ms    | Standard nav       |
| Screen → screen (back)         | Slide out-left        | 300ms    | Matches forward    |
| Screen → screen (up hierarchy) | Dissolve              | 250ms    | Dashboard → detail |
| Modal open                     | Dissolve              | 200ms    | Fast, minimal      |
| Drawer open                    | Slide in-left/right   | 300ms    | Match drawer side  |
| Tooltip / popover              | Dissolve              | 150ms    | Very fast          |
| Splash → main                  | Dissolve              | 400ms    | Deliberate         |
| Onboarding step                | Push                  | 300ms    | Conveys sequence   |
| Error / success state          | Dissolve              | 200ms    | No slide needed    |

**Easing guide:**

- `ease-in-out` → smooth, polished; best for screen transitions
- `ease-out` → overlay appears quickly, feels responsive
- `ease-in` → element leaves quickly; use for exit animations
- `linear` → only for loading bars or mechanical animations