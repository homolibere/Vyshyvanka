# Requirements Document

## Introduction

FlowForge is a workflow automation platform built on .NET 10, inspired by n8n. It enables users to create, manage, and execute automated workflows through a visual node-based interface. The platform supports connecting various services, transforming data, and automating business processes with minimal code.

## Glossary

- **Workflow**: A collection of connected nodes that define an automated process
- **Node**: A building block that performs a specific action within a workflow
- **Trigger_Node**: A node that initiates workflow execution based on events or schedules
- **Action_Node**: A node that performs operations like HTTP requests, database queries, or service integrations
- **Logic_Node**: A node that controls workflow flow (conditionals, loops, merges)
- **Connection**: A link between two nodes that defines data flow
- **Execution**: A single run of a workflow
- **Credential**: Encrypted authentication data for external services
- **Workflow_Engine**: The component responsible for executing workflows
- **Designer**: The visual interface for creating and editing workflows
- **Node_Registry**: The catalog of available node types
- **Execution_Context**: Runtime state and data during workflow execution

## Requirements

### Requirement 1: Workflow Definition

**User Story:** As a developer, I want to define workflows as structured data, so that I can create, store, and version control my automation processes.

#### Acceptance Criteria

1. THE Workflow_Engine SHALL represent workflows as JSON documents containing nodes, connections, and metadata
2. WHEN a workflow is created, THE System SHALL assign a unique identifier and version number
3. THE Workflow_Engine SHALL support workflow metadata including name, description, tags, and creation timestamp
4. WHEN a workflow JSON is loaded, THE Workflow_Engine SHALL validate it against the workflow schema
5. IF a workflow definition is invalid, THEN THE Workflow_Engine SHALL return descriptive validation errors
6. THE Pretty_Printer SHALL format Workflow objects back into valid JSON documents
7. FOR ALL valid Workflow objects, parsing then printing then parsing SHALL produce an equivalent object (round-trip property)

### Requirement 2: Node System

**User Story:** As a workflow designer, I want a variety of node types, so that I can build workflows for different automation scenarios.

#### Acceptance Criteria

1. THE Node_Registry SHALL support three primary node categories: Trigger_Nodes, Action_Nodes, and Logic_Nodes
2. WHEN a node is added to a workflow, THE Designer SHALL validate that required configuration properties are provided
3. THE System SHALL support the following core Trigger_Nodes: Manual Trigger, Webhook Trigger, Schedule Trigger, and Event Trigger
4. THE System SHALL support the following core Action_Nodes: HTTP Request, Database Query, Email Send, and File Operations
5. THE System SHALL support the following core Logic_Nodes: If/Switch, Merge, Loop, and Wait
6. WHEN a custom node is registered, THE Node_Registry SHALL validate the node definition and make it available in the Designer
7. THE Node_Registry SHALL provide node metadata including name, description, inputs, outputs, and configuration schema

### Requirement 3: Workflow Execution Engine

**User Story:** As a system operator, I want workflows to execute reliably, so that my automated processes run consistently.

#### Acceptance Criteria

1. WHEN a workflow is triggered, THE Workflow_Engine SHALL create an Execution_Context and begin processing from the trigger node
2. THE Workflow_Engine SHALL execute nodes in topological order respecting connection dependencies
3. WHEN a node completes execution, THE Workflow_Engine SHALL pass output data to all connected downstream nodes
4. IF a node execution fails, THEN THE Workflow_Engine SHALL capture the error and execute configured error handling
5. THE Workflow_Engine SHALL support parallel execution of independent branches
6. WHEN an execution completes, THE Workflow_Engine SHALL persist the execution result with status, duration, and output data
7. THE Workflow_Engine SHALL support both synchronous (short-running) and asynchronous (long-running) workflow execution modes
8. WHILE a workflow is executing, THE Workflow_Engine SHALL track execution progress and allow status queries

### Requirement 4: Visual Designer

**User Story:** As a workflow designer, I want a visual drag-and-drop interface, so that I can create workflows without writing code.

#### Acceptance Criteria

1. THE Designer SHALL display a canvas where users can place and connect nodes
2. WHEN a user drags a node from the palette, THE Designer SHALL add the node to the canvas at the drop location
3. WHEN a user draws a connection between node ports, THE Designer SHALL validate port compatibility before creating the connection
4. THE Designer SHALL provide real-time validation feedback for incomplete or invalid workflow configurations
5. WHEN a workflow is saved, THE Designer SHALL serialize the canvas state to a valid workflow JSON document
6. THE Designer SHALL support undo/redo operations for all canvas modifications
7. THE Designer SHALL display node configuration panels when a node is selected
8. WHEN the user executes a workflow from the Designer, THE Designer SHALL display execution progress and results in real-time

