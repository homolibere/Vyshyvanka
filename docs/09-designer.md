# Designer (Visual Workflow Editor)

## Overview

The Vyshyvanka Designer is a Blazor WebAssembly single-page application that provides a visual canvas for designing workflows. It communicates with the API exclusively over HTTP and has no direct dependency on the Engine or persistence layers.

## Application Structure

```mermaid
flowchart TD
    subgraph "Pages"
        Home["Home<br/>Dashboard"]
        Login["Login<br/>Authentication"]
        DesignerPage["Designer<br/>Workflow Canvas"]
        NotFound["NotFound<br/>404"]
    end

    subgraph "Layout"
        MainLayout["MainLayout<br/>App shell"]
        DesignerLayout["DesignerLayout<br/>Canvas layout with panels"]
    end

    subgraph "Canvas Components"
        WorkflowCanvas["WorkflowCanvas<br/>SVG canvas with pan/zoom"]
        CanvasNode["CanvasNodeComponent<br/>Visual node on canvas"]
        ConnectionLine["ConnectionLine<br/>SVG path between ports"]
        NodeIcon["NodeIcon / SvgNodeIcon<br/>Category-based icons"]
    end

    subgraph "Panel Components"
        NodePalette["NodePalette<br/>Draggable node catalog"]
        NodeConfigPanel["NodeConfigPanel<br/>Selected node properties"]
        WorkflowBrowser["WorkflowBrowser<br/>Workflow list & search"]
    end

    subgraph "Editor Components"
        NodeEditorModal["NodeEditorModal<br/>Full node editor"]
        ConfigPanel["NodeEditorConfigPanel<br/>Configuration properties"]
        InputPanel["NodeEditorInputPanel<br/>Input port mapping"]
        OutputPanel["NodeEditorOutputPanel<br/>Output port display"]
    end

    subgraph "Property Editors"
        StringEditor["StringPropertyEditor"]
        NumberEditor["NumberPropertyEditor"]
        BooleanEditor["BooleanPropertyEditor"]
        JsonEditor["JsonPropertyEditor"]
        SelectEditor["SelectPropertyEditor"]
        PropertyEditor["PropertyEditor<br/>Type dispatcher"]
    end

    subgraph "Plugin & Package Components"
        PluginManager["PluginManager<br/>Installed packages"]
        BrowsePackages["BrowsePackages<br/>NuGet search"]
        InstalledPackages["InstalledPackages<br/>Package list"]
        PackageCard["PackageCard<br/>Package summary"]
        PackageDetailsModal["PackageDetailsModal<br/>Version, deps, install"]
        SourceManager["SourceManager<br/>Feed configuration"]
        SourceEditModal["SourceEditModal<br/>Add/edit source"]
    end

    subgraph "Shared Components"
        ConfirmDialog["ConfirmDialog<br/>Confirmation prompts"]
        Toast["Toast / ToastContainer<br/>Notifications"]
    end

    MainLayout --> Home
    MainLayout --> Login
    DesignerLayout --> DesignerPage
    DesignerPage --> WorkflowCanvas
    DesignerPage --> NodePalette
    DesignerPage --> NodeConfigPanel
    WorkflowCanvas --> CanvasNode
    WorkflowCanvas --> ConnectionLine
    CanvasNode --> NodeIcon
    NodeConfigPanel --> NodeEditorModal
    NodeEditorModal --> ConfigPanel
    NodeEditorModal --> InputPanel
    NodeEditorModal --> OutputPanel
    ConfigPanel --> PropertyEditor
    PropertyEditor --> StringEditor
    PropertyEditor --> NumberEditor
    PropertyEditor --> BooleanEditor
    PropertyEditor --> JsonEditor
    PropertyEditor --> SelectEditor
```

## Component Pattern

Every Blazor component follows a strict three-file pattern:

| File | Purpose |
|------|---------|
| `Component.razor` | Markup only. No `@code` blocks. No `<style>` blocks. |
| `Component.razor.cs` | Code-behind as a `partial` class. Uses `[Inject]` for DI. |
| `Component.razor.css` | Scoped CSS styles (optional). |

## Client Services

