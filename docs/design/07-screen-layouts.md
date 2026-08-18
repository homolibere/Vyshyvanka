# Screen Layouts

All screens target 1440×900 desktop resolution. The app has two visual modes: **light mode** for management screens and **dark mode** for the workflow designer.

## Screen Inventory

| Screen | Mode | Background | Layout Pattern |
|--------|------|------------|----------------|
| Login | Light | `#FAFAF9` | Split panel (brand + form) |
| Workflow Browser | Light | `#FAFAF9` | Toolbar + Sidebar + Content |
| Execution History | Light | `#FAFAF9` | Toolbar + Sidebar + Content |
| Packages | Light | `#FAFAF9` | Toolbar + Sidebar + Content |
| Settings | Light | `#FAFAF9` | Toolbar + Sidebar + Content |
| Not Found | Light | `#FAFAF9` | Centered content |
| Designer | Dark | `#1C1917` | Toolbar + Node Palette + Canvas + Config Panel |
| Node Editor | Dark | `#1C1917` | Modal overlay over designer |

## Layout Shell A: Management (Light)

Used by: Workflow Browser, Execution History, Packages, Settings.

```
┌────────────────────────── 1440px ──────────────────────────┐
│ TOOLBAR (56px height, white bg, full width)                 │
│ [brand] ──────────── [search 240×32] ──────────── [avatar] │
├────────────┬───────────────────────────────────────────────┤
│            │                                                │
│  SIDEBAR   │            CONTENT AREA                        │
│  240px     │            1200px                              │
│  white bg  │            transparent bg                      │
│            │                                                │
│  nav items │            (cards, tables, forms)              │
│  224×36    │                                                │
│  2px gap   │                                                │
│            │                                                │
│  844px     │            844px                               │
│            │                                                │
└────────────┴───────────────────────────────────────────────┘
```

### Key Dimensions

| Region | Width | Height | Background |
|--------|-------|--------|------------|
| Toolbar | 1440 | 56 | `#FFFFFF` |
| Sidebar | 240 | 844 | `#FFFFFF` |
| Content | 1200 | 844 | Transparent (page bg shows through) |
| Nav item | 224 | 36 | Transparent / Primary-Light when active |

### Toolbar Internal Layout

- Layout: Flex row, items vertically centered
- Left: Brand text / logo
- Center: Search box (240×32, Neutral-100 bg, radius-md)
- Right: User avatar (32×32 circle)
- Horizontal padding: 24px from edges

### Sidebar Internal Layout

- Layout: Flex column, 2px gap between items
- Padding: 8px all sides
- Items: 224×36, radius 6px, 12px horizontal padding
- Active item: Primary-Light background, Primary text color
- Inactive item: Transparent background, Neutral-700 text

## Layout Shell B: Designer (Dark)

Used by: Designer screen.

```
┌────────────────────────── 1440px ──────────────────────────┐
│ TOOLBAR (48px height, #292524 bg, full width)               │
│ [← back] [workflow name] ─────── [execute] [save]          │
├──────────┬──────────────────────────────────┬──────────────┤
│          │                                   │              │
│  NODE    │         CANVAS                    │   CONFIG     │
│  PALETTE │         (dark, infinite pan/zoom) │   PANEL      │
│  240px   │         960×852                   │   300px      │
│  #292524 │         #1C1917                   │   #292524    │
│          │                                   │              │
│  node    │         [nodes + connections]     │   [selected  │
│  list    │                                   │    node      │
│  items   │                                   │    props]    │
│          │                                   │              │
│  852px   │                                   │   852px      │
│          │                                   │              │
└──────────┴──────────────────────────────────┴──────────────┘
```

### Key Dimensions

| Region | Width | Height | Background | Position |
|--------|-------|--------|------------|----------|
| Toolbar | 1440 | 48 | `#292524` | Top, full width |
| Node Palette | 240 | 852 | `#292524` | Left, below toolbar |
| Canvas | 960 | 852 | `#1C1917` | Center |
| Config Panel | 300 | 852 | `#292524` | Right, below toolbar |