### Requirement 5: Data Flow and Transformation

**User Story:** As a workflow designer, I want to transform and map data between nodes, so that I can integrate services with different data formats.

#### Acceptance Criteria

1. THE Workflow_Engine SHALL pass data between nodes as JSON objects
2. WHEN configuring a node input, THE Designer SHALL allow users to reference output data from previous nodes using expressions
3. THE System SHALL support expression syntax for accessing nested properties, array elements, and applying transformations
4. THE Workflow_Engine SHALL evaluate expressions at runtime within the Execution_Context
5. IF an expression evaluation fails, THEN THE Workflow_Engine SHALL report the error with expression location and context
6. THE System SHALL provide built-in transformation functions for common operations (string manipulation, date formatting, math operations)

### Requirement 6: Credentials Management

**User Story:** As a workflow designer, I want to securely store authentication credentials, so that I can connect to external services without exposing secrets.

#### Acceptance Criteria

1. THE System SHALL encrypt all stored credentials using AES-256 encryption
2. WHEN a credential is created, THE System SHALL validate the credential type matches the expected schema
3. THE System SHALL support credential types for: API Key, OAuth2, Basic Auth, and Custom Headers
4. WHEN a node requires credentials, THE Workflow_Engine SHALL decrypt and inject credentials at execution time
5. THE System SHALL never expose decrypted credential values in logs, execution history, or API responses
6. WHEN a credential is deleted, THE System SHALL remove all encrypted data and invalidate any cached values

### Requirement 7: Execution History and Debugging

**User Story:** As a workflow operator, I want to view execution history and debug failed workflows, so that I can troubleshoot and improve my automations.

#### Acceptance Criteria

1. THE System SHALL persist execution records including start time, end time, status, and trigger information
2. WHEN viewing an execution, THE System SHALL display the data flow through each node with input and output values
3. THE System SHALL retain execution history according to configurable retention policies
4. WHEN an execution fails, THE System SHALL capture error details including stack trace, node identifier, and input data
5. THE Designer SHALL allow users to replay failed executions with modified input data for debugging
6. THE System SHALL support filtering and searching execution history by workflow, status, date range, and tags

### Requirement 8: API and Webhooks

**User Story:** As a developer, I want to interact with FlowForge programmatically, so that I can integrate it with other systems.

#### Acceptance Criteria

1. THE System SHALL expose a REST API for workflow CRUD operations
2. THE System SHALL expose a REST API for triggering workflow executions
3. THE System SHALL expose a REST API for querying execution status and history
4. WHEN a Webhook Trigger receives an HTTP request, THE Workflow_Engine SHALL start a workflow execution with the request data as input
5. THE API SHALL require authentication using API keys or JWT tokens
6. THE API SHALL return consistent error responses with error codes and descriptive messages
7. THE System SHALL support webhook signature validation for secure integrations

### Requirement 9: User Management and Security

**User Story:** As an administrator, I want to manage users and their permissions, so that I can control access to workflows and sensitive data.

#### Acceptance Criteria

1. THE System SHALL support user authentication via local accounts and external identity providers (OIDC)
2. THE System SHALL implement role-based access control with predefined roles: Admin, Editor, and Viewer
3. WHEN a user attempts an action, THE System SHALL verify the user has the required permission before proceeding
4. IF a user lacks required permissions, THEN THE System SHALL return an authorization error without revealing protected resource details
5. THE System SHALL log all authentication attempts and permission-sensitive operations for audit purposes
6. THE System SHALL support API key generation for programmatic access with configurable scopes

### Requirement 10: Plugin and Extension System

**User Story:** As a developer, I want to create custom nodes and extensions, so that I can extend FlowForge for my specific integration needs.

#### Acceptance Criteria

1. THE System SHALL support loading custom nodes from plugin assemblies at startup
2. WHEN a plugin is loaded, THE Node_Registry SHALL validate the plugin implements required interfaces
3. THE System SHALL provide a plugin SDK with base classes and interfaces for creating custom nodes
4. THE Plugin_System SHALL isolate plugin execution to prevent plugins from affecting core system stability
5. WHEN a plugin throws an unhandled exception, THE Workflow_Engine SHALL catch the exception and fail the node gracefully
6. THE System SHALL support plugin configuration through environment variables or configuration files