```mermaid
flowchart LR
    subgraph "Services"
        ApiClient["VyshyvankaApiClient<br/>HTTP calls to API"]
        ApiClientPkg["VyshyvankaApiClient.Packages<br/>Package operations"]
        AuthService["AuthService<br/>Login, Register, Logout"]
        AuthState["AuthStateService<br/>Token storage, auth state"]
        WorkflowState["WorkflowStateService<br/>Canvas state management"]
        PluginState["PluginStateService<br/>Package state"]
        ToastService["ToastService<br/>Notification management"]
        Storage["BrowserStorageService<br/>localStorage wrapper"]
        SchemaParser["ConfigurationSchemaParser<br/>JSON Schema to editors"]
        UrlResolver["ApiUrlResolver<br/>Service discovery URL"]
        AuthHandler["AuthorizationMessageHandler<br/>JWT header injection"]
    end

    AuthService --> AuthState
    AuthState --> Storage
    ApiClient --> AuthHandler
    AuthHandler --> AuthState
    ApiClient --> UrlResolver
```

### Key Services

#### WorkflowStateService

Central state manager for the Designer canvas. Manages:

- Current workflow (nodes, connections, metadata)
- Selected node tracking
- Undo/redo history
- Node add, move, delete, and configure operations
- Connection create and delete operations
- Workflow serialization and deserialization
- Validation state

#### VyshyvankaApiClient

Typed HTTP client for all API communication:

- Workflow CRUD operations
- Execution triggering and history
- Node definition retrieval
- Package search, install, update, uninstall
- Package source management
- Authentication token injection via `AuthorizationMessageHandler`

#### AuthStateService

Manages authentication state in the browser:

- Stores JWT access and refresh tokens in `localStorage`
- Tracks current user information
- Provides authentication state to components
- Handles token refresh on expiry

#### ConfigurationSchemaParser

Parses JSON Schema from node definitions and maps property types to appropriate editor components:

| Schema Type | Editor |
|-------------|--------|
| `string` | StringPropertyEditor |
| `number` / `integer` | NumberPropertyEditor |
| `boolean` | BooleanPropertyEditor |
| `object` | JsonPropertyEditor |
| `string` with `enum` | SelectPropertyEditor |

## Canvas Interaction

The workflow canvas is an SVG-based interactive surface with JavaScript interop for performance-critical operations:

```mermaid
flowchart TD
    subgraph "User Actions"
        Drag["Drag Node<br/>from Palette"]
        Move["Move Node<br/>on Canvas"]
        Connect["Draw Connection<br/>between Ports"]
        Select["Select Node<br/>Click"]
        Pan["Pan Canvas<br/>Mouse drag"]
        Zoom["Zoom Canvas<br/>Scroll wheel"]
        Delete["Delete Node<br/>or Connection"]
        Configure["Configure Node<br/>Double-click"]
    end

    subgraph "State Updates"
        AddNode["WorkflowState<br/>AddNode"]
        MoveNode["WorkflowState<br/>UpdatePosition"]
        AddConn["WorkflowState<br/>AddConnection"]
        SetSelected["WorkflowState<br/>SelectNode"]
        CanvasTransform["JS Interop<br/>Transform matrix"]
        RemoveNode["WorkflowState<br/>RemoveNode"]
        OpenEditor["NodeEditorModal<br/>Open"]
    end

    Drag --> AddNode
    Move --> MoveNode
    Connect --> AddConn
    Select --> SetSelected
    Pan --> CanvasTransform
    Zoom --> CanvasTransform
    Delete --> RemoveNode
    Configure --> OpenEditor
```

The `canvas-interop.js` file handles low-level mouse and touch events, coordinate transformations, and SVG rendering optimizations that would be too slow in Blazor's render cycle.

## Pages

### Home

Dashboard page showing workflow summaries and recent execution activity.

### Login

Authentication page. When the built-in provider is active, shows an email/password form. When Keycloak or Authentik is configured, redirects to the external identity provider's login page. The active provider is determined by calling `GET /api/auth/config` at startup. On successful login, stores tokens and redirects to the Designer.

### Designer

The main workspace containing:
- Workflow canvas (center)
- Node palette (left sidebar)
- Node configuration panel (right sidebar)
- Workflow browser (top bar)
- Plugin manager (accessible from toolbar)

### NotFound

404 page for unmatched routes.

## Client Models

The Designer maintains its own model types that mirror the API DTOs:

| Model File | Contents |
|-----------|----------|
| AuthModels | Login/register request and response types |
| WorkflowApiModels | Workflow, node, and connection DTOs |
| ExecutionApiModels | Execution response and summary types |
| DesignerModels | Canvas-specific state (positions, selections, zoom) |
| NodeEditorModels | Node editor form state |
| PackageApiModels | Package search, install, and source DTOs |
| PluginModels | Plugin state and metadata |
| ApiError | Error response type matching the API format |

These models are independent of the Core project's domain models, maintaining the architectural boundary where the Designer communicates only via HTTP.