### Node Palette

- Layout: Flex column, 4px gap
- Contains categorized list of available node types
- Each item: Node name + category color indicator
- Draggable onto canvas

### Config Panel

- Layout: Flex column, 16px gap
- Shows properties of the selected node
- Header: Node name + category badge
- Body: Form fields for node configuration
- Appears only when a node is selected

## Login Screen

```
┌────────────────────────── 1440px ──────────────────────────┐
│                                                             │
│  ┌─────────────────────┬──────────────────────────────┐    │
│  │                      │                               │    │
│  │   BRAND PANEL        │        FORM PANEL             │    │
│  │   (left half)        │        (right half)           │    │
│  │                      │                               │    │
│  │   Flex column        │        Flex column            │    │
│  │   16px gap           │        24px gap               │    │
│  │   centered content   │        centered content       │    │
│  │                      │                               │    │
│  │   - Logo             │        - Title (H2)           │    │
│  │   - Tagline          │        - Email input          │    │
│  │   - Brand image      │        - Password input       │    │
│  │                      │        - Login button         │    │
│  │                      │        - Forgot password link │    │
│  │                      │                               │    │
│  └─────────────────────┴──────────────────────────────┘    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

- Background: `#FAFAF9`
- Brand panel: Visual/decorative left side
- Form panel: Interactive right side, flex column with 24px spacing

## Node Editor Modal

Overlaid on top of the designer screen for detailed node configuration.

```
┌────────────────────────── 1440px ──────────────────────────┐
│  ┌───────────────── BACKDROP (50% black) ────────────────┐ │
│  │                                                        │ │
│  │   ┌─────────── MODAL (1200×780) ───────────────┐      │ │
│  │   │  white bg, radius 12px                      │      │ │
│  │   │  shadow: 0 20px 60px -12px rgba(0,0,0,0.3) │      │ │
│  │   │                                             │      │ │
│  │   │  Positioned: 120px from left, 60px from top │      │ │
│  │   │                                             │      │ │
│  │   │  [Node editor content — tabbed interface]   │      │ │
│  │   │                                             │      │ │
│  │   └─────────────────────────────────────────────┘      │ │
│  │                                                        │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

| Property | Value |
|----------|-------|
| Backdrop | Full screen, `rgba(0, 0, 0, 0.5)` |
| Modal size | 1200×780 |
| Modal offset | 120px left, 60px top (centered with slight upward bias) |
| Modal bg | `#FFFFFF` |
| Modal radius | 12px (`radius.xl`) |
| Modal shadow | `0 20px 60px -12px rgba(0, 0, 0, 0.3)` |

## Not Found Screen

Simple centered layout for 404/error states.

| Property | Value |
|----------|-------|
| Background | `#FAFAF9` |
| Content | Centered vertically and horizontally |
| Headline | Large text (Display or H1) |
| Message | Body text, Neutral-500 |
| Action | Flex row of buttons (e.g., "Go Home") |

## Responsive Considerations

The current design targets 1440px fixed. When implementing responsive behavior:

- **Sidebar:** Collapsible below 1280px (icon-only mode at 64px width)
- **Config Panel (designer):** Collapsible, slides out from right
- **Node Palette:** Collapsible, icon-only mode available
- **Content area:** Fluid, fills remaining space
- **Minimum supported width:** 1024px (below this, sidebar collapses automatically)
- **Cards grid:** Responsive columns — 3 at 1440px, 2 at 1024px, 1 below 768px

## Z-Index Layers

| Layer | Z-Index | Elements |
|-------|---------|----------|
| Base | 0 | Content area, cards |
| Sidebar | 10 | Fixed sidebar |
| Toolbar | 20 | Fixed toolbar |
| Canvas nodes | 30 | Node cards on canvas |
| Dropdown/Popover | 100 | Select dropdowns, tooltips |
| Modal backdrop | 200 | Dark overlay |
| Modal | 210 | Dialog/modal content |
| Toast | 300 | Notification toasts |
